using System.Collections.Generic;
using UnityEngine;

public class PathfindingTester : MonoBehaviour
{
    [Header("Target and Movement")]
    public Transform target;
    public float moveSpeed = 5f;
    public float stoppingDistance = 0.5f;
    public float waypointThreshold = 0.45f;
    
    [Header("Pathfinding")]
    public BasicAStar bAstar;
    public float pathRequestCooldown = 0.5f;
    
    [Header("Movement Smoothing")]
    public float turnSpeed = 10f;
    public bool useRotation = false;
    
    [Header("Animation (Optional)")]
    public Animator animator;
    
    private List<BasicAStar.Node> currentPath;
    private List<Vector3> currentWorldPath;
    private int currentWaypointIndex;
    private float lastPathRequestTime;
    private Vector3 smoothedDirection;
    private Rigidbody2D rb2d;
    private int agentId;
    
    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        if (rb2d == null)
        {
            Debug.LogWarning("Rigidbody2D not found! Adding one automatically.");
            rb2d = gameObject.AddComponent<Rigidbody2D>();
        }
        
        rb2d.freezeRotation = !useRotation;
        
        agentId = transform.GetInstanceID();
        

    }

    void Update()
    {
        if (target == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget <= stoppingDistance)
        {
            StopMoving();
            return;
        }

        if (Time.time - lastPathRequestTime > pathRequestCooldown)
        {
            RequestNewPath();
        }
    }
    
    void FixedUpdate()
    {
        if (currentWorldPath != null && currentWorldPath.Count > 0)
        {
            FollowPath();
        }
    }

    void RequestNewPath()
    {
        lastPathRequestTime = Time.time;
        
        if (bAstar != null)
        {
            currentPath = bAstar.FindPath(transform.position, target.position);
            // Debug.Log(currentPath.Count);
            if (currentPath != null && currentPath.Count > 0)
            {
                
                ConvertPathToWorldPositions();
                currentWaypointIndex = 0;
                
                Debug.Log($"BasicAStar: New path found with {currentWorldPath.Count} waypoints");
            }
            else {
                Debug.Log("BasicAStar: No path found to target!");
                // StopMoving();
                bAstar.RefreshGrid();
            }
        }
        else
        {
            if (AStarManager.Instance != null)
            {
                AStarManager.Instance.RequestPath(
                    transform.position,
                    target.position,
                    agentId,
                    OnPathReceived
                );
                Debug.Log("AStarManager: Path requested");
            }
            else
            {
                Debug.LogError("Neither BasicAStar nor AStarManager is available!");
            }
        }
    }
    
    void ConvertPathToWorldPositions()
    {
        currentWorldPath = new List<Vector3>();
        
        if (bAstar != null)
        {
            foreach (var node in currentPath)
            {
                Vector3 worldPos = bAstar.GridToWorldPosition(node.position.x, node.position.y);
                currentWorldPath.Add(worldPos);
            }
        }
    }
    
    void OnPathReceived(List<Vector3> path)
    {
        currentWorldPath = path;
        currentWaypointIndex = 0;
        
        if (path != null && path.Count > 0)
        {
            Debug.Log($"AStarManager: New path received with {currentWorldPath.Count} waypoints");
        }
        else
        {
            Debug.Log("AStarManager: No path received!");
            StopMoving();
        }
    }
    
    void FollowPath()
    {
        if (currentWaypointIndex >= currentWorldPath.Count)
        {
            currentWorldPath = null;
            StopMoving();
            return;
        }

        Vector3 targetWaypoint = currentWorldPath[currentWaypointIndex];
        targetWaypoint.z = transform.position.z; 
        
        Vector3 direction = (targetWaypoint - transform.position).normalized;
        
        if (animator != null)
        {
            animator.SetFloat("moveX", direction.x);
            animator.SetFloat("moveY", direction.y);
        }
        
        smoothedDirection = Vector3.Slerp(smoothedDirection, direction, Time.fixedDeltaTime * turnSpeed);
        
        rb2d.linearVelocity = new Vector2(smoothedDirection.x, smoothedDirection.y) * moveSpeed;
        
        if (useRotation && smoothedDirection != Vector3.zero)
        {
            float angle = Mathf.Atan2(smoothedDirection.y, smoothedDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
        
        if (Vector3.Distance(transform.position, targetWaypoint) < waypointThreshold)
        {
            currentWaypointIndex++;
        }
        
        if (animator != null)
        {
            float velocity = rb2d.linearVelocity.magnitude;
            animator.SetFloat("Speed", velocity);
            animator.SetBool("Moving", true);
        }
    }

    void StopMoving()
    {
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }
        
        if (currentWorldPath != null)
        {
            currentWorldPath.Clear();
        }
        
        if (animator != null)
        {
            animator.SetBool("Moving", false);
            animator.SetFloat("Speed", 0);
        }
    }
    
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        lastPathRequestTime = 0;
    }
    
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }
    
    public void SetStoppingDistance(float distance)
    {
        stoppingDistance = distance;
    }
    
    public bool HasPath()
    {
        return currentWorldPath != null && currentWorldPath.Count > 0;
    }
    
    public bool IsMoving()
    {
        return rb2d != null && rb2d.linearVelocity.magnitude > 0.1f;
    }
    
    void OnDrawGizmos()
    {
        if (currentWorldPath != null && currentWorldPath.Count > 1)
        {
            Gizmos.color = Color.blue;
            
            for (int i = 0; i < currentWorldPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentWorldPath[i], currentWorldPath[i + 1]);
            }
            
            if (currentWaypointIndex < currentWorldPath.Count)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(currentWorldPath[currentWaypointIndex], 0.3f);
            }
        }
        
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}