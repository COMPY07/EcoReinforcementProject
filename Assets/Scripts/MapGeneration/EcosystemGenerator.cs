
using UnityEngine;
using System.Collections.Generic;


[System.Serializable]
public class EcosystemData
{
    public int width;
    public int height;
    public float[,] heightMap;
    public int[,] biomeMap; // 0: 사막, 1: 초원, 2: 숲, 3: 물
    public List<AnimalSpawnData> animals;
    public List<PlantSpawnData> plants;
    
    public EcosystemData(int w, int h)
    {
        width = w;
        height = h;
        heightMap = new float[width, height];
        biomeMap = new int[width, height];
        animals = new List<AnimalSpawnData>();
        plants = new List<PlantSpawnData>();
    }
}

[System.Serializable]
public class AnimalSpawnData
{
    public Vector3 position;
    public int animalType; 
    public float health;
}

[System.Serializable]
public class PlantSpawnData
{
    public Vector3 position;
    public int plantType;
    public float growthStage;
}




public class EcosystemGenerator : MonoBehaviour
{
    [Header("맵 설정")]
    public int mapWidth = 100;
    public int mapHeight = 100;
    public float noiseScale = 0.1f;
    public float heightMultiplier = 10f;
    
    [Header("생태계 프리팹")]
    public GameObject[] animalPrefabs; 
    public GameObject[] plantPrefabs; 
    public Material[] biomeMaterials; 
    
    [Header("생성 밀도")]
    public float animalDensity = 0.02f;
    public float plantDensity = 0.1f;
    
    private EcosystemData currentEcosystem;
    private GameObject terrainObject;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    
    public EcosystemData GenerateEcosystem()
    {
        currentEcosystem = new EcosystemData(mapWidth, mapHeight);
        
        GenerateHeightMap();
        GenerateBiomeMap();
        GenerateTerrainMesh(); 
        SpawnPlants();
        SpawnAnimals();
        
        return currentEcosystem;
    }
    
    private void GenerateHeightMap()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                float noiseValue = Mathf.PerlinNoise(x * noiseScale, z * noiseScale);
                currentEcosystem.heightMap[x, z] = noiseValue * heightMultiplier;
            }
        }
    }
    
    private void GenerateBiomeMap()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                float height = currentEcosystem.heightMap[x, z];
                float moisture = Mathf.PerlinNoise(x * 0.05f, z * 0.05f);
                
                if (height < 2f)
                {
                    currentEcosystem.biomeMap[x, z] = 3; 
                }
                else if (moisture < 0.3f)
                {
                    currentEcosystem.biomeMap[x, z] = 0; 
                }
                else if (moisture < 0.6f)
                {
                    currentEcosystem.biomeMap[x, z] = 1; 
                }
                else
                {
                    currentEcosystem.biomeMap[x, z] = 2; 
                }
            }
        }
    }
    
    private void GenerateTerrainMesh()
    {
        if (terrainObject != null)
            DestroyImmediate(terrainObject);
            
        terrainObject = new GameObject("Generated Terrain");
        MeshFilter meshFilter = terrainObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = terrainObject.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[mapWidth * mapHeight];
        int[] triangles = new int[(mapWidth - 1) * (mapHeight - 1) * 6];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        
        int vertIndex = 0;
        for (int z = 0; z < mapHeight; z++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                vertices[vertIndex] = new Vector3(x, currentEcosystem.heightMap[x, z], z);
                uvs[vertIndex] = new Vector2((float)x / mapWidth, (float)z / mapHeight);
                
                int biome = currentEcosystem.biomeMap[x, z];
                switch (biome)
                {
                    case 0: colors[vertIndex] = Color.yellow; break;
                    case 1: colors[vertIndex] = Color.green; break;  
                    case 2: colors[vertIndex] = Color.black; break; 
                    case 3: colors[vertIndex] = Color.blue; break;   
                }
                vertIndex++;
            }
        }
        
        int triIndex = 0;
        for (int z = 0; z < mapHeight - 1; z++)
        {
            for (int x = 0; x < mapWidth - 1; x++)
            {
                int i = z * mapWidth + x;
                
                triangles[triIndex] = i;
                triangles[triIndex + 1] = i + mapWidth;
                triangles[triIndex + 2] = i + 1;
                
                triangles[triIndex + 3] = i + 1;
                triangles[triIndex + 4] = i + mapWidth;
                triangles[triIndex + 5] = i + mapWidth + 1;
                
                triIndex += 6;
            }
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        
        meshFilter.mesh = mesh;
        meshRenderer.material = biomeMaterials[0];
    }
    
    private void SpawnPlants()
    {
        ClearSpawnedObjects();
        
        for (int x = 0; x < mapWidth; x += 2)
        {
            for (int z = 0; z < mapHeight; z += 2)
            {
                if (Random.value < plantDensity)
                {
                    int biome = currentEcosystem.biomeMap[x, z];
                    if (biome == 3) continue;
                    
                    Vector3 position = new Vector3(x, currentEcosystem.heightMap[x, z], z);
                    int plantType = GetPlantTypeForBiome(biome);
                    
                    if (plantType >= 0 && plantType < plantPrefabs.Length && plantPrefabs[plantType] != null)
                    {
                        GameObject plant = Instantiate(plantPrefabs[plantType], position, Quaternion.identity);
                        spawnedObjects.Add(plant);
                        
                        PlantSpawnData plantData = new PlantSpawnData
                        {
                            position = position,
                            plantType = plantType,
                            growthStage = Random.Range(0.5f, 1.0f)
                        };
                        currentEcosystem.plants.Add(plantData);
                    }
                }
            }
        }
    }
    
    private void SpawnAnimals()
    {
        for (int x = 5; x < mapWidth; x += 10)
        {
            for (int z = 5; z < mapHeight; z += 10)
            {
                if (Random.value < animalDensity)
                {
                    int biome = currentEcosystem.biomeMap[x, z];
                    if (biome == 3) continue; 
                    
                    Vector3 position = new Vector3(x, currentEcosystem.heightMap[x, z] + 1, z);
                    int animalType = GetAnimalTypeForBiome(biome);
                    
                    if (animalType >= 0 && animalType < animalPrefabs.Length && animalPrefabs[animalType] != null)
                    {
                        GameObject animal = Instantiate(animalPrefabs[animalType], position, Quaternion.identity);
                        spawnedObjects.Add(animal);
                        
                        AnimalSpawnData animalData = new AnimalSpawnData
                        {
                            position = position,
                            animalType = animalType,
                            health = Random.Range(80f, 100f)
                        };
                        currentEcosystem.animals.Add(animalData);
                    }
                }
            }
        }
    }
    
    private int GetPlantTypeForBiome(int biome)
    {
        switch (biome)
        {
            case 0: return 1; 
            case 1: return 3; 
            case 2: return 0;
            default: return -1;
        }
    }
    
    private int GetAnimalTypeForBiome(int biome)
    {
        switch (biome)
        {
            case 0: return 0; 
            case 1: return 1;
            case 2: return 2; 
            default: return 3;
        }
    }
    
    private void ClearSpawnedObjects()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
                DestroyImmediate(obj);
        }
        spawnedObjects.Clear();
        
        if (currentEcosystem != null)
        {
            currentEcosystem.plants.Clear();
            currentEcosystem.animals.Clear();
        }
    }
}
