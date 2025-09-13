using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;



public class EachEcosystemGenerator : MonoBehaviour
{
    [Header("2D 설정")]
    [Tooltip("2D 스프라이트를 사용할 경우 체크")]
    public bool use2DSprites = false;
    [Tooltip("타일 간격 (기본값 1.0)")]
    public float tileSpacing = 1.0f;
    
    [Header("맵 설정")]
    public int mapWidth = 100;
    public int mapHeight = 100;
    public bool useBSP = true; // true: BSP 사용, false: WFC 사용
    
    [Header("BSP 설정")]
    public int minRoomSize = 8;
    public int maxRoomSize = 25;
    public int maxDepth = 6;
    
    [Header("WFC 설정")]
    public int maxIterations = 1000;
    public int seed = 42;
    
    [Header("시각화")]
    public bool showGizmos = true;
    public GameObject tilePrefab;
    
    private EcosystemTile[,] ecosystem;
    private Dictionary<EcosystemTileType, EcosystemTile> tileDatabase;
    private List<WFCRule> wfcRules;
    private BSPNode rootNode;
    
    void Start()
    {
        InitializeTileDatabase();
        InitializeWFCRules();
        
        GenerateEcosystem();
        
        SetupCamera();
        
        PrintEcosystemStats();
    }
    
    void SetupCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 mapCenter = new Vector3(
                (mapWidth - 1) * tileSpacing / 2f, 
                (mapHeight - 1) * tileSpacing / 2f, 
                0
            );
            float cameraDistance = Mathf.Max(mapWidth, mapHeight) * tileSpacing * 0.8f;
            
            mainCam.transform.position = mapCenter + Vector3.back * cameraDistance;
            mainCam.transform.LookAt(mapCenter);
            
