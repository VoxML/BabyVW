using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using VoxSimPlatform.CogPhysics;
using VoxSimPlatform.Global;
using VoxSimPlatform.SpatialReasoning.QSR;
using VoxSimPlatform.Vox;

public class PushingAgent : Agent
{
    public enum DestinationSelection
    {
        Highest,    // select the highest object as the destination object
        Consistent  // always use the same object as the destination (only use for 2-object tasks)
    };

    public GameObject themeObj, destObj;
    public int observationSize;
    public bool useVectorObservations, noisyVectors;

    [SerializeField]
    int episodeCount;
    public int EpisodeCount
    {
        get { return episodeCount; }
        set { episodeMaxAttempts = value; }
    }

    [SerializeField]
    int episodeMaxAttempts;
    public int EpisodeMaxAttempts
    {
        get { return episodeMaxAttempts; }
        set { episodeMaxAttempts = value; }
    }

    public int episodeNumAttempts;
    public bool useAllAttempts;

    public DestinationSelection destSelectionMethod;

    public float posRewardMultiplier;
    public float negRewardMultiplier;
    public float partialSuccessReward;
    public float observationSpaceScale;

    [SerializeField]
    float forceMultiplier;
    public float ForceMultiplier
    {
        get { return forceMultiplier; }
        set { forceMultiplier = value; }
    }

    public bool saveImages;
    public bool writeOutSamples;

    [SerializeField]
    string outFileName;
    public string OutFileName
    {
        get { return outFileName; }
        set { outFileName = value; }
    }

    [SerializeField]
    ScenarioController scenarioController;
    public ScenarioController ScenarioController
    {
        get { return scenarioController; }
        set { scenarioController = value; }
    }

    Dictionary<string, int[]> relDict = new Dictionary<string, int[]>()
    {
        { "support", new int[]{1, 0, 0, 0, 0} },
        { "left", new int[]{0, 1, 0, 0, 0} },
        { "right", new int[]{0, 0, 1, 0, 0} },
        { "in_front", new int[]{0, 0, 0, 1, 0} },
        { "behind", new int[]{0, 0, 0, 0, 1} }
    };

    List<Transform> _usedDestObjs = new List<Transform>();
    List<Transform> usedDestObjs
    {
        get { return _usedDestObjs; }
        set
        {
            if (_usedDestObjs != value)
            {
                OnUsedDestObjsChanged(_usedDestObjs, value);
            }
            _usedDestObjs = value;
        }
    }

    List<Transform> interactableObjs;

    CameraSensor cameraSensor;
    VectorSensor sensor;

    protected float[] lastAction;

    Vector3 themeStartLocation;
    Vector3 themeStartRotation;

    Vector3 themeMidLocation;
    Vector3 themeMidRotation;

    Vector3 lastForceApplied;

    int defaultMaxStep;

    float episodeTotalReward;

    DateTime episodeBeginTime, episodeEndTime;
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

