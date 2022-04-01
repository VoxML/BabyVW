using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VoxSimPlatform.CogPhysics;
using VoxSimPlatform.Global;
using VoxSimPlatform.SpatialReasoning.QSR;
using VoxSimPlatform.Vox;

public class VectorActionEventArgs : EventArgs
{
    public float[] vectorAction;

    public VectorActionEventArgs(float[] vectorAction)
    {
        this.vectorAction = new float[] { -Mathf.Infinity, -Mathf.Infinity };
        vectorAction.CopyTo(this.vectorAction, 0);
    }
}

public class StochasticAgent : MonoBehaviour
{
    public enum DestinationSelection
    {
        Highest,    // select the highest object as the destination object
        Consistent  // always use the same object as the destination (only use for 2-object tasks)
    };

    public GameObject themeObj, destObj;
    public int observationSize;
    public bool useVectorObservations, noisyVectors;
    public bool useHeight;
    public bool useRelations;
    public bool useCenterOfGravity;

    public int maxEpisodes;
    public int episodeCount;
    public int episodeMaxAttempts;
    public int episodeNumAttempts;
    public bool useAllAttempts;

    public DestinationSelection destSelectionMethod;

    public float observationSpaceScale;

    public float forceMultiplier;

    public bool saveImages;
    public bool writeOutSamples;

    public string outFileName;

    public ScenarioController scenarioController;

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
    Vector2 centerOfGravity;

    void Start()
    {
        lastAction = new float[] { -Mathf.Infinity, -Mathf.Infinity };

        episodeCount = 0;

        if (scenarioController != null)
        {
            scenarioController.squareFOV = saveImages;
            scenarioController.imagesDest = outFileName.Replace(".csv", "");
            scenarioController.saveImages = saveImages;
            scenarioController.ObjectsInited += ObjectsPlaced;
            scenarioController.EventExecuting += ExecutingEvent;
            scenarioController.EventCompleted += CalcCenterOfGravity;
            scenarioController.EventCompleted += ApplyForce;
            scenarioController.PostEventWaitCompleted += ReadyForAction;
            scenarioController.PostEventWaitCompleted += ResultObserved;
            scenarioController.ForceEndEpisode += ForceEndEpisode;
        }
        else
        {
            Debug.LogWarning("StochasticAgent.Start: scenarioController is null!");
        }

        Time.timeScale = scenarioController.timeScale;
    }

