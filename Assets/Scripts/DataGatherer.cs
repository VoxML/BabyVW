using UnityEngine;

using System;
using System.Collections.Generic;
using System.Reflection;

using VoxSimPlatform.Global;

public class DataGatherer : MonoBehaviour
{
    public List<List<object>> iterations = new List<List<object>>
    {
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-2cubes-stochastic10-noreset", 0, 10, 10 }),
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-cube_sphere-stochastic10-noreset", 1, 10, 10 }),
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-cube_cylinder-stochastic10-noreset", 2, 10, 10 }),
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-cube_capsule-stochastic10-noreset", 3, 10, 10 }),
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-bigcube_smallcube-stochastic10-noreset", 4, 10, 10 }),
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-cube_egg-stochastic10-noreset", 5, 10, 10 }),
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-cube_rectprism-stochastic10-noreset", 6, 10, 10 }),
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-cube_cone-stochastic10-noreset", 7, 10, 10 }),
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-cube_pyramid-stochastic10-noreset", 8, 10, 10 }),
        new List<object>(new object[] { "stacker-agent/analysis/trial-data/040322-cube_banana-stochastic10-noreset", 9, 10, 10 })
    };

    public int index;

    public float maxForceMultiplier;

    public MonoBehaviour agent;

    Type agentType;
    PropertyInfo propInfo;

    public event EventHandler IterationFinished;

    public void OnIterationFinished(object sender, EventArgs e)
    {
        if (IterationFinished != null)
        {
            IterationFinished(this, e);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        agentType = agent.GetType();

        index = 0;

        IterationFinished += StartNextIteration;
    }

    // Update is called once per frame
    void Update()
    {
        if ((((string)agentType.GetProperty("OutFileName").GetValue(agent, null)).Replace(".csv",string.Empty) !=
            (iterations[index][0] as string).Replace(".csv",string.Empty)) ||
            (((int)agentType.GetProperty("MaxEpisodes").GetValue(agent, null)) != (int)iterations[index][2]) ||
            (((int)agentType.GetProperty("EpisodeMaxAttempts").GetValue(agent, null)) != (int)iterations[index][3]))
        {
            Debug.Break();
        }
    }

    void StartNextIteration(object sender, EventArgs e)
    {
        index++;
        Debug.Log(string.Format("DataGatherer: StartNextInteration: index = {0}", index));

        if (index < iterations.Count)
        {
            agentType.GetProperty("OutFileName").SetValue(agent, iterations[index][0] as string);

            ((ScenarioController)agentType.GetProperty("ScenarioController").GetValue(agent, null)).interactableObjectTypes[1] =
                ((ScenarioController)agentType.GetProperty("ScenarioController").GetValue(agent, null)).objectTypes.GetChild((int)iterations[index][1]);

            if (((ScenarioController)agentType.GetProperty("ScenarioController").GetValue(agent, null)).saveImages)
            {
                ((ScenarioController)agentType.GetProperty("ScenarioController").GetValue(agent, null)).imagesDest =
                    ((string)agentType.GetProperty("OutFileName").GetValue(agent, null)).Replace(".csv", "");
            }

            Bounds bounds = GlobalHelper.GetObjectWorldSize(((ScenarioController)agentType.GetProperty("ScenarioController").GetValue(agent, null)).
                interactableObjectTypes[1].gameObject);
            float forceMultiplerValue = maxForceMultiplier * (bounds.size.x / 0.1f) * (bounds.size.z / 0.1f);
            forceMultiplerValue = forceMultiplerValue > maxForceMultiplier ? maxForceMultiplier : forceMultiplerValue;
            agentType.GetProperty("ForceMultiplier").SetValue(agent, forceMultiplerValue);

            agentType.GetProperty("MaxEpisodes").SetValue(agent, (int)iterations[index][2]);
            agentType.GetProperty("EpisodeCount").SetValue(agent, 0);
            agentType.GetProperty("EpisodeMaxAttempts").SetValue(agent, (int)iterations[index][3]);
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
