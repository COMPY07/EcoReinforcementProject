using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool _applicationIsQuitting = false;
    
    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[SingletonHolder] Instance '{typeof(T)}' 요청이 무시되었습니다. 애플리케이션이 종료 중입니다.");
                return null;
            }
            
            if (_instance != null)
            {
                return _instance;
            }
            
            return SingletonHolderInternal.HolderInstance;
        }
    }
    
    private static class SingletonHolderInternal
    {
        static SingletonHolderInternal()
        {
            _instance = FindObjectOfType<T>();
            
            if (_instance == null)
            {
                GameObject singletonObject = new GameObject($"[Singleton] {typeof(T).Name}");
                _instance = singletonObject.AddComponent<T>();
                DontDestroyOnLoad(singletonObject);
                Debug.Log($"[SingletonHolder] '{typeof(T).Name}' 인스턴스를 생성했습니다.");
            }
            else if (FindObjectsOfType<T>().Length > 1)
            {
                Debug.LogWarning($"[SingletonHolder] '{typeof(T).Name}'의 인스턴스가 여러 개 있습니다!");
            }
            else
            {
                Debug.Log($"[SingletonHolder] '{typeof(T).Name}'의 기존 인스턴스를 사용합니다.");
                DontDestroyOnLoad(_instance.gameObject);
            }
        }

        public static T HolderInstance
        {
            get { return _instance; }
        }
    }
    
    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[SingletonHolder] '{typeof(T).Name}'의 중복 인스턴스를 파괴합니다: {gameObject.name}");
            Destroy(gameObject);
        }
        else if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[SingletonHolder] '{typeof(T).Name}' 인스턴스가 초기화되었습니다: {gameObject.name}");
        }
    }
    
    protected virtual void OnDestroy() {
        if (_instance == this)
            _applicationIsQuitting = true;
    }
    
    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }
    
    
    protected GameObject FindChild(GameObject parent, string name)
    {
        int child = parent.transform.childCount;

        for (int i = 0; i < child; i++)
        {
            GameObject target = parent.transform.GetChild(i).gameObject;
            if (target.name == name) return target;

            GameObject findTarget = FindChild(target, name);

            if (findTarget != null) return findTarget;
        }
        return null;
    }
    
    
}