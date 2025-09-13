using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpeciesDataSource : MonoBehaviour, IDataSource
{
    [SerializeField] private string[] speciesNames;
    [SerializeField] private string[] foodNames;
    
    [Header("데이터 추적 설정")]
    public DataType dataType = DataType.TotalAgents;
    public string targetSpeciesName = "";
    public float updateInterval = 0.2f;
    
    private SimpleEcosystemManager manager;
    private bool isCollecting = false;
    
    public event Action<float> OnNewData;
    
    public enum DataType
    {
        TotalAgents,      
        SpecificSpecies, 
        TotalFood,      
        SpeciesRatio  
    }

    public void Awake()
    {
        manager = GameObject.FindAnyObjectByType<SimpleEcosystemManager>();
        if (manager == null) 
            Debug.LogError("SimpleEcosystemManager를 찾을 수 없습니다!");
    }

    public void StartCollection()
    {
        if (manager == null) return;
        
        isCollecting = true;
        StartCoroutine(CollectData());
        Debug.Log($"{GetSourceName()} 데이터 수집 시작");
    }

    public void StopCollection()
    {
        isCollecting = false;
        Debug.Log($"{GetSourceName()} 데이터 수집 중지");
    }

    public float GetCurrentValue()
    {
        if (manager == null) return 0f;
        
        switch (dataType)
        {
            case DataType.TotalAgents:
                return manager.AllAgents.Count;
                
            case DataType.SpecificSpecies:
                return GetSpeciesCount(targetSpeciesName);
                
            case DataType.TotalFood:
                return manager.ActiveFoods.Count;
                
            case DataType.SpeciesRatio:
                return GetSpeciesRatio(targetSpeciesName);
                
            default:
                return 0f;
        }
    }

    public string GetSourceName()
    {
        switch (dataType)
        {
            case DataType.TotalAgents:
                return "전체 개체수";
                
            case DataType.SpecificSpecies:
                return string.IsNullOrEmpty(targetSpeciesName) ? "특정 종" : targetSpeciesName;
                
            case DataType.TotalFood:
                return "음식 개수";
                
            case DataType.SpeciesRatio:
                return $"{targetSpeciesName} 비율";
                
            default:
                return "생태계 데이터";
        }
    }

    public IDataSource Clone()
    {
        GameObject cloneObj = new GameObject($"Clone_{gameObject.name}");
        SpeciesDataSource clone = cloneObj.AddComponent<SpeciesDataSource>();
    
        clone.speciesNames = (string[])speciesNames?.Clone();
        clone.foodNames = (string[])foodNames?.Clone();
        clone.dataType = dataType;
        clone.targetSpeciesName = targetSpeciesName;
        clone.updateInterval = updateInterval;
    
        clone.manager = GameObject.FindAnyObjectByType<SimpleEcosystemManager>();
    
        clone.isCollecting = false;
    
    
        return clone;
    }

    private IEnumerator CollectData()
    {
        while (isCollecting)
        {
            float currentValue = GetCurrentValue();
            OnNewData?.Invoke(currentValue);
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    
    private float GetSpeciesCount(string speciesName)
    {
        if (string.IsNullOrEmpty(speciesName) || manager.AllAgents == null)
            return 0f;
            
        
        int count = manager.AllAgents.Count(agent => 
            agent.name.Contains(speciesName) || 
            agent.gameObject.name.Contains(speciesName));
        return count;
    }
    
    private float GetSpeciesRatio(string speciesName)
    {
        if (manager.AllAgents == null || manager.AllAgents.Count == 0)
            return 0f;
            
        float speciesCount = GetSpeciesCount(speciesName);
        float totalCount = manager.AllAgents.Count;
        
        return (speciesCount / totalCount) * 100f; 
    }
    

    public Dictionary<string, int> GetAllSpeciesCounts()
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        
        if (manager?.AllAgents == null) return counts;
        
        foreach (string species in speciesNames)
        {
            counts[species] = (int)GetSpeciesCount(species);
        }
        
        return counts;
    }

    public string GetDominantSpecies()
    {
        var counts = GetAllSpeciesCounts();
        if (counts.Count == 0) return "없음";
        
        return counts.OrderByDescending(kvp => kvp.Value).First().Key;
    }
    
    public float GetEcosystemHealth()
    {
        var counts = GetAllSpeciesCounts();
        if (counts.Count == 0) return 0f;
        
        int activeSpecies = counts.Values.Count(count => count > 0);
        return (activeSpecies / (float)speciesNames.Length) * 100f;
    }
    
    
    [ContextMenu("전체 개체수 모드")]
    public void SetToTotalAgentsMode()
    {
        dataType = DataType.TotalAgents;
        Debug.Log("데이터 타입: 전체 개체수");
    }
    
    [ContextMenu("음식 개수 모드")]
    public void SetToFoodMode()
    {
        dataType = DataType.TotalFood;
        Debug.Log("데이터 타입: 음식 개수");
    }
    
    [ContextMenu("첫 번째 종 추적")]
    public void SetToFirstSpecies()
    {
        if (speciesNames != null && speciesNames.Length > 0)
        {
            dataType = DataType.SpecificSpecies;
            targetSpeciesName = speciesNames[0];
            Debug.Log($"데이터 타입: {targetSpeciesName} 개체수");
        }
    }
    
    
    [ContextMenu("현재 상태 출력")]
    public void PrintCurrentStatus()
    {
        if (manager == null)
        {
            Debug.Log("Manager가 없습니다!");
            return;
        }
        
        Debug.Log("=== 생태계 현재 상태 ===");
        Debug.Log($"전체 에이전트: {manager.AllAgents.Count}");
        Debug.Log($"전체 음식: {manager.ActiveFoods.Count}");
        Debug.Log($"현재 값: {GetCurrentValue()}");
        Debug.Log($"지배 종: {GetDominantSpecies()}");
        Debug.Log($"생태계 건강도: {GetEcosystemHealth():F1}%");
        
        var counts = GetAllSpeciesCounts();
        foreach (var kvp in counts)
        {
            Debug.Log($"- {kvp.Key}: {kvp.Value}마리");
        }
    }
    
    void OnValidate()
    {
        if (dataType == DataType.SpecificSpecies && speciesNames != null && speciesNames.Length > 0)
        {
            if (string.IsNullOrEmpty(targetSpeciesName))
            {
                targetSpeciesName = speciesNames[0];
            }
        }
    }
}
