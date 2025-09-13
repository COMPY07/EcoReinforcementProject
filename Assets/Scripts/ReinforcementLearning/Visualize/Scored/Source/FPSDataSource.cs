using System;
using System.Collections;
using UnityEngine;

public class FPSDataSource : MonoBehaviour, IDataSource
{
    public event Action<float> OnNewData;
    
    private bool isCollecting = false;
    private float updateInterval = 0.1f;
    
    public void StartCollection()
    {
        isCollecting = true;
        StartCoroutine(CollectFPS());
    }
    
    public void StopCollection()
    {
        isCollecting = false;
    }
    
    public float GetCurrentValue()
    {
        return 1f / Time.deltaTime;
    }
    
    public string GetSourceName()
    {
        return "FPS";
    }

    public IDataSource Clone()
    {
        return null;
    }

    private IEnumerator CollectFPS()
    {
        while (isCollecting)
        {
            float fps = GetCurrentValue();
            OnNewData?.Invoke(fps);
            yield return new WaitForSeconds(updateInterval);
        }
    }
}