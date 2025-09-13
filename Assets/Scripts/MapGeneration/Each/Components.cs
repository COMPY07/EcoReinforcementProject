using System.Collections.Generic;
using UnityEngine;
using System;

public enum EcosystemTileType
{
    Empty,
    Grass,
    Forest,
    Water,
    Mountain,
    Desert,
    Snow
}

[System.Serializable]
public class EcosystemTile
{
    public EcosystemTileType type;
    public float fertility;   
    public float moisture;  
    public float temperature;
    public Color color;      
    
    public EcosystemTile(EcosystemTileType type, float fertility, float moisture, float temperature, Color color)
    {
        this.type = type;
        this.fertility = fertility;
        this.moisture = moisture;
        this.temperature = temperature;
        this.color = color;
    }
    
    public EcosystemTile(EcosystemTile other)
    {
        this.type = other.type;
        this.fertility = other.fertility;
        this.moisture = other.moisture;
        this.temperature = other.temperature;
        this.color = other.color;
    }
    
    public float GetSimilarity(EcosystemTile other)
    {
        if (other == null) return 0f;
        
        float typeSimilarity = (type == other.type) ? 1f : 0f;
        float fertilityDiff = 1f - Mathf.Abs(fertility - other.fertility);
        float moistureDiff = 1f - Mathf.Abs(moisture - other.moisture);
        float temperatureDiff = 1f - (Mathf.Abs(temperature - other.temperature) / 100f);
        
        return (typeSimilarity * 0.4f + fertilityDiff * 0.2f + moistureDiff * 0.2f + temperatureDiff * 0.2f);
    }
    
    
    public float GetSuitability(float targetFertility, float targetMoisture, float targetTemperature)
    {
        float fertilityScore = 1f - Mathf.Abs(fertility - targetFertility);
        float moistureScore = 1f - Mathf.Abs(moisture - targetMoisture);
        float temperatureScore = 1f - (Mathf.Abs(temperature - targetTemperature) / 100f);
        
        return (fertilityScore + moistureScore + temperatureScore) / 3f;
    }
    
    public override string ToString()
    {
        return $"{type} (F:{fertility:F1}, M:{moisture:F1}, T:{temperature:F1}°)";
    }
}


[System.Serializable]
public class BSPNode
{
    public Rect rect;
    public BSPNode leftChild;
    public BSPNode rightChild;
    public EcosystemTileType biomeType = EcosystemTileType.Empty;
    public int depth = 0;
    public bool isProcessed = false;
    
    public BSPNode(Rect rect)
    {
        this.rect = rect;
    }
    
    public BSPNode(float x, float y, float width, float height)
    {
        this.rect = new Rect(x, y, width, height);
    }
    
    public bool IsLeaf()
    {
        return leftChild == null && rightChild == null;
    }
    
    public List<BSPNode> GetAllNodes()
    {
        List<BSPNode> nodes = new List<BSPNode> { this };
        
        if (leftChild != null)
            nodes.AddRange(leftChild.GetAllNodes());
            
        if (rightChild != null)
            nodes.AddRange(rightChild.GetAllNodes());
            
        return nodes;
    }
    
    public List<BSPNode> GetLeafNodes()
    {
        List<BSPNode> leafNodes = new List<BSPNode>();
        
        if (IsLeaf())
        {
            leafNodes.Add(this);
        }
        else
        {
            if (leftChild != null)
                leafNodes.AddRange(leftChild.GetLeafNodes());
                
            if (rightChild != null)
                leafNodes.AddRange(rightChild.GetLeafNodes());
        }
        
        return leafNodes;
    }
    
    public float GetArea()
    {
        return rect.width * rect.height;
    }
    
    public Vector2 GetCenter()
    {
        return rect.center;
    }
    
    public bool ContainsPoint(Vector2 point)
    {
        return rect.Contains(point);
    }
    
    public bool ContainsPoint(int x, int y)
    {
        return rect.Contains(new Vector2(x, y));
    }
    public float GetDistanceTo(BSPNode other)
    {
        return Vector2.Distance(GetCenter(), other.GetCenter());
    }
    
    public bool IsAdjacentTo(BSPNode other)
    {
        if (other == null) return false;
        
        bool horizontallyAdjacent = (Mathf.Approximately(rect.xMax, other.rect.xMin) || 
                                   Mathf.Approximately(other.rect.xMax, rect.xMin)) &&
                                  !(rect.yMax <= other.rect.yMin || other.rect.yMax <= rect.yMin);
        
        bool verticallyAdjacent = (Mathf.Approximately(rect.yMax, other.rect.yMin) || 
                                 Mathf.Approximately(other.rect.yMax, rect.yMin)) &&
                                !(rect.xMax <= other.rect.xMin || other.rect.xMax <= rect.xMin);
        
        return horizontallyAdjacent || verticallyAdjacent;
    }
    
    public override string ToString()
    {
        return $"BSPNode(x:{rect.x}, y:{rect.y}, w:{rect.width}, h:{rect.height}, biome:{biomeType}, leaf:{IsLeaf()})";
    }
}

[System.Serializable]
public class WFCRule
{
    public EcosystemTileType centerType;       
    public List<EcosystemTileType> allowedNeighbors;
    public float weight;                           
    public int minClusterSize = 1;                 
    public int maxClusterSize = int.MaxValue;      
    
