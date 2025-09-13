using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LineGraph : BaseGraph
{
    [Header("선그래프 전용 설정")]
    public bool showPoints = true;          // 점 표시 여부
    public bool showLines = true;           // 선 표시 여부
    public bool smoothLines = false;        // 부드러운 선 (미래 확장용)
    
    private List<GameObject> points = new List<GameObject>();
    private List<GameObject> lines = new List<GameObject>();
    
    protected override void OnDataReceived(float newValue)
    {
        base.OnDataReceived(newValue);
        UpdateGraphVisuals();
    }
    
    protected override void UpdateGraphVisuals()
    {
        ClearVisuals();
        DrawGraph();
        
        if (config.enableAnimation)
        {
            StartCoroutine(AnimateGraph());
        }
    }
    
    private void ClearVisuals()
    {
        foreach (var point in points)
        {
            if (point != null) DestroyImmediate(point);
        }
        points.Clear();
        
        foreach (var line in lines)
        {
            if (line != null) DestroyImmediate(line);
        }
        lines.Clear();
    }
    
    private void DrawGraph()
    {
        if (dataList.Count == 0) return;
        
        if (showPoints)
        {
            DrawPoints();
        }
        
        if (showLines && dataList.Count > 1)
        {
            DrawLines();
        }
    }
    
    private void DrawPoints()
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            Vector2 position = ValueToPosition(i, dataList[i]);
            GameObject point = CreatePoint(position);
            points.Add(point);
        }
    }
    
    private void DrawLines()
    {
        for (int i = 0; i < dataList.Count - 1; i++)
        {
            Vector2 startPos = ValueToPosition(i, dataList[i]);
            Vector2 endPos = ValueToPosition(i + 1, dataList[i + 1]);
            GameObject line = CreateLine(startPos, endPos);
            lines.Add(line);
        }
    }
    
    private GameObject CreatePoint(Vector2 position)
    {
        GameObject point = Instantiate(pointPrefab, config.container);
        RectTransform rectTransform = point.GetComponent<RectTransform>();
        
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = Vector2.one * config.pointSize;
        
        Image image = point.GetComponent<Image>();
        if (image != null)
        {
            image.color = config.pointColor;
        }
        
        return point;
    }
    
    private GameObject CreateLine(Vector2 startPos, Vector2 endPos)
    {
        GameObject line = Instantiate(linePrefab, config.container);
        RectTransform rectTransform = line.GetComponent<RectTransform>();
        
        Vector2 centerPos = (startPos + endPos) * 0.5f;
        rectTransform.anchoredPosition = centerPos;
        
        Vector2 direction = endPos - startPos;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        rectTransform.sizeDelta = new Vector2(distance, config.lineWidth);
        rectTransform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        Image image = line.GetComponent<Image>();
        if (image != null)
        {
            image.color = config.lineColor;
        }
        
        return line;
    }
    
    private IEnumerator AnimateGraph()
    {
        SetElementsAlpha(0f);
        
        float elapsed = 0f;
        while (elapsed < config.animDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = elapsed / config.animDuration;
            SetElementsAlpha(alpha);
            yield return null;
        }
        
        SetElementsAlpha(1f);
    }
    
    private void SetElementsAlpha(float alpha)
    {
        foreach (var point in points)
        {
            if (point != null)
            {
                Image img = point.GetComponent<Image>();
                if (img != null)
                {
                    Color color = img.color;
                    color.a = alpha;
                    img.color = color;
                }
            }
        }
        
        foreach (var line in lines)
        {
            if (line != null)
            {
                Image img = line.GetComponent<Image>();
                if (img != null)
                {
                    Color color = img.color;
                    color.a = alpha;
                    img.color = color;
                }
            }
        }
    }
    
    protected override string GetGraphTitle()
    {
        return dataSource != null ? $"{dataSource.GetSourceName()} Graph" : "Line Graph";
    }
    
    public override void ClearGraph()
    {
        ClearVisuals();
        base.ClearGraph();
    }
}