using System.Collections.Generic;
using System;
using UnityEngine;

public abstract class Node {
    protected NodeState state;
    public Node parent;
    protected List<Node> children = new List<Node>();
    
    protected bool wasRunning = false;
    public Action OnInterrupted;
    
    public Node()
    {
        parent = null;
    }

    public Node(List<Node> children)
    {
        foreach (Node child in children)
            Attach(child);
    }

    private void Attach(Node node) {
        node.parent = this;
        children.Add(node);
    }

    public virtual NodeState Evaluate()
    {
        bool isCurrentlyRunning = (state == NodeState.Running);
        
        state = DoEvaluate();
        
        if (wasRunning && state != NodeState.Running)
        {
            OnExitNode();
        }
        else if (!wasRunning && state == NodeState.Running)
        {
            OnEnterNode();
        }
        
        wasRunning = (state == NodeState.Running);
        return state;
    }
    
    public virtual void Interrupt()
    {
        foreach (Node child in children) child.Interrupt();

        if (wasRunning)
        {
            OnInterrupted?.Invoke();
            OnExitNode();
            state = NodeState.Failure;
            wasRunning = false;
        }
        
    }

    protected abstract NodeState DoEvaluate();
    
    protected virtual void OnEnterNode() { }
    protected virtual void OnExitNode() { }

    public virtual void FixedEvaluate()
    {
        foreach(Node child in children) 
            child.FixedEvaluate();
    }
}