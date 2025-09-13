using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class EnhanceProceduralMapGenerator3D : ProceduralMapGenerator
{
    [Header("3D Generation Settings")]
    public bool generate3D = true;
    public List<EnhancedStructureData> structureDatabase = new List<EnhancedStructureData>();
    
    [Header("Height Visualization")]
    public bool visualizeHeights = true;
    public Material heightVisualizationMaterial;
    public float heightScaleFactor = 1f;
    
    [Header("Structure Generation")]
    public bool generateStructures = true;
    public Transform structuresParent;
    
    [Header("3D UI")]
    public Toggle enable3DToggle;
    public Toggle showHeightToggle;
    public Toggle showStructuresToggle;
    public Slider heightScaleSlider;
    public TextMeshProUGUI heightStatsText;
    
    
    private EnhancedWFC3DExtension wfc3DExtension;
    private HeightMapResult heightMapResult;
    private StructurePlacementResult structurePlacementResult;
    private Transform heightVisualizationParent;
    
    protected void Start()
    {
        base.Start();
        Setup3DUI();
    }
    
    private void Setup3DUI()
    {
        if (enable3DToggle != null)
        {
            enable3DToggle.onValueChanged.AddListener(OnEnable3DChanged);
            enable3DToggle.isOn = generate3D;
        }
        
        if (showHeightToggle != null)
        {
            showHeightToggle.onValueChanged.AddListener(OnShowHeightChanged);
            showHeightToggle.isOn = visualizeHeights;
        }
        
        if (showStructuresToggle != null)
        {
            showStructuresToggle.onValueChanged.AddListener(OnShowStructuresChanged);
            showStructuresToggle.isOn = generateStructures;
        }
        
        if (heightScaleSlider != null)
        {
            heightScaleSlider.onValueChanged.AddListener(OnHeightScaleChanged);
            heightScaleSlider.value = heightScaleFactor;
        }
    }
    
    public override void GenerateMap()
    {
        if (isGenerating) return;
        
        StartCoroutine(GenerateMap3DCoroutine());
    }
    
    private IEnumerator GenerateMap3DCoroutine()
    {
        isGenerating = true;
        generationTimer = System.Diagnostics.Stopwatch.StartNew();
        
        if (generateButton != null)
            generateButton.interactable = false;
        
        
        ClearMap();
        
        
        generatedMapParent = new GameObject("Generated Map 3D").transform;
        generatedMapParent.SetParent(transform);
        
        if (structuresParent == null)
        {
            structuresParent = new GameObject("Structures").transform;
            structuresParent.SetParent(generatedMapParent);
        }
        
        heightVisualizationParent = new GameObject("Height Visualization").transform;
        heightVisualizationParent.SetParent(generatedMapParent);
        
        
        int actualSeed = useRandomSeed ? Random.Range(0, 10000) : seed;
        
        UpdateStatusText($"Initializing 3D generation (Seed: {actualSeed})...");
        yield return null;
        
        
        UpdateStatusText("Step: Generating 2D base map...");
        
        
        yield return StartCoroutine(Generate2DBase(actualSeed));
        bool success = generationSuccess;
        
        if (!success)
        {
            GenerationFailed("2D base generation failed");
            yield break;
        }
        
        
        if (generate3D)
        {
            UpdateStatusText("Step: Generating height map...");
            yield return StartCoroutine(GenerateHeightMapCoroutine());
        }
        
        
        UpdateStatusText("Step: Spawning tiles with height...");
        yield return StartCoroutine(SpawnTilesWithHeight());
        
        
        if (generate3D && generateStructures)
        {
            UpdateStatusText("Step: Placing structures...");
            yield return StartCoroutine(PlaceStructuresCoroutine());
        }
        
        
        generationTimer.Stop();
        UpdateStatusText($"3D Generation completed! ({generationTimer.ElapsedMilliseconds}ms)");
        UpdateStatsDisplay();
        Update3DStatsDisplay();
        
        if (generateButton != null)
            generateButton.interactable = true;
        
        isGenerating = false;
    }
    
    private IEnumerator Generate2DBase(int seed)
    {
        
        wfcAlgorithm = new WFCAlgorithm(gridWidth, gridHeight, allTileData, biomeType, layoutType, seed);
        wfcAlgorithm.OnProgressUpdate += OnGenerationProgress;
        wfcAlgorithm.OnStatusUpdate += UpdateStatusText;
        
        bool success = false;
        if (asyncGeneration)
        {
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
        }
        
        yield return success;
    }
    
    private IEnumerator GenerateHeightMapCoroutine()
    {
        wfc3DExtension = new EnhancedWFC3DExtension(currentGrid, structureDatabase, biomeType, seed);
        wfc3DExtension.OnStatusUpdate += UpdateStatusText;
        
        
        heightMapResult = wfc3DExtension.GenerateHeightMap();
        
        if (visualizeHeights)
        {
            yield return StartCoroutine(VisualizeHeightMap());
        }
        
        yield return null;
    }
    
    private IEnumerator PlaceStructuresCoroutine()
    {
        if (wfc3DExtension == null) yield break;
        
        structurePlacementResult = wfc3DExtension.PlaceStructures();
        
        
        int structuresSpawned = 0;
        foreach (var kvp in structurePlacementResult.placedStructures)
        {
            Vector2Int gridPos = kvp.Key;
            StructureInstance structure = kvp.Value;
            
            SpawnStructureObject(structure, gridPos);
            structuresSpawned++;
            
            if (structuresSpawned % tilesPerFrame == 0)
            {
                yield return null;
            }
        }
        
        UpdateStatusText($"Spawned {structuresSpawned} structures");
    }
    
    private IEnumerator SpawnTilesWithHeight()
    {
        int tilesSpawned = 0;
        
        for (int x = 0; x < currentGrid.GetLength(0); x++)
        {
            for (int y = 0; y < currentGrid.GetLength(1); y++)
            {
                Cell cell = currentGrid[x, y];
                if (cell.isCollapsed && cell.collapsedTile != null)
                {
                    SpawnTileWithHeight(cell, x, y);
                    tilesSpawned++;
                    
                    if (tilesSpawned % tilesPerFrame == 0)
                    {
                        yield return null;
                    }
                }
            }
        }
        
        UpdateStatusText($"Spawned {tilesSpawned} tiles with height");
    }
    
    private void SpawnTileWithHeight(Cell cell, int x, int y)
    {
        TileData tileData = cell.collapsedTile;
        GameObject prefabToUse = tileData.prefab;
        
        if (tileData.variations.Count > 0)
        {
            prefabToUse = tileData.variations[Random.Range(0, tileData.variations.Count)];
        }
        
        if (prefabToUse != null)
        {
            float height = generate3D && heightMapResult != null ? 
                          heightMapResult.heightMap[x, y] * heightScaleFactor : 0f;
            
            Vector3 worldPosition = new Vector3(x * tileSize, height, y * tileSize);
            Quaternion rotation = Quaternion.identity;
            
            if (tileData.allowRotation)
            {
                rotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
            }
            
            GameObject tileObject = Instantiate(prefabToUse, worldPosition, rotation, generatedMapParent);
            tileObject.name = $"{tileData.tileName}_{x}_{y}";
            cell.spawnedObject = tileObject;
            
            var tileInfo = tileObject.GetComponent<PaperTileInfo3D>();
            if (tileInfo == null)
                tileInfo = tileObject.AddComponent<PaperTileInfo3D>();
            
            tileInfo.gridPosition = new Vector2Int(x, y);
            tileInfo.tileData = tileData;
            tileInfo.height = height;
        }
    }
    
    private void SpawnStructureObject(StructureInstance structure, Vector2Int gridPos)
    {
        if (structure.prefab == null) return;
        
        float tileHeight = heightMapResult.heightMap[gridPos.x, gridPos.y] * heightScaleFactor;
        Vector3 worldPosition = new Vector3(
            gridPos.x * tileSize + structure.structureData.positionOffset.x,
            tileHeight + structure.structureData.positionOffset.y,
            gridPos.y * tileSize + structure.structureData.positionOffset.z
        );
        
        Quaternion rotation = Quaternion.Euler(0, structure.rotation, 0);
        
        GameObject structureObject = Instantiate(structure.prefab, worldPosition, rotation, structuresParent);
        structureObject.transform.localScale = Vector3.one * structure.scale;
        structureObject.name = $"{structure.structureData.structureType}_{gridPos.x}_{gridPos.y}";
        
        structure.spawnedObject = structureObject;
        
        
        var structureInfo = structureObject.AddComponent<StructureInfo3D>();
        structureInfo.structureInstance = structure;
        structureInfo.gridPosition = gridPos;
    }
    
    private IEnumerator VisualizeHeightMap()
    {
        if (heightMapResult == null || heightVisualizationMaterial == null) yield break;
        
        for (int x = 0; x < heightMapResult.heightMap.GetLength(0); x++)
        {
            for (int y = 0; y < heightMapResult.heightMap.GetLength(1); y++)
            {
                float height = heightMapResult.heightMap[x, y] * heightScaleFactor;
                Vector3 position = new Vector3(x * tileSize, height + 0.01f, y * tileSize);
                
                GameObject heightQuad = CreateHeightVisualizationQuad(position, height);
                heightQuad.transform.SetParent(heightVisualizationParent);
            }
        }
        
        yield return null;
    }
    
    private GameObject CreateHeightVisualizationQuad(Vector3 position, float height)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = position;
        quad.transform.rotation = Quaternion.Euler(90, 0, 0);
        quad.transform.localScale = Vector3.one * tileSize * 0.9f;
        
        Renderer renderer = quad.GetComponent<Renderer>();
        if (heightVisualizationMaterial != null)
        {
            renderer.material = heightVisualizationMaterial;
        }
        
        
        float normalizedHeight = Mathf.InverseLerp(heightMapResult.minHeight, heightMapResult.maxHeight, height / heightScaleFactor);
        Color heightColor = Color.Lerp(Color.blue, Color.red, normalizedHeight);
        renderer.material.color = heightColor;
        
        return quad;
    }
    
    public override void ClearMap()
    {
        base.ClearMap();
        
        if (heightVisualizationParent != null)
        {
            DestroyImmediate(heightVisualizationParent.gameObject);
        }
        
        if (structuresParent != null)
        {
            
            for (int i = structuresParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(structuresParent.GetChild(i).gameObject);
            }
        }
        
        heightMapResult = null;
        structurePlacementResult = null;
    }
    
    private void Update3DStatsDisplay()
    {
        if (heightStatsText == null || heightMapResult == null) return;
        
        string heightStats = $"Height Stats:\n";
        heightStats += $"Min: {heightMapResult.minHeight:F2}\n";
        heightStats += $"Max: {heightMapResult.maxHeight:F2}\n";
        heightStats += $"Avg: {heightMapResult.averageHeight:F2}\n";
        
        if (structurePlacementResult != null)
        {
            heightStats += $"\nStructures:\n";
            heightStats += $"Total: {structurePlacementResult.totalPlaced}\n";
            
            foreach (var kvp in structurePlacementResult.structuresByType)
            {
                heightStats += $"{kvp.Key}: {kvp.Value}\n";
            }
        }
        
        heightStatsText.text = heightStats;
    }
    
    private void GenerationFailed(string reason)
    {
        generationTimer.Stop();
        UpdateStatusText($"Generation failed: {reason}");
        
        if (generateButton != null)
            generateButton.interactable = true;
        
        isGenerating = false;
    }
    
    
    private void OnEnable3DChanged(bool enabled)
    {
        generate3D = enabled;
    }
    
    private void OnShowHeightChanged(bool enabled)
    {
        visualizeHeights = enabled;
        if (heightVisualizationParent != null)
        {
            heightVisualizationParent.gameObject.SetActive(enabled);
        }
    }
    
    private void OnShowStructuresChanged(bool enabled)
    {
        generateStructures = enabled;
        if (structuresParent != null)
        {
            structuresParent.gameObject.SetActive(enabled);
        }
    }
    
    private void OnHeightScaleChanged(float scale)
    {
        heightScaleFactor = scale;
        
        
        if (currentGrid != null && heightMapResult != null)
        {
            for (int x = 0; x < currentGrid.GetLength(0); x++)
            {
                for (int y = 0; y < currentGrid.GetLength(1); y++)
                {
                    Cell cell = currentGrid[x, y];
                    if (cell.spawnedObject != null)
                    {
                        float newHeight = heightMapResult.heightMap[x, y] * heightScaleFactor;
                        Vector3 pos = cell.spawnedObject.transform.position;
                        cell.spawnedObject.transform.position = new Vector3(pos.x, newHeight, pos.z);
                    }
                }
            }
        }
        
        
        if (structurePlacementResult != null)
        {
            foreach (var kvp in structurePlacementResult.placedStructures)
            {
                if (kvp.Value.spawnedObject != null)
                {
                    Vector2Int gridPos = kvp.Key;
                    float newHeight = heightMapResult.heightMap[gridPos.x, gridPos.y] * heightScaleFactor;
                    Vector3 pos = kvp.Value.spawnedObject.transform.position;
                    kvp.Value.spawnedObject.transform.position = new Vector3(pos.x, newHeight, pos.z);
                }
            }
        }
    }
    
    
    public HeightMapResult GetHeightMapResult() => heightMapResult;
    public StructurePlacementResult GetStructurePlacementResult() => structurePlacementResult;
    public WFC3DExtension GetWFC3DExtension() => wfc3DExtension;
}



#if UNITY_EDITOR
[CustomEditor(typeof(EnhanceProceduralMapGenerator3D))]
public class EnhanceProceduralMapGenerator3DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EnhanceProceduralMapGenerator3D generator = (EnhanceProceduralMapGenerator3D)target;
        
        DrawDefaultInspector();
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Generate 3D Map"))
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
        
        GUILayout.Space(10);
        
        if (generator.GetHeightMapResult() != null)
        {
            EditorGUILayout.HelpBox($"Height Map Generated!\n" +
                $"Min: {generator.GetHeightMapResult().minHeight:F2}\n" +
                $"Max: {generator.GetHeightMapResult().maxHeight:F2}\n" +
                $"Avg: {generator.GetHeightMapResult().averageHeight:F2}", 
                MessageType.Info);
        }
        //
        // if (generator.GetStructurePlacementResult() != null)
        // {
        //     string structureInfo = $"Structures Placed: {generator.GetStructurePlacementResult().totalPlaced}\n";
        //     foreach (var kvp in generator.GetStructurePlacementResult().structuresByType)
        //     {
        //         structureInfo += $"{kvp.Key}: {kvp.Value}\n";
        //     }
        //     
        //     EditorGUILayout.HelpBox(structureInfo, MessageType.Info);
        // }
        
        
    }
}
#endif