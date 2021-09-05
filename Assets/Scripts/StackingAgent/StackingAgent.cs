using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using VoxSimPlatform.Global;
using VoxSimPlatform.SpatialReasoning.QSR;
using VoxSimPlatform.Vox;

public class StackingAgent : Agent
{
    public GameObject themeObj, destObj;
    public int observationSize;
    public bool useVectorObservations, noisyVectors;
    public bool circumventEventManager;

    public int episodeCount;
    public int episodeMaxActions;
    public int episodeNumActions;

    public float forceMultiplier;

    public bool writeOutSamples;

    public string outFileName;

    public ScenarioController scenarioController;

    List<Transform> usedDestObjs = new List<Transform>();
    List<Transform> interactableObjs;

    CameraSensor cameraSensor;
    VectorSensor sensor;

    protected float[] lastAction;

    Vector3 themeStartRotation;

    int defaultMaxStep;

    float episodeTotalReward;

    DateTime episodeBeginTime,episodeEndTime;
    DateTime trialBeginTime, trialEndTime;

    bool _objectsPlaced = false;
    public bool objectsPlaced
    {
        get { return _objectsPlaced; }
        set
        {
            if (_objectsPlaced != value)
            {
                OnObjectsPlacedChanged(_objectsPlaced, value);
            }
            _objectsPlaced = value;
        }
    }

    bool _waitingForAction = false;
    public bool waitingForAction
    {
        get { return _waitingForAction; }
        set
        {
            if (_waitingForAction != value)
            {
                OnWaitingForActionChanged(_waitingForAction, value);
            }
            _waitingForAction = value;
        }
    }

    bool _episodeStarted = false;
    public bool episodeStarted
    {
        get { return _episodeStarted; }
        set
        {
            if (_episodeStarted != value)
            {
                OnEpisodeStartedChanged(_episodeStarted, value);
            }
            _episodeStarted = value;
        }
    }

    bool _running = false;
    public bool running
    {
        get { return _running; }
        set
        {
            if (_running != value)
            {
                OnRunningChanged(_running, value);
            }
            _running = value;
        }
    }

    bool _executingEvent = false;
    public bool executingEvent
    {
        get { return _executingEvent; }
        set
        {
            if (_executingEvent != value)
            {
                OnExecutingEventChanged(_executingEvent, value);
            }
            _executingEvent = value;
        }
    }

    bool _resolvePhysics = false;
    public bool resolvePhysics
    {
        get { return _resolvePhysics; }
        set
        {
            if (_resolvePhysics != value)
            {
                OnResolvePhysicsChanged(_resolvePhysics, value);
            }
            _resolvePhysics = value;
        }
    }

    bool _constructObservation = false;
    public bool constructObservation
    {
        get { return _constructObservation; }
        set
        {
            if (_constructObservation != value)
            {
                OnConstructObservationChanged(_constructObservation, value);
            }
            _constructObservation = value;
        }
    }

    bool _endEpisode = false;
    public bool endEpisode
    {
        get { return _endEpisode; }
        set
        {
            if (_endEpisode != value)
            {
                OnEndEpisodeChanged(_endEpisode, value);
            }
            _endEpisode = value;
        }
    }

    float observation, lastObservation, noisyObservation;

    void Start()
    {
        if (useVectorObservations)
        {
            sensor = new VectorSensor(observationSize);
        }
        else
        {
            sensor = new VectorSensor(0);
            GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize = 0;
        }

        lastAction = new float[] { -Mathf.Infinity, -Mathf.Infinity };

        episodeCount = 0;

        if (scenarioController != null)
        {
            scenarioController.usingRLClient = true;
            scenarioController.squareFOV = GetComponent<CameraSensorComponent>() != null;
            scenarioController.ObjectsInited += ObjectsPlaced;
            scenarioController.EventExecuting += ExecutingEvent;
            scenarioController.EventCompleted += ApplyForce;
            scenarioController.PostEventWaitCompleted += MakeDecisionRequest;
            scenarioController.PostEventWaitCompleted += ResultObserved;
        }
        else
        {
            Debug.LogWarning("StackingAgent.Start: scenarioController is null!");
        }

        Time.timeScale = scenarioController.timeScale;
        defaultMaxStep = MaxStep;
    }

