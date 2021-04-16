using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

using Unity.MLAgents;
using Unity.MLAgents.Sensors;

using VoxSimPlatform.Global;
using VoxSimPlatform.Vox;

public class StackingAgent : Agent
{
    public GameObject themeObj, destObj;
    public int observationSize;

    public ScenarioController scenarioController;

    List<Transform> interactableObjs;
    
    VectorSensor sensor;

    float[] lastAction;

    bool objectsPlaced = false;
    bool waitingForAction = false;
    bool episodeStarted = false;
    bool running = false;
    bool executingEvent = false;
    bool resolvePhysics = false;
    bool constructObservation = false;
    bool endEpisode = false;

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
        running = objectsPlaced && episodeStarted;

        if (constructObservation)
        {
            observation = ConstructObservation();
            Debug.LogFormat("StackingAgent.FixedUpdate: Observation = {0}; Reward = {1}", observation, observation - lastObservation);
            AddReward((observation - lastObservation) > 0 ? (observation - lastObservation) : (observation - lastObservation)-1);

            if (observation - lastObservation != 0)
            {
                // figure out which is the new destination object
                List<Transform> sortedByHeight = interactableObjs.OrderByDescending(t => t.position.y).ToList();

                destObj = sortedByHeight.First().gameObject;
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

        Debug.LogFormat("StackingAgent.ObjectsPlaced: Objects placed: [{0}]",
            string.Join(",\n\t",
                interactableObjs.Select(t => string.Format("{{{0}:{1}}}", t.name, GlobalHelper.VectorToParsable(t.position))).ToArray()));

        destObj = interactableObjs[0].gameObject;

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

        if (themeObj == null)
        {
            theme = interactableObjs[1].gameObject;
        }
        else
        {
            if (interactableObjs.IndexOf(themeObj.transform) + 1 < interactableObjs.Count)
            {
                theme = interactableObjs[interactableObjs.IndexOf(themeObj.transform) + 1].gameObject;
            }
        }

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
                sensor.AddObservation(-1);
            }

            waitingForAction = true;
        }
        else
        {
            // if the episode has terminated, return the last observation
            //  (i.e., the observation at the final state)
            sensor.AddObservation(lastObservation);
        }
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        if (waitingForAction && !executingEvent)
        {
            Vector2 targetOnSurface = new Vector2(vectorAction[0], vectorAction[1]);

            if (scenarioController.IsValidAction(targetOnSurface))
            {
                if (!vectorAction.SequenceEqual(lastAction))
                {
                    themeObj = SelectThemeObject();

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
                    Vector3 targetPos = new Vector3(
                        destBounds.center.x + (destBounds.size.x * targetOnSurface.x),
                        destBounds.max.y + themeBounds.extents.y,
                        destBounds.center.z + (destBounds.size.z * targetOnSurface.y));

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
}
