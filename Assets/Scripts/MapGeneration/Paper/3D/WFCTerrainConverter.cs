using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class WFCTerrainConverter : MonoBehaviour
{
    [Header("References")]
    public ProceduralMapGenerator wfcGenerator;
    public Button convertButton;
    
    [Header("Terrain Settings")]
    public Vector3 terrainSize = new Vector3(50, 10, 50);
    [Range(33, 513)]
    public int heightmapResolution = 129;
    
    [Header("Height Settings")]
    [Range(0.1f, 1f)]
    public float heightScale = 0.3f;
    [Range(1, 5)]
    public int smoothIterations = 2;
    
    [Header("Color Painting")]
    public bool applyColors = true;
    [Range(0.5f, 3f)]
    public float blendDistance = 1.5f;
    
    [Header("Structure Placement")]
    public bool placeStructures = true;
    public List<StructureInfo> structureInfos = new List<StructureInfo>();
    public Transform structuresParent;
    [Range(0.1f, 2f)]
    public float globalStructureDensity = 1f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    private Terrain createdTerrain;
    
    void Start()
    {
        if (convertButton != null)
        {
            convertButton.onClick.AddListener(ConvertToTerrainWithStructures);
        }
        
        if (wfcGenerator == null)
        {
            wfcGenerator = FindObjectOfType<ProceduralMapGenerator>();
        }
    }
    
    [ContextMenu("Convert to Terrain with Structures")]
    public void ConvertToTerrainWithStructures()
    {
        if (wfcGenerator == null)
        {
            Debug.LogError("WFC Generator not found!");
            return;
        }
        
        Cell[,] grid = wfcGenerator.wfcAlgorithm.GetGrid();
        if (grid == null)
        {
            Debug.LogError("No WFC grid found! Generate a map first.");
            return;
        }
        
        Debug.Log($"Converting WFC grid {grid.GetLength(0)}x{grid.GetLength(1)} to terrain with structures...");
        
        float[,] heights = ExtractAndSmoothHeights(grid);
        
        CreateTerrainWithColors(heights, grid);
        
        if (placeStructures && createdTerrain != null)
        {
            PlaceStructuresOnTerrain(grid);
        }
        
        Debug.Log("Conversion complete!");
    }
    
    private float[,] ExtractAndSmoothHeights(Cell[,] grid)
    {
        float[,] heights = ExtractHeightsFromWFC(grid);
        
        for (int i = 0; i < smoothIterations; i++)
        {
            heights = SmoothHeightMap(heights);
        }
        
        return heights;
    }
    
    private void CreateTerrainWithColors(float[,] wfcHeights, Cell[,] grid)
    {
        int wfcWidth = wfcHeights.GetLength(0);
        int wfcHeight = wfcHeights.GetLength(1);
        
        TerrainData terrainData = new TerrainData();
        terrainData.size = terrainSize;
        terrainData.heightmapResolution = heightmapResolution;
        
        float[,] terrainHeights = new float[heightmapResolution, heightmapResolution];
        
        for (int y = 0; y < heightmapResolution; y++)
        {
            for (int x = 0; x < heightmapResolution; x++)
            {
                float wfcX = (float)x / (heightmapResolution - 1) * (wfcWidth - 1);
                float wfcY = (float)y / (heightmapResolution - 1) * (wfcHeight - 1);
                
                float interpolatedHeight = BilinearInterpolate(wfcHeights, wfcX, wfcY);
                terrainHeights[y, x] = interpolatedHeight * heightScale;
            }
        }
        
        terrainData.SetHeights(0, 0, terrainHeights);
        
        if (applyColors)
        {
            ApplyColorsToTerrain(terrainData, grid);
        }
        
        GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
        terrainObj.name = "WFC_Terrain_" + System.DateTime.Now.ToString("HHmmss");
        createdTerrain = terrainObj.GetComponent<Terrain>();
        
        Vector3 terrainPos = transform.position;
        terrainPos.x -= terrainSize.x * 0.5f;
        terrainPos.z -= terrainSize.z * 0.5f;
        terrainObj.transform.position = terrainPos;
        
        Debug.Log($"Terrain created: {terrainObj.name}");
    }
    
    private void PlaceStructuresOnTerrain(Cell[,] grid)
    {
        Debug.Log("Placing structures on terrain...");
        
        if (structuresParent == null)
        {
            GameObject parentObj = new GameObject("Terrain_Structures");
            structuresParent = parentObj.transform;
        }
        
        ClearExistingStructures();
        
        int totalPlaced = 0;
        int wfcWidth = grid.GetLength(0);
        int wfcHeight = grid.GetLength(1);
        List<Vector3> allPlacedPositions = new List<Vector3>();
        
        for (int x = 0; x < wfcWidth; x++)
        {
            for (int y = 0; y < wfcHeight; y++)
            {
                Vector2Int gridPos = new Vector2Int(x, y);
                Cell cell = grid[x, y];
                
                if (!cell.isCollapsed || cell.collapsedTile == null) continue;
                
                TileType tileType = cell.collapsedTile.tileType;
                
                var compatibleStructures = structureInfos.Where(s => s.CanPlaceOnTile(tileType)).ToList();
                
                foreach (var structureInfo in compatibleStructures)
                {
                    float spawnMultiplier = structureInfo.CalculateSpawnMultiplier(gridPos, grid);
                    float finalSpawnChance = structureInfo.spawnChance * globalStructureDensity * spawnMultiplier;
                    finalSpawnChance = Mathf.Clamp01(finalSpawnChance);
                    
                    int toPlace = 0;
                    for (int i = 0; i < structureInfo.maxPerTile; i++)
                    {
                        if (Random.Range(0f, 1f) < finalSpawnChance)
                        {
                            toPlace++;
                        }
                    }
                    
                    for (int i = 0; i < toPlace; i++)
                    {
                        Vector3 worldPos = GridToWorldPosition(gridPos, wfcWidth, wfcHeight);
                        Vector3 finalPos = AddPositionVariation(worldPos, structureInfo);
                        
                        if (!CheckSpacing(finalPos, allPlacedPositions, structureInfo)) continue;
                        
                        finalPos.y = createdTerrain.SampleHeight(finalPos);
                        finalPos += structureInfo.positionOffset;
                        
                        GameObject structure = CreateStructure(structureInfo, finalPos, gridPos);
                        if (structure != null)
                        {
                            totalPlaced++;
                            allPlacedPositions.Add(finalPos);
                        }
                    }
                }
            }
        }
        
        Debug.Log($"Placed {totalPlaced} structures on terrain");
    }
    
    private Vector3 GridToWorldPosition(Vector2Int gridPos, int wfcWidth, int wfcHeight)
    {
        float normalizedX = (float)gridPos.x / (wfcWidth - 1);
        float normalizedZ = (float)gridPos.y / (wfcHeight - 1);
        
        Vector3 terrainPos = createdTerrain.transform.position;
        Vector3 terrainSize = createdTerrain.terrainData.size;
        
        return new Vector3(
            terrainPos.x + normalizedX * terrainSize.x,
            0f,
            terrainPos.z + normalizedZ * terrainSize.z
        );
    }
    
    private Vector3 AddPositionVariation(Vector3 basePos, StructureInfo structureInfo)
    {
        Vector3 variation = new Vector3(
            Random.Range(-0.4f, 0.4f),
            0f,
            Random.Range(-0.4f, 0.4f)
        );
        
        return basePos + variation;
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
    
    private GameObject CreateStructure(StructureInfo structureInfo, Vector3 position, Vector2Int gridPos)
    {
        GameObject prefabToUse = structureInfo.prefab;
        if (structureInfo.variations.Count > 0)
        {
            var allPrefabs = new List<GameObject> { structureInfo.prefab };
            allPrefabs.AddRange(structureInfo.variations.Where(v => v != null));
            prefabToUse = allPrefabs[Random.Range(0, allPrefabs.Count)];
        }
        
        if (prefabToUse == null) return null;
        
        Quaternion rotation = Quaternion.identity;
        if (structureInfo.randomRotation)
        {
            rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }
        
        GameObject structure = Instantiate(prefabToUse, position, rotation, structuresParent);
        
        float scale = Random.Range(structureInfo.scaleMin, structureInfo.scaleMax);
        structure.transform.localScale = Vector3.one * scale;
        
        structure.name = $"{structureInfo.structureName}_{gridPos.x}_{gridPos.y}";
        
        return structure;
    }
    
    private float[,] ExtractHeightsFromWFC(Cell[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        float[,] heights = new float[width, height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float heightValue = 0f;
                
                if (grid[x, y].isCollapsed && grid[x, y].collapsedTile != null)
                {
                    TileData tile = grid[x, y].collapsedTile;
                    
                    if (tile.heightData != null)
                    {
                        heightValue = tile.heightData.baseHeight;
                        if (tile.heightData.heightVariation > 0)
                        {
                            heightValue += Random.Range(-tile.heightData.heightVariation, tile.heightData.heightVariation);
                        }
                    }
                    else
                    {
                        heightValue = tile.baseWeight * 0.1f;
                    }
                }
                
                heights[x, y] = heightValue;
            }
        }
        
        return heights;
    }
    
    private float[,] SmoothHeightMap(float[,] heights)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        float[,] smoothed = new float[width, height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float sum = 0f;
                int count = 0;
                
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            sum += heights[nx, ny];
                            count++;
                        }
                    }
                }
                
                smoothed[x, y] = sum / count;
            }
        }
        
        return smoothed;
    }
    
    private float BilinearInterpolate(float[,] heightMap, float x, float y)
    {
        int x0 = Mathf.FloorToInt(x);
        int x1 = Mathf.Min(x0 + 1, heightMap.GetLength(0) - 1);
        int y0 = Mathf.FloorToInt(y);
        int y1 = Mathf.Min(y0 + 1, heightMap.GetLength(1) - 1);
        
        float fx = x - x0;
        float fy = y - y0;
        
        float h00 = heightMap[x0, y0];
        float h10 = heightMap[x1, y0];
        float h01 = heightMap[x0, y1];
        float h11 = heightMap[x1, y1];
        
        float h0 = Mathf.Lerp(h00, h10, fx);
        float h1 = Mathf.Lerp(h01, h11, fx);
        
        return Mathf.Lerp(h0, h1, fy);
    }
    
    private void ApplyColorsToTerrain(TerrainData terrainData, Cell[,] grid)
    {
        int wfcWidth = grid.GetLength(0);
        int wfcHeight = grid.GetLength(1);
        
        Debug.Log("Extracting colors from WFC tiles...");
        
        List<Color> uniqueColors = new List<Color>();
        Color[,] gridColors = new Color[wfcWidth, wfcHeight];
        
        for (int x = 0; x < wfcWidth; x++)
        {
            for (int y = 0; y < wfcHeight; y++)
            {
                Color tileColor = Color.gray;  
                
                if (grid[x, y].isCollapsed && grid[x, y].collapsedTile != null)
                {
                    tileColor = grid[x, y].collapsedTile.gizmoColor;
                }
                
                gridColors[x, y] = tileColor;
                
                bool colorExists = false;
                foreach (Color existingColor in uniqueColors)
                {
                    if (ColorsAreSimilar(existingColor, tileColor, 0.1f))
                    {
                        colorExists = true;
                        break;
                    }
                }
                
                if (!colorExists)
                {
                    uniqueColors.Add(tileColor);
                }
            }
        }
        
        Debug.Log($"Found {uniqueColors.Count} unique colors in WFC grid");
        
        TerrainLayer[] terrainLayers = new TerrainLayer[uniqueColors.Count];
        
        for (int i = 0; i < uniqueColors.Count; i++)
        {
            TerrainLayer layer = new TerrainLayer();
            layer.diffuseTexture = CreateSolidColorTexture(uniqueColors[i]);
            layer.tileSize = Vector2.one * 10f;
            layer.tileOffset = Vector2.zero;
            
            terrainLayers[i] = layer;
        }
        
        terrainData.terrainLayers = terrainLayers;
        
        int mapResolution = terrainData.alphamapResolution;
        float[,,] splatMaps = new float[mapResolution, mapResolution, uniqueColors.Count];
        
        for (int y = 0; y < mapResolution; y++)
        {
            for (int x = 0; x < mapResolution; x++)
            {
                float worldX = (float)x / (mapResolution - 1) * (wfcWidth - 1);
                float worldY = (float)y / (mapResolution - 1) * (wfcHeight - 1);
                
                Color blendedColor = CalculateBlendedColorAt(worldX, worldY, gridColors, wfcWidth, wfcHeight);
                
                float[] weights = new float[uniqueColors.Count];
                float totalWeight = 0f;
                
                for (int colorIndex = 0; colorIndex < uniqueColors.Count; colorIndex++)
                {
                    float similarity = GetColorSimilarity(blendedColor, uniqueColors[colorIndex]);
                    weights[colorIndex] = similarity;
                    totalWeight += similarity;
                }
                
                if (totalWeight > 0)
                {
                    for (int colorIndex = 0; colorIndex < uniqueColors.Count; colorIndex++)
                    {
                        splatMaps[y, x, colorIndex] = weights[colorIndex] / totalWeight;
                    }
                }
                else
                {
                    splatMaps[y, x, 0] = 1f;
                }
            }
        }
        
        terrainData.SetAlphamaps(0, 0, splatMaps);
        
        Debug.Log($"Applied {uniqueColors.Count} colors to terrain with blending");
    }
    private Texture2D CreateSolidColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGB24, false);
    
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
    
        Color[] pixels = new Color[32 * 32];
    
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
    
        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = $"SolidColor_{color.r:F2}_{color.g:F2}_{color.b:F2}";
    
        return texture;
    }
    private Color CalculateBlendedColorAt(float worldX, float worldY, Color[,] gridColors, int width, int height)
    {
        Color blendedColor = Color.black;
        float totalWeight = 0f;
        
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int sampleX = Mathf.Clamp(Mathf.RoundToInt(worldX + dx), 0, width - 1);
                int sampleY = Mathf.Clamp(Mathf.RoundToInt(worldY + dy), 0, height - 1);
                
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float weight = Mathf.Exp(-distance / blendDistance);
                
                Color sampleColor = gridColors[sampleX, sampleY];
                blendedColor += sampleColor * weight;
                totalWeight += weight;
            }
        }
        
        if (totalWeight > 0)
        {
            blendedColor /= totalWeight;
        }
        else
        {
            int nearestX = Mathf.Clamp(Mathf.RoundToInt(worldX), 0, width - 1);
            int nearestY = Mathf.Clamp(Mathf.RoundToInt(worldY), 0, height - 1);
            blendedColor = gridColors[nearestX, nearestY];
        }
        
        return blendedColor;
    }
    
    private Texture2D CreateSolidColorTexture_(Color color)
    {
        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGB24, false);
        Color[] pixels = new Color[16];
        
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = $"Color_{color.r:F2}_{color.g:F2}_{color.b:F2}";
        
        return texture;
    }
    
    private bool ColorsAreSimilar(Color a, Color b, float threshold)
    {
        float distance = Mathf.Sqrt(
            Mathf.Pow(a.r - b.r, 2) + 
            Mathf.Pow(a.g - b.g, 2) + 
            Mathf.Pow(a.b - b.b, 2)
        );
        return distance < threshold;
    }
    
    private float GetColorSimilarity(Color a, Color b)
    {
        float distance = Mathf.Sqrt(
            Mathf.Pow(a.r - b.r, 2) + 
            Mathf.Pow(a.g - b.g, 2) + 
            Mathf.Pow(a.b - b.b, 2)
        );
        return Mathf.Exp(-distance * 3f); 
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
    }
    
    [ContextMenu("Clear All")]
    public void ClearAll()
    {
        ClearExistingStructures();
        
        if (createdTerrain != null)
        {
            if (Application.isPlaying)
                Destroy(createdTerrain.gameObject);
            else
                DestroyImmediate(createdTerrain.gameObject);
            
            createdTerrain = null;
        }
        
        Debug.Log("Cleared terrain and structures");
    }
    
    
