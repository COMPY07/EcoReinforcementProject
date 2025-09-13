using UnityEngine;

public class Breeding : ActionNode
{
    private AlgorithmBaseEntity entity;
    private BlackBoard blackboard;
    private string mateTag = "Animal";
    private float breedingRadius = 10f;
    private float breedingCooldown = 10f; // breeding.초마다 한 번씩만 교배 가능
    private GameObject offspringPrefab;
    
    public Breeding(Transform transform, GameObject offspringPrefab, string metaTag, float radius = 10f, float cooldown = 10f) 
        : base(transform)
    {
        this.offspringPrefab = offspringPrefab;
        this.breedingRadius = radius;
        this.breedingCooldown = cooldown;
        this.mateTag = metaTag;
        entity = transform.GetComponent<AlgorithmBaseEntity>();
        if (entity != null)
        {
            blackboard = entity.GetBlack();
        }
    }

    protected override NodeState DoEvaluate()
    {
        if (!CanBreed())
        {
            return NodeState.Failure;
        }
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, breedingRadius, LayerMask.GetMask("Entity"));
        
        foreach (Collider col in colliders)
        {
            if (col.transform == transform) continue;
            if (!col.CompareTag(mateTag)) continue;
            
            AlgorithmBaseEntity mate = col.GetComponent<AlgorithmBaseEntity>();
            if (mate == null || !mate.IsAlive) continue;
            
            if (IsMateValid(mate))
            {
                BreedWith(mate);
                return NodeState.Success;
            }
        }
        
        return NodeState.Failure;
    }
    
    private bool CanBreed()
    {
        if (entity == null || !entity.IsAlive) return false;
        if (!entity.CanReproduce() || Bug.count > 120) return false;
        
        float lastBreedTime = blackboard?.Get<float>("lastBreedTime") ?? 0f;
        return Time.time - lastBreedTime >= breedingCooldown;
    }
    
    private bool IsMateValid(AlgorithmBaseEntity mate)
    {
        if (!mate.CanReproduce()) return false;
        
        BlackBoard mateBlackboard = mate.GetBlack();
        float mateLastBreedTime = mateBlackboard?.Get<float>("lastBreedTime") ?? 0f;
        
        return Time.time - mateLastBreedTime >= breedingCooldown;
    }
    
    private void BreedWith(AlgorithmBaseEntity mate)
    {
        
        float currentTime = Time.time;
        blackboard?.Set("lastBreedTime", currentTime);
        mate.GetBlack()?.Set("lastBreedTime", currentTime);
        
        entity.Reproduce(mate);
        
        SpawnOffspring();
    }
    
    private void SpawnOffspring()
    {
        if (offspringPrefab == null) return;
        
        int count = Random.Range(1, 3);
        
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + Random.insideUnitSphere * 3f;
            spawnPos.y = transform.position.y;
            
            GameObject offspring = Object.Instantiate(offspringPrefab, spawnPos, Quaternion.identity);
            
        }
    }
}
