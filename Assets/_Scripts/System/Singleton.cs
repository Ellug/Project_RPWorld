using UnityEngine;

// 제네릭 싱글톤 베이스 클래스. 씬 전환 시에도 유지되는 전역 매니저용.
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    [SerializeField] private bool _dontDestroyOnLoad = true;

    protected virtual void Awake()
    {
        // 이미 인스턴스가 존재하면 중복 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this as T;

        if (_dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        OnSingletonAwake();
    }

    protected virtual void OnSingletonAwake() { }
}
