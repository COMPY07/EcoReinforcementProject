using System.Collections.Generic;
using UnityEngine;

public class Rabbit : AlgorithmBaseEntity
{

    private Rigidbody rigid;

    public GameObject rabbitPrefab;
    public void Awake()
    {
        base.Awake();

        rigid = GetComponent<Rigidbody>();
        moveSpeed = 5f;

        
    }

    // protected override void Update()
    // {
    //     base.Update();
    //     
    //     Debug.Log($"Update - Velocity: {rigid.linearVelocity}");
    // }
    // // Rabbit.cs
    // private void FixedUpdate()
    // {
    //     base.FixedUpdate();
    //     Debug.Log($"FixedUpdate - Velocity: {rigid.linearVelocity}");
    // }
    // private void LateUpdate()
    // {
    //     
    //     Debug.Log($"LateUpdate - Velocity: {rigid.linearVelocity}");
    // }
    
    protected override BehaviourTree CreateBehaviourTree() {
        BehaviourTree bt = new BehaviourTree();

        Node flee = new Sequence(new List<Node>()
            {
                // Transform transform, string tag = "Default", string layer = "Entity", 
                // float distance = 10f
                new IsInRangeTag(this.transform, "Wolf", "Entity", 15),
                // Transform transform, Transform target, float speed, float fleeDistance
                new FleeFromTarget(this.transform, "Wolf", moveSpeed)
            }
        
        );

        
        
        Node food = new Parallel(new List<Node>() {
            new Sequence(new List<Node>()
                {
                    new IsInRangeTag(this.transform, "Food", "Env", 50),
                    new MoveToTargetAnimal(transform, "Food", moveSpeed * 0.7f),
                }
            ),
            new Eating(transform, "Food", "Env")
        }, 1, 2);

        // Node reproduction = new Parallel(new List<Node>(){new Sequence(new List<Node>()
        // {
        //     new FindMate(transform, "Rabbit", "Entity", 15f),
        //     new ApproachMate(transform, moveSpeed * 0.8f, 2.0f),
        //     
        // }),
        //     new Mating(transform, 3f, 0.7f)
        //     }, 1, 2);
        //
        // Node pregnancy = new Sequence(new List<Node>()
        // {
        //     new PregnancyManager(transform, rabbitPrefab, 20f, 2),
        // });
        
        Node moveAround = new Sequence(
            new List<Node>()
            {
                new MoveAround(transform, moveSpeed * 0.6f)
            }
        );
        

        Node root = new Sequence(new List<Node>()
        {
            new BasicActivity(this.transform),
            new Selector(
                new List<Node>()
                {
                    flee,
                    new Breeding(transform, rabbitPrefab, "Rabbit"),
                    food,
                    moveAround
                }
            )
        });
        
        bt.SetRootNode(root);
        
        
        return bt;
    }
        
}