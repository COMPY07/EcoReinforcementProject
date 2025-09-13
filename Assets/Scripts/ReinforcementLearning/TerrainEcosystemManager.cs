
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using UnityEditor;

public class SimpleEcosystemManager : Singleton<SimpleEcosystemManager>
{
    [Header("Simple Map Configuration")]
    public Vector2 mapSize = new Vector2(50f, 50f);
    public Vector2 mapOrigin = Vector2.zero;
    public float mapHeight = 1f;
    
    [Header("Agent Configuration")]
    public List<SimpleSpeciesConfig> speciesConfigs = new List<SimpleSpeciesConfig>();
    public GameObject[] agentPrefabs;
    public GameObject[] foodPrefabs;
    
    [Header("Food System")]
    public int maxFoodCount = 100;
    public float foodSpawnRate = 2f;
    public float foodRespawnTime = 5f;
    
    [Header("Multi-Agent Learning")]
    public bool enableGroupRewards = true;
    public bool enableCompetitiveRewards = true;
    public bool enableEcosystemRewards = true;
    public float groupRewardMultiplier = 0.1f;
    public float competitionRewardMultiplier = 0.1f;
    public float ecosystemRewardMultiplier = 0.05f;
    
    [Header("Simulation Control")]
    public bool autoReset = true;
    public float episodeDuration = 40f;
    public int maxAgentsPerSpecies = 50;
    public int maxTotalAgents = 200;

    [Header("Performance")] public bool enableCulling = false;
    public float cullingDistance = 30f;
    
    [Header("Predator System")]
    public GameObject predatorPrefab;
    public int predatorCount = 3;
    public float predatorSpawnRadius = 15f;
    
    [Header("Spawn Settings")]
    public float minSpawnDistance = 2f;
    public float foodSpawnPadding = 1f;
    
    private Dictionary<string, List<SimpleEcoAgent>> speciesGroups = new Dictionary<string, List<SimpleEcoAgent>>();
    private Dictionary<string, AgentTrainingData> trainingData = new Dictionary<string, AgentTrainingData>();
    private List<SimpleEcoAgent> allAgents = new List<SimpleEcoAgent>();
    
    private List<GameObject> activePredators = new List<GameObject>();
    private List<GameObject> activeFoods = new List<GameObject>();
    
    private float episodeStartTime;
    private float lastFoodSpawn;
    private int totalAgentsSpawned;
    
    private SimpleEcoAgent selectedAgent;
    private Vector2 scrollPosition;

    
    public List<GameObject> ActiveFoods => activeFoods;
    public List<SimpleEcoAgent> AllAgents => allAgents;
    
    [System.Serializable]
    public class SimpleSpeciesConfig
    {
        public SpeciesData speciesData;
        public GameObject customAgentPrefab;
        public int initialCount;
        public int maxPopulation = 50;
        public Vector2 spawnArea = Vector2.one;
        public bool enableRespawn = true;
        public bool enableGroupLearning = true;
        public float cooperationBonus = 0.1f;
        public string[] allySpecies;
        public string[] enemySpecies;
    }
    
    [System.Serializable]
    public class AgentTrainingData
    {
        public string speciesName;
        public int totalSpawned;
        public int currentAlive;
        public float averageAge;
        public float cooperationScore;
        public float competitionScore;
        public Dictionary<string, int> interactionCounts = new Dictionary<string, int>();
    }

    private void Start()
    {
        InitializeEnvironment();
        SpawnInitialPopulation();
        SpawnPredators();
        episodeStartTime = Time.time;
    }
    
    private void InitializeEnvironment()
    {
        InitializeSpeciesGroups();
        SpawnInitialFood();
        
        Debug.Log($"Simple Ecosystem initialized - Map: {mapSize} at {mapOrigin}");
        Debug.Log($"Map bounds: ({mapOrigin.x}, {mapOrigin.y}) to ({mapOrigin.x + mapSize.x}, {mapOrigin.y + mapSize.y})");
    }
    
    private void InitializeSpeciesGroups()
    {
        foreach (var config in speciesConfigs)
        {
            string speciesName = config.speciesData.speciesName;
            
            if (!speciesGroups.ContainsKey(speciesName))
            {
                speciesGroups[speciesName] = new List<SimpleEcoAgent>();
            }
            
            if (!trainingData.ContainsKey(speciesName))
            {
                trainingData[speciesName] = new AgentTrainingData
                {
                    speciesName = speciesName,
                    totalSpawned = 0,
                    currentAlive = 0,
                    averageAge = 0f,
                    cooperationScore = 0f,
                    competitionScore = 0f
                };
            }
        }
    }

