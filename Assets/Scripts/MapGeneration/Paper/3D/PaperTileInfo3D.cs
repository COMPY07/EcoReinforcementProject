

using UnityEngine;

public class PaperTileInfo3D : PaperTileInfo
{
    public float height;
    
    void Start()
    {
        
    }
    
    public void UpdateHeight(float newHeight)
    {
        height = newHeight;
        transform.position = new Vector3(transform.position.x, newHeight, transform.position.z);
    }
}