using Unity.Collections;

public struct NativeMinHeap : System.IDisposable
{
    private NativeArray<HeapNode> items;
    private int currentItemCount;
    private int capacity;
    
    public int Count => currentItemCount;
    
    public NativeMinHeap(int maxHeapSize, Allocator allocator)
    {
        capacity = maxHeapSize;
        items = new NativeArray<HeapNode>(maxHeapSize, allocator);
        currentItemCount = 0;
    }
    
    public void Push(HeapNode item)
    {
        if (currentItemCount >= capacity)
            return;
            
        items[currentItemCount] = item;
        SortUp(currentItemCount);
        currentItemCount++;
    }
    
    public HeapNode Pop()
    {
        HeapNode firstItem = items[0];
        currentItemCount--;
        
        if (currentItemCount > 0)
        {
            items[0] = items[currentItemCount];
            SortDown(0);
        }
        
        return firstItem;
    }
    
    public void Clear()
    {
        currentItemCount = 0;
    }
    
    private void SortDown(int index)
    {
        while (true)
        {
            int childIndexLeft = index * 2 + 1;
            int childIndexRight = index * 2 + 2;
            int swapIndex = index;
            
            if (childIndexLeft < currentItemCount)
            {
                if (items[childIndexLeft].fCost < items[swapIndex].fCost)
                {
                    swapIndex = childIndexLeft;
                }
                
                if (childIndexRight < currentItemCount)
                {
                    if (items[childIndexRight].fCost < items[swapIndex].fCost)
                    {
                        swapIndex = childIndexRight;
                    }
                }
                
                if (swapIndex != index)
                {
                    Swap(index, swapIndex);
                    index = swapIndex;
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }
    }
    
    private void SortUp(int index)
    {
        int parentIndex = (index - 1) / 2;
        
        while (index > 0 && items[index].fCost < items[parentIndex].fCost)
        {
            Swap(index, parentIndex);
            index = parentIndex;
            parentIndex = (index - 1) / 2;
        }
    }
    
    private void Swap(int indexA, int indexB)
    {
        HeapNode temp = items[indexA];
        items[indexA] = items[indexB];
        items[indexB] = temp;
    }
    
    public void Dispose()
    {
        if (items.IsCreated)
            items.Dispose();
    }
}