using UnityEngine;

/// <summary>
/// 게임 전역 상태 관리자. 씬 간 맵 이름 전달 등 담당.
/// </summary>
public class GameManager : Singleton<GameManager>
{
    [Header("Map")]
    [SerializeField] private string _defaultMapName = "default";

    private string _pendingMapName;

    public string DefaultMapName => _defaultMapName;

    // 앱 시작 시 인스턴스 자동 생성 (씬에 없어도 동작)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        var gameObject = new GameObject(nameof(GameManager));
        gameObject.AddComponent<GameManager>();
    }

    /// <summary>
    /// 다음 씬에서 사용할 맵 이름 설정. Loading → Stage 전환 시 사용.
    /// </summary>
    public void SetPendingMapName(string mapName)
    {
        _pendingMapName = mapName;
    }

    /// <summary>
    /// 대기 중인 맵 이름 반환 후 초기화. 일회성 전달용.
    /// </summary>
    public string ConsumePendingMapName()
    {
        var value = _pendingMapName;
        _pendingMapName = null;
        return value;
    }
}
