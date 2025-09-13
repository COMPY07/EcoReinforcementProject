using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class ProceduralMapGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public int gridWidth = 10;
    public int gridHeight = 10;
    public BiomeType biomeType = BiomeType.Forest;
    public LayoutType layoutType = LayoutType.Continuous;
    public float tileSize = 1f;
    
    [Header("Tile Data")]
    public List<TileData> allTileData = new List<TileData>();
    
    [Header("Generation Control")]
    [Range(0, 10000)]
    public int seed = 0;
    public bool useRandomSeed = true;
    public bool generateOnStart = true;
    public bool showGizmos = true;
    
    [Header("Performance")]
    public bool asyncGeneration = true;
    public int tilesPerFrame = 5;
    
    [Header("UI References")]
    public Slider progressBar;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI statsText;
    public Button generateButton;
    public Dropdown biomeDropdown;
    public Toggle layoutToggle;
    
    public WFCAlgorithm wfcAlgorithm;
    public Cell[,] currentGrid;
    public Transform generatedMapParent;
    public bool isGenerating = false;
    public bool generationSuccess = false;
    public Stopwatch generationTimer;
    
    protected void Start()
    {
        SetupUI();
        
        if (generateOnStart)
        {
            GenerateMap();
        }
    }
    
    private void SetupUI()
    {
        if (generateButton != null)
        {
            generateButton.onClick.AddListener(GenerateMap);
        }
        
        if (biomeDropdown != null)
        {
            biomeDropdown.onValueChanged.AddListener(OnBiomeChanged);
            biomeDropdown.value = (int)biomeType;
        }
        
        if (layoutToggle != null)
        {
            layoutToggle.onValueChanged.AddListener(OnLayoutChanged);
            layoutToggle.isOn = layoutType == LayoutType.Continuous;
        }
        
        UpdateStatsDisplay();
    }
    
    public virtual void GenerateMap()
    {
        if (isGenerating) return;
        
        StartCoroutine(GenerateMapCoroutine());
    }
    
    private IEnumerator GenerateMapCoroutine()
    {
        isGenerating = true;
        generationTimer = System.Diagnostics.Stopwatch.StartNew();
        
        if (generateButton != null)
            generateButton.interactable = false;
        
        
        ClearMap();
        
        
        
        
        
        
        generatedMapParent = new GameObject("Generated Map").transform;
        generatedMapParent.SetParent(transform);
        int actualSeed = useRandomSeed ? Random.Range(0, 10000) : seed;
        
        
        
        
        UpdateStatusText($"Initializing generation (Seed: {actualSeed})...");
        yield return null;
        
        
        wfcAlgorithm = new WFCAlgorithm(gridWidth, gridHeight, allTileData, biomeType, layoutType, actualSeed);
        wfcAlgorithm.OnProgressUpdate += OnGenerationProgress;
        wfcAlgorithm.OnStatusUpdate += UpdateStatusText;
        
        bool success = false;
        if (asyncGeneration) {
            yield return StartCoroutine(GenerateAsync());
            success = generationSuccess; 
        }
        else
        {
            success = wfcAlgorithm.Generate();
        }
        
        if (success)
        {
            currentGrid = wfcAlgorithm.GetGrid();
            yield return StartCoroutine(SpawnTiles());
            
            generationTimer.Stop();
            UpdateStatusText($"Generation completed! ({generationTimer.ElapsedMilliseconds}ms)");
            UpdateStatsDisplay();
        }
        else
        {
            generationTimer.Stop();
            UpdateStatusText("Generation failed!");
        }
        
        if (generateButton != null)
            generateButton.interactable = true;
        
        isGenerating = false;
    }
    
    public IEnumerator GenerateAsync()
    {
        generationSuccess = false;
        
        yield return StartCoroutine(RunGenerationSteps());
    }
    
    private IEnumerator RunGenerationSteps()
    {
        
        int maxStepsPerFrame = 100;
        int stepCount = 0;
        
        UpdateStatusText("Starting step-by-step generation...");
        
        while (!wfcAlgorithm.IsComplete())
        {
            bool stepResult = wfcAlgorithm.GenerateStep();
            
            if (!stepResult)
            {
                generationSuccess = false;
                yield break;
            }
            
            stepCount++;
            
            
            if (stepCount >= maxStepsPerFrame)
            {
                stepCount = 0;
                yield return null;
            }
        }
        
        generationSuccess = true;
    }
    
    private IEnumerator SpawnTiles()
    {
        int tilesSpawned = 0;
        
        for (int x = 0; x < currentGrid.GetLength(0); x++)
        {
            for (int y = 0; y < currentGrid.GetLength(1); y++)
            {
                Cell cell = currentGrid[x, y];
                if (cell.isCollapsed && cell.collapsedTile != null)
                {
                    SpawnTileAtCell(cell, x, y);
                    tilesSpawned++;
                    
                    if (tilesSpawned % tilesPerFrame == 0)
                    {
                        yield return null;
                    }
                }
            }
        }
        
        UpdateStatusText($"Spawned {tilesSpawned} tiles");
    }
    
    private void SpawnTileAtCell(Cell cell, int x, int y) {
        TileData tileData = cell.collapsedTile;
        GameObject prefabToUse = tileData.prefab;
        
        if (tileData.variations.Count > 0)
        {
            prefabToUse = tileData.variations[Random.Range(0, tileData.variations.Count)];
        }
        
        if (prefabToUse != null)
        {
            Vector3 worldPosition = new Vector3(x * tileSize, 0, y * tileSize);
            Quaternion rotation = Quaternion.identity;
            
            if (tileData.allowRotation)
            {
                rotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
            }
            
            GameObject tileObject = Instantiate(prefabToUse, worldPosition, rotation, generatedMapParent);
            tileObject.name = $"{tileData.tileName}_{x}_{y}";
            cell.spawnedObject = tileObject;
            
            var tileInfo = tileObject.AddComponent<PaperTileInfo>();
            tileInfo.gridPosition = new Vector2Int(x, y);
            tileInfo.tileData = tileData;
        }
    }
    
    public virtual void ClearMap()
    {
        if (generatedMapParent != null)
        {
            DestroyImmediate(generatedMapParent.gameObject);
        }
        currentGrid = null;
    }
    
    public void OnGenerationProgress(float progress)
    {
        if (progressBar != null)
        {
            progressBar.value = progress;
        }
    }
    
    public void UpdateStatusText(string status) {
        if (statusText != null) {
            statusText.text = status;
        }
        Debug.Log($"[WFC] {status}");
    }
    
    public void UpdateStatsDisplay() {
        if (statsText == null) return;
        
        string stats = $"Grid Size: {gridWidth}x{gridHeight}\n";
        stats += $"Biome: {biomeType}\n";
        stats += $"Layout: {layoutType}\n";
        stats += $"Tile Types: {allTileData.Count}\n";
        
        if (wfcAlgorithm != null)
        {
            stats += $"Backtracks: {wfcAlgorithm.GetBacktrackCount()}\n";
        }
        
        if (generationTimer != null)
        {
            stats += $"Generation Time: {generationTimer.ElapsedMilliseconds}ms";
        }
        
        statsText.text = stats;
    }
    
    public void OnBiomeChanged(int index)
    {
        biomeType = (BiomeType)index;
    }
    
    public void OnLayoutChanged(bool isContinuous)
    {
        layoutType = isContinuous ? LayoutType.Continuous : LayoutType.Sparse;
    }
    
    public void SetGridSize(int size)
    {
        gridWidth = gridHeight = size;
        UpdateStatsDisplay();
    }
    
    public void RandomizeSeed()
    {
        seed = Random.Range(0, 10000);
    }
    
    
    void OnDrawGizmos()
    {
        if (!showGizmos || currentGrid == null) return;
        
        for (int x = 0; x < currentGrid.GetLength(0); x++)
        {
            for (int y = 0; y < currentGrid.GetLength(1); y++)
            {
                Cell cell = currentGrid[x, y];
                Vector3 position = new Vector3(x * tileSize, 0.1f, y * tileSize);
                
                if (cell.isCollapsed && cell.collapsedTile != null)
                {
                    Gizmos.color = cell.collapsedTile.gizmoColor;
                    Gizmos.DrawCube(position, Vector3.one * tileSize * 0.8f);
                }
                else
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawWireCube(position, Vector3.one * tileSize);
                    
                    
                    Gizmos.color = Color.white;
                    Vector3 textPos = position + Vector3.up * 0.5f;
                    
                }
            }
        }
    }
}




public class PaperTileInfo : MonoBehaviour
{
    public Vector2Int gridPosition;
    public TileData tileData;
    

}


#if UNITY_EDITOR


[CustomEditor(typeof(ProceduralMapGenerator))]
public class ProceduralMapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ProceduralMapGenerator generator = (ProceduralMapGenerator)target;
        
        DrawDefaultInspector();
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Generate New Map"))
        {
            generator.GenerateMap();
        }
        
        if (GUILayout.Button("Clear Map"))
        {
            generator.ClearMap();
        }
        
        if (GUILayout.Button("Randomize Seed"))
        {
            generator.RandomizeSeed();
        }

    }
}
#endif