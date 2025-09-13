using UnityEngine;

public class WalkOriginPosition : MoveActionNode 
{
    private Vector3 originPosition;
    private float maxDistanceFromOrigin;
    private bool isReturning;
    private bool hasReachedOrigin;

    public WalkOriginPosition(Transform transform, Transform target, float moveSpeed = 5f, 
                             Vector3 originPosition = default, float maxDistanceFromOrigin = 20f) 
        : base(transform, target, moveSpeed)
    {
        this.originPosition = originPosition == default ? transform.position : originPosition;
        this.maxDistanceFromOrigin = maxDistanceFromOrigin;
        this.isReturning = false;
        this.hasReachedOrigin = false;
    }

    protected override NodeState DoEvaluate()
    {
        float distanceFromOrigin = Vector3.Distance(transform.position, originPosition);
        if (distanceFromOrigin <= maxDistanceFromOrigin && !isReturning)
        {
            state = NodeState.Failure;
            return state;
        }

        if (!isReturning)
        {
            StartReturning();
        }

        float distanceToOrigin = Vector3.Distance(transform.position, originPosition);
        if (distanceToOrigin <= stoppingDistance)
        {
            hasReachedOrigin = true;
            state = NodeState.Success;
            return state;
        }

        if (Time.time - lastPathRequestTime > pathRequestCooldown && !hasReachedOrigin)
        {
            RequestNewPath();
        }

        state = NodeState.Running;
        return state;
    }

    public override void FixedEvaluate()
    {
        if (currentPath != null && currentPath.Count > 0)
        {
            FollowPath();
        }
    }

    private void StartReturning()
    {
        isReturning = true;
        hasReachedOrigin = false;
    }

    protected override void OnEnterNode()
    {
        isReturning = false;
        hasReachedOrigin = false;
        
        float distanceFromOrigin = Vector3.Distance(transform.position, originPosition);
        if (distanceFromOrigin > maxDistanceFromOrigin)
        {
            StartReturning();
        }
    }

    protected override void OnExitNode()
    {
        StopMoving();
        isReturning = false;
        hasReachedOrigin = false;
        
    }

    public void SetOriginPosition(Vector3 newOrigin)
    {
        originPosition = newOrigin;
    }

    public void SetMaxDistance(float newMaxDistance)
    {
        maxDistanceFromOrigin = newMaxDistance;
    }

    public float GetDistanceFromOrigin()
    {
        return Vector3.Distance(transform.position, originPosition);
    }

    public bool ShouldReturnToOrigin()
    {
        return Vector3.Distance(transform.position, originPosition) > maxDistanceFromOrigin;
    }
}