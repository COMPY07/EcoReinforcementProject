using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct AStar : IJobParallelFor {
    [ReadOnly] public NativeArray<PathNode> grid;
    [ReadOnly] public NativeArray<PathRequest> requests;
    [ReadOnly] public int gridWidth;
    [ReadOnly] public int gridHeight;
    [ReadOnly] public int maxPathLength;
    
    [NativeDisableParallelForRestriction]
    public NativeArray<PathResult> results;
    
    [NativeDisableParallelForRestriction]
    public NativeArray<int2> pathBuffer;
    
    public void Execute(int index)
    {
        PathRequest request = requests[index];
        PathResult result = new PathResult
        {
            requestId = request.requestId,
            agentId = request.agentId,
            success = false,
            pathStartIndex = index * maxPathLength,
            pathLength = 0
        };
        
        if (!IsValidPosition(request.start) || !IsValidPosition(request.goal))
        {
            results[index] = result;
            return;
        }
        
        if (request.start.x == request.goal.x && request.start.y == request.goal.y)
        {
            result.success = true;
            result.pathLength = 1;
            pathBuffer[result.pathStartIndex] = request.goal;
            results[index] = result;
            return;
        }
        
        float distance = math.distance(request.start, request.goal);
        if (distance < 15) 
            SimpleAStar(index, request, ref result);
        else 
            ExecuteHierarchicalAStar(index, request, ref result);
        
        results[index] = result;
    }
    
    private void SimpleAStar(int jobIndex, PathRequest request, ref PathResult result)
    {
        const int MAX_ITERATIONS = 1000;
        const int MAX_OPEN_SET_SIZE = 512;
        int iterations = 0;
        
        NativeMinHeap openSet = new NativeMinHeap(MAX_OPEN_SET_SIZE, Allocator.Temp);
        NativeHashSet<int> closedSet = new NativeHashSet<int>(MAX_OPEN_SET_SIZE, Allocator.Temp);
        NativeHashMap<int, int> cameFrom = new NativeHashMap<int, int>(MAX_OPEN_SET_SIZE, Allocator.Temp);
        NativeHashMap<int, int> gScore = new NativeHashMap<int, int>(MAX_OPEN_SET_SIZE, Allocator.Temp);
        
        int startIndex = GetIndex(request.start);
        int goalIndex = GetIndex(request.goal);
        
        openSet.Push(new HeapNode { index = startIndex, fCost = 0 });
        gScore[startIndex] = 0;
        
        while (openSet.Count > 0 && iterations++ < MAX_ITERATIONS)
        {
            HeapNode current = openSet.Pop();
            
            if (current.index == goalIndex)
            {
                result.success = true;
                ReconstructPath(jobIndex, startIndex, goalIndex, cameFrom, ref result);
                break;
            }
            
            if (closedSet.Contains(current.index))
                continue;
                
            closedSet.Add(current.index);
            int2 currentPos = GetPosition(current.index);
            
            for (int dir = 0; dir < 8; dir++)
            {
                int2 neighborPos = GetNeighborPosition(currentPos, dir);
                
                if (!IsValidPosition(neighborPos))
                    continue;
                    
                int neighborIndex = GetIndex(neighborPos);
                
                if (closedSet.Contains(neighborIndex) || !grid[neighborIndex].isWalkable)
                    continue;
                
                if (dir % 2 == 1) 
                {
                    int2 checkPos1 = GetNeighborPosition(currentPos, (dir - 1) % 8);
                    int2 checkPos2 = GetNeighborPosition(currentPos, (dir + 1) % 8);
                    
                    if (!IsValidPosition(checkPos1) || !IsValidPosition(checkPos2))
                        continue;
                        
                    int checkIndex1 = GetIndex(checkPos1);
                    int checkIndex2 = GetIndex(checkPos2);
                    
                    if (!grid[checkIndex1].isWalkable || !grid[checkIndex2].isWalkable)
                        continue;
                }
                
                int currentGScore = gScore.TryGetValue(current.index, out int g) ? g : int.MaxValue;
                int tentativeGScore = currentGScore + GetMoveCost(dir);
                
                int neighborGScore = gScore.TryGetValue(neighborIndex, out int ng) ? ng : int.MaxValue;
                
                if (tentativeGScore < neighborGScore)
                {
                    cameFrom[neighborIndex] = current.index;
                    gScore[neighborIndex] = tentativeGScore;
                    
                    int hCost = HeuristicDistance(neighborPos, request.goal);
                    int fCost = tentativeGScore + hCost;
                    
                    openSet.Push(new HeapNode { index = neighborIndex, fCost = fCost });
                }
            }
        }
        
        openSet.Dispose();
        closedSet.Dispose();
        cameFrom.Dispose();
        gScore.Dispose();
    }
    
    private void ExecuteHierarchicalAStar(int jobIndex, PathRequest request, ref PathResult result)
    {
        SimpleAStar(jobIndex, request, ref result);
    }
    
    private void ReconstructPath(int jobIndex, int startIndex, int goalIndex, 
        NativeHashMap<int, int> cameFrom, ref PathResult result)
    {
        NativeList<int> path = new NativeList<int>(maxPathLength, Allocator.Temp);
        int current = goalIndex;
        int safetyCounter = 0;
        const int MAX_PATH_RECONSTRUCTION = 1000;
        
        path.Add(current);
        
        while (current != startIndex && safetyCounter++ < MAX_PATH_RECONSTRUCTION)
        {
            if (!cameFrom.TryGetValue(current, out int parent))
                break;
                
            current = parent;
            path.Add(current);
            
            if (path.Length > maxPathLength)
                break;
        }
        
        if (current != startIndex)
        {
            result.success = false;
            path.Dispose();
            return;
        }
        
        int pathStartIdx = result.pathStartIndex;
        result.pathLength = math.min(path.Length, maxPathLength);
        
        for (int i = 0; i < result.pathLength; i++)
        {
            pathBuffer[pathStartIdx + i] = GetPosition(path[path.Length - 1 - i]);
        }
        
        path.Dispose();
    }
    
    private int GetIndex(int2 pos) => pos.y * gridWidth + pos.x;
    
    private int2 GetPosition(int index) => new int2(index % gridWidth, index / gridWidth);
    
    private bool IsValidPosition(int2 pos)
    {
        return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
    }
    
    private int2 GetNeighborPosition(int2 current, int dir)
    {
        switch (dir)
        {
            case 0: return current + new int2(0, 1);   // 북
            case 1: return current + new int2(1, 1);   // 북동
            case 2: return current + new int2(1, 0);   // 동
            case 3: return current + new int2(1, -1);  // 남동
            case 4: return current + new int2(0, -1);  // 남
            case 5: return current + new int2(-1, -1); // 남서
            case 6: return current + new int2(-1, 0);  // 서
            case 7: return current + new int2(-1, 1);  // 북서
            default: return current;
        }
    }
    
    private int GetMoveCost(int direction) => (direction % 2 == 0) ? 10 : 14;
    
    private int HeuristicDistance(int2 a, int2 b)
    {
        int dx = math.abs(a.x - b.x);
        int dy = math.abs(a.y - b.y);
        return math.min(dx, dy) * 14 + math.abs(dx - dy) * 10;
    }
}