    bool _abortAction = false;
    public bool abortAction
    {
        get { return _abortAction; }
        set
        {
            if (_abortAction != value)
            {
                OnAbortActionChanged(_abortAction, value);
            }
            _abortAction = value;
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

    protected int curNumObjsStacked, lastNumObjsStacked;

    List<float> observation, lastObservation, noisyObservation;

    public float actionBufferTime = 2000f;
    Timer actionBufferTimer;
    bool actionBufferElapsed = false;
 

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

        lastAction = new float[] { 0, 0 };

        actionBufferTimer = new Timer(actionBufferTime);
        actionBufferTimer.Elapsed += OnBufferElapsed;

        episodeCount = 0;

        if (scenarioController != null)
        {
            scenarioController.usingRLClient = true;
            scenarioController.squareFOV = GetComponent<CameraSensorComponent>() != null;
            scenarioController.imagesDest = outFileName.Replace(".csv", "");
            scenarioController.saveImages = saveImages;
            scenarioController.ObjectsInited += ObjectsPlaced;
            scenarioController.EventExecuting += ExecutingEvent;
            scenarioController.EventCompleted += ApplyForce;
            scenarioController.PostEventWaitCompleted += MakeDecisionRequest;
            scenarioController.PostEventWaitCompleted += ResultObserved;
            scenarioController.AbortAction += AbortAction;
            scenarioController.ForceEndEpisode += ForceEndEpisode;

            Time.timeScale = scenarioController.timeScale;
        }
        else
        {
            Debug.LogWarning("StackingAgent.Start: scenarioController is null!");
        }

        defaultMaxStep = MaxStep;
    }

    void Update()
    {
        //Debug.Log($"{running} {waitingForAction} {!resolvePhysics} {!endEpisode}");
        running = objectsPlaced && episodeStarted;

        if (!waitingForAction)
        {
            actionBufferTimer.Enabled = true;
        }
        if (themeObj.GetComponent<Rigidbody>().velocity.magnitude == 0 && actionBufferElapsed)
        {
            actionBufferElapsed = false;
            waitingForAction = true;
        }

        if (running && waitingForAction && !resolvePhysics && !endEpisode)
        {
            MaxStep = (int)Time.timeScale * defaultMaxStep;
            RequestDecision();
        }

        if (scenarioController.circumventEventManager)
        {
            if (themeObj != null)
            {
                // if we're not using the event manager, we have to track the theme object ourselves
                if ((themeObj.GetComponent<Voxeme>().transform.position - themeObj.GetComponent<Voxeme>().targetPosition).sqrMagnitude < Constants.EPSILON)
                {
                    if (executingEvent && !scenarioController.postEventWaitTimer.Enabled)
                    {
                        themeObj.GetComponent<Rigging>().ActivatePhysics(true);
                        scenarioController.OnEventCompleted(null, null);
                        // start the wait timer
                        scenarioController.postEventWaitTimer.Enabled = true;
                    }
                }
            }
        }

        if (constructObservation)
        {
            observation = ConstructObservation().Select(o => (float)o).ToList();
            noisyObservation = observation.Select(o => o + (float)GaussianNoise(0, 0.1f)).ToList();
            float reward = (curNumObjsStacked - lastNumObjsStacked) > 0 ?
                (curNumObjsStacked - lastNumObjsStacked) * (curNumObjsStacked - 1) :
                (curNumObjsStacked == interactableObjs.Count) ?
                (curNumObjsStacked - 1) * (curNumObjsStacked - 1) : (curNumObjsStacked - lastNumObjsStacked) - 1;
            reward = reward > 0 ? reward * posRewardMultiplier : reward * negRewardMultiplier; // scale up
            reward = reward > 0 ? (reward / episodeMaxAttempts) * (episodeMaxAttempts - episodeNumAttempts + 1) : reward; // decay positive rewards
            reward = reward > 0 ? reward : reward + partialSuccessReward; // add reward for partial success, if any
            Debug.LogFormat("StackingAgent.Update: Observation = {0}; Last observation = {1}; Reward = {2}", observation, lastObservation, reward);
            AddReward(reward);
            episodeTotalReward += reward;
            WriteOutSample(themeObj.transform, destObj.transform, lastAction, reward);

            Debug.LogFormat("StackingAgent.Update: themeObj = {0}; destObj = {1}", string.Format("{{{0}:{1}}}", themeObj.name, themeObj.transform.position.y),
                string.Format("{{{0}:{1}}}", destObj.name, destObj.transform.position.y));

            List<Transform> sortedByHeight = interactableObjs.Where(t => SurfaceClear(t.gameObject)).OrderByDescending(t => t.position.y).ToList();
            Debug.LogFormat("StackingAgent.Update: objects sorted by height = {0}", string.Format("[{0}]", string.Join(", ",
                    sortedByHeight.Select(t => string.Format("{{{0}:{1}}}", t.name, t.transform.position.y)))));

            Transform topmostObj = sortedByHeight.First();
            RaycastHit[] hits = Physics.RaycastAll(topmostObj.position, Vector3.down, topmostObj.position.y - Constants.EPSILON);
            Debug.LogFormat("StackingAgent.Update: hits = {0}", string.Format("[{0}]", string.Join(", ",
                    hits.Select(h => string.Format("{{{0}:{1}}}", h.collider.name, h.transform.position.y)))));

            Debug.LogFormat("StackingAgent.Update: usedDestObjs = {0}", string.Format("[{0}]", string.Join(", ",
                    usedDestObjs.Select(o => string.Format("{{{0}:{1}}}", o.name, o.transform.position.y)))));

            usedDestObjs = hits.Select(h => GlobalHelper.GetMostImmediateParentVoxeme(h.collider.gameObject).transform).ToList();

            if (!useAllAttempts)
            {

                if (!usedDestObjs.Contains(destObj.transform))
                {
                    usedDestObjs.Add(destObj.transform);
                    OnUsedDestObjsChanged(usedDestObjs.GetRange(0, usedDestObjs.Count - 1), usedDestObjs);
                }

                else if (episodeNumAttempts >= episodeMaxAttempts)
                {
                    endEpisode = true;
                }
            }
            else
            {
                if (curNumObjsStacked != interactableObjs.Count)
                {

                    if (!usedDestObjs.Contains(destObj.transform))
                    {
                        usedDestObjs.Add(destObj.transform);
                        OnUsedDestObjsChanged(usedDestObjs.GetRange(0, usedDestObjs.Count - 1), usedDestObjs);
                    }
                }

                if (episodeNumAttempts >= episodeMaxAttempts)
                {
                    endEpisode = true;
                }
            }

            scenarioController.SavePostEventImage(themeObj, string.Format("{0}{1}{2}", themeObj.name, episodeCount, episodeNumAttempts));

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
                for (int i = 0; i < observation.Count; i++)
                {
                    sensor.AddObservation(noisyVectors ? noisyObservation[i] : observation[i]);
                }
                Debug.LogFormat("StackingAgent.Update: Collecting {0} observation(s) - [{1}]",
                    sensor.ObservationSize(), noisyVectors ?
                    string.Join(", ", noisyObservation.Select(o => o.ToString()).ToArray()) :
                    string.Join(", ", observation.Select(o => o.ToString()).ToArray()));
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
            { "Capsule", 3 },
            { "SmallCube", 4 },
            { "Egg", 5 },
            { "RectPrism", 6 },
            { "Cone", 7 },
            { "Pyramid", 8 },
            { "Banana", 9 }
        };

        float angleOffsetStart = Vector3.Angle(Vector3.up, Quaternion.Euler(themeStartRotation) * Vector3.up);
        float angleOffsetMid = Vector3.Angle(Vector3.up, Quaternion.Euler(themeMidRotation) * Vector3.up);
        Vector3 themeEndLocation = new Vector3(themeTransform.position.x, themeTransform.position.y, themeTransform.position.z);
        Vector3 themeEndRotation = new Vector3(themeTransform.eulerAngles.x > 180.0f ? themeTransform.eulerAngles.x - 360.0f : themeTransform.eulerAngles.x,
            themeTransform.eulerAngles.y > 180.0f ? themeTransform.eulerAngles.y - 360.0f : themeTransform.eulerAngles.y,
            themeTransform.eulerAngles.z > 180.0f ? themeTransform.eulerAngles.z - 360.0f : themeTransform.eulerAngles.z);
        float angleOffsetEnd = Vector3.Angle(Vector3.up, themeTransform.up);

        Bounds themeBounds = GlobalHelper.GetObjectWorldSize(themeObj);

        float[] arr1 = new float[] {
            episodeCount,
            objNameToIntDict[themeTransform.name.Split(new char[]{ '0','1','2','3','4','5','6','7','8','9' })[0]],
            objNameToIntDict[destTransform.name.Split(new char[]{ '0','1','2','3','4','5','6','7','8','9' })[0]],
            themeStartLocation.x * Mathf.Deg2Rad, themeStartLocation.y * Mathf.Deg2Rad, themeStartLocation.z * Mathf.Deg2Rad,
            themeStartRotation.x * Mathf.Deg2Rad, themeStartRotation.y * Mathf.Deg2Rad, themeStartRotation.z * Mathf.Deg2Rad,
            angleOffsetStart * Mathf.Deg2Rad
            };

        float[] arr2 = action;

        float[] arr3 = new float[] {
            themeMidLocation.x * Mathf.Deg2Rad, themeMidLocation.y * Mathf.Deg2Rad, themeMidLocation.z * Mathf.Deg2Rad,
            themeMidRotation.x * Mathf.Deg2Rad, themeMidRotation.y * Mathf.Deg2Rad, themeMidRotation.z * Mathf.Deg2Rad,
            angleOffsetMid * Mathf.Deg2Rad,
            themeEndLocation.x * Mathf.Deg2Rad, themeEndLocation.y * Mathf.Deg2Rad, themeEndLocation.z * Mathf.Deg2Rad,
            themeEndRotation.x * Mathf.Deg2Rad, themeEndRotation.y * Mathf.Deg2Rad, themeEndRotation.z * Mathf.Deg2Rad,
            angleOffsetEnd * Mathf.Deg2Rad
            };

        float[] arr4 = new float[] {
            lastForceApplied.x, lastForceApplied.y, lastForceApplied.z
        };

        float[] arr5 = new float[] {
            themeBounds.center.x,themeBounds.center.y,themeBounds.center.z,
            themeBounds.size.x,themeBounds.size.y,themeBounds.size.z
            };

        float[] arr6 = noisyVectors ? noisyObservation.ToArray() : observation.ToArray();

        float[] arr7 = new float[] {
            reward,
            episodeTotalReward,
            episodeTotalReward/episodeNumAttempts
            };

        float[] arr = arr1.Concat(arr2).Concat(arr3).Concat(arr4).
            Concat(arr5).Concat(arr6).Concat(arr7).ToArray();
        string csv = string.Join(",", arr);
        Debug.LogFormat("WriteOutSample: {0}", csv);

        if (outFileName != string.Empty)
        {
            if (!outFileName.EndsWith(".csv"))
            {
                outFileName = string.Format("{0}.csv", outFileName);
            }
            string dirPath = Path.GetDirectoryName(outFileName);

            if (!Directory.Exists(dirPath))
            {
                Debug.LogFormat("WriteOutSample: creating directory at {0}", dirPath);
                DirectoryInfo dirInfo = Directory.CreateDirectory(dirPath);
            }

            if (!File.Exists(outFileName))
            {
                using (StreamWriter writer = new StreamWriter(outFileName))
                {
                    string[] header1 = new string[] {
                        "Episode #",            // 0
                        "Theme",                // 1
                        "Dest",                 // 2
                        "Theme Start Loc X",    // 3
                        "Theme Start Loc Y",    // 4
                        "Theme Start Loc Z",    // 5
                        "Theme Start Rot X",    // 6
                        "Theme Start Rot Y",    // 7
                        "Theme Start Rot Z",    // 8
                        "Theme Start Theta",    // 9
                        "Action[0]",            // 10
                        "Action[1]",            // 11
                        "Theme Mid Loc X",      // 12
                        "Theme Mid Loc Y",      // 13
                        "Theme Mid Loc Z",      // 14
                        "Theme Mid Rot X",      // 15
                        "Theme Mid Rot Y",      // 16
                        "Theme Mid Rot Z",      // 17
                        "Theme Mid Theta",      // 18
                        "Theme End Loc X",      // 19
                        "Theme End Loc Y",      // 20
                        "Theme End Loc Z",      // 21
                        "Theme End Rot X",      // 22
                        "Theme End Rot Y",      // 23
                        "Theme End Rot Z",      // 24
                        "Theme End Theta",      // 25
                        "Jitter X",             // 26
                        "Jitter Y",             // 27
                        "Jitter Z",             // 28
                        "Theme Center X",       // 29
                        "Theme Center Loc Y",   // 30
                        "Theme Center Loc Z",   // 31
                        "Theme Size X",         // 32
                        "Theme Size Y",         // 33
                        "Theme Size Z"          // 34
                    };

                    string[] header2 = new string[] { };
                    for (int i = 0; i < observation.Count; i++)
                    {
                        header2 = header2.Concat(new string[] { string.Format("Observation[{0}]", i) }).ToArray();
                    }

                    string[] header = header1.Concat(header2).ToArray();
                    string csvHeader = string.Join(",", header);

                    writer.WriteLine(csvHeader);
                }
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

        lastForceApplied = force;

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
            lastNumObjsStacked = curNumObjsStacked;
            lastObservation = observation;
            resolvePhysics = true;
            executingEvent = false;

            trialEndTime = DateTime.Now;
            Debug.LogFormat("StackingAgent.ResultObserved: Episode {0} - Trial took {1}-{2} = {3} seconds",
                episodeCount, trialEndTime.ToString("hh:mm:ss.fffffff"), trialBeginTime.ToString("hh:mm:ss.fffffff"), (trialEndTime - trialBeginTime).TotalSeconds);
        }
    }

    public void AbortAction(object sender, EventArgs e)
    {
        episodeNumAttempts--;
        abortAction = true;
    }

    public void ForceEndEpisode(object sender, EventArgs e)
    {
        endEpisode = true;
    }

    protected GameObject SelectThemeObject()
    {
        GameObject theme = interactableObjs[0].gameObject;

        themeStartLocation = new Vector3(theme.transform.position.x, theme.transform.position.y, theme.transform.position.z);
        themeStartRotation = new Vector3(theme.transform.eulerAngles.x > 180.0f ? theme.transform.eulerAngles.x - 360.0f : theme.transform.eulerAngles.x,
                theme.transform.eulerAngles.y > 180.0f ? theme.transform.eulerAngles.y - 360.0f : theme.transform.eulerAngles.y,
                theme.transform.eulerAngles.z > 180.0f ? theme.transform.eulerAngles.z - 360.0f : theme.transform.eulerAngles.z);

        return theme;
    }

    protected List<float> ConstructObservation()
    {
        float dist_x = destObj.transform.position.x - themeObj.transform.position.x;
        float dist_z = destObj.transform.position.z - themeObj.transform.position.z;
        List<float> obs = new List<float>{
            (lastAction != null) ? lastAction[0] : 0,
            (lastAction != null) ? lastAction[1] : 0,
            (lastObservation != null) ? lastObservation[4] : dist_x,
            (lastObservation != null) ? lastObservation[5] : dist_z,
            dist_x,
            dist_z };

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
        episodeNumAttempts = 0;

        Debug.LogFormat("StackingAgent.OnEpisodeBegin: Beginning episode {0}", episodeCount);
        episodeTotalReward = 0f;

        GameObject newTheme = scenarioController.PlaceMaterialBlock();
        OnThemeObjChanged(themeObj, newTheme);
        themeObj = newTheme;

        GameObject newDest = scenarioController.PlaceGoal();
        OnDestObjChanged(destObj, newDest);
        destObj = newDest;

        scenarioController.InitializeVoxemes();
        PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);

        observation = ConstructObservation();
        //Debug.Log(observation);
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
                for (int i = 0; i < observation.Count; i++)
                {
                    sensor.AddObservation(observation[i]);
                }
                Debug.LogFormat("PushingAgent.CollectObservations: Collecting {0} observation(s) - [{1}]",
                    sensor.ObservationSize(), string.Join(", ", observation.Select(o => o.ToString()).ToArray()));
            }
            waitingForAction = true;
        }
    }

    public void OnBufferElapsed(object sender, ElapsedEventArgs e)
    {
        actionBufferElapsed = true;
        actionBufferTimer.Interval = actionBufferTime;
        actionBufferTimer.Enabled = false;
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
    /// Triggered when the themeObj changes
    /// </summary>
    // IN: oldVal -- previous value of themeObj
    //      newVal -- new or current value of themeObj
    protected void OnThemeObjChanged(GameObject oldVal, GameObject newVal)
    {
        Debug.Log(string.Format("==================== themeObj changed ==================== {0}->{1}",
            oldVal == null ? "NULL" : oldVal.name, newVal == null ? "NULL" : newVal.name));
    }

    /// <summary>
    /// Triggered when the destObj changes
    /// </summary>
    // IN: oldVal -- previous value of destObj
    //      newVal -- new or current value of destObj
    protected void OnDestObjChanged(GameObject oldVal, GameObject newVal)
    {
        Debug.Log(string.Format("==================== destObj changed ==================== {0}->{1}",
            oldVal == null ? "NULL" : oldVal.name, newVal == null ? "NULL" : newVal.name));

        if (scenarioController.centerDestObj)
        {
            if (newVal != null)
            {
                //scenarioController.CenterObjectInView(newVal);
            }
        }
    }

    /// <summary>
    /// Triggered when usedDestObjs changes
    /// </summary>
    // IN: oldVal -- previous value of usedDestObjs
    //      newVal -- new or current value of usedDestObjs
    protected void OnUsedDestObjsChanged(List<Transform> oldVal, List<Transform> newVal)
    {
        Debug.Log(string.Format("==================== usedDestObjs changed ==================== {0}->{1}",
            string.Format("[{0}]", string.Join(", ",
                    oldVal.Select(o => string.Format("{{{0}:{1}}}", o.name, o.transform.position.y)))),
            string.Format("[{0}]", string.Join(", ",
                    newVal.Select(o => string.Format("{{{0}:{1}}}", o.name, o.transform.position.y))))));
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
    // IN: oldVal -- previous value of abortAction
    //      newVal -- new or current value of abortAction
    protected void OnAbortActionChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== abortAction flag changed ==================== {0}->{1}", oldVal, newVal));
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
