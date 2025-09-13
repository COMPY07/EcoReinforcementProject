using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EvaluationReport
{
    public string className;
    public float overallScore;
    public Dictionary<string, float> categoryScores;
    public Dictionary<string, float> formulaResults;
    public List<string> strengths;
    public List<string> weaknesses;
    public List<string> recommendations;
    public Dictionary<string, object> metrics;
    
    public EvaluationReport()
    {
        categoryScores = new Dictionary<string, float>();
        formulaResults = new Dictionary<string, float>();
        strengths = new List<string>();
        weaknesses = new List<string>();
        recommendations = new List<string>();
        metrics = new Dictionary<string, object>();
    }
}

[CreateAssetMenu(fileName = "EcosystemEvaluationReport", menuName = "Evaluation/Ecosystem Report")]
public class EcosystemEvaluationReport : ScriptableObject
{
    public List<EvaluationReport> reports = new List<EvaluationReport>();
    public string generationDate;
    public float comparativeAnalysisScore;
    public string summary;
}