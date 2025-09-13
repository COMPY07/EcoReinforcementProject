using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WFCRLAgent : Agent
{
    [Header("WFC Settings")]
    public int gridWidth = 15;
    public int gridHeight = 15;
    public List<TileData> allTileData;
    
    [Header("Training Parameters")]
    public float k1_efficiency = 0.2f;  // k1 value
    public float k2_efficiency = 0.1f;  // k2 value
    public int maxBacktrackDepth = 50;
    
    [Header("Environment")]
    public Transform gridParent;
    public bool enableDebugLogs = false;
    
    private BiomeType currentBiome;
    private LayoutType currentLayout;
    private Cell[,] currentGrid;
    private TrainWFCAlgorithm wfcAlgorithm;
    private Dictionary<BiomeType, List<TileData>> biomeToTiles;
    
    // Episode tracking
    private int episodeCount = 0;
    private int successfulEpisodes = 0;
    private float episodeStartTime;
    
    // Performance metrics
    private float lastCompleteness = 0f;
    private float lastBiomeCoherence = 0f;
    private float lastEfficiency = 0f;
    
    public override void Initialize()
    {
        biomeToTiles = new Dictionary<BiomeType, List<TileData>>();
        foreach (BiomeType biome in System.Enum.GetValues(typeof(BiomeType)))
        {
            biomeToTiles[biome] = allTileData.Where(t => t.biome == biome).ToList();
        }
        
        MaxStep = gridWidth * gridHeight * 2;
        
        if (enableDebugLogs)
        {
            Debug.Log($"WFC Agent initialized - Grid: {gridWidth}x{gridHeight}, Max Steps: {MaxStep}");
        }
    }
    
    public override void OnEpisodeBegin()
    {
        episodeCount++;
        episodeStartTime = Time.time;
        
        lastCompleteness = 0f;
        lastBiomeCoherence = 0f;
        lastEfficiency = 0f;
        
        currentBiome = (BiomeType)Random.Range(0, System.Enum.GetValues(typeof(BiomeType)).Length);
        currentLayout = Random.value > 0.5f ? LayoutType.Continuous : LayoutType.Sparse;
        
        ClearGrid();
        
        int seed = Random.Range(0, 10000);
        List<TileData> biomeTiles = biomeToTiles[currentBiome];
        
        wfcAlgorithm = new TrainWFCAlgorithm(gridWidth, gridHeight, biomeTiles, currentBiome, currentLayout, seed);
        currentGrid = wfcAlgorithm.GetGrid();
        
        if (enableDebugLogs && episodeCount % 100 == 0)
        {
            Debug.Log($"Episode {episodeCount}: {currentBiome} biome, {currentLayout} layout");
        }
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (currentGrid != null && x < currentGrid.GetLength(0) && y < currentGrid.GetLength(1))
                {
                    Cell cell = currentGrid[x, y];
                    
                    sensor.AddObservation(cell.isCollapsed ? 1f : 0f);
                    
                    int maxPossibleTiles = biomeToTiles[currentBiome].Count;
                    float normalizedEntropy = maxPossibleTiles > 0 ? (float)cell.Entropy / maxPossibleTiles : 0f;
                    sensor.AddObservation(normalizedEntropy);
                }
                else
                {
                    sensor.AddObservation(0f); // collapsed
                    sensor.AddObservation(0f); // entropy
                }
            }
        }
        
        sensor.AddObservation(currentBiome == BiomeType.City ? 1f : 0f);
        sensor.AddObservation(currentBiome == BiomeType.Desert ? 1f : 0f);
        sensor.AddObservation(currentBiome == BiomeType.Forest ? 1f : 0f);
        
        sensor.AddObservation(currentLayout == LayoutType.Continuous ? 1f : 0f);
        
        sensor.AddObservation(GetCompletionRate());
        sensor.AddObservation(Mathf.Clamp01(GetBacktrackCount() / (float)maxBacktrackDepth));
        sensor.AddObservation((float)StepCount / MaxStep);
        
        sensor.AddObservation(lastCompleteness);
        sensor.AddObservation(lastBiomeCoherence);
        sensor.AddObservation(lastEfficiency);
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (IsGenerationComplete())
        {
            float finalReward = CalculateFinalReward();
            AddReward(finalReward);
            
            successfulEpisodes++;
            LogEpisodeStats(true, finalReward);
            EndEpisode();
            return;
        }
        
        float rlAdjustment = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        
        bool stepSuccess = PerformWFCStep(rlAdjustment);
        
        float stepReward = CalculateStepReward(stepSuccess);
        AddReward(stepReward);
        
        if (!stepSuccess && CannotContinue())
        {
            float failureReward = CalculateFailureReward();
            AddReward(failureReward);
            
            LogEpisodeStats(false, GetCumulativeReward());
            EndEpisode();
        }
        else if (StepCount >= MaxStep)
        {
            float timeoutReward = CalculateTimeoutReward();
            AddReward(timeoutReward);
            
            LogEpisodeStats(false, GetCumulativeReward());
            EndEpisode();
        }
        
        UpdateMetrics();
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        
        float rlAdjustment = 0f;
        
        if (Input.GetKey(KeyCode.UpArrow))
            rlAdjustment = 1f;        
        else if (Input.GetKey(KeyCode.DownArrow))
            rlAdjustment = -1f;       
        else if (Input.GetKey(KeyCode.LeftArrow))
            rlAdjustment = -0.5f;     
        else if (Input.GetKey(KeyCode.RightArrow))
            rlAdjustment = 0.5f;      
        
        continuousActions[0] = rlAdjustment;
    }
    
    
    private bool PerformWFCStep(float rlAdjustment)
    {
        if (wfcAlgorithm == null) return false;
        return wfcAlgorithm.PerformSingleStep(rlAdjustment);
    }
    
    private bool IsGenerationComplete()
    {
        if (currentGrid == null) return false;
        
        for (int x = 0; x < currentGrid.GetLength(0); x++)
        {
            for (int y = 0; y < currentGrid.GetLength(1); y++)
            {
                if (!currentGrid[x, y].isCollapsed)
                    return false;
            }
        }
        return true;
    }
    
    private bool CannotContinue()
    {
        return wfcAlgorithm?.HasFailedGeneration() ?? true;
    }
    
    
    private float CalculateStepReward(bool stepSuccess)
    {
        if (!stepSuccess) return -0.01f;
        
        float reward = 0f;
        
        float currentCompletion = GetCompletionRate();
        float completionDelta = currentCompletion - lastCompleteness;
        reward += completionDelta * 0.1f;
        
        if (GetBacktrackCount() == 0)
            reward += 0.02f;
        
        float currentCoherence = CalculateBiomeCoherence();
        if (currentCoherence > lastBiomeCoherence)
            reward += (currentCoherence - lastBiomeCoherence) * 0.05f;
        
        return reward;
    }
    
    private float CalculateFinalReward()
    {
        float completeness = CalculateCompleteness();
        float biomeCoherence = CalculateBiomeCoherence();
        float efficiency = CalculateEfficiency();
        
        float totalReward = completeness + biomeCoherence + efficiency;
        
        Academy.Instance.StatsRecorder.Add("Reward/Completeness", completeness);
        Academy.Instance.StatsRecorder.Add("Reward/BiomeCoherence", biomeCoherence);
        Academy.Instance.StatsRecorder.Add("Reward/Efficiency", efficiency);
        Academy.Instance.StatsRecorder.Add("Reward/Total", totalReward);
        
        return totalReward;
    }
    
    private float CalculateFailureReward()
    {
        float completionRate = GetCompletionRate();
        return -1f + (completionRate * 0.3f); 
    }
    
    private float CalculateTimeoutReward()
    {
        float completionRate = GetCompletionRate();
        return -0.5f + (completionRate * 0.4f);
    }
    
    private float CalculateCompleteness()
    {
        if (currentGrid == null) return -1f;
        
        int totalCells = currentGrid.GetLength(0) * currentGrid.GetLength(1);
        int collapsedCells = 0;
        bool hasInvalidConfig = false;
        
        for (int x = 0; x < currentGrid.GetLength(0); x++)
        {
            for (int y = 0; y < currentGrid.GetLength(1); y++)
            {
                Cell cell = currentGrid[x, y];
                if (cell.isCollapsed)
                {
                    collapsedCells++;
                    if (cell.collapsedTile == null)
                        hasInvalidConfig = true;
                }
            }
        }
        
        if (hasInvalidConfig) return -1f;
        if (collapsedCells == totalCells) return 1f;
        return -0.5f;
    }
    
    private float CalculateBiomeCoherence()
    {
        if (currentGrid == null) return 0f;
        
        int totalTiles = 0;
        int coherentTiles = 0;
        
        for (int x = 0; x < currentGrid.GetLength(0); x++)
        {
            for (int y = 0; y < currentGrid.GetLength(1); y++)
            {
                Cell cell = currentGrid[x, y];
                if (cell.isCollapsed && cell.collapsedTile != null)
                {
                    totalTiles++;
                    if (CheckBiomeCoherence(cell, x, y))
                        coherentTiles++;
                }
            }
        }
        
        return totalTiles > 0 ? (float)coherentTiles / totalTiles : 0f;
    }
    
    private float CalculateEfficiency()
    {
        float Smax = MaxStep;
        float Sused = StepCount;
        float backtrackOperations = GetBacktrackCount();
        
        return k1_efficiency * (Smax - Sused) - k2_efficiency * backtrackOperations;
    }
    
    
    private bool CheckBiomeCoherence(Cell cell, int x, int y)
    {
        TileData tile = cell.collapsedTile;
        
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int neighborPos = new Vector2Int(x, y) + dir;
            if (IsValidPosition(neighborPos))
            {
                Cell neighbor = currentGrid[neighborPos.x, neighborPos.y];
                if (neighbor.isCollapsed && neighbor.collapsedTile != null)
                {
                    if (!AreCompatible(tile, neighbor.collapsedTile, dir))
                        return false;
                }
            }
        }
        
        switch (currentBiome)
        {
            case BiomeType.City:
                if (currentLayout == LayoutType.Continuous && tile.tileType == TileType.Path)
                    return HasPathNeighbors(x, y) || IsGridEdge(x, y);
                break;
                
            case BiomeType.Desert:
                if (tile.tileType == TileType.Impassable)
                    return !HasTooManyObstacleNeighbors(x, y, 3);
                break;
                
            case BiomeType.Forest:
                if (tile.tileType == TileType.Impassable)
                    return HasBalancedNeighborhood(x, y);
                break;
        }
        
        return true;
    }
    
    private bool AreCompatible(TileData tile1, TileData tile2, Vector2Int direction)
    {
        List<string> compatibleTiles = null;
        
        if (direction == Vector2Int.up) compatibleTiles = tile1.compatibleUp;
        else if (direction == Vector2Int.down) compatibleTiles = tile1.compatibleDown;
        else if (direction == Vector2Int.left) compatibleTiles = tile1.compatibleLeft;
        else if (direction == Vector2Int.right) compatibleTiles = tile1.compatibleRight;
        
        return compatibleTiles != null && 
               (compatibleTiles.Contains(tile2.tileName) || compatibleTiles.Contains("*"));
    }
    
    private bool HasPathNeighbors(int x, int y)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int pos = new Vector2Int(x, y) + dir;
            if (IsValidPosition(pos))
            {
                Cell neighbor = currentGrid[pos.x, pos.y];
                if (neighbor.isCollapsed && 
                    neighbor.collapsedTile != null && 
                    neighbor.collapsedTile.tileType == TileType.Path)
                    return true;
            }
        }
        return false;
    }
    
    private bool HasTooManyObstacleNeighbors(int x, int y, int threshold)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        int obstacleCount = 0;
        
        foreach (var dir in directions)
        {
            Vector2Int pos = new Vector2Int(x, y) + dir;
            if (IsValidPosition(pos))
            {
                Cell neighbor = currentGrid[pos.x, pos.y];
                if (neighbor.isCollapsed && 
                    neighbor.collapsedTile != null && 
                    neighbor.collapsedTile.tileType == TileType.Impassable)
                    obstacleCount++;
            }
        }
        return obstacleCount > threshold;
    }
    
    private bool HasBalancedNeighborhood(int x, int y)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        int pathCount = 0, obstacleCount = 0;
        
        foreach (var dir in directions)
        {
            Vector2Int pos = new Vector2Int(x, y) + dir;
            if (IsValidPosition(pos))
            {
                Cell neighbor = currentGrid[pos.x, pos.y];
                if (neighbor.isCollapsed && neighbor.collapsedTile != null)
                {
                    if (neighbor.collapsedTile.tileType == TileType.Path) pathCount++;
                    else if (neighbor.collapsedTile.tileType == TileType.Impassable) obstacleCount++;
                }
            }
        }
        
        return pathCount > 0 && obstacleCount < 4;
    }
    
    private bool IsValidPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < currentGrid.GetLength(0) && 
               pos.y >= 0 && pos.y < currentGrid.GetLength(1);
    }
    
    private bool IsGridEdge(int x, int y)
    {
        return x == 0 || y == 0 || 
               x == currentGrid.GetLength(0) - 1 || 
               y == currentGrid.GetLength(1) - 1;
    }
    
    private float GetCompletionRate()
    {
        if (currentGrid == null) return 0f;
        
        int totalCells = currentGrid.GetLength(0) * currentGrid.GetLength(1);
        int collapsedCells = 0;
        
        for (int x = 0; x < currentGrid.GetLength(0); x++)
        {
            for (int y = 0; y < currentGrid.GetLength(1); y++)
            {
                if (currentGrid[x, y].isCollapsed)
                    collapsedCells++;
            }
        }
        
        return (float)collapsedCells / totalCells;
    }
    
    private int GetBacktrackCount()
    {
        return wfcAlgorithm?.GetBacktrackCount() ?? 0;
    }
    
    private void UpdateMetrics()
    {
        lastCompleteness = CalculateCompleteness();
        lastBiomeCoherence = CalculateBiomeCoherence();
        lastEfficiency = CalculateEfficiency();
    }
    
    private void LogEpisodeStats(bool success, float finalReward)
    {
        float episodeDuration = Time.time - episodeStartTime;
        float successRate = episodeCount > 0 ? (float)successfulEpisodes / episodeCount : 0f;
        
        Academy.Instance.StatsRecorder.Add("Training/SuccessRate", success ? 1f : 0f);
        Academy.Instance.StatsRecorder.Add("Training/EpisodeDuration", episodeDuration);
        Academy.Instance.StatsRecorder.Add("Training/CompletionRate", GetCompletionRate());
        Academy.Instance.StatsRecorder.Add("Training/BacktrackCount", GetBacktrackCount());
        Academy.Instance.StatsRecorder.Add("Training/OverallSuccessRate", successRate);
        
        if (enableDebugLogs && episodeCount % 50 == 0)
        {
            Debug.Log($"Episode {episodeCount}: Success={success}, Reward={finalReward:F3}, " +
                     $"Success Rate={successRate:F2}");
        }
    }
    
    private void ClearGrid()
    {
        if (gridParent != null)
        {
            for (int i = gridParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(gridParent.GetChild(i).gameObject);
            }
        }
    }
}