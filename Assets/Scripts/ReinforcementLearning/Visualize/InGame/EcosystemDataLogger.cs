using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using System.Linq;

public class EcosystemDataLogger : MonoBehaviour
{
    [Header("Logging Configuration")]
    public float logInterval = 10f; 
    public bool enableLogging = true;
    public bool logPositions = true; 
    public string fileName = "ecosystem_data";
    
    [Header("CSV Settings")]
    public bool useCommaDelimiter = true; // true: CSV, false: TSV
    
    private SimpleEcosystemManager ecosystemManager;
    private float lastLogTime;
    private int logCount = 0;
    private string fullFilePath;
    private List<EcosystemDataEntry> dataEntries = new List<EcosystemDataEntry>();
    
    [System.Serializable]
    public class EcosystemDataEntry
    {
        public float timestamp;
        public int logIndex;
        public Dictionary<string, SpeciesData> speciesData;
        
        public EcosystemDataEntry()
        {
            speciesData = new Dictionary<string, SpeciesData>();
        }
    }
    
    [System.Serializable]
    public class SpeciesData
    {
        public string speciesName;
        public int population;
        public float avgHealth;
        public float avgEnergy;
        public float avgAge;
        public List<Vector3> positions;
        public Vector3 centerOfMass;
        public float spreadRadius;
        
        public SpeciesData()
        {
            positions = new List<Vector3>();
        }
    }
    
    private void Start()
    {
        ecosystemManager = FindObjectOfType<SimpleEcosystemManager>();
        
        if (ecosystemManager == null)
        {
            Debug.LogError("EcosystemDataLogger: SimpleEcosystemManager not found!");
            enabled = false;
            return;
        }
        
        InitializeLogging();
    }
    
    private void InitializeLogging()
    {
        string datasFolderPath = Path.Combine(Application.dataPath, "datas");
        if (!Directory.Exists(datasFolderPath))
        {
            Directory.CreateDirectory(datasFolderPath);
            Debug.Log($"Created datas folder: {datasFolderPath}");
        }
        
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string extension = useCommaDelimiter ? ".csv" : ".tsv";
        string fullFileName = $"{fileName}_{timestamp}{extension}";
        
        fullFilePath = Path.Combine(datasFolderPath, fullFileName);
        
        WriteHeaders();
        
        Debug.Log($"EcosystemDataLogger initialized. Logging to: {fullFilePath}");
        Debug.Log($"Log interval: {logInterval} seconds");
    }
    
    private void WriteHeaders()
    {
        StringBuilder header = new StringBuilder();
        string delimiter = useCommaDelimiter ? "," : "\t";
        
        header.Append("Timestamp").Append(delimiter);
        header.Append("LogIndex").Append(delimiter);
        header.Append("TotalPopulation").Append(delimiter);
        header.Append("BiodiversityIndex").Append(delimiter);
        header.Append("FoodCount").Append(delimiter);
        header.Append("PredatorCount");
        
        var speciesConfigs = ecosystemManager.speciesConfigs;
        foreach (var config in speciesConfigs)
        {
            string speciesName = config.speciesData.speciesName;
            
            header.Append(delimiter).Append($"{speciesName}_Population");
            header.Append(delimiter).Append($"{speciesName}_AvgHealth");
            header.Append(delimiter).Append($"{speciesName}_AvgEnergy");
            header.Append(delimiter).Append($"{speciesName}_AvgAge");
            header.Append(delimiter).Append($"{speciesName}_CenterX");
            header.Append(delimiter).Append($"{speciesName}_CenterY");
            header.Append(delimiter).Append($"{speciesName}_CenterZ");
            header.Append(delimiter).Append($"{speciesName}_SpreadRadius");
            
            if (logPositions)
            {
                header.Append(delimiter).Append($"{speciesName}_Positions");
            }
        }
        
        try
        {
            File.WriteAllText(fullFilePath, header.ToString() + "\n", Encoding.UTF8);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to write CSV header: {e.Message}");
            enableLogging = false;
        }
    }
    
    private void Update()
    {
        if (!enableLogging || ecosystemManager == null) return;
        
        if (Time.time - lastLogTime >= logInterval)
        {
            LogEcosystemData();
            lastLogTime = Time.time;
        }
    }
    
