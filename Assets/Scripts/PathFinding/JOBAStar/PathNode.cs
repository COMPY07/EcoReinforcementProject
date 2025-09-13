using Unity.Mathematics;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct PathNode
{
    public int2 position;
    public bool isWalkable;
    public int gCost;
    public int hCost;
    public int fCost;
    public int parentIndex;
    public int heapIndex;
    public PathNode(int2 pos, bool walkable)
    {
        position = pos;
        isWalkable = walkable;
        gCost = 0;
        hCost = 0;
        fCost = 0;
        parentIndex = -1;
        heapIndex = -1;
    }
}