    void Update()
    {
        running = objectsPlaced && episodeStarted;

        if (running && waitingForAction && !resolvePhysics && !endEpisode)
        {
            MaxStep = (int)Time.timeScale * defaultMaxStep;
            RequestDecision();
        }

        if (circumventEventManager)
        {
            if (themeObj != null)
            {
                // if we're not using the event manager, we have to track the theme object ourselves
                if ((themeObj.GetComponent<Voxeme>().transform.position - themeObj.GetComponent<Voxeme>().targetPosition).sqrMagnitude < Constants.EPSILON)
                {
                    if (executingEvent && !scenarioController.postEventWaitTimer.Enabled)
                    {
                        scenarioController.OnEventCompleted(null, null);
                        // start the wait timer
                        scenarioController.postEventWaitTimer.Enabled = true;
                    }
                }
            }
        }

        if (constructObservation)
        {
            observation = ConstructObservation();
            noisyObservation = observation + (float)GaussianNoise(0, 0.1f);
            Debug.LogFormat("StackingAgent.Update: Observation = {0}; Last observation = {1}; Reward = {2}", observation, lastObservation, observation - lastObservation);
            float reward = (observation - lastObservation) > 0 ? (observation - lastObservation) : (observation - lastObservation) - 1;
            AddReward(reward);
            episodeTotalReward += reward;
            WriteOutSample(themeObj.transform, destObj.transform, lastAction, reward);

            if (observation - lastObservation != 0)
            {
                if (observation - lastObservation < 0)
                {
                    usedDestObjs = usedDestObjs.Take(usedDestObjs.Count-1 + (int)(observation - lastObservation)).ToList();
                    Debug.LogFormat("StackingAgent.Update: usedDestObjs = [{0}]", string.Join(", ",
                        usedDestObjs.Select(o => o.name)));
                }

                // figure out which is the new destination object
                List<Transform> sortedByHeight = interactableObjs.Where(t => SurfaceClear(t.gameObject)).OrderByDescending(t => t.position.y).ToList();
                Debug.LogFormat("StackingAgent.Update: object sequence = {0}", string.Format("[{0}]", string.Join(", ",
                    sortedByHeight.Select(t => string.Format("{{{0}:{1}}}", t.name, t.transform.position.y)))));

                GameObject newDest = sortedByHeight.First().gameObject;

                OnDestObjChanged(destObj, newDest);
                destObj = newDest;

                if (!usedDestObjs.Contains(destObj.transform))
                { 
                    usedDestObjs.Add(destObj.transform);
                }
            }

            if (observation == interactableObjs.Count)
            {
                Debug.LogFormat("StackingAgent.Update: observation = {0} (interactableObjs.Count = {1})", observation, interactableObjs.Count);
                endEpisode = true;
            }
            else if (episodeNumActions >= episodeMaxActions)
            {
                endEpisode = true;
            }

            constructObservation = false;
        }

        if (resolvePhysics)
        {
            Debug.Log("StackingAgent.Update: resolving physics");
            PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);
            resolvePhysics = false;
            constructObservation = true;
        }

