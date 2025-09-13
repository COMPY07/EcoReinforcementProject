using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnhancedWFC3DExtension : WFC3DExtension
{
    private List<EnhancedStructureData> enhancedStructureDatabase;
    private Dictionary<Vector2Int, List<StructureType>> structureAdjacencyMap;
    private List<Vector2Int> pathTiles;
    private Dictionary<StructureType, List<Vector2Int>> placedByType;
    
    // Poisson disk sampling을 위한 변수들
    private float[,] influenceMap;
    private const float POISSON_RADIUS = 2f;
    
    public EnhancedWFC3DExtension(Cell[,] baseGrid, 
                                    List<EnhancedStructureData> structures, BiomeType biome, int seed = 0) 
        : base(baseGrid, null, biome, seed) // base constructor call
    {
        if (structures == null) structures = new List<EnhancedStructureData>();
        enhancedStructureDatabase = structures.Where(s => s.compatibleBiomes.Contains(biome)).ToList();
        placedByType = new Dictionary<StructureType, List<Vector2Int>>();
        InitializePathTiles();
        InitializeInfluenceMap();
        
        OnStatusUpdate?.Invoke($"Enhanced 3D Extension initialized with {enhancedStructureDatabase.Count} structure types");
    }
    
    private void InitializePathTiles()
    {
        pathTiles = new List<Vector2Int>();
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                Cell cell = grid[x, y];
                if (cell.isCollapsed && cell.collapsedTile != null && 
                    cell.collapsedTile.tileType == TileType.Path)
                {
                    pathTiles.Add(new Vector2Int(x, y));
                }
            }
        }
        
        OnStatusUpdate?.Invoke($"Found {pathTiles.Count} path tiles");
    }
    
    private void InitializeInfluenceMap()
    {
        influenceMap = new float[grid.GetLength(0), grid.GetLength(1)];
    }
    
    public new StructurePlacementResult PlaceStructures()
    {
        OnStatusUpdate?.Invoke("Starting enhanced structure placement...");
        
        placedStructures.Clear();
        placedByType.Clear();
        int totalPlaced = 0;
        
        var strategySorted = enhancedStructureDatabase
            .OrderBy(s => GetPlacementPriority(s.placementStrategy))
            .ThenByDescending(s => s.placementProbability);
        
        foreach (var structureData in strategySorted)
        {
            int placedCount = PlaceStructureTypeWithStrategy(structureData);
            totalPlaced += placedCount;
            
            if (!placedByType.ContainsKey(structureData.structureType))
                placedByType[structureData.structureType] = new List<Vector2Int>();
            
            OnStatusUpdate?.Invoke($"Placed {placedCount} {structureData.structureType} structures using {structureData.placementStrategy} strategy");
        }
        
        OnStatusUpdate?.Invoke($"Enhanced structure placement completed: {totalPlaced} total structures");
        
        return new StructurePlacementResult
        {
            placedStructures = new Dictionary<Vector2Int, StructureInstance>(placedStructures),
            totalPlaced = totalPlaced,
            structuresByType = GetStructureCountByType()
        };
    }
    
    private int GetPlacementPriority(PlacementStrategy strategy)
    {
        return strategy switch
        {
            PlacementStrategy.HeightBased => 1,    // 먼저 높이 기반 배치
            PlacementStrategy.Grid => 2,           // 그 다음 격자 배치
            PlacementStrategy.PathAdjacent => 3,   // 경로 인접 배치
            PlacementStrategy.Clustered => 4,      // 클러스터 배치
            PlacementStrategy.Organic => 5,        // 유기적 배치
            PlacementStrategy.Scattered => 6,      // 분산 배치
            PlacementStrategy.Random => 7,         // 마지막에 랜덤 배치
            _ => 10
        };
    }
    
    private int PlaceStructureTypeWithStrategy(EnhancedStructureData structureData)
    {
        return structureData.placementStrategy switch
        {
            PlacementStrategy.Random => PlaceRandom(structureData),
            PlacementStrategy.Clustered => PlaceClustered(structureData),
            PlacementStrategy.Grid => PlaceGrid(structureData),
            PlacementStrategy.Scattered => PlaceScattered(structureData),
            PlacementStrategy.PathAdjacent => PlacePathAdjacent(structureData),
            PlacementStrategy.HeightBased => PlaceHeightBased(structureData),
            PlacementStrategy.Organic => PlaceOrganic(structureData),
            _ => PlaceRandom(structureData)
        };
    }
    
    private int PlaceRandom(EnhancedStructureData structureData)
    {
        int placedCount = 0;
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                
                if (CanPlaceStructureEnhanced(position, structureData))
                {
                    if (random.NextDouble() <= structureData.placementProbability)
                    {
                        PlaceStructureAtEnhanced(position, structureData);
                        placedCount++;
                    }
                }
            }
        }
        
        return placedCount;
    }
    
    private int PlaceClustered(EnhancedStructureData structureData)
    {
        int placedCount = 0;
        int attemptedClusters = 0;
        int maxClusters = Mathf.RoundToInt(grid.GetLength(0) * grid.GetLength(1) * structureData.placementProbability * 0.1f);
        
        while (attemptedClusters < maxClusters)
        {
            // 랜덤한 클러스터 중심 선택
            Vector2Int clusterCenter = new Vector2Int(
                random.Next(grid.GetLength(0)),
                random.Next(grid.GetLength(1))
            );
            
            if (CanPlaceStructureEnhanced(clusterCenter, structureData))
            {
                // 클러스터 배치
                int clusterPlaced = PlaceClusterAt(clusterCenter, structureData);
                placedCount += clusterPlaced;
                
                if (clusterPlaced > 0)
                {
                    UpdateInfluenceMap(clusterCenter, structureData.clusterRadius);
                }
            }
            
            attemptedClusters++;
        }
        
        return placedCount;
    }
    
    private int PlaceClusterAt(Vector2Int center, EnhancedStructureData structureData)
    {
        int placed = 0;
        List<Vector2Int> clusterPositions = GetClusterPositions(center, structureData.clusterRadius, structureData.clusterSize);
        
        foreach (Vector2Int pos in clusterPositions)
        {
            if (CanPlaceStructureEnhanced(pos, structureData) && 
                random.NextDouble() <= structureData.clusterDensity)
            {
                PlaceStructureAtEnhanced(pos, structureData);
                placed++;
            }
        }
        
        return placed;
    }
    
    private List<Vector2Int> GetClusterPositions(Vector2Int center, float radius, int maxCount)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        int radiusInt = Mathf.RoundToInt(radius);
        
        for (int dx = -radiusInt; dx <= radiusInt; dx++)
        {
            for (int dy = -radiusInt; dy <= radiusInt; dy++)
            {
                Vector2Int pos = center + new Vector2Int(dx, dy);
                
                if (IsValidPosition(pos) && Vector2Int.Distance(center, pos) <= radius)
                {
                    positions.Add(pos);
                }
            }
        }
        
        positions = positions.OrderBy(p => Vector2Int.Distance(center, p))
                           .Take(maxCount)
                           .OrderBy(p => random.NextDouble()) // 셔플
                           .ToList();
        
        return positions;
    }
    
    private int PlaceGrid(EnhancedStructureData structureData)
    {
        int placedCount = 0;
        int spacing = Mathf.Max(1, structureData.minDistanceFromSameType);
        
        for (int x = spacing/2; x < grid.GetLength(0); x += spacing)
        {
            for (int y = spacing/2; y < grid.GetLength(1); y += spacing)
            {
                Vector2Int position = new Vector2Int(x, y);
                
                if (CanPlaceStructureEnhanced(position, structureData) &&
                    random.NextDouble() <= structureData.placementProbability)
                {
                    // 약간의 랜덤 오프셋 추가 (완전한 격자를 피하기 위해)
                    Vector2Int offset = new Vector2Int(
                        random.Next(-spacing/3, spacing/3 + 1),
                        random.Next(-spacing/3, spacing/3 + 1)
                    );
                    
                    Vector2Int adjustedPos = position + offset;
                    if (IsValidPosition(adjustedPos) && CanPlaceStructureEnhanced(adjustedPos, structureData))
                    {
                        PlaceStructureAtEnhanced(adjustedPos, structureData);
                        placedCount++;
                    }
                }
            }
        }
        
        return placedCount;
    }
    
    private int PlaceScattered(EnhancedStructureData structureData)
    {
        int placedCount = 0;
        List<Vector2Int> candidates = GeneratePoissonDiskSamples(POISSON_RADIUS, structureData.placementProbability);
        
        foreach (Vector2Int pos in candidates)
        {
            if (CanPlaceStructureEnhanced(pos, structureData))
            {
                PlaceStructureAtEnhanced(pos, structureData);
                placedCount++;
                UpdateInfluenceMap(pos, POISSON_RADIUS);
            }
        }
        
        return placedCount;
    }
    
    private int PlacePathAdjacent(EnhancedStructureData structureData)
    {
        int placedCount = 0;
        
        foreach (Vector2Int pathPos in pathTiles)
        {
            foreach (Vector2Int dir in GetNeighborDirections())
            {
                Vector2Int adjacentPos = pathPos + dir;
                
                if (IsValidPosition(adjacentPos) && 
                    CanPlaceStructureEnhanced(adjacentPos, structureData) &&
                    GetDistanceToNearestPath(adjacentPos) <= structureData.maxDistanceFromPath)
                {
                    if (random.NextDouble() <= structureData.placementProbability)
                    {
                        PlaceStructureAtEnhanced(adjacentPos, structureData);
                        placedCount++;
                    }
                }
            }
        }
        
        return placedCount;
    }
    
    private int PlaceHeightBased(EnhancedStructureData structureData)
    {
        int placedCount = 0;
        
        List<(Vector2Int pos, float score)> heightCandidates = new List<(Vector2Int, float)>();
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                
                if (CanPlaceStructureEnhanced(position, structureData))
                {
                    float height = heightMap[x, y];
                    float slope = CalculateSlope(position);
                    
                    float heightScore = Mathf.InverseLerp(structureData.minHeight, structureData.maxHeight, height);
                    float slopeScore = 1f - Mathf.Abs(slope - structureData.preferredSlope) / structureData.slopeThreshold;
                    
                    float totalScore = (heightScore + slopeScore) * 0.5f;
                    
                    if (totalScore > 0.3f) 
                    {
                        heightCandidates.Add((position, totalScore));
                    }
                }
            }
        }
        
        heightCandidates.Sort((a, b) => b.score.CompareTo(a.score));
        
        int maxToPlace = Mathf.RoundToInt(heightCandidates.Count * structureData.placementProbability);
        for (int i = 0; i < Mathf.Min(maxToPlace, heightCandidates.Count); i++)
        {
            PlaceStructureAtEnhanced(heightCandidates[i].pos, structureData);
            placedCount++;
        }
        
        return placedCount;
    }
    
    private int PlaceOrganic(EnhancedStructureData structureData)
    {
        int placedCount = 0;
        
        int seedCount = Mathf.Max(1, Mathf.RoundToInt(grid.GetLength(0) * grid.GetLength(1) * 0.01f));
        List<Vector2Int> seedPositions = new List<Vector2Int>();
        
        for (int i = 0; i < seedCount; i++)
        {
            Vector2Int randomPos = new Vector2Int(
                random.Next(grid.GetLength(0)),
                random.Next(grid.GetLength(1))
            );
            
            if (CanPlaceStructureEnhanced(randomPos, structureData))
            {
                PlaceStructureAtEnhanced(randomPos, structureData);
                seedPositions.Add(randomPos);
                placedCount++;
            }
        }
        
        Queue<Vector2Int> growthQueue = new Queue<Vector2Int>(seedPositions);
        
        while (growthQueue.Count > 0 && placedCount < grid.GetLength(0) * grid.GetLength(1) * 0.1f)
        {
            Vector2Int currentPos = growthQueue.Dequeue();
            
            foreach (Vector2Int dir in GetExtendedNeighborDirections())
            {
                Vector2Int newPos = currentPos + dir;
                
                if (IsValidPosition(newPos) && 
                    CanPlaceStructureEnhanced(newPos, structureData) &&
                    !placedStructures.ContainsKey(newPos))
                {
                    float relationshipScore = CalculateRelationshipScore(newPos, structureData);
                    float spreadProbability = structureData.placementProbability * (1f + relationshipScore);
                    
                    if (random.NextDouble() <= spreadProbability)
                    {
                        PlaceStructureAtEnhanced(newPos, structureData);
                        growthQueue.Enqueue(newPos);
                        placedCount++;
                    }
                }
            }
        }
        
        return placedCount;
    }
    
    private float CalculateRelationshipScore(Vector2Int position, EnhancedStructureData structureData)
    {
        float totalScore = 0f;
        int scoreCount = 0;
        
        foreach (var kvp in placedStructures)
        {
            Vector2Int otherPos = kvp.Key;
            StructureInstance otherInstance = kvp.Value;
            
            float distance = Vector2Int.Distance(position, otherPos);
            
            if (distance <= 5f) 
            {

            }
        }
        
        return scoreCount > 0 ? totalScore / scoreCount : 0f;
    }
    
    private bool CanPlaceStructureEnhanced(Vector2Int position, EnhancedStructureData structureData)
    {
        if (!IsValidPosition(position) || placedStructures.ContainsKey(position))
            return false;
        
        Cell cell = grid[position.x, position.y];
        if (!cell.isCollapsed || cell.collapsedTile == null)
            return false;
        
        if (!structureData.compatibleTileTypes.Contains(cell.collapsedTile.tileType))
            return false;
        
        float height = heightMap[position.x, position.y];
        if (height < structureData.minHeight || height > structureData.maxHeight)
            return false;
        
        float slope = CalculateSlope(position);
        if (slope > structureData.slopeThreshold)
            return false;
        
        if (structureData.requiresPathAccess)
        {
            if (GetDistanceToNearestPath(position) > structureData.maxDistanceFromPath)
                return false;
        }
        
        if (!CheckMinimumDistanceEnhanced(position, structureData))
            return false;
        
        if (!CheckStructureRelationships(position, structureData))
            return false;
        
        if (influenceMap[position.x, position.y] > 2f)
            return false;
        
        return true;
    }
    
    private bool CheckMinimumDistanceEnhanced(Vector2Int position, EnhancedStructureData structureData)
    {
        if (!placedByType.ContainsKey(structureData.structureType))
            return true;
        
        foreach (Vector2Int placedPos in placedByType[structureData.structureType])
        {
            if (Vector2Int.Distance(position, placedPos) < structureData.minDistanceFromSameType)
                return false;
        }
        
        return true;
    }
    
    private bool CheckStructureRelationships(Vector2Int position, EnhancedStructureData structureData)
    {
        foreach (var kvp in placedStructures)
        {
            Vector2Int otherPos = kvp.Key;
            StructureInstance otherInstance = kvp.Value;
            float distance = Vector2Int.Distance(position, otherPos);
            
            if (distance < 1f)
                return false;
        }
        
        return true;
    }
    
    private void PlaceStructureAtEnhanced(Vector2Int position, EnhancedStructureData structureData)
    {
        GameObject prefab = structureData.prefab;
        
        if (structureData.variations.Count > 0)
        {
            prefab = structureData.variations[random.Next(structureData.variations.Count)];
        }
        
        var structureInstance = new StructureInstance
        {
            position = position,
            structureData = null,
            prefab = prefab,
            scale = Random.Range(1f / structureData.scaleVariation, structureData.scaleVariation),
            rotation = structureData.allowRotation ? Random.Range(0f, 360f) : 0f,
            height = heightMap[position.x, position.y]
        };
        
        placedStructures[position] = structureInstance;
        
        if (!placedByType.ContainsKey(structureData.structureType))
            placedByType[structureData.structureType] = new List<Vector2Int>();
        placedByType[structureData.structureType].Add(position);
        
        UpdateInfluenceMap(position, 2f);
    }
    
    
    private void UpdateInfluenceMap(Vector2Int center, float radius)
    {
        int radiusInt = Mathf.RoundToInt(radius);
        
        for (int dx = -radiusInt; dx <= radiusInt; dx++)
        {
            for (int dy = -radiusInt; dy <= radiusInt; dy++)
            {
                Vector2Int pos = center + new Vector2Int(dx, dy);
                if (IsValidPosition(pos))
                {
                    float distance = Vector2Int.Distance(center, pos);
                    if (distance <= radius)
                    {
                        float influence = 1f - (distance / radius);
                        influenceMap[pos.x, pos.y] += influence;
                    }
                }
            }
        }
    }
    
    private float CalculateSlope(Vector2Int position)
    {
        float currentHeight = heightMap[position.x, position.y];
        float maxDifference = 0f;
        
        foreach (Vector2Int dir in GetNeighborDirections())
        {
            Vector2Int neighbor = position + dir;
            if (IsValidPosition(neighbor))
            {
                float neighborHeight = heightMap[neighbor.x, neighbor.y];
                float difference = Mathf.Abs(currentHeight - neighborHeight);
                maxDifference = Mathf.Max(maxDifference, difference);
            }
        }
        
        return maxDifference;
    }
    
    private int GetDistanceToNearestPath(Vector2Int position)
    {
        int minDistance = int.MaxValue;
        
        foreach (Vector2Int pathPos in pathTiles)
        {
            int distance = Mathf.RoundToInt(Vector2Int.Distance(position, pathPos));
            minDistance = Mathf.Min(minDistance, distance);
        }
        
        return minDistance == int.MaxValue ? 999 : minDistance;
    }
    
    private List<Vector2Int> GeneratePoissonDiskSamples(float radius, float density)
    {
        List<Vector2Int> samples = new List<Vector2Int>();
        int maxAttempts = 30;
        
        Vector2Int firstSample = new Vector2Int(
            random.Next(grid.GetLength(0)),
            random.Next(grid.GetLength(1))
        );
        samples.Add(firstSample);
        
        List<Vector2Int> activeList = new List<Vector2Int> { firstSample };
        
        while (activeList.Count > 0 && samples.Count < grid.GetLength(0) * grid.GetLength(1) * density)
        {
            int randomIndex = random.Next(activeList.Count);
            Vector2Int currentPoint = activeList[randomIndex];
            bool found = false;
            
            for (int i = 0; i < maxAttempts; i++)
            {
                float angle = (float)(random.NextDouble() * 2 * Mathf.PI);
                float distance = radius + (float)(random.NextDouble() * radius);
                
                Vector2Int candidate = new Vector2Int(
                    Mathf.RoundToInt(currentPoint.x + distance * Mathf.Cos(angle)),
                    Mathf.RoundToInt(currentPoint.y + distance * Mathf.Sin(angle))
                );
                
                if (IsValidPosition(candidate) && IsValidPoissonSample(candidate, samples, radius))
                {
                    samples.Add(candidate);
                    activeList.Add(candidate);
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                activeList.RemoveAt(randomIndex);
            }
        }
        
        return samples;
    }
    
    private bool IsValidPoissonSample(Vector2Int candidate, List<Vector2Int> existingSamples, float minDistance)
    {
        foreach (Vector2Int sample in existingSamples)
        {
            if (Vector2Int.Distance(candidate, sample) < minDistance)
                return false;
        }
        return true;
    }
    
    private Vector2Int[] GetNeighborDirections()
    {
        return new Vector2Int[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
    }
    
    private Vector2Int[] GetExtendedNeighborDirections()
    {
        return new Vector2Int[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };
    }
    
    private bool IsValidPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < grid.GetLength(0) && 
               pos.y >= 0 && pos.y < grid.GetLength(1);
    }
    
    private Dictionary<StructureType, int> GetStructureCountByType()
    {
        var counts = new Dictionary<StructureType, int>();
        
        foreach (var kvp in placedByType)
        {
            counts[kvp.Key] = kvp.Value.Count;
        }
        
        return counts;
    }
}