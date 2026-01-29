using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    [Header("Map Switch")]
    [SerializeField] private WorldMapState _worldMapState;
    [SerializeField] private string[] _mapRotation = { "default" };
    [SerializeField] private bool _useLoadingSceneForMapSwitch = true;
    [SerializeField] private string _loadingScenePath = "Assets/_Scenes/Loading.unity";

    private int _mapIndex;

    private Keyboard Keyboard => Keyboard.current;

    private void Awake()
    {
        AutoWireIfNeeded();
        if (_mapRotation != null && _mapRotation.Length > 0)
            _mapIndex = Mathf.Clamp(_mapIndex, 0, _mapRotation.Length - 1);
    }

    private void Update()
    {
        if (!IsHost)
            return;

        if (_mapRotation == null || _mapRotation.Length == 0)
            return;

        if (Keyboard == null)
            return;

        // PageUp/PageDown은 직접 프로퍼티로 접근
        if (Keyboard.pageUpKey.wasPressedThisFrame)
            SwitchMap(1);
        else if (Keyboard.pageDownKey.wasPressedThisFrame)
            SwitchMap(-1);
    }

    private bool IsHost
    {
        get
        {
            var runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;
            return runner != null && runner.IsSharedModeMasterClient;
        }
    }

    private void SwitchMap(int direction)
    {
        _mapIndex = (_mapIndex + direction) % _mapRotation.Length;
        if (_mapIndex < 0)
            _mapIndex += _mapRotation.Length;

        var mapName = _mapRotation[_mapIndex];
        if (string.IsNullOrWhiteSpace(mapName))
            return;

        if (_useLoadingSceneForMapSwitch)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.SetPendingMapName(mapName);

            var runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;
            if (runner != null)
                runner.LoadScene(_loadingScenePath, LoadSceneMode.Single, LocalPhysicsMode.None, true);
        }
        else
        {
            _worldMapState?.RequestChangeMap(mapName);
        }
    }

    private void AutoWireIfNeeded()
    {
        if (_worldMapState == null)
            _worldMapState = FindFirstObjectByType<WorldMapState>();
    }
}
