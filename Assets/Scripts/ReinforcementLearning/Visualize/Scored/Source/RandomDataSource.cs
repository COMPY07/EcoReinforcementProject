using System;
using System.Collections;
using UnityEngine;

public class RandomDataSource : MonoBehaviour, IDataSource
{
    public event Action<float> OnNewData;
    
    [Header("랜덤 설정")]
    public float minValue = 0f;
    public float maxValue = 100f;
    public float updateInterval = 0.2f;
    public string sourceName = "Random";
    
    private bool isCollecting = false;
    
    public void StartCollection()
    {
        isCollecting = true;
        StartCoroutine(GenerateRandomData());
    }
    
    public void StopCollection()
    {
        isCollecting = false;
    }
    
    public float GetCurrentValue()
    {
        return UnityEngine.Random.Range(minValue, maxValue);
    }
    
    public string GetSourceName()
    {
        return sourceName;
    }

    public IDataSource Clone()
    {
        return null;
    }

    private IEnumerator GenerateRandomData()
    {
        while (isCollecting)
        {
            float randomValue = GetCurrentValue();
            OnNewData?.Invoke(randomValue);
            yield return new WaitForSeconds(updateInterval);
        }
    }
}