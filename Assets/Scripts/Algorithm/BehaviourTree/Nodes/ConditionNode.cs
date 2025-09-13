using UnityEngine;

public abstract class ConditionNode : Node
{
    protected Transform transform;
    protected Animator animator;
    protected Rigidbody rigidbody;

    public ConditionNode(Transform transform)
    {
        this.transform = transform;
        animator = transform.GetComponent<Animator>();
        rigidbody = transform.GetComponent<Rigidbody>();
    }

    protected sealed override NodeState DoEvaluate()
    {
        return Evaluate();
    }

    public abstract override NodeState Evaluate();
    
    public override void Interrupt()
    {
        if (wasRunning)
        {
            OnInterrupted?.Invoke();
            state = NodeState.Failure;
            wasRunning = false;
        }
    }

    public override void FixedEvaluate()
    {
    }
}