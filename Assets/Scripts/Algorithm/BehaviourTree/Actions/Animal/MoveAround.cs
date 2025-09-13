using System.Collections.Generic;
using UnityEngine;

public class MoveAround : MoveActionNode
{
    private float wanderRadius;
    private float wanderTimer;
    private float wanderTimerMax;
   
    private BlackBoard blackBoard;
    private Vector3 originPosition;

    private float pathStartTime;
    private const float pathTimeout = 10f;
    private bool needsNewPath = true;

    public MoveAround(Transform transform, float speed = 10f, float wanderRadius = 10f, float wanderTime = 5f)
        : base(transform, null, speed) 
    {
        this.wanderRadius = wanderRadius;
        this.wanderTimerMax = wanderTime;
        this.wanderTimer = 0f;
        this.stoppingDistance = .2f; // 도착 거리를 좀 더 넉넉하게
        this.pathRequestCooldown = 0.3f;
        
        this.originPosition = transform.position;

        // target = new GameObject($"{transform.name}_WanderTarget").transform;
        // target.position = transform.position;
        
        
        if (transform.TryGetComponent<AlgorithmBaseEntity>(out var entity))
        {
            blackBoard = entity.GetBlack();

            target = blackBoard.Get<GameObject>("movementTarget")?.transform;
        }
    }
   
    protected override NodeState DoEvaluate()
    {
        wanderTimer += Time.deltaTime;
        
        
        bool hasTimedOut = Time.time - pathStartTime > pathTimeout;
        bool shouldFindNewPath = wanderTimer >= wanderTimerMax || hasTimedOut || needsNewPath;
        // Debug.Log(hasTimedOut+" " + shouldFindNewPath);
        if (shouldFindNewPath) {
            FindNewWanderPath();
            needsNewPath = false;
        }
        
        float distanceToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                new Vector3(target.position.x, 0, target.position.z));

        if (target != null)
        {
            if (distanceToTarget <= stoppingDistance)
            {
                // Debug.Log("Reached wander target, finding new path");
                FindNewWanderPath();
            }
        }
        
        
        if (Time.time - lastPathRequestTime > pathRequestCooldown && target != null)
        {
            if (distanceToTarget > stoppingDistance)
            {
                
                if (currentPath == null || currentPath.Count == 0) {
                    RequestNewPath();
                }
            }
        }
       
        return NodeState.Running;
    }

    public override void FixedEvaluate()
    {
        if (target != null && currentPath != null && currentPath.Count > 0) {
            FollowPath();
            // Debug.Log("what?");
        }
    }
    
    private void FindNewWanderPath()
    {
        Vector3 newWanderTarget = AStarManager.Instance.GetRandomWalkablePosition(
            originPosition, 
            wanderRadius,
            15
        );
        
        
        if (target != null)
        {
            target.position = new Vector3(newWanderTarget.x, transform.position.y, newWanderTarget.z);
        }

        
        pathStartTime = Time.time;
        wanderTimer = 0f;
        
        
        if (blackBoard != null)
        {
            blackBoard.Set("isWandering", true);
            blackBoard.Set("wanderTarget", target);
        }
        
        lastPathRequestTime = 0f;
    }
    


    protected override void OnExitNode()
    {
        StopMoving(); 
        // Debug.Log("check");
        if (blackBoard != null)
        {
            blackBoard.Set("isWandering", false);
        }
    }
    
    

}