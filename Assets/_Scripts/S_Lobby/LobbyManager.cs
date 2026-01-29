using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로비 씬 관리자. 룸 목록 표시, 룸 생성/참가, 퀵스타트 처리.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    // Fusion 세션 프로퍼티 키
    private const string SessionPasswordKey = "pw";
    private const string SessionHostKey = "host";
    private const string SessionMapKey = "map";

    [SerializeField] private RoomListView _roomListView;
    [SerializeField] private CreateRoomPanelView _createRoomPanel;
    [SerializeField] private JoinPwPanelView _joinPwPanel;
    [SerializeField] private TextMeshProUGUI _noticeText;
    [SerializeField] private float _noticeDuration = 2f;
    [SerializeField] private string _loadingScenePath = "Assets/_Scenes/Loading.unity";
    [SerializeField] private string _titleSceneName = "Title";

    [Header("Map")]
    [SerializeField] private string _defaultMapName = "default";

    private readonly List<RoomListItemData> _rooms = new();
    private RoomListItemData _pendingJoinRoom; // 비밀번호 입력 대기 중인 룸
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
        // 세션 목록 갱신 이벤트 구독
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

        // Title에서 연결 안 됐으면 여기서 재연결 시도
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

        // 캐시된 세션 목록으로 UI 초기화
        var cachedList = NetworkManager.Instance != null ? NetworkManager.Instance.LastSessionList : null;
        HandleSessionListUpdated(cachedList ?? new List<SessionInfo>());
    }

    private void OnDisable()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.SessionListUpdated -= HandleSessionListUpdated;
    }

    #region UI Button Callbacks (씬에서 연결)

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

    /// <summary>
    /// 퀵스타트: 비밀번호 없고 입장 가능한 첫 번째 룸에 자동 참가.
    /// </summary>
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

        ShowNotice("참여 가능한 방이 없습니다.");
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

    #endregion

    #region Room Creation/Join

    /// <summary>
    /// 룸 생성 요청. CreateRoomPanelView에서 호출.
    /// </summary>
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

    /// <summary>
    /// 비밀번호 입력 완료. JoinPwPanelView에서 호출.
    /// </summary>
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

    #endregion

    /// <summary>
    /// Fusion 세션 목록 갱신 시 호출. 룸 리스트 UI 업데이트.
    /// </summary>
    private void HandleSessionListUpdated(IReadOnlyList<SessionInfo> sessionList)
    {
        _rooms.Clear();

        if (sessionList != null)
        {
            foreach (var session in sessionList)
            {
                if (!session.IsVisible)
                    continue;

                // 세션 프로퍼티에서 비밀번호, 호스트명 추출
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

        // 입장 가능 여부 확인
        if (!data.SessionInfo.IsOpen || (data.SessionInfo.MaxPlayers > 0 && data.SessionInfo.PlayerCount >= data.SessionInfo.MaxPlayers))
        {
            ShowNotice("입장할 수 없는 방입니다.");
            return;
        }

        // 비밀번호 있으면 입력 패널 표시
        if (data.HasPassword)
        {
            _pendingJoinRoom = data;
            _joinPwPanel?.Show();
            return;
        }

        TryJoinRoom(data);
    }

    /// <summary>
    /// 퀵스타트용 룸 검색. 비밀번호 없고 열려있고 자리 있는 첫 룸.
    /// </summary>
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

    /// <summary>
    /// 룸 참가 시도. Fusion JoinOrCreateRoom 호출.
    /// </summary>
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

    /// <summary>
    /// 룸 생성. 세션 프로퍼티에 비밀번호, 호스트, 맵 정보 포함.
    /// </summary>
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

        // 세션 프로퍼티 설정 (Fusion에서 룸 목록 조회 시 사용)
        if (!string.IsNullOrEmpty(password) || !string.IsNullOrEmpty(hostName) || !string.IsNullOrEmpty(_defaultMapName))
        {
            properties = new Dictionary<string, SessionProperty>();

            if (!string.IsNullOrEmpty(password))
                properties[SessionPasswordKey] = password;

            if (!string.IsNullOrEmpty(hostName))
                properties[SessionHostKey] = hostName;

            if (!string.IsNullOrEmpty(_defaultMapName))
                properties[SessionMapKey] = _defaultMapName;
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

    /// <summary>
    /// 세션 프로퍼티에서 문자열 값 추출.
    /// </summary>
    private string GetSessionString(SessionInfo sessionInfo, string key)
    {
        if (sessionInfo.Properties == null)
            return null;

        return sessionInfo.Properties.TryGetValue(key, out var value) ? (string)value : null;
    }

    /// <summary>
    /// 타이틀로 돌아가기. 네트워크 연결 해제 및 로그아웃.
    /// </summary>
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

    /// <summary>
    /// Inspector 미할당 시 자동 탐색.
    /// </summary>
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
