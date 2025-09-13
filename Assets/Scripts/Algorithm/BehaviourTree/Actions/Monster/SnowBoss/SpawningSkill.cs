using UnityEngine;

public class SpawningSkill : ActionNode
{
    public SpawningSkill(Transform transform, bool forceComplete = false, string cooldownKey = null) : base(transform, forceComplete, cooldownKey)
    {
        
    }

    protected override NodeState DoEvaluate()
    {
        throw new System.NotImplementedException();
    }
}