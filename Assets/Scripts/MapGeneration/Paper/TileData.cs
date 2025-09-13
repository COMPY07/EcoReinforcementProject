using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Tile Data", menuName = "WFC/Tile Data")]
public class TileData : ScriptableObject
{
    [Header("Basic Info")]
    public string tileName;
    public TileType tileType;
    public GameObject prefab;
    public BiomeType biome;
    public Color gizmoColor = Color.white;
    
    [Header("Adjacency Rules")]
    [Tooltip("Empty list behavior depends on 'allowAllByDefault'. Use '*' for wildcard.")]
    public List<string> compatibleUp = new List<string>();
    public List<string> compatibleDown = new List<string>();
    public List<string> compatibleLeft = new List<string>();
    public List<string> compatibleRight = new List<string>();
    
    [Header("Advanced Rules")]
    [Tooltip("If true: empty compatibility list means 'allow all'. If false: empty list means 'allow none'.")]
    public bool allowAllByDefault = true;
    
    [Header("Weights")]
    [Range(0.1f, 5f)]
    public float baseWeight = 1f;
    [Range(0.1f, 3f)]
    public float biomeWeight = 1f;
    
    [Header("Visual Variations")]
    public List<GameObject> variations = new List<GameObject>();
    public bool allowRotation = true;
    public bool allowMirroring = false;
    
    [Header("3D Extensions")]
    public HeightData heightData = new HeightData();
    
}

/*
그 색을 
   
   using System.Collections.Generic;
   using UnityEngine;
   
   [CreateAssetMenu(fileName = "New Tile Data", menuName = "WFC/Tile Data")]
   public class TileData : ScriptableObject
   {
   [Header("Basic Info")]
   public string tileName;
   public TileType tileType;
   public GameObject prefab;
   public BiomeType biome;
   public Color gizmoColor = Color.white;
   
   [Header("Adjacency Rules")]
   [Tooltip("Empty list behavior depends on 'allowAllByDefault'. Use '*' for wildcard.")]
   public List<string> compatibleUp = new List<string>();
   public List<string> compatibleDown = new List<string>();
   public List<string> compatibleLeft = new List<string>();
   public List<string> compatibleRight = new List<string>();
   
   [Header("Advanced Rules")]
   [Tooltip("If true: empty compatibility list means 'allow all'. If false: empty list means 'allow none'.")]
   public bool allowAllByDefault = true;
   
   [Header("Weights")]
   [Range(0.1f, 5f)]
   public float baseWeight = 1f;
   [Range(0.1f, 3f)]
   public float biomeWeight = 1f;
   
   [Header("Visual Variations")]
   public List<GameObject> variations = new List<GameObject>();
   public bool allowRotation = true;
   public bool allowMirroring = false;
   
   [Header("3D Extensions")]
   public HeightData heightData = new HeightData();
   
   }
   
   여기 color에 맞게 해보는거지. 위치를 알고, 상대 위치도 알테니.. 그거 비율에 맞춰서 또 바뀌는 구간이면 그 중간이나 블러 같은거 넣어서 색을 좀 섞어서 처리하고, 
   이런식으로 색을 하도록 해야되누
   */
   
   