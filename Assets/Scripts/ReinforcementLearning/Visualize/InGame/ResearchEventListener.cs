using System;
using UnityEngine;


public class ResearchEventListener : MonoBehaviour
{

    public event Action<GameObject> ObjectClickEvent;
    
    // 딥페이크, 자살 방지, 강홯ㄱ습 알고리즘 새로 마든거, 이거 하나, 서베이 하난
    
    
    private Camera mainCamera;
    public void Start() {
        mainCamera = Camera.main;
    }
    
    private void ClickEntity()
    {
        if (Input.GetMouseButtonDown(0)) {
            
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit)) ObjectClickEvent?.Invoke(hit.transform.gameObject);
            else ObjectClickEvent?.Invoke(null);
            
        }
    }
    
    
    void Update() {
        ClickEntity();
    }
    
    
    
}