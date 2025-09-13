using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Species", menuName = "EcoSystem/Species Data")]
public class EcoSpeciesData : ScriptableObject {
    [Header("Basic Properties")]
    public string speciesName;
    public Color speciesColor = Color.white;
    public float baseMovementSpeed = 5f;
    public float baseEnergyCapacity = 100f;
    public GameObject prefab;
        
    [Header("Lifecycle")]
    public float baseLifespan = 1000f;

    [Header("Research Focus - Behavioral Capabilities")]
    [Tooltip("Enable/disable specific behaviors for focused research")]
    public bool canCommunicate = true;
    public bool canFlee = true;
    public bool canAttack = true;
    public bool canCooperate = true;
    public bool canReproduce = true;
    public bool canEat = true;
    public bool hasMemory = true;
    public bool trackSocialInteractions = true;

}