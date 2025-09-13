

using System.Collections.Generic;
using UnityEngine;

public class TerrainStructureManager : MonoBehaviour
{
    [Header("Settings")]
    public TerrainStructureSettings settings;
    
    [Header("References")]
    public WFCTerrainStructurePlacer structurePlacer;
    public Transform structuresParent;
    
    private List<Vector3> placedPositions = new List<Vector3>();
    
    public bool ValidatePlacement(Vector3 position, EnhancedStructureData structureData, Terrain terrain)
    {
        if (terrain == null) return false;
        
        float terrainHeight = GetTerrainHeightAtWorldPos(position, terrain);
        Vector3 terrainNormal = GetTerrainNormalAtWorldPos(position, terrain);
        float slope = Vector3.Angle(terrainNormal, Vector3.up);
        
        if (settings.avoidSteepSlopes && slope > settings.maxAllowedSlope)
        {
            return false;
        }
        
        if (settings.useHeightConstraints)
        {
            if (terrainHeight < settings.minTerrainHeight || terrainHeight > settings.maxTerrainHeight)
            {
                return false;
            }
        }
        
        if (settings.enforceMinimumSpacing)
        {
            foreach (Vector3 placedPos in placedPositions)
            {
                if (Vector3.Distance(position, placedPos) < settings.minimumSpacing)
                {
                    return false;
                }
            }
        }
        
        return true;
    }
    
    public void RegisterPlacement(Vector3 position)
    {
        placedPositions.Add(position);
    }
    
    public void ClearPlacementHistory()
    {
        placedPositions.Clear();
    }
    
    private float GetTerrainHeightAtWorldPos(Vector3 worldPos, Terrain terrain)
    {
        Vector3 terrainLocalPos = worldPos - terrain.transform.position;
        Vector3 normalizedPos = new Vector3(
            terrainLocalPos.x / terrain.terrainData.size.x,
            0,
            terrainLocalPos.z / terrain.terrainData.size.z
        );
        
        return terrain.terrainData.GetInterpolatedHeight(normalizedPos.x, normalizedPos.z);
    }
    
    private Vector3 GetTerrainNormalAtWorldPos(Vector3 worldPos, Terrain terrain)
    {
        Vector3 terrainLocalPos = worldPos - terrain.transform.position;
        Vector3 normalizedPos = new Vector3(
            terrainLocalPos.x / terrain.terrainData.size.x,
            0,
            terrainLocalPos.z / terrain.terrainData.size.z
        );
        
        return terrain.terrainData.GetInterpolatedNormal(normalizedPos.x, normalizedPos.z);
    }
}
[System.Serializable]
public class TerrainStructureSettings
{
    [Header("Placement Rules")]
    public bool respectOriginalPlacement = true;
    public bool analyzeTerrainSlope = true;
    public bool avoidSteepSlopes = true;
    [Range(0f, 45f)]
    public float maxAllowedSlope = 30f;
    
    [Header("Spacing")]
    public bool enforceMinimumSpacing = true;
    [Range(1f, 10f)]
    public float minimumSpacing = 2f;
    
    [Header("Height Constraints")]
    public bool useHeightConstraints = false;
    [Range(0f, 100f)]
    public float minTerrainHeight = 0f;
    [Range(0f, 100f)]
    public float maxTerrainHeight = 50f;
}
