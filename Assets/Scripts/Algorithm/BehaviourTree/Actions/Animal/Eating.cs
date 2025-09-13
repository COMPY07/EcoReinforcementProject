using Unity.VisualScripting;
using UnityEngine;

public class Eating : ActionNode
{
    private string targetTag;
    private string layer;
    private AlgorithmBaseEntity entity;
    private BlackBoard blackboard;
    
    public Eating(Transform transform, string targetTag, string layer) : base(transform)
    {
        this.targetTag = targetTag;
        this.layer = layer;
        this.transform = transform;
        entity = this.transform.GetComponent<AlgorithmBaseEntity>();
        
        if (entity != null)
        {
            blackboard = entity.GetBlack();
        }
    }

    protected override NodeState DoEvaluate() 
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 1, LayerMask.GetMask(layer));
        
        foreach (Collider col in colliders)
        {
            if (col.CompareTag(targetTag))
            {
                Food food = col.GetComponent<Food>();
                if (food != null)
                {
                    entity.Eat(food.Eat());
                    
                    ClearCurrentTarget();
                    
                    // Debug.Log($"Ate food: {col.name}");
                    return NodeState.Success;
                }
            }
        }

        return NodeState.Failure;
    }
    
    private void ClearCurrentTarget()
    {
        if (blackboard != null)
        {
            blackboard.Set<Transform>("currentTarget", null);
            blackboard.Set("isMovingToTarget", false);
            blackboard.Set("hasTarget", false);
            
            blackboard.Set("isHungry", false); 
        }
    }
}