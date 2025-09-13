using System.Collections.Generic;
using UnityEngine;

public class MoveActionNode : ActionNode {
        protected Transform target;
        protected float moveSpeed;
        protected float stoppingDistance = .4f;
        protected List<Vector3> currentPath;
        protected int currentWaypointIndex;
        protected float pathRequestCooldown;
        protected float lastPathRequestTime;
        protected int agentId;
        protected Vector3 smoothedDirection;
        protected float turnSpeed = 10f;
        protected bool useRotate = false;
        
        
        
        public MoveActionNode(Transform transform, Transform target, float moveSpeed = 5f) 
            : base(transform) {
            this.target = target;
            this.moveSpeed = moveSpeed;
            
            this.agentId = transform.GetInstanceID();
            pathRequestCooldown = 1f; // 이거 경로를 분할해서 받아가지고 하나씩 받을 때 이거 중간에 넘겨주니까 이상하게 가는건데 이거는 금방 해결할 듯?
            
            // Debug.Log(agentId);
        }

        
        protected override NodeState DoEvaluate() {
            // if (target == null) return NodeState.Failure;
            //
            // float distanceToTarget = Vector3.Distance(transform.position, target.position);
            //
            // if (distanceToTarget <= stoppingDistance) {
            //     StopMoving();
            //     return NodeState.Success;
            // }
            //
            // if (Time.time - lastPathRequestTime > pathRequestCooldown) RequestNewPath();
            //
            //
            // // if (currentPath != null && currentPath.Count > 0)
            // //     return NodeState.Running;
            
            return NodeState.Running;
        }
        public override void FixedEvaluate()
        {
            // if (currentPath != null && currentPath.Count > 0) FollowPath();
        }

        protected void RequestNewPath() {
            
            lastPathRequestTime = Time.time;
            
            AStarManager.Instance.RequestPath(
                transform.position,
                target.position,
                agentId,
                OnPathReceived
            );
        }

        protected void OnPathReceived(List<Vector3> path)
        {
            // Debug.Log(path.Count);
            currentPath = path;
            
            currentWaypointIndex = 0;
        }

        protected void FollowPath() {
            if (currentWaypointIndex >= currentPath.Count) {
                currentPath = null;
                return;
            }
            Vector3 targetWaypoint = currentPath[currentWaypointIndex];
            Vector3 targetWaypointTrans = new Vector3(targetWaypoint.x, 0, targetWaypoint.z);
            Vector3 direction = (targetWaypointTrans - new Vector3(transform.position.x, 0, transform.position.z)).normalized;

            if (Vector3.Distance(new Vector3(transform.position.x, 0,transform.position.z), targetWaypointTrans) < stoppingDistance)
                currentWaypointIndex++;

            if (animator != null)
            {
                animator.SetFloat("moveX", direction.x);
                animator.SetFloat("moveY", direction.y);
            }

            smoothedDirection = Vector3.Slerp(smoothedDirection, direction, Time.fixedDeltaTime * turnSpeed);
            
             Vector3 newPos = new Vector3(smoothedDirection.x, 0, smoothedDirection.z) * moveSpeed;
             newPos.y = rigidbody.linearVelocity.y;
             rigidbody.linearVelocity = newPos;
            
            
            // Debug.Log(rigidbody.linearVelocity);
            Vector3 lookDirection = targetWaypointTrans - transform.position;
            lookDirection.y = 0;

            
            if (lookDirection != Vector3.zero) {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed);
            }
            
            // Debug.Log(Vector3.Distance(new Vector3(transform.position.x, 0,transform.position.z), targetWaypointTrans)); 이 
            // 오호라.. 이게 왜 처리가 안됨. rigidbody 저거 rotation을 x, z축 고정한게 뭐가 문제인거임?
            
            if (animator != null)
            {
                float velocity = rigidbody.linearVelocity.magnitude;
                animator.SetFloat("Speed", velocity);
                animator.SetBool("Moving", true);
            }
        }

        protected void StopMoving()
        {
            // Debug.Log("here");
            rigidbody.linearVelocity = Vector3.zero;
            if(currentPath != null)
                currentPath.Clear();
            

            if (animator != null)
            {
                animator.SetFloat("Speed", 0);
                animator.SetBool("Moving", false);
            }
            
        }


        public void SetTarget(Transform transform)
        {
            this.target = transform;
        }
    
}