    public WFCRule(EcosystemTileType centerType, List<EcosystemTileType> allowedNeighbors, float weight)
    {
        this.centerType = centerType;
        this.allowedNeighbors = new List<EcosystemTileType>(allowedNeighbors);
        this.weight = weight;
    }
    
    public WFCRule(EcosystemTileType centerType, List<EcosystemTileType> allowedNeighbors, float weight, int minCluster, int maxCluster)
    {
        this.centerType = centerType;
        this.allowedNeighbors = new List<EcosystemTileType>(allowedNeighbors);
        this.weight = weight;
        this.minClusterSize = minCluster;
        this.maxClusterSize = maxCluster;
    }
    
    public bool CanBeNeighbor(EcosystemTileType neighborType)
    {
        return allowedNeighbors.Contains(neighborType);
    }
    
    public float GetStrictness()
    {
        int totalTypes = System.Enum.GetValues(typeof(EcosystemTileType)).Length - 1;
        return 1f - ((float)allowedNeighbors.Count / totalTypes);
    }
    
    public override string ToString()
    {
        string neighbors = string.Join(", ", allowedNeighbors);
        return $"WFCRule({centerType} -> [{neighbors}], weight:{weight})";
    }
}

public class TileInfo : MonoBehaviour
{
    [SerializeField] private EcosystemTile tileData;
    [SerializeField] private int gridX;
    [SerializeField] private int gridY;
    [SerializeField] private float lastUpdateTime;
    
    [SerializeField] private bool isHighlighted = false;
    [SerializeField] private bool isSelected = false;
    [SerializeField] private float animationProgress = 0f;
    

    public Vector2Int GridPosition => new Vector2Int(gridX, gridY);

    
    public void SetTileData(EcosystemTile data, int x, int y)
    {
        tileData = new EcosystemTile(data);
        gridX = x;
        gridY = y;
        lastUpdateTime = Time.time;
        
        gameObject.name = $"Tile_{x}_{y}_{data.type}";
    }
    
    public void UpdateTileData(EcosystemTile data)
    {
        if (tileData != null)
        {
            tileData.type = data.type;
            tileData.fertility = data.fertility;
            tileData.moisture = data.moisture;
            tileData.temperature = data.temperature;
            tileData.color = data.color;
            lastUpdateTime = Time.time;
        }
    }
    
    public void SetHighlight(bool highlighted, Color? highlightColor = null)
    {
        isHighlighted = highlighted;
        
        if (highlighted && highlightColor.HasValue)
        {
            SetVisualColor(highlightColor.Value);
        }
        else if (!highlighted && tileData != null)
        {
            SetVisualColor(tileData.color);
        }
    }
    
    public void SetSelected(bool selected)
    {
        isSelected = selected;
    }
    
    private void SetVisualColor(Color color)
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            if (renderer.material.name.Contains("(Instance)") == false)
            {
                renderer.material = new Material(renderer.material);
            }
            renderer.material.color = color;
        }
        
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }
    
    public void SetAnimationProgress(float progress)
    {
        animationProgress = Mathf.Clamp01(progress);
        
        if (tileData != null)
        {
            Color animatedColor = tileData.color;
            animatedColor.a = animationProgress;
            SetVisualColor(animatedColor);
        }
    }
    
    private void OnMouseDown()
    {
        if (tileData != null)
        {
            SetSelected(!isSelected);
        }
    }
    
    private void OnMouseEnter()
    {
        if (!isHighlighted && tileData != null)
        {
            SetHighlight(true, Color.white);
        }
    }
    
    private void OnMouseExit()
    {
        if (isHighlighted && !isSelected)
        {
            SetHighlight(false);
        }
    }
    
    public List<TileInfo> GetNeighborTiles(int range = 1)
    {
        List<TileInfo> neighbors = new List<TileInfo>();
        
        Transform parent = transform.parent;
        if (parent != null)
        {
            TileInfo[] allTiles = parent.GetComponentsInChildren<TileInfo>();
            
            foreach (var tile in allTiles)
            {
                if (tile != this)
                {
                    int dx = Mathf.Abs(tile.gridX - gridX);
                    int dy = Mathf.Abs(tile.gridY - gridY);
                    
                    if (dx <= range && dy <= range && (dx + dy) <= range * 2)
                    {
                        neighbors.Add(tile);
                    }
                }
            }
        }
        
        return neighbors;
    }
    
    public TileInfo GetNeighborTile(Vector2Int direction)
    {
        Vector2Int targetPos = GridPosition + direction;
        
        Transform parent = transform.parent;
        if (parent != null)
        {
            TileInfo[] allTiles = parent.GetComponentsInChildren<TileInfo>();
            
            foreach (var tile in allTiles)
            {
                if (tile.GridPosition == targetPos)
                {
                    return tile;
                }
            }
        }
        
        return null;
    }
    
    public override string ToString()
    {
        if (tileData != null)
        {
            return $"TileInfo({gridX},{gridY}) - {tileData}";
        }
        return $"TileInfo({gridX},{gridY}) - No Data";
    }
    
    private void OnDrawGizmos()
    {
        if (isSelected)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, transform.localScale * 1.2f);
        }
        
        if (isHighlighted)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position, transform.localScale * 1.1f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (tileData != null)
        {
            Gizmos.color = Color.green;
            var neighbors = GetNeighborTiles(1);
            foreach (var neighbor in neighbors)
            {
                if (neighbor != null)
                {
                    Gizmos.DrawLine(transform.position, neighbor.transform.position);
                }
            }
        }
    }
}