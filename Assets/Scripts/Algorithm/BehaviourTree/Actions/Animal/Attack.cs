using UnityEngine;

public class WolfAttacking : ActionNode
{
    private BlackBoard blackBoard;
    private float damage;

    private AlgorithmBaseEntity targetEntity, entity;
    private float attackRange;
    private string targetKey;
    public WolfAttacking(Transform transform, float damage,float attackRange, string targetBlackboardKey) : base(transform)
    {

        this.damage = damage;
        this.attackRange = attackRange;
        if (transform.TryGetComponent(out entity))
        {
            blackBoard = entity.GetBlack();
        }

        this.targetKey = targetBlackboardKey;
    }

    protected override NodeState DoEvaluate()
    {

        if (!UpdateTarget() || !CanAttack()) return NodeState.Failure;
        
        
        targetEntity.TakeDamage(damage, "Wolf Attack!!");
        if (!targetEntity.IsAlive) {
            entity.Eat(targetEntity.Energy);
        }
        
        return NodeState.Success;

    }

    private bool UpdateTarget()
    {
        if (blackBoard == null) return false;

        Transform currentEnttiy = blackBoard.Get<Transform>(targetKey);
        if (currentEnttiy == null) return false;

        
        
        targetEntity = currentEnttiy.GetComponent<AlgorithmBaseEntity>();

        return true;
    }


    private bool CanAttack()
    {
        if (targetEntity == null) return false;
        if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(targetEntity.transform.position.x, targetEntity.transform.position.z)) > attackRange) return false;

        return true;
    }
}