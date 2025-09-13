// using System;
// using EcoSystem.Management;
// using TMPro;
// using UnityEngine;
// using UnityEngine.UI;
//
// public class AgentHud : MonoBehaviour
// {
//     private Image hp, energy;
//     private TMP_Text hpText, energyText;
//     private GameObject panel;
//     private Camera mainCamera;
//
//     public BaseEcoAgent agent;
//     public void Awake()
//     {
//         Initialized();
//     }
//
//
//     private void Initialized() {
//         panel = EcosystemManager.GetObjectFromParent(this.transform.parent, "HUD");
//         
//
//         if (panel == null) return;
//         hp = EcosystemManager.GetObjectFromParent(panel.transform, "HPImage").GetComponent<Image>();
//         hpText = EcosystemManager.GetObjectFromParent(panel.transform, "HPText").GetComponent<TMP_Text>();
//         
//         energy = EcosystemManager.GetObjectFromParent(panel.transform, "EnergyImage").GetComponent<Image>();
//         energyText = EcosystemManager.GetObjectFromParent(panel.transform, "EnergyText").GetComponent<TMP_Text>();
//         
//         
//         mainCamera = Camera.main;
//         agent = this.transform.parent.GetComponent<BaseEcoAgent>();
//         //
//         // Debug.Log(hp);
//         // Debug.Log(mainCamera);
//         // Debug.Log(agent);
//     }
//
//
//     private void ValueUpdate()
//     {
//         if (hp == null) return;
//         hp.fillAmount = agent.health / agent.maxHealth;
//         hpText.text = agent.health.ToString();
//
//         energy.fillAmount = agent.energy / agent.maxEnergy;
//         energyText.text = agent.energy.ToString();
//     }
//
//     private void CameraLook()
//     {
//         if (panel == null || mainCamera == null) return;
//         panel.transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
//             mainCamera.transform.rotation * Vector3.up);
//
//     }
//     
//     private void LateUpdate() {
//         CameraLook();
//         ValueUpdate();
//
//
//     }
// }