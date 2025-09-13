using UnityEngine;

public class BasicResource : EcoResource {
    [SerializeField] private float maxValue = 10f;
    [SerializeField] private float currentValue = 10f;
    
    void Start() {
        currentValue = maxValue;
    }
    
    public override bool IsAvailable() {
        return isAvailable && currentValue > 0;
    }
    
    public override float Consume(BaseEcoAgent consumer) {
        if (!IsAvailable()) return 0f;
        
        float consumed = Mathf.Min(currentValue, nutritionalValue);
        currentValue -= consumed;
        
        if (currentValue <= 0) {
            isAvailable = false;
            Destroy(gameObject, 0.5f);
        }
        
        return consumed;
    }
}