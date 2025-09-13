using UnityEngine;

public enum EcoResourceType {
    Vegetation,
    Seeds,
    Water,
    Shelter,
    Minerals
}

public enum EcoRelationshipType {
    Neutral,
    Prey,
    Predator,
    Mate,
    Competitor,
    Cooperator
}

public enum EcoActionType {
    DoNothing = 0,
    Eat = 1,
    Attack = 2,
    Flee = 3,
    Reproduce = 4,
    Communicate = 5,
    Cooperate = 6,
    Flock = 7,
    Hide = 8,
    Alert = 9
}

public enum EcoDeathCause {
    Starvation,
    OldAge,
    Injury,
    Predation,
    Disease
}

public enum CommunicationMessageType {
    Warning = 0,
    FoodLocation = 1,
    MateCall = 2,
    Cooperation = 3,
    Territorial = 4
}

public enum EcoInteractionType {
    Attack,
    Reproduction,
    Communication,
    Cooperation,
    Competition
}

[System.Serializable]
public class CommunicationMessage {
    public BaseEcoAgent sender;
    public CommunicationMessageType messageType;
    public Vector3 position;
    public float intensity;
    public float timestamp;
}