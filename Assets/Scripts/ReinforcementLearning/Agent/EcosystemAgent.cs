
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class EcosystemAgent : Agent
{
    [Header("Species Configuration")]
    public SpeciesData species;
    
    [Header("Runtime Stats")]
    public float health;
    public float energy;
    public float age;
    
    [Header("Pathfinding Settings")]
    [SerializeField] private float pathUpdateInterval = 0.5f;
    [SerializeField] private float pathFollowDistance = 1.5f;
    [SerializeField] private float stuckThreshold = 0.1f;
    [SerializeField] private float stuckCheckTime = 2f;
    
    protected Rigidbody rb;
    protected Renderer bodyRenderer;
    protected bool isAlive = true;
    
    protected List<GameObject> nearbyAgents = new List<GameObject>();
    protected List<GameObject> nearbyFood = new List<GameObject>();
    protected List<GameObject> nearbyPredators = new List<GameObject>();
    protected List<GameObject> nearbyPrey = new List<GameObject>();
    
    protected List<Vector3> currentPath;
    protected int currentPathIndex = 0;
    protected float lastPathRequest = 0f;
    protected Vector3 currentTarget;
    protected bool isFollowingPath = false;
    protected bool hasValidTarget = false;
    
    protected Vector3 lastPosition;
    protected float stuckTimer = 0f;
    protected int agentId;
    
    public System.Action<EcosystemAgent> OnDeath;
    public System.Action<EcosystemAgent, EcosystemAgent> OnReproduction;

    public bool IsAlive => isAlive;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyRenderer = GetComponentInChildren<Renderer>();
        agentId = GetInstanceID(); 
        InitializeFromSpecies();
    }

    private void InitializeFromSpecies()
    {
        if (species == null) return;
        
        health = species.maxHealth;
        energy = species.maxEnergy;
        age = 0f;
        
        transform.localScale = species.bodyScale * species.size;
        
        if (bodyRenderer && species.bodyMaterial)
            bodyRenderer.material = species.bodyMaterial;
            
        currentPath = null;
        currentPathIndex = 0;
        isFollowingPath = false;
        hasValidTarget = false;
        lastPosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        InitializeFromSpecies();
        isAlive = true;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
        }
        
        transform.position = GetValidSpawnPosition();
        transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        
        EcosystemManager manager = FindObjectOfType<EcosystemManager>();
        if (manager != null)
        {
            manager.RepositionPredator();
        }
    }

    private Vector3 GetValidSpawnPosition()
    {
        if (AStarManager.Instance != null)
        {
            Vector3 centerPos = new Vector3(25f, 1f, 25f); // 맵 중앙
            return AStarManager.Instance.GetRandomWalkablePosition(centerPos, 20f);
        }
        
        float x = Random.Range(5f, 45f);
        float z = Random.Range(5f, 45f);
        return new Vector3(x, 1f, z);
    }

    private void FixedUpdate()
    {
        if (!isAlive || species == null) return;
        
        UpdateVitals();
        UpdatePerception();
        UpdateMovement();
        CheckIfStuck();
    }

    private void UpdateVitals()
    {
        age += Time.fixedDeltaTime;
        energy -= species.energyDecayRate * Time.fixedDeltaTime;
        
        if (energy <= 0f)
        {
            health -= 20f * Time.fixedDeltaTime;
        }
        
        if (health <= 0f || age >= species.maxAge)
        {
            Die();
        }
    }

    private void UpdatePerception()
    {
        ClearPerceptionLists();
        
        Collider[] detected = Physics.OverlapSphere(transform.position, species.visionRange);
        
        foreach (var collider in detected)
        {
            if (collider.gameObject == gameObject) continue;
            
            if (IsInVisionCone(collider.transform.position))
            {
                CategorizeObject(collider.gameObject);
            }
        }
    }

    private void UpdateMovement()
    {
        if (isFollowingPath && currentPath != null && currentPath.Count > 0)
        {
            FollowPath();
        }
    }

    private void CheckIfStuck()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        
        if (distanceMoved < stuckThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > stuckCheckTime)
            {
                RequestNewRandomPath();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        
        lastPosition = transform.position;
    }

    private void RequestNewRandomPath()
    {
        if (AStarManager.Instance == null) return;
        
        Vector3 randomTarget = AStarManager.Instance.GetRandomWalkablePosition(
            transform.position, 10f);
        RequestPathTo(randomTarget);
    }

    private void RequestPathTo(Vector3 target)
    {
        if (AStarManager.Instance == null || Time.time - lastPathRequest < pathUpdateInterval)
            return;
            
        lastPathRequest = Time.time;
        currentTarget = target;
        hasValidTarget = true;
        
        AStarManager.Instance.RequestPath(
            transform.position, 
            target, 
            agentId, 
            OnPathReceived);
    }

    private void OnPathReceived(List<Vector3> path)
    {
        if (path == null || path.Count == 0)
        {
            currentPath = null;
            isFollowingPath = false;
            return;
        }
        
        currentPath = path;
        currentPathIndex = 0;
        isFollowingPath = true;
        
        Debug.Log($"[{gameObject.name}] Received path with {path.Count} waypoints");
    }

    private void FollowPath()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count)
        {
            isFollowingPath = false;
            return;
        }
        
        Vector3 targetWaypoint = currentPath[currentPathIndex];
        Vector3 direction = (targetWaypoint - transform.position).normalized;
        float distanceToWaypoint = Vector3.Distance(transform.position, targetWaypoint);
        
        if (distanceToWaypoint < pathFollowDistance)
        {
            currentPathIndex++;
            if (currentPathIndex >= currentPath.Count)
            {
                isFollowingPath = false;
                hasValidTarget = false;
                return;
            }
        }
        
        MoveInDirection(direction);
    }

    private void MoveInDirection(Vector3 direction)
    {
        if (direction.magnitude < 0.1f) return;
        
        Vector3 targetVelocity = direction * species.moveSpeed;
        targetVelocity.y = 0f;
        
        rb.linearVelocity = targetVelocity;
        
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 
            species.turnSpeed * Time.fixedDeltaTime);
        
        energy -= direction.magnitude * 0.1f;
    }

    private void ClearPerceptionLists()
    {
        nearbyAgents.Clear();
        nearbyFood.Clear();
        nearbyPredators.Clear();
        nearbyPrey.Clear();
    }

    private bool IsInVisionCone(Vector3 targetPos)
    {
        Vector3 dirToTarget = (targetPos - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToTarget);
        return angle <= species.visionAngle / 2f;
    }

    private void CategorizeObject(GameObject obj)
    {
        EcosystemAgent otherAgent = obj.GetComponent<EcosystemAgent>();
        
        if (otherAgent != null)
        {
            nearbyAgents.Add(obj);
            
            if (species.predatorTags.Contains(otherAgent.species.speciesName))
                nearbyPredators.Add(obj);
            else if (species.preyTags.Contains(otherAgent.species.speciesName))
                nearbyPrey.Add(obj);
        }
        else
        {
            foreach (string foodTag in species.foodTags)
            {
                if (obj.CompareTag(foodTag))
                {
                    nearbyFood.Add(obj);
                    break;
                }
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (species == null) return;
        
        sensor.AddObservation(health / species.maxHealth);
        sensor.AddObservation(energy / species.maxEnergy);
        sensor.AddObservation(age / species.maxAge);
        
        sensor.AddObservation(transform.position / 50f);
        sensor.AddObservation(rb.linearVelocity / species.moveSpeed);
        sensor.AddObservation(transform.forward);
        
        sensor.AddObservation(nearbyFood.Count / 10f);
        sensor.AddObservation(nearbyPredators.Count / 10f);
        sensor.AddObservation(nearbyPrey.Count / 10f);
        sensor.AddObservation(nearbyAgents.Count / 10f);
        
        Vector3 nearestFoodDir = GetDirectionToNearest(nearbyFood);
        Vector3 nearestPredatorDir = GetDirectionToNearest(nearbyPredators);
        Vector3 nearestPreyDir = GetDirectionToNearest(nearbyPrey);
        
        sensor.AddObservation(nearestFoodDir);
        sensor.AddObservation(nearestPredatorDir);
        sensor.AddObservation(nearestPreyDir);
        
        sensor.AddObservation(isFollowingPath ? 1f : 0f);
        sensor.AddObservation(hasValidTarget ? 1f : 0f);
        
        Vector3 targetDirection = Vector3.zero;
        if (hasValidTarget && currentPath != null && currentPath.Count > 0)
        {
            if (currentPathIndex < currentPath.Count)
            {
                targetDirection = (currentPath[currentPathIndex] - transform.position).normalized;
            }
        }
        sensor.AddObservation(targetDirection);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isAlive || species == null) return;
        
        ProcessMovement(actions.ContinuousActions);
        ProcessAction(actions.DiscreteActions);
        
        float reward = CalculateReward();
        AddReward(reward);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;
        
        if (species == null || !isAlive)
        {
            for (int i = 0; i < continuousActionsOut.Length; i++)
                continuousActionsOut[i] = 0f;
            for (int i = 0; i < discreteActionsOut.Length; i++)
                discreteActionsOut[i] = 0;
            return;
        }
        
        Vector3 moveDirection = Vector3.zero;
        ActionType chosenAction = ActionType.DoNothing;
        
        if (nearbyPredators.Count > 0)
        {
            GameObject nearestPredator = GetNearestObject(nearbyPredators);
            if (nearestPredator != null)
            {
                Vector3 fleeDirection = (transform.position - nearestPredator.transform.position).normalized;
                Vector3 fleeTarget = transform.position + fleeDirection * 10f;
                RequestPathTo(fleeTarget);
                chosenAction = ActionType.Flee;
            }
        }
        else if (species.canAttack && nearbyPrey.Count > 0)
        {
            GameObject nearestPrey = GetNearestObject(nearbyPrey);
            if (nearestPrey != null)
            {
                RequestPathTo(nearestPrey.transform.position);
                
                if (Vector3.Distance(transform.position, nearestPrey.transform.position) < 3f)
                {
                    chosenAction = ActionType.Attack;
                }
            }
        }
        else if (energy < species.maxEnergy * 0.6f && nearbyFood.Count > 0)
        {
            GameObject nearestFood = GetNearestObject(nearbyFood);
            if (nearestFood != null)
            {
                RequestPathTo(nearestFood.transform.position);
                
                if (Vector3.Distance(transform.position, nearestFood.transform.position) < 2f)
                {
                    chosenAction = ActionType.Eat;
                }
            }
        }
        else if (species.canReproduce && energy >= species.reproductionThreshold)
        {
            foreach (var agent in nearbyAgents)
            {
                EcosystemAgent partner = agent.GetComponent<EcosystemAgent>();
                if (partner && partner.species == species && partner.energy >= species.reproductionThreshold)
                {
                    RequestPathTo(partner.transform.position);
                    
                    if (Vector3.Distance(transform.position, partner.transform.position) < 3f)
                    {
                        chosenAction = ActionType.Reproduce;
                        break;
                    }
                }
            }
        }
        else if (!isFollowingPath)
        {
            RequestNewRandomPath();
        }
        
        if (isFollowingPath && currentPath != null && currentPathIndex < currentPath.Count)
        {
            Vector3 targetWaypoint = currentPath[currentPathIndex];
            moveDirection = (targetWaypoint - transform.position).normalized;
        }
        
        if (continuousActionsOut.Length >= 2)
        {
            continuousActionsOut[0] = moveDirection.x;
            continuousActionsOut[1] = moveDirection.z;
        }
        
        if (discreteActionsOut.Length > 0)
        {
            discreteActionsOut[0] = (int)chosenAction;
        }
    }

    private void ProcessMovement(ActionSegment<float> actions)
    {
        if (actions.Length < 2) return;
    
        Vector3 moveDirection = new Vector3(actions[0], 0, actions[1]);
        
        if (moveDirection.magnitude > 0.1f)
        {
            Vector3 targetPos = transform.position + moveDirection * 5f;
            if (Time.time - lastPathRequest > pathUpdateInterval)
            {
                RequestPathTo(targetPos);
            }
        }
        
        Vector3 finalDirection = moveDirection.magnitude > 0.1f ? moveDirection : Vector3.zero;
        
        if (finalDirection.magnitude > 0.1f)
        {
            MoveInDirection(finalDirection);
        }
        
        energy -= moveDirection.magnitude * 0.1f;
    }

    private void ProcessAction(ActionSegment<int> actions)
    {
        if (actions.Length == 0) return;
        
        ActionType actionType = (ActionType)actions[0];
        
        switch (actionType)
        {
            case ActionType.DoNothing:
                break;
            case ActionType.Eat:
                if (species.canEat) TryEat();
                break;
            case ActionType.Attack:
                if (species.canAttack) TryAttack();
                break;
            case ActionType.Reproduce:
                if (species.canReproduce) TryReproduce();
                break;
            case ActionType.Flee:
                if (species.canFlee) TryFlee();
                break;
        }
    }

    private bool TryEat()
    {
        GameObject nearestFood = GetNearestObject(nearbyFood);
        if (nearestFood && Vector3.Distance(transform.position, nearestFood.transform.position) < 2f)
        {
            Destroy(nearestFood);
            energy = Mathf.Min(energy + 30f, species.maxEnergy);
            AddReward(species.feedingReward);
            return true;
        }
        return false;
    }

    private bool TryAttack()
    {
        GameObject nearestPrey = GetNearestObject(nearbyPrey);
        if (nearestPrey && Vector3.Distance(transform.position, nearestPrey.transform.position) < 3f)
        {
            EcosystemAgent preyAgent = nearestPrey.GetComponent<EcosystemAgent>();
            if (preyAgent)
            {
                preyAgent.TakeDamage(50f);
                energy = Mathf.Min(energy + 20f, species.maxEnergy);
                AddReward(species.feedingReward);
                return true;
            }
        }
        return false;
    }

    private bool TryReproduce()
    {
        if (energy < species.reproductionThreshold) return false;
        
        foreach (var agent in nearbyAgents)
        {
            EcosystemAgent partner = agent.GetComponent<EcosystemAgent>();
            if (partner && partner.species == species && partner.energy >= species.reproductionThreshold)
            {
                if (Vector3.Distance(transform.position, partner.transform.position) < 3f)
                {
                    CreateOffspring(partner);
                    energy *= 0.6f;
                    partner.energy *= 0.6f;
                    AddReward(species.reproductionReward);
                    return true;
                }
            }
        }
        return false;
    }

    private void TryFlee()
    {
        GameObject nearestThreat = GetNearestObject(nearbyPredators);
        if (nearestThreat)
        {
            Vector3 fleeDirection = (transform.position - nearestThreat.transform.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDirection * 10f;
            RequestPathTo(fleeTarget);
            energy -= 0.2f;
        }
    }

    private void CreateOffspring(EcosystemAgent partner)
    {
        Vector3 spawnPos = (transform.position + partner.transform.position) / 2f + Random.insideUnitSphere;
        spawnPos.y = transform.position.y;
        
        if (AStarManager.Instance != null)
        {
            spawnPos = AStarManager.Instance.GetRandomWalkablePosition(spawnPos, 5f);
        }
        
        GameObject offspring = Instantiate(gameObject, spawnPos, Quaternion.identity);
        EcosystemAgent offspringAgent = offspring.GetComponent<EcosystemAgent>();
        offspringAgent.species = species;
        offspringAgent.InitializeFromSpecies();
        
        OnReproduction?.Invoke(this, offspringAgent);
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        if (health <= 0f)
            Die();
    }

    private float CalculateReward()
    {
        float reward = 0f;
        
        if (isAlive)
            reward += species.survivalReward;
        
        float energyRatio = energy / species.maxEnergy;
        if (energyRatio > 0.7f)
            reward += 0.02f;
        else if (energyRatio < 0.3f)
            reward -= 0.05f;
        
        if (isFollowingPath)
            reward += 0.01f;
            
        return reward;
    }

    private void Die()
    {
        isAlive = false;
        AddReward(species.deathPenalty);
        OnDeath?.Invoke(this);
        EndEpisode();
    }

    private Vector3 GetDirectionToNearest(List<GameObject> objects)
    {
        GameObject nearest = GetNearestObject(objects);
        if (nearest == null) return Vector3.zero;
        
        return (nearest.transform.position - transform.position).normalized;
    }

    private GameObject GetNearestObject(List<GameObject> objects)
    {
        if (objects.Count == 0) return null;
        
        GameObject nearest = null;
        float minDistance = float.MaxValue;
        
        foreach (var obj in objects)
        {
            if (obj == null) continue;
            
            float distance = Vector3.Distance(transform.position, obj.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = obj;
            }
        }
        
        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        if (species == null) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, species.visionRange);
        
        Vector3 leftBound = Quaternion.AngleAxis(-species.visionAngle / 2, Vector3.up) * transform.forward * species.visionRange;
        Vector3 rightBound = Quaternion.AngleAxis(species.visionAngle / 2, Vector3.up) * transform.forward * species.visionRange;
        
        Gizmos.color = Color.white;
        Gizmos.DrawRay(transform.position, leftBound);
        Gizmos.DrawRay(transform.position, rightBound);
        
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            foreach (var food in nearbyFood)
                if (food) Gizmos.DrawLine(transform.position, food.transform.position);
                
            Gizmos.color = Color.red;
            foreach (var predator in nearbyPredators)
                if (predator) Gizmos.DrawLine(transform.position, predator.transform.position);
            
            if (currentPath != null && currentPath.Count > 0)
            {
                Gizmos.color = Color.blue;
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
                }
                
                if (currentPathIndex < currentPath.Count)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(currentPath[currentPathIndex], 0.5f);
                }
            }
        }
    }
}