using System;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

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

    public event Action<IReadOnlyList<SessionInfo>> SessionListUpdated;

    public async Task<bool> JoinLobby(string nickname)
    {
        _playerNickname = nickname;

        if (_runner == null)
        {
            _runner = Instantiate(_runnerPrefab);
            _runner.name = "NetworkRunner";
            _runner.AddCallbacks(this);
            DontDestroyOnLoad(_runner.gameObject);
        }

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

    public Task<bool> JoinOrCreateRoom(string roomName, string sceneName)
    {
        return JoinOrCreateRoom(roomName, sceneName, null, null, null, null);
    }

    public async Task<bool> JoinOrCreateRoom(
        string roomName,
        string sceneName,
        Dictionary<string, SessionProperty> sessionProperties,
        int? maxPlayers,
        bool? isVisible,
        bool? isOpen)
    {
        if (_runner == null || !_runner.IsRunning)
        {
            Debug.LogError("NetworkRunner is not running. Call JoinLobby first.");
            return false;
        }

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

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Connected to server");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"Connect failed: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        request.Accept();
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from server: {reason}");
    }

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
