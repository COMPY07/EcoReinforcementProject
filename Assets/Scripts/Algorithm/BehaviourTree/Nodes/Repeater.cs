using UnityEngine;
using System.Collections.Generic;

public class Repeater : Node
{
    private int maxRepeats;
    private int currentRepeats;

    public Repeater(int maxRepeats = -1) : base()
    {
        this.maxRepeats = maxRepeats;
        this.currentRepeats = 0;
    }

    public Repeater(List<Node> children, int maxRepeats = -1) : base(children)
    {
        this.maxRepeats = maxRepeats;
        this.currentRepeats = 0;
    }

    protected override NodeState DoEvaluate()
    {
        if (children.Count != 1)
        {
            state = NodeState.Failure;
            return state;
        }

        if (maxRepeats != -1 && currentRepeats >= maxRepeats)
        {
            state = NodeState.Success;
            currentRepeats = 0;
            return state;
        }

        switch (children[0].Evaluate())
        {
            case NodeState.Running:
                state = NodeState.Running;
                return state;
            case NodeState.Failure:
                state = NodeState.Failure;
                currentRepeats = 0;
                return state;
            case NodeState.Success:
                currentRepeats++;
                if (maxRepeats == -1 || currentRepeats < maxRepeats)
                {
                    state = NodeState.Running;
                    return state;
                }
                else
                {
                    state = NodeState.Success;
                    currentRepeats = 0;
                    return state;
                }
        }

        state = NodeState.Success;
        return state;
        
    }

    public sealed override NodeState Evaluate()
    {
        return Evaluate();
    }

    public override void Interrupt()
    {
        
        if (children.Count == 1)
        {
            children[0].Interrupt();
        }
        
        base.Interrupt();
        currentRepeats = 0;
    }

    protected override void OnEnterNode()
    {
        currentRepeats = 0;
    }

    public override void FixedEvaluate()
    {
        if (children.Count == 1)
            children[0].FixedEvaluate();
    }
}