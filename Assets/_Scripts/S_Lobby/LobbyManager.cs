using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    private const string SessionPasswordKey = "pw";
    private const string SessionHostKey = "host";

    [SerializeField] private RoomListView _roomListView;
    [SerializeField] private CreateRoomPanelView _createRoomPanel;
    [SerializeField] private JoinPwPanelView _joinPwPanel;
    [SerializeField] private TextMeshProUGUI _noticeText;
    [SerializeField] private float _noticeDuration = 2f;
    [SerializeField] private string _loadingScenePath = "Assets/_Scenes/Loading.unity";
    [SerializeField] private string _titleSceneName = "Title";

    private readonly List<RoomListItemData> _rooms = new();
    private RoomListItemData _pendingJoinRoom;
    private bool _isBusy;
    private Coroutine _noticeRoutine;

    private void Awake()
    {
        AutoWireIfNeeded();
        if (_noticeText != null)
            _noticeText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.SessionListUpdated += HandleSessionListUpdated;
    }

    private async void Start()
    {
        AutoWireIfNeeded();

        _createRoomPanel?.SetLobbyManager(this);
        _joinPwPanel?.SetLobbyManager(this);
        _createRoomPanel?.Hide();
        _joinPwPanel?.Hide();

        if (NetworkManager.Instance != null && !NetworkManager.Instance.IsConnected)
        {
            try
            {
                var nickname = AuthManager.Instance?.CurrentUserNickname ?? "Player";
                var joined = await NetworkManager.Instance.JoinLobby(nickname);
                if (!joined)
                    ShowNotice("로비 연결에 실패했습니다.");
            }
            catch
            {
                ShowNotice("로비 연결에 실패했습니다.");
            }
        }

        var cachedList = NetworkManager.Instance != null ? NetworkManager.Instance.LastSessionList : null;
        HandleSessionListUpdated(cachedList ?? new List<SessionInfo>());
    }

    private void OnDisable()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.SessionListUpdated -= HandleSessionListUpdated;
    }

    public void OnClickLeaveToTitle()
    {
        LeaveToTitle();
    }

    public void OnClickOpenCreateRoom()
    {
        if (_isBusy)
            return;

        _createRoomPanel?.Show();
        _joinPwPanel?.Hide();
    }

    public void OnClickQuickStart()
    {
        if (_isBusy)
            return;

        var candidate = FindQuickJoinRoom();
        if (candidate != null)
        {
            TryJoinRoom(candidate);
            return;
        }

        var roomName = $"Room_{Random.Range(1000, 9999)}";
        RequestCreateRoom(roomName, string.Empty);
    }

    public void OnClickRefresh()
    {
        if (_isBusy)
            return;

        var cachedList = NetworkManager.Instance != null ? NetworkManager.Instance.LastSessionList : null;
        if (cachedList == null || cachedList.Count == 0)
        {
            HandleSessionListUpdated(new List<SessionInfo>());
            ShowNotice("현재 방 목록이 없습니다.");
            return;
        }

        HandleSessionListUpdated(cachedList);
        ShowNotice("방 목록을 갱신했습니다.");
    }

    public void RequestCreateRoom(string roomName, string password)
    {
        if (_isBusy)
            return;

        if (string.IsNullOrWhiteSpace(roomName))
        {
            ShowNotice("방 이름을 입력해주세요.");
            return;
        }

        CreateRoomAsync(roomName, password);
    }

    public void CancelCreateRoom()
    {
        _createRoomPanel?.Hide();
    }

    public void SubmitJoinPassword(string password)
    {
        if (_pendingJoinRoom == null)
            return;

        var expected = _pendingJoinRoom.Password ?? string.Empty;
        var provided = password ?? string.Empty;

        if (!string.Equals(expected, provided))
        {
            _joinPwPanel?.ShowError("Password is Wrong");
            return;
        }

        _joinPwPanel?.Hide();
        TryJoinRoom(_pendingJoinRoom);
    }

    public void CancelJoinPassword()
    {
        _pendingJoinRoom = null;
        _joinPwPanel?.Hide();
    }

    private void HandleSessionListUpdated(IReadOnlyList<SessionInfo> sessionList)
    {
        _rooms.Clear();

        if (sessionList != null)
        {
            foreach (var session in sessionList)
            {
                if (!session.IsVisible)
                    continue;

                var password = GetSessionString(session, SessionPasswordKey);
                var hostName = GetSessionString(session, SessionHostKey);
                _rooms.Add(new RoomListItemData(session, hostName, password));
            }
        }

        _roomListView?.Render(_rooms, OnRoomJoinClicked);
    }

    private void OnRoomJoinClicked(RoomListItemData data)
    {
        if (_isBusy || data == null)
            return;

        if (!data.SessionInfo.IsOpen || (data.SessionInfo.MaxPlayers > 0 && data.SessionInfo.PlayerCount >= data.SessionInfo.MaxPlayers))
        {
            ShowNotice("입장할 수 없는 방입니다.");
            return;
        }

        if (data.HasPassword)
        {
            _pendingJoinRoom = data;
            _joinPwPanel?.Show();
            return;
        }

        TryJoinRoom(data);
    }

    private RoomListItemData FindQuickJoinRoom()
    {
        foreach (var room in _rooms)
        {
            if (room == null)
                continue;

            if (room.HasPassword)
                continue;

            if (!room.SessionInfo.IsOpen)
                continue;

            if (room.SessionInfo.MaxPlayers > 0 && room.SessionInfo.PlayerCount >= room.SessionInfo.MaxPlayers)
                continue;

            return room;
        }

        return null;
    }

    private async void TryJoinRoom(RoomListItemData data)
    {
        if (_isBusy || data == null)
            return;

        if (NetworkManager.Instance == null)
        {
            ShowNotice("네트워크 연결이 필요합니다.");
            return;
        }

        _isBusy = true;

        var ok = await NetworkManager.Instance.JoinOrCreateRoom(
            data.SessionInfo.Name,
            _loadingScenePath);

        if (!ok)
            ShowNotice("방 입장에 실패했습니다.");

        _pendingJoinRoom = null;
        _isBusy = false;
    }

    private async void CreateRoomAsync(string roomName, string password)
    {
        if (_isBusy)
            return;

        if (NetworkManager.Instance == null)
        {
            ShowNotice("네트워크 연결이 필요합니다.");
            return;
        }

        _isBusy = true;
        _createRoomPanel?.Hide();

        Dictionary<string, SessionProperty> properties = null;

        var hostName = AuthManager.Instance?.CurrentUserNickname;
        if (string.IsNullOrEmpty(hostName))
            hostName = NetworkManager.Instance?.PlayerNickname;

        if (!string.IsNullOrEmpty(password) || !string.IsNullOrEmpty(hostName))
        {
            properties = new Dictionary<string, SessionProperty>();

            if (!string.IsNullOrEmpty(password))
                properties[SessionPasswordKey] = password;

            if (!string.IsNullOrEmpty(hostName))
                properties[SessionHostKey] = hostName;
        }

        var ok = await NetworkManager.Instance.JoinOrCreateRoom(
            roomName,
            _loadingScenePath,
            properties,
            null,
            true,
            true);

        if (!ok)
            ShowNotice("방 만들기에 실패했습니다.");

        _isBusy = false;
    }

    private string GetSessionString(SessionInfo sessionInfo, string key)
    {
        if (sessionInfo.Properties == null)
            return null;

        return sessionInfo.Properties.TryGetValue(key, out var value) ? (string)value : null;
    }

    private void LeaveToTitle()
    {
        _pendingJoinRoom = null;
        _rooms.Clear();

        if (_noticeRoutine != null)
        {
            StopCoroutine(_noticeRoutine);
            _noticeRoutine = null;
        }

        if (NetworkManager.Instance != null)
            NetworkManager.Instance.Disconnect();

        if (AuthManager.Instance != null)
            AuthManager.Instance.SignOut();

        SceneManager.LoadScene(_titleSceneName);
    }

    private void ShowNotice(string message)
    {
        if (_noticeText == null)
            return;

        if (_noticeRoutine != null)
            StopCoroutine(_noticeRoutine);

        _noticeRoutine = StartCoroutine(NoticeRoutine(message));
    }

    private System.Collections.IEnumerator NoticeRoutine(string message)
    {
        _noticeText.gameObject.SetActive(true);
        _noticeText.text = message;

        yield return new WaitForSeconds(_noticeDuration);

        _noticeText.gameObject.SetActive(false);
        _noticeRoutine = null;
    }

    private void AutoWireIfNeeded()
    {
        if (_roomListView == null)
            _roomListView = FindFirstObjectByType<RoomListView>();

        if (_createRoomPanel == null)
            _createRoomPanel = FindFirstObjectByType<CreateRoomPanelView>();

        if (_joinPwPanel == null)
            _joinPwPanel = FindFirstObjectByType<JoinPwPanelView>();
    }
}