    private void Update()
    {
        UpdateFoodSystem();
        UpdatePopulation();
        UpdateTrainingData();
        UpdateGroupRewards();
        UpdateSimulation();
        CleanupDestroyedAgents();
        HandleMouseClick();
        
        if (enableCulling)
            UpdateCulling();
    }

    private void SpawnInitialPopulation()
    {
        foreach (var config in speciesConfigs)
        {
            for (int i = 0; i < config.initialCount; i++)
            {
                SpawnAgent(config);
            }
        }
        
        Debug.Log($"Simple Ecosystem: Spawned {totalAgentsSpawned} agents across {speciesConfigs.Count} species");
    }

    private SimpleEcoAgent SpawnAgent(SimpleSpeciesConfig config)
    {
        if (totalAgentsSpawned >= maxTotalAgents)
        {
            Debug.LogWarning($"Cannot spawn agent - reached max total agents limit: {maxTotalAgents}");
            return null;
        }
        
        if (speciesGroups[config.speciesData.speciesName].Count >= config.maxPopulation)
        {
            Debug.LogWarning($"Cannot spawn {config.speciesData.speciesName} - reached species limit: {config.maxPopulation}");
            return null;
        }
        
        Vector3 spawnPos = GetValidSpawnPosition(config.spawnArea);
        
        GameObject prefabToUse = null;
        if (config.customAgentPrefab != null)
        {
            prefabToUse = config.customAgentPrefab;
        }
        else if (config.speciesData.customPrefab != null)
        {
            prefabToUse = config.speciesData.customPrefab;
        }
        else if (agentPrefabs.Length > 0)
        {
            prefabToUse = agentPrefabs[Random.Range(0, agentPrefabs.Length)];
        }
        
        if (prefabToUse == null)
        {
            Debug.LogError($"No prefab available for {config.speciesData.speciesName}!");
            return null;
        }
        
        GameObject agentObj = Instantiate(prefabToUse, spawnPos, GetRandomRotation());
        SimpleEcoAgent agent = agentObj.GetComponent<SimpleEcoAgent>();
        
        if (agent == null)
        {
            agent = agentObj.AddComponent<SimpleEcoAgent>();
        }
        
        if (agent != null) {
            agent.SetMapBounds(mapOrigin, mapSize);
            agent.SetSpecies(config.speciesData);
            agent.OnDeath += HandleAgentDeath;
            agent.OnReproduction += HandleReproduction;
            
            allAgents.Add(agent);
            speciesGroups[config.speciesData.speciesName].Add(agent);
            totalAgentsSpawned++;
            
            if (trainingData.ContainsKey(config.speciesData.speciesName))
            {
                trainingData[config.speciesData.speciesName].totalSpawned++;
            }
            
            agentObj.name = $"{config.speciesData.speciesName}_{totalAgentsSpawned}";
            agentObj.transform.position = spawnPos;
            Debug.Log($"Spawned {agentObj.name} at {spawnPos}");
        }
        
        return agent;
    }

    private Vector3 GetValidSpawnPosition(Vector2 spawnArea)
    {
        int maxAttempts = 50;
        
        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            float areaWidth = mapSize.x * spawnArea.x;
            float areaHeight = mapSize.y * spawnArea.y;
            
            float x = Random.Range(mapOrigin.x, mapOrigin.x + areaWidth);
            float z = Random.Range(mapOrigin.y, mapOrigin.y + areaHeight);
            
            Vector3 candidatePos = new Vector3(x, mapHeight, z);
            
            if (IsValidSpawnPosition(candidatePos))
            {
                return candidatePos;
            }
        }
        
        Vector3 fallbackPos = new Vector3(
            mapOrigin.x + mapSize.x / 2f,
            mapHeight,
            mapOrigin.y + mapSize.y / 2f
        );
        
        Debug.LogWarning($"Using fallback spawn position: {fallbackPos}");
        return fallbackPos;
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        if (position.x < mapOrigin.x || position.x > mapOrigin.x + mapSize.x ||
            position.z < mapOrigin.y || position.z > mapOrigin.y + mapSize.y)
        {
            return false;
        }
        
        foreach (var agent in allAgents)
        {
            if (agent != null && Vector3.Distance(position, agent.transform.position) < minSpawnDistance)
            {
                return false;
            }
        }
        
