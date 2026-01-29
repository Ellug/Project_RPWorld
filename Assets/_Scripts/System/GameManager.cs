using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [Header("Map")]
    [SerializeField] private string _defaultMapName = "default";

    private string _pendingMapName;

    public string DefaultMapName => _defaultMapName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        var gameObject = new GameObject(nameof(GameManager));
        gameObject.AddComponent<GameManager>();
    }

    public void SetPendingMapName(string mapName)
    {
        _pendingMapName = mapName;
    }

    public string ConsumePendingMapName()
    {
        var value = _pendingMapName;
        _pendingMapName = null;
        return value;
    }
}
