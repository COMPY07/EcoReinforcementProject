using System.Collections.Generic;
using UnityEngine;

public class FleeFromTarget : MoveActionNode
{
    private string threatBlackboardKey;  
    private Transform threat;          
    private BlackBoard blackboard;
    private Vector3 lastThreatPosition;
    private float threatMovementThreshold = 1f;
    private float safeDistance = 15f;
    private float fleeDistance = 20f;
    private GameObject fleeTargetDummy;  
    private Vector3 currentFleeTarget;  
    private float pathTime, targetUpdateTime;

    public FleeFromTarget(Transform transform, string threatKey, float speed, float pathRequestCooldown = 0.5f, float threatMovementThreshold = 1f)
        : base(transform, null, speed)
    {
        this.threatBlackboardKey = threatKey;
        this.stoppingDistance = 2f;
        this.lastThreatPosition = Vector3.positiveInfinity;
        this.pathRequestCooldown = pathRequestCooldown;
        this.threatMovementThreshold = threatMovementThreshold;
        this.targetUpdateTime = pathRequestCooldown;
        this.safeDistance = 15f;
        this.fleeDistance = 20f;
        this.currentFleeTarget = Vector3.zero;
        
        
        if (transform.TryGetComponent<AlgorithmBaseEntity>(out var entity))
        {
            this.blackboard = entity.GetBlack();
            fleeTargetDummy = blackboard.Get<GameObject>("movementTarget");
            SetTarget(fleeTargetDummy.transform);

        }
    }

    protected override NodeState DoEvaluate()
    {
        UpdateThreatFromBlackboard();

        if (threat == null)
        {
            StopMoving();
            ClearBlackboardFlee();
            return NodeState.Failure;
        }

        float distanceToThreat = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z), 
            new Vector2(threat.position.x, threat.position.z)
        );
        
        if (distanceToThreat >= safeDistance) 
        {
            StopMoving();
            if (blackboard != null)
                blackboard.Set("isFleeing", false);
            return NodeState.Success;
        }

        if (ShouldRequestNewFleePath())
        {
            RequestFleePathAwayFromThreat();
            lastThreatPosition = threat.position;
        }

        return NodeState.Running;
    }

    public override void FixedEvaluate() 
    {
        if (target != null && currentPath != null && currentPath.Count > 0) 
        {
            FollowPath();
        }
    }

    private void UpdateThreatFromBlackboard()
    {
        pathTime += Time.deltaTime;
        if (blackboard == null || pathTime < targetUpdateTime) return;
        
        Transform newThreat = blackboard.Get<Transform>(threatBlackboardKey);
        pathTime = 0f;
        
        if (this.threat != newThreat)
        {
            this.threat = newThreat;
            
            if (newThreat != null && blackboard != null)
            {
                blackboard.Set("isFleeing", true);
                blackboard.Set("hasFleeTarget", true);
            }
        }
    }
    
    private bool ShouldRequestNewFleePath()
    {
        if (threat == null) return false;
        
        if (currentPath == null || currentPath.Count == 0)
            return true;
        
        if (Time.time - lastPathRequestTime < pathRequestCooldown)
            return false;
        
        float threatMovedDistance = Vector3.Distance(threat.position, lastThreatPosition);
        if (threatMovedDistance > threatMovementThreshold)
            return true;
        
        if (target != null)
        {
            float distanceToFleeTarget = Vector3.Distance(transform.position, target.position);
            if (distanceToFleeTarget <= stoppingDistance)
                return true;
        }
        
        return false;
    }

    private void RequestFleePathAwayFromThreat()
    {
        if (threat == null) return;

        Vector3 directionAwayFromThreat = (transform.position - threat.position).normalized;
        Vector3 fleeTargetPosition = transform.position + directionAwayFromThreat * fleeDistance;
        
        if (AStarManager.Instance != null)
        {
            fleeTargetPosition = AStarManager.Instance.GetRandomWalkablePosition(fleeTargetPosition, fleeDistance * 0.5f);
        }
        
        
        // Debug.Log(fleeTargetDummy);
        fleeTargetDummy.transform.position = fleeTargetPosition;
        currentFleeTarget = fleeTargetPosition;
        
        RequestNewPath();
        
        if (blackboard != null)
        {
            blackboard.Set("fleeTargetPosition", fleeTargetPosition);
            blackboard.Set("distanceToThreat", Vector3.Distance(transform.position, threat.position));
            blackboard.Set("threatPosition", threat.position);
        }
    }
    
    private void ClearBlackboardFlee()
    {
        if (blackboard != null)
        {
            blackboard.Set("isFleeing", false);
            blackboard.Set("hasFleeTarget", false);
        }
    }

    protected override void OnExitNode()
    {
        StopMoving();
        ClearBlackboardFlee();
        
    }

}