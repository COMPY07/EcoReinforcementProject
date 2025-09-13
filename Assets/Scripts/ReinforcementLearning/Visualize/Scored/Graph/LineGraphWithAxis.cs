using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using TMPro;

public class LineGraphWithAxis : BaseGraph
{
    [Header("선그래프 설정")]
    public bool showPoints = true;
    public bool showLines = true;
    
    [Header("축 표시기 설정")]
    public bool showAxisLabels = true;
    public bool showGridLines = true;
    public GameObject axisLabelPrefab;
    public GameObject gridLinePrefab;
    
    [Header("축 설정")]
    public int yAxisSteps = 5;
    public int xAxisSteps = 5;
    public Color gridColor = Color.gray;
    public Color axisColor = Color.white;
    public float gridLineThickness = 1f;
    
    private List<GameObject> points = new List<GameObject>();
    private List<GameObject> lines = new List<GameObject>();
    private List<GameObject> axisLabels = new List<GameObject>();
    private List<GameObject> gridLines = new List<GameObject>();
    
    private Vector2 actualContainerSize;
    private bool layoutUpdatePending = false;
    
    protected override void OnDataReceived(float newValue)
    {
        base.OnDataReceived(newValue);
        
        UpdateDisplay();
        
        if (!layoutUpdatePending)
        {
            layoutUpdatePending = true;
            StartCoroutine(UpdateGraphAfterLayout());
        }
    }
    
    private IEnumerator UpdateGraphAfterLayout()
    {
        yield return new WaitForEndOfFrame();
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(config.container.parent.GetComponent<RectTransform>());
        
        yield return new WaitForEndOfFrame();
        
        UpdateActualContainerSize();
        UpdateGraphVisuals();
        
        layoutUpdatePending = false;
    }
    
    private void UpdateActualContainerSize()
    {
        actualContainerSize = config.container.rect.size;
        
        
        if (actualContainerSize.x <= 0) actualContainerSize.x = 400f;
        if (actualContainerSize.y <= 0) actualContainerSize.y = 300f;
    }
    
    protected override void UpdateGraphVisuals()
    {
        UpdateActualContainerSize();
        
        if (actualContainerSize.x < 50f || actualContainerSize.y < 50f)
        {
            Debug.LogWarning("2");
            return;
        }
        
        ClearVisuals();
        
        if (showGridLines) DrawGridLines();
        if (showAxisLabels) DrawAxisLabels();
        
        DrawGraph();
        
        if (config.enableAnimation)
        {
            StartCoroutine(AnimateGraph());
        }
    }
    
    private void ClearVisuals()
    {
        ClearList(points);
        ClearList(lines);
        ClearList(axisLabels);
        ClearList(gridLines);
    }
    
    private void ClearList(List<GameObject> list)
    {
        foreach (var obj in list)
            if (obj != null) DestroyImmediate(obj);
        list.Clear();
    }
    
    private void DrawGridLines() {
        if (gridLinePrefab == null) return;
        
        float width = actualContainerSize.x;
        float height = actualContainerSize.y;
        
        for (int i = 0; i <= xAxisSteps; i++)
        {
            float x = (i / (float)xAxisSteps) * width - width * 0.5f;
            GameObject gridLine = CreateVerticalGridLine(x, height);
            gridLines.Add(gridLine);
        }
        
        for (int i = 0; i <= yAxisSteps; i++)
        {
            float y = (i / (float)yAxisSteps) * height - height * 0.5f;
            GameObject gridLine = CreateHorizontalGridLine(y, width);
            gridLines.Add(gridLine);
        }
    }
    
    private GameObject CreateVerticalGridLine(float xPos, float height)
    {
        GameObject line = Instantiate(gridLinePrefab, config.container);
        RectTransform rect = line.GetComponent<RectTransform>();
        
        rect.anchoredPosition = new Vector2(xPos, 0);
        rect.sizeDelta = new Vector2(gridLineThickness, height);
        
        Image img = line.GetComponent<Image>();
        if (img != null) img.color = gridColor;
        
        return line;
    }
    
    private GameObject CreateHorizontalGridLine(float yPos, float width)
    {
        GameObject line = Instantiate(gridLinePrefab, config.container);
        RectTransform rect = line.GetComponent<RectTransform>();
        
        rect.anchoredPosition = new Vector2(0, yPos);
        rect.sizeDelta = new Vector2(width, gridLineThickness);
        
        Image img = line.GetComponent<Image>();
        if (img != null) img.color = gridColor;
        
        return line;
    }
    
    private void DrawAxisLabels()
    {
        if (axisLabelPrefab == null) return;
        
        DrawXAxisLabels();
        DrawYAxisLabels();
    }
    
