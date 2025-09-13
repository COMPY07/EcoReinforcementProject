using UnityEngine;

public abstract class EcoResource : MonoBehaviour {
    public EcoResourceType resourceType;
    public float nutritionalValue = 10f;
    public bool isAvailable = true;
    
    public abstract bool IsAvailable();
    public abstract float Consume(BaseEcoAgent consumer);
}