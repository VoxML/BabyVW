using UnityEngine;
using VoxSimPlatform.CogPhysics;
using VoxSimPlatform.Global;
using VoxSimPlatform.Vox;

public class ContinuousPushingAgent : PushingAgent
{
    public Vector2 actionSpaceLow, actionSpaceHigh;
    public Vector2 targetAction;    // as a percentage of X/Y action space (-1 to 1)
    public bool randomizeTargetAction;
    public float rewardBoost;

    public override void OnActionReceived(float[] vectorAction)
    {
        base.OnActionReceived(vectorAction);

        if (waitingForAction && !executingEvent && !resolvePhysics && !constructObservation)
        {
            Vector2 forceToPush = new Vector2(vectorAction[0], vectorAction[1]);

            if (ScenarioController.IsValidAction(forceToPush))
            {
                partialSuccessReward = 0;

                vectorAction.CopyTo(lastAction, 0);

                Debug.LogFormat("ContinuousPushingAgent.OnActionReceived: Action received: {0}, themeObs = {1}, destObj = {2}",
                    string.Format("[{0}]", string.Join(",", vectorAction)), themeObj.name, destObj.name);

                //Bounds themeBounds = GlobalHelper.GetObjectWorldSize(themeObj);
                //Bounds destBounds = GlobalHelper.GetObjectWorldSize(destObj);
                //Debug.LogFormat("StackingAgent.OnActionReceived: Action received: " +
                //	"[{0}] themeBounds.center = {1}; themeBounds.size = {2}; " +
                //    "[{3}] destBounds.center = {4}; destBounds.size = {5}",
                //    themeObj.name,
                //    GlobalHelper.VectorToParsable(themeBounds.center),
                //    GlobalHelper.VectorToParsable(themeBounds.size),
                //    destObj.name,
                //    GlobalHelper.VectorToParsable(destBounds.center),
                //    GlobalHelper.VectorToParsable(destBounds.size));

                themeObj.GetComponent<Rigidbody>().AddForce(new Vector3(forceToPush.x, 0, forceToPush.y));

                // Depends on whether the implementation is 1D or 2D
                // Potential to add partial reward if within a certain radius from the target

                //if (projectedBounds.Intersects(destBounds))
                //{ 
                //    Vector3 inputPoint = new Vector3(targetPos.x, destBounds.max.y, targetPos.z);
                //    Vector3 closestPoint = Physics.ClosestPoint(inputPoint,
                //        themeObj.GetComponentInChildren<Collider>(),
                //        targetPos, themeObj.transform.rotation);

                //    if (!ScenarioController.circumventEventManager)
                //    {
                //        PhysicsHelper.ResolveAllPhysicsDiscrepancies(false);

                //        string eventStr = string.Format("put({0},{1})", themeObj.name, GlobalHelper.VectorToParsable(targetPos));
                //        Debug.LogFormat("StackingAgent.OnActionReceived: executing event: {0}", eventStr);
                //        ScenarioController.SendToEventManager(eventStr);
                //    }
                //    partialSuccessReward = rewardBoost;
                //    waitingForAction = false;
                //}
                episodeNumAttempts += 1;
                Debug.LogFormat("ContinuousPushingAgent.OnActionReceived: episodeNumAttempts = {0}", episodeNumAttempts);

                waitingForAction = false;
            }
            else
            {
                Debug.LogFormat("ContinuousPushingAgent.OnActionReceived: Invalid action {0} - equal to {1}",
                    string.Format("[{0}]", string.Join(",", vectorAction)),
                    string.Format("[{0}]", string.Join(",", lastAction)));
            }
        }
        else
        {
            if (!waitingForAction)
            {
                Debug.LogFormat("ContinuousPushingAgent.OnActionReceived: Not waiting for action");
            }

            if (executingEvent)
            {
                Debug.LogFormat("ContinuousPushingAgent.OnActionReceived: Currently executing event");
            }
        }
    }
}