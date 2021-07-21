using UnityEngine;
using System.Linq;

using VoxSimPlatform.Global;

public class DiscreteStackingAgent : StackingAgent
{
    public override void OnActionReceived(float[] vectorAction)
    {
        base.OnActionReceived(vectorAction);
        
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
                        destBounds.center.x + (destBounds.size.x * (targetOnSurface.x - 1)),
                        destBounds.max.y + themeBounds.extents.y,
                        destBounds.center.z + (destBounds.size.z * (targetOnSurface.y - 1)));

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
}
