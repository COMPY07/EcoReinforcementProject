using System;
using UnityEngine;
using System.Collections;

public abstract class AlgorithmBaseEntity : MonoBehaviour
{
    [Header("기본 정보")]
    [SerializeField] protected string entityName;
    [SerializeField] protected int entityId;
    [SerializeField] protected bool isAlive = true;
    [SerializeField] protected float age = 0f;
    [SerializeField] protected float maxAge = 100f;
    
    [Header("생존 요소")]
    [SerializeField] protected float health = 100f;
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float energy = 100f;
    [SerializeField] protected float maxEnergy = 100f;
    [SerializeField] protected float hunger = 0f;
    [SerializeField] protected float maxHunger = 100f;
    [SerializeField] protected float thirst = 0f;
    [SerializeField] protected float maxThirst = 100f;
    [SerializeField] protected float hydration = 100f; 
    [SerializeField] protected float maxHydration = 100f;
    
    [Header("이동 및 행동")]
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float rotationSpeed = 90f;
    [SerializeField] protected float detectionRange = 10f;
    [SerializeField] protected Vector3 targetPosition;
    [SerializeField] protected bool isMoving = false;
    
    [Header("환경 적응")]
    [SerializeField] protected float temperature = 20f;
    [SerializeField] protected float optimalTemperature = 20f;
    [SerializeField] protected float temperatureTolerance = 10f;
    [SerializeField] protected float stressLevel = 0f;
    [SerializeField] protected float maxStress = 100f;
    
    [Header("번식")]
    [SerializeField] protected bool canReproduce = true;
    [SerializeField] protected float reproductionCooldown = 0f;
    [SerializeField] protected float reproductionThreshold = 50f; 
    [SerializeField] protected int reproductionCount = 0;
    
    [Header("시간 설정")]
    [SerializeField] protected float timeScale = 1f;
    [SerializeField] protected float deltaTime;


    private BehaviourTree bt;
    private BlackBoard bb;    
    
    public System.Action<AlgorithmBaseEntity> OnEntityDeath;
    public System.Action<AlgorithmBaseEntity, AlgorithmBaseEntity> OnEntityReproduction;
    public System.Action<AlgorithmBaseEntity, float> OnHealthChanged;
    public System.Action<AlgorithmBaseEntity, float> OnEnergyChanged;
    
    
    public string EntityName => entityName;
    public int EntityId => entityId;
    public bool IsAlive => isAlive;
    public float Age => age;
    public float Health => health;
    public float Energy => energy;
    public float Hunger => hunger;
    public float Thirst => thirst;
    public float Hydration => hydration;
    public Vector3 Position => transform.position;
    public float StressLevel => stressLevel;
    
    protected virtual void Awake()
    {
        if (entityId == 0)
            entityId = GetInstanceID();
            
        if (string.IsNullOrEmpty(entityName))
            entityName = gameObject.name;
            
        targetPosition = transform.position;
        bb = new BlackBoard();
        
    }
    
    protected virtual void Start()
    {
        GameObject movementTarget = new GameObject(EntityName+"_movement_target");
        GetBlack().Set("movementTarget", movementTarget);
        
        InitializeEntity();
        
        
    }
    
    protected virtual void Update()
    {
        if (!isAlive) return;
        
        deltaTime = Time.deltaTime * timeScale;

        if (bt != null) bt.Update();
        
        
        
        
    }


    public BlackBoard GetBlack()
    {
        return bb;
    }
    protected virtual BehaviourTree CreateBehaviourTree()
    {
        return new BehaviourTree(this.transform, null);
    }
    
    protected void FixedUpdate()
    {
        
        if (bt != null)
        {
            bt.FixedUpdate();
        }
        
    }

    protected virtual void InitializeEntity() {
        
        bt = CreateBehaviourTree();
    }
    
    public virtual void UpdateAge()
    {
        age += deltaTime;
        
        if (age >= maxAge)
        {
            Die("수명");
        }
    }
    
    public virtual void UpdateVitalSigns()
    {
        float baseConsumption = deltaTime;
        
        energy = Mathf.Max(0, energy - baseConsumption * 0.5f);
        
        hunger = Mathf.Min(maxHunger, hunger + baseConsumption * 0.8f);
        
        thirst = Mathf.Min(maxThirst, thirst + baseConsumption * 1.2f);
        
        hydration = Mathf.Max(0, hydration - baseConsumption * 0.3f);
        
        if (isMoving)
        {
            energy -= baseConsumption * 1.5f;
            thirst += baseConsumption * 0.5f;
            hydration -= baseConsumption * 0.2f;
        }
        
        if (energy > 50 && hunger < 50 && thirst < 50)
        {
            health = Mathf.Min(maxHealth, health + baseConsumption * 0.2f);
        }
        else if (energy < 20 || hunger > 80 || thirst > 80 || hydration < 20)
        {
            health = Mathf.Max(0, health - baseConsumption * 2f);
        }
        
        OnHealthChanged?.Invoke(this, health);
        OnEnergyChanged?.Invoke(this, energy);
    }
    
