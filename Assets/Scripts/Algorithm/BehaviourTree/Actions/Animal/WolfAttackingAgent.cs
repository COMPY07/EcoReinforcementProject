using UnityEngine;

public class WolfAttackingAgent : ActionNode
{
    private BlackBoard blackBoard;
    private float damage;
    private EcosystemAgent targetAgent;
    private AlgorithmBaseEntity thisAgent;
    private float attackRange;
    private string targetKey;

    public WolfAttackingAgent(Transform transform, float damage, float attackRange, string targetBlackboardKey) : base(transform)
    {
        this.damage = damage;
        this.attackRange = attackRange;
        
        if (transform.TryGetComponent(out thisAgent))
        {
            blackBoard = thisAgent.GetBlack();
        }

        this.targetKey = targetBlackboardKey;
    }

    protected override NodeState DoEvaluate()
    {
        if (!UpdateTarget() || !CanAttack()) 
            return NodeState.Failure;
        Debug.Log("check!");
        
        targetAgent.TakeDamage(damage);
        
        
        
        return NodeState.Success;
    }

    private bool UpdateTarget()
    {
        if (blackBoard == null) return false;

        Transform currentTarget = blackBoard.Get<Transform>(targetKey);
        if (currentTarget == null) return false;

        targetAgent = currentTarget.GetComponent<EcosystemAgent>();
        return targetAgent != null;
    }

    private bool CanAttack()
    {
        if (targetAgent == null || !targetAgent.IsAlive) return false;
        
        Vector2 thisPos = new Vector2(transform.position.x, transform.position.z);
        Vector2 targetPos = new Vector2(targetAgent.transform.position.x, targetAgent.transform.position.z);
        
        float distance = Vector2.Distance(thisPos, targetPos);
        return distance <= attackRange;
    }
}