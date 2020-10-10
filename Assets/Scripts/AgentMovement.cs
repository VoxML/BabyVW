using UnityEngine;
using System.Collections;

public class AgentMovement : MonoBehaviour
{
    // TODO: declare the agent GameObject here

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // TODO: Create four key-press events
        // Press 1: agent turns left
        // Press 2: agent turns right
        // Press 3: agent looks up
        // Press 4: agent looks down

        // Recommendation: call a different method under each key-press to keep things clean and modular
        // See here for handling key inputs: https://docs.unity3d.com/ScriptReference/Input.html (the "GetKey..." methods)

        // To move an object in VoxSim, the object must have a "Voxeme" component (click on "CrawlCamera" in the Unity hierarchy to view it
        //  in the inspector to see that it has a Voxeme component assigned).  To use the Voxeme component, the object must have an equivalent
        //  VoxML encoding in the VoxML folder (check VoxML/voxml/objects and see that CrawlCamera.xml has already been created).
        // To turn the agent, we need to alter the "targetRotation" field of the agent's "Voxeme" component

        //  targetRotation is a 3-vector (Unity type "Vector3") representing the Euler angles values of the agent's current rotation
        //  Since the agent has no embodiment at this point, "agent" is currently synonymous with the camera: the "CrawlCamera" object in Unity
        //  Therefore, changing the Y-value of "targetRotation" will alter the camera's yaw (left-right)
        //   and changing the X-value of "targetRotation" will alter the camera pitch (up-down)

        // 1) Declare the "agent" variable above.  This must be of type GameObject.  Save this file, then switch to the Unity editor (make sure BabyVoxDemo is open).
        // 2) Create a new game object in the scene, name it "AgentMotionController".
        // 3) Add this script as a component to the new object. (https://docs.unity3d.com/Manual/CreatingAndUsingScripts.html - see "Controlling a Game Object")
        // 4) Drag "CrawlCamera" from the hierarchy over the "Agent" field in the newly-created AgentMovement component of AgentMotionController
        // 5) Get the "Voxeme" component of the "agent" member field of this class (= CrawlCamera)
        //  how to get a component of an object: https://docs.unity3d.com/ScriptReference/GameObject.GetComponent.html -- VoxSim usage uses the public T GetComponent() version
        // 6) Now you should be able to access the "targetRotation" field of the Voxeme component.  In each of your key press methods, make changes to this value
        //  VoxSim will automatically interpolate the motion of the object until it gets to the target orientation. Since targetRotation is a Vector3, pay attention to
        //  how to update Vector3s: https://docs.unity3d.com/ScriptReference/Vector3-x.html (you must contruct a new Vector3 object and assign it to the Vector3 variable you wish
        //  to change; the individual x,y,z components of a Vector3 are read-only

        // Suggestions:
        //  Rather than hard-code the amount by which each key press changes the target rotation, make it an editable public field by declaring a public float or public double at the
        //   top of this class.  Then you will be able to change the amount directly through the Unity Editor.
        //  Select CrawlCamera in the hierarchy and inspect its Voxeme component.  By changing the value of "Turn Speed" you can change how fast the camera turns
    }
}
