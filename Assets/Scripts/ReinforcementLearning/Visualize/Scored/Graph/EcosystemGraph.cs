using UnityEngine;

public class EcosystemGraph : MonoBehaviour
{
    [Header("그래프들")]
    public LineGraph totalAgentsGraph;
    public LineGraph foodGraph;
    public LineGraphWithAxis species1Graph;
    public LineGraphWithAxis species2Graph;
    
    [Header("데이터 소스들")]
    public SpeciesDataSource totalAgentsSource;
    public SpeciesDataSource foodSource;
    public SpeciesDataSource species1Source;
    public SpeciesDataSource species2Source;
    
    [Header("설정")]
    public string[] trackSpecies;
    
    void Start()
    {
        SetupGraphs();
    }
    
    void SetupGraphs()
    {
        if (totalAgentsGraph && totalAgentsSource)
        {
            totalAgentsSource.dataType = SpeciesDataSource.DataType.TotalAgents;
            totalAgentsGraph.Initialize(totalAgentsSource);
            totalAgentsGraph.StartUpdating();
        }
        
        if (foodGraph && foodSource)
        {
            foodSource.dataType = SpeciesDataSource.DataType.TotalFood;
            foodGraph.Initialize(foodSource);
            foodGraph.StartUpdating();
        }
        
        if (species1Graph && species1Source && trackSpecies.Length > 0)
        {
            species1Source.dataType = SpeciesDataSource.DataType.SpecificSpecies;
            species1Source.targetSpeciesName = trackSpecies[0];
            species1Graph.Initialize(species1Source);
            species1Graph.StartUpdating();
        }
        
        if (species2Graph && species2Source && trackSpecies.Length > 1)
        {
            species2Source.dataType = SpeciesDataSource.DataType.SpecificSpecies;
            species2Source.targetSpeciesName = trackSpecies[1];
            species2Graph.Initialize(species2Source);
            species2Graph.StartUpdating();
        }
        
        Debug.Log("생태계 그래프 시스템 초기화 완료!");
    }
    
    [ContextMenu("그래프 재시작")]
    public void RestartGraphs()
    {
        StopAllGraphs();
        SetupGraphs();
    }
    
    void StopAllGraphs()
    {
        totalAgentsGraph?.StopUpdating();
        foodGraph?.StopUpdating();
        species1Graph?.StopUpdating();
        species2Graph?.StopUpdating();
    }
    
    void OnDestroy()
    {
        StopAllGraphs();
    }
}