    void Update()
    {
        if (objectsPlaced && !episodeStarted)
        {
            OnEpisodeBegin(this, null);
        }

        running = objectsPlaced && episodeStarted;

        if (running && waitingForAction && !resolvePhysics && !endEpisode)
        {
            float[] action = new float[] {
                RandomHelper.RandomFloat(-0.5f, 0.5f, (int)(RandomHelper.RangeFlags.MinInclusive & RandomHelper.RangeFlags.MaxInclusive)),
                RandomHelper.RandomFloat(-0.5f, 0.5f, (int)(RandomHelper.RangeFlags.MinInclusive & RandomHelper.RangeFlags.MaxInclusive))
            };
            OnActionReceived(this, new VectorActionEventArgs(action));
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
            WriteOutSample(themeObj.transform, destObj.transform, lastAction);

            Debug.LogFormat("StochasticAgent.Update: themeObj = {0}; destObj = {1}", string.Format("{{{0}:{1}}}", themeObj.name, themeObj.transform.position.y),
                string.Format("{{{0}:{1}}}", destObj.name, destObj.transform.position.y));

            List<Transform> sortedByHeight = interactableObjs.Where(t => SurfaceClear(t.gameObject)).OrderByDescending(t => t.position.y).ToList();
            Debug.LogFormat("StochasticAgent.Update: objects sorted by height = {0}", string.Format("[{0}]", string.Join(", ",
                    sortedByHeight.Select(t => string.Format("{{{0}:{1}}}", t.name, t.transform.position.y)))));

            Transform topmostObj = sortedByHeight.First();
            RaycastHit[] hits = Physics.RaycastAll(topmostObj.position, Vector3.down, topmostObj.position.y - Constants.EPSILON);
            Debug.LogFormat("StochasticAgent.Update: hits = {0}", string.Format("[{0}]", string.Join(", ",
                    hits.Select(h => string.Format("{{{0}:{1}}}", h.collider.name, h.transform.position.y)))));

            Debug.LogFormat("StochasticAgent.Update: usedDestObjs = {0}", string.Format("[{0}]", string.Join(", ",
                    usedDestObjs.Select(o => string.Format("{{{0}:{1}}}", o.name, o.transform.position.y))))); 

            usedDestObjs = hits.Select(h => GlobalHelper.GetMostImmediateParentVoxeme(h.collider.gameObject).transform).ToList();

            //GameObject newDest = sortedByHeight.Except(new List<Transform>() { themeObj.transform }).First().gameObject;
            GameObject newDest = sortedByHeight.First().gameObject;

            Debug.LogFormat("StochasticAgent.Update: newDest = {0}", string.Format("{{{0}:{1}}}", newDest.name, newDest.transform.position.y));

            if (newDest != destObj)
            {
                if ((Mathf.Abs(newDest.transform.position.y - destObj.transform.position.y) < Constants.EPSILON) ||
                        (destSelectionMethod == DestinationSelection.Consistent))
                {
                    newDest = destObj;
                }
            }

            if (!useAllAttempts)
            { 
                OnDestObjChanged(destObj, newDest);
                destObj = newDest;

                if (!usedDestObjs.Contains(destObj.transform))
                {
                    usedDestObjs.Add(destObj.transform);
                    OnUsedDestObjsChanged(usedDestObjs.GetRange(0, usedDestObjs.Count - 1), usedDestObjs);
                }

                if (curNumObjsStacked == interactableObjs.Count)
                {
                    Debug.LogFormat("StochasticAgent.Update: observation = {0} (interactableObjs.Count = {1})", observation, interactableObjs.Count);

                    endEpisode = true;
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
                    OnDestObjChanged(destObj, newDest);
                    destObj = newDest;

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

            scenarioController.SavePostEventImage(themeObj,string.Format("{0}{1}{2}",themeObj.name,episodeCount,episodeNumAttempts));

            constructObservation = false;
        }

        if (resolvePhysics)
        {
            Debug.Log("StochasticAgent.Update: resolving physics");
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

            episodeEndTime = DateTime.Now;

            Debug.LogFormat("StochasticAgent.Update: Episode {0} - Episode took {1}-{2} = {3} seconds",
                episodeCount, episodeEndTime.ToString("hh:mm:ss.fffffff"), episodeBeginTime.ToString("hh:mm:ss.fffffff"), (episodeEndTime - episodeBeginTime).TotalSeconds);

            OnEpisodeEnd(this, null);
        }
    }

    void WriteOutSample(Transform themeTransform, Transform destTransform, float[] action)
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
        }   ;

        float[] arr5 = new float[] {
            themeBounds.center.x,themeBounds.center.y,themeBounds.center.z,
            themeBounds.size.x,themeBounds.size.y,themeBounds.size.z
            };

        float[] arr6 = noisyVectors ? noisyObservation.ToArray() : observation.ToArray();

        float[] arr = arr1.Concat(arr2).Concat(arr3).Concat(arr4).
            Concat(arr5).Concat(arr6).ToArray();
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
        Debug.LogFormat("StochasticAgent.ObjectsPlaced: usedDestObjs = [{0}]", string.Join(", ",
            usedDestObjs.Select(o => o.name)));

        Debug.LogFormat("StochasticAgent.ObjectsPlaced: Objects placed: [{0}]",
            string.Join(",\n\t",
                interactableObjs.Select(t => string.Format("{{{0}:{1}}}", t.name, GlobalHelper.VectorToParsable(t.position))).ToArray()));

        List<Transform> sortedByHeight = interactableObjs.Where(t => SurfaceClear(t.gameObject)).OrderByDescending(t => t.position.y).ToList();
        Debug.LogFormat("StochasticAgent.ObjectsPlaced: object sequence = {0}", string.Format("[{0}]", string.Join(", ",
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
            Debug.LogFormat("StochasticAgent.ObjectsPlaced: Setting destination object: {0}", destObj.name);
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

    public void CalcCenterOfGravity(object sender, EventArgs e)
    {
        themeMidLocation = new Vector3(themeObj.transform.position.x, themeObj.transform.position.y, themeObj.transform.position.z);
        themeMidRotation = new Vector3(themeObj.transform.eulerAngles.x > 180.0f ? themeObj.transform.eulerAngles.x - 360.0f : themeObj.transform.eulerAngles.x,
            themeObj.transform.eulerAngles.y > 180.0f ? themeObj.transform.eulerAngles.y - 360.0f : themeObj.transform.eulerAngles.y,
            themeObj.transform.eulerAngles.z > 180.0f ? themeObj.transform.eulerAngles.z - 360.0f : themeObj.transform.eulerAngles.z);

        // calc center of stack bounds
        // calc center of theme object bounds
        // CoG = center of theme bounds - center of stack bounds
        Debug.LogFormat("StochasticAgent.CalcCenterOfGravity: stack = [{0}]", string.Join(", ",
                        usedDestObjs.Select(o => o.name)));

        Bounds stackBounds = GlobalHelper.GetObjectWorldSize(usedDestObjs.Select(t => t.gameObject).ToList());
        Bounds themeBounds = GlobalHelper.GetObjectWorldSize(themeObj);
        centerOfGravity = new Vector2(themeBounds.center.x, themeBounds.center.z) -
            new Vector2(stackBounds.center.x, stackBounds.center.z);
        // scale by size of the stack bounds
        centerOfGravity = new Vector2(centerOfGravity.x / stackBounds.size.x * observationSpaceScale,
            centerOfGravity.y / stackBounds.size.z * observationSpaceScale);

        Debug.LogFormat("StochasticAgent.CalcCenterOfGravity: <{0};{1}>", centerOfGravity.x, centerOfGravity.y);
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

    public void ReadyForAction(object sender, EventArgs e)
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
            Debug.LogFormat("StochasticAgent.ResultObserved: Episode {0} - Trial took {1}-{2} = {3} seconds",
                episodeCount, trialEndTime.ToString("hh:mm:ss.fffffff"), trialBeginTime.ToString("hh:mm:ss.fffffff"), (trialEndTime - trialBeginTime).TotalSeconds);
        }
    }

    public void ForceEndEpisode(object sender, EventArgs e)
    {
        endEpisode = true;
    }

    protected GameObject SelectThemeObject()
    {
        GameObject theme = null;

        Debug.LogFormat("StochasticAgent.SelectThemeObject: usedDestObjs = [{0}]", string.Join(", ",
            usedDestObjs.Select(o => o.name)));

        List<Transform> sortedByHeight = interactableObjs.Except(usedDestObjs).Where(t => SurfaceClear(t.gameObject))
             .OrderBy(t => t.position.y).ToList();
        Debug.LogFormat("StochasticAgent.SelectThemeObject: object sequence = {0}", string.Format("[{0}]", string.Join(",",
            sortedByHeight.Select(t => string.Format("({0}, {1})", t.name, t.transform.position.y)))));

        try
        { 
            theme = sortedByHeight.First().gameObject;

            themeStartLocation = new Vector3(theme.transform.position.x,theme.transform.position.y, theme.transform.position.z);
            themeStartRotation = new Vector3(theme.transform.eulerAngles.x > 180.0f ? theme.transform.eulerAngles.x - 360.0f : theme.transform.eulerAngles.x,
                theme.transform.eulerAngles.y > 180.0f ? theme.transform.eulerAngles.y - 360.0f : theme.transform.eulerAngles.y,
                theme.transform.eulerAngles.z > 180.0f ? theme.transform.eulerAngles.z - 360.0f : theme.transform.eulerAngles.z);

        }
        catch (Exception ex)
        {
            if (ex is InvalidOperationException)
            {
                scenarioController.OnForceEndEpisode(this, null);  // rather inelegant solution
            }
        }
        
        return theme;
    }

    protected List<float> ConstructObservation()
    {
        // sort objects by height
        List<Transform> sortedByHeight = interactableObjs.OrderByDescending(t => t.position.y).ToList();
        Debug.LogFormat("StochasticAgent.ConstructObservation: [{0}]", string.Join(",", sortedByHeight.Select(o => o.position.y).ToList()));

        lastNumObjsStacked = curNumObjsStacked;

        Transform topmostObj = sortedByHeight.First();
        Debug.LogFormat("StochasticAgent.ConstructObservation: topmostObj = {0}", topmostObj.name);
        RaycastHit[] hits = Physics.RaycastAll(topmostObj.position, Vector3.down, topmostObj.position.y-Constants.EPSILON);
        Debug.LogFormat("StochasticAgent.ConstructObservation: hits = {0}", string.Format("[{0}]", string.Join(", ",
            hits.Select(h => string.Format("{{{0}:{1}}}", h.collider.name, h.transform.position.y)))));
        curNumObjsStacked = hits.Length+1;

        Debug.LogFormat("StochasticAgent.ConstructObservation: curNumObjsStacked = {0}; lastNumObjsStacked = {1}", curNumObjsStacked, lastNumObjsStacked);

        List<float> obs = new List<float>();
        if (useHeight)
        {
            obs.Add(curNumObjsStacked*observationSpaceScale);
        }

        if (useRelations)
        {
            List<string> rels = scenarioController.GetRelations(destObj, themeObj);
            if (rels.Count == 0)
            {
                obs.AddRange(new float[] { 0, 0, 0, 0, 0 });
            }
            else
            { 
                foreach (string r in rels)
                {
                    if (relDict.Keys.Contains(r))
                    {
                        foreach (int i in relDict[r])
                        {
                            obs.Add(i * observationSpaceScale);
                        }
                    }
                }
            }
        }

        if (useCenterOfGravity)
        {
            obs.Add(centerOfGravity.x*observationSpaceScale);
            obs.Add(centerOfGravity.y*observationSpaceScale);
        }

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

    public void OnEpisodeBegin(object sender, EventArgs e)
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

        Debug.LogFormat("StochasticAgent.OnEpisodeBegin: Beginning episode {0}", episodeCount);
        episodeTotalReward = 0f;

        scenarioController.PlaceRandomly(scenarioController.surface);
        PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);

        GameObject newTheme = SelectThemeObject();
        OnThemeObjChanged(themeObj, newTheme);
        themeObj = newTheme;

        curNumObjsStacked = 1;
        lastNumObjsStacked = 1;

        episodeStarted = true;
        episodeBeginTime = DateTime.Now;

        Debug.LogFormat("StochasticAgent.OnEpisodeBegin: Episode {0} - Resetting took {1}-{2} = {3} seconds",
            episodeCount, episodeBeginTime.ToString("hh:mm:ss.fffffff"), episodeEndTime.ToString("hh:mm:ss.fffffff"), (episodeBeginTime - episodeEndTime).TotalSeconds);

        if (episodeCount <= 2)
        {
            // give the Python client time to connect and initialize
            scenarioController.postEventWaitTimer.Interval = scenarioController.postEventWaitTimerTime;
        }
        else
        {
            scenarioController.postEventWaitTimer.Interval = 1;
        }
        scenarioController.postEventWaitTimer.Enabled = true;
    }

    public void OnActionReceived(object sender, EventArgs e)
    {
        trialBeginTime = DateTime.Now;

        if (waitingForAction && !executingEvent && !resolvePhysics && !constructObservation)
        {
            float[] vectorAction = ((VectorActionEventArgs)e).vectorAction;

            Vector2 targetOnSurface = new Vector2(vectorAction[0], vectorAction[1]);

            if (scenarioController.IsValidAction(targetOnSurface))
            {
                GameObject newTheme = SelectThemeObject();

                // when this happens the physics resolution hasn't finished yet so the new position of the destination object hasn't updated
                OnThemeObjChanged(themeObj, newTheme);
                themeObj = newTheme;

                if (themeObj == null)
                {
                    return;
                }

                vectorAction.CopyTo(lastAction, 0);

                Debug.LogFormat("StackingAgent.OnActionReceived: Action received: {0}, themeObj = {1}, destObj = {2}",
                    string.Format("[{0}]", string.Join(",", vectorAction)), themeObj.name, destObj.name);

                Bounds themeBounds = GlobalHelper.GetObjectWorldSize(themeObj);
                Bounds destBounds = GlobalHelper.GetObjectWorldSize(destObj);
                Debug.LogFormat("StackingAgent.OnActionReceived: Action received: " +
                    "[{0}] themeBounds.center = {1}; themeBounds.size = {2}; " +
                    "[{3}] destBounds.center = {4}; destBounds.size = {5}",
                    themeObj.name,
                    GlobalHelper.VectorToParsable(themeBounds.center),
                    GlobalHelper.VectorToParsable(themeBounds.size),
                    destObj.name,
                    GlobalHelper.VectorToParsable(destBounds.center),
                    GlobalHelper.VectorToParsable(destBounds.size));

                // convert the action value to a location on the surface of the destination object
                Vector3 targetPos = new Vector3(
                        destBounds.center.x + (destBounds.size.x * targetOnSurface.x),
                        destBounds.max.y + themeBounds.extents.y,
                        destBounds.center.z + (destBounds.size.z * targetOnSurface.y));

                Debug.Log(GlobalHelper.VectorToParsable(targetPos));

                // if the the object wouldn't touch the destination object at this location, don't even bother simulating it
                // we know it'll fall
                Bounds projectedBounds = new Bounds(targetPos, themeBounds.size);
                projectedBounds.size = new Vector3(projectedBounds.size.x + Constants.EPSILON,
                    projectedBounds.size.y + Constants.EPSILON,
                    projectedBounds.size.z + Constants.EPSILON);

                if (projectedBounds.Intersects(destBounds))
                {
                    Vector3 inputPoint = new Vector3(targetPos.x, destBounds.max.y, targetPos.z);
                    Vector3 closestPoint = Physics.ClosestPoint(inputPoint,
                        themeObj.GetComponentInChildren<Collider>(),
                        targetPos, themeObj.transform.rotation);

                    if (closestPoint.y > inputPoint.y)
                    {
                        targetPos = new Vector3(targetPos.x, targetPos.y - (closestPoint.y - inputPoint.y), targetPos.z);
                    }

                    if (!scenarioController.circumventEventManager)
                    {
                        themeObj.GetComponent<Voxeme>().rigidbodiesOutOfSync = true;
                        PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);

                        string eventStr = string.Format("put({0},{1})", themeObj.name, GlobalHelper.VectorToParsable(targetPos));
                        Debug.LogFormat("StackingAgent.OnActionReceived: executing event: {0}", eventStr);
                        scenarioController.SendToEventManager(eventStr);
                    }
                    else
                    {
                        //themeObj.GetComponent<Voxeme>().rigidbodiesOutOfSync = true;
                        //PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);

                        themeObj.GetComponent<Rigging>().ActivatePhysics(false);
                        themeObj.GetComponent<Voxeme>().targetPosition = targetPos;
                        scenarioController.OnEventExecuting(null, null);
                    }

                    waitingForAction = false;
                }
                else
                {
                    targetPos = new Vector3(targetPos.x, destBounds.center.y, targetPos.z);

                    themeObj.GetComponent<Voxeme>().targetPosition = targetPos;
                    themeObj.transform.position = targetPos;
                    //resolvePhysics = true;
                    lastNumObjsStacked = curNumObjsStacked;
                    scenarioController.OnEventCompleted(null, null);
                    themeObj.GetComponent<Voxeme>().rigidbodiesOutOfSync = true;
                    PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);
                    constructObservation = true;
                }

                episodeNumAttempts += 1;
                Debug.LogFormat("StackingAgent.OnActionReceived: episodeNumAttempts = {0}", episodeNumAttempts);
            }
            else
            {
                Debug.LogFormat("StackingAgent.OnActionReceived: Invalid action {0} - equal to {1}",
                    string.Format("[{0}]", string.Join(",", vectorAction)),
                    string.Format("[{0}]", string.Join(",", lastAction)));
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

    public void OnEpisodeEnd(object sender, EventArgs e)
    {
        endEpisode = false;

        if (episodeCount >= maxEpisodes)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
         Application.Quit();
#endif
        }
        else
        {
            OnEpisodeBegin(this, null);
        }
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
