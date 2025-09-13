using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WFCTrainingEnvironment : MonoBehaviour
{
    [Header("Environment Settings")]
    public int gridWidth = 15;
    public int gridHeight = 15;
    public List<TileData> allTileData = new List<TileData>();
    
    [Header("Training Progress")]
    public bool showDebugInfo = true;
    public int totalEpisodes = 0;
    public float averageReward = 0f;
    
    private TrainWFCAlgorithm currentAlgorithm;
    private Cell[,] currentGrid;
    private BiomeType currentBiome;
    private LayoutType currentLayout;
    private Dictionary<BiomeType, List<TileData>> biomeToTiles;
    
    void Start()
    {
        InitializeBiomeDictionary();
    }
    
    private void InitializeBiomeDictionary()
    {
        biomeToTiles = new Dictionary<BiomeType, List<TileData>>();
        
        foreach (BiomeType biome in System.Enum.GetValues(typeof(BiomeType)))
        {
            biomeToTiles[biome] = allTileData.Where(t => t.biome == biome).ToList();
        }
    }
    
    public void ResetEnvironment(BiomeType biome, LayoutType layout)
    {
        currentBiome = biome;
        currentLayout = layout;
        
        int seed = Random.Range(0, 10000);
        currentAlgorithm = new TrainWFCAlgorithm(
            gridWidth, gridHeight, 
            biomeToTiles[biome], 
            biome, layout, seed);
        
        currentGrid = currentAlgorithm.GetGrid();
        totalEpisodes++;
        
        if (showDebugInfo)
        {
            Debug.Log($"Environment Reset: Episode {totalEpisodes}, Biome: {biome}, Layout: {layout}");
        }
    }
    
    public bool PerformWFCStep(float rlAdjustment)
    {
        if (currentAlgorithm == null) return false;
        
        return currentAlgorithm.PerformSingleStep(rlAdjustment);
    }
    
    public bool IsGenerationComplete()
    {
        if (currentGrid == null) return false;
        
        for (int x = 0; x < currentGrid.GetLength(0); x++)
        {
            for (int y = 0; y < currentGrid.GetLength(1); y++)
            {
                if (!currentGrid[x, y].isCollapsed)
                {
                    return false;
                }
            }
        }
        return true;
    }
    
    public bool CannotContinue()
    {
        return currentAlgorithm?.HasFailedGeneration() ?? true;
    }
    
    public Cell[,] GetCurrentGrid()
    {
        return currentGrid;
    }
    
    public WFCAlgorithm GetWFCAlgorithm()
    {
        return currentAlgorithm;
    }
    
    public int GetTileCountForBiome(BiomeType biome)
    {
        return biomeToTiles.ContainsKey(biome) ? biomeToTiles[biome].Count : 0;
    }
    
    public bool AreCompatible(TileData tile1, TileData tile2, Vector2Int direction)
    {
        List<string> compatibleTiles = null;
        
        if (direction == Vector2Int.up)
            compatibleTiles = tile1.compatibleUp;
        else if (direction == Vector2Int.down)
            compatibleTiles = tile1.compatibleDown;
        else if (direction == Vector2Int.left)
            compatibleTiles = tile1.compatibleLeft;
        else if (direction == Vector2Int.right)
            compatibleTiles = tile1.compatibleRight;
        
        return compatibleTiles != null && 
               (compatibleTiles.Contains(tile2.tileName) || compatibleTiles.Contains("*"));
    }
}