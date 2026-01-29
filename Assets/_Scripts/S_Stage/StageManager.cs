using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 씬 관리자. 호스트 전용 맵 전환 기능.
/// </summary>
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
        // 호스트만 맵 전환 가능
        if (!IsHost)
            return;

        if (_mapRotation == null || _mapRotation.Length == 0)
            return;

        if (Keyboard == null)
            return;

        // PageUp/PageDown으로 맵 순환
        if (Keyboard.pageUpKey.wasPressedThisFrame)
            SwitchMap(1);
        else if (Keyboard.pageDownKey.wasPressedThisFrame)
            SwitchMap(-1);
    }

    /// <summary>
    /// Fusion Shared Mode에서 MasterClient 여부 확인.
    /// </summary>
    private bool IsHost
    {
        get
        {
            var runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;
            return runner != null && runner.IsSharedModeMasterClient;
        }
    }

    /// <summary>
    /// 맵 전환. Loading 씬 경유 또는 직접 전환 선택 가능.
    /// </summary>
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
            // GameManager에 맵 이름 저장 후 Loading 씬으로 이동
            if (GameManager.Instance != null)
                GameManager.Instance.SetPendingMapName(mapName);

            var runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;
            if (runner != null)
                runner.LoadScene(_loadingScenePath, LoadSceneMode.Single, LocalPhysicsMode.None, true);
        }
        else
        {
            // 직접 맵 변경 (씬 전환 없이 타일만 교체)
            _worldMapState?.RequestChangeMap(mapName);
        }
    }

    private void AutoWireIfNeeded()
    {
        if (_worldMapState == null)
            _worldMapState = FindFirstObjectByType<WorldMapState>();
    }
}
