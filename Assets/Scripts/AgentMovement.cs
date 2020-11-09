using UnityEngine;
using System.Collections;
using VoxSimPlatform.Vox;
using VoxSimPlatform.Global;

public class AgentMovement : MonoBehaviour
{
    public float rotationIntervalY;
    public float rotationIntervalX;
    
    public GameObject agent;

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // To move an object in VoxSim, the object must have a "Voxeme" component (click on "CrawlCamera" in the Unity hierarchy to view it
        //  in the inspector to see that it has a Voxeme component assigned).  To use the Voxeme component, the object must have an equivalent
        //  VoxML encoding in the VoxML folder (check VoxML/voxml/objects and see that CrawlCamera.xml has already been created).
        // To turn the agent, we need to alter the "targetRotation" field of the agent's "Voxeme" component

        //  targetRotation is a 3-vector (Unity type "Vector3") representing the Euler angles values of the agent's current rotation
        //  Since the agent has no embodiment at this point, "agent" is currently synonymous with the camera: the "CrawlCamera" object in Unity
        //  Therefore, changing the Y-value of "targetRotation" will alter the camera's yaw (left-right)
        //   and changing the X-value of "targetRotation" will alter the camera pitch (up-down)

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TurnLeft();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TurnRight();

        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TurnUp();

        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            TurnDown();
        }
    }

    void TurnLeft()
    {
        Vector3 curRotation = GlobalHelper.GetMostImmediateParentVoxeme(agent).GetComponent<Voxeme>().transform.eulerAngles;

        GlobalHelper.GetMostImmediateParentVoxeme(agent).GetComponent<Voxeme>().targetRotation = new Vector3(curRotation.x, curRotation.y + rotationIntervalY, curRotation.z);
    }


    void TurnRight()
    {
        Vector3 curRotation = GlobalHelper.GetMostImmediateParentVoxeme(agent).GetComponent<Voxeme>().transform.eulerAngles;

        GlobalHelper.GetMostImmediateParentVoxeme(agent).GetComponent<Voxeme>().targetRotation = new Vector3(curRotation.x, curRotation.y - rotationIntervalY, curRotation.z);
    }



    void TurnUp()
    {
        Vector3 curRotation = GlobalHelper.GetMostImmediateParentVoxeme(agent).GetComponent<Voxeme>().transform.eulerAngles;

        GlobalHelper.GetMostImmediateParentVoxeme(agent).GetComponent<Voxeme>().targetRotation = new Vector3(curRotation.x + rotationIntervalX, curRotation.y, curRotation.z);
    }


    void TurnDown()
    {
        Vector3 curRotation = GlobalHelper.GetMostImmediateParentVoxeme(agent).GetComponent<Voxeme>().transform.eulerAngles;

        GlobalHelper.GetMostImmediateParentVoxeme(agent).GetComponent<Voxeme>().targetRotation = new Vector3(curRotation.x - rotationIntervalX, curRotation.y, curRotation.z);
    }
}