    // public virtual void UpdateStress()
    // {
    //     float targetStress = 0f;
    //     
    //     // 온도에 따른 스트레스
    //     float tempDiff = Mathf.Abs(temperature - optimalTemperature);
    //     if (tempDiff > temperatureTolerance)
    //     {
    //         targetStress += (tempDiff - temperatureTolerance) * 2f;
    //     }
    //     
    //     // 생존 요소에 따른 스트레스
    //     if (health < 30) targetStress += 20f;
    //     if (energy < 20) targetStress += 15f;
    //     if (hunger > 70) targetStress += 10f;
    //     if (thirst > 70) targetStress += 15f;
    //     if (hydration < 30) targetStress += 10f;
    //     
    //     // 스트레스 조절
    //     if (stressLevel < targetStress)
    //         stressLevel = Mathf.Min(maxStress, stressLevel + deltaTime * 10f);
    //     else if (stressLevel > targetStress)
    //         stressLevel = Mathf.Max(0, stressLevel - deltaTime * 5f);
    // }
    //
    protected virtual void UpdateMovement()
    {
        if (!isMoving) return;
        
        Vector3 direction = (targetPosition - transform.position).normalized;
        
        if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position += direction * moveSpeed * deltaTime;
            
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * deltaTime);
            }
        }
        else
        {
            isMoving = false;
        }
    }
    
    protected virtual void UpdateBehavior()
    {
       
    }
    
    public virtual void CheckSurvival()
    {
        if (health <= 0)
            Die("체력 고갈");
        
        else if (energy <= 0)
            Die("에너지 고갈");
        
        else if (thirst >= maxThirst)
            Die("탈수");
        
        else if (hydration <= 0)
            Die("수분 부족");
        
        else if (hunger >= maxHunger)
            Die("기아");
        
        else if (stressLevel >= maxStress)
            Die("스트레스");
        
    }
    
    public virtual void MoveTo(Vector3 destination)
    {
        targetPosition = destination;
        isMoving = true;
    }
    
    public virtual void Eat(float nutritionValue)
    {
        energy = Mathf.Min(maxEnergy, energy + nutritionValue * 0.8f);
        hunger = Mathf.Max(0, hunger - nutritionValue);
        health = Mathf.Min(maxHealth, health + nutritionValue * 0.1f);
    }
    
    public virtual void Drink(float hydrationValue)
    {
        hydration = Mathf.Min(maxHydration, hydration + hydrationValue);
        thirst = Mathf.Max(0, thirst - hydrationValue * 0.8f);
        energy = Mathf.Min(maxEnergy, energy + hydrationValue * 0.2f);
    }
    
    public virtual void TakeDamage(float damage, string cause = "")
    {
        if (!isAlive) return;
        
        health = Mathf.Max(0, health - damage);
        stressLevel = Mathf.Min(maxStress, stressLevel + damage * 0.5f);
        
        OnHealthChanged?.Invoke(this, health);
        
        if (health <= 0)
        {
            Die(string.IsNullOrEmpty(cause) ? "손상" : cause);
        }
    }
    
    public virtual void Die(string cause)
    {
        if (!isAlive) return;
        
        isAlive = false;
        Debug.Log($"{entityName}(ID: {entityId})가 {cause}으로 사망했습니다. 나이: {age:F1}");
        
        OnEntityDeath?.Invoke(this);
        
        StartCoroutine(DeathSequence());
    }
    
    protected virtual IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(1f);
        
        gameObject.SetActive(false);
    }
    
    public virtual bool CanReproduce()
    {
        return isAlive && canReproduce && 
               energy >= reproductionThreshold && 
               health > 50 && 
               reproductionCooldown <= 0 &&
               stressLevel < 50;
    }
    
    public virtual void Reproduce(AlgorithmBaseEntity partner)
    {
        if (!CanReproduce() || partner == null || !partner.CanReproduce())
            return;
            
        energy -= reproductionThreshold * 0.5f;
        partner.energy -= partner.reproductionThreshold * 0.5f;
        
        reproductionCooldown = 30f;
        partner.reproductionCooldown = 30f;
        
        reproductionCount++;
        partner.reproductionCount++;
        
        OnEntityReproduction?.Invoke(this, partner);
    }
    
    public virtual void SetTemperature(float temp)
    {
        temperature = temp;
    }

    public void OnDestroy()
    {
        Destroy(bb.Get<GameObject>("movementTarget"));
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        if (isMoving)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(targetPosition, 0.5f);
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }
}