
using System.Linq;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WFCAlgorithm
{
    protected Cell[,] grid;
    protected List<TileData> allTiles;
    protected BiomeType currentBiome;
    protected LayoutType layoutType;
    protected System.Random random;
    protected Stack<Cell[,]> stateStack = new Stack<Cell[,]>();
    protected int backtrackDepth = 0;
    protected const int MAX_BACKTRACK_DEPTH = 200;
    protected const int MAX_GENERATION_ATTEMPTS = 3;
    
    protected RLAgent rlAgent;
    protected int generationAttempt = 0;
    
    public System.Action<float> OnProgressUpdate;
    public System.Action<string> OnStatusUpdate;
    
    public WFCAlgorithm(int width, int height, List<TileData> tiles, BiomeType biome, LayoutType layout, int seed = 0)
    {
        grid = new Cell[width, height];
        allTiles = tiles.Where(t => t.biome == biome).ToList();
        currentBiome = biome;
        layoutType = layout;
        random = seed == 0 ? new System.Random() : new System.Random(seed);
        rlAgent = new RLAgent();
        
        if (allTiles.Count == 0)
        {
            OnStatusUpdate?.Invoke($"ERROR: No tiles found for biome {biome}!");
            return;
        }
        
        InitializeGrid();
        OnStatusUpdate?.Invoke($"Initialized {width}x{height} grid with {allTiles.Count} tiles for {biome} biome");
        
        LogTileCompatibility();
    }
    
    protected void LogTileCompatibility()
    {
        OnStatusUpdate?.Invoke("=== TILE COMPATIBILITY INFO ===");
        
        foreach (var tile in allTiles.Take(5))
        {
            string info = $"{tile.tileName}: ";
            if (tile.allowAllByDefault)
            {
                info += "AllowAll=true";
                if (tile.compatibleUp.Count > 0)
                    info += $", Up=[{string.Join(",", tile.compatibleUp.Take(3))}]";
            }
            else
            {
                info += $"Up=[{string.Join(",", tile.compatibleUp.Take(3))}]";
            }
            OnStatusUpdate?.Invoke(info);
        }
        
        if (allTiles.Count > 5)
        {
            OnStatusUpdate?.Invoke($"... and {allTiles.Count - 5} more tiles");
        }
    }
    
    protected void InitializeGrid()
    {
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                grid[x, y] = new Cell
                {
                    position = new Vector2Int(x, y),
                    possibleTiles = new List<TileData>(allTiles)
                };
            }
        }
    }
    
    public bool Generate()
    {
        int totalCells = grid.GetLength(0) * grid.GetLength(1);
        
        for (generationAttempt = 1; generationAttempt <= MAX_GENERATION_ATTEMPTS; generationAttempt++)
        {
            OnStatusUpdate?.Invoke($"Generation attempt {generationAttempt}/{MAX_GENERATION_ATTEMPTS}");
            
            if (generationAttempt > 1)
            {
                ResetGrid();
            }
            
            backtrackDepth = 0;
            stateStack.Clear();
            
            if (AttemptGeneration())
            {
                OnStatusUpdate?.Invoke($"Wave function collapse completed successfully on attempt {generationAttempt}!");
                return true;
            }
            
            OnStatusUpdate?.Invoke($"Attempt {generationAttempt} failed, trying different approach...");
        }
        
        OnStatusUpdate?.Invoke("All generation attempts failed!");
        return false;
    }
    
    protected bool AttemptGeneration()
    {
        int totalCells = grid.GetLength(0) * grid.GetLength(1);
        int stuckCounter = 0;
        int maxStuckCount = 10;
        
        while (!IsComplete())
        {
            if (!GenerateStep()) return false;
            
            
            int currentCollapsed = GetCollapsedCellCount();
            if (currentCollapsed > 0 && backtrackDepth > currentCollapsed * 2)
            {
                stuckCounter++;
                if (stuckCounter > maxStuckCount)
                {
                    OnStatusUpdate?.Invoke("Too many backtracks, restarting with different strategy...");
                    return false;
                }
            }
            else
            {
                stuckCounter = 0;
            }
            
            float progress = (float)currentCollapsed / totalCells;
            OnProgressUpdate?.Invoke(progress);
        }
        
        return true;
    }
    
    protected void ResetGrid()
    {
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                grid[x, y] = new Cell
                {
                    position = new Vector2Int(x, y),
                    possibleTiles = new List<TileData>(allTiles)
                };
            }
        }
        
        random = new System.Random(random.Next());
    }
    
    public bool GenerateStep()
    {
        float rlAdjustment = rlAgent.GetTileWeightAdjustment(GetGridState(), currentBiome, layoutType);
        
        CalculateEntropy();
        
        Cell selectedCell = SelectCellToCollapse();
        if (selectedCell == null)
        {
            OnStatusUpdate?.Invoke("ERROR: No cell available for collapse!");
            LogCurrentState();
            return false;
        }
        
        SaveState();
        
        TileData selectedTile = SelectTileWithRLWeighting(selectedCell, rlAdjustment);
        if (selectedTile == null)
        {
            OnStatusUpdate?.Invoke($"No valid tile for cell at {selectedCell.position} (entropy: {selectedCell.Entropy})");
            if (!Backtrack())
            {
                OnStatusUpdate?.Invoke("ERROR: Generation failed - cannot backtrack further");
                LogFailureAnalysis();
                return false;
            }
            return true; 
        }
        
        selectedCell.Collapse(selectedTile);
        
        if (!PropagateConstraints(selectedCell))
        {
            OnStatusUpdate?.Invoke($"Constraint violation at {selectedCell.position} with tile '{selectedTile.tileName}'");
            if (!Backtrack())
            {
                OnStatusUpdate?.Invoke("ERROR: Generation failed - constraint propagation failed");
                LogFailureAnalysis();
                return false;
            }
            return true; 
        }
        
        return true;
    }
    
    protected void LogCurrentState()
    {
        int collapsedCount = GetCollapsedCellCount();
        int totalCells = grid.GetLength(0) * grid.GetLength(1);
        
        OnStatusUpdate?.Invoke($"Current state: {collapsedCount}/{totalCells} cells collapsed");
        OnStatusUpdate?.Invoke($"Backtrack depth: {backtrackDepth}/{MAX_BACKTRACK_DEPTH}");
        OnStatusUpdate?.Invoke($"Stack size: {stateStack.Count}");
        
        var zeroCells = new List<Vector2Int>();
        var lowEntropyCells = new List<(Vector2Int pos, int entropy)>();
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (!grid[x, y].isCollapsed)
                {
                    if (grid[x, y].Entropy == 0)
                    {
                        zeroCells.Add(new Vector2Int(x, y));
                    }
                    else if (grid[x, y].Entropy <= 2)
                    {
                        lowEntropyCells.Add((new Vector2Int(x, y), grid[x, y].Entropy));
                    }
                }
            }
        }
        
        if (zeroCells.Count > 0)
        {
            OnStatusUpdate?.Invoke($"Cells with zero entropy (impossible): {string.Join(", ", zeroCells)}");
        }
        
        if (lowEntropyCells.Count > 0)
        {
            OnStatusUpdate?.Invoke($"Cells with low entropy: {string.Join(", ", lowEntropyCells.Take(5).Select(c => $"{c.pos}({c.entropy})"))}");
        }
    }
    
    protected void LogFailureAnalysis()
    {
        OnStatusUpdate?.Invoke("=== FAILURE ANALYSIS ===");
        
        var tileUsage = new Dictionary<string, int>();
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (grid[x, y].isCollapsed && grid[x, y].collapsedTile != null)
                {
                    string tileName = grid[x, y].collapsedTile.tileName;
                    tileUsage[tileName] = tileUsage.GetValueOrDefault(tileName, 0) + 1;
                }
            }
        }
        
        OnStatusUpdate?.Invoke($"Tile usage: {string.Join(", ", tileUsage.Select(kv => $"{kv.Key}:{kv.Value}"))}");
        
        var restrictiveTiles = allTiles
            .Where(t => GetTotalCompatibilityCount(t) < allTiles.Count * 2) 
            .OrderBy(t => GetTotalCompatibilityCount(t))
            .Take(3);
        
        if (restrictiveTiles.Any())
        {
            OnStatusUpdate?.Invoke($"Most restrictive tiles: {string.Join(", ", restrictiveTiles.Select(t => t.tileName))}");
        }
        
        OnStatusUpdate?.Invoke("=== SUGGESTIONS ===");
        OnStatusUpdate?.Invoke("- Try increasing tile variety for this biome");
        OnStatusUpdate?.Invoke("- Check adjacency rules for conflicts");
        OnStatusUpdate?.Invoke("- Consider using '*' wildcard in compatibility lists");
        OnStatusUpdate?.Invoke("- Try smaller grid size first");
    }
    
    protected int GetTotalCompatibilityCount(TileData tile)
    {
        return tile.compatibleUp.Count + tile.compatibleDown.Count + 
               tile.compatibleLeft.Count + tile.compatibleRight.Count;
    }
    
    public bool IsComplete()
    {
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (!grid[x, y].isCollapsed)
                {
                    return false;
                }
            }
        }
        return true;
    }
    
    protected void CalculateEntropy()
    {
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (!grid[x, y].isCollapsed)
                {
                    UpdatePossibleTiles(grid[x, y]);
                }
            }
        }
    }
    
    protected Cell SelectCellToCollapse()
    {
        List<Cell> candidateCells = new List<Cell>();
        int minEntropy = int.MaxValue;
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                Cell cell = grid[x, y];
                if (!cell.isCollapsed && cell.Entropy > 0)
                {
                    if (cell.Entropy < minEntropy)
                    {
                        minEntropy = cell.Entropy;
                        candidateCells.Clear();
                        candidateCells.Add(cell);
                    }
                    else if (cell.Entropy == minEntropy)
                    {
                        candidateCells.Add(cell);
                    }
                }
            }
        }
        
        return candidateCells.Count > 0 ? candidateCells[random.Next(candidateCells.Count)] : null;
    }
    
    protected TileData SelectTileWithRLWeighting(Cell cell, float rlAdjustment)
    {
        if (cell.possibleTiles.Count == 0) return null;
        
        List<(TileData tile, float weight, float safetyScore)> tileScores = new List<(TileData, float, float)>();
        
        foreach (TileData tile in cell.possibleTiles)
        {
            float weight = CalculateTileWeight(cell, tile) + rlAdjustment;
            
            float safetyScore = CalculateSafetyScore(cell, tile);
            
            float adjustedWeight = weight * (0.5f + safetyScore * 0.5f);
            
            tileScores.Add((tile, Mathf.Max(0.1f, adjustedWeight), safetyScore));
        }
        
        if (backtrackDepth > 20)
        {
            tileScores.Sort((a, b) => b.safetyScore.CompareTo(a.safetyScore));
            var safeTiles = tileScores.Take(Mathf.Max(1, tileScores.Count / 2)).ToList();
            return WeightedRandomSelect(safeTiles.Select(t => (t.tile, t.weight)).ToList());
        }
        
        return WeightedRandomSelect(tileScores.Select(t => (t.tile, t.weight)).ToList());
    }
    
    protected float CalculateSafetyScore(Cell cell, TileData tile)
    {
        float safetyScore = 1.0f;
        
        foreach (Vector2Int dir in GetNeighborDirections())
        {
            Vector2Int neighborPos = cell.position + dir;
            if (IsValidPosition(neighborPos))
            {
                Cell neighbor = grid[neighborPos.x, neighborPos.y];
                if (!neighbor.isCollapsed)
                {
                    int validNeighborTiles = 0;
                    foreach (TileData neighborTile in neighbor.possibleTiles)
                    {
                        if (IsCompatible(neighborTile, tile, -dir))
                        {
                            validNeighborTiles++;
                        }
                    }
                    
                    float neighborSafety = (float)validNeighborTiles / neighbor.possibleTiles.Count;
                    if (validNeighborTiles == 0)
                    {
                        return 0.0f;
                    }
                    
                    safetyScore *= neighborSafety;
                }
            }
        }
        
        return safetyScore;
    }
    
    protected TileData WeightedRandomSelect(List<(TileData tile, float weight)> tilesWithWeights)
    {
        float totalWeight = tilesWithWeights.Sum(t => t.weight);
        if (totalWeight <= 0) return tilesWithWeights.First().tile;
        
        float randomValue = (float)random.NextDouble() * totalWeight;
        float cumulativeWeight = 0f;
        
        foreach (var (tile, weight) in tilesWithWeights)
        {
            cumulativeWeight += weight;
            if (randomValue <= cumulativeWeight)
            {
                return tile;
            }
        }
        
        return tilesWithWeights.Last().tile;
    }
    
    protected float CalculateTileWeight(Cell cell, TileData tile)
    {
        float weight = tile.baseWeight;
        
        foreach (Vector2Int dir in GetNeighborDirections())
        {
            Vector2Int neighborPos = cell.position + dir;
            if (IsValidPosition(neighborPos))
            {
                Cell neighbor = grid[neighborPos.x, neighborPos.y];
                if (neighbor.isCollapsed)
                {
                    
                    if (IsCompatible(tile, neighbor.collapsedTile, dir))
                    {
                        weight += 1.5f; 
                    }
                    else
                    {
                        weight += 0.5f; 
                    }
                }
            }
        }
        
        weight *= tile.biomeWeight;
        
        if (layoutType == LayoutType.Continuous && tile.tileType == TileType.Path)
        {
            weight *= 1.3f;
        }
        else if (layoutType == LayoutType.Sparse && tile.tileType == TileType.Impassable)
        {
            weight *= 1.2f;
        }
        
        weight += CalculatePathConnectivityBonus(cell, tile);
        
        return weight;
    }
    
    protected float CalculatePathConnectivityBonus(Cell cell, TileData tile)
    {
        if (tile.tileType != TileType.Path) return 0f;
        
        float bonus = 0f;
        int pathNeighbors = 0;
        
        foreach (Vector2Int dir in GetNeighborDirections())
        {
            Vector2Int neighborPos = cell.position + dir;
            if (IsValidPosition(neighborPos))
            {
                Cell neighbor = grid[neighborPos.x, neighborPos.y];
                if (neighbor.isCollapsed && neighbor.collapsedTile.tileType == TileType.Path)
                {
                    pathNeighbors++;
                }
            }
        }
        
        if (layoutType == LayoutType.Continuous)
        {
            bonus = pathNeighbors * 0.3f; 
        }
        else
        {
            bonus = pathNeighbors > 2 ? -0.2f : pathNeighbors * 0.1f; 
        }
        
        return bonus;
    }
    
    protected bool PropagateConstraints(Cell collapsedCell)
    {
        Queue<Cell> propagationQueue = new Queue<Cell>();
        propagationQueue.Enqueue(collapsedCell);
        
        int propagationSteps = 0;
        const int MAX_PROPAGATION_STEPS = 1000; 
        
        while (propagationQueue.Count > 0 && propagationSteps < MAX_PROPAGATION_STEPS)
        {
            Cell currentCell = propagationQueue.Dequeue();
            propagationSteps++;
            
            foreach (Vector2Int dir in GetNeighborDirections())
            {
                Vector2Int neighborPos = currentCell.position + dir;
                if (IsValidPosition(neighborPos))
                {
                    Cell neighbor = grid[neighborPos.x, neighborPos.y];
                    if (!neighbor.isCollapsed)
                    {
                        int originalCount = neighbor.possibleTiles.Count;
                        List<TileData> toRemove = new List<TileData>();
                        
                        foreach (TileData tile in neighbor.possibleTiles)
                        {
                            if (currentCell.collapsedTile == null)
                            {
                                continue;
                            }
                            if (!IsCompatible(tile, currentCell.collapsedTile, -dir))
                            {
                                toRemove.Add(tile);
                            }
                        }
                        
                        bool changed = false;
                        foreach (TileData tile in toRemove)
                        {
                            neighbor.RemovePossibility(tile);
                            changed = true;
                        }
                        
                        if (neighbor.Entropy == 0)
                        {
                            OnStatusUpdate?.Invoke($"CONSTRAINT ERROR: Cell {neighbor.position} has no valid options after {currentCell.position} placed '{currentCell.collapsedTile.tileName}'");
                            OnStatusUpdate?.Invoke($"  Removed {toRemove.Count} tiles, had {originalCount} originally");
                            OnStatusUpdate?.Invoke($"  Removed tiles: {string.Join(", ", toRemove.Take(3).Select(t => t.tileName))}");
                            return false; 
                        }
                        
                        if (changed)
                        {
                            propagationQueue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }
        
        if (propagationSteps >= MAX_PROPAGATION_STEPS)
        {
            OnStatusUpdate?.Invoke("WARNING: Propagation loop detected, stopping");
        }
        
        return true;
    }
    
    protected bool IsCompatible(TileData tile1, TileData tile2, Vector2Int direction)
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
        
        if (compatibleTiles == null) return false;
        
        if (compatibleTiles.Count == 0)
        {
            return tile1.allowAllByDefault;
        }
        
        if (compatibleTiles.Contains("*"))
        {
            return true;
        }
        
        return compatibleTiles.Contains(tile2.tileName);
    }
    
    protected void UpdatePossibleTiles(Cell cell)
    {
        if (cell.isCollapsed) return;
        
        List<TileData> validTiles = new List<TileData>();
        
        foreach (TileData tile in cell.possibleTiles)
        {
            bool isValid = true;
            
            foreach (Vector2Int dir in GetNeighborDirections())
            {
                Vector2Int neighborPos = cell.position + dir;
                if (IsValidPosition(neighborPos))
                {
                    Cell neighbor = grid[neighborPos.x, neighborPos.y];
                    if (neighbor.isCollapsed)
                    {
                        if (!IsCompatible(tile, neighbor.collapsedTile, dir))
                        {
                            isValid = false;
                            break;
                        }
                    }
                }
            }
            
            if (isValid)
            {
                validTiles.Add(tile);
            }
        }
        
        if (validTiles.Count == 0 && cell.possibleTiles.Count > 0)
        {
            OnStatusUpdate?.Invoke($"WARNING: Cell {cell.position} lost all possibilities!");
            
            foreach (Vector2Int dir in GetNeighborDirections())
            {
                Vector2Int neighborPos = cell.position + dir;
                if (IsValidPosition(neighborPos))
                {
                    Cell neighbor = grid[neighborPos.x, neighborPos.y];
                    if (neighbor.isCollapsed)
                    {
                        OnStatusUpdate?.Invoke($"  Neighbor {dir}: {neighbor.collapsedTile.tileName}");
                    }
                }
            }
        }
        
        cell.possibleTiles = validTiles;
    }
    
    protected Vector2Int[] GetNeighborDirections()
    {
        return new Vector2Int[]
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };
    }
    
    protected bool IsValidPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < grid.GetLength(0) && 
               pos.y >= 0 && pos.y < grid.GetLength(1);
    }
    
    protected int GetCollapsedCellCount()
    {
        int count = 0;
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (grid[x, y].isCollapsed) count++;
            }
        }
        return count;
    }
    
    protected void SaveState()
    {
        Cell[,] stateCopy = new Cell[grid.GetLength(0), grid.GetLength(1)];
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                stateCopy[x, y] = grid[x, y].DeepCopy();
            }
        }
        stateStack.Push(stateCopy);
    }
    
    protected bool Backtrack()
    {
        backtrackDepth++;
        
        if (backtrackDepth > MAX_BACKTRACK_DEPTH)
        {
            OnStatusUpdate?.Invoke($"ERROR: Max backtrack depth ({MAX_BACKTRACK_DEPTH}) exceeded!");
            LogFailureReason("TOO_MANY_BACKTRACKS");
            return false;
        }
        
        if (stateStack.Count == 0)
        {
            OnStatusUpdate?.Invoke("ERROR: No previous states to backtrack to!");
            LogFailureReason("NO_PREVIOUS_STATES");
            return false;
        }
        
        grid = stateStack.Pop();
        OnStatusUpdate?.Invoke($"Backtracked to previous state (depth: {backtrackDepth})");
        
        return true;
    }
    
    protected void LogFailureReason(string reason)
    {
        OnStatusUpdate?.Invoke($"Generation failed due to: {reason}");
        
        switch (reason)
        {
            case "TOO_MANY_BACKTRACKS":
                OnStatusUpdate?.Invoke("Possible causes: Too restrictive adjacency rules, insufficient tile variety");
                break;
            case "NO_PREVIOUS_STATES":
                OnStatusUpdate?.Invoke("Possible causes: Invalid initial constraints, contradictory tile rules");
                break;
        }
    }
    
    protected bool ValidateRules()
    {
        foreach (var tile in allTiles)
        {
            if (!HasValidNeighbors(tile))
            {
                OnStatusUpdate?.Invoke($"ERROR: Tile '{tile.tileName}' has invalid adjacency rules!");
                return false;
            }
        }
        return true;
    }
    
    protected bool HasValidNeighbors(TileData tile)
    {
        var allTileNames = allTiles.Select(t => t.tileName).ToHashSet();
        
        return CheckDirection(tile.compatibleUp, allTileNames, "up") &&
               CheckDirection(tile.compatibleDown, allTileNames, "down") &&
               CheckDirection(tile.compatibleLeft, allTileNames, "left") &&
               CheckDirection(tile.compatibleRight, allTileNames, "right");
    }
    
    protected bool CheckDirection(List<string> compatible, HashSet<string> allNames, string direction)
    {
        if (compatible.Count == 0)
        {
            OnStatusUpdate?.Invoke($"Warning: No compatible tiles defined for {direction} direction");
            return true;
        }
        
        foreach (string tileName in compatible)
        {
            if (tileName != "*" && !allNames.Contains(tileName))
            {
                OnStatusUpdate?.Invoke($"ERROR: Compatible tile '{tileName}' does not exist!");
                return false;
            }
        }
        return true;
    }
    
    protected float[] GetGridState()
    {
        List<float> state = new List<float>();
        
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                state.Add(grid[x, y].isCollapsed ? 1f : 0f);
                state.Add(grid[x, y].Entropy / (float)allTiles.Count);
            }
        }
        
        state.Add((int)currentBiome / 3f);
        state.Add(layoutType == LayoutType.Continuous ? 1f : 0f);
        
        return state.ToArray();
    }
    
    public Cell[,] GetGrid() => grid;
    public int GetBacktrackCount() => backtrackDepth;
}
