using Unity.MLAgents;
using UnityEngine;

public class WFCTrainingManager : MonoBehaviour
{
    [Header("Training Configuration")]
    public WFCRLAgent agent;
    public WFCTrainingEnvironment environment;
    
    [Header("Training Monitoring")]
    public bool enableLogging = true;
    public int logInterval = 100;
    public UnityEngine.UI.Text statusText;
    
    private int currentEpisode = 0;
    private float totalTrainingTime = 0f;
    private System.Diagnostics.Stopwatch trainingTimer;
    
    void Start()
    {
        if (agent == null)
            agent = FindObjectOfType<WFCRLAgent>();
        
        if (environment == null)
            environment = FindObjectOfType<WFCTrainingEnvironment>();
        
        trainingTimer = System.Diagnostics.Stopwatch.StartNew();
        
        Academy.Instance.AgentPreStep += OnAgentPreStep;
    }
    
    private void OnAgentPreStep(int academyStepCount)
    {
        totalTrainingTime = (float)trainingTimer.Elapsed.TotalSeconds;
        
        if (enableLogging && academyStepCount % logInterval == 0)
        {
            LogTrainingProgress();
        }
        
        UpdateStatusUI();
    }
    
    private void LogTrainingProgress()
    {
        Debug.Log($"Training Progress - Episodes: {environment.totalEpisodes}, " +
                  $"Time: {totalTrainingTime:F1}s, " +
                  $"Avg Reward: {environment.averageReward:F3}");
    }
    
    private void UpdateStatusUI()
    {
        if (statusText != null)
        {
            statusText.text = $"Episode: {environment.totalEpisodes}\n" +
                              $"Training Time: {totalTrainingTime:F1}s\n" +
                              $"Avg Reward: {environment.averageReward:F3}";
        }
    }
    
    void OnDestroy()
    {
        if (Academy.Instance != null)
        {
            Academy.Instance.AgentPreStep -= OnAgentPreStep;
        }
    }
}