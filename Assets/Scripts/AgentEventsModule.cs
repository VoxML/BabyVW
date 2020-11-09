using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using VoxSimPlatform.CogPhysics;

public class AgentEventsModule : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void GRASP(object[] args)
    {
        if (args[0] is GameObject)
        {
            GameObject obj = args[0] as GameObject;
            obj.GetComponent<Rigging>().ActivatePhysics(false);
        }
    }

    public void UNGRASP(object[] args)
    {
        if (args[0] is GameObject)
        {
            GameObject obj = args[0] as GameObject;
            obj.GetComponent<Rigging>().ActivatePhysics(true);
        }
    }
}
