using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class WFCTerrainStructurePlacer : MonoBehaviour
{
    [Header("References")]
    public ProceduralMapGenerator wfcGenerator;
    public Terrain targetTerrain;
    
    [Header("Structure Settings")]
    public List<StructureInfo> structureInfos = new List<StructureInfo>();
    public Transform structuresParent;
    
    [Header("Global Settings")]
    [Range(0.1f, 3f)]
    public float globalDensityMultiplier = 1f;
    [Range(0f, 1f)]
    public float positionRandomness = 0.3f;
    public bool alignToTerrainNormal = false;
    
    [Header("Performance")]
    [Range(10, 1000)]
    public int structuresPerFrame = 50;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showGizmos = true;
    
    private Cell[,] wfcGrid;
    private List<PlacedStructureData> placedStructures = new List<PlacedStructureData>();
    
    [System.Serializable]
    public class PlacedStructureData
    {
        public GameObject structureObject;
        public StructureInfo sourceInfo;
        public Vector2Int originalGridPosition;
        public Vector3 worldPosition;
        public TileType sourceTileType;
    }
    
    void Start()
    {
        if (wfcGenerator == null)
            wfcGenerator = FindObjectOfType<ProceduralMapGenerator>();
            
        if (targetTerrain == null)
            targetTerrain = FindMostRecentTerrain();
            
        SetupStructuresParent();
    }
    
    [ContextMenu("Place All Structures")]
    public void PlaceAllStructures()
    {
        if (!ValidateComponents())
        {
            Debug.LogError("Cannot place structures - missing components");
            return;
        }
        
        StartCoroutine(PlaceStructuresCoroutine());
    }
    
    [ContextMenu("Clear All Structures")]
    public void ClearAllStructures()
    {
        ClearExistingStructures();
        Debug.Log("All structures cleared");
    }
    
    [ContextMenu("Refresh Structure Info")]
    public void RefreshStructureInfo()
    {
        if (showDebugInfo)
        {
            Debug.Log($"=== Structure Placement Info ===");
            Debug.Log($"WFC Grid Size: {(wfcGrid != null ? $"{wfcGrid.GetLength(0)}x{wfcGrid.GetLength(1)}" : "Not loaded")}");
            Debug.Log($"Target Terrain: {(targetTerrain != null ? targetTerrain.name : "None")}");
            Debug.Log($"Structure Types: {structureInfos.Count}");
            Debug.Log($"Currently Placed: {placedStructures.Count}");
            
            if (wfcGrid != null)
            {
                var tileTypeCounts = GetTileTypeDistribution();
                foreach (var kvp in tileTypeCounts)
                {
                    Debug.Log($"  {kvp.Key}: {kvp.Value} tiles");
                }
            }
        }
    }
    
    public System.Collections.IEnumerator PlaceStructuresCoroutine()
    {
        Debug.Log("Starting structure placement on terrain...");
        
        ClearExistingStructures();
        LoadWFCGrid();
        
        if (wfcGrid == null)
        {
            Debug.LogError("No WFC grid available");
            yield break;
        }
        
        int totalPlaced = 0;
        int wfcWidth = wfcGrid.GetLength(0);
        int wfcHeight = wfcGrid.GetLength(1);
        int processedThisFrame = 0;
        
        List<Vector3> allPlacedPositions = new List<Vector3>();
        
        for (int x = 0; x < wfcWidth; x++)
        {
            for (int y = 0; y < wfcHeight; y++)
            {
                Vector2Int gridPos = new Vector2Int(x, y);
                Cell cell = wfcGrid[x, y];
                
                if (!cell.isCollapsed || cell.collapsedTile == null) continue;
                
                TileType tileType = cell.collapsedTile.tileType;
                
                var compatibleStructures = structureInfos.Where(s => s.CanPlaceOnTile(tileType)).ToList();
                
                foreach (var structureInfo in compatibleStructures)
                {
                    int placed = PlaceStructuresForTile(gridPos, structureInfo, allPlacedPositions, tileType);
                    totalPlaced += placed;
                    processedThisFrame += placed;
                }
                
                if (processedThisFrame >= structuresPerFrame)
                {
                    processedThisFrame = 0;
                    yield return null;
                }
            }
        }
        
        Debug.Log($"Structure placement completed! Placed {totalPlaced} structures total");
        
        if (showDebugInfo)
        {
            PrintPlacementStatistics();
        }
    }
    
    private int PlaceStructuresForTile(Vector2Int gridPos, StructureInfo structureInfo, List<Vector3> allPlacedPositions, TileType sourceTileType)
    {
        int placedCount = 0;
        
        float spawnMultiplier = structureInfo.CalculateSpawnMultiplier(gridPos, wfcGrid);
        float finalSpawnChance = structureInfo.spawnChance * globalDensityMultiplier * spawnMultiplier;
        finalSpawnChance = Mathf.Clamp01(finalSpawnChance);
        
        int structuresToPlace = 0;
        for (int i = 0; i < structureInfo.maxPerTile; i++)
        {
            if (Random.Range(0f, 1f) < finalSpawnChance)
            {
                structuresToPlace++;
            }
        }
        
        for (int i = 0; i < structuresToPlace; i++)
        {
            Vector3 worldPos = GridToWorldPosition(gridPos);
            Vector3 finalPos = AddPositionVariation(worldPos);
            
            if (!CheckSpacing(finalPos, allPlacedPositions, structureInfo))
                continue;
                
            finalPos.y = SampleTerrainHeight(finalPos);
            finalPos += structureInfo.positionOffset;
            
            GameObject structure = CreateStructureObject(structureInfo, finalPos, gridPos);
            
            if (structure != null)
            {
                var placedData = new PlacedStructureData
                {
                    structureObject = structure,
                    sourceInfo = structureInfo,
                    originalGridPosition = gridPos,
                    worldPosition = finalPos,
                    sourceTileType = sourceTileType
                };
                
                placedStructures.Add(placedData);
                allPlacedPositions.Add(finalPos);
                placedCount++;
            }
        }
        
        return placedCount;
    }
    
    private Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        if (targetTerrain == null) return new Vector3(gridPos.x, 0, gridPos.y);
        
        int wfcWidth = wfcGrid.GetLength(0);
        int wfcHeight = wfcGrid.GetLength(1);
        
        float normalizedX = (float)gridPos.x / (wfcWidth - 1);
        float normalizedZ = (float)gridPos.y / (wfcHeight - 1);
        
        Vector3 terrainPos = targetTerrain.transform.position;
        Vector3 terrainSize = targetTerrain.terrainData.size;
        
        return new Vector3(
            terrainPos.x + normalizedX * terrainSize.x,
            0f,
            terrainPos.z + normalizedZ * terrainSize.z
        );
    }
    
    private Vector3 AddPositionVariation(Vector3 basePosition)
    {
        Vector3 variation = new Vector3(
            Random.Range(-positionRandomness, positionRandomness),
            0f,
            Random.Range(-positionRandomness, positionRandomness)
        );
        
        return basePosition + variation;
    }
    
    private bool CheckSpacing(Vector3 position, List<Vector3> existingPositions, StructureInfo structureInfo)
    {
        foreach (Vector3 existingPos in existingPositions)
        {
            if (Vector3.Distance(position, existingPos) < structureInfo.minSpacing)
            {
                return false;
            }
        }
        return true;
    }
    
    private float SampleTerrainHeight(Vector3 worldPosition)
    {
        if (targetTerrain == null) return 0f;
        
        return targetTerrain.SampleHeight(worldPosition);
    }
    
    private GameObject CreateStructureObject(StructureInfo structureInfo, Vector3 position, Vector2Int gridPos)
    {
        GameObject prefabToUse = SelectPrefab(structureInfo);
        if (prefabToUse == null) return null;
        
        Quaternion rotation = CalculateRotation(structureInfo, position);
        
        GameObject structure = Instantiate(prefabToUse, position, rotation, structuresParent);
        
        float scale = Random.Range(structureInfo.scaleMin, structureInfo.scaleMax);
        structure.transform.localScale = Vector3.one * scale;
        
        structure.name = $"{structureInfo.structureName}_{gridPos.x}_{gridPos.y}_{Random.Range(100, 999)}";
        
        return structure;
    }
    
    private GameObject SelectPrefab(StructureInfo structureInfo)
    {
        if (structureInfo.prefab == null) return null;
        
        if (structureInfo.variations.Count == 0)
        {
            return structureInfo.prefab;
        }
        
        var allPrefabs = new List<GameObject> { structureInfo.prefab };
        allPrefabs.AddRange(structureInfo.variations.Where(v => v != null));
        
        return allPrefabs[Random.Range(0, allPrefabs.Count)];
    }
    
    private Quaternion CalculateRotation(StructureInfo structureInfo, Vector3 position)
    {
        Quaternion rotation = Quaternion.identity;
        
        if (alignToTerrainNormal && targetTerrain != null)
        {
            Vector3 normal = GetTerrainNormal(position);
            Vector3 forward = Vector3.Cross(normal, Vector3.right).normalized;
            if (forward == Vector3.zero) forward = Vector3.forward;
            
            rotation = Quaternion.LookRotation(forward, normal);
        }
        
        if (structureInfo.randomRotation)
        {
            rotation *= Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }
        
        return rotation;
    }
    
    private Vector3 GetTerrainNormal(Vector3 worldPosition)
    {
        if (targetTerrain == null) return Vector3.up;
        
        Vector3 terrainLocalPos = worldPosition - targetTerrain.transform.position;
        Vector2 normalizedPos = new Vector2(
            terrainLocalPos.x / targetTerrain.terrainData.size.x,
            terrainLocalPos.z / targetTerrain.terrainData.size.z
        );
        
        normalizedPos.x = Mathf.Clamp01(normalizedPos.x);
        normalizedPos.y = Mathf.Clamp01(normalizedPos.y);
        
        return targetTerrain.terrainData.GetInterpolatedNormal(normalizedPos.x, normalizedPos.y);
    }
    
    private bool ValidateComponents()
    {
        if (wfcGenerator == null)
        {
            Debug.LogError("WFC Generator not assigned");
            return false;
        }
        
        if (targetTerrain == null)
        {
            Debug.LogError("Target Terrain not found");
            return false;
        }
        
        if (structureInfos.Count == 0)
        {
            Debug.LogWarning("No StructureInfo objects assigned");
            return false;
        }
        
        return true;
    }
    
    private void LoadWFCGrid()
    {
        if (wfcGenerator.wfcAlgorithm == null)
        {
            Debug.LogError("WFC Algorithm not initialized");
            return;
        }
        
        wfcGrid = wfcGenerator.wfcAlgorithm.GetGrid();
        
        if (wfcGrid == null)
        {
            Debug.LogError("WFC Grid is null - generate a map first");
        }
    }
    
    private Terrain FindMostRecentTerrain()
    {
        Terrain[] terrains = FindObjectsOfType<Terrain>();
        Terrain mostRecent = null;
        
        foreach (var terrain in terrains)
        {
            if (terrain.name.Contains("WFC_Terrain"))
            {
                mostRecent = terrain;
            }
        }
        
        if (mostRecent == null && terrains.Length > 0)
        {
            mostRecent = terrains[0];
        }
        
        return mostRecent;
    }
    
    private void SetupStructuresParent()
    {
        if (structuresParent == null)
        {
            GameObject parentObj = new GameObject("Placed_Structures");
            structuresParent = parentObj.transform;
            structuresParent.SetParent(transform);
        }
    }
    
    private void ClearExistingStructures()
    {
        if (structuresParent == null) return;
        
        for (int i = structuresParent.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(structuresParent.GetChild(i).gameObject);
            else
                DestroyImmediate(structuresParent.GetChild(i).gameObject);
        }
        
        placedStructures.Clear();
    }
    
    private Dictionary<TileType, int> GetTileTypeDistribution()
    {
        if (wfcGrid == null) return new Dictionary<TileType, int>();
        
        var distribution = new Dictionary<TileType, int>();
        
        for (int x = 0; x < wfcGrid.GetLength(0); x++)
        {
            for (int y = 0; y < wfcGrid.GetLength(1); y++)
            {
                Cell cell = wfcGrid[x, y];
                if (cell.isCollapsed && cell.collapsedTile != null)
                {
                    TileType tileType = cell.collapsedTile.tileType;
                    if (!distribution.ContainsKey(tileType))
                        distribution[tileType] = 0;
                    distribution[tileType]++;
                }
            }
        }
        
        return distribution;
    }
    
    private void PrintPlacementStatistics()
    {
        var structureTypeCounts = placedStructures
            .GroupBy(p => p.sourceInfo.structureName)
            .ToDictionary(g => g.Key, g => g.Count());
            
        var tileTypeCounts = placedStructures
            .GroupBy(p => p.sourceTileType)
            .ToDictionary(g => g.Key, g => g.Count());
        
        Debug.Log("=== Structure Placement Statistics ===");
        
        Debug.Log("By Structure Type:");
        foreach (var kvp in structureTypeCounts)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value}");
        }
        
        Debug.Log("By Source Tile Type:");
        foreach (var kvp in tileTypeCounts)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value}");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        if (targetTerrain != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = targetTerrain.transform.position + targetTerrain.terrainData.size * 0.5f;
            center.y = targetTerrain.transform.position.y;
            Gizmos.DrawWireCube(center, targetTerrain.terrainData.size);
        }
        
        Gizmos.color = Color.green;
        foreach (var placedData in placedStructures)
        {
            if (placedData.structureObject != null)
            {
                Gizmos.DrawWireSphere(placedData.worldPosition, 0.5f);
            }
        }
    }
}
#if UNITY_EDITOR
[CustomEditor(typeof(WFCTerrainStructurePlacer))]
public class WFCTerrainStructurePlacerEditor : Editor
{
    private WFCTerrainStructurePlacer placer;
    private bool showAdvancedSettings = false;
    private bool showStatistics = true;
    private bool showHelpInfo = false;
    
