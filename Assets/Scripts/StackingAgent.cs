using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;
using VoxSimPlatform.Global;
using VoxSimPlatform.Vox;

public class StackingAgent : Agent
{
    public GameObject themeObj, destObj;
    public int observationSize;
    public bool useNoisyObservations;

    public int episodeCount;

    public float forceMultiplier;

    public string outFileName;

    public ScenarioController scenarioController;

    List<Transform> usedDestObjs = new List<Transform>();
    List<Transform> interactableObjs;

    VectorSensor sensor;

    float[] lastAction;

    Vector3 themeStartRotation;

    float episodeTotalReward;
    int episodeNumTrials;

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
        sensor = new VectorSensor(observationSize);

        lastAction = new float[] { -Mathf.Infinity, -Mathf.Infinity };

        episodeCount = 0;

        if (scenarioController != null)
        {
            scenarioController.usingRLClient = true;
            scenarioController.ObjectsInited += ObjectsPlaced;
            scenarioController.EventExecuting += ExecutingEvent;
            scenarioController.EventCompleted += ApplyForce;
            scenarioController.PostEventWaitCompleted += ResultObserved;
        }
        else
        {
            Debug.LogWarning("StackingAgent.Start: scenarioController is null!");
        }
    }

    void FixedUpdate()
    {
        Time.timeScale = scenarioController.timeScale;

        running = objectsPlaced && episodeStarted;

        if (constructObservation)
        {
            observation = ConstructObservation();
            noisyObservation = observation + (float)GaussianNoise(0, 0.1f);
            Debug.LogFormat("StackingAgent.FixedUpdate: Observation = {0}; Reward = {1}", observation, observation - lastObservation);
            float reward = (observation - lastObservation) > 0 ? (observation - lastObservation) : (observation - lastObservation) - 1;
            AddReward(reward);
            episodeTotalReward += reward;
            WriteOutSample(themeObj.transform, destObj.transform, lastAction, reward);

            if (observation - lastObservation != 0)
            {
                // figure out which is the new destination object
                List<Transform> sortedByHeight = interactableObjs.OrderByDescending(t => t.position.y).ToList();

                GameObject newDest = sortedByHeight.First().gameObject;
                OnDestObjChanged(destObj, newDest);
                destObj = newDest;

                if (observation - lastObservation < 0)
                {
                    usedDestObjs = usedDestObjs.Take(usedDestObjs.Count + (int)(observation - lastObservation)).ToList();
                } 
            }

            if (observation == interactableObjs.Count)
            {
                endEpisode = true;
            }

            constructObservation = false;
        }

        if (resolvePhysics)
        {
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
            endEpisode = false;
            EndEpisode();
        }
    }

    void WriteOutSample(Transform themeTransform, Transform destTransform, float[] action, float reward)
    {
        Dictionary<string, int> objNameToIntDict = new Dictionary<string, int>()
        {
            { "Cube", 0 },
            { "Sphere", 1 },
            { "Cylinder", 2 }
        };

        float[] arr = new float[] {
            episodeCount,
            objNameToIntDict[themeTransform.name.Split(new char[]{ '0','1','2','3','4','5','6','7','8','9' })[0]],
            objNameToIntDict[destTransform.name.Split(new char[]{ '0','1','2','3','4','5','6','7','8','9' })[0]],
            themeStartRotation.x * Mathf.Deg2Rad, themeStartRotation.y * Mathf.Deg2Rad, themeStartRotation.z * Mathf.Deg2Rad,
            action[0], action[1],
            useNoisyObservations ? noisyObservation : observation,
            reward,
            episodeTotalReward,
            episodeTotalReward/episodeNumTrials
            };
        string csv = string.Join(",", arr);
        Debug.LogFormat("WriteOutSample: {0}",csv);

        using (StreamWriter writer = new StreamWriter(string.Format("{0}.csv", outFileName), true))
        {
            writer.WriteLine(csv);
        }
    }

    public void ObjectsPlaced(object sender, EventArgs e)
    {
        if (!objectsPlaced)
        {
            objectsPlaced = true;
        }

        interactableObjs = scenarioController.interactableObjects.
            GetComponentsInChildren<Voxeme>().Where(v => v.isActiveAndEnabled).Select(v => v.transform).ToList();
        usedDestObjs.Clear();

        Debug.LogFormat("StackingAgent.ObjectsPlaced: Objects placed: [{0}]",
            string.Join(",\n\t",
                interactableObjs.Select(t => string.Format("{{{0}:{1}}}", t.name, GlobalHelper.VectorToParsable(t.position))).ToArray()));

        destObj = interactableObjs.OrderByDescending(t => t.position.y).ToList().First().gameObject;
        usedDestObjs.Add(destObj.transform);

        if (destObj != null)
        {
            Debug.LogFormat("StackingAgent.ObjectsPlaced: Setting destination object: {0}", destObj.name);
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
                force = new Vector3((float)GaussianNoise(0, 1),
                    0,
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

    public void ResultObserved(object sender, EventArgs e)
    {
        if (executingEvent) 
        {
            lastObservation = observation;
            resolvePhysics = true;
            executingEvent = false;
        }
    }

    GameObject SelectThemeObject()
    {
        GameObject theme = null;

        theme = interactableObjs.Except(usedDestObjs)
            .OrderBy(t => t.position.y).ToList().First().gameObject;

        themeStartRotation = new Vector3(theme.transform.eulerAngles.x > 180.0f ? theme.transform.eulerAngles.x - 360.0f : theme.transform.eulerAngles.x,
            theme.transform.eulerAngles.y > 180.0f ? theme.transform.eulerAngles.y - 360.0f : theme.transform.eulerAngles.y,
            theme.transform.eulerAngles.z > 180.0f ? theme.transform.eulerAngles.z - 360.0f : theme.transform.eulerAngles.z);

        return theme;
    }

    int ConstructObservation()
    {
        // sort objects by height
        List<Transform> sortedByHeight = interactableObjs.OrderByDescending(t => t.position.y).ToList();

        // take the topmost object and round its y-coord up to nearest int
        //  multiply by 10 (blocks are .1 x .1 x .1)
        int obs = (int)Mathf.Ceil(sortedByHeight.First().transform.position.y * 10);

        return obs;
    }

    public override void OnEpisodeBegin()
    {
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
                                              from item in list select item).ToList();
            foreach (Rigidbody rb in allRigidbodies)
            {
                rb.velocity = Vector3.zero;
            }
        }

        episodeCount += 1;
        episodeNumTrials = 0;

        Debug.Log("StackingAgent.OnEpisodeBegin: Beginning episode");
        episodeTotalReward = 0f;
        observation = 1;
        noisyObservation = observation + (float)GaussianNoise(0, 0.1f);
        sensor.AddObservation(useNoisyObservations ? noisyObservation : observation);
        Debug.LogFormat("StackingAgent.OnEpisodeBegin: Collecting {0} observation(s) - [{1}]",
            sensor.ObservationSize(), useNoisyObservations ? noisyObservation : observation);

        scenarioController.PlaceRandomly(scenarioController.surface);
        PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);

        episodeStarted = true;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (running)
        {
            if (!executingEvent)
            {
                sensor.AddObservation(useNoisyObservations ? noisyObservation : observation);
                Debug.LogFormat("StackingAgent.CollectObservations: Collecting {0} observation(s) - [{1}]",
                    sensor.ObservationSize(), useNoisyObservations ? noisyObservation : observation);
            }
            else
            {
                sensor.AddObservation(lastObservation);
                Debug.LogFormat("StackingAgent.CollectObservations: Collecting {0} observation(s) - [{1}]",
                    sensor.ObservationSize(), lastObservation);
            }

            waitingForAction = true;
        }
        else
        {
            // if the episode has terminated, return the last observation
            //  (i.e., the observation at the final state)
            sensor.AddObservation(useNoisyObservations ? noisyObservation : observation);
            Debug.LogFormat("StackingAgent.CollectObservations: Collecting {0} observation(s) - [{1}]",
                sensor.ObservationSize(), useNoisyObservations ? noisyObservation : observation);
        }
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        if (base.StepCount == 0)
        {
            return;
        }

        if (waitingForAction && !executingEvent && !resolvePhysics && !constructObservation)
        {
            Vector2 targetOnSurface = new Vector2(vectorAction[0], vectorAction[1]);

            if (scenarioController.IsValidAction(targetOnSurface))
            {
                if (!vectorAction.SequenceEqual(lastAction))
                {
                    GameObject newTheme = SelectThemeObject();

                    //when this happens the physics resolution hasn't finished yet so the new position of the destination object hasn't updated
                    OnThemeObjChanged(themeObj, newTheme);
                    themeObj = newTheme;

                    if (themeObj == null)
                    {
                        return;
                    }

                    vectorAction.CopyTo(lastAction, 0);

                    Debug.LogFormat("StackingAgent.OnActionReceived: Action received: {0}", string.Format("[{0}]", string.Join(",", vectorAction)));

                    if (targetOnSurface == Vector2.zero)
                    {
                        //UnityEditor.EditorApplication.ExecuteMenuItem("Edit/Play");
                    }

                    Bounds themeBounds = GlobalHelper.GetObjectWorldSize(themeObj);
                    Bounds destBounds = GlobalHelper.GetObjectWorldSize(destObj);
                    Debug.Log(GlobalHelper.VectorToParsable(destBounds.center));
                    Debug.Log(GlobalHelper.VectorToParsable(destBounds.max));
                    Vector3 targetPos = new Vector3(
                        destBounds.center.x + (destBounds.size.x * targetOnSurface.x),
                        destBounds.max.y + themeBounds.extents.y,
                        destBounds.center.z + (destBounds.size.z * targetOnSurface.y));

                    string eventStr = string.Format("put({0},{1})", themeObj.name, GlobalHelper.VectorToParsable(targetPos));
                    Debug.LogFormat("StackingAgent.OnActionReceived: executing event: {0}", eventStr);
                    scenarioController.SendToEventManager(eventStr);
                    episodeNumTrials += 1;

                    waitingForAction = false;
                }
                else
                {
                    Debug.LogFormat("StackingAgent.OnActionReceived: Invalid action {0} - equal to {1}",
                        string.Format("[{0}]", string.Join(",", vectorAction)),
                        string.Format("[{0}]", string.Join(",", lastAction)));
                }
            }
        }
        else
        {
            if (!waitingForAction)
            {
                Debug.LogFormat("StackingAgent.OnActionReceived: Not waiting for action");
            }

            if (executingEvent)
            {
                Debug.LogFormat("StackingAgent.OnActionReceived: Currently executing event");
            }
        }
    }

    public override void Heuristic(float[] actionsOut)
    {

    }

    /// <summary>
    /// Triggered when the themeObj flag changes
    /// </summary>
    // IN: oldVal -- previous value of themeObj
    //      newVal -- new or current value of themeObj
    void OnThemeObjChanged(GameObject oldVal, GameObject newVal)
    {
        Debug.Log(string.Format("==================== themeObj changed ==================== {0}->{1}",
            oldVal == null ? "NULL" : oldVal.name, newVal == null ? "NULL" : newVal.name));
    }

    /// <summary>
    /// Triggered when the destObj flag changes
    /// </summary>
    // IN: oldVal -- previous value of destObj
    //      newVal -- new or current value of destObj
    void OnDestObjChanged(GameObject oldVal, GameObject newVal)
    {
        Debug.Log(string.Format("==================== destObj changed ==================== {0}->{1}",
            oldVal == null ? "NULL" : oldVal.name, newVal == null ? "NULL" : newVal.name));
    }

    /// <summary>
    /// Triggered when the objectsPlaced flag changes
    /// </summary>
    // IN: oldVal -- previous value of objectsPlaced
    //      newVal -- new or current value of objectsPlaced
    void OnObjectsPlacedChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== objectsPlaced flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the waitingForAction flag changes
    /// </summary>
    // IN: oldVal -- previous value of waitingForAction
    //      newVal -- new or current value of waitingForAction
    void OnWaitingForActionChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== waitingForAction flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the episodeStarted flag changes
    /// </summary>
    // IN: oldVal -- previous value of episodeStarted
    //      newVal -- new or current value of episodeStarted
    void OnEpisodeStartedChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== episodeStarted flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the running flag changes
    /// </summary>
    // IN: oldVal -- previous value of running
    //      newVal -- new or current value of running
    void OnRunningChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== running flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the executingEvent flag changes
    /// </summary>
    // IN: oldVal -- previous value of executingEvent
    //      newVal -- new or current value of executingEvent
    void OnExecutingEventChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== executingEvent flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the resolvePhysics flag changes
    /// </summary>
    // IN: oldVal -- previous value of resolvePhysics
    //      newVal -- new or current value of resolvePhysics
    void OnResolvePhysicsChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== resolvePhysics flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the constructObservation flag changes
    /// </summary>
    // IN: oldVal -- previous value of constructObservation
    //      newVal -- new or current value of constructObservation
    void OnConstructObservationChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== constructObservation flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    /// <summary>
    /// Triggered when the endEpisode flag changes
    /// </summary>
    // IN: oldVal -- previous value of endEpisode
    //      newVal -- new or current value of endEpisode
    void OnEndEpisodeChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== endEpisode flag changed ==================== {0}->{1}", oldVal, newVal));
    }

    double GaussianNoise(float mean, float stdDev)
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