    private void LogEcosystemData()
    {
        try
        {
            var dataEntry = CollectEcosystemData();
            dataEntries.Add(dataEntry);
            
            WriteDataToCSV(dataEntry);
            
            logCount++;
            Debug.Log($"Logged ecosystem data #{logCount} at timestamp {dataEntry.timestamp:F2}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to log ecosystem data: {e.Message}");
        }
    }
    
    private EcosystemDataEntry CollectEcosystemData()
    {
        var dataEntry = new EcosystemDataEntry
        {
            timestamp = Time.time,
            logIndex = logCount
        };
        
        var populations = ecosystemManager.GetSpeciesPopulations();
        
        foreach (var kvp in populations)
        {
            string speciesName = kvp.Key;
            var agents = GetAgentsOfSpecies(speciesName);
            
            var speciesData = new SpeciesData
            {
                speciesName = speciesName,
                population = kvp.Value
            };
            
            if (agents.Count > 0)
            {
                var aliveAgents = agents.Where(a => a != null && a.IsAlive).ToList();
                
                if (aliveAgents.Count > 0)
                {
                    speciesData.avgHealth = aliveAgents.Average(a => a.health);
                    speciesData.avgEnergy = aliveAgents.Average(a => a.energy);
                    speciesData.avgAge = aliveAgents.Average(a => a.age);
                    
                    speciesData.positions = aliveAgents.Select(a => a.transform.position).ToList();
                    speciesData.centerOfMass = CalculateCenterOfMass(speciesData.positions);
                    speciesData.spreadRadius = CalculateSpreadRadius(speciesData.positions, speciesData.centerOfMass);
                }
            }
            
            dataEntry.speciesData[speciesName] = speciesData;
        }
        
        return dataEntry;
    }
    
    private List<SimpleEcoAgent> GetAgentsOfSpecies(string speciesName)
    {
        var allAgents = FindObjectsOfType<SimpleEcoAgent>();
        return allAgents.Where(a => a.species != null && a.species.speciesName == speciesName).ToList();
    }
    
    private Vector3 CalculateCenterOfMass(List<Vector3> positions)
    {
        if (positions.Count == 0) return Vector3.zero;
        
        Vector3 sum = Vector3.zero;
        foreach (var pos in positions)
        {
            sum += pos;
        }
        
        return sum / positions.Count;
    }
    
    private float CalculateSpreadRadius(List<Vector3> positions, Vector3 center)
    {
        if (positions.Count == 0) return 0f;
        
        float maxDistance = 0f;
        foreach (var pos in positions)
        {
            float distance = Vector3.Distance(pos, center);
            if (distance > maxDistance)
            {
                maxDistance = distance;
            }
        }
        
        return maxDistance;
    }
    
    private void WriteDataToCSV(EcosystemDataEntry dataEntry)
    {
        StringBuilder csvLine = new StringBuilder();
        string delimiter = useCommaDelimiter ? "," : "\t";
        
        csvLine.Append(dataEntry.timestamp.ToString("F2")).Append(delimiter);
        csvLine.Append(dataEntry.logIndex).Append(delimiter);
        csvLine.Append(ecosystemManager.GetTotalPopulation()).Append(delimiter);
        csvLine.Append(ecosystemManager.GetBiodiversityIndex().ToString("F3")).Append(delimiter);
        
        var foodCount = FindObjectsOfType<GameObject>().Count(go => go.name.Contains("Bug"));
        // var predatorCount = FindObjectsOfType<GameObject>().Count(go => go.name.Contains("Predator"));
        
        csvLine.Append(foodCount).Append(delimiter);
        // csvLine.Append(predatorCount);
        
        var speciesConfigs = ecosystemManager.speciesConfigs;
        foreach (var config in speciesConfigs)
        {
            string speciesName = config.speciesData.speciesName;
            
            if (dataEntry.speciesData.ContainsKey(speciesName))
            {
                var speciesData = dataEntry.speciesData[speciesName];
                
                csvLine.Append(delimiter).Append(speciesData.population);
                csvLine.Append(delimiter).Append(speciesData.avgHealth.ToString("F2"));
                csvLine.Append(delimiter).Append(speciesData.avgEnergy.ToString("F2"));
                csvLine.Append(delimiter).Append(speciesData.avgAge.ToString("F2"));
                csvLine.Append(delimiter).Append(speciesData.centerOfMass.x.ToString("F2"));
                csvLine.Append(delimiter).Append(speciesData.centerOfMass.y.ToString("F2"));
                csvLine.Append(delimiter).Append(speciesData.centerOfMass.z.ToString("F2"));
                csvLine.Append(delimiter).Append(speciesData.spreadRadius.ToString("F2"));
                
                if (logPositions)
                {
                    string positionsString = string.Join("|", 
                        speciesData.positions.Select(p => $"{p.x:F1};{p.y:F1};{p.z:F1}"));
                    csvLine.Append(delimiter).Append($"\"{positionsString}\"");
                }
            }
            else
            {
                csvLine.Append(delimiter).Append("0"); // population
                csvLine.Append(delimiter).Append("0"); // avgHealth
                csvLine.Append(delimiter).Append("0"); // avgEnergy
                csvLine.Append(delimiter).Append("0"); // avgAge
                csvLine.Append(delimiter).Append("0"); // centerX
                csvLine.Append(delimiter).Append("0"); // centerY  
                csvLine.Append(delimiter).Append("0"); // centerZ
                csvLine.Append(delimiter).Append("0"); // spreadRadius
                
                if (logPositions)
                {
                    csvLine.Append(delimiter).Append("\"\""); // positions
                }
            }
        }
        
        File.AppendAllText(fullFilePath, csvLine.ToString() + "\n", Encoding.UTF8);
    }
    
