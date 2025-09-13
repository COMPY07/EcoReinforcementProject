using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class SimpleEcoAgent : Agent
{
    [Header("Species Configuration")]
    public SpeciesData species;
    
    [Header("Map Settings")]
    [SerializeField] private Vector2 mapSize = new Vector2(50f, 50f);
    [SerializeField] private Vector2 mapOrigin = Vector2.zero;
    [SerializeField] private bool bounceOffWalls = true;
    [SerializeField] private float wallAvoidanceDistance = 2f;
    private RayPerceptionSensorComponent3D raySensor;

    [Header("Multi-Agent Settings")]
    [SerializeField] private string behaviorName;
    [SerializeField] private int teamId = 0;
    
    [Header("Runtime Stats")]
    public float health;
    public float energy;
    public float age;
    
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 180f;
    [SerializeField] private float randomMoveChance = 0.1f;
    [SerializeField] private float stuckCheckTime = 3f;
    [SerializeField] private float stuckThreshold = 0.5f;
    
    private Rigidbody rb;
    private Renderer bodyRenderer;
    private bool isAlive = true;
    private bool checking = false;
    
    private List<GameObject> nearbyAgents = new List<GameObject>();
    private List<GameObject> nearbyFood = new List<GameObject>();
    private List<GameObject> nearbyPredators = new List<GameObject>();
    private List<GameObject> nearbyPrey = new List<GameObject>();
    private List<GameObject> nearbyAllies = new List<GameObject>();
    
    private Vector3 currentMoveDirection;
    private Vector3 targetPosition;
    private bool hasTarget = false;
    private float lastMoveTime;
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    
    private SimpleEcosystemManager ecosystemManager;
    private int agentId;
    
    public System.Action<SimpleEcoAgent> OnDeath;
    public System.Action<SimpleEcoAgent, SimpleEcoAgent> OnReproduction;

    public bool IsAlive => isAlive;
    public string BehaviorName => behaviorName;
    public int TeamId => teamId;
    public Vector2 MapSize => mapSize;
    public Vector2 MapOrigin => mapOrigin;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyRenderer = GetComponentInChildren<Renderer>();
        agentId = GetInstanceID();
        ecosystemManager = FindObjectOfType<SimpleEcosystemManager>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        SetupRaySensor();
        rb.mass = 1f;
        rb.angularDamping = 5f; 
        rb.linearDamping = 2f;
        rb.freezeRotation = false; 

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        InitializeFromSpecies();
        if (ecosystemManager != null) ecosystemManager.RegisterAgent(this);
    }

    private void InitializeFromSpecies()
    {
        if (species == null) return;
        
        health = species.maxHealth;
        energy = species.maxEnergy;
        age = 0f;
        
        moveSpeed = species.moveSpeed;
        turnSpeed = species.turnSpeed;
        
        transform.localScale = species.bodyScale * species.size;
        
        if (bodyRenderer && species.bodyMaterial)
            bodyRenderer.material = species.bodyMaterial;
            
        SetupMultiAgentBehavior();
        
        currentMoveDirection = Random.insideUnitSphere;
        currentMoveDirection.y = 0f;
        currentMoveDirection.Normalize();
        
        lastPosition = transform.position;
        lastMoveTime = Time.time;
    }
    
    private void SetupMultiAgentBehavior()
    {
        if (species == null) return;
        
        behaviorName = species.speciesName;
        teamId = species.speciesName.GetHashCode();
        
        var behaviorParams = GetComponent<BehaviorParameters>();
        if (behaviorParams == null)
        {
            behaviorParams = gameObject.AddComponent<BehaviorParameters>();
        }
        
        behaviorParams.BehaviorName = behaviorName;
        behaviorParams.TeamId = teamId;
        behaviorParams.UseChildSensors = true;
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
            
            rb.angularDamping = 5f;
            rb.linearDamping = 2f;
        }
        
        transform.position = GetValidSpawnPosition();
        transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        
        if (ecosystemManager != null)
        {
            ecosystemManager.RegisterAgent(this);
        }
    }

    private Vector3 GetValidSpawnPosition()
    {
        float padding = 2f; 
        
        float x = Random.Range(mapOrigin.x + padding, mapOrigin.x + mapSize.x - padding);
        float z = Random.Range(mapOrigin.y + padding, mapOrigin.y + mapSize.y - padding);
        
        return new Vector3(x, ecosystemManager.mapHeight+0.5f, z);
    }

    private void FixedUpdate()
    {
        if (!isAlive || species == null) return;
        
        UpdateVitals();
        UpdatePerception();
        UpdateMovement();
        CheckIfStuck();
        UpdateMultiAgentRewards();

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
            if(age >= species.maxAge) AddReward(species.deathPenalty + 0.1f);
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

    private void UpdateMovement_()
    {
        Vector3 wallAvoidance = GetWallAvoidanceDirection();
        if (wallAvoidance != Vector3.zero)
        {
            currentMoveDirection = wallAvoidance;
            hasTarget = false;
        }
        
        if (hasTarget)
        {
            Vector3 directionToTarget = (targetPosition - transform.position).normalized;
            directionToTarget.y = 0f;
            
            if (Vector3.Distance(transform.position, targetPosition) < 1.5f)
            {
                hasTarget = false;
            }
            else
            {
                currentMoveDirection = Vector3.Slerp(currentMoveDirection, directionToTarget, Time.fixedDeltaTime * 2f);
            }
        }
        
        if (Random.value < randomMoveChance * Time.fixedDeltaTime)
        {
            Vector3 randomDir = Random.insideUnitSphere;
            randomDir.y = 0f;
            randomDir.Normalize();
            currentMoveDirection = Vector3.Slerp(currentMoveDirection, randomDir, 0.3f);
        }
        
        MoveInDirection(currentMoveDirection);
    }

    private Vector3 GetWallAvoidanceDirection()
    {
        Vector3 pos = transform.position;
        Vector3 avoidance = Vector3.zero;
        
        float leftDist = pos.x - mapOrigin.x;
        float rightDist = (mapOrigin.x + mapSize.x) - pos.x;
        float bottomDist = pos.z - mapOrigin.y;
        float topDist = (mapOrigin.y + mapSize.y) - pos.z;
        
        if (leftDist < wallAvoidanceDistance)
        {
            avoidance += Vector3.right * (wallAvoidanceDistance - leftDist);
        }
        if (rightDist < wallAvoidanceDistance)
        {
            avoidance += Vector3.left * (wallAvoidanceDistance - rightDist);
        }
        if (bottomDist < wallAvoidanceDistance)
        {
            avoidance += Vector3.forward * (wallAvoidanceDistance - bottomDist);
        }
        if (topDist < wallAvoidanceDistance)
        {
            avoidance += Vector3.back * (wallAvoidanceDistance - topDist);
        }
        
        return avoidance.normalized;
    }

    private void CheckIfStuck()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        
        if (distanceMoved < stuckThreshold * Time.fixedDeltaTime)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > stuckCheckTime)
            {
                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = 0f;
                currentMoveDirection = randomDirection.normalized;
                hasTarget = false;
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        
        lastPosition = transform.position;
    }
    private void SetupRaySensor()
    {
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        if (raySensor == null)
        {
            raySensor = gameObject.AddComponent<RayPerceptionSensorComponent3D>();
        }
        
        raySensor.SensorName = "AgentRaySensor";
        raySensor.RaysPerDirection = 10; 
        raySensor.MaxRayDegrees = 90f;  
        raySensor.RayLength = species != null ? species.visionRange : 20f;
        raySensor.DetectableTags = new List<string>();

        if (species != null)
        {
            foreach (string tag in species.foodTags)
                if (!raySensor.DetectableTags.Contains(tag)) raySensor.DetectableTags.Add(tag);

            foreach (string tag in species.preyTags)
                if (!raySensor.DetectableTags.Contains(tag)) raySensor.DetectableTags.Add(tag);

            foreach (string tag in species.predatorTags)
                if (!raySensor.DetectableTags.Contains(tag)) raySensor.DetectableTags.Add(tag);

            raySensor.DetectableTags.Add("Rizard");
        }
    }
    private void UpdateMultiAgentRewards()
    {
        if (ecosystemManager == null) return;
        
        // float cooperationReward = nearbyAllies.Count * 0.01f;
        // float competitionReward = (nearbyAgents.Count - nearbyAllies.Count) > nearbyAllies.Count ? -0.01f : 0.01f;
        //
        // AddReward((cooperationReward + competitionReward) * Time.fixedDeltaTime);
    }

    private void MoveInDirection_(Vector3 direction)
    {
        if (direction.magnitude < 0.1f) 
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }
        
        Vector3 currentPos = transform.position;
        Vector3 nextPos = currentPos + direction * moveSpeed * Time.fixedDeltaTime;
        
        Vector3 finalDirection = direction;
        
        if (!IsPositionInBounds(nextPos))
        {
            if (bounceOffWalls)
            {
                finalDirection = GetBounceDirection(direction, nextPos);
            }
            else
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                return;
            }
        }
        
        rb.linearVelocity = new Vector3(
            finalDirection.x * moveSpeed, 
            rb.linearVelocity.y,  
            finalDirection.z * moveSpeed
        ); // ㅇ;가 ㅈ모 수정 좀 필요할 듯?
        
        if (finalDirection.magnitude > 0.1f)
        {
            Vector3 lookDirection = finalDirection.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            
            Quaternion rotationDifference = targetRotation * Quaternion.Inverse(transform.rotation);
            rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);
            
            if (angle > 180f) angle -= 360f;
            
            Vector3 angularVelocity = axis * angle * Mathf.Deg2Rad * turnSpeed / 180f;
            rb.angularVelocity = new Vector3(0f, angularVelocity.y, 0f);
        }
        else
        {
            rb.angularVelocity = Vector3.zero;
        }
        
        energy -= direction.magnitude * 0.1f * Time.fixedDeltaTime;
    }

    private bool IsPositionInBounds(Vector3 position)
    {
        return position.x >= mapOrigin.x && position.x <= mapOrigin.x + mapSize.x &&
               position.z >= mapOrigin.y && position.z <= mapOrigin.y + mapSize.y;
    }

    private Vector3 GetBounceDirection(Vector3 currentDirection, Vector3 nextPosition)
    {
        Vector3 bounceDir = currentDirection;
        
        if (nextPosition.x < mapOrigin.x || nextPosition.x > mapOrigin.x + mapSize.x)
        {
            bounceDir.x = -bounceDir.x;
        }
        if (nextPosition.z < mapOrigin.y || nextPosition.z > mapOrigin.y + mapSize.y)
        {
            bounceDir.z = -bounceDir.z;
        }
        
        return bounceDir;
    }

    public void SetTarget(Vector3 target)
    {
        if (IsPositionInBounds(target))
        {
            targetPosition = target;
            hasTarget = true;
        }
    }

    public void MoveTowards(Vector3 target)
    {
        SetTarget(target);
    }

    private void ClearPerceptionLists()
    {
        nearbyAgents.Clear();
        nearbyFood.Clear();
        nearbyPredators.Clear();
        nearbyPrey.Clear();
        nearbyAllies.Clear();
    }

    private bool IsInVisionCone(Vector3 targetPos)
    {
        Vector3 dirToTarget = (targetPos - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToTarget);
        return angle <= species.visionAngle / 2f;
    }

    private void CategorizeObject(GameObject obj)
    {
        SimpleEcoAgent otherAgent = obj.GetComponent<SimpleEcoAgent>();
        
        if (otherAgent != null)
        {
            nearbyAgents.Add(obj);
            
            if (otherAgent.species.speciesName == species.speciesName)
            {
                nearbyAllies.Add(obj);
            }
            else
            {
                if (species.predatorTags.Contains(otherAgent.species.speciesName))
                    nearbyPredators.Add(obj);
                else if (species.preyTags.Contains(otherAgent.species.speciesName))
                    nearbyPrey.Add(obj);
            }
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

    public void CollectObservations_(VectorSensor sensor)
    {
        if (species == null) return;
        
        sensor.AddObservation(health / species.maxHealth);
        sensor.AddObservation(energy / species.maxEnergy);
        sensor.AddObservation(age / species.maxAge);
        
        Vector2 normalizedPos = new Vector2(
            (transform.position.x - mapOrigin.x) / mapSize.x,
            (transform.position.z - mapOrigin.y) / mapSize.y
        );
        sensor.AddObservation(normalizedPos);
        
        sensor.AddObservation(rb.linearVelocity / moveSpeed);
        sensor.AddObservation(transform.forward);
        
        float leftWall = (transform.position.x - mapOrigin.x) / mapSize.x;
        float rightWall = (mapOrigin.x + mapSize.x - transform.position.x) / mapSize.x;
        float bottomWall = (transform.position.z - mapOrigin.y) / mapSize.y;
        float topWall = (mapOrigin.y + mapSize.y - transform.position.z) / mapSize.y;
        
        sensor.AddObservation(leftWall);
        sensor.AddObservation(rightWall);
        sensor.AddObservation(bottomWall);
        sensor.AddObservation(topWall);
        
        sensor.AddObservation(nearbyFood.Count / 10f);
        sensor.AddObservation(nearbyPredators.Count / 10f);
        sensor.AddObservation(nearbyPrey.Count / 10f);
        sensor.AddObservation(nearbyAllies.Count / 10f);
        sensor.AddObservation((nearbyAgents.Count - nearbyAllies.Count) / 10f);
        
        Vector3 nearestFoodDir = GetDirectionToNearest(nearbyFood);
        Vector3 nearestPredatorDir = GetDirectionToNearest(nearbyPredators);
        Vector3 nearestPreyDir = GetDirectionToNearest(nearbyPrey);
        Vector3 nearestAllyDir = GetDirectionToNearest(nearbyAllies);
        
        sensor.AddObservation(nearestFoodDir);
        sensor.AddObservation(nearestPredatorDir);
        sensor.AddObservation(nearestPreyDir);
        sensor.AddObservation(nearestAllyDir);
        
        sensor.AddObservation(currentMoveDirection);
        sensor.AddObservation(hasTarget ? 1f : 0f);
        
        if (hasTarget)
        {
            Vector3 targetDir = (targetPosition - transform.position).normalized;
            sensor.AddObservation(targetDir);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
        }
        
        if (ecosystemManager != null)
        {
            var populations = ecosystemManager.GetSpeciesPopulations();
            float totalPop = ecosystemManager.GetTotalPopulation();
            
            float mySpeciesRatio = populations.ContainsKey(species.speciesName) ? 
                                  (float)populations[species.speciesName] / totalPop : 0f;
            sensor.AddObservation(mySpeciesRatio);
        }
        else
        {
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isAlive || species == null) return;
        
        ProcessMovement(actions.ContinuousActions);
        ProcessAction(actions.DiscreteActions);
        
        float reward = CalculateBaseReward();
        AddReward(reward);
    }

    private void ProcessMovement(ActionSegment<float> actions)
    {
        if (actions.Length < 1) return;
        
        float turnInput = Mathf.Clamp(actions[0], -1f, 1f);
        
        float turnAmount = turnInput * turnSpeed * Time.fixedDeltaTime;
        transform.Rotate(0, turnAmount, 0);
        
        Vector3 forwardDirection = transform.forward;
        currentMoveDirection = forwardDirection;
        
        MoveInDirection(forwardDirection);
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
        
        float turnInput = 0f;
        ActionType chosenAction = ActionType.DoNothing;
        Vector3 desiredDirection = Vector3.zero;
        

        if (nearbyPredators.Count > 0)
        {
            GameObject nearestPredator = GetNearestObject(nearbyPredators);
            Vector3 fleeDirection = (transform.position - nearestPredator.transform.position).normalized;
            desiredDirection = fleeDirection;
            chosenAction = ActionType.Flee;
        }
        else if (species.canAttack && nearbyPrey.Count > 0)
        {
            GameObject nearestPrey = GetNearestObject(nearbyPrey);
            desiredDirection = (nearestPrey.transform.position - transform.position).normalized;
            if (Vector3.Distance(transform.position, nearestPrey.transform.position) < 3f)
            {
                chosenAction = ActionType.Attack;
            }
        }
        else if (energy < species.maxEnergy * 0.6f && nearbyFood.Count > 0)
        {
            GameObject nearestFood = GetNearestObject(nearbyFood);
            desiredDirection = (nearestFood.transform.position - transform.position).normalized;
            if (Vector3.Distance(transform.position, nearestFood.transform.position) < 2f)
            {
                chosenAction = ActionType.Eat;
            }
        }
        else if (species.canReproduce && energy >= species.reproductionThreshold)
        {
            foreach (var ally in nearbyAllies)
            {
                SimpleEcoAgent partner = ally.GetComponent<SimpleEcoAgent>();
                if (partner && partner.energy >= species.reproductionThreshold)
                {
                    desiredDirection = (partner.transform.position - transform.position).normalized;
                    if (Vector3.Distance(transform.position, partner.transform.position) < 3f)
                    {
                        chosenAction = ActionType.Reproduce;
                        break;
                    }
                }
            }
        }
        
        if (desiredDirection != Vector3.zero)
        {
            float angle = Vector3.SignedAngle(transform.forward, desiredDirection, Vector3.up);
            turnInput = Mathf.Clamp(angle / 180f, -1f, 1f);
        }
        
        if (continuousActionsOut.Length >= 1)
            continuousActionsOut[0] = turnInput;
        
        
        if (discreteActionsOut.Length > 0)
            discreteActionsOut[0] = (int)chosenAction;
        
    }

    private void MoveInDirection(Vector3 direction)
    {
        if (direction.magnitude < 0.1f) 
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }
        
        Vector3 currentPos = transform.position;
        Vector3 nextPos = currentPos + direction * moveSpeed * Time.fixedDeltaTime;
        
        Vector3 finalDirection = direction;
        
        if (!IsPositionInBounds(nextPos))
        {
            if (bounceOffWalls)
            {
                finalDirection = GetBounceDirection(direction, nextPos);
            }
            else
            {

                
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                return;
            }
        }
        
        rb.linearVelocity = new Vector3(
            finalDirection.x * moveSpeed, 
            rb.linearVelocity.y, 
            finalDirection.z * moveSpeed
        );
        
        energy -= direction.magnitude * 0.1f * Time.fixedDeltaTime;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (species == null) return;
        
        sensor.AddObservation(health / species.maxHealth);
        sensor.AddObservation(energy / species.maxEnergy);
        sensor.AddObservation(age / species.maxAge);
        
        Vector2 normalizedPos = new Vector2(
            (transform.position.x - mapOrigin.x) / mapSize.x,
            (transform.position.z - mapOrigin.y) / mapSize.y
        );
        sensor.AddObservation(normalizedPos);
        
        sensor.AddObservation(rb.linearVelocity / moveSpeed);
        sensor.AddObservation(transform.forward); 
        
        
        
        float leftWall = (transform.position.x - mapOrigin.x) / mapSize.x;
        float rightWall = (mapOrigin.x + mapSize.x - transform.position.x) / mapSize.x;
        float bottomWall = (transform.position.z - mapOrigin.y) / mapSize.y;
        float topWall = (mapOrigin.y + mapSize.y - transform.position.z) / mapSize.y;
        
        sensor.AddObservation(leftWall);
        sensor.AddObservation(rightWall);
        sensor.AddObservation(bottomWall);
        sensor.AddObservation(topWall);
        

        Vector3 forward = transform.forward;
        Vector3 toLeftWall = Vector3.left;
        Vector3 toRightWall = Vector3.right;
        Vector3 toBottomWall = Vector3.back;
        Vector3 toTopWall = Vector3.forward;
        
        sensor.AddObservation(Vector3.Dot(forward, toLeftWall));   
        sensor.AddObservation(Vector3.Dot(forward, toRightWall)); 
        sensor.AddObservation(Vector3.Dot(forward, toBottomWall));
        sensor.AddObservation(Vector3.Dot(forward, toTopWall));  
        

        sensor.AddObservation(nearbyFood.Count / 10f);
        sensor.AddObservation(nearbyPredators.Count / 10f);
        sensor.AddObservation(nearbyPrey.Count / 10f);
        sensor.AddObservation(nearbyAllies.Count / 10f);
        sensor.AddObservation((nearbyAgents.Count - nearbyAllies.Count) / 10f);
        
        Vector3 nearestFoodDir = GetDirectionToNearest(nearbyFood);
        Vector3 nearestPredatorDir = GetDirectionToNearest(nearbyPredators);
        Vector3 nearestPreyDir = GetDirectionToNearest(nearbyPrey);
        Vector3 nearestAllyDir = GetDirectionToNearest(nearbyAllies);
        
        sensor.AddObservation(GetRelativeAngle(nearestFoodDir));
        sensor.AddObservation(GetRelativeAngle(nearestPredatorDir));
        sensor.AddObservation(GetRelativeAngle(nearestPreyDir));
        sensor.AddObservation(GetRelativeAngle(nearestAllyDir));
        
        sensor.AddObservation(GetDistanceToNearest(nearbyFood) / species.visionRange);
        sensor.AddObservation(GetDistanceToNearest(nearbyPredators) / species.visionRange);
        sensor.AddObservation(GetDistanceToNearest(nearbyPrey) / species.visionRange);
        sensor.AddObservation(GetDistanceToNearest(nearbyAllies) / species.visionRange);
        
        if (ecosystemManager != null)
        {
            var populations = ecosystemManager.GetSpeciesPopulations();
            float totalPop = ecosystemManager.GetTotalPopulation();
            
            float mySpeciesRatio = populations.ContainsKey(species.speciesName) ? 
                                  (float)populations[species.speciesName] / totalPop : 0f;
            sensor.AddObservation(mySpeciesRatio);
        }
        else
        {
            sensor.AddObservation(0f);
        }
    }

    private float GetRelativeAngle(Vector3 direction)
    {
        if (direction == Vector3.zero) return 0f;
        
        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        return angle / 180f; 
    }

    private float GetDistanceToNearest(List<GameObject> objects)
    {
        GameObject nearest = GetNearestObject(objects);
        if (nearest == null) return species.visionRange;
        
        return Vector3.Distance(transform.position, nearest.transform.position);
    }

    private void UpdateMovement()
    {
        Vector3 wallAvoidance = GetWallAvoidanceDirection();
        if (wallAvoidance != Vector3.zero)
        {
            float avoidanceAngle = Vector3.SignedAngle(transform.forward, wallAvoidance, Vector3.up);
            float turnAmount = Mathf.Sign(avoidanceAngle) * turnSpeed * Time.fixedDeltaTime;
            transform.Rotate(0, turnAmount, 0);
        }
        
        MoveInDirection(transform.forward);
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
            energy = Mathf.Min(energy + 5f, species.maxEnergy);
             // = Mathf.Min(energy + 30f, species.maxEnergy);
            AddReward(species.feedingReward);
            checking = true;
            return true;
        }
        return false;
    }

    private bool TryAttack()
    {
        GameObject nearestPrey = GetNearestObject(nearbyPrey);
        if (nearestPrey && Vector3.Distance(transform.position, nearestPrey.transform.position) < 3f)
        {
            SimpleEcoAgent preyAgent = nearestPrey.GetComponent<SimpleEcoAgent>();
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
        if (energy < species.reproductionThreshold || !checking) return false;
        
        foreach (var ally in nearbyAllies)
        {
            SimpleEcoAgent partner = ally.GetComponent<SimpleEcoAgent>();
            
            if (partner && partner.energy >= species.reproductionThreshold && partner.species.speciesName == species.speciesName)
            {
                if (Vector3.Distance(transform.position, partner.transform.position) < 3f)
                {
                    CreateOffspring(partner);
                    energy *= 0.7f;
                    partner.energy *= 0.7f;
                    AddReward(species.reproductionReward);
                    partner.AddReward(species.reproductionReward);
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
            
            if (IsPositionInBounds(fleeTarget))
            {
                SetTarget(fleeTarget);
            }
            
            energy -= 0.2f;
        }
    }

    private void CreateOffspring(SimpleEcoAgent partner)
    {
        Vector3 spawnPos = (transform.position + partner.transform.position) / 2f;
        spawnPos += Random.insideUnitSphere * 2f;
        spawnPos.y = transform.position.y;
        
        if (IsPositionInBounds(spawnPos))
        {
            GameObject offspring = Instantiate(gameObject, spawnPos, Quaternion.identity);
            SimpleEcoAgent offspringAgent = offspring.GetComponent<SimpleEcoAgent>();
            offspringAgent.species = species;
            offspringAgent.InitializeFromSpecies();
            
            OnReproduction?.Invoke(this, offspringAgent);
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        AddReward(-0.1f);
        
        if (health <= 0f)
            Die();
    }

    private float CalculateBaseReward()
    {
        float reward = 0f;
        
        if (isAlive)
            reward += species.survivalReward * Time.fixedDeltaTime;
        
        Vector3 mapCenter = new Vector3(mapOrigin.x + mapSize.x/2f, transform.position.y, mapOrigin.y + mapSize.y/2f);
        float distanceFromCenter = Vector3.Distance(transform.position, mapCenter);
        float maxDistance = Mathf.Max(mapSize.x, mapSize.y) / 2f;
        
        if (distanceFromCenter > maxDistance * 0.8f)
        {
            reward -= 0.01f * Time.fixedDeltaTime;
        }
        
        return reward;
    }

    private void Die()
    {
        isAlive = false;
        AddReward(species.deathPenalty);
        
        OnDeath?.Invoke(this);
        
        if (ecosystemManager != null)
        {
            ecosystemManager.UnregisterAgent(this);
        }
        
        Destroy(this.gameObject);
        // EndEpisode();
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
        
        Gizmos.color = Color.white;
        Vector3 mapCenter = new Vector3(mapOrigin.x + mapSize.x/2f, transform.position.y, mapOrigin.y + mapSize.y/2f);
        Gizmos.DrawWireCube(mapCenter, new Vector3(mapSize.x, 0.1f, mapSize.y));
        
        if (hasTarget)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetPosition, 1f);
            Gizmos.DrawLine(transform.position, targetPosition);
        }
        
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, currentMoveDirection * 3f);
        
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            foreach (var food in nearbyFood)
                if (food) Gizmos.DrawLine(transform.position, food.transform.position);
                
            Gizmos.color = Color.red;
            foreach (var predator in nearbyPredators)
                if (predator) Gizmos.DrawLine(transform.position, predator.transform.position);
                
            Gizmos.color = Color.blue;
            foreach (var ally in nearbyAllies)
                if (ally) Gizmos.DrawLine(transform.position, ally.transform.position);
        }
    }
    
    public void SetSpecies(SpeciesData newSpecies)
    {
        species = newSpecies;
        InitializeFromSpecies();
    }
    
    public void SetMapBounds(Vector2 origin, Vector2 size)
    {
        mapOrigin = origin;
        mapSize = size;
    }
    
    public Dictionary<string, object> GetAgentStats()
    {
        return new Dictionary<string, object>
        {
            {"species", species?.speciesName ?? "Unknown"},
            {"health", health},
            {"energy", energy},
            {"age", age},
            {"isAlive", isAlive},
            {"position", transform.position},
            {"hasTarget", hasTarget},
            {"targetPosition", targetPosition}
        };
    }
}