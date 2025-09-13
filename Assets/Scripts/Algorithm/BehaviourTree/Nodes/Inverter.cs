using UnityEngine;
using System.Collections.Generic;

public class Inverter : Node
{
    public Inverter() : base() { }
    public Inverter(List<Node> children) : base(children) { }

    protected override NodeState DoEvaluate() {
        if(children.Count != 1)
        {
            state = NodeState.Failure;
            return state;
        }

        switch(children[0].Evaluate())
        {
            case NodeState.Failure:
                state = NodeState.Success;
                return state;
            case NodeState.Success:
                state = NodeState.Failure;
                return state;
            case NodeState.Running:
                state = NodeState.Running;
                return state;
        }
        state = NodeState.Success;
        return state;
        
    }   

    public sealed override NodeState Evaluate() 
    {
        return base.Evaluate();
    }

    public override void Interrupt()
    {
        if (children.Count == 1)
        {
            children[0].Interrupt();
        }
        
        base.Interrupt();
    }

    public override void FixedEvaluate()
    {
        if (children.Count == 1)
            children[0].FixedEvaluate();
    }
}