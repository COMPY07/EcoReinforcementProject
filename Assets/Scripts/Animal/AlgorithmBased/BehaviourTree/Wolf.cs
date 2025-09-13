using System;
using System.Collections.Generic;
using UnityEngine;

public class Wolf : AlgorithmBaseEntity
{

    public GameObject wolfPrefab;

    public void LateUpdate()
    {
        
    }

    protected override BehaviourTree CreateBehaviourTree()
    {

        var bt = new BehaviourTree();
        
        // Transform transform, float speed = 10f, float wanderRadius = 10f, float wanderTime = 5f)
        Node moveAround = new MoveAround(transform, moveSpeed * .6f);
        
    // public Breeding(Transform transform, GameObject offspringPrefab, string metaTag, float radius = 10f, float cooldown = 10f)
        Node reproductive = new Breeding(transform, wolfPrefab, "Wolf");
        
        Node food = new Parallel(new List<Node>() {
            new Sequence(new List<Node>()
            {
                new IsInRangeTag(this.transform, "Rabbit", "Entity", 30),
                new MoveToTargetAnimal(transform, "Rabbit", moveSpeed, .5f, .2f),
            }),
            new WolfAttackingAgent(transform, 100, 2, "Rabbit")
        }, 1, 2);
        
        

        Node root = new Sequence(new List<Node>()
        {
            // new BasicActivity(transform),
            new Selector(new List<Node>()
            {
                // reproductive,
                food,
                moveAround
            }),
        });
        bt.SetRootNode(root);

        return bt;

    }
        
}