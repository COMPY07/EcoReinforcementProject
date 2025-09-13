using System.Collections.Generic;
using UnityEngine;

public class BasicAStar : MonoBehaviour
{
    [System.Serializable]
    public class Node
    {
        public Vector2Int position;
        public bool isWalkable = true;
        public float gCost = 0f; // 시작점부터의 거리
        public float hCost = 0f; // 목표점까지의 추정 거리
        public float fCost => gCost + hCost; // 총 비용
        public Node parent;

        public Node(Vector2Int pos, bool walkable = true)
        {
            position = pos;
            isWalkable = walkable;
        }

        public void Reset()
        {
            gCost = 0f;
            hCost = 0f;
            parent = null;
        }
    }

    [Header("Grid Settings")]
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float nodeSize = 1f;
    public Vector3 gridOrigin = Vector3.zero;
    
    [Header("Pathfinding")]
    public LayerMask obstacleLayer = 1;
    public bool allowDiagonal = true;
    
    [Header("Debug")]
    public bool showGrid = true;
    public bool showPath = true;
    public Color gridColor = Color.white;
    public Color obstacleColor = Color.red;
    public Color pathColor = Color.green;

    private Node[,] grid;
    private List<Node> currentPath = new List<Node>();

    void Start()
    {
        if (gridOrigin == Vector3.zero)
            gridOrigin = transform.position;
            
        CreateGrid();
    }

    void CreateGrid()
    {
        grid = new Node[gridWidth, gridHeight];
        
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3 worldPos = GridToWorldPosition(x, y);
                bool isWalkable = !Physics2D.OverlapCircle(worldPos, nodeSize * 0.4f, obstacleLayer);
                
                grid[x, y] = new Node(new Vector2Int(x, y), isWalkable);
            }
        }
    }

    public List<Node> FindPath(Vector3 startWorldPos, Vector3 targetWorldPos)
    {
        Vector2Int startPos = WorldToGridPosition(startWorldPos);
        Vector2Int targetPos = WorldToGridPosition(targetWorldPos);

        return FindPath(startPos, targetPos);
    }

    public List<Node> FindPath(Vector2Int startPos, Vector2Int targetPos)
    {
        if (!IsValidGridPosition(startPos) || !IsValidGridPosition(targetPos))
            return new List<Node>();

        Node startNode = grid[startPos.x, startPos.y];
        Node targetNode = grid[targetPos.x, targetPos.y];

        if (!startNode.isWalkable || !targetNode.isWalkable)
            return new List<Node>();

        ResetAllNodes();

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = GetLowestFCostNode(openSet);
            
            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                List<Node> path = RetracePath(startNode, targetNode);
                currentPath = path;
                return path;
            }

            foreach (Node neighbor in GetNeighbors(currentNode))
            {
                if (!neighbor.isWalkable || closedSet.Contains(neighbor))
                    continue;

                float newCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor);
                
                if (newCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newCostToNeighbor;
                    neighbor.hCost = GetDistance(neighbor, targetNode);
                    neighbor.parent = currentNode;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return new List<Node>(); 
    }

    private void ResetAllNodes()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[x, y].Reset();
            }
        }
    }

    private Node GetLowestFCostNode(List<Node> nodeList)
    {
        Node lowestFCostNode = nodeList[0];
        
        for (int i = 1; i < nodeList.Count; i++)
        {
            if (nodeList[i].fCost < lowestFCostNode.fCost || 
                (nodeList[i].fCost == lowestFCostNode.fCost && nodeList[i].hCost < lowestFCostNode.hCost))
            {
                lowestFCostNode = nodeList[i];
            }
        }
        
        return lowestFCostNode;
    }

    private List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();
        
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                if (!allowDiagonal && x != 0 && y != 0)
                    continue;

                int checkX = node.position.x + x;
                int checkY = node.position.y + y;

                if (IsValidGridPosition(checkX, checkY))
                {
                    neighbors.Add(grid[checkX, checkY]);
                }
            }
        }
        
        return neighbors;
    }

    private List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        
        path.Reverse();
        return path;
    }

    private float GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.position.x - nodeB.position.x);
        int dstY = Mathf.Abs(nodeA.position.y - nodeB.position.y);

        if (allowDiagonal)
        {
            if (dstX > dstY)
                return 1.4f * dstY + 1.0f * (dstX - dstY);
            return 1.4f * dstX + 1.0f * (dstY - dstX);
        }
        else
        {
            return dstX + dstY;
        }
    }

    public Vector3 GridToWorldPosition(int x, int y)
    {
        return new Vector3(x * nodeSize, y * nodeSize, 0) + gridOrigin;
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        Vector3 localPos = worldPosition - gridOrigin;
        int x = Mathf.RoundToInt(localPos.x / nodeSize);
        int y = Mathf.RoundToInt(localPos.y / nodeSize);
        return new Vector2Int(x, y);
    }

    private bool IsValidGridPosition(Vector2Int pos)
    {
        return IsValidGridPosition(pos.x, pos.y);
    }

    private bool IsValidGridPosition(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

    public Node GetNodeAtWorldPosition(Vector3 worldPos)
    {
        Vector2Int gridPos = WorldToGridPosition(worldPos);
        if (IsValidGridPosition(gridPos))
            return grid[gridPos.x, gridPos.y];
        return null;
    }

    public void SetNodeWalkable(Vector2Int gridPos, bool walkable)
    {
        if (IsValidGridPosition(gridPos))
            grid[gridPos.x, gridPos.y].isWalkable = walkable;
    }

    public void RefreshGrid()
    {
        CreateGrid();
    }
    
    public void SetGridOrigin(Vector3 newOrigin)
    {
        gridOrigin = newOrigin;
        CreateGrid();
    }
    
    public Vector3 GetGridOrigin()
    {
        return gridOrigin;
    }

    void OnDrawGizmos()
    {
        if (!showGrid || grid == null)
            return;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3 worldPos = GridToWorldPosition(x, y);
                
                if (!grid[x, y].isWalkable)
                {
                    Gizmos.color = obstacleColor;
                    Gizmos.DrawCube(worldPos, Vector3.one * nodeSize * 0.8f);
                }
                else
                {
                    Gizmos.color = gridColor;
                    Gizmos.DrawWireCube(worldPos, Vector3.one * nodeSize);
                }
            }
        }

        if (showPath && currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < currentPath.Count; i++)
            {
                Vector3 worldPos = GridToWorldPosition(currentPath[i].position.x, currentPath[i].position.y);
                Gizmos.DrawCube(worldPos, Vector3.one * nodeSize * 0.6f);
                
                if (i < currentPath.Count - 1)
                {
                    Vector3 nextWorldPos = GridToWorldPosition(currentPath[i + 1].position.x, currentPath[i + 1].position.y);
                    Gizmos.DrawLine(worldPos, nextWorldPos);
                }
            }
        }
    }
}