            mainCam.orthographic = true;
            mainCam.orthographicSize = Mathf.Max(mapWidth, mapHeight) * tileSpacing * 0.6f;
            
        }
    }
    
    void PrintEcosystemStats()
    {
        var stats = GetEcosystemStats();
        
        foreach (var stat in stats)
        {
            float percentage = (stat.Value / (float)(mapWidth * mapHeight)) * 100f;
            Debug.Log($"{stat.Key}: {stat.Value}개 ({percentage:F1}%)");
        }
    }
    
    void InitializeTileDatabase()
    {
        tileDatabase = new Dictionary<EcosystemTileType, EcosystemTile>
        {
            { EcosystemTileType.Empty, new EcosystemTile(EcosystemTileType.Empty, 0f, 0f, 0f, Color.black) },
            { EcosystemTileType.Grass, new EcosystemTile(EcosystemTileType.Grass, 0.8f, 0.6f, 20f, Color.green) },
            { EcosystemTileType.Forest, new EcosystemTile(EcosystemTileType.Forest, 0.9f, 0.8f, 15f, new Color(0, 0.5f, 0)) },
            { EcosystemTileType.Water, new EcosystemTile(EcosystemTileType.Water, 0.3f, 1f, 18f, Color.blue) },
            { EcosystemTileType.Mountain, new EcosystemTile(EcosystemTileType.Mountain, 0.2f, 0.3f, 5f, Color.gray) },
            { EcosystemTileType.Desert, new EcosystemTile(EcosystemTileType.Desert, 0.1f, 0.1f, 35f, Color.yellow) },
            { EcosystemTileType.Snow, new EcosystemTile(EcosystemTileType.Snow, 0.1f, 0.9f, -10f, Color.white) }
        };
    }
    
    void InitializeWFCRules()
    {
        wfcRules = new List<WFCRule>
        {
            new WFCRule(EcosystemTileType.Grass, 
                new List<EcosystemTileType> { EcosystemTileType.Grass, EcosystemTileType.Forest, EcosystemTileType.Water }, 
                0.4f),
            
            new WFCRule(EcosystemTileType.Forest, 
                new List<EcosystemTileType> { EcosystemTileType.Forest, EcosystemTileType.Grass, EcosystemTileType.Water }, 
                0.3f),
            
            
            new WFCRule(EcosystemTileType.Water, 
                new List<EcosystemTileType> { EcosystemTileType.Water, EcosystemTileType.Grass, EcosystemTileType.Forest }, 
                0.15f),
            
            new WFCRule(EcosystemTileType.Mountain, 
                new List<EcosystemTileType> { EcosystemTileType.Mountain, EcosystemTileType.Snow, EcosystemTileType.Grass }, 
                0.1f),
            
            new WFCRule(EcosystemTileType.Desert, 
                new List<EcosystemTileType> { EcosystemTileType.Desert, EcosystemTileType.Grass }, 
                0.08f),
            
            new WFCRule(EcosystemTileType.Snow, 
                new List<EcosystemTileType> { EcosystemTileType.Snow, EcosystemTileType.Mountain }, 
                0.05f)
        };
    }
    
    public void GenerateEcosystem()
    {
        ecosystem = new EcosystemTile[mapWidth, mapHeight];
        UnityEngine.Random.InitState(seed);
        
        if (useBSP)
        {
            GenerateBSPEcosystem();
        }
        else
        {
            GenerateWFCEcosystem();
        }
        
        if (tilePrefab != null)
        {
            CreateVisualTiles();
        }
    }
    
    #region BSP 생성 왁
    void GenerateBSPEcosystem()
    {
        rootNode = new BSPNode(new Rect(0, 0, mapWidth, mapHeight));
        SplitBSPNode(rootNode, 0);
        AssignBiomesToBSPNodes(rootNode);
        FillEcosystemFromBSP(rootNode);
        ApplyEcosystemTransitions();
    }
    
    void SplitBSPNode(BSPNode node, int depth)
    {
        if (depth >= maxDepth || 
            node.rect.width < minRoomSize * 2 || 
            node.rect.height < minRoomSize * 2)
        {
            return;
        }
        
        bool splitHorizontally = UnityEngine.Random.Range(0f, 1f) > 0.5f;
        
        if (node.rect.width / node.rect.height >= 1.25f)
        {
            splitHorizontally = false; 
        }
        else if (node.rect.height / node.rect.width >= 1.25f)
        {
            splitHorizontally = true;
        }
        
        if (splitHorizontally)
        {
            int splitY = UnityEngine.Random.Range(
                Mathf.RoundToInt(node.rect.y + minRoomSize),
                Mathf.RoundToInt(node.rect.y + node.rect.height - minRoomSize)
            );
            
            node.leftChild = new BSPNode(new Rect(node.rect.x, node.rect.y, node.rect.width, splitY - node.rect.y));
            node.rightChild = new BSPNode(new Rect(node.rect.x, splitY, node.rect.width, node.rect.y + node.rect.height - splitY));
        }
        else
        {
            int splitX = UnityEngine.Random.Range(
                Mathf.RoundToInt(node.rect.x + minRoomSize),
                Mathf.RoundToInt(node.rect.x + node.rect.width - minRoomSize)
            );
            
            node.leftChild = new BSPNode(new Rect(node.rect.x, node.rect.y, splitX - node.rect.x, node.rect.height));
            node.rightChild = new BSPNode(new Rect(splitX, node.rect.y, node.rect.x + node.rect.width - splitX, node.rect.height));
        }
        
        SplitBSPNode(node.leftChild, depth + 1);
        SplitBSPNode(node.rightChild, depth + 1);
    }
    
    void AssignBiomesToBSPNodes(BSPNode node)
    {
        if (node.IsLeaf())
        {
            float centerX = node.rect.center.x / mapWidth;
            float centerY = node.rect.center.y / mapHeight;
            
            if (centerY > 0.8f)
            {
                node.biomeType = EcosystemTileType.Snow; 
            }
            else if (centerY < 0.2f)
            {
                node.biomeType = EcosystemTileType.Desert; 
            }
            else if (centerX < 0.3f && centerY > 0.3f && centerY < 0.7f)
            {
                node.biomeType = EcosystemTileType.Water; 
            }
            else if (centerX > 0.7f)
            {
                node.biomeType = EcosystemTileType.Mountain;
            }
            else if (UnityEngine.Random.Range(0f, 1f) > 0.5f)
            {
                node.biomeType = EcosystemTileType.Forest;
            }
            else
            {
                node.biomeType = EcosystemTileType.Grass;
            }
        }
        else
        {
            AssignBiomesToBSPNodes(node.leftChild);
            AssignBiomesToBSPNodes(node.rightChild);
        }
    }
    
    void FillEcosystemFromBSP(BSPNode node)
    {
        if (node.IsLeaf())
        {
            for (int x = Mathf.RoundToInt(node.rect.x); x < Mathf.RoundToInt(node.rect.x + node.rect.width); x++)
            {
                for (int y = Mathf.RoundToInt(node.rect.y); y < Mathf.RoundToInt(node.rect.y + node.rect.height); y++)
                {
                    if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
                    {
                        ecosystem[x, y] = new EcosystemTile(
                            node.biomeType,
                            tileDatabase[node.biomeType].fertility,
                            tileDatabase[node.biomeType].moisture,
                            tileDatabase[node.biomeType].temperature,
                            tileDatabase[node.biomeType].color
                        );
                    }
                }
            }
        }
        else
        {
            FillEcosystemFromBSP(node.leftChild);
            FillEcosystemFromBSP(node.rightChild);
        }
    }
    #endregion
    
    #region WFC 생성
    void GenerateWFCEcosystem()
    {
        bool[,] collapsed = new bool[mapWidth, mapHeight];
        List<EcosystemTileType>[,] possibilities = new List<EcosystemTileType>[mapWidth, mapHeight];
        
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                possibilities[x, y] = new List<EcosystemTileType>(System.Enum.GetValues(typeof(EcosystemTileType)).Cast<EcosystemTileType>());
                possibilities[x, y].Remove(EcosystemTileType.Empty);
            }
        }
        
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            Vector2Int cellToCollapse = FindLowestEntropyCell(collapsed, possibilities);
            
            if (cellToCollapse.x == -1) break; 
            
            CollapseCell(cellToCollapse.x, cellToCollapse.y, collapsed, possibilities);
            
            PropagateConstraints(cellToCollapse.x, cellToCollapse.y, collapsed, possibilities);
        }
        
        FillEcosystemFromWFC(collapsed, possibilities);
    }
    
    Vector2Int FindLowestEntropyCell(bool[,] collapsed, List<EcosystemTileType>[,] possibilities)
    {
        int minEntropy = int.MaxValue;
        List<Vector2Int> candidates = new List<Vector2Int>();
        
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (!collapsed[x, y] && possibilities[x, y].Count > 0)
                {
                    if (possibilities[x, y].Count < minEntropy)
                    {
                        minEntropy = possibilities[x, y].Count;
                        candidates.Clear();
                        candidates.Add(new Vector2Int(x, y));
                    }
                    else if (possibilities[x, y].Count == minEntropy)
                    {
                        candidates.Add(new Vector2Int(x, y));
                    }
                }
            }
        }
        
        if (candidates.Count == 0) return new Vector2Int(-1, -1);
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }
    
    void CollapseCell(int x, int y, bool[,] collapsed, List<EcosystemTileType>[,] possibilities)
    {
        if (possibilities[x, y].Count == 0) return;
        
        float totalWeight = 0f;
        foreach (var tileType in possibilities[x, y])
        {
            var rule = wfcRules.FirstOrDefault(r => r.centerType == tileType);
            totalWeight += rule?.weight ?? 0.1f;
        }
        
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        EcosystemTileType selectedType = possibilities[x, y][0];
        
        foreach (var tileType in possibilities[x, y])
        {
            var rule = wfcRules.FirstOrDefault(r => r.centerType == tileType);
            currentWeight += rule?.weight ?? 0.1f;
            if (randomValue <= currentWeight)
            {
                selectedType = tileType;
                break;
            }
        }
        
        possibilities[x, y].Clear();
        possibilities[x, y].Add(selectedType);
        collapsed[x, y] = true;
    }
    
    void PropagateConstraints(int x, int y, bool[,] collapsed, List<EcosystemTileType>[,] possibilities)
    {
        Queue<Vector2Int> propagationQueue = new Queue<Vector2Int>();
        propagationQueue.Enqueue(new Vector2Int(x, y));
        
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        while (propagationQueue.Count > 0)
        {
            Vector2Int current = propagationQueue.Dequeue();
            
            if (!collapsed[current.x, current.y] || possibilities[current.x, current.y].Count == 0) continue;
            
            EcosystemTileType currentType = possibilities[current.x, current.y][0];
            var currentRule = wfcRules.FirstOrDefault(r => r.centerType == currentType);
            
            if (currentRule == null) continue;
            
            foreach (var dir in directions)
            {
                int nx = current.x + dir.x;
                int ny = current.y + dir.y;
                
                if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight && !collapsed[nx, ny])
                {
                    bool changed = false;
                    for (int i = possibilities[nx, ny].Count - 1; i >= 0; i--)
                    {
                        if (!currentRule.allowedNeighbors.Contains(possibilities[nx, ny][i]))
                        {
                            possibilities[nx, ny].RemoveAt(i);
                            changed = true;
                        }
                    }
                    
                    if (changed && possibilities[nx, ny].Count > 0)
                    {
                        propagationQueue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }
    }
    
    void FillEcosystemFromWFC(bool[,] collapsed, List<EcosystemTileType>[,] possibilities)
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                EcosystemTileType tileType = EcosystemTileType.Grass; 
                
                if (possibilities[x, y].Count > 0)
                {
                    tileType = possibilities[x, y][0];
                }
                
                ecosystem[x, y] = new EcosystemTile(
                    tileType,
                    tileDatabase[tileType].fertility,
                    tileDatabase[tileType].moisture,
                    tileDatabase[tileType].temperature,
                    tileDatabase[tileType].color
                );
            }
        }
    }
    #endregion
    
    void ApplyEcosystemTransitions()
    {
        EcosystemTile[,] newEcosystem = new EcosystemTile[mapWidth, mapHeight];
        
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                newEcosystem[x, y] = ecosystem[x, y];
                
                if (IsNearBoundary(x, y))
                {
                    var neighborTypes = GetNeighborTypes(x, y);
                    if (neighborTypes.Count > 1)
                    {
                        float avgFertility = neighborTypes.Average(t => tileDatabase[t].fertility);
                        float avgMoisture = neighborTypes.Average(t => tileDatabase[t].moisture);
                        float avgTemperature = neighborTypes.Average(t => tileDatabase[t].temperature);
                        
                        newEcosystem[x, y].fertility = avgFertility;
                        newEcosystem[x, y].moisture = avgMoisture;
                        newEcosystem[x, y].temperature = avgTemperature;
                        
                        Color avgColor = Color.black;
                        foreach (var type in neighborTypes)
                        {
                            avgColor += tileDatabase[type].color;
                        }
                        avgColor /= neighborTypes.Count;
                        newEcosystem[x, y].color = avgColor;
                    }
                }
            }
        }
        
        ecosystem = newEcosystem;
    }
    
    bool IsNearBoundary(int x, int y)
    {
        var currentType = ecosystem[x, y].type;
        
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                
                int nx = x + dx;
                int ny = y + dy;
                
                if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                {
                    if (ecosystem[nx, ny].type != currentType)
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    List<EcosystemTileType> GetNeighborTypes(int x, int y)
    {
        HashSet<EcosystemTileType> types = new HashSet<EcosystemTileType>();
        
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                
                if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                {
                    types.Add(ecosystem[nx, ny].type);
                }
            }
        }
        
        return types.ToList();
    }
    
    void CreateVisualTiles()
    {
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }
        
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                GameObject tile = Instantiate(tilePrefab, transform);
                tile.transform.position = new Vector3(x * tileSpacing, y * tileSpacing, 0);
                tile.transform.localScale = Vector3.one * tileSpacing;
                tile.name = $"Tile_{x}_{y}_{ecosystem[x, y].type}";
                
                Renderer renderer = tile.GetComponent<Renderer>();
                if (renderer != null && !use2DSprites)
                {
                    Material tileMaterial = new Material(renderer.material);
                    tileMaterial.color = ecosystem[x, y].color;
                    renderer.material = tileMaterial;
                }
                
                SpriteRenderer spriteRenderer = tile.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && use2DSprites)
                {
                    spriteRenderer.color = ecosystem[x, y].color;
                }
                
                TileInfo tileInfo = tile.GetComponent<TileInfo>();
                if (tileInfo == null)
                {
                    tileInfo = tile.AddComponent<TileInfo>();
                }
                tileInfo.SetTileData(ecosystem[x, y], x, y);
            }
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showGizmos || ecosystem == null) return;
        
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (ecosystem[x, y] != null)
                {
                    Vector3 tilePos = new Vector3(x * tileSpacing, y * tileSpacing, 0);
                    Vector3 tileSize = Vector3.one * (tileSpacing * 0.9f);
                    
                    Gizmos.color = ecosystem[x, y].color;
                    Gizmos.DrawCube(tilePos, tileSize);
                    
                    if (Application.isPlaying)
                    {
                        Gizmos.color = Color.white;
                        Gizmos.DrawWireCube(tilePos, Vector3.one * tileSpacing);
                    }
                }
            }
        }
    }
    

    
    public EcosystemTile GetTile(int x, int y)
    {
        if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
        {
            return ecosystem[x, y];
        }
        return null;
    }
    
    public Dictionary<EcosystemTileType, int> GetEcosystemStats()
    {
        var stats = new Dictionary<EcosystemTileType, int>();
        
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                var type = ecosystem[x, y].type;
                if (stats.ContainsKey(type))
                    stats[type]++;
                else
                    stats[type] = 1;
            }
        }
        
        return stats;
    }
}