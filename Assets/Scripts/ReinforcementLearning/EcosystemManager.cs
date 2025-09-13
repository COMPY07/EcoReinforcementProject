

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using UnityEditor;

public class EcosystemManager : MonoBehaviour
{
    [Header("Environment Setup")]
    public Vector2 worldSize = new Vector2(50f, 50f);
    public GameObject agentPrefab;
    public GameObject[] foodPrefabs;
    public Transform obstacleParent;
    
    [Header("Species Configuration")]
    public List<SpeciesConfig> speciesConfigs = new List<SpeciesConfig>();
    
    [Header("Food System")]
    public int maxFoodCount = 100;
    public float foodSpawnRate = 2f;
    public float foodRespawnTime = 5f;
    
    [Header("Simulation Control")]
    public bool autoReset = true;
    public float episodeDuration = 300f;
    public int maxAgents = 200;
    
    [Header("Performance")]
    public bool enableCulling = true;
    public float cullingDistance = 30f;
    
    [Header("Predator System")]
    public GameObject predatorPrefab;
    public int predatorCount = 3;
    public float predatorSpawnRadius = 15f;
    
    private List<GameObject> activePredators = new List<GameObject>();
    
    private List<EcosystemAgent> allAgents = new List<EcosystemAgent>();
    private List<GameObject> activeFoods = new List<GameObject>();
    private Dictionary<string, List<EcosystemAgent>> speciesGroups = new Dictionary<string, List<EcosystemAgent>>();
    
    private float episodeStartTime;
    private float lastFoodSpawn;
    private int totalAgentsSpawned;
    
    private EcosystemAgent selectedAgent;
    private AlgorithmBaseEntity selectedAlgorithmEntity;
    private Vector2 scrollPosition;
    
    [System.Serializable]
    public class SpeciesConfig
    {
        public SpeciesData speciesData;
        public int initialCount;
        public Vector2 spawnArea = Vector2.one;
        public bool enableRespawn = true;
        public int maxPopulation = 50;
    }

    private void Start()
    {
        InitializeEnvironment();
        SpawnInitialPopulation();
        SpawnPredators();
        episodeStartTime = Time.time;
    }

    private void Update()
    {
        UpdateFoodSystem();
        UpdatePopulation();
        UpdateSimulation();
        CleanupDestroyedAgents();
        HandleMouseClick(); 
        
        // if (enableCulling)
        //     UpdateCulling();
    }
    
