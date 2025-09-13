

using UnityEditor;
using UnityEngine;

public class StructureInfo3D : MonoBehaviour
{
    public StructureInstance structureInstance;
    public Vector2Int gridPosition;
    
    void Start()
    {
    }
    
    public void UpdateScale(float newScale)
    {
        structureInstance.scale = newScale;
        transform.localScale = Vector3.one * newScale;
    }
    
    public void UpdateRotation(float newRotation)
    {
        structureInstance.rotation = newRotation;
        transform.rotation = Quaternion.Euler(0, newRotation, 0);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ProceduralMapGenerator3D))]
public class ProceduralMapGenerator3DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ProceduralMapGenerator3D generator = (ProceduralMapGenerator3D)target;
        
        DrawDefaultInspector();
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Generate 3D Map"))
        {
            generator.GenerateMap();
        }
        
        if (GUILayout.Button("Clear Map"))
        {
            generator.ClearMap();
        }
        
        if (GUILayout.Button("Randomize Seed"))
        {
            generator.RandomizeSeed();
        }
        
        GUILayout.Space(10);
        
        if (generator.GetHeightMapResult() != null)
        {
            EditorGUILayout.HelpBox($"Height Map Generated!\n" +
                $"Min: {generator.GetHeightMapResult().minHeight:F2}\n" +
                $"Max: {generator.GetHeightMapResult().maxHeight:F2}\n" +
                $"Avg: {generator.GetHeightMapResult().averageHeight:F2}", 
                MessageType.Info);
        }
        //
        // if (generator.GetStructurePlacementResult() != null)
        // {
        //     string structureInfo = $"Structures Placed: {generator.GetStructurePlacementResult().totalPlaced}\n";
        //     foreach (var kvp in generator.GetStructurePlacementResult().structuresByType)
        //     {
        //         structureInfo += $"{kvp.Key}: {kvp.Value}\n";
        //     }
        //     
        //     EditorGUILayout.HelpBox(structureInfo, MessageType.Info);
        // }
        

    }
}
#endif