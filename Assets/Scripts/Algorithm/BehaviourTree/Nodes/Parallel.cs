using UnityEngine;
using System.Collections.Generic;

public class Parallel : Node
{
    private int successThreshold;
    private int failureThreshold;

    public Parallel(int successThreshold, int failureThreshold) : base()
    {
        this.successThreshold = successThreshold;
        this.failureThreshold = failureThreshold;
    }

    public Parallel(List<Node> children, int successThreshold, int failureThreshold) : base(children)
    {
        this.successThreshold = successThreshold;
        this.failureThreshold = failureThreshold;
    }

    protected override NodeState DoEvaluate()
    {
        int successCount = 0;
        int failureCount = 0;

        foreach (Node child in children)
        {
            NodeState childState = child.Evaluate();
            if (childState == NodeState.Success)
                successCount++;
            else if (childState == NodeState.Failure)
                failureCount++;
        }

        if (successCount >= successThreshold)
        {
            state = NodeState.Success;
            return state;
        }

        if (failureCount >= failureThreshold)
        {
            state = NodeState.Failure;
            return state;
        }

        state = NodeState.Running;
        return state;
        
    }

    public sealed override NodeState Evaluate()
    {
        return base.Evaluate();
    }

    public override void Interrupt() {
        foreach (Node child in children)
        {
            child.Interrupt();
        }
        
        base.Interrupt();
    }
}