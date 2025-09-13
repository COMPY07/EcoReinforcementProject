using System;
using System.Collections.Generic;
using UnityEngine;

public class HybridTileInfo : MonoBehaviour
{
    public EcosystemTile tileData;
    public HybridRegionData regionData;
    public int gridX, gridY;
    
    public void SetTileData(EcosystemTile tileInfo, HybridRegionData regionInfo, int x, int y)
    {
        tileData = tileInfo;
        regionData = regionInfo;
        gridX = x;
        gridY = y;
    }
    
    void OnMouseDown()
    {
        Debug.Log($"하이브리드 타일 ({gridX}, {gridY}):\n" +
                 $"타입: {tileData.type}\n" +
                 $"지역: {regionData.regionType} (ID: {regionData.regionId})\n" +
                 $"경계거리: {regionData.distanceFromBoundary:F1}\n" +
                 $"속성: 생식력={tileData.fertility:F2}, 습도={tileData.moisture:F2}, 온도={tileData.temperature:F1}°C");
    }
}

public enum HybridRegionType
{
    Northern, 
    Southern,  
    Western,   
    Eastern,  
    Central 
}

[Serializable]
public struct HybridRegionData
{
    public int regionId;
    public HybridRegionType regionType;
    public float distanceFromBoundary;
    
    public HybridRegionData(int id, HybridRegionType type, float distance)
    {
        regionId = id;
        regionType = type;
        distanceFromBoundary = distance;
    }
}

[Serializable]
public class HybridRegionInfo
{
    public int id;
    public Rect bounds;
    public HybridRegionType regionType;
    public EcosystemTileType dominantBiome;
    public List<EcosystemTileType> allowedBiomes;
    
    public HybridRegionInfo()
    {
        allowedBiomes = new List<EcosystemTileType>();
    }
    
    public Vector2 GetCenter()
    {
        return bounds.center;
    }
    
    public float GetArea()
    {
        return bounds.width * bounds.height;
    }
    
    public bool ContainsPoint(int x, int y)
    {
        return bounds.Contains(new Vector2(x, y));
    }
}

[Serializable]
public class HybridBiomeRegionSettings
{
    public HybridRegionType regionType;
    public EcosystemTileType[] allowedBiomes;
    
    public HybridBiomeRegionSettings()
    {
        regionType = HybridRegionType.Central;
        allowedBiomes = new EcosystemTileType[] { EcosystemTileType.Grass };
    }
    
    public HybridBiomeRegionSettings(HybridRegionType type, EcosystemTileType[] biomes)
    {
        regionType = type;
        allowedBiomes = biomes;
    }
}

public class HybridBSPNode
{
    public Rect rect;
    public HybridBSPNode leftChild;
    public HybridBSPNode rightChild;
    public EcosystemTileType biomeType;
    
    public HybridBSPNode(Rect r)
    {
        rect = r;
        leftChild = null;
        rightChild = null;
        biomeType = EcosystemTileType.Empty;
    }
    
    public bool IsLeaf()
    {
        return leftChild == null && rightChild == null;
    }
    
    public List<HybridBSPNode> GetLeaves()
    {
        var leaves = new List<HybridBSPNode>();
        CollectLeaves(leaves);
        return leaves;
    }
    
    private void CollectLeaves(List<HybridBSPNode> leaves)
    {
        if (IsLeaf())
        {
            leaves.Add(this);
        }
        else
        {
            leftChild?.CollectLeaves(leaves);
            rightChild?.CollectLeaves(leaves);
        }
    }
}

[Serializable]
public class HybridWFCRule
{
    public EcosystemTileType centerType;
    public List<EcosystemTileType> allowedNeighbors;
    public float weight;

    public HybridWFCRule(EcosystemTileType center, List<EcosystemTileType> neighbors, float w)
    {
        centerType = center;
        allowedNeighbors = new List<EcosystemTileType>(neighbors);
        weight = w;
    }
    
    public bool IsNeighborAllowed(EcosystemTileType neighborType)
    {
        return allowedNeighbors.Contains(neighborType);
    }
    
    public override string ToString()
    {
        return $"Hybrid {centerType} -> [{string.Join(", ", allowedNeighbors)}] (w:{weight:F2})";
    }
}