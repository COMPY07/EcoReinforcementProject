using UnityEngine;

public class RLAgent
{
    private System.Random random = new System.Random();
    
    public float GetTileWeightAdjustment(float[] gridState, BiomeType biome, LayoutType layout)
    {
        float adjustment = 0f;
        
        float completionRate = CalculateCompletionRate(gridState);
        
        switch (biome)
        {
            case BiomeType.City:
                if (layout == LayoutType.Continuous)
                    adjustment += 0.3f * (1f - completionRate);
                break;
            case BiomeType.Forest:
                if (layout == LayoutType.Sparse)
                    adjustment += 0.2f;
                break;
            case BiomeType.Desert:
                adjustment += 0.1f * completionRate;
                break;
        }
        
        adjustment += ((float)random.NextDouble() - 0.5f) * 0.15f;
        
        return adjustment;
    }
    
    private float CalculateCompletionRate(float[] gridState)
    {
        int collapsedCells = 0;
        for (int i = 0; i < gridState.Length; i += 2)
        {
            if (gridState[i] > 0.5f) collapsedCells++;
        }
        return (float)collapsedCells / (gridState.Length / 2);
    }
    
    public float EvaluateMap(Cell[,] grid, BiomeType biome, LayoutType layout)
    {
        float completeness = CalculateCompleteness(grid);
        float biomeCoherence = CalculateBiomeCoherence(grid, biome, layout);
        float pathConnectivity = CalculatePathConnectivity(grid);
        
        return completeness + biomeCoherence + pathConnectivity;
    }
    
    private float CalculateCompleteness(Cell[,] grid)
    {
        int totalCells = grid.GetLength(0) * grid.GetLength(1);
        int collapsedCells = 0;
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (grid[x, y].isCollapsed && grid[x, y].collapsedTile != null)
                {
                    collapsedCells++;
                }
            }
        }
        
        return (float)collapsedCells / totalCells;
    }
    
    private float CalculateBiomeCoherence(Cell[,] grid, BiomeType biome, LayoutType layout)
    {
        float coherenceScore = 0f;
        int totalAdjacencies = 0;
        int validAdjacencies = 0;
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (grid[x, y].isCollapsed)
                {
                    Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                    foreach (var dir in directions)
                    {
                        Vector2Int neighborPos = new Vector2Int(x, y) + dir;
                        if (neighborPos.x >= 0 && neighborPos.x < grid.GetLength(0) && 
                            neighborPos.y >= 0 && neighborPos.y < grid.GetLength(1))
                        {
                            if (grid[neighborPos.x, neighborPos.y].isCollapsed)
                            {
                                totalAdjacencies++;
                                validAdjacencies++; 
                            }
                        }
                    }
                }
            }
        }
        
        return totalAdjacencies > 0 ? (float)validAdjacencies / totalAdjacencies : 1f;
    }
    
    private float CalculatePathConnectivity(Cell[,] grid) {
        int pathTiles = 0;
        int connectedPaths = 0;
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (grid[x, y].isCollapsed && grid[x, y].collapsedTile?.tileType == TileType.Path)
                {
                    pathTiles++;
                    
                    int pathNeighbors = 0;
                    Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                    foreach (var dir in directions)
                    {
                        Vector2Int neighborPos = new Vector2Int(x, y) + dir;
                        if (neighborPos.x >= 0 && neighborPos.x < grid.GetLength(0) && 
                            neighborPos.y >= 0 && neighborPos.y < grid.GetLength(1))
                        {
                            if (grid[neighborPos.x, neighborPos.y].isCollapsed && 
                                grid[neighborPos.x, neighborPos.y].collapsedTile?.tileType == TileType.Path)
                            {
                                pathNeighbors++;
                            }
                        }
                    }
                    
                    if (pathNeighbors > 0) connectedPaths++;
                }
            }
        }
        
        return pathTiles > 0 ? (float)connectedPaths / pathTiles : 1f;
    }
}