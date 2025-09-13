using System.Collections.Generic;
using UnityEngine;

public class Sequence : Node
{
    private int idx = 0; 
    private int previousIdx = -1;
    
    public Sequence() : base() { }
    public Sequence(List<Node> children) : base(children) { }

    protected override NodeState DoEvaluate()
    {
        bool anyChildIsRunning = false;
        
        for (int i = 0; i < children.Count; i++)
        {
            Node node = children[i];
            switch (node.Evaluate()) 
            {
                case NodeState.Failure:
                    state = NodeState.Failure;
                    idx = i;
                    return state;
                case NodeState.Success:
                    continue;
                case NodeState.Running:
                    anyChildIsRunning = true;
                    
                    if (previousIdx != -1 && previousIdx != i)
                    {
                        for (int j = previousIdx; j < i && j < children.Count; j++)
                        {
                            children[j].Interrupt();
                        }
                    }
                    idx = i;
                    previousIdx = i;
                    continue;
                default:
                    state = NodeState.Success;
                    return state;
            }
        }

        idx = children.Count;
        previousIdx = -1;
        state = anyChildIsRunning ? NodeState.Running : NodeState.Success;
        return state;
        
    }

    public sealed override NodeState Evaluate() 
    {
        return base.Evaluate();
    }

    public override void Interrupt() {
        for (int i = 0; i <= idx && i < children.Count; i++)
        {
            children[i].Interrupt();
        }
        
        base.Interrupt();
        idx = 0;
        previousIdx = -1;
    }

    protected override void OnExitNode()
    {
        idx = 0;
        previousIdx = -1;
    }

    public override void FixedEvaluate()
    {
        for (int i = 0; i < idx && i < children.Count; i++)
        {
            children[i].FixedEvaluate();
        }
    }
}