    private void DrawXAxisLabels()
    {
        float width = actualContainerSize.x;
        float height = actualContainerSize.y;
        
        for (int i = 0; i <= xAxisSteps; i++)
        {
            float x = (i / (float)xAxisSteps) * width - width * 0.5f;
            float timeValue = (config.maxPoints - 1) * config.updateInterval * (i / (float)xAxisSteps);
            string timeText = $"{timeValue:F1}s";
            
            GameObject label = CreateAxisLabel(new Vector2(x, -height * 0.5f - 20f), timeText);
            axisLabels.Add(label);
        }
    }
    
    private void DrawYAxisLabels()
    {
        float width = actualContainerSize.x;
        float height = actualContainerSize.y;
        
        for (int i = 0; i <= yAxisSteps; i++)
        {
            float y = (i / (float)yAxisSteps) * height - height * 0.5f;
            float value = Mathf.Lerp(config.minValue, config.maxValue, i / (float)yAxisSteps);
            string valueText = $"{value:F0}";
            
            GameObject label = CreateAxisLabel(new Vector2(-width * 0.5f - 30f, y), valueText);
            axisLabels.Add(label);
        }
    }
    
    private GameObject CreateAxisLabel(Vector2 position, string text)
    {
        GameObject label = Instantiate(axisLabelPrefab, config.container.parent);
        RectTransform rect = label.GetComponent<RectTransform>();
        
        rect.anchoredPosition = position;
        
        TMP_Text textComp = label.GetComponent<TMP_Text>();
        if (textComp != null)
        {
            textComp.text = text;
            textComp.color = axisColor;
            textComp.fontSize = 32;
        }
        
        return label;
    }
    
    private void DrawGraph()
    {
        if (dataList.Count == 0) return;
        
        if (showPoints) DrawPoints();
        if (showLines && dataList.Count > 1) DrawLines();
    }
    
    private void DrawPoints()
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            Vector2 position = ValueToActualPosition(i, dataList[i]);
            GameObject point = CreatePoint(position);
            points.Add(point);
        }
    }
    
    private void DrawLines()
    {
        for (int i = 0; i < dataList.Count - 1; i++)
        {
            Vector2 startPos = ValueToActualPosition(i, dataList[i]);
            Vector2 endPos = ValueToActualPosition(i + 1, dataList[i + 1]);
            GameObject line = CreateLine(startPos, endPos);
            lines.Add(line);
        }
    }
    
    private Vector2 ValueToActualPosition(int index, float value)
    {
        float width = actualContainerSize.x;
        float height = actualContainerSize.y;
        
        float xPos = (index / (float)(config.maxPoints - 1)) * width - width * 0.5f;
        
        float normalizedY = Mathf.InverseLerp(config.minValue, config.maxValue, value);
        float yPos = normalizedY * height - height * 0.5f;
        
        return new Vector2(xPos, yPos);
    }
    
    private GameObject CreatePoint(Vector2 position)
    {
        GameObject point = Instantiate(pointPrefab, config.container);
        RectTransform rect = point.GetComponent<RectTransform>();
        
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * config.pointSize;
        
        Image image = point.GetComponent<Image>();
        if (image != null) image.color = config.pointColor;
        
        return point;
    }
    
    private GameObject CreateLine(Vector2 startPos, Vector2 endPos)
    {
        GameObject line = Instantiate(linePrefab, config.container);
        RectTransform rect = line.GetComponent<RectTransform>();
        
        Vector2 direction = endPos - startPos;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        rect.anchoredPosition = (startPos + endPos) * 0.5f;
        rect.sizeDelta = new Vector2(distance, config.lineWidth);
        rect.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        Image image = line.GetComponent<Image>();
        
        if (image != null) image.color = config.lineColor;
        
        
        RawImage rawImage = line.GetComponent<RawImage>();
        
        if (rawImage != null) rawImage.color = config.lineColor;
        
        
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
        SetListAlpha(points, alpha);
        SetListAlpha(lines, alpha);
    }
    
    private void SetListAlpha(List<GameObject> list, float alpha)
    {
        foreach (var obj in list)
        {
            if (obj != null)
            {
                Image img = obj.GetComponent<Image>();
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
        return dataSource != null ? $"{dataSource.GetSourceName()}" : "Layout Safe Graph";
    }
    
    public override void ClearGraph()
    {
        ClearVisuals();
        base.ClearGraph();
    }
    
    
    [ContextMenu("크기 강제 갱신")]
    public void ForceUpdateSize()
    {
        UpdateActualContainerSize();
        UpdateGraphVisuals();
    }
    
    [ContextMenu("Layout 강제 리빌드")]
    public void ForceLayoutRebuild()
    {
        StartCoroutine(UpdateGraphAfterLayout());
    }
}