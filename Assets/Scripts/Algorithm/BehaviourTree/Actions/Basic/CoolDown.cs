using System.Collections.Generic;
using UnityEngine;

public class Cooldown : Node
{
    private string cooldownKey;
    private float cooldownDuration;
    private static Dictionary<string, float> cooldowns = new Dictionary<string, float>();

    public Cooldown(string key, float duration) {
        cooldownKey = key;
        cooldownDuration = duration;
        StartCooldown(key);
    }

    protected override NodeState DoEvaluate()
    {
        
        if (Cooldown.cooldowns.TryGetValue(cooldownKey, out float lastUsedTime)) {
            float timePassed = Time.time - lastUsedTime;
            // Debug.Log(timePassed);
            if (timePassed < cooldownDuration)
                return NodeState.Failure;
            // Debug.Log(timePassed);
        }

        
        return NodeState.Success;
    }
    
    public static void StartCooldown(string key)
    {
        Cooldown.cooldowns[key] = Time.time;
    }
    
    public static void ResetCooldown(string key)
    {
        if (Cooldown.cooldowns.ContainsKey(key))
        {
            cooldowns.Remove(key);
        }
    }
    
    public static void ResetAllCooldowns()
    {
        cooldowns.Clear();
    }
}