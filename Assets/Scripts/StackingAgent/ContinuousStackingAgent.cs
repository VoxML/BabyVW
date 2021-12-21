using System.Linq;
using UnityEngine;
using VoxSimPlatform.CogPhysics;
using VoxSimPlatform.Global;
using VoxSimPlatform.Vox;

public class ContinuousStackingAgent : StackingAgent
{
    public Vector2 actionSpaceLow,actionSpaceHigh;
    public Vector2 targetAction;    // as a percentage of X/Y action space (-1 to 1)
    public bool randomizeTargetAction;
    public float rewardBoost;

    public override void OnActionReceived(float[] vectorAction)
    {
        base.OnActionReceived(vectorAction);

        if (waitingForAction && !executingEvent && !resolvePhysics && !constructObservation)
        {
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

                partialSuccessReward = 0;

                vectorAction.CopyTo(lastAction, 0);

                Debug.LogFormat("StackingAgent.OnActionReceived: Action received: {0}, themeObs = {1}, destObj = {2}",
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

                if (themeBounds.center.y < 0.04f)
                    Debug.Break();

                // convert the action value to a location on the surface of the destination object
                Vector3 targetPos = new Vector3(
                    destBounds.center.x + (destBounds.size.x * (.01f * (targetOnSurface.x - ((actionSpaceHigh.x - actionSpaceLow.x) * 
                        ((1f + targetAction.x) * .5f))))),
                    destBounds.max.y + themeBounds.extents.y,
                    destBounds.center.z + (destBounds.size.z * (.01f * (targetOnSurface.y - ((actionSpaceHigh.y - actionSpaceLow.y) *
                        ((1f + targetAction.y) * .5f))))));

                // if the the object wouldn't touch the destination object at this location, don't even bother simulating it
                // we know it'll fall
                Bounds projectedBounds = new Bounds(targetPos, themeBounds.size);

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

                    partialSuccessReward = rewardBoost;

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

                episodeNumActions += 1;
                Debug.LogFormat("StackingAgent.OnActionReceived: episodeNumActions = {0}", episodeNumActions);
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
}
