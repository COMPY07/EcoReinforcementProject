using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class HeightData
{
    [Range(-10f, 10f)]
    public float baseHeight = 0f;
    [Range(-10f, 10f)]
    public float heightVariation = 0.5f;
    public bool canHaveStructures = true;
    public List<StructureType> allowedStructures = new List<StructureType>();
}



[Serializable]
public class StructureData
{
    public StructureType structureType;
    public GameObject prefab;
    public List<GameObject> variations = new List<GameObject>();
    
    [Header("Placement Rules")]
    public List<TileType> compatibleTileTypes = new List<TileType>();
    public List<BiomeType> compatibleBiomes = new List<BiomeType>();
    [Range(0f, 10f)]
    public float minHeight = 0f;
    [Range(0f, 10f)]
    public float maxHeight = 10f;
    
    [Header("Spacing")]
    [Range(1, 10)]
    public int minDistanceFromSameType = 2;
    [Range(0f, 1f)]
    public float placementProbability = 0.3f;
    
    [Header("Visual")]
    [Range(0.5f, 2f)]
    public float scaleVariation = 1f;
    public bool allowRotation = true;
    public Vector3 positionOffset = Vector3.zero;
}


// public partial class TileData : ScriptableObject
// {
//     [Header("3D Extensions")]
//     public HeightData heightData = new HeightData();
// }


[Serializable]
public enum LayoutType
{
    Continuous,
    Sparse
}



[Serializable]
public enum StructureType
{
    House,
    Tree,
    Rock,
    Flower,
    Grass,
    Bush,
    Water,
    Bridge,
    Tower,
    Village,
    Forest,
    Mountain,
    Custom
}

[Serializable]
public enum BiomeType
{
    Grassland,
    Desert,
    Forest,
    Tundra,
    Swamp,
    Mountain,
    Ocean,
    Custom,
    City
}
[Serializable]
public enum RuleType
{
    Boost,  
    Reduce,   
    Require  
}
[Serializable]
public enum TileType
{
    Grass,
    Water,
    Stone,
    Sand,
    Dirt,
    Snow,
    Lava,
    Ice,
    Wood,
    Path,
    Custom,
    Impassable
}



[Serializable]
public class Cell
{
    public Vector2Int position;
    public List<TileData> possibleTiles = new List<TileData>();
    public TileData collapsedTile = null;
    public GameObject spawnedObject = null;
    public bool isCollapsed = false;
    
    public int Entropy => isCollapsed ? 0 : possibleTiles.Count;
    
    public void Collapse(TileData tile)
    {
        collapsedTile = tile;
        possibleTiles.Clear();
        possibleTiles.Add(tile);
        isCollapsed = true;
    }
    
    public void RemovePossibility(TileData tile)
    {
        if (!isCollapsed && possibleTiles.Contains(tile))
        {
            possibleTiles.Remove(tile);
        }
    }
    
    public Cell DeepCopy()
    {
        return new Cell
        {
            position = this.position,
            possibleTiles = new List<TileData>(this.possibleTiles),
            collapsedTile = this.collapsedTile,
            spawnedObject = null, // Don't copy the spawned object reference
            isCollapsed = this.isCollapsed
        };
    }
    
    
}

[Serializable]
public class HeightMapResult
{
    public float[,] heightMap;
    public float minHeight;
    public float maxHeight;
    public float averageHeight;
}

[Serializable]
public class StructurePlacementResult
{
    public Dictionary<Vector2Int, StructureInstance> placedStructures;
    public int totalPlaced;
    public Dictionary<StructureType, int> structuresByType;
}

[Serializable]
public class StructureInstance
{
    public Vector2Int position;
    public StructureData structureData;
    public GameObject prefab;
    public float scale = 1f;
    public float rotation = 0f;
    public float height = 0f;
    public GameObject spawnedObject;
}




[System.Serializable]
public enum PlacementStrategy
{
    Random,           // 기존 방식
    Clustered,        // 클러스터 형태로 배치
    Grid,             // 격자 형태로 배치
    Scattered,        // 분산 배치 (Poisson disk)
    PathAdjacent,     // 경로 인접 배치
    HeightBased,      // 높이 기반 배치
    Organic           // 유기적 배치 (WFC 스타일)
}

[System.Serializable]
public class StructureRelationship
{
    public StructureType relatedType;
    public float attractionDistance = 3f;  // 끌어당기는 거리
    public float repulsionDistance = 1f;   // 밀어내는 거리
    public float attractionStrength = 1f;  // 끌어당기는 힘
    public float repulsionStrength = 2f;   // 밀어내는 힘
}
