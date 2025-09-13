

using System;
using System.Collections.Generic;
using UnityEngine;


public class BehaviourTree
{
    private Node rootNode;
    private HashSet<ActionNode> activeActionNodes = new HashSet<ActionNode>();
    private bool wait;
    public BehaviourTree()
    {
        rootNode = SetupTree();
        wait = false;
    }
    public BehaviourTree(Transform transform, Node rootNode)
    {
        this.rootNode = rootNode;

    }
    public NodeState Update()
    {
        if (wait) return NodeState.Failure;
        
        if(rootNode != null)
            return rootNode.Evaluate();
        return NodeState.Failure;
    }
    public void FixedUpdate() {
        if (wait) return;
        if(rootNode != null) rootNode.FixedEvaluate();
    }

    protected virtual Node SetupTree()
    {
        return null;
    }

    public void SetRootNode(Node node)
    {
        rootNode = node;
    }
    public void SetWait(bool state)
    {
        this.wait = state;
    }
}