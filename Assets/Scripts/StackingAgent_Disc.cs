using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

using Unity.MLAgents;
using Unity.MLAgents.Sensors;

using VoxSimPlatform.Global;
using VoxSimPlatform.Vox;

public class StackingAgent_Disc : Agent
{
    public GameObject themeObj, destObj;
    public int observationSize;

    public ScenarioController scenarioController;

    List<Transform> usedDestObjs = new List<Transform>();
    List<Transform> interactableObjs;

    VectorSensor sensor;

    float[] lastAction;

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

    float observation, lastObservation;

    void Start()
    {
        sensor = new VectorSensor(observationSize);

        lastAction = new float[] { -Mathf.Infinity, -Mathf.Infinity };

        if (scenarioController != null)
        {
            scenarioController.usingRLClient = true;
            scenarioController.ObjectsInited += ObjectsPlaced;
            scenarioController.EventExecuting += ExecutingEvent;
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
            Debug.LogFormat("StackingAgent.FixedUpdate: Observation = {0}; Reward = {1}", observation, observation - lastObservation);
            AddReward((observation - lastObservation) > 0 ? (observation - lastObservation) : (observation - lastObservation) - 1);

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
        
        Debug.Log("StackingAgent.OnEpisodeBegin: Beginning episode");
        observation = 1;
        sensor.AddObservation(observation);

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
                sensor.AddObservation(observation);
                Debug.LogFormat("StackingAgent.CollectObservations: Collecting {0} observation(s)",
                    sensor.ObservationSize());
            }
            else
            {
                sensor.AddObservation(lastObservation);
            }

            waitingForAction = true;
        }
        else
        {
            // if the episode has terminated, return the last observation
            //  (i.e., the observation at the final state)
            sensor.AddObservation(observation);
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
                        destBounds.center.x + (destBounds.size.x * (targetOnSurface.x) - 1),
                        destBounds.max.y + themeBounds.extents.y,
                        destBounds.center.z + (destBounds.size.z * (targetOnSurface.y) - 1));

                    string eventStr = string.Format("put({0},{1})", themeObj.name, GlobalHelper.VectorToParsable(targetPos));
                    Debug.LogFormat("StackingAgent.OnActionReceived: executing event: {0}", eventStr);
                    scenarioController.SendToEventManager(eventStr);

                    waitingForAction = false;
                }
                else
                {
                    Debug.LogFormat("StackingAgent.OnActionReceived: Invalid action");
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
}
