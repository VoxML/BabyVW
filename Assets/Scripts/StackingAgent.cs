using UnityEngine;
using System;

using Unity.MLAgents;
using Unity.MLAgents.Sensors;

public class StackingAgent : Agent
{
    public GameObject testObj;
    public int observationSize;

    public ScenarioController scenarioController;

    VectorSensor sensor;

    void Start()
    {
        sensor = new VectorSensor(observationSize);

        if (scenarioController != null)
        {
            scenarioController.ObjectsInited += ObjectsPlaced;
        }
        else
        {
            Debug.LogWarning("StackingAgent.Start: scenarioController is null!");
        }
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("Beginning episode");
    }

    public void ObjectsPlaced(object sender, EventArgs e)
    {
        testObj = GameObject.Find("Cube0");

        if (testObj != null)
        {
            Debug.Log("Found Cube0");
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(testObj.transform.position);
    }

    public override void OnActionReceived(float[] vectorAction)
    {
       
    }

    public override void Heuristic(float[] actionsOut)
    {

    }
}
