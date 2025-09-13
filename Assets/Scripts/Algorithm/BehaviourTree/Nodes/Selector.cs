using System.Collections.Generic;

public class Selector : Node
{
    private Node sucessNode = null;
    private Node previousSuccessNode = null;
    
    public Selector() : base() { }
    public Selector(List<Node> children) : base(children) { }

    protected override NodeState DoEvaluate()
    {
        foreach (Node node in children)
        {
            switch (node.Evaluate())
            {
                case NodeState.Failure:
                    continue;
                case NodeState.Success: 
                    if (previousSuccessNode != null && previousSuccessNode != node)
                    {
                        previousSuccessNode.Interrupt();
                    }
                    state = NodeState.Success;
                    sucessNode = node;
                    previousSuccessNode = node;
                    return state;
                case NodeState.Running:
                    
                    if (previousSuccessNode != null && previousSuccessNode != node)
                    {
                        previousSuccessNode.Interrupt();
                    }
                    state = NodeState.Running;
                    sucessNode = node;
                    previousSuccessNode = node;
                    return state;
                default:
                    continue;
            }
        }
        
        if (previousSuccessNode != null)
        {
            previousSuccessNode.Interrupt();
            previousSuccessNode = null;
        }
        
        sucessNode = null;
        state = NodeState.Failure;
        return state;
        
    }

    public sealed override NodeState Evaluate()
    {
        return base.Evaluate();
    }

    public override void Interrupt() {
        if (sucessNode != null)
        {
            sucessNode.Interrupt();
        }
        
        base.Interrupt();
        sucessNode = null;
        previousSuccessNode = null;
    }

    public override void FixedEvaluate()
    {
        if(sucessNode != null) sucessNode.FixedEvaluate();
    }
}