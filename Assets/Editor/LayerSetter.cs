// 이 스크립트는 에디터에서만 작동하며, 빌드에는 포함되지 않습니다.
#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

public class LayerSetter
{
    // 메뉴 아이템의 경로를 정의합니다.
    private const string MENU_PATH = "Tools/Set Water Layer to Obstacle";

    /// <summary>
    /// 메뉴 아이템을 클릭했을 때 이 함수가 호출됩니다.
    /// </summary>
    [MenuItem(MENU_PATH)]
    private static void SetWaterLayers()
    {
        
        // "Obstacle" 레이어의 정수 인덱스를 가져옵니다.
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");

        // "Obstacle" 레이어가 프로젝트에 존재하는지 확인합니다.
        if (obstacleLayer == -1)
        {
            // 레이어가 없으면 에러 메시지를 출력하고 실행을 중단합니다.
            Debug.LogError("에러: 'Obstacle' 레이어가 존재하지 않습니다. 'Edit -> Project Settings -> Tags and Layers'에서 먼저 추가해주세요.");
            return;
        }

        // 현재 씬에 있는 모든 게임 오브젝트를 찾습니다.
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
        int objectsChanged = 0;

        Debug.Log("'Water'가 포함된 오브젝트의 레이어를 'Obstacle'로 변경하는 작업을 시작합니다...");

        // 찾은 모든 오브젝트를 순회합니다.
        foreach (GameObject obj in allObjects)
        {
            // 오브젝트 이름에 "water"가 포함되어 있는지 대소문자 구분 없이 확인합니다.
            if (obj.name.ToLower().Contains("water"))
            {
                // 해당 오브젝트의 레이어가 이미 "Obstacle"이 아닌 경우에만 변경합니다.
                if (obj.layer != obstacleLayer)
                {
                    obj.layer = obstacleLayer;
                    objectsChanged++;
                    // 어떤 오브젝트가 변경되었는지 확인을 위해 로그를 남깁니다.
                    Debug.Log($"'{obj.name}' 오브젝트의 레이어를 'Obstacle'로 변경했습니다.");
                }
            }
        }

        // 작업 완료 후 요약 메시지를 콘솔에 출력합니다.
        if (objectsChanged > 0)
        {
            Debug.Log($"작업 완료. 총 {objectsChanged}개의 오브젝트 레이어가 변경되었습니다.");
        }
        else
        {
            Debug.Log("작업 완료. 이름에 'Water'를 포함하는 오브젝트를 찾지 못했거나, 이미 레이어가 올바르게 설정되어 있습니다.");
        }
    }
}

#endif