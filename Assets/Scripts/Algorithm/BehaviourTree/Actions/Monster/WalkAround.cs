using UnityEngine;


public class WalkAround : MoveActionNode {
    private float wanderArea;
    private Vector3 originPosition;

    private float movingTime;
    private float currentTime;
    private bool moving;
    private Vector2 direction;


    private bool success;
    public WalkAround(Transform transform, Transform target, float moveSpeed = 5f,
        float wanderArea = 10f, float movingTime = 10f) : base(transform, target, moveSpeed)
    {
        this.wanderArea = wanderArea;
        
        moving = false;
        this.movingTime = movingTime;
        this.currentTime = 0;
        originPosition = this.transform.position;
        
        Vector3 newPosition = setTarget();
        success = false;
    }

    protected override NodeState DoEvaluate() {
        currentTime += Time.deltaTime; 
        
        if (!success) {
            float distanceToTarget = Vector3.Distance(this.transform.position, target.position);
            if (distanceToTarget <= stoppingDistance) {
                success = true;
                // Debug.Log("stop!");
                return NodeState.Success;
            }
        }


        // Debug.Log((Time.time+" "+ lastPathRequestTime +" "+pathRequestCooldown));
        if (Time.time - lastPathRequestTime > pathRequestCooldown && !success)
        {
            RequestNewPath();
            
        }
        // Debug.Log("check");
        
        if (movingTime > currentTime) return NodeState.Running;
        currentTime = 0;
        
        

        if (success)
        {
            Vector3 newPosition = setTarget();
            // Debug.Log($"새로운 위치 이동 : {newPosition}");
            
            success = false;
        }


        return NodeState.Running;
    }


    public override void FixedEvaluate()
    {
        
        
        if (currentPath != null && currentPath.Count > 0)
            FollowPath();
        
    }
    
    
    private Vector3 setTarget()
    {
        // float half = wanderArea / 2;
        //
        // float originX = originPosition.x;
        // float originY = originPosition.y;
        //
        // float x = Random.Range(originX - half, originX + half);
        // float y = Random.Range(originY - half, originY + half);
        //
        // Vector3 newPosition = new Vector3(x, y, target.position.z);
        //
        
        //
        Vector3 newPosition = AStarManager.Instance.GetRandomWalkablePosition(originPosition, wanderArea);
        target.position = newPosition;
        return newPosition;
    }

    protected override void OnEnterNode()
    {
        // Debug.Log("start "+this.transform.gameObject.name);
    }
    
    protected override void OnExitNode()
    {
        // Debug.Log("End!");
        StopMoving();
        success = false;
    }
}