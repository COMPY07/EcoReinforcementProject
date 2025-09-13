using System;
using TMPro;
using UnityEngine;

public interface IDataSource
{
    event Action<float> OnNewData;
    
    void StartCollection();
    void StopCollection();
    
    float GetCurrentValue();
    
    string GetSourceName();

    IDataSource Clone();
}

// ==================== 2. 그래프 설정 데이터 ====================

[System.Serializable]
public class GraphConfig
{
    [Header("그래프 설정")]
    public float maxValue = 100f;         
    public float minValue = 0f;          
    public int maxPoints = 50;              
    public float updateInterval = 0.1f;    
    
    [Header("UI 참조")]
    public RectTransform container;    
    public TMP_Text titleText;            
    public TMP_Text valueText;          
    
    [Header("비주얼")]
    public Color lineColor = Color.white;  
    public Color pointColor = Color.red;   
    public float lineWidth = 2f;       
    public float pointSize = 6f;      
    
    [Header("애니메이션")]
    public bool enableAnimation = true;     
    public float animDuration = 0.3f;     
}