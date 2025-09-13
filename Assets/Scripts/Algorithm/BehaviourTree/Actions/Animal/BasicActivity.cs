using UnityEngine;

public class BasicActivity : ConditionNode
{
    private AlgorithmBaseEntity entity;
    
    public BasicActivity(Transform transform) : base(transform)
    {
        this.transform = transform;
        entity = transform.GetComponent<AlgorithmBaseEntity>();
    }


    public override NodeState Evaluate()
    {
        if (entity == null || !entity.IsAlive) return NodeState.Failure;
        
        
        entity.UpdateAge();
        entity.UpdateVitalSigns();
        entity.CheckSurvival();

        
        return NodeState.Success;
    }
}