[System.Serializable]
public class WFCHeightMapData
{
    public string mapName;
    public int width;
    public int height;
    public float[] heightValues; 
    public float heightScale;
    public BiomeType biome;
    
    public WFCHeightMapData(string name, float[,] heightMap, float scale, BiomeType biomeType)
    {
        mapName = name;
        width = heightMap.GetLength(0);
        height = heightMap.GetLength(1);
        heightScale = scale;
        biome = biomeType;
        
        heightValues = new float[width * height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                heightValues[x * height + y] = heightMap[x, y];
            }
        }
    }
    
    public float[,] GetHeightMap2D()
    {
        float[,] heightMap = new float[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                heightMap[x, y] = heightValues[x * height + y];
            }
        }
        return heightMap;
    }
}