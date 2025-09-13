
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Structure Data", menuName = "WFC/EnhancedStructureData")]
public class EnhancedStructureData : ScriptableObject
{
    [Header("Basic Settings")]
    public StructureType structureType;
    public GameObject prefab;
    public List<GameObject> variations = new List<GameObject>();
    
    [Header("Placement Strategy")]
    public PlacementStrategy placementStrategy = PlacementStrategy.Random;
    [Range(0f, 1f)]
    public float placementProbability = 0.3f;
    
    [Header("Placement Rules")]
    public List<TileType> compatibleTileTypes = new List<TileType>();
    public List<BiomeType> compatibleBiomes = new List<BiomeType>();
    [Range(0f, 10f)]
    public float minHeight = 0f;
    [Range(0f, 10f)]
    public float maxHeight = 10f;
    
    [Header("Clustering Settings")]
    [Range(1, 10)]
    public int clusterSize = 3;
    [Range(1f, 5f)]
    public float clusterRadius = 2f;
    [Range(0f, 1f)]
    public float clusterDensity = 0.7f;
    
    [Header("Structure Relationships")]
    public List<StructureRelationship> relationships = new List<StructureRelationship>();
    
    [Header("Advanced Rules")]
    [Range(1, 10)]
    public int minDistanceFromSameType = 2;
    [Range(0f, 5f)]
    public float preferredSlope = 0f;
    [Range(0f, 2f)]
    public float slopeThreshold = 1f;
    
    [Header("Visual")]
    [Range(0.5f, 2f)]
    public float scaleVariation = 1f;
    public bool allowRotation = true;
    public Vector3 positionOffset = Vector3.zero;
    
    [Header("Path Interaction")]
    public bool requiresPathAccess = false;
    [Range(1, 5)]
    public int maxDistanceFromPath = 2;
    
    
    public bool IsCompatibleWith(EnhancedStructureData other, float distance)
    {
        foreach (var relationship in relationships)
        {
            if (relationship.relatedType == other.structureType)
            {
                if (distance < relationship.repulsionDistance)
                    return false;
                return true;
            }
        }
        return true;
    }
    
    
    public float GetRelationshipScore(EnhancedStructureData other, float distance)
    {
        foreach (var relationship in relationships)
        {
            if (relationship.relatedType == other.structureType)
            {
                if (distance < relationship.repulsionDistance)
                {
                    return -relationship.repulsionStrength * (relationship.repulsionDistance - distance);
                }
                else if (distance < relationship.attractionDistance)
                {
                    float normalizedDistance = distance / relationship.attractionDistance;
                    return relationship.attractionStrength * (1f - normalizedDistance);
                }
            }
        }
        return 0f;
    }
}
