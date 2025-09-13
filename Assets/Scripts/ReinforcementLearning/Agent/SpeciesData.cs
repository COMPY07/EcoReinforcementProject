using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Species", menuName = "Ecosystem/Species Data")]
public class SpeciesData : ScriptableObject
{
    [Header("Basic Stats")]
    public string speciesName = "Unknown";
    public float maxHealth = 100f;
    public float maxEnergy = 100f;
    public float moveSpeed = 5f;
    public float turnSpeed = 180f;
    public float size = 1f;
    
    [Header("Survival")]
    public float energyDecayRate = 1f;
    public float reproductionThreshold = 80f;
    public float maxAge = 1000f;
    
    [Header("Vision")]
    public float visionRange = 10f;
    public float visionAngle = 120f;
    
    [Header("Behavior Capabilities")]
    public bool canEat = true;
    public bool canAttack = false;
    public bool canReproduce = true;
    public bool canFlee = true;
    
    [Header("Diet")]
    public List<string> foodTags = new List<string>();
    public List<string> predatorTags = new List<string>();
    public List<string> preyTags = new List<string>();
    
    [Header("Rewards")]
    public float survivalReward = 0.01f;
    public float feedingReward = 0.1f;
    public float reproductionReward = 1f;
    public float deathPenalty = -1f;
    
    [Header("Visual")]
    public Material bodyMaterial;
    public Vector3 bodyScale = Vector3.one;
    public Mesh bodyMesh;
    public GameObject customPrefab;
}