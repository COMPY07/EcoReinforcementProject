using UnityEngine;

public class ResourceDistributionTracker : MonoBehaviour
    {
        [Header("Distribution Tracking")]
        public bool enableHeatmapGeneration = true;
        public float trackingRadius = 20f;
        public int gridResolution = 10;
        
        private float[,] accessHeatmap;
        private float[,] competitionHeatmap;
        
        private void Start()
        {
            if (enableHeatmapGeneration)
            {
                accessHeatmap = new float[gridResolution, gridResolution];
                competitionHeatmap = new float[gridResolution, gridResolution];
            }
        }
        
        public void UpdateCompetitionData(int competitorCount, float timeSinceAccess)
        {
            if (!enableHeatmapGeneration) return;
            
            Vector2 gridPos = WorldToGridPosition(transform.position);
            int x = Mathf.FloorToInt(gridPos.x);
            int y = Mathf.FloorToInt(gridPos.y);
            
            if (x >= 0 && x < gridResolution && y >= 0 && y < gridResolution)
            {
                accessHeatmap[x, y] += Time.deltaTime;
                competitionHeatmap[x, y] = Mathf.Max(competitionHeatmap[x, y], competitorCount);
            }
        }
        
        private Vector2 WorldToGridPosition(Vector3 worldPos)
        {
            Vector2 relativePos = new Vector2(worldPos.x, worldPos.z);
            relativePos += Vector2.one * trackingRadius / 2f; // Center the grid
            relativePos /= trackingRadius; // Normalize to 0-1
            relativePos *= gridResolution; // Scale to grid
            
            return relativePos;
        }
        
        public float[,] GetAccessHeatmap() => accessHeatmap;
        public float[,] GetCompetitionHeatmap() => competitionHeatmap;
    }
    

    #region Data Structures
    [System.Serializable]
    public enum ResourceCategory
    {
        Consumable,    
        Shelter,       
        Tool,       
        Territory,    
        Information    
    }

    [System.Serializable]
    public class ResourceConsumptionResult
    {
        public bool success;
        public float amountConsumed;
        public float energyGained;
        public float healthGained;
        public float qualityModifier = 1f;
        public float freshnessModifier = 1f;
        public string reason;
    }

    [System.Serializable]
    public class CompetitionData
    {
        public int recentConsumerCount;
        public float accessFrequency;
        public float timeSinceLastAccess;
        public bool isHighlyCompeted;
    }

    [System.Serializable]
    public class ResourceAnalyticsData
    {
        public string resourceType;
        public string resourceSubType;
        public float currentAmount;
        public float maxAmount;
        public float currentFreshness;
        public float qualityModifier;
        public float regenerationRate;
        public CompetitionData competitionData;
        public Vector3 position;
        public float age;
        public bool isAvailable;
        public float environmentalStress;
    }
    #endregion