[ContextMenu("Save Current Terrain")]
public void SaveTerrain()
{
#if UNITY_EDITOR
    if (createdTerrain == null)
    {
        Debug.LogError("No terrain to save! Create a terrain first.");
        return;
    }
    
    TerrainData terrainData = createdTerrain.terrainData;
    if (terrainData == null)
    {
        Debug.LogError("Terrain has no TerrainData!");
        return;
    }
    
    string terrainFolder = "Assets/Generated_Terrains";
    if (!AssetDatabase.IsValidFolder(terrainFolder))
    {
        AssetDatabase.CreateFolder("Assets", "Generated_Terrains");
    }
    
    string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
    string terrainDataPath = $"{terrainFolder}/WFC_TerrainData_{timestamp}.asset";
    string prefabPath = $"{terrainFolder}/WFC_Terrain_{timestamp}.prefab";
    
    try
    {
        AssetDatabase.CreateAsset(terrainData, terrainDataPath);
        
        if (terrainData.terrainLayers != null && terrainData.terrainLayers.Length > 0)
        {
            for (int i = 0; i < terrainData.terrainLayers.Length; i++)
            {
                TerrainLayer layer = terrainData.terrainLayers[i];
                if (layer != null && AssetDatabase.GetAssetPath(layer) == "")
                {
                    if (layer.diffuseTexture != null && AssetDatabase.GetAssetPath(layer.diffuseTexture) == "")
                    {
                        Texture2D originalTexture = layer.diffuseTexture;
                        Texture2D saveableTexture = new Texture2D(originalTexture.width, originalTexture.height, TextureFormat.RGB24, false);
                        
                        Color[] pixels = originalTexture.GetPixels();
                        saveableTexture.SetPixels(pixels);
                        saveableTexture.Apply();
                        
                        byte[] pngData = saveableTexture.EncodeToPNG();
                        string texturePath = $"{terrainFolder}/TerrainTexture_{timestamp}_{i}.png";
                        System.IO.File.WriteAllBytes(texturePath, pngData);
                        
                        AssetDatabase.ImportAsset(texturePath);
                        Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        layer.diffuseTexture = importedTexture;
                        
                        if (Application.isPlaying)
                            Destroy(saveableTexture);
                        else
                            DestroyImmediate(saveableTexture);
                    }
                    
                    string layerPath = $"{terrainFolder}/TerrainLayer_{timestamp}_{i}.asset";
                    AssetDatabase.CreateAsset(layer, layerPath);
                }
            }
        }
        
        GameObject terrainPrefab = PrefabUtility.SaveAsPrefabAsset(createdTerrain.gameObject, prefabPath);
        
        if (structuresParent != null && structuresParent.childCount > 0)
        {
            string structuresPrefabPath = $"{terrainFolder}/WFC_TerrainStructures_{timestamp}.prefab";
            PrefabUtility.SaveAsPrefabAsset(structuresParent.gameObject, structuresPrefabPath);
            Debug.Log($"Structures saved as prefab: {structuresPrefabPath}");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath));
        
        Debug.Log($"Terrain successfully saved!");
        Debug.Log($"TerrainData: {terrainDataPath}");
        Debug.Log($"Terrain Prefab: {prefabPath}");
        
        if (showDebugInfo)
        {
            Debug.Log($"Terrain Size: {terrainData.size}");
            Debug.Log($"Heightmap Resolution: {terrainData.heightmapResolution}");
            Debug.Log($"Terrain Layers: {(terrainData.terrainLayers?.Length ?? 0)}");
            if (structuresParent != null)
            {
                Debug.Log($"Structures: {structuresParent.childCount}");
            }
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Failed to save terrain: {e.Message}");
    }
    
#else
    Debug.LogWarning("SaveTerrain only works in the Unity Editor!");
#endif
}

}