    private void HandleMouseClick()
    {
        if (Input.GetMouseButtonDown(0)) 
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                GameObject clickedObject = hit.collider.gameObject;
                
                EcosystemAgent agent = clickedObject.GetComponent<EcosystemAgent>();
                AlgorithmBaseEntity algorithmEntity = clickedObject.GetComponent<AlgorithmBaseEntity>();
                
                if (agent != null)
                {
                    selectedAgent = agent;
                    selectedAlgorithmEntity = null;
                    Debug.Log($"Selected Agent: {agent.name}");
                }
                else if (algorithmEntity != null)
                {
                    selectedAlgorithmEntity = algorithmEntity;
                    selectedAgent = null;
                    Debug.Log($"Selected Algorithm Entity: {algorithmEntity.EntityName}");
                }
                else
                {
                    selectedAgent = null;
                    selectedAlgorithmEntity = null;
                }
            }
        }
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

    public void RepositionPredator()
    {
        foreach (GameObject obj in activePredators)
        {
            Vector3 pos = GetValidPredatorSpawnPosition();
            obj.transform.position = pos;
        }
    }
    
    private Vector3 GetValidPredatorSpawnPosition()
    {
        var aliveAgents = allAgents.Where(a => a != null && a.IsAlive).ToList();
        
        if (aliveAgents.Count > 0)
        {
            EcosystemAgent targetAgent = aliveAgents[Random.Range(0, aliveAgents.Count)];
            
            for (int attempts = 0; attempts < 30; attempts++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * predatorSpawnRadius;
                Vector3 candidatePos = targetAgent.transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
                
                candidatePos.x = Mathf.Clamp(candidatePos.x, 0f, worldSize.x);
                candidatePos.z = Mathf.Clamp(candidatePos.z, 0f, worldSize.y);
                candidatePos.y = 1f;
                
                if (AStarManager.Instance != null)
                {
                    Vector3 validPos = AStarManager.Instance.GetRandomWalkablePosition(candidatePos, 0.5f, 3);
                    if (Vector3.Distance(candidatePos, validPos) < 1f && IsPredatorPositionSafe(validPos))
                    {
                        validPos.y = 1f;
                        return validPos;
                    }
                }
                else if (IsPredatorPositionSafe(candidatePos))
                {
                    return candidatePos;
                }
            }
        }
        
        for (int attempts = 0; attempts < 20; attempts++)
        {
            Vector3 candidatePos = new Vector3(
                Random.Range(0f, worldSize.x), 
                1f, 
                Random.Range(0f, worldSize.y)
            );
            
            if (AStarManager.Instance != null)
            {
                Vector3 validPos = AStarManager.Instance.GetRandomWalkablePosition(candidatePos, 0.5f, 3);
                if (Vector3.Distance(candidatePos, validPos) < 1f && IsPredatorPositionSafe(validPos))
                {
                    validPos.y = 1f;
                    return validPos;
                }
            }
            else if (IsPredatorPositionSafe(candidatePos))
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
    
    private void CleanupDestroyedAgents()
    {
        allAgents.RemoveAll(agent => agent == null);
        
        foreach (var group in speciesGroups.Values)
        {
            group.RemoveAll(agent => agent == null);
        }
    }

    private void InitializeEnvironment()
    {
        foreach (var config in speciesConfigs)
        {
            if (!speciesGroups.ContainsKey(config.speciesData.speciesName))
            {
                speciesGroups[config.speciesData.speciesName] = new List<EcosystemAgent>();
            }
        }
        
        SpawnInitialFood();
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
        
        Debug.Log($"Spawned {totalAgentsSpawned} agents across {speciesConfigs.Count} species");
    }

    private EcosystemAgent SpawnAgent(SpeciesConfig config)
    {
        if (totalAgentsSpawned >= maxAgents)
        {
            Debug.LogWarning($"Cannot spawn agent - reached max agents limit: {maxAgents}");
            return null;
        }
            
        Vector3 spawnPos = GetRandomSpawnPosition(config.spawnArea);
        Debug.Log($"Spawning {config.speciesData.speciesName} at position: {spawnPos}");
        
        GameObject prefabToUse = config.speciesData.customPrefab != null ? 
                                config.speciesData.customPrefab : agentPrefab;
        
        if (prefabToUse == null)
        {
            Debug.LogError($"No prefab available for {config.speciesData.speciesName}! CustomPrefab: {config.speciesData.customPrefab}, AgentPrefab: {agentPrefab}");
            return null;
        }
        
        GameObject agentObj = Instantiate(prefabToUse, spawnPos, GetRandomRotation());
        Debug.Log($"Instantiated {agentObj.name} at {agentObj.transform.position}");
        
        EcosystemAgent agent = agentObj.GetComponent<EcosystemAgent>();
        if (agent == null)
        {
            Debug.Log($"No EcosystemAgent found on {agentObj.name}, adding component...");
            agent = agentObj.AddComponent<EcosystemAgent>();
        }
        
        if (agent != null)
        {
            Debug.Log($"Setting species data for {agentObj.name}: {config.speciesData.speciesName}");
            agent.species = config.speciesData;
            agent.OnDeath += HandleAgentDeath;
            agent.OnReproduction += HandleReproduction;
            
            allAgents.Add(agent);
            speciesGroups[config.speciesData.speciesName].Add(agent);
            totalAgentsSpawned++;
            
            agentObj.name = $"{config.speciesData.speciesName}_{totalAgentsSpawned}";
            
            Debug.Log($"Successfully spawned {agentObj.name} - Total agents: {allAgents.Count}");
        }
        else
        {
            Debug.LogError($"Failed to get or create EcosystemAgent component for {agentObj.name}");
        }
        
        return agent;
    }

    private Vector3 GetRandomSpawnPosition(Vector2 spawnArea)
    {
        int maxAttempts = 50;
        
        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            float maxX = worldSize.x * spawnArea.x;
            float maxZ = worldSize.y * spawnArea.y;
            
            float x = Random.Range(0f, maxX);
            float z = Random.Range(0f, maxZ);
            Vector3 candidatePos = new Vector3(x, 1f, z);
            
            Debug.Log($"Attempt {attempts + 1}: Testing position ({x:F2}, {z:F2})");
            
            if (IsValidSpawnPosition(candidatePos))
            {
                Debug.Log($"Found valid spawn position after {attempts + 1} attempts: {candidatePos}");
                return candidatePos;
            }
        }
        
        if (AStarManager.Instance != null)
        {
            Vector3 centerPos = new Vector3(worldSize.x / 2f, 1f, worldSize.y / 2f);
            Vector3 aStarPos = AStarManager.Instance.GetRandomWalkablePosition(centerPos, worldSize.x / 2f, 10);
            Debug.LogWarning($"Using A* fallback position: {aStarPos}");
            return aStarPos;
        }
        
        Debug.LogError($"Failed to find valid spawn position after {maxAttempts} attempts and no A* Manager available!");
        return new Vector3(worldSize.x / 2f, 1f, worldSize.y / 2f);
    }

    private bool IsValidSpawnPosition(Vector3 pos)
    {
        if (AStarManager.Instance != null)
        {
            Vector3 validPos = AStarManager.Instance.GetRandomWalkablePosition(pos, 0.1f, 1);
            bool isWalkable = Vector3.Distance(pos, validPos) < 0.5f;
            
            Debug.Log($"Checking position {pos} - Valid walkable position: {validPos}, Distance: {Vector3.Distance(pos, validPos):F2}, IsWalkable: {isWalkable}");
            
            if (!isWalkable)
            {
                Debug.Log($"Position {pos} is not walkable (likely water/obstacle)");
                return false;
            }
        }
        
        Collider[] overlapping = Physics.OverlapSphere(pos, 1f);
        foreach (var col in overlapping)
        {
            if (col.CompareTag("Obstacle") || col.GetComponent<EcosystemAgent>())
            {
                Debug.Log($"Position {pos} blocked by {col.name} with tag {col.tag}");
                return false;
            }
        }
        
        return true;
    }

    private Quaternion GetRandomRotation()
    {
        return Quaternion.Euler(0, Random.Range(0f, 360f), 0);
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

    private void SpawnInitialFood()
    {
        for (int i = 0; i < maxFoodCount / 2; i++)
        {
            SpawnFood();
        }
    }

    private void SpawnFood()
    {
        if (foodPrefabs.Length == 0) return;
        
        Vector3 spawnPos = GetValidFoodSpawnPosition();
        
        if (spawnPos != Vector3.zero)
        {
            GameObject foodPrefab = foodPrefabs[Random.Range(0, foodPrefabs.Length)];
            GameObject food = Instantiate(foodPrefab, spawnPos, Quaternion.identity);
            activeFoods.Add(food);
        }
    }

    private Vector3 GetValidFoodSpawnPosition()
    {
        int maxAttempts = 30;
        
        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            float x = Random.Range(0f, worldSize.x);
            float z = Random.Range(0f, worldSize.y);
            Vector3 candidatePos = new Vector3(x, 1f, z);
            
            if (AStarManager.Instance != null)
            {
                Vector3 validPos = AStarManager.Instance.GetRandomWalkablePosition(candidatePos, 0.5f, 3);
                
                if (Vector3.Distance(candidatePos, validPos) < 1f)
                {
                    if (IsFoodPositionClear(validPos))
                    {
                        validPos.y = 1f;
                        return validPos;
                    }
                }
            }
            else
            {
                if (IsValidSpawnPosition(candidatePos) && IsFoodPositionClear(candidatePos))
                {
                    return candidatePos;
                }
            }
        }
        
        Debug.LogWarning("Could not find valid position for food spawn after " + maxAttempts + " attempts");
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

    private void CleanupDestroyedFood()
    {
        activeFoods.RemoveAll(food => food == null);
    }

    private void UpdatePopulation()
    {
        foreach (var config in speciesConfigs)
        {
            if (!config.enableRespawn) continue;
            
            var speciesList = speciesGroups[config.speciesData.speciesName];
            
            int aliveCount = 0;
            foreach (var agent in speciesList)
            {
                if (agent != null && agent.IsAlive)
                {
                    aliveCount++;
                }
            }
            
            int targetCount = config.initialCount;
            
            if (aliveCount < targetCount && speciesList.Count < config.maxPopulation)
            {
                int spawnCount = Mathf.Min(targetCount - aliveCount, 3);
                for (int i = 0; i < spawnCount; i++)
                {
                    SpawnAgent(config);
                }
            }
        }
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

    private void HandleAgentDeath(EcosystemAgent agent)
    {
        Debug.Log($"Agent {agent.name} died but will respawn through episode reset");
        LogSpeciesPopulation();
    }

    private void HandleReproduction(EcosystemAgent parent1, EcosystemAgent parent2)
    {
        Debug.Log($"Reproduction: {parent1.species.speciesName}");
        
        var config = GetSpeciesConfig(parent1.species);
        if (config != null && speciesGroups[parent1.species.speciesName].Count < config.maxPopulation)
        {
            Vector3 spawnPos = GetValidReproductionPosition(parent1.transform.position, parent2.transform.position);
            
            if (spawnPos != Vector3.zero)
            {
                GameObject prefabToUse = parent1.species.customPrefab != null ? 
                                        parent1.species.customPrefab : agentPrefab;
                
                GameObject offspring = Instantiate(prefabToUse, spawnPos, GetRandomRotation());
                EcosystemAgent offspringAgent = offspring.GetComponent<EcosystemAgent>();
                
                if (offspringAgent == null)
                {
                    offspringAgent = offspring.AddComponent<EcosystemAgent>();
                }
                
                if (offspringAgent != null)
                {
                    offspringAgent.species = parent1.species;
                    offspringAgent.OnDeath += HandleAgentDeath;
                    offspringAgent.OnReproduction += HandleReproduction;
                    
                    allAgents.Add(offspringAgent);
                    speciesGroups[parent1.species.speciesName].Add(offspringAgent);
                    
                    offspring.name = $"{parent1.species.speciesName}_{totalAgentsSpawned++}";
                }
            }
        }
    }

    private Vector3 GetValidReproductionPosition(Vector3 parent1Pos, Vector3 parent2Pos)
    {
        Vector3 midpoint = (parent1Pos + parent2Pos) / 2f;
        int maxAttempts = 10;
        
        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            Vector3 offset = Random.insideUnitSphere * 3f;
            offset.y = 0f;
            Vector3 candidatePos = midpoint + offset;
            
            candidatePos.x = Mathf.Clamp(candidatePos.x, 0f, worldSize.x);
            candidatePos.z = Mathf.Clamp(candidatePos.z, 0f, worldSize.y);
            candidatePos.y = 1f;
            
            if (IsValidSpawnPosition(candidatePos))
            {
                return candidatePos;
            }
        }
        
        return GetRandomSpawnPosition(Vector2.one);
    }

    private SpeciesConfig GetSpeciesConfig(SpeciesData speciesData)
    {
        return speciesConfigs.Find(config => config.speciesData == speciesData);
    }

    public void ResetEpisode()
    {
        Debug.Log("Resetting ecosystem episode");
        
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
        
        allAgents.Clear();
        activeFoods.Clear();
        activePredators.Clear();
        
        foreach (var group in speciesGroups.Values)
        {
            group.Clear();
        }
        
        totalAgentsSpawned = 0;
        episodeStartTime = Time.time;
        
        selectedAgent = null;
        selectedAlgorithmEntity = null;
        
        SpawnInitialPopulation();
        SpawnInitialFood();
        SpawnPredators();
    }

    public void AddSpecies(SpeciesData speciesData, int count)
    {
        var newConfig = new SpeciesConfig
        {
            speciesData = speciesData,
            initialCount = count,
            spawnArea = Vector2.one,
            enableRespawn = true,
            maxPopulation = count * 3
        };
        
        speciesConfigs.Add(newConfig);
        
        if (!speciesGroups.ContainsKey(speciesData.speciesName))
        {
            speciesGroups[speciesData.speciesName] = new List<EcosystemAgent>();
        }
        
        for (int i = 0; i < count; i++)
        {
            SpawnAgent(newConfig);
        }
    }

    public void RemoveSpecies(string speciesName)
    {
        if (speciesGroups.ContainsKey(speciesName))
        {
            foreach (var agent in speciesGroups[speciesName].ToArray())
            {
                if (agent != null)
                {
                    allAgents.Remove(agent);
                    Destroy(agent.gameObject);
                }
            }
            
            speciesGroups[speciesName].Clear();
            speciesConfigs.RemoveAll(config => config.speciesData.speciesName == speciesName);
        }
    }

    public int GetSpeciesCount(string speciesName)
    {
        if (speciesGroups.ContainsKey(speciesName))
            return speciesGroups[speciesName].Count;
        return 0;
    }

    public List<EcosystemAgent> GetAgentsOfSpecies(string speciesName)
    {
        if (speciesGroups.ContainsKey(speciesName))
            return new List<EcosystemAgent>(speciesGroups[speciesName]);
        return new List<EcosystemAgent>();
    }

    public EcosystemAgent GetNearestAgent(Vector3 position, string speciesName = null)
    {
        EcosystemAgent nearest = null;
        float minDistance = float.MaxValue;
        
        var searchList = string.IsNullOrEmpty(speciesName) ? allAgents : 
                        speciesGroups.ContainsKey(speciesName) ? speciesGroups[speciesName] : new List<EcosystemAgent>();
        
        foreach (var agent in searchList)
        {
            if (agent == null) continue;
            
            float distance = Vector3.Distance(position, agent.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = agent;
            }
        }
        
        return nearest;
    }

    private void LogSpeciesPopulation()
    {
        foreach (var kvp in speciesGroups)
        {
            Debug.Log($"{kvp.Key}: {kvp.Value.Count} agents");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Vector3 center = new Vector3(worldSize.x / 2f, 1f, worldSize.y / 2f);
        Gizmos.DrawWireCube(center, new Vector3(worldSize.x, 2f, worldSize.y));
        
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            foreach (var food in activeFoods)
            {
                if (food != null)
                    Gizmos.DrawWireSphere(food.transform.position, 0.5f);
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
        
        GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 400));
        GUILayout.Label("Ecosystem Status", EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).box);
        
        GUILayout.Label($"Total Agents: {allAgents.Count}");
        GUILayout.Label($"Food Items: {activeFoods.Count}");
        GUILayout.Label($"Predators: {activePredators.Count}");
        GUILayout.Label($"Episode Time: {Time.time - episodeStartTime:F1}s");
        
        GUILayout.Space(10);
        
        foreach (var kvp in speciesGroups)
        {
            int aliveCount = 0;
            foreach (var agent in kvp.Value)
            {
                if (agent != null && agent.IsAlive) aliveCount++;
            }
            GUILayout.Label($"{kvp.Key}: {aliveCount}/{kvp.Value.Count}");
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
        
        GUILayout.EndArea();
        
        if (selectedAgent != null || selectedAlgorithmEntity != null)
        {
            GUI.backgroundColor = new Color(0.8f, 0.5f, 0.2f, 0.9f);
            GUI.color = Color.white;
            
            
            float infoWidth = 350f;
            float infoHeight = 300f;
            GUILayout.BeginArea(new Rect(10, Screen.height - infoHeight - 10, infoWidth, infoHeight));
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).box);
            
            if (selectedAgent != null)
            {
                DrawAgentInfo(selectedAgent);
            }
            else if (selectedAlgorithmEntity != null)
            {
                DrawAlgorithmEntityInfo(selectedAlgorithmEntity);
            }
            
            GUILayout.Space(10);
            
            GUI.backgroundColor = new Color(0.6f, 0.3f, 0.3f, 0.9f);
            
            if (GUILayout.Button("Deselect"))
            {
                selectedAgent = null;
                selectedAlgorithmEntity = null;
            }
            
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        else
        {
            GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.8f); 
            
            GUI.color = Color.yellow; 
            
            
            GUILayout.BeginArea(new Rect(10, Screen.height - 60, 300, 50));
            GUILayout.Label("Click on an Agent or Algorithm Entity to view details", EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).box);
            GUILayout.EndArea();
        }
        

        GUI.color = originalColor;
        GUI.backgroundColor = originalBgColor;
    }
    
    private void DrawAgentInfo(EcosystemAgent agent)
    {
        if (agent == null) return;
        
        GUILayout.Label($"[ML-Agents] {agent.name}", EditorStyles.boldLabel);
        GUILayout.Space(5);
        

        if (agent.species != null)
        {
            GUILayout.Label($"Species: {agent.species.speciesName}");
            GUILayout.Label($"Size: {agent.species.size:F2}");
        }
        

        GUILayout.Label($"Alive: {(agent.IsAlive ? "Yes" : "No")}");
        GUILayout.Label($"Health: {agent.health:F1} / {(agent.species != null ? agent.species.maxHealth : 100):F1}");
        GUILayout.Label($"Energy: {agent.energy:F1} / {(agent.species != null ? agent.species.maxEnergy : 100):F1}");
        GUILayout.Label($"Age: {agent.age:F1} / {(agent.species != null ? agent.species.maxAge : 100):F1}");
        

        Vector3 pos = agent.transform.position;
        GUILayout.Label($"Position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
        

        Rigidbody rb = agent.GetComponent<Rigidbody>();
        if (rb != null)
        {
            GUILayout.Label($"Velocity: {rb.linearVelocity.magnitude:F2}");
        }
        

        if (agent.species != null)
        {
            GUILayout.Space(5);
            GUILayout.Label("Abilities:", EditorStyles.boldLabel);
            GUILayout.Label($"Can Eat: {agent.species.canEat}");
            GUILayout.Label($"Can Attack: {agent.species.canAttack}");
            GUILayout.Label($"Can Reproduce: {agent.species.canReproduce}");
            GUILayout.Label($"Can Flee: {agent.species.canFlee}");
            
            GUILayout.Space(5);
            GUILayout.Label($"Vision Range: {agent.species.visionRange:F1}");
            GUILayout.Label($"Vision Angle: {agent.species.visionAngle:F1}°");
            GUILayout.Label($"Move Speed: {agent.species.moveSpeed:F1}");
        }
    }
    
    private void DrawAlgorithmEntityInfo(AlgorithmBaseEntity entity)
    {
        if (entity == null) return;
        
        GUILayout.Label($"[Algorithm] {entity.EntityName}", EditorStyles.boldLabel);
        GUILayout.Space(5);
        

        GUILayout.Label($"ID: {entity.EntityId}");
        GUILayout.Label($"Alive: {(entity.IsAlive ? "Yes" : "No")}");
        GUILayout.Label($"Age: {entity.Age:F1}");
        

        
        GUILayout.Space(5);
        GUILayout.Label("Survival Stats:", EditorStyles.boldLabel);
        GUILayout.Label($"Health: {entity.Health:F1}");
        GUILayout.Label($"Energy: {entity.Energy:F1}");
        GUILayout.Label($"Hunger: {entity.Hunger:F1}");
        GUILayout.Label($"Thirst: {entity.Thirst:F1}");
        GUILayout.Label($"Hydration: {entity.Hydration:F1}");
        GUILayout.Label($"Stress: {entity.StressLevel:F1}");
        

        Vector3 pos = entity.Position;
        GUILayout.Label($"Position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
    }
}