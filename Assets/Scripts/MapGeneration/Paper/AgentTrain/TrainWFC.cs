using System.Collections.Generic;

public class TrainWFCAlgorithm : WFCAlgorithm{
    private bool generationFailed = false;
    private Queue<WFCStep> stepQueue = new Queue<WFCStep>();
    private bool isStepByStepMode = false;
    
    private struct WFCStep
    {
        public Cell targetCell;
        public float rlAdjustment;
        public System.Action<bool> callback;
    }
    
    public void EnableStepByStepMode()
    {
        isStepByStepMode = true;
    }
    
    public bool PerformSingleStep(float rlAdjustment)
    {
        if (IsComplete()) return true;
        if (generationFailed) return false;
        
        SaveState();
        
        CalculateEntropy();
        
        Cell selectedCell = SelectCellToCollapse();
        if (selectedCell == null)
        {
            generationFailed = true;
            return false;
        }
        
        TileData selectedTile = SelectTileWithRLWeighting(selectedCell, rlAdjustment);
        if (selectedTile == null)
        {
            if (!Backtrack())
            {
                generationFailed = true;
                return false;
            }
            return false;
        }
        
        selectedCell.Collapse(selectedTile);
        
        if (!PropagateConstraints(selectedCell))
        {
            if (!Backtrack())
            {
                generationFailed = true;
                return false;
            }
            return false; 
        }
        
        return true; 
    }
    
    public bool HasFailedGeneration()
    {
        return generationFailed;
    }
    
    public void ResetFailureState()
    {
        generationFailed = false;
    }

    public TrainWFCAlgorithm(int width, int height, List<TileData> tiles, BiomeType biome, LayoutType layout, int seed = 0) : base(width, height, tiles, biome, layout, seed)
    {
        
    }
}