using System;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Photon Fusion 2 네트워크 관리자. 로비 연결, 룸 생성/참가, 세션 목록 관리.
/// </summary>
public class NetworkManager : Singleton<NetworkManager>, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner _runnerPrefab;

    private NetworkRunner _runner;
    private string _playerNickname;
    private readonly List<SessionInfo> _lastSessionList = new();

    public NetworkRunner Runner => _runner;
    public bool IsConnected => _runner != null && _runner.IsRunning;
    public string PlayerNickname => _playerNickname;
    public IReadOnlyList<SessionInfo> LastSessionList => _lastSessionList;

    // 세션 목록이 갱신될 때 발생 (로비에서 구독)
    public event Action<IReadOnlyList<SessionInfo>> SessionListUpdated;

    /// <summary>
    /// Photon 세션 로비에 연결. 연결 후 SessionListUpdated 이벤트로 룸 목록 수신.
    /// </summary>
    public async Task<bool> JoinLobby(string nickname)
    {
        _playerNickname = nickname;

        if (!EnsureRunner())
            return false;

        // SessionLobby.ClientServer: 클라이언트-서버 모드의 세션 목록 조회
        var result = await _runner.JoinSessionLobby(SessionLobby.ClientServer);

        if (result.Ok)
        {
            Debug.Log($"Joined lobby with nickname: {nickname}");
            return true;
        }
        else
        {
            Debug.LogError($"Failed to join lobby: {result.ShutdownReason}");
            return false;
        }
    }

    /// <summary>
    /// 룸 참가 또는 생성 (간단 버전).
    /// </summary>
    public Task<bool> JoinOrCreateRoom(string roomName, string sceneName)
    {
        return JoinOrCreateRoom(roomName, sceneName, null, null, null, null);
    }

    /// <summary>
    /// 룸 참가 또는 생성. Fusion Shared Mode로 동작하며, 룸이 없으면 자동 생성.
    /// </summary>
    /// <param name="sessionProperties">커스텀 세션 속성 (pw, host, map 등)</param>
    public async Task<bool> JoinOrCreateRoom(
        string roomName,
        string sceneName,
        Dictionary<string, SessionProperty> sessionProperties,
        int? maxPlayers,
        bool? isVisible,
        bool? isOpen)
    {
        if (!EnsureRunner())
            return false;

        // 씬 경로를 Fusion SceneRef로 변환
        var sceneInfo = new NetworkSceneInfo();
        var sceneIndex = SceneUtility.GetBuildIndexByScenePath(sceneName);
        if (sceneIndex >= 0)
        {
            sceneInfo.AddSceneRef(SceneRef.FromIndex(sceneIndex));
        }
        else
        {
            Debug.LogWarning($"Scene not found in build settings: {sceneName}");
        }

        // Shared Mode: 모든 클라이언트가 동등한 권한, StateAuthority로 오브젝트 소유권 관리
        var startGameArgs = new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = roomName,
            Scene = sceneInfo,
            SceneManager = _runner.GetComponent<INetworkSceneManager>() ?? _runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            IsVisible = isVisible ?? true,
            IsOpen = isOpen ?? true,
            SessionProperties = sessionProperties
        };

        if (maxPlayers.HasValue)
            startGameArgs.PlayerCount = maxPlayers.Value;

        var result = await _runner.StartGame(startGameArgs);

        if (result.Ok)
        {
            Debug.Log($"Joined/Created room: {roomName}");
            return true;
        }
        else
        {
            Debug.LogError($"Failed to join/create room: {result.ShutdownReason}");
            return false;
        }
    }

    /// <summary>
    /// NetworkRunner 인스턴스 생성 및 초기화. 없으면 프리팹에서 생성.
    /// </summary>
    private bool EnsureRunner()
    {
        if (_runner != null)
            return true;

        if (_runnerPrefab == null)
        {
            Debug.LogError("NetworkRunner prefab is not assigned.");
            return false;
        }

        _runner = Instantiate(_runnerPrefab);
        _runner.name = "NetworkRunner";
        _runner.AddCallbacks(this); // INetworkRunnerCallbacks 등록
        DontDestroyOnLoad(_runner.gameObject);
        return true;
    }

    /// <summary>
    /// 네트워크 연결 해제 및 상태 초기화.
    /// </summary>
    public void Disconnect()
    {
        if (_runner != null)
        {
            _runner.Shutdown();
        }

        _playerNickname = null;
        _lastSessionList.Clear();
    }

    #region INetworkRunnerCallbacks

#pragma warning disable UNT0006 // Incorrect message signature
    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Connected to server");
    }
#pragma warning restore UNT0006

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"Connect failed: {reason}");
    }

    // 클라이언트 연결 요청 시 호출 - 기본적으로 모두 수락
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        request.Accept();
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

#pragma warning disable UNT0006
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from server: {reason}");
    }
#pragma warning restore UNT0006

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player joined: {player}");
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player left: {player}");
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("Scene load done");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("Scene load start");
    }

    /// <summary>
    /// 로비에서 세션 목록이 갱신될 때 호출. SessionListUpdated 이벤트 발생.
    /// </summary>
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        var count = sessionList != null ? sessionList.Count : 0;
        Debug.Log($"Session list updated: {count} sessions");
        _lastSessionList.Clear();
        if (sessionList != null)
            _lastSessionList.AddRange(sessionList);
        SessionListUpdated?.Invoke(_lastSessionList);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Runner shutdown: {shutdownReason}");
        _runner = null;
        _lastSessionList.Clear();
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }

    #endregion
}
