using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Structure Info", menuName = "WFC/StructureInfo")]
public class StructureInfo : ScriptableObject
{
    [Header("Basic Settings")]
    public string structureName;
    public GameObject prefab;
    public List<GameObject> variations = new List<GameObject>();
    
    [Header("Placement Rules")]
    public List<TileType> canPlaceOnTiles = new List<TileType>();
    public List<NeighborRule> neighborRules = new List<NeighborRule>();
    
    [Header("Spawn Settings")]
    [Range(0f, 1f)]
    public float spawnChance = 0.3f;
    [Range(1, 5)]
    public int maxPerTile = 1;
    
    [Header("Visual Settings")]
    [Range(0.5f, 2f)]
    public float scaleMin = 0.8f;
    [Range(0.5f, 2f)]
    public float scaleMax = 1.2f;
    public bool randomRotation = true;
    public Vector3 positionOffset = Vector3.zero;
    
    [Header("Spacing")]
    [Range(0.5f, 5f)]
    public float minSpacing = 1f;
    
    public bool CanPlaceOnTile(TileType tileType)
    {
        return canPlaceOnTiles.Contains(tileType);
    }
    
    public float CalculateSpawnMultiplier(Vector2Int position, Cell[,] grid)
    {
        float multiplier = 1f;
        
        foreach (var rule in neighborRules)
        {
            multiplier *= rule.GetMultiplier(position, grid);
        }
        
        return Mathf.Clamp(multiplier, 0f, 3f);
    }
}

[System.Serializable]
public class NeighborRule
{
    public string ruleName = "New Rule";
    public List<TileType> requiredTileTypes = new List<TileType>();
    public int searchRadius = 1;
    public RuleType ruleType = RuleType.Boost;
    [Range(0.5f, 2f)]
    public float effectStrength = 1.5f;
    
    public float GetMultiplier(Vector2Int centerPos, Cell[,] grid)
    {
        int foundCount = 0;
        int totalChecked = 0;
        
        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                
                Vector2Int checkPos = centerPos + new Vector2Int(dx, dy);
                if (IsValidPosition(checkPos, grid))
                {
                    Cell cell = grid[checkPos.x, checkPos.y];
                    if (cell.isCollapsed && cell.collapsedTile != null)
                    {
                        if (requiredTileTypes.Contains(cell.collapsedTile.tileType))
                        {
                            foundCount++;
                        }
                        totalChecked++;
                    }
                }
            }
        }
        
        if (totalChecked == 0) return 1f;
        
        float ratio = (float)foundCount / totalChecked;
        
        return ruleType switch
        {
            RuleType.Boost => 1f + (ratio * (effectStrength - 1f)),
            RuleType.Reduce => 1f - (ratio * (1f - effectStrength)),
            RuleType.Require => foundCount > 0 ? effectStrength : 0f,
            _ => 1f
        };
    }
    
    private bool IsValidPosition(Vector2Int pos, Cell[,] grid)
    {
        return pos.x >= 0 && pos.x < grid.GetLength(0) && 
               pos.y >= 0 && pos.y < grid.GetLength(1);
    }
}

