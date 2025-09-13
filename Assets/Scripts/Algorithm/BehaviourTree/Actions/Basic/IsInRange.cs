using UnityEngine;

public class IsInRange : ConditionNode
{
    private Transform target;
    private float range;

    public IsInRange(Transform transform, Transform target, float range) : base(transform)
    {
        this.target = target;
        this.range = range;
    }

    public override NodeState Evaluate()
    {
        if(target == null)
        {
            state = NodeState.Failure;
            return state;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        state = distance <= range ? NodeState.Success : NodeState.Failure;
        return state;
    }
}