using System.Collections.Generic;
using UnityEngine;

public class MoveToTarget : MoveActionNode {


    public MoveToTarget(Transform transform, Transform target, float moveSpeed = 5f)
        : base(transform, target, moveSpeed)
    {
        pathRequestCooldown = .55f;
    }
        protected override NodeState DoEvaluate() {
            if (target == null) return NodeState.Failure;
            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            if (distanceToTarget <= stoppingDistance) {
            
                return NodeState.Success;
            }

            if (Time.time - lastPathRequestTime > pathRequestCooldown)
            {
                RequestNewPath();
            }

            // if (currentPath != null && currentPath.Count > 0)
            //     return NodeState.Running;

            return NodeState.Running;
        }
        public override void FixedEvaluate() {
            if (currentPath != null && currentPath.Count > 0) {
                FollowPath();
            }
        }
        
        public void SetTarget(Transform transform)
        {
            this.target = transform;
        }

        protected override void OnEnterNode()
        {
            // Debug.Log("chase");
        }
        protected override void OnExitNode()
        {
            
            StopMoving();
        }
}