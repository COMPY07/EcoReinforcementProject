using UnityEngine;

public class RushSkill : ActionNode
{

    private bool active;
    
    
    
    public RushSkill(Transform transform, bool forceComplete = false, string cooldownKey = null) : base(transform, forceComplete, cooldownKey)
    {
        
        active = false;
    }

    protected override NodeState DoEvaluate()
    {
        throw new System.NotImplementedException();
    }
    
    
}