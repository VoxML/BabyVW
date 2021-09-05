using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;
using VoxSimPlatform.Global;
using VoxSimPlatform.Vox;

public class ContinuousStackingAgent : StackingAgent
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

                    // when this happens the physics resolution hasn't finished yet so the new position of the destination object hasn't updated
                    OnThemeObjChanged(themeObj, newTheme);
                    themeObj = newTheme;

                    if (themeObj == null)
                    {
                        return;
                    }

                    vectorAction.CopyTo(lastAction, 0);

                    Debug.LogFormat("StackingAgent.OnActionReceived: Action received: {0}", string.Format("[{0}]", string.Join(",", vectorAction)));

                    Bounds themeBounds = GlobalHelper.GetObjectWorldSize(themeObj);
                    Bounds destBounds = GlobalHelper.GetObjectWorldSize(destObj);
                    Debug.LogFormat("StackingAgent.OnActionReceived: Action received: " +
                    	"themeBounds.center = {0}; themeBounds.size = {1}; " +
                        "destBounds.center = {2}; destBounds.size = {3}",
                        GlobalHelper.VectorToParsable(themeBounds.center),
                        GlobalHelper.VectorToParsable(themeBounds.size),
                        GlobalHelper.VectorToParsable(destBounds.center),
                        GlobalHelper.VectorToParsable(destBounds.size));
                        
                    Vector3 targetPos = new Vector3(
                        destBounds.center.x + (destBounds.size.x * targetOnSurface.x),
                        destBounds.max.y + themeBounds.extents.y,
                        destBounds.center.z + (destBounds.size.z * targetOnSurface.y));

                    Vector3 inputPoint = new Vector3(targetPos.x, destBounds.max.y, targetPos.z);
                    Vector3 closestPoint = Physics.ClosestPoint(inputPoint,
                        themeObj.GetComponentInChildren<Collider>(),
                        targetPos, themeObj.transform.rotation);

                    if (closestPoint.y > inputPoint.y)
                    {
                        targetPos = new Vector3(targetPos.x, targetPos.y - (closestPoint.y - inputPoint.y), targetPos.z);
                    }

                    if (!circumventEventManager)
                    {
                        themeObj.GetComponent<Voxeme>().rigidbodiesOutOfSync = true;
                        PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);

                        string eventStr = string.Format("put({0},{1})", themeObj.name, GlobalHelper.VectorToParsable(targetPos));
                        Debug.LogFormat("StackingAgent.OnActionReceived: executing event: {0}", eventStr);
                        scenarioController.SendToEventManager(eventStr);
                    }
                    else
                    {
                        themeObj.GetComponent<Voxeme>().targetPosition = targetPos;
                        scenarioController.OnEventExecuting(null, null);
                    }

                    episodeNumActions += 1;
                    Debug.LogFormat("StackingAgent.OnActionReceived: episodeNumTrials = {0}", episodeNumActions);

                    waitingForAction = false;
                }
                else
                {
                    Debug.LogFormat("StackingAgent.OnActionReceived: Invalid action {0} - equal to {1}",
                        string.Format("[{0}]", string.Join(",", vectorAction)),
                        string.Format("[{0}]", string.Join(",", lastAction)));
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
