using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class EcosystemSaveLoad : MonoBehaviour
{
    private EcosystemGenerator generator;
    
    void Start()
    {
        generator = GetComponent<EcosystemGenerator>();
    }
    
    public void SaveEcosystem(EcosystemData ecosystem, string fileName)
    {
        try
        {
            SerializableEcosystemData saveData = new SerializableEcosystemData();
            saveData.width = ecosystem.width;
            saveData.height = ecosystem.height;
            
            saveData.heightMapData = new float[ecosystem.width * ecosystem.height];
            saveData.biomeMapData = new int[ecosystem.width * ecosystem.height];
            
            for (int x = 0; x < ecosystem.width; x++)
            {
                for (int z = 0; z < ecosystem.height; z++)
                {
                    int index = x * ecosystem.height + z;
                    saveData.heightMapData[index] = ecosystem.heightMap[x, z];
                    saveData.biomeMapData[index] = ecosystem.biomeMap[x, z];
                }
            }
            
            saveData.animals = ecosystem.animals;
            saveData.plants = ecosystem.plants;
            
            string json = JsonUtility.ToJson(saveData, true);
            string filePath = Path.Combine(Application.persistentDataPath, fileName + ".json");
            File.WriteAllText(filePath, json);
            
            Debug.Log($"생태계가 저장되었습니다: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"저장 실패: {e.Message}");
        }
    }
    
    public EcosystemData LoadEcosystem(string fileName) {
        try {
            string filePath = Path.Combine(Application.persistentDataPath, fileName + ".json");
            
            if (!File.Exists(filePath))
            {
                Debug.LogError($"파일을 찾을 수 없습니다: {filePath}");
                return null;
            }
            
            string json = File.ReadAllText(filePath);
            SerializableEcosystemData saveData = JsonUtility.FromJson<SerializableEcosystemData>(json);
            
            EcosystemData ecosystem = new EcosystemData(saveData.width, saveData.height);
            
            for (int x = 0; x < saveData.width; x++)
            {
                for (int z = 0; z < saveData.height; z++)
                {
                    int index = x * saveData.height + z;
                    ecosystem.heightMap[x, z] = saveData.heightMapData[index];
                    ecosystem.biomeMap[x, z] = saveData.biomeMapData[index];
                }
            }
            
            ecosystem.animals = saveData.animals ?? new List<AnimalSpawnData>();
            ecosystem.plants = saveData.plants ?? new List<PlantSpawnData>();
            
            Debug.Log($"생태계가 로드되었습니다: {filePath}");
            return ecosystem;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"로드 실패: {e.Message}");
            return null;
        }
    }
    
    public void LoadAndApplyEcosystem(string fileName)
    {
        EcosystemData ecosystem = LoadEcosystem(fileName);
        if (ecosystem != null && generator != null)
        {
            ApplyEcosystemData(ecosystem);
        }
    }
    
    private void ApplyEcosystemData(EcosystemData ecosystem)
    {
        ClearExistingObjects();
        
        generator.mapWidth = ecosystem.width;
        generator.mapHeight = ecosystem.height;
        
        typeof(EcosystemGenerator)
            .GetField("currentEcosystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(generator, ecosystem);
            
        System.Reflection.MethodInfo generateTerrainMethod = typeof(EcosystemGenerator)
            .GetMethod("GenerateTerrainMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        generateTerrainMethod?.Invoke(generator, null);
        
        RestorePlants(ecosystem);
        RestoreAnimals(ecosystem);
    }
    
    private void RestorePlants(EcosystemData ecosystem)
    {
        foreach (PlantSpawnData plantData in ecosystem.plants)
        {
            if (plantData.plantType >= 0 && plantData.plantType < generator.plantPrefabs.Length 
                && generator.plantPrefabs[plantData.plantType] != null)
            {
                GameObject plant = Instantiate(generator.plantPrefabs[plantData.plantType], 
                                             plantData.position, Quaternion.identity);
                
                plant.transform.localScale *= plantData.growthStage;
            }
        }
    }
    
    private void RestoreAnimals(EcosystemData ecosystem)
    {
        foreach (AnimalSpawnData animalData in ecosystem.animals)
        {
            if (animalData.animalType >= 0 && animalData.animalType < generator.animalPrefabs.Length 
                && generator.animalPrefabs[animalData.animalType] != null)
            {
                GameObject animal = Instantiate(generator.animalPrefabs[animalData.animalType], 
                                              animalData.position, Quaternion.identity);
                
            }
        }
    }
    
    private void ClearExistingObjects()
    {
        GameObject[] plants = GameObject.FindGameObjectsWithTag("Plant");
        GameObject[] animals = GameObject.FindGameObjectsWithTag("Animal");
        GameObject terrain = GameObject.Find("Generated Terrain");
        
        foreach (GameObject plant in plants)
            DestroyImmediate(plant);
            
        foreach (GameObject animal in animals)
            DestroyImmediate(animal);
            
        if (terrain != null)
            DestroyImmediate(terrain);
    }
    
    public string[] GetSavedEcosystems()
    {
        string[] files = Directory.GetFiles(Application.persistentDataPath, "*.json");
        List<string> ecosystemFiles = new List<string>();
        
        foreach (string file in files)
        {
            ecosystemFiles.Add(Path.GetFileNameWithoutExtension(file));
        }
        
        return ecosystemFiles.ToArray();
    }
}

[System.Serializable]
public class SerializableEcosystemData
{
    public int width;
    public int height;
    public float[] heightMapData;
    public int[] biomeMapData;
    public List<AnimalSpawnData> animals;
    public List<PlantSpawnData> plants;
}