        if (endEpisode)
        {
            objectsPlaced = false;
            waitingForAction = false;
            episodeStarted = false;
            running = false;
            executingEvent = false;
            resolvePhysics = false;
            constructObservation = false;

            if (useVectorObservations)
            {
                sensor.AddObservation(noisyVectors ? noisyObservation : observation);
                Debug.LogFormat("StackingAgent.Update: Collecting {0} observation(s) - [{1}]",
                    sensor.ObservationSize(), noisyVectors ? noisyObservation : observation);
                CollectObservations(sensor);
            }

            EndEpisode();
            episodeEndTime = DateTime.Now;

            Debug.LogFormat("StackingAgent.Update: Episode {0} - Episode took {1}-{2} = {3} seconds",
                episodeCount, episodeEndTime.ToString("hh:mm:ss.fffffff"), episodeBeginTime.ToString("hh:mm:ss.fffffff"), (episodeEndTime - episodeBeginTime).TotalSeconds);
        }
    }

    void WriteOutSample(Transform themeTransform, Transform destTransform, float[] action, float reward)
    {
        if (!writeOutSamples)
        {
            return;
        }

        Dictionary<string, int> objNameToIntDict = new Dictionary<string, int>()
        {
            { "Cube", 0 },
            { "Sphere", 1 },
            { "Cylinder", 2 },
            { "Capsule", 3 }
        };

        Vector3 themeEndRotation = new Vector3(themeTransform.eulerAngles.x > 180.0f ? themeTransform.eulerAngles.x - 360.0f : themeTransform.eulerAngles.x,
            themeTransform.eulerAngles.y > 180.0f ? themeTransform.eulerAngles.y - 360.0f : themeTransform.eulerAngles.y,
            themeTransform.eulerAngles.z > 180.0f ? themeTransform.eulerAngles.z - 360.0f : themeTransform.eulerAngles.z);
        float angleOffsetStart = Vector3.Angle(Vector3.up, Quaternion.Euler(themeStartRotation) * Vector3.up);
        float angleOffsetEnd = Vector3.Angle(Vector3.up, themeTransform.up);

        float[] arr = new float[] {
            episodeCount,
            objNameToIntDict[themeTransform.name.Split(new char[]{ '0','1','2','3','4','5','6','7','8','9' })[0]],
            objNameToIntDict[destTransform.name.Split(new char[]{ '0','1','2','3','4','5','6','7','8','9' })[0]],
            themeStartRotation.x * Mathf.Deg2Rad, themeStartRotation.y * Mathf.Deg2Rad, themeStartRotation.z * Mathf.Deg2Rad,
            angleOffsetStart * Mathf.Deg2Rad,
            action[0], action[1],
            themeEndRotation.x * Mathf.Deg2Rad, themeEndRotation.y * Mathf.Deg2Rad, themeEndRotation.z * Mathf.Deg2Rad,
            angleOffsetEnd * Mathf.Deg2Rad,
            noisyVectors ? noisyObservation : observation,
            reward,
            episodeTotalReward,
            episodeTotalReward/episodeNumActions
            };
        string csv = string.Join(",", arr);
        Debug.LogFormat("WriteOutSample: {0}", csv);

        if (outFileName != string.Empty)
        {
            if (!outFileName.EndsWith(".csv"))
            {
                outFileName = string.Format("{0}.csv", outFileName);
            }
            string dirPath = new DirectoryInfo(outFileName).Name;

            if (!Directory.Exists(dirPath))
            {
                DirectoryInfo dirInfo = Directory.CreateDirectory(dirPath);
            }

            using (StreamWriter writer = new StreamWriter(outFileName, true))
            {
                writer.WriteLine(csv);
            }
        }
    }

    public void ObjectsPlaced(object sender, EventArgs e)
    {
        interactableObjs = scenarioController.interactableObjects.
            GetComponentsInChildren<Voxeme>().Where(v => v.isActiveAndEnabled).Select(v => v.transform).ToList();

        usedDestObjs.Clear();
        Debug.LogFormat("StackingAgent.ObjectsPlaced: usedDestObjs = [{0}]", string.Join(", ",
            usedDestObjs.Select(o => o.name)));

        Debug.LogFormat("StackingAgent.ObjectsPlaced: Objects placed: [{0}]",
            string.Join(",\n\t",
                interactableObjs.Select(t => string.Format("{{{0}:{1}}}", t.name, GlobalHelper.VectorToParsable(t.position))).ToArray()));

        List<Transform> sortedByHeight = interactableObjs.Where(t => SurfaceClear(t.gameObject)).OrderByDescending(t => t.position.y).ToList();
        Debug.LogFormat("StackingAgent.ObjectsPlaced: object sequence = {0}", string.Format("[{0}]", string.Join(", ",
                    sortedByHeight.Select(t => string.Format("{{{0}:{1}}}", t.name, t.transform.position.y)))));

        GameObject newDest = destObj == null ? null : destObj;

        newDest = sortedByHeight.First().gameObject;

        OnDestObjChanged(destObj, newDest);
        destObj = newDest;

        if (!usedDestObjs.Contains(destObj.transform))
        {
            usedDestObjs.Add(destObj.transform);
        }

        if (destObj != null)
        {
            Debug.LogFormat("StackingAgent.ObjectsPlaced: Setting destination object: {0}", destObj.name);
        }

        if (!objectsPlaced)
        {
            objectsPlaced = true;
        }
    }

    public void ExecutingEvent(object sender, EventArgs e)
    {
        if (!executingEvent)
        {
            executingEvent = true;
        }
    }

    public void ApplyForce(object sender, EventArgs e)
    {
        // Fetch the Rigidbody from the GameObject
        Rigidbody themeRigidbody = themeObj.GetComponentInChildren<Rigidbody>();
        Vector3 force = Vector3.zero;

        Voxeme themeVox = themeObj.GetComponent<Voxeme>();

        if (themeVox != null)
        {
            string[] axes = themeVox.voxml.Type.RotatSym.Split(',');
             
            if ((axes.Length == 0) || (axes.Length == 3))
            {
                force = new Vector3((float)GaussianNoise(0, 1), 0,
                    (float)GaussianNoise(0, 1)).normalized * forceMultiplier;
            }
            else if (axes.Length == 2)
            {
                Vector3 dir = Vector3.zero;
                string axis = axes[RandomHelper.RandomInt(0, 1, (int)RandomHelper.RangeFlags.MaxInclusive)];
                switch (axis)
                {
                    case "X":
                        dir = themeObj.transform.rotation * Vector3.right;
                        break;

                    case "Y":
                        dir = themeObj.transform.rotation * Vector3.up;
                        break;

                    case "Z":
                        dir = themeObj.transform.rotation * Vector3.forward;
                        break;

                    default:
                        break;
                }

                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;

                if (perp.magnitude < Constants.EPSILON)
                {
                    perp = Vector3.forward;
                }

                force = perp * forceMultiplier;
            }
            else if (axes.Length == 1)
            {
                Vector3 dir = Vector3.zero;
                string axis = axes[0];
                switch (axis)
                {
                    case "X":
                        dir = themeObj.transform.rotation * Vector3.right;
                        break;

                    case "Y":
                        dir = themeObj.transform.rotation * Vector3.up;
                        break;

                    case "Z":
                        dir = themeObj.transform.rotation * Vector3.forward;
                        break;

                    default:
                        break;
                }

                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;

                if (perp.magnitude < Constants.EPSILON)
                {
                    perp = Vector3.forward;
                }

                force = perp * forceMultiplier;
            }
        }
        else
        {
            force = new Vector3((float)GaussianNoise(0, 1),
                0,
                (float)GaussianNoise(0, 1)).normalized * forceMultiplier;
        }

        Debug.LogFormat("ApplyForce: Applying force {0} to GameObject {1} (Rigidbody {2})",
            GlobalHelper.VectorToParsable(force), themeObj.name, themeRigidbody.name);

        themeRigidbody.AddForce(force, ForceMode.Impulse);
    }

    public void MakeDecisionRequest(object sender, EventArgs e)
    {
        waitingForAction = true;
    }

    public void ResultObserved(object sender, EventArgs e)
    {
        if (executingEvent)
        {
            lastObservation = observation;
            resolvePhysics = true;
            executingEvent = false;

            trialEndTime = DateTime.Now;
            Debug.LogFormat("StackingAgent.ResultObserved: Episode {0} - Trial took {1}-{2} = {3} seconds",
                episodeCount, trialEndTime.ToString("hh:mm:ss.fffffff"), trialBeginTime.ToString("hh:mm:ss.fffffff"), (trialEndTime - trialBeginTime).TotalSeconds);
        }
    }

    protected GameObject SelectThemeObject()
    {
        GameObject theme = null;

        Debug.LogFormat("StackingAgent.SelectThemeObject: usedDestObjs = [{0}]", string.Join(", ",
            usedDestObjs.Select(o => o.name)));

        List<Transform> sortedByHeight = interactableObjs.Except(usedDestObjs).Where(t => SurfaceClear(t.gameObject))
             .OrderBy(t => t.position.y).ToList();
        Debug.LogFormat("StackingAgent.SelectThemeObject: object sequence = {0}", string.Format("[{0}]", string.Join(",",
            sortedByHeight.Select(t => string.Format("({0}, {1})", t.name, t.transform.position.y)))));

        theme = sortedByHeight.First().gameObject;

        themeStartRotation = new Vector3(theme.transform.eulerAngles.x > 180.0f ? theme.transform.eulerAngles.x - 360.0f : theme.transform.eulerAngles.x,
            theme.transform.eulerAngles.y > 180.0f ? theme.transform.eulerAngles.y - 360.0f : theme.transform.eulerAngles.y,
            theme.transform.eulerAngles.z > 180.0f ? theme.transform.eulerAngles.z - 360.0f : theme.transform.eulerAngles.z);

        return theme;
    }

    protected int ConstructObservation()
    {
        // sort objects by height
        List<Transform> sortedByHeight = interactableObjs.OrderByDescending(t => t.position.y).ToList();
        Debug.LogFormat("StackingAgent.ConstructObservation: [{0}]", string.Join(",", sortedByHeight.Select(o => o.position.y).ToList()));

        // take the topmost object and round its y-coord up to nearest int
        //  multiply by 10 (blocks are .1 x .1 x .1)
        int obs = (int)Mathf.Ceil(sortedByHeight.First().transform.position.y * 10);

        return obs;
    }

    protected bool SurfaceClear(GameObject obj)
    {
        bool surfaceClear = true;
        List<GameObject> excludeChildren = obj.GetComponentsInChildren<Renderer>().Where(
            o => (GlobalHelper.GetMostImmediateParentVoxeme(o.gameObject) != obj)).Select(o => o.gameObject).ToList();

        Bounds objBounds = GlobalHelper.GetObjectWorldSize(obj, excludeChildren);
        foreach (Transform otherObj in obj.transform)
        {
            if (otherObj.tag != "UnPhysic")
            {
                excludeChildren = otherObj.GetComponentsInChildren<Renderer>().Where(
                    o => (GlobalHelper.GetMostImmediateParentVoxeme(o.gameObject) != otherObj.gameObject)).Select(o => o.gameObject).ToList();

                Bounds otherBounds = GlobalHelper.GetObjectWorldSize(otherObj.gameObject, excludeChildren);
                Region blockMax = new Region(new Vector3(objBounds.min.x, objBounds.max.y, objBounds.min.z),
                    new Vector3(objBounds.max.x, objBounds.max.y, objBounds.max.z));
                Region otherMin = new Region(new Vector3(otherBounds.min.x, objBounds.max.y, otherBounds.min.z),
                    new Vector3(otherBounds.max.x, objBounds.max.y, otherBounds.max.z));
                if ((QSR.Above(otherBounds, objBounds)) &&
                    ((GlobalHelper.RegionOfIntersection(blockMax, otherMin, Constants.MajorAxis.Y).Area() / blockMax.Area()) >
                     0.25f) &&
                    (RCC8.EC(otherBounds, objBounds)))
                {
                    surfaceClear = false;
                    break;
                }
            }
        }

        Debug.Log(string.Format("SurfaceClear({0}):{1}", obj.name, surfaceClear));
        return surfaceClear;
    }

    public override void OnEpisodeBegin()
    {
        if (endEpisode)
        {
            endEpisode = false;
            return;
        }
        // clear the event manager in case any actions have been
        //  received from the RL client while we're in the middle
        //  of a reset
        // clear the executingEvent flag so the next action received
        //  will actually be processed
        scenarioController.ClearEventManager();
        executingEvent = false;

        if (interactableObjs != null)
        {
            List<Rigidbody> allRigidbodies = (from list in interactableObjs.Select(go => go.GetComponentsInChildren<Rigidbody>())
                                              from item in list
                                              select item).ToList();
            foreach (Rigidbody rb in allRigidbodies)
            {
                rb.velocity = Vector3.zero;
            }
        }

        episodeCount += 1;
        episodeNumActions = 0;

        Debug.LogFormat("StackingAgent.OnEpisodeBegin: Beginning episode {0}", episodeCount);
        episodeTotalReward = 0f;

        if (useVectorObservations)
        {
            observation = 1;
            noisyObservation = observation + (float)GaussianNoise(0, 0.1f);
            sensor.AddObservation(noisyVectors ? noisyObservation : observation);
            Debug.LogFormat("StackingAgent.OnEpisodeBegin: Collecting {0} observation(s) - [{1}]",
                sensor.ObservationSize(), noisyVectors ? noisyObservation : observation);
        }

        scenarioController.PlaceRandomly(scenarioController.surface);
        PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);

        episodeStarted = true;
        episodeBeginTime = DateTime.Now;

        Debug.LogFormat("StackingAgent.OnEpisodeBegin: Episode {0} - Resetting took {1}-{2} = {3} seconds",
            episodeCount, episodeBeginTime.ToString("hh:mm:ss.fffffff"), episodeEndTime.ToString("hh:mm:ss.fffffff"), (episodeBeginTime - episodeEndTime).TotalSeconds);

        if (episodeCount <= 2)
        {
            // give the Python client time to connect and initialize
            scenarioController.postEventWaitTimer.Interval = scenarioController.postEventWaitTimerTime * scenarioController.timeScale;
        }
        else
        {
            scenarioController.postEventWaitTimer.Interval = 1;
        }
        scenarioController.postEventWaitTimer.Enabled = true;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (running)
        {
            if (!executingEvent)
            {
                if (useVectorObservations)
                {
                    sensor.AddObservation(noisyVectors ? noisyObservation : observation);
                    Debug.LogFormat("StackingAgent.CollectObservations: Collecting {0} observation(s) - [{1}]",
                        sensor.ObservationSize(), noisyVectors ? noisyObservation : observation);
                }
            }
            else
            {
                if (useVectorObservations)
                {
                    sensor.AddObservation(lastObservation);
                    Debug.LogFormat("StackingAgent.CollectObservations: Collecting {0} observation(s) - [{1}]",
                        sensor.ObservationSize(), lastObservation);
                }
            }

            waitingForAction = true;
        }
        else
        {
            if (useVectorObservations)
            {
                // if the episode has terminated, return the last observation
                //  (i.e., the observation at the final state)
                sensor.AddObservation(noisyVectors ? noisyObservation : observation);
                Debug.LogFormat("StackingAgent.CollectObservations: Collecting {0} observation(s) - [{1}]",
                    sensor.ObservationSize(), noisyVectors ? noisyObservation : observation);
            }
        }
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        if (base.StepCount == 0)
        {
            return;
        }

        trialBeginTime = DateTime.Now;
    }

    public override void Heuristic(float[] actionsOut)
    {

    }

    /// <summary>
    /// Triggered when the themeObj flag changes
    /// </summary>
    // IN: oldVal -- previous value of themeObj
    //      newVal -- new or current value of themeObj
    protected void OnThemeObjChanged(GameObject oldVal, GameObject newVal)
    {
        Debug.Log(string.Format("==================== themeObj changed ==================== {0}->{1}",
            oldVal == null ? "NULL" : oldVal.name, newVal == null ? "NULL" : newVal.name));
    }

    /// <summary>
    /// Triggered when the destObj flag changes
    /// </summary>
    // IN: oldVal -- previous value of destObj
    //      newVal -- new or current value of destObj
    protected void OnDestObjChanged(GameObject oldVal, GameObject newVal)
    {
        Debug.Log(string.Format("==================== destObj changed ==================== {0}->{1}",
            oldVal == null ? "NULL" : oldVal.name, newVal == null ? "NULL" : newVal.name));
    }

    /// <summary>
    /// Triggered when the objectsPlaced flag changes
    /// </summary>
    // IN: oldVal -- previous value of objectsPlaced
    //      newVal -- new or current value of objectsPlaced
    protected void OnObjectsPlacedChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== objectsPlaced flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the waitingForAction flag changes
    /// </summary>
    // IN: oldVal -- previous value of waitingForAction
    //      newVal -- new or current value of waitingForAction
    protected void OnWaitingForActionChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== waitingForAction flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the episodeStarted flag changes
    /// </summary>
    // IN: oldVal -- previous value of episodeStarted
    //      newVal -- new or current value of episodeStarted
    protected void OnEpisodeStartedChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== episodeStarted flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the running flag changes
    /// </summary>
    // IN: oldVal -- previous value of running
    //      newVal -- new or current value of running
    protected void OnRunningChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== running flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the executingEvent flag changes
    /// </summary>
    // IN: oldVal -- previous value of executingEvent
    //      newVal -- new or current value of executingEvent
    protected void OnExecutingEventChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== executingEvent flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the resolvePhysics flag changes
    /// </summary>
    // IN: oldVal -- previous value of resolvePhysics
    //      newVal -- new or current value of resolvePhysics
    protected void OnResolvePhysicsChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== resolvePhysics flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the constructObservation flag changes
    /// </summary>
    // IN: oldVal -- previous value of constructObservation
    //      newVal -- new or current value of constructObservation
    protected void OnConstructObservationChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== constructObservation flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the endEpisode flag changes
    /// </summary>
    // IN: oldVal -- previous value of endEpisode
    //      newVal -- new or current value of endEpisode
    protected void OnEndEpisodeChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== endEpisode flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    protected double GaussianNoise(float mean, float stdDev)
    {
        System.Random rand = new System.Random(); //reuse this if you are generating many
        double u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
        double u2 = 1.0 - rand.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                     Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
        double randNormal =
                     mean + stdDev * randStdNormal; //random normal(mean,stdDev^2)

        Debug.LogFormat("Gaussian noise: {0}", randNormal);

        return randNormal;
    }
}
