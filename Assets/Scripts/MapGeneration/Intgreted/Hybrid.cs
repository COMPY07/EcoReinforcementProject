using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class HybridEcosystemGenerator : MonoBehaviour
{
    [Header("전체 설정")] 
    public bool use2DSprites = false;

    public float tileSpacing = 1.0f;

    [Header("맵 설정")] public int mapWidth = 100;
    public int mapHeight = 100;

    [Header("Hybrid Setting")]
    [Range(0f, 1f)]
    public float bspInfluence = 0.7f;
    public bool enforceRegionalBiomes = true;
    public bool enableBoundaryBlending = true;
    [Range(1, 10)] public int blendingRadius = 3;

    [Header("BSP")] public int minRegionSize = 15;
    public int maxRegionSize = 35;
    public int maxDepth = 5;

    [Header("WFC")] public int maxWFCIterations = 500;
    public int seed = 42;

    [Range(0.1f, 2.0f)]
    public float wfcDetailLevel = 1.0f;

    [Header("지역별 바이옴 설정")] public HybridBiomeRegionSettings[] biomeRegions = new HybridBiomeRegionSettings[]
    {
        new HybridBiomeRegionSettings(HybridRegionType.Northern,
            new EcosystemTileType[] { EcosystemTileType.Snow, EcosystemTileType.Mountain, EcosystemTileType.Forest }),
        new HybridBiomeRegionSettings(HybridRegionType.Southern,
            new EcosystemTileType[] { EcosystemTileType.Desert, EcosystemTileType.Grass }),
        new HybridBiomeRegionSettings(HybridRegionType.Western,
            new EcosystemTileType[] { EcosystemTileType.Water, EcosystemTileType.Forest, EcosystemTileType.Grass }),
        new HybridBiomeRegionSettings(HybridRegionType.Eastern,
            new EcosystemTileType[] { EcosystemTileType.Mountain, EcosystemTileType.Forest }),
        new HybridBiomeRegionSettings(HybridRegionType.Central,
            new EcosystemTileType[] { EcosystemTileType.Grass, EcosystemTileType.Forest, EcosystemTileType.Water })
    };

    [Header("시각화")]
    public bool showGizmos = true;
    public bool showRegionBoundaries = true;
    public GameObject tilePrefab;

    private EcosystemTile[,] ecosystem;
    private HybridRegionData[,] regionMap;
    private Dictionary<EcosystemTileType, EcosystemTile> tileDatabase;
    private List<HybridWFCRule> wfcRules;
    private List<HybridWFCRule> regionalWFCRules;
    private HybridBSPNode rootNode;
    private List<HybridRegionInfo> regions;

    List<EcosystemTileType> GetCompatibleBiomes(EcosystemTileType dominantBiome)
    {
        var compatibilityMap = new Dictionary<EcosystemTileType, List<EcosystemTileType>>
        {
            {
                EcosystemTileType.Grass,
                new List<EcosystemTileType> { EcosystemTileType.Forest, EcosystemTileType.Water }
            },
            {
                EcosystemTileType.Forest,
                new List<EcosystemTileType>
                    { EcosystemTileType.Grass, EcosystemTileType.Mountain, EcosystemTileType.Water }
            },
            {
                EcosystemTileType.Water,
                new List<EcosystemTileType> { EcosystemTileType.Grass, EcosystemTileType.Forest }
            },
            {
                EcosystemTileType.Mountain,
                new List<EcosystemTileType>
                    { EcosystemTileType.Snow, EcosystemTileType.Forest, EcosystemTileType.Grass }
            },
            { EcosystemTileType.Desert, new List<EcosystemTileType> { EcosystemTileType.Grass } },
            { EcosystemTileType.Snow, new List<EcosystemTileType> { EcosystemTileType.Mountain } }
        };

        return compatibilityMap.ContainsKey(dominantBiome)
            ? compatibilityMap[dominantBiome]
            : new List<EcosystemTileType>();
    }

    void Start()
    {
        InitializeTileDatabase();
        InitializeWFCRules();
        GenerateHybridEcosystem();
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

    void InitializeTileDatabase()
    {
        tileDatabase = new Dictionary<EcosystemTileType, EcosystemTile>
        {
            { EcosystemTileType.Empty, new EcosystemTile(EcosystemTileType.Empty, 0f, 0f, 0f, Color.black) },
            { EcosystemTileType.Grass, new EcosystemTile(EcosystemTileType.Grass, 0.8f, 0.6f, 20f, Color.green) },
            {
                EcosystemTileType.Forest,
                new EcosystemTile(EcosystemTileType.Forest, 0.9f, 0.8f, 15f, new Color(0, 0.5f, 0))
            },
            { EcosystemTileType.Water, new EcosystemTile(EcosystemTileType.Water, 0.3f, 1f, 18f, Color.blue) },
            { EcosystemTileType.Mountain, new EcosystemTile(EcosystemTileType.Mountain, 0.2f, 0.3f, 5f, Color.gray) },
            { EcosystemTileType.Desert, new EcosystemTile(EcosystemTileType.Desert, 0.1f, 0.1f, 35f, Color.yellow) },
            { EcosystemTileType.Snow, new EcosystemTile(EcosystemTileType.Snow, 0.1f, 0.9f, -10f, Color.white) }
        };
    }

    void InitializeWFCRules()
    {
        wfcRules = new List<HybridWFCRule>
        {
            new HybridWFCRule(EcosystemTileType.Grass,
                new List<EcosystemTileType>
                    { EcosystemTileType.Grass, EcosystemTileType.Forest, EcosystemTileType.Water },
                0.4f),
            new HybridWFCRule(EcosystemTileType.Forest,
                new List<EcosystemTileType>
                    { EcosystemTileType.Forest, EcosystemTileType.Grass, EcosystemTileType.Water },
                0.3f),
            new HybridWFCRule(EcosystemTileType.Water,
                new List<EcosystemTileType>
                    { EcosystemTileType.Water, EcosystemTileType.Grass, EcosystemTileType.Forest },
                0.15f),
            new HybridWFCRule(EcosystemTileType.Mountain,
                new List<EcosystemTileType>
                    { EcosystemTileType.Mountain, EcosystemTileType.Snow, EcosystemTileType.Grass },
                0.1f),
            new HybridWFCRule(EcosystemTileType.Desert,
                new List<EcosystemTileType> { EcosystemTileType.Desert, EcosystemTileType.Grass },
                0.08f),
            new HybridWFCRule(EcosystemTileType.Snow,
                new List<EcosystemTileType> { EcosystemTileType.Snow, EcosystemTileType.Mountain },
                0.05f)
        };
    }

    public void GenerateHybridEcosystem()
    {
        ecosystem = new EcosystemTile[mapWidth, mapHeight];
        regionMap = new HybridRegionData[mapWidth, mapHeight];
        regions = new List<HybridRegionInfo>();
        UnityEngine.Random.InitState(seed);

        CreateBSPRegions();

        AssignRegionalCharacteristics();

        GenerateRegionalWFC();

        if (enableBoundaryBlending)
        {
            ApplyBoundaryBlending();
        }

        ApplyFinalPostProcessing();

        if (tilePrefab != null)
        {
            CreateVisualTiles();
        }
    }

    #region BSP 구역 생성

    void CreateBSPRegions()
    {
        rootNode = new HybridBSPNode(new Rect(0, 0, mapWidth, mapHeight));
        SplitBSPNode(rootNode, 0);
        CollectRegions(rootNode);

    }

    void SplitBSPNode(HybridBSPNode node, int depth)
    {
        if (depth >= maxDepth ||
            node.rect.width < minRegionSize * 2 ||
            node.rect.height < minRegionSize * 2)
        {
            return;
        }

        bool splitHorizontally = UnityEngine.Random.Range(0f, 1f) > 0.5f;

        if (node.rect.width / node.rect.height >= 1.5f)
        {
            splitHorizontally = false;
        }
        else if (node.rect.height / node.rect.width >= 1.5f)
        {
            splitHorizontally = true;
        }

        if (splitHorizontally)
        {
            int splitY = UnityEngine.Random.Range(
                Mathf.RoundToInt(node.rect.y + minRegionSize),
                Mathf.RoundToInt(node.rect.y + node.rect.height - minRegionSize)
            );

            node.leftChild =
                new HybridBSPNode(new Rect(node.rect.x, node.rect.y, node.rect.width, splitY - node.rect.y));
            node.rightChild = new HybridBSPNode(new Rect(node.rect.x, splitY, node.rect.width,
                node.rect.y + node.rect.height - splitY));
        }
        else
        {
            int splitX = UnityEngine.Random.Range(
                Mathf.RoundToInt(node.rect.x + minRegionSize),
                Mathf.RoundToInt(node.rect.x + node.rect.width - minRegionSize)
            );

            node.leftChild =
                new HybridBSPNode(new Rect(node.rect.x, node.rect.y, splitX - node.rect.x, node.rect.height));
            node.rightChild = new HybridBSPNode(new Rect(splitX, node.rect.y, node.rect.x + node.rect.width - splitX,
                node.rect.height));
        }

        SplitBSPNode(node.leftChild, depth + 1);
        SplitBSPNode(node.rightChild, depth + 1);
    }

    void CollectRegions(HybridBSPNode node)
    {
        if (node.IsLeaf())
        {
            var region = new HybridRegionInfo
            {
                bounds = node.rect,
                regionType = DetermineRegionType(node.rect),
                id = regions.Count
            };
            regions.Add(region);

            for (int x = Mathf.RoundToInt(node.rect.x); x < Mathf.RoundToInt(node.rect.x + node.rect.width); x++)
            {
                for (int y = Mathf.RoundToInt(node.rect.y); y < Mathf.RoundToInt(node.rect.y + node.rect.height); y++)
                {
                    if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
                    {
                        regionMap[x, y] = new HybridRegionData
                        {
                            regionId = region.id,
                            regionType = region.regionType,
                            distanceFromBoundary = CalculateDistanceFromBoundary(x, y, node.rect)
                        };
                    }
                }
            }
        }
        else
        {
            CollectRegions(node.leftChild);
            CollectRegions(node.rightChild);
        }
    }

    HybridRegionType DetermineRegionType(Rect bounds)
    {
        float centerX = bounds.center.x / mapWidth;
        float centerY = bounds.center.y / mapHeight;

        if (centerY > 0.75f) return HybridRegionType.Northern;
        if (centerY < 0.25f) return HybridRegionType.Southern;
        if (centerX < 0.3f) return HybridRegionType.Western;
        if (centerX > 0.7f) return HybridRegionType.Eastern;
        return HybridRegionType.Central;
    }

    float CalculateDistanceFromBoundary(int x, int y, Rect bounds)
    {
        float minX = bounds.x;
        float maxX = bounds.x + bounds.width - 1;
        float minY = bounds.y;
        float maxY = bounds.y + bounds.height - 1;

        float distToLeft = x - minX;
        float distToRight = maxX - x;
        float distToBottom = y - minY;
        float distToTop = maxY - y;

        return Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);
    }

    #endregion

    #region 지역적 특성 부여

    void AssignRegionalCharacteristics()
    {
        foreach (var region in regions)
        {
            var biomeSettings = biomeRegions.FirstOrDefault(br => br.regionType == region.regionType);
            if (biomeSettings != null)
            {
                region.allowedBiomes = new List<EcosystemTileType>(biomeSettings.allowedBiomes);
                region.dominantBiome =
                    biomeSettings.allowedBiomes[UnityEngine.Random.Range(0, biomeSettings.allowedBiomes.Length)];
            }
            else
            {
                region.allowedBiomes = new List<EcosystemTileType>
                    { EcosystemTileType.Grass, EcosystemTileType.Forest };
                region.dominantBiome = EcosystemTileType.Grass;
            }

        }
    }

    #endregion

    #region 지역별 WFC 생성

    void GenerateRegionalWFC()
    {
        foreach (var region in regions)
        {
            GenerateWFCForRegion(region);
        }
    }

    void GenerateWFCForRegion(HybridRegionInfo region)
    {
        int minX = Mathf.RoundToInt(region.bounds.x);
        int maxX = Mathf.RoundToInt(region.bounds.x + region.bounds.width);
        int minY = Mathf.RoundToInt(region.bounds.y);
        int maxY = Mathf.RoundToInt(region.bounds.y + region.bounds.height);

        int regionWidth = maxX - minX;
        int regionHeight = maxY - minY;

        bool[,] collapsed = new bool[regionWidth, regionHeight];
        List<EcosystemTileType>[,] possibilities = new List<EcosystemTileType>[regionWidth, regionHeight];

        for (int x = 0; x < regionWidth; x++)
        {
            for (int y = 0; y < regionHeight; y++)
            {
                possibilities[x, y] = new List<EcosystemTileType>();

                if (bspInfluence > 0.8f && enforceRegionalBiomes)
                {
                    possibilities[x, y].AddRange(region.allowedBiomes);
                }
                else if (bspInfluence > 0.5f)
                {
                    possibilities[x, y].AddRange(region.allowedBiomes);

                    float distanceFromCenter = Vector2.Distance(
                        new Vector2(x, y),
                        new Vector2(regionWidth / 2, regionHeight / 2)
                    ) / Mathf.Min(regionWidth, regionHeight);

                    if (distanceFromCenter > 0.3f)
                    {
                        var compatibleBiomes = GetCompatibleBiomes(region.dominantBiome);
                        foreach (var biome in compatibleBiomes)
                        {
                            if (!possibilities[x, y].Contains(biome))
                            {
                                possibilities[x, y].Add(biome);
                            }
                        }
                    }
                }
                else
                {
                    var allBiomes = System.Enum.GetValues(typeof(EcosystemTileType)).Cast<EcosystemTileType>();
                    possibilities[x, y].AddRange(allBiomes.Where(b => b != EcosystemTileType.Empty));
                }
            }
        }

        if (bspInfluence > 0.2f)
        {
            PlaceSeedCells(region, collapsed, possibilities, regionWidth, regionHeight);
        }

        if (bspInfluence > 0.9f)
        {
            FillRegionWithDominantBiome(region, collapsed, possibilities, regionWidth, regionHeight);
        }

        for (int iteration = 0; iteration < maxWFCIterations; iteration++)
        {
            Vector2Int cellToCollapse =
                FindLowestEntropyCellInRegion(collapsed, possibilities, regionWidth, regionHeight);

            if (cellToCollapse.x == -1) break;

            CollapseCellInRegion(cellToCollapse.x, cellToCollapse.y, collapsed, possibilities, region);
            PropagateConstraintsInRegion(cellToCollapse.x, cellToCollapse.y, collapsed, possibilities, regionWidth,
                regionHeight, region);
        }

        CopyRegionToEcosystem(region, collapsed, possibilities, minX, minY, regionWidth, regionHeight);
    }

    void PlaceSeedCells(HybridRegionInfo region, bool[,] collapsed, List<EcosystemTileType>[,] possibilities,
        int regionWidth, int regionHeight)
    {
        int centerX = regionWidth / 2;
        int centerY = regionHeight / 2;
        float baseRadius = Mathf.Min(regionWidth, regionHeight) * 0.15f;
        int seedRadius = Mathf.RoundToInt(baseRadius * bspInfluence * 2f);

        for (int x = Mathf.Max(0, centerX - seedRadius); x < Mathf.Min(regionWidth, centerX + seedRadius + 1); x++)
        {
            for (int y = Mathf.Max(0, centerY - seedRadius); y < Mathf.Min(regionHeight, centerY + seedRadius + 1); y++)
            {
                float distanceFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                float seedProbability = 1f - (distanceFromCenter / (seedRadius + 1));

                if (UnityEngine.Random.Range(0f, 1f) < seedProbability * bspInfluence)
                {
                    possibilities[x, y].Clear();
                    possibilities[x, y].Add(region.dominantBiome);
                    collapsed[x, y] = true;
                }
            }
        }

        if (bspInfluence > 0.6f)
        {
            int additionalSeeds = Mathf.RoundToInt(regionWidth * regionHeight * 0.1f * bspInfluence);
            for (int i = 0; i < additionalSeeds; i++)
            {
                int randX = UnityEngine.Random.Range(0, regionWidth);
                int randY = UnityEngine.Random.Range(0, regionHeight);

                if (!collapsed[randX, randY] && UnityEngine.Random.Range(0f, 1f) < bspInfluence)
                {
                    var selectedBiome = region.allowedBiomes[UnityEngine.Random.Range(0, region.allowedBiomes.Count)];
                    possibilities[randX, randY].Clear();
                    possibilities[randX, randY].Add(selectedBiome);
                    collapsed[randX, randY] = true;
                }
            }
        }
    }

    void FillRegionWithDominantBiome(HybridRegionInfo region, bool[,] collapsed,
        List<EcosystemTileType>[,] possibilities, int regionWidth, int regionHeight)
    {
        for (int x = 0; x < regionWidth; x++)
        {
            for (int y = 0; y < regionHeight; y++)
            {
                if (!collapsed[x, y] && UnityEngine.Random.Range(0f, 1f) < (bspInfluence - 0.5f) * 2f)
                {
                    possibilities[x, y].Clear();
                    possibilities[x, y].Add(region.dominantBiome);
                    collapsed[x, y] = true;
                }
            }
        }
    }

    Vector2Int FindLowestEntropyCellInRegion(bool[,] collapsed, List<EcosystemTileType>[,] possibilities, int width,
        int height)
    {
        int minEntropy = int.MaxValue;
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
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

    void CollapseCellInRegion(int x, int y, bool[,] collapsed, List<EcosystemTileType>[,] possibilities,
        HybridRegionInfo region)
    {
        if (possibilities[x, y].Count == 0) return;

        float totalWeight = 0f;
        Dictionary<EcosystemTileType, float> weights = new Dictionary<EcosystemTileType, float>();

        foreach (var tileType in possibilities[x, y])
        {
            float baseWeight = wfcRules.FirstOrDefault(r => r.centerType == tileType)?.weight ?? 0.1f;

            if (region.allowedBiomes.Contains(tileType))
            {
                baseWeight *= (1f + bspInfluence * 3f);
            }
            else
            {
                baseWeight *= (1f - bspInfluence * 0.8f);
            }

            if (tileType == region.dominantBiome)
            {
                baseWeight *= (1f + bspInfluence * 2f);
            }

            float centerX = possibilities.GetLength(0) / 2f;
            float centerY = possibilities.GetLength(1) / 2f;
            float distanceFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
            float normalizedDistance = distanceFromCenter / Mathf.Min(centerX, centerY);

            if (tileType == region.dominantBiome)
            {
                baseWeight *= (1f + (1f - normalizedDistance) * bspInfluence);
            }

            baseWeight = Mathf.Max(baseWeight, 0.01f);

            weights[tileType] = baseWeight;
            totalWeight += baseWeight;
        }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        EcosystemTileType selectedType = possibilities[x, y][0];

        foreach (var kvp in weights)
        {
            currentWeight += kvp.Value;
            if (randomValue <= currentWeight)
            {
                selectedType = kvp.Key;
                break;
            }
        }

        possibilities[x, y].Clear();
        possibilities[x, y].Add(selectedType);
        collapsed[x, y] = true;
    }

    void PropagateConstraintsInRegion(int x, int y, bool[,] collapsed, List<EcosystemTileType>[,] possibilities,
        int width, int height, HybridRegionInfo region)
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

                if (nx >= 0 && nx < width && ny >= 0 && ny < height && !collapsed[nx, ny])
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

    void CopyRegionToEcosystem(HybridRegionInfo region, bool[,] collapsed, List<EcosystemTileType>[,] possibilities,
        int offsetX, int offsetY, int regionWidth, int regionHeight)
    {
        for (int x = 0; x < regionWidth; x++)
        {
            for (int y = 0; y < regionHeight; y++)
            {
                int globalX = offsetX + x;
                int globalY = offsetY + y;

                if (globalX >= 0 && globalX < mapWidth && globalY >= 0 && globalY < mapHeight)
                {
                    EcosystemTileType tileType = EcosystemTileType.Grass;

                    if (possibilities[x, y].Count > 0)
                    {
                        tileType = possibilities[x, y][0];
                    }
                    else if (region.allowedBiomes.Count > 0)
                    {
                        tileType = region.allowedBiomes[UnityEngine.Random.Range(0, region.allowedBiomes.Count)];
                    }

                    ecosystem[globalX, globalY] = new EcosystemTile(
                        tileType,
                        tileDatabase[tileType].fertility,
                        tileDatabase[tileType].moisture,
                        tileDatabase[tileType].temperature,
                        tileDatabase[tileType].color
                    );
                }
            }
        }
    }

    #endregion

    #region 경계 블렌딩

    void ApplyBoundaryBlending()
    {
        EcosystemTile[,] blendedEcosystem = new EcosystemTile[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                blendedEcosystem[x, y] = ecosystem[x, y];

                if (regionMap[x, y].distanceFromBoundary <= blendingRadius)
                {
                    ApplyBlendingAtPosition(x, y, blendedEcosystem);
                }
            }
        }

        ecosystem = blendedEcosystem;
    }

    void ApplyBlendingAtPosition(int x, int y, EcosystemTile[,] blendedEcosystem)
    {
        List<EcosystemTile> nearbyTiles = new List<EcosystemTile>();
        List<float> weights = new List<float>();

        for (int dx = -blendingRadius; dx <= blendingRadius; dx++)
        {
            for (int dy = -blendingRadius; dy <= blendingRadius; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                {
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance <= blendingRadius)
                    {
                        nearbyTiles.Add(ecosystem[nx, ny]);
                        weights.Add(1f / (1f + distance));
                    }
                }
            }
        }

        if (nearbyTiles.Count > 1)
        {
            float totalFertility = 0f;
            float totalMoisture = 0f;
            float totalTemperature = 0f;
            Color totalColor = Color.black;
            float totalWeight = 0f;

            for (int i = 0; i < nearbyTiles.Count; i++)
            {
                float weight = weights[i];
                totalFertility += nearbyTiles[i].fertility * weight;
                totalMoisture += nearbyTiles[i].moisture * weight;
                totalTemperature += nearbyTiles[i].temperature * weight;
                totalColor += nearbyTiles[i].color * weight;
                totalWeight += weight;
            }

            if (totalWeight > 0)
            {
                blendedEcosystem[x, y] = new EcosystemTile(
                    ecosystem[x, y].type, // 타입 유 지 ㄱ
                    totalFertility / totalWeight,
                    totalMoisture / totalWeight,
                    totalTemperature / totalWeight,
                    totalColor / totalWeight
                );
            }
        }
    }

    #endregion

    #region 후처리

    void ApplyFinalPostProcessing()
    {
        CleanupIsolatedTiles();

        ImproveWaterConnectivity();
    }

    void CleanupIsolatedTiles()
    {
        EcosystemTile[,] cleanedEcosystem = new EcosystemTile[mapWidth, mapHeight];
        Array.Copy(ecosystem, cleanedEcosystem, ecosystem.Length);

        for (int x = 1; x < mapWidth - 1; x++)
        {
            for (int y = 1; y < mapHeight - 1; y++)
            {
                if (IsIsolatedTile(x, y))
                {
                    var dominantNeighborType = GetDominantNeighborType(x, y);
                    cleanedEcosystem[x, y] = new EcosystemTile(
                        dominantNeighborType,
                        tileDatabase[dominantNeighborType].fertility,
                        tileDatabase[dominantNeighborType].moisture,
                        tileDatabase[dominantNeighborType].temperature,
                        tileDatabase[dominantNeighborType].color
                    );
                }
            }
        }

        ecosystem = cleanedEcosystem;
    }

    bool IsIsolatedTile(int x, int y)
    {
        EcosystemTileType currentType = ecosystem[x, y].type;
        int sameTypeNeighbors = 0;

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in directions)
        {
            int nx = x + dir.x;
            int ny = y + dir.y;

            if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
            {
                if (ecosystem[nx, ny].type == currentType)
                {
                    sameTypeNeighbors++;
                }
            }
        }

        return sameTypeNeighbors == 0;
    }

    EcosystemTileType GetDominantNeighborType(int x, int y)
    {
        Dictionary<EcosystemTileType, int> typeCounts = new Dictionary<EcosystemTileType, int>();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                {
                    EcosystemTileType neighborType = ecosystem[nx, ny].type;
                    typeCounts[neighborType] = typeCounts.GetValueOrDefault(neighborType, 0) + 1;
                }
            }
        }

        return typeCounts.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    void ImproveWaterConnectivity()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (ecosystem[x, y].type == EcosystemTileType.Water)
                {
                    ConnectWaterTile(x, y);
                }
            }
        }
    }

    void ConnectWaterTile(int x, int y)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        foreach (var dir in directions)
        {
            int nx = x + dir.x;
            int ny = y + dir.y;

            if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
            {
                if (ecosystem[nx, ny].type == EcosystemTileType.Grass && UnityEngine.Random.Range(0f, 1f) < 0.3f)
                {
                    ecosystem[nx, ny] = new EcosystemTile(
                        EcosystemTileType.Water,
                        tileDatabase[EcosystemTileType.Water].fertility,
                        tileDatabase[EcosystemTileType.Water].moisture,
                        tileDatabase[EcosystemTileType.Water].temperature,
                        tileDatabase[EcosystemTileType.Water].color
                    );
                }
            }
        }
    }

    #endregion

    #region 시각화

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
                Vector3 position = new Vector3(x * tileSpacing, y * tileSpacing, 0);
                GameObject tile = Instantiate(tilePrefab, position, Quaternion.identity, transform);
                
                Renderer renderer = tile.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = ecosystem[x, y].color;
                }

                if (use2DSprites)
                {
                    SpriteRenderer spriteRenderer = tile.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = ecosystem[x, y].color;
                    }
                }

                tile.name = $"Tile_{x}_{y}_{ecosystem[x, y].type}";
            }
        }
    }

    void PrintEcosystemStats()
    {
        Dictionary<EcosystemTileType, int> tileCounts = new Dictionary<EcosystemTileType, int>();

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                EcosystemTileType type = ecosystem[x, y].type;
                tileCounts[type] = tileCounts.GetValueOrDefault(type, 0) + 1;
            }
        }

        Debug.Log("=== 생태계 통계 ===");
        foreach (var kvp in tileCounts.OrderByDescending(x => x.Value))
        {
            float percentage = (float)kvp.Value / (mapWidth * mapHeight) * 100f;
            Debug.Log($"{kvp.Key}: {kvp.Value}개 ({percentage:F1}%)");
        }
    }

    #endregion

    #region Gizmos

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        if (showRegionBoundaries && regions != null)
        {
            Gizmos.color = Color.red;
            foreach (var region in regions)
            {
                Vector3 center = new Vector3(
                    region.bounds.center.x * tileSpacing,
                    region.bounds.center.y * tileSpacing,
                    0
                );
                Vector3 size = new Vector3(
                    region.bounds.width * tileSpacing,
                    region.bounds.height * tileSpacing,
                    0.1f
                );
                Gizmos.DrawWireCube(center, size);
            }
        }
    }

    #endregion
}