        foreach (var food in activeFoods)
        {
            if (food != null && Vector3.Distance(position, food.transform.position) < minSpawnDistance)
            {
                return false;
            }
        }
        
        foreach (var predator in activePredators)
        {
            if (predator != null && Vector3.Distance(position, predator.transform.position) < minSpawnDistance)
            {
                return false;
            }
        }
        
        return true;
    }

    private Quaternion GetRandomRotation()
    {
        return Quaternion.Euler(0, Random.Range(0f, 360f), 0);
    }

    private void SpawnInitialFood()
    {
        if (foodPrefabs == null || foodPrefabs.Length == 0)
        {
            Debug.LogWarning("No food prefabs assigned!");
            return;
        }
        
        int targetFoodCount = maxFoodCount / 2;
        int successfulSpawns = 0;
        int maxAttempts = targetFoodCount * 3;
        int attempts = 0;
        
        Debug.Log($"Spawning initial food: Target={targetFoodCount}");
        
        while (successfulSpawns < targetFoodCount && attempts < maxAttempts)
        {
            attempts++;
            
            GameObject selectedFoodPrefab = foodPrefabs[Random.Range(0, foodPrefabs.Length)];
            Vector3 spawnPosition = GetValidFoodSpawnPosition();
            
            if (spawnPosition != Vector3.zero)
            {
                try
                {
                    GameObject foodObj = Instantiate(selectedFoodPrefab, spawnPosition, GetRandomRotation());
                    
                    if (foodObj != null)
                    {
                        foodObj.name = $"Food_{successfulSpawns + 1}_{selectedFoodPrefab.name}";
                        
                        float scaleVariation = Random.Range(0.8f, 1.2f);
                        foodObj.transform.localScale *= scaleVariation;
                        
                        activeFoods.Add(foodObj);
                        successfulSpawns++;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error spawning food: {e.Message}");
                }
            }
        }
        
        Debug.Log($"Initial food spawn completed: {successfulSpawns}/{targetFoodCount} foods spawned");
    }

    private Vector3 GetValidFoodSpawnPosition()
    {
        int maxAttempts = 30;
        
        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            float x = Random.Range(mapOrigin.x + foodSpawnPadding, 
                                   mapOrigin.x + mapSize.x - foodSpawnPadding);
            float z = Random.Range(mapOrigin.y + foodSpawnPadding, 
                                   mapOrigin.y + mapSize.y - foodSpawnPadding);
            
            Vector3 candidatePos = new Vector3(x, mapHeight, z);
            
            if (IsFoodPositionClear(candidatePos))
            {
                return candidatePos;
            }
        }
        
        return Vector3.zero;
    }

    private bool IsFoodPositionClear(Vector3 pos)
    {
        float minDistanceFromFood = 2f;
        float minDistanceFromAgent = 1f;
        
        foreach (var food in activeFoods)
        {
            if (food != null && Vector3.Distance(pos, food.transform.position) < minDistanceFromFood)
                return false;
        }
        
        foreach (var agent in allAgents)
        {
            if (agent != null && Vector3.Distance(pos, agent.transform.position) < minDistanceFromAgent)
                return false;
        }
        
        return true;
    }

    private void UpdateFoodSystem()
    {
        if (Time.time - lastFoodSpawn > foodSpawnRate)
        {
            if (activeFoods.Count < maxFoodCount)
            {
                SpawnFood();
            }
            lastFoodSpawn = Time.time;
        }
        
        CleanupDestroyedFood();
    }

    private void SpawnFood()
    {
        if (foodPrefabs.Length == 0) return;
        
        Vector3 spawnPos = GetValidFoodSpawnPosition();
        
        if (spawnPos != Vector3.zero)
        {
            GameObject foodPrefab = foodPrefabs[Random.Range(0, foodPrefabs.Length)];
            GameObject food = Instantiate(foodPrefab, spawnPos, GetRandomRotation());
            activeFoods.Add(food);
        }
    }

    private void CleanupDestroyedFood()
    {
        activeFoods.RemoveAll(food => food == null);
    }

    private void SpawnPredators()
    {
        if (predatorPrefab == null) return;
        
        for (int i = 0; i < predatorCount; i++)
        {
            Vector3 spawnPos = GetValidPredatorSpawnPosition();
            if (spawnPos != Vector3.zero)
            {
                GameObject predator = Instantiate(predatorPrefab, spawnPos, GetRandomRotation());
                activePredators.Add(predator);
                predator.name = $"Predator_{i + 1}";
            }
        }
    }

    private Vector3 GetValidPredatorSpawnPosition()
    {
        var aliveAgents = allAgents.Where(a => a != null && a.IsAlive).ToList();
        
        if (aliveAgents.Count > 0)
        {
            SimpleEcoAgent targetAgent = aliveAgents[Random.Range(0, aliveAgents.Count)];
            
            for (int attempts = 0; attempts < 30; attempts++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * predatorSpawnRadius;
                Vector3 candidatePos = targetAgent.transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
                
                candidatePos.x = Mathf.Clamp(candidatePos.x, mapOrigin.x, mapOrigin.x + mapSize.x);
                candidatePos.z = Mathf.Clamp(candidatePos.z, mapOrigin.y, mapOrigin.y + mapSize.y);
                candidatePos.y = mapHeight;
                
                if (IsPredatorPositionSafe(candidatePos))
                {
                    return candidatePos;
                }
            }
        }
        
        for (int attempts = 0; attempts < 20; attempts++)
        {
            Vector3 candidatePos = new Vector3(
                Random.Range(mapOrigin.x, mapOrigin.x + mapSize.x),
                mapHeight,
                Random.Range(mapOrigin.y, mapOrigin.y + mapSize.y)
            );
            
            if (IsPredatorPositionSafe(candidatePos))
            {
                return candidatePos;
            }
        }
        
        return Vector3.zero;
    }

    private bool IsPredatorPositionSafe(Vector3 pos)
    {
        foreach (var predator in activePredators)
        {
            if (predator != null && Vector3.Distance(pos, predator.transform.position) < 5f)
                return false;
        }
        
        foreach (var agent in allAgents)
        {
            if (agent != null && Vector3.Distance(pos, agent.transform.position) < 2f)
                return false;
        }
        
        return true;
    }

    private void UpdatePopulation()
    {
        foreach (var config in speciesConfigs)
        {
            if (!config.enableRespawn) continue;
            
            var speciesList = speciesGroups[config.speciesData.speciesName];
            
            int aliveCount = speciesList.Count(agent => agent != null && agent.IsAlive);
            int targetCount = config.initialCount;
            
            if (aliveCount < targetCount && speciesList.Count < config.maxPopulation && totalAgentsSpawned < maxTotalAgents)
            {
                int spawnCount = Mathf.Min(targetCount - aliveCount, 3);
                for (int i = 0; i < spawnCount; i++)
                {
                    SpawnAgent(config);
                }
            }
        }
    }

    private void UpdateTrainingData()
    {
        foreach (var kvp in speciesGroups)
        {
            string speciesName = kvp.Key;
            var agents = kvp.Value.Where(a => a != null).ToList();
            
            if (trainingData.ContainsKey(speciesName))
            {
                var data = trainingData[speciesName];
                data.currentAlive = agents.Count(a => a.IsAlive);
                
                if (agents.Count > 0)
                {
                    data.averageAge = agents.Average(a => a.age);
                }
                
                data.cooperationScore = CalculateSpeciesCooperationScore(agents);
                data.competitionScore = CalculateSpeciesCompetitionScore(agents);
            }
        }
    }
    
    private float CalculateSpeciesCooperationScore(List<SimpleEcoAgent> agents)
    {
        if (agents.Count == 0) return 0f;
        
        float score = 0f;
        int pairs = 0;
        
        foreach (var agent1 in agents)
        {
            foreach (var agent2 in agents)
            {
                if (agent1 != agent2 && agent1.IsAlive && agent2.IsAlive)
                {
                    float distance = Vector3.Distance(agent1.transform.position, agent2.transform.position);
                    if (distance < 10f)
                    {
                        score += Mathf.Max(0f, 10f - distance) / 10f;
                        pairs++;
                    }
                }
            }
        }
        
        return pairs > 0 ? score / pairs : 0f;
    }
    
    private float CalculateSpeciesCompetitionScore(List<SimpleEcoAgent> agents)
    {
        if (agents.Count == 0) return 0f;
        
        float score = 0f;
        int interactions = 0;
        
        foreach (var agent in agents)
        {
            if (!agent.IsAlive) continue;
            
            Collider[] nearby = Physics.OverlapSphere(agent.transform.position, agent.species.visionRange);
            
            foreach (var collider in nearby)
            {
                var otherAgent = collider.GetComponent<SimpleEcoAgent>();
                if (otherAgent != null && otherAgent.species.speciesName != agent.species.speciesName)
                {
                    score += 1f;
                    interactions++;
                }
            }
        }
        
        return interactions > 0 ? score / interactions : 0f;
    }

    private void UpdateGroupRewards()
    {
        if (!enableGroupRewards && !enableCompetitiveRewards && !enableEcosystemRewards) return;
        
        foreach (var kvp in speciesGroups)
        {
            string speciesName = kvp.Key;
            var agents = kvp.Value.Where(a => a != null && a.IsAlive).ToList();
            
            if (agents.Count == 0) continue;
            
            if (enableGroupRewards)
            {
                float groupReward = CalculateGroupReward(agents) * groupRewardMultiplier;
                if (groupReward != 0f)
                {
                    foreach (var agent in agents)
                    {
                        agent.AddReward(groupReward * Time.deltaTime);
                    }
                }
            }
            
            if (enableCompetitiveRewards)
            {
                float competitionReward = CalculateCompetitionReward(speciesName) * competitionRewardMultiplier;
                if (competitionReward != 0f)
                {
                    foreach (var agent in agents)
                    {
                        agent.AddReward(competitionReward * Time.deltaTime);
                    }
                }
            }
            
            if (enableEcosystemRewards)
            {
                float ecosystemReward = CalculateEcosystemReward(speciesName) * ecosystemRewardMultiplier;
                if (ecosystemReward != 0f)
                {
                    foreach (var agent in agents)
                    {
                        agent.AddReward(ecosystemReward * Time.deltaTime);
                    }
                }
            }
        }
    }
    
    private float CalculateGroupReward(List<SimpleEcoAgent> agents)
    {
        if (agents.Count < 2) return 0f;
        
        float reward = 0f;
        
        reward += agents.Count * 0.01f;
        
        float avgHealth = agents.Average(a => a.health / a.species.maxHealth);
        if (avgHealth > 0.7f) reward += 0.05f;
        
        return reward;
    }
    
    private float CalculateCompetitionReward(string speciesName)
    {
        var config = GetSpeciesConfig(speciesName);
        if (config == null) return 0f;
        
        float reward = 0f;
        int myPopulation = GetSpeciesPopulation(speciesName);
        
        foreach (string enemySpecies in config.enemySpecies)
        {
            int enemyPopulation = GetSpeciesPopulation(enemySpecies);
            if (myPopulation > enemyPopulation)
            {
                reward += 0.02f;
            }
            else if (myPopulation < enemyPopulation)
            {
                reward -= 0.01f;
            }
        }
        
        foreach (string allySpecies in config.allySpecies)
        {
            int allyPopulation = GetSpeciesPopulation(allySpecies);
            if (allyPopulation > 0)
            {
                reward += 0.01f;
            }
        }
        
        return reward;
    }
    
    private float CalculateEcosystemReward(string speciesName)
    {
        float reward = 0f;
        int totalPopulation = GetTotalPopulation();
        int speciesPopulation = GetSpeciesPopulation(speciesName);
        
        if (totalPopulation == 0) return 0f;
        
        float populationRatio = (float)speciesPopulation / totalPopulation;
        
        if (populationRatio > 0.05f && populationRatio < 0.4f)
        {
            reward += 0.02f;
        }
        else if (populationRatio > 0.6f)
        {
            reward -= 0.03f;
        }
        else if (populationRatio < 0.02f)
        {
            reward += 0.05f;
        }
        
        return reward;
    }

    private void UpdateSimulation()
    {
        if (autoReset && Time.time - episodeStartTime > episodeDuration)
        {
            ResetEpisode();
        }
        
        if (allAgents.Count == 0)
        {
            Debug.Log("All agents died. Resetting episode.");
            ResetEpisode();
        }
    }

    private void UpdateCulling()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;
        
        Vector3 cameraPos = mainCamera.transform.position;
        
        foreach (var agent in allAgents)
        {
            if (agent == null) continue;
            
            float distance = Vector3.Distance(cameraPos, agent.transform.position);
            bool shouldBeActive = distance <= cullingDistance;
            
            if (agent.gameObject.activeSelf != shouldBeActive)
            {
                agent.gameObject.SetActive(shouldBeActive);
            }
        }
    }

    private void CleanupDestroyedAgents()
    {
        allAgents.RemoveAll(agent => agent == null);
        
        foreach (var group in speciesGroups.Values)
        {
            group.RemoveAll(agent => agent == null);
        }
    }

    private void HandleMouseClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                SimpleEcoAgent agent = hit.collider.GetComponent<SimpleEcoAgent>();
                
                if (agent != null)
                {
                    selectedAgent = agent;
                    Debug.Log($"Selected Agent: {agent.name}");
                }
                else
                {
                    selectedAgent = null;
                }
            }
        }
    }

    private void HandleAgentDeath(SimpleEcoAgent agent)
    {
        Debug.Log($"Agent {agent.name} died");
        
        string speciesName = agent.species.speciesName;
        if (speciesGroups.ContainsKey(speciesName))
        {
            foreach (var teammate in speciesGroups[speciesName])
            {
                if (teammate != null && teammate != agent && teammate.IsAlive)
                {
                    float distance = Vector3.Distance(teammate.transform.position, agent.transform.position);
                    if (distance < 15f)
                    {
                        teammate.AddReward(-0.05f);
                    }
                }
            }
        }
        
        LogSpeciesPopulation();
    }

    private void HandleReproduction(SimpleEcoAgent parent1, SimpleEcoAgent parent2)
    {
        Debug.Log($"Reproduction: {parent1.species.speciesName}");
        
        var config = GetSpeciesConfig(parent1.species.speciesName);
        if (config != null && speciesGroups[parent1.species.speciesName].Count < config.maxPopulation && totalAgentsSpawned < maxTotalAgents)
        {
            Vector3 spawnPos = GetValidReproductionPosition(parent1.transform.position, parent2.transform.position);
            
            if (spawnPos != Vector3.zero)
            {
                GameObject prefabToUse = config.customAgentPrefab ?? parent1.species.customPrefab ?? agentPrefabs[0];
                
                GameObject offspring = Instantiate(prefabToUse, spawnPos, GetRandomRotation());
                SimpleEcoAgent offspringAgent = offspring.GetComponent<SimpleEcoAgent>();
                
                if (offspringAgent == null)
                {
                    offspringAgent = offspring.AddComponent<SimpleEcoAgent>();
                }
                
                if (offspringAgent != null)
                {
                    offspringAgent.SetMapBounds(mapOrigin, mapSize);
                    offspringAgent.SetSpecies(parent1.species);
                    offspringAgent.OnDeath += HandleAgentDeath;
                    offspringAgent.OnReproduction += HandleReproduction;
                    
                    allAgents.Add(offspringAgent);
                    speciesGroups[parent1.species.speciesName].Add(offspringAgent);
                    
                    offspring.name = $"{parent1.species.speciesName}_{totalAgentsSpawned++}";
                    
                    foreach (var teammate in speciesGroups[parent1.species.speciesName])
                    {
                        if (teammate != null && teammate.IsAlive)
                        {
                            float distance = Vector3.Distance(teammate.transform.position, spawnPos);
                            if (distance < 10f)
                            {
                                teammate.AddReward(0.02f);
                            }
                        }
                    }
                }
            }
        }
    }

    private Vector3 GetValidReproductionPosition(Vector3 parent1Pos, Vector3 parent2Pos)
    {
        Vector3 midpoint = (parent1Pos + parent2Pos) / 2f;
        
        for (int attempts = 0; attempts < 10; attempts++)
        {
            Vector3 offset = Random.insideUnitSphere * 3f;
            offset.y = 0f;
            Vector3 candidatePos = midpoint + offset;
            
            candidatePos.x = Mathf.Clamp(candidatePos.x, mapOrigin.x, mapOrigin.x + mapSize.x);
            candidatePos.z = Mathf.Clamp(candidatePos.z, mapOrigin.y, mapOrigin.y + mapSize.y);
            candidatePos.y = mapHeight;
            
            if (IsValidSpawnPosition(candidatePos))
            {
                return candidatePos;
            }
        }
        
        return GetValidSpawnPosition(Vector2.one);
    }

    public void ResetEpisode()
    {
        Debug.Log("Resetting Simple Ecosystem episode");
        
        foreach (var agent in allAgents.ToArray())
        {
            if (agent != null)
                Destroy(agent.gameObject);
        }
        
        foreach (var food in activeFoods.ToArray())
        {
            if (food != null)
                Destroy(food);
        }
        
        foreach (var predator in activePredators.ToArray())
        {
            if (predator != null)
                Destroy(predator);
        }

        AlgorithmBaseEntity[] algorithmBaseEntities = GameObject.FindObjectsByType<AlgorithmBaseEntity>(FindObjectsSortMode.None);

        foreach (var entity in algorithmBaseEntities)
        {
            if (entity != null)
            {
                Destroy(entity.gameObject);
            }
        }
        
        // SimpleEcoAgent[] agents = GameObject.FindObjectsByType<SimpleEcoAgent>(FindObjectsSortMode.None);
        //
        // foreach (var entity in agents)
        // {
        //     if (entity != null)
        //     {
        //         if(entity.gameObject != null)
        //             Destroy(entity.gameObject);
        //     }
        //         
        // }
        // 효율이 조져써ㅓ엉어 살려줘ㅓㅓ어엉 컴터가 계속 멈춘다ㅏㅏㅏ
        allAgents.Clear();
        activeFoods.Clear();
        activePredators.Clear();
        
        foreach (var group in speciesGroups.Values)
        {
            group.Clear();
        }
        
        foreach (var data in trainingData.Values)
        {
            data.totalSpawned = 0;
            data.currentAlive = 0;
            data.averageAge = 0f;
            data.cooperationScore = 0f;
            data.competitionScore = 0f;
            data.interactionCounts.Clear();
        }
        
        totalAgentsSpawned = 0;
        episodeStartTime = Time.time;
        selectedAgent = null;
        
        SpawnInitialPopulation();
        SpawnInitialFood();
        SpawnPredators();
    }

    public void RegisterAgent(SimpleEcoAgent agent)
    {
        if (agent?.species == null) return;
        
        string speciesName = agent.species.speciesName;
        if (!speciesGroups.ContainsKey(speciesName))
        {
            speciesGroups[speciesName] = new List<SimpleEcoAgent>();
        }
        
        if (!speciesGroups[speciesName].Contains(agent))
        {
            speciesGroups[speciesName].Add(agent);
            allAgents.Add(agent);
        }
    }

    public void UnregisterAgent(SimpleEcoAgent agent)
    {
        if (agent?.species == null) return;
        
        string speciesName = agent.species.speciesName;
        if (speciesGroups.ContainsKey(speciesName))
        {
            speciesGroups[speciesName].Remove(agent);

        }
        
        allAgents.Remove(agent);
    }

    public Dictionary<string, int> GetSpeciesPopulations()
    {
        var populations = new Dictionary<string, int>();
        
        foreach (var kvp in speciesGroups)
        {
            populations[kvp.Key] = kvp.Value.Count(a => a != null && a.IsAlive);
        }
        
        return populations;
    }

    public int GetSpeciesPopulation(string speciesName)
    {
        if (speciesGroups.ContainsKey(speciesName))
        {
            return speciesGroups[speciesName].Count(a => a != null && a.IsAlive);
        }
        return 0;
    }

    public int GetTotalPopulation()
    {
        return allAgents.Count(a => a != null && a.IsAlive) + 1;
    }

    public float GetBiodiversityIndex()
    {
        var populations = GetSpeciesPopulations();
        int totalPop = populations.Values.Sum();
        
        if (totalPop == 0) return 0f;
        
        float diversity = 0f;
        foreach (var pop in populations.Values)
        {
            if (pop > 0)
            {
                float proportion = (float)pop / totalPop;
                diversity -= proportion * Mathf.Log(proportion);
            }
        }
        
        return diversity;
    }

    private SimpleSpeciesConfig GetSpeciesConfig(string speciesName)
    {
        return speciesConfigs.Find(config => config.speciesData.speciesName == speciesName);
    }

    private void LogSpeciesPopulation()
    {
        foreach (var kvp in speciesGroups)
        {
            int alive = kvp.Value.Count(a => a != null && a.IsAlive);
            int total = kvp.Value.Count(a => a != null);
            Debug.Log($"{kvp.Key}: {alive}/{total} agents alive");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Vector3 mapCenter = new Vector3(mapOrigin.x + mapSize.x / 2f, mapHeight, mapOrigin.y + mapSize.y / 2f);
        Gizmos.DrawWireCube(mapCenter, new Vector3(mapSize.x, 0.1f, mapSize.y));
        
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            foreach (var food in activeFoods)
            {
                if (food != null)
                    Gizmos.DrawWireSphere(food.transform.position, 0.5f);
            }
            
            if (speciesGroups.Count > 0)
            {
                Color[] speciesColors = { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta, Color.cyan };
                int colorIndex = 0;
                
                foreach (var kvp in speciesGroups)
                {
                    Gizmos.color = speciesColors[colorIndex % speciesColors.Length];
                    foreach (var agent in kvp.Value)
                    {
                        if (agent != null && agent.IsAlive)
                        {
                            Gizmos.DrawWireSphere(agent.transform.position, 1f);
                        }
                    }
                    colorIndex++;
                }
            }
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        
        Color originalColor = GUI.color;
        Color originalBgColor = GUI.backgroundColor;
        
        GUI.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.9f);
        GUI.color = Color.white;
        
        GUILayout.BeginArea(new Rect(Screen.width - 350, 10, 340, 450));
        GUILayout.Label("Simple Ecosystem Status", EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).box);
        
        GUILayout.Label($"Total Agents: {GetTotalPopulation()}");
        GUILayout.Label($"Food Items: {activeFoods.Count}");
        GUILayout.Label($"Predators: {activePredators.Count}");
        GUILayout.Label($"Episode Time: {Time.time - episodeStartTime:F1}s");
        GUILayout.Label($"Biodiversity Index: {GetBiodiversityIndex():F2}");
        
        GUILayout.Space(5);
        GUILayout.Label("Map Info:", EditorStyles.boldLabel);
        GUILayout.Label($"Size: {mapSize.x:F0} x {mapSize.y:F0}");
        GUILayout.Label($"Origin: ({mapOrigin.x:F0}, {mapOrigin.y:F0})");
        
        GUILayout.Space(5);
        GUILayout.Label("Species Populations:", EditorStyles.boldLabel);
        
        var populations = GetSpeciesPopulations();
        foreach (var kvp in populations)
        {
            var config = GetSpeciesConfig(kvp.Key);
            int maxPop = config?.maxPopulation ?? 50;
            GUILayout.Label($"{kvp.Key}: {kvp.Value}/{maxPop}");
        }
        
        GUILayout.Space(10);
        
        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f, 0.9f);
        if (GUILayout.Button("Reset Episode"))
        {
            ResetEpisode();
        }
        
        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.3f, 0.9f);
        if (GUILayout.Button("Add Random Food"))
        {
            SpawnFood();
        }
        
        GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f, 0.9f);
        if (GUILayout.Button("Spawn Predator"))
        {
            Vector3 pos = GetValidPredatorSpawnPosition();
            if (pos != Vector3.zero && predatorPrefab != null)
            {
                GameObject predator = Instantiate(predatorPrefab, pos, GetRandomRotation());
                activePredators.Add(predator);
            }
        }
        
        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.5f, 0.3f, 0.8f, 0.9f);
        
        enableGroupRewards = GUILayout.Toggle(enableGroupRewards, "Group Rewards");
        enableCompetitiveRewards = GUILayout.Toggle(enableCompetitiveRewards, "Competition Rewards");
        enableEcosystemRewards = GUILayout.Toggle(enableEcosystemRewards, "Ecosystem Rewards");
        
        GUILayout.EndArea();
        
        if (selectedAgent != null)
        {
            GUI.backgroundColor = new Color(0.8f, 0.5f, 0.2f, 0.9f);
            GUI.color = Color.white;
            
            GUILayout.BeginArea(new Rect(10, Screen.height - 250, 300, 240));
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).box);
            
            var stats = selectedAgent.GetAgentStats();
            GUILayout.Label($"Selected: {selectedAgent.name}", EditorStyles.boldLabel);
            GUILayout.Label($"Species: {stats["species"]}");
            GUILayout.Label($"Health: {stats["health"]:F1}");
            GUILayout.Label($"Energy: {stats["energy"]:F1}");
            GUILayout.Label($"Age: {stats["age"]:F1}");
            GUILayout.Label($"Alive: {stats["isAlive"]}");
            GUILayout.Label($"Position: {stats["position"]}");
            GUILayout.Label($"Has Target: {stats["hasTarget"]}");
            
            if ((bool)stats["hasTarget"])
            {
                GUILayout.Label($"Target: {stats["targetPosition"]}");
            }
            
            GUI.backgroundColor = new Color(0.6f, 0.3f, 0.3f, 0.9f);
            if (GUILayout.Button("Deselect"))
            {
                selectedAgent = null;
            }
            
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        
        GUI.color = originalColor;
        GUI.backgroundColor = originalBgColor;
    }
}