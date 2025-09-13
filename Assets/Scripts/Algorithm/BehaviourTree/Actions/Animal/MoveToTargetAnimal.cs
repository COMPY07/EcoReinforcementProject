using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MoveToTargetAnimal : MoveActionNode
{
    private string targetBlackboardKey;
    private BlackBoard blackboard;
    private Vector3 lastTargetPosition;
    private float targetMovementThreshold = .15f;

    private float pathTime, targetUpdateTime;
    public MoveToTargetAnimal(Transform transform, string targetKey, float speed, float pathRequestCooldown = .2f, float targetMovementThreshold = .15f)
        : base(transform, null, speed)
    {
        this.targetBlackboardKey = targetKey;
        this.stoppingDistance = .5f;
        this.lastTargetPosition = Vector3.positiveInfinity; // 처음엔 무조건 경로 요청
        this.pathRequestCooldown =.5f;
        this.targetMovementThreshold = targetMovementThreshold;
        this.targetUpdateTime = pathRequestCooldown;
        
        if (transform.TryGetComponent<AlgorithmBaseEntity>(out var entity))
        {
            this.blackboard = entity.GetBlack();
        }
    }

    protected override NodeState DoEvaluate()
    {
        UpdateTargetFromBlackboard();

        if (target == null)
        {
            StopMoving();
            ClearBlackboardTarget();
            return NodeState.Failure;
        }

        float distanceToTarget = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(target.position.x, target.position.z));
        
        if (distanceToTarget <= stoppingDistance) 
        {
            StopMoving();
            if (blackboard != null)
                blackboard.Set("isMovingToTarget", false);
            return NodeState.Success;
        }

        if (ShouldRequestNewPath())
        {
            RequestNewPath();
            lastTargetPosition = target.position;
        }

        return NodeState.Running;
    }

    public override void FixedEvaluate() 
    {
        
        
        // Debug.Log("check: "+target+" " + currentPath?.Count);
        // if(targetBlackboardKey == "Food") Debug.Log("check: "+target+" " + currentPath?.Count);
        if (target != null && currentPath != null && currentPath.Count > 0) {
            FollowPath();
        }
    }

    private void UpdateTargetFromBlackboard()
    {
        pathTime += Time.deltaTime;
        if (blackboard == null || pathTime < targetUpdateTime) return;
        
        Transform newTarget = blackboard.Get<Transform>(targetBlackboardKey);
        pathTime = 0;
        if (this.target != newTarget)
        {
            this.target = newTarget;
            SetTarget(newTarget);
            
            if (newTarget != null && blackboard != null)
            {
                blackboard.Set("isMovingToTarget", true);
                // Debug.Log($"Target updated to: {newTarget.name}");
            }
        }
    }
    
    private bool ShouldRequestNewPath()
    {
        if (target == null) return false;
        
        if (currentPath == null || currentPath.Count == 0)
            return true;
        
        float targetMovedDistance = Vector3.Distance(target.position, lastTargetPosition);
        if (targetMovedDistance > targetMovementThreshold)
            return true;
        
        return false;
    }
    
    private void ClearBlackboardTarget()
    {
        if (blackboard != null)
        {
            blackboard.Set("isMovingToTarget", false);
            blackboard.Set("hasTarget", false);
        }
    }

    protected override void OnExitNode()
    {
        StopMoving();
        ClearBlackboardTarget();
    }

    public void SetTargetUpdateThreshold(float threshold)
    {
        this.targetMovementThreshold = threshold;
    }
    
    public Transform GetCurrentTarget()
    {
        return target;
    }
}