using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WFC3DExtension
{
    protected Cell[,] grid;
    protected float[,] heightMap;
    protected List<StructureData> structureDatabase;
    protected System.Random random;
    protected BiomeType currentBiome;
    
    protected Dictionary<Vector2Int, StructureInstance> placedStructures;
    
    public System.Action<string> OnStatusUpdate;
    
    public WFC3DExtension(Cell[,] baseGrid, List<StructureData> structures, BiomeType biome, int seed = 0)
    {
        grid = baseGrid;
        if(structures != null)
            structureDatabase = structures.Where(s => s.compatibleBiomes.Contains(biome)).ToList();
        currentBiome = biome;
        random = seed == 0 ? new System.Random() : new System.Random(seed);
        placedStructures = new Dictionary<Vector2Int, StructureInstance>();
        
        OnStatusUpdate?.Invoke($"3D Extension initialized with {structureDatabase.Count} structure types");
    }
    
    public HeightMapResult GenerateHeightMap()
    {
        OnStatusUpdate?.Invoke("Generating height map from 2D WFC data...");
        
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        heightMap = new float[width, height];
        
        GenerateBaseHeights();
        
        SmoothHeightMap();
        
        ApplyBiomeHeightModifiers();
        
        OnStatusUpdate?.Invoke("Height map generation completed");
        
        return new HeightMapResult
        {
            heightMap = heightMap,
            minHeight = GetMinHeight(),
            maxHeight = GetMaxHeight(),
            averageHeight = GetAverageHeight()
        };
    }
    
    private void GenerateBaseHeights()
    {
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                Cell cell = grid[x, y];
                if (cell.isCollapsed && cell.collapsedTile != null)
                {
                    HeightData heightData = cell.collapsedTile.heightData;
                    float baseHeight = heightData.baseHeight;
                    float variation = UnityEngine.Random.Range(-heightData.heightVariation, heightData.heightVariation);
                    heightMap[x, y] = Mathf.Max(0f, baseHeight + variation);
                }
                else
                {
                    heightMap[x, y] = 0f;
                }
            }
        }
    }
    
    private void SmoothHeightMap()
    {
        float[,] smoothedMap = new float[grid.GetLength(0), grid.GetLength(1)];
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                float sum = heightMap[x, y];
                int count = 1;
                
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        int nx = x + dx;
                        int ny = y + dy;
                        
                        if (nx >= 0 && nx < grid.GetLength(0) && ny >= 0 && ny < grid.GetLength(1))
                        {
                            sum += heightMap[nx, ny];
                            count++;
                        }
                    }
                }
                
                float averageHeight = sum / count;
                smoothedMap[x, y] = Mathf.Lerp(heightMap[x, y], averageHeight, 0.3f);
            }
        }
        
        heightMap = smoothedMap;
    }
    
    private void ApplyBiomeHeightModifiers()
    {
        float modifier = currentBiome switch
        {
            BiomeType.City => 1.0f,      // 도시는 변화 없음
            BiomeType.Desert => 0.7f,    // 사막은 낮게
            BiomeType.Forest => 1.3f,    // 숲은 높게
            _ => 1.0f
        };
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                heightMap[x, y] *= modifier;
            }
        }
    }
    
    public StructurePlacementResult PlaceStructures()
    {
        OnStatusUpdate?.Invoke("Starting structure placement...");
        
        placedStructures.Clear();
        int totalPlaced = 0;
        
        foreach (var structureData in structureDatabase.OrderBy(s => s.placementProbability))
        {
            int placedCount = PlaceStructureType(structureData);
            totalPlaced += placedCount;
            OnStatusUpdate?.Invoke($"Placed {placedCount} {structureData.structureType} structures");
        }
        
        OnStatusUpdate?.Invoke($"Structure placement completed: {totalPlaced} total structures");
        
        return new StructurePlacementResult
        {
            placedStructures = new Dictionary<Vector2Int, StructureInstance>(placedStructures),
            totalPlaced = totalPlaced,
            structuresByType = GetStructureCountByType()
        };
    }
    
    private int PlaceStructureType(StructureData structureData)
    {
        int placedCount = 0;
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                
                if (CanPlaceStructure(position, structureData))
                {
                    if (UnityEngine.Random.Range(0f, 1f) <= structureData.placementProbability)
                    {
                        PlaceStructureAt(position, structureData);
                        placedCount++;
                    }
                }
            }
        }
        
        return placedCount;
    }
    
    private bool CanPlaceStructure(Vector2Int position, StructureData structureData)
    {
        int x = position.x;
        int y = position.y;
        
        if (x < 0 || x >= grid.GetLength(0) || y < 0 || y >= grid.GetLength(1))
            return false;
        
        Cell cell = grid[x, y];
        
        if (!cell.isCollapsed || cell.collapsedTile == null)
            return false;
        
        if (placedStructures.ContainsKey(position))
            return false;
        
        if (!structureData.compatibleTileTypes.Contains(cell.collapsedTile.tileType))
            return false;
        
        float height = heightMap[x, y];
        if (height < structureData.minHeight || height > structureData.maxHeight)
            return false;
        
        if (!cell.collapsedTile.heightData.canHaveStructures)
            return false;
        
        if (cell.collapsedTile.heightData.allowedStructures.Count > 0 &&
            !cell.collapsedTile.heightData.allowedStructures.Contains(structureData.structureType))
            return false;
        
        if (!CheckMinimumDistance(position, structureData))
            return false;
        
        return true;
    }
    
    private bool CheckMinimumDistance(Vector2Int position, StructureData structureData)
    {
        int minDistance = structureData.minDistanceFromSameType;
        
        foreach (var kvp in placedStructures)
        {
            if (kvp.Value.structureData.structureType == structureData.structureType)
            {
                float distance = Vector2Int.Distance(position, kvp.Key);
                if (distance < minDistance)
                    return false;
            }
        }
        
        return true;
    }
    
    private void PlaceStructureAt(Vector2Int position, StructureData structureData)
    {
        GameObject prefab = structureData.prefab;
        
        if (structureData.variations.Count > 0)
        {
            prefab = structureData.variations[random.Next(structureData.variations.Count)];
        }
        
        
        var structureInstance = new StructureInstance
        {
            position = position,
            structureData = structureData,
            prefab = prefab,
            scale = UnityEngine.Random.Range(1f / structureData.scaleVariation, structureData.scaleVariation),
            rotation = structureData.allowRotation ? UnityEngine.Random.Range(0f, 360f) : 0f,
            height = heightMap[position.x, position.y]
        };
        
        placedStructures[position] = structureInstance;
    }
    
    private Dictionary<StructureType, int> GetStructureCountByType()
    {
        var counts = new Dictionary<StructureType, int>();
        
        foreach (var structure in placedStructures.Values)
        {
            if (!counts.ContainsKey(structure.structureData.structureType))
                counts[structure.structureData.structureType] = 0;
            
            counts[structure.structureData.structureType]++;
        }
        
        return counts;
    }
    
    private float GetMinHeight()
    {
        float min = float.MaxValue;
        for (int x = 0; x < heightMap.GetLength(0); x++)
        {
            for (int y = 0; y < heightMap.GetLength(1); y++)
            {
                min = Mathf.Min(min, heightMap[x, y]);
            }
        }
        return min;
    }
    
    private float GetMaxHeight()
    {
        float max = float.MinValue;
        for (int x = 0; x < heightMap.GetLength(0); x++)
        {
            for (int y = 0; y < heightMap.GetLength(1); y++)
            {
                max = Mathf.Max(max, heightMap[x, y]);
            }
        }
        return max;
    }
    
    private float GetAverageHeight()
    {
        float sum = 0f;
        int count = 0;
        for (int x = 0; x < heightMap.GetLength(0); x++)
        {
            for (int y = 0; y < heightMap.GetLength(1); y++)
            {
                sum += heightMap[x, y];
                count++;
            }
        }
        return count > 0 ? sum / count : 0f;
    }
    
    public float[,] GetHeightMap() => heightMap;
    public Dictionary<Vector2Int, StructureInstance> GetPlacedStructures() => placedStructures;
}