    [ContextMenu("Force Log Now")]
    public void ForceLogNow()
    {
        if (enableLogging && ecosystemManager != null)
        {
            LogEcosystemData();
        }
    }
    
    [ContextMenu("Export Summary")]
    public void ExportSummary()
    {
        if (dataEntries.Count == 0)
        {
            Debug.LogWarning("No data to export summary");
            return;
        }
        
        try
        {
            string summaryPath = fullFilePath.Replace(".csv", "_summary.txt").Replace(".tsv", "_summary.txt");
            
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("=== Ecosystem Simulation Summary ===");
            summary.AppendLine($"Total Log Entries: {dataEntries.Count}");
            summary.AppendLine($"Simulation Duration: {dataEntries.Last().timestamp:F2} seconds");
            summary.AppendLine($"Log Interval: {logInterval} seconds");
            summary.AppendLine();
            
            var speciesNames = dataEntries.First().speciesData.Keys.ToList();
            float avgPop;
            foreach (string speciesName in speciesNames)
            {
                var speciesDataList = dataEntries.Select(e => e.speciesData[speciesName]).ToList();
                
                int maxPop = speciesDataList.Max(s => s.population);
                int minPop = speciesDataList.Min(s => s.population);
                avgPop = (float)speciesDataList.Average(s => s.population);
                
                summary.AppendLine($"=== {speciesName} ===");
                summary.AppendLine($"Population - Max: {maxPop}, Min: {minPop}, Avg: {avgPop:F1}");
                
                if (speciesDataList.Any(s => s.population > 0))
                {
                    var nonZeroEntries = speciesDataList.Where(s => s.population > 0).ToList();
                    float avgHealth = nonZeroEntries.Average(s => s.avgHealth);
                    float avgEnergy = nonZeroEntries.Average(s => s.avgEnergy);
                    float avgAge = nonZeroEntries.Average(s => s.avgAge);
                    
                    summary.AppendLine($"Avg Health: {avgHealth:F2}");
                    summary.AppendLine($"Avg Energy: {avgEnergy:F2}");
                    summary.AppendLine($"Avg Age: {avgAge:F2}");
                }
                
                summary.AppendLine();
            }
            
            File.WriteAllText(summaryPath, summary.ToString(), Encoding.UTF8);
            Debug.Log($"Summary exported to: {summaryPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to export summary: {e.Message}");
        }
    }
    
    public void SetLoggingEnabled(bool enabled)
    {
        enableLogging = enabled;
        Debug.Log($"Ecosystem logging {(enabled ? "enabled" : "disabled")}");
    }
    
    public void SetLogInterval(float interval)
    {
        logInterval = Mathf.Max(0.1f, interval);
        Debug.Log($"Log interval set to {logInterval} seconds");
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && enableLogging)
        {
            ExportSummary();
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && enableLogging)
        {
            ExportSummary();
        }
    }
    
    private void OnDestroy()
    {
        if (enableLogging && dataEntries.Count > 0)
        {
            ExportSummary();
        }
    }
}