using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public abstract class BaseGraph : MonoBehaviour
{
    [SerializeField] protected GraphConfig config;
    [SerializeField] protected GameObject pointPrefab;   
    [SerializeField] protected GameObject linePrefab;    
    
    protected IDataSource dataSource;                  
    protected List<float> dataList = new List<float>();
    protected List<GameObject> graphObjects = new List<GameObject>();
    protected bool isActive = false;                  
    
    
    public virtual void Initialize(IDataSource source)
    {
        dataSource = source.Clone();
        SetupGraph();
        ConnectDataSource();
        OnInitialized();
    }
    
    public virtual void StartUpdating()
    {
        if (!isActive)
        {
            isActive = true;
            dataSource?.StartCollection();
            StartCoroutine(UpdateLoop());
        }
    }
    
    public virtual void StopUpdating()
    {
        isActive = false;
        dataSource?.StopCollection();
    }
    
    public virtual void ClearGraph()
    {
        foreach (var obj in graphObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        graphObjects.Clear();
        dataList.Clear();
    }
    
    
    protected virtual void SetupGraph()
    {
        if (config.titleText != null)
            config.titleText.text = GetGraphTitle();
        ClearGraph();
    }
    
    protected virtual void ConnectDataSource()
    {
        if (dataSource != null)
        {
            dataSource.OnNewData += OnDataReceived;
        }
    }
    
    protected virtual void OnDataReceived(float newValue)
    {
        AddDataPoint(newValue);
        UpdateDisplay();
    }
    
    protected virtual void AddDataPoint(float value)
    {
        dataList.Add(value);
        
        if (dataList.Count > config.maxPoints)
        {
            dataList.RemoveAt(0);
        }
    }
    
    protected virtual void UpdateDisplay()
    {
        if (config.valueText != null && dataList.Count > 0)
        {
            float currentValue = dataList[dataList.Count - 1];
            config.valueText.text = $"{currentValue:F1}";
        }
    }
    
    protected virtual IEnumerator UpdateLoop()
    {
        while (isActive)
        {
            yield return new WaitForSeconds(config.updateInterval);
            // 여따가 좀 예쁘장한거 넣어보자
        }
    }
    
    protected abstract void UpdateGraphVisuals();
    
    protected abstract string GetGraphTitle();
    
    protected virtual void OnInitialized() { }
    
    protected Vector2 ValueToPosition(int index, float value)
    {
        float width = config.container.rect.width;
        float height = config.container.rect.height;
        
        float xPos = (index / (float)(config.maxPoints - 1)) * width - width * 0.5f;
        
        float normalizedY = Mathf.InverseLerp(config.minValue, config.maxValue, value);
        float yPos = normalizedY * height - height * 0.5f;
        
        return new Vector2(xPos, yPos);
    }
    
    protected virtual void OnDestroy()
    {
        if (dataSource != null)
        {
            dataSource.OnNewData -= OnDataReceived;
        }
    }
}