    private void OnEnable()
    {
        placer = (WFCTerrainStructurePlacer)target;
    }
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        DrawMainActionButtons();
        
        EditorGUILayout.Space(10);
        
        DrawQuickSetupSection();
        
        EditorGUILayout.Space(10);
        
        DrawStatusSection();
        
        EditorGUILayout.Space(10);
        
        DrawAdvancedSection();
        
        EditorGUILayout.Space(10);
        

        DrawHelpSection();
    }
    
    private void DrawMainActionButtons()
    {
        EditorGUILayout.LabelField("Structure Placement", EditorStyles.boldLabel);
        
        EditorGUI.BeginDisabledGroup(Application.isPlaying && placer.gameObject.activeInHierarchy == false);
        
        if (GUILayout.Button("Place All Structures", GUILayout.Height(35)))
        {
            if (Application.isPlaying)
            {
                placer.PlaceAllStructures();
            }
            else
            {
                EditorApplication.isPlaying = true;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }
        }
        
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Clear Structures"))
        {
            placer.ClearAllStructures();
        }
        
        if (GUILayout.Button("Refresh Info"))
        {
            placer.RefreshStructureInfo();
        }
        
        EditorGUILayout.EndHorizontal();
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Structure placement requires Play Mode. Click 'Place All Structures' to enter Play Mode automatically.", MessageType.Info);
        }
    }
    
    private void DrawQuickSetupSection()
    {
        EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Auto-Find WFC Generator"))
        {
            var generator = FindObjectOfType<ProceduralMapGenerator>();
            if (generator != null)
            {
                placer.wfcGenerator = generator;
                EditorUtility.SetDirty(placer);
                Debug.Log($"Found and assigned: {generator.name}");
            }
            else
            {
                Debug.LogWarning("No ProceduralMapGenerator found in scene");
            }
        }
        
        if (GUILayout.Button("Auto-Find Terrain"))
        {
            var terrain = FindMostRecentTerrain();
            if (terrain != null)
            {
                placer.targetTerrain = terrain;
                EditorUtility.SetDirty(placer);
                Debug.Log($"Found and assigned terrain: {terrain.name}");
            }
            else
            {
                Debug.LogWarning("No suitable terrain found in scene");
            }
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Create Structure Parent"))
        {
            if (placer.structuresParent == null)
            {
                GameObject parentObj = new GameObject("Placed_Structures");
                parentObj.transform.SetParent(placer.transform);
                placer.structuresParent = parentObj.transform;
                EditorUtility.SetDirty(placer);
                Debug.Log("Created structures parent object");
            }
            else
            {
                Debug.Log("Structure parent already exists");
            }
        }
        
        if (GUILayout.Button("Load StructureInfos"))
        {
            LoadAllStructureInfos();
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawStatusSection()
    {
        showStatistics = EditorGUILayout.Foldout(showStatistics, "Status & Statistics", true);
        
        if (showStatistics)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.LabelField("Component Status:", EditorStyles.boldLabel);
            
            DrawStatusLine("WFC Generator", placer.wfcGenerator != null, 
                placer.wfcGenerator != null ? placer.wfcGenerator.name : "Not Assigned");
            
            DrawStatusLine("Target Terrain", placer.targetTerrain != null,
                placer.targetTerrain != null ? placer.targetTerrain.name : "Not Assigned");
            
            DrawStatusLine("Structures Parent", placer.structuresParent != null,
                placer.structuresParent != null ? $"{placer.structuresParent.childCount} children" : "Not Assigned");
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Structure Configuration:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Structure Types: {placer.structureInfos.Count}");
            
            if (placer.structureInfos.Count > 0)
            {
                EditorGUI.indentLevel++;
                foreach (var structureInfo in placer.structureInfos)
                {
                    if (structureInfo != null)
                    {
                        string tileTypes = string.Join(", ", structureInfo.canPlaceOnTiles);
                        EditorGUILayout.LabelField($"• {structureInfo.structureName}", $"On: {tileTypes}");
                    }
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);
            
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Runtime Statistics:", EditorStyles.boldLabel);
                
                int placedCount = placer.structuresParent != null ? placer.structuresParent.childCount : 0;
                EditorGUILayout.LabelField($"Currently Placed: {placedCount}");
                
                if (placedCount > 0 && placer.structuresParent != null)
                {
                    var typeCounts = new System.Collections.Generic.Dictionary<string, int>();
                    
                    foreach (Transform child in placer.structuresParent)
                    {
                        string structureName = child.name.Split('_')[0];
                        if (!typeCounts.ContainsKey(structureName))
                            typeCounts[structureName] = 0;
                        typeCounts[structureName]++;
                    }
                    
                    EditorGUI.indentLevel++;
                    foreach (var kvp in typeCounts)
                    {
                        EditorGUILayout.LabelField($"• {kvp.Key}: {kvp.Value}");
                    }
                    EditorGUI.indentLevel--;
                }
            }
            
            EditorGUI.indentLevel--;
        }
    }
    
    private void DrawAdvancedSection()
    {
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Tools", true);
        
        if (showAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Validate Setup"))
            {
                ValidateSetup();
            }
            
            if (GUILayout.Button("Export Statistics"))
            {
                ExportStatistics();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Randomize All Scales"))
            {
                RandomizeAllScales();
            }
            
            if (GUILayout.Button("Align to Terrain"))
            {
                AlignAllToTerrain();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;
        }
    }
    
    private void DrawHelpSection()
    {
        showHelpInfo = EditorGUILayout.Foldout(showHelpInfo, "Help & Information", true);
        
        if (showHelpInfo)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.HelpBox(
                "Setup Steps:\n" +
                "1. Create StructureInfo assets (Assets → Create → WFC → StructureInfo)\n" +
                "2. Configure which tiles each structure can be placed on\n" +
                "3. Set up neighbor rules for clustering/avoidance\n" +
                "4. Assign WFC Generator and Target Terrain\n" +
                "5. Click 'Place All Structures' to execute",
                MessageType.Info
            );
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "Common Issues:\n" +
                "• No structures placed: Check if StructureInfo tile types match WFC output\n" +
                "• Structures in wrong places: Verify terrain is aligned with WFC grid\n" +
                "• Too many/few structures: Adjust Global Density Multiplier",
                MessageType.Warning
            );
            
            EditorGUI.indentLevel--;
        }
    }
    
    private void DrawStatusLine(string label, bool isValid, string value)
    {
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.LabelField(label, GUILayout.Width(120));
        
        Color originalColor = GUI.color;
        GUI.color = isValid ? Color.green : Color.red;
        string status = isValid ? "✓" : "✗";
        EditorGUILayout.LabelField(status, GUILayout.Width(20));
        GUI.color = originalColor;
        
        EditorGUILayout.LabelField(value);
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void LoadAllStructureInfos()
    {
        string[] guids = AssetDatabase.FindAssets("t:StructureInfo");
        var structureInfos = new System.Collections.Generic.List<StructureInfo>();
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StructureInfo structureInfo = AssetDatabase.LoadAssetAtPath<StructureInfo>(path);
            if (structureInfo != null)
            {
                structureInfos.Add(structureInfo);
            }
        }
        
        placer.structureInfos = structureInfos;
        EditorUtility.SetDirty(placer);
        
        Debug.Log($"Loaded {structureInfos.Count} StructureInfo assets");
    }
    
    private Terrain FindMostRecentTerrain()
    {
        Terrain[] terrains = FindObjectsOfType<Terrain>();
        Terrain mostRecent = null;
        
        foreach (var terrain in terrains)
        {
            if (terrain.name.Contains("WFC_Terrain"))
            {
                mostRecent = terrain;
            }
        }
        
        if (mostRecent == null && terrains.Length > 0)
        {
            mostRecent = terrains[0];
        }
        
        return mostRecent;
    }
    
    private void ValidateSetup()
    {
        Debug.Log("=== Setup Validation ===");
        
        bool hasErrors = false;
        
        if (placer.wfcGenerator == null)
        {
            Debug.LogError("Missing WFC Generator reference");
            hasErrors = true;
        }
        
        if (placer.targetTerrain == null)
        {
            Debug.LogError("Missing Target Terrain reference");
            hasErrors = true;
        }
        
        if (placer.structureInfos.Count == 0)
        {
            Debug.LogWarning("No StructureInfo objects assigned");
            hasErrors = true;
        }
        else
        {
            foreach (var info in placer.structureInfos)
            {
                if (info == null)
                {
                    Debug.LogError("Null StructureInfo in list");
                    hasErrors = true;
                    continue;
                }
                
                if (info.prefab == null)
                {
                    Debug.LogError($"StructureInfo '{info.structureName}' has no prefab assigned");
                    hasErrors = true;
                }
                
                if (info.canPlaceOnTiles.Count == 0)
                {
                    Debug.LogWarning($"StructureInfo '{info.structureName}' has no target tile types");
                }
            }
        }
        
        if (!hasErrors)
        {
            Debug.Log("Setup validation passed - all components properly configured");
        }
    }
    
    private void ExportStatistics()
    {
        if (!Application.isPlaying || placer.structuresParent == null)
        {
            Debug.LogWarning("Cannot export statistics - enter Play Mode and place structures first");
            return;
        }
        
        string stats = "=== Structure Placement Statistics ===\n";
        stats += $"Total Structures: {placer.structuresParent.childCount}\n";
        stats += $"Global Density: {placer.globalDensityMultiplier}\n";
        stats += $"Position Randomness: {placer.positionRandomness}\n\n";
        
        var typeCounts = new System.Collections.Generic.Dictionary<string, int>();
        foreach (Transform child in placer.structuresParent)
        {
            string typeName = child.name.Split('_')[0];
            if (!typeCounts.ContainsKey(typeName))
                typeCounts[typeName] = 0;
            typeCounts[typeName]++;
        }
        
        stats += "By Structure Type:\n";
        foreach (var kvp in typeCounts)
        {
            stats += $"  {kvp.Key}: {kvp.Value}\n";
        }
        
        Debug.Log(stats);
        GUIUtility.systemCopyBuffer = stats;
        Debug.Log("Statistics copied to clipboard");
    }
    
    private void RandomizeAllScales()
    {
        if (placer.structuresParent == null) return;
        
        int count = 0;
        foreach (Transform child in placer.structuresParent)
        {
            float randomScale = Random.Range(0.8f, 1.2f);
            child.localScale = Vector3.one * randomScale;
            count++;
        }
        
        Debug.Log($"Randomized scale for {count} structures");
    }
    
    private void AlignAllToTerrain()
    {
        if (placer.structuresParent == null || placer.targetTerrain == null) return;
        
        int count = 0;
        foreach (Transform child in placer.structuresParent)
        {
            Vector3 pos = child.position;
            pos.y = placer.targetTerrain.SampleHeight(pos) + 0.1f;
            child.position = pos;
            count++;
        }
        
        Debug.Log($"Aligned {count} structures to terrain surface");
    }
    
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            
            EditorApplication.delayCall += () =>
            {
                if (placer != null)
                {
                    placer.PlaceAllStructures();
                }
            };
        }
    }
}
#endif