using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// RTS 스타일 탑다운 카메라 컨트롤러 (네트워크 동기화 지원)
/// - WASD/화살표: 카메라 이동
/// - 마우스 휠: 줌 인/아웃
/// - 화면 가장자리: 스크롤 (옵션)
/// - 마우스 드래그: 카메라 패닝 (중클릭)
/// - F1~F4: 다른 플레이어 시점으로 이동
/// </summary>
public class RTSCameraController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 20f;
    [SerializeField] private float _fastMoveMultiplier = 2f;
    [SerializeField] private Key _fastMoveKey = Key.LeftShift;

    [Header("Edge Scrolling")]
    [SerializeField] private bool _enableEdgeScrolling = true;
    [SerializeField] private float _edgeScrollThreshold = 10f;
    [SerializeField] private float _edgeScrollSpeed = 15f;

    [Header("Zoom")]
    [SerializeField] private float _zoomSpeed = 5f;
    [SerializeField] private float _minZoom = 5f;
    [SerializeField] private float _maxZoom = 50f;
    [SerializeField] private float _zoomSmoothTime = 0.1f;

    [Header("Drag Pan")]
    [SerializeField] private bool _enableDragPan = true;
    // [SerializeField] private float _dragSpeed = 0.5f;

    [Header("Bounds (Optional)")]
    [SerializeField] private bool _useBounds;
    [SerializeField] private Vector2 _boundsMin = new(-100f, -100f);
    [SerializeField] private Vector2 _boundsMax = new(100f, 100f);

    [Header("Camera Angle")]
    [SerializeField] private float _cameraAngle = 60f;
    [SerializeField] private float _cameraHeight = 10f;

    [Header("Player View Switch")]
    [SerializeField] private float _viewSwitchSmoothTime = 0.3f;

    // 네트워크 동기화: 각 플레이어의 카메라 위치를 다른 플레이어에게 공유
    [Networked]
    public Vector3 NetworkedCameraPosition { get; private set; }

    [Networked]
    public float NetworkedZoom { get; private set; }

    private Camera _camera;
    private float _targetZoom;
    private float _zoomVelocity;
    private Vector3 _dragStartPosition;
    private Vector3 _dragCurrentPosition;
    private bool _isDragging;

    // 다른 플레이어 시점 전환용
    private bool _isViewingOtherPlayer;
    private PlayerRef _viewingPlayerRef;
    private Vector3 _viewSwitchVelocity;
    private float _viewSwitchZoomVelocity;

    // 전역 카메라 컨트롤러 목록 (다른 플레이어 찾기용)
    private static readonly Dictionary<PlayerRef, RTSCameraController> _allCameras = new();

    private Keyboard Keyboard => Keyboard.current;
    private Mouse Mouse => Mouse.current;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Start()
    {
        _targetZoom = _cameraHeight;
        ApplyCameraAngle();
    }

    public override void Spawned()
    {
        // 자신의 카메라만 등록
        if (HasInputAuthority)
        {
            _allCameras[Runner.LocalPlayer] = this;
        }
        else
        {
            // 다른 플레이어의 카메라 컨트롤러도 등록
            if (Object.InputAuthority != PlayerRef.None)
                _allCameras[Object.InputAuthority] = this;
        }

        // 초기 네트워크 값 설정
        if (HasStateAuthority)
        {
            NetworkedCameraPosition = transform.position;
            NetworkedZoom = _cameraHeight;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // 등록 해제
        if (HasInputAuthority)
        {
            _allCameras.Remove(Runner.LocalPlayer);
        }
        else if (Object.InputAuthority != PlayerRef.None)
        {
            _allCameras.Remove(Object.InputAuthority);
        }
    }

    private void Update()
    {
        if (Keyboard == null || Mouse == null)
            return;

        // 다른 플레이어 시점 전환 입력 처리
        HandlePlayerViewSwitch();

        // 내 카메라가 아니면 조작 불가
        if (!HasInputAuthority)
            return;

        // 다른 플레이어 시점 보는 중이면 조작 제한
        if (_isViewingOtherPlayer)
        {
            UpdateViewingOtherPlayer();
            return;
        }

        HandleKeyboardMovement();
        HandleEdgeScrolling();
        HandleZoom();
        HandleDragPan();
        ApplyBounds();

        // 네트워크에 현재 위치 동기화
        UpdateNetworkedPosition();
    }

    /// <summary>
    /// F1~F4 키로 플레이어 시점 전환 처리
    /// </summary>
    private void HandlePlayerViewSwitch()
    {
        if (!HasInputAuthority)
            return;

        // ESC로 원래 시점 복귀
        if (_isViewingOtherPlayer && Keyboard.escapeKey.wasPressedThisFrame)
        {
            ReturnToOwnView();
            return;
        }

        // F1~F4 키 체크
        PlayerRef? targetPlayer = null;
        int playerIndex = -1;

        if (Keyboard.f1Key.wasPressedThisFrame) playerIndex = 0;
        else if (Keyboard.f2Key.wasPressedThisFrame) playerIndex = 1;
        else if (Keyboard.f3Key.wasPressedThisFrame) playerIndex = 2;
        else if (Keyboard.f4Key.wasPressedThisFrame) playerIndex = 3;

        if (playerIndex < 0)
            return;

        // 플레이어 목록에서 해당 인덱스의 플레이어 찾기
        targetPlayer = GetPlayerByIndex(playerIndex);

        if (targetPlayer.HasValue && targetPlayer.Value != Runner.LocalPlayer)
        {
            SwitchToPlayerView(targetPlayer.Value);
        }
        else if (targetPlayer.HasValue && targetPlayer.Value == Runner.LocalPlayer)
        {
            // 자기 자신이면 원래 시점으로 복귀
            ReturnToOwnView();
        }
    }

    /// <summary>
    /// 인덱스로 플레이어 찾기 (0 = Player 1, 1 = Player 2, ...)
    /// </summary>
    private PlayerRef? GetPlayerByIndex(int index)
    {
        if (Runner == null)
            return null;

        var players = new List<PlayerRef>();
        foreach (var player in Runner.ActivePlayers)
        {
            players.Add(player);
        }

        // 플레이어 ID 순으로 정렬
        players.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));

        if (index >= 0 && index < players.Count)
            return players[index];

        return null;
    }

    /// <summary>
    /// 특정 플레이어 시점으로 전환
    /// </summary>
    private void SwitchToPlayerView(PlayerRef playerRef)
    {
        if (!_allCameras.TryGetValue(playerRef, out var targetCamera))
        {
            Debug.LogWarning($"[RTSCameraController] Player {playerRef.PlayerId} camera not found");
            return;
        }

        _isViewingOtherPlayer = true;
        _viewingPlayerRef = playerRef;

        Debug.Log($"[RTSCameraController] Switching to Player {playerRef.PlayerId} view");
    }

    /// <summary>
    /// 다른 플레이어 시점 보는 중 업데이트
    /// </summary>
    private void UpdateViewingOtherPlayer()
    {
        if (!_allCameras.TryGetValue(_viewingPlayerRef, out var targetCamera))
        {
            ReturnToOwnView();
            return;
        }

        // 타겟 플레이어의 네트워크 동기화된 카메라 위치로 부드럽게 이동
        var targetPos = targetCamera.NetworkedCameraPosition;
        var targetZoom = targetCamera.NetworkedZoom;

        var currentPos = transform.position;
        currentPos = Vector3.SmoothDamp(currentPos, targetPos, ref _viewSwitchVelocity, _viewSwitchSmoothTime);
        transform.position = currentPos;

        _cameraHeight = Mathf.SmoothDamp(_cameraHeight, targetZoom, ref _viewSwitchZoomVelocity, _viewSwitchSmoothTime);
        _targetZoom = _cameraHeight;
        ApplyCameraAngle();
    }

    /// <summary>
    /// 자신의 시점으로 복귀
    /// </summary>
    private void ReturnToOwnView()
    {
        _isViewingOtherPlayer = false;
        _viewingPlayerRef = default;
        _viewSwitchVelocity = Vector3.zero;
        _viewSwitchZoomVelocity = 0f;

        Debug.Log("[RTSCameraController] Returned to own view");
    }

    /// <summary>
    /// 네트워크에 현재 카메라 위치 동기화
    /// </summary>
    private void UpdateNetworkedPosition()
    {
        if (!HasStateAuthority)
            return;

        NetworkedCameraPosition = transform.position;
        NetworkedZoom = _cameraHeight;
    }

    private void HandleKeyboardMovement()
    {
        var input = Vector3.zero;

        if (Keyboard.wKey.isPressed || Keyboard.upArrowKey.isPressed)
            input.z += 1f;
        if (Keyboard.sKey.isPressed || Keyboard.downArrowKey.isPressed)
            input.z -= 1f;
        if (Keyboard.aKey.isPressed || Keyboard.leftArrowKey.isPressed)
            input.x -= 1f;
        if (Keyboard.dKey.isPressed || Keyboard.rightArrowKey.isPressed)
            input.x += 1f;

        if (input == Vector3.zero)
            return;

        var speed = _moveSpeed;
        if (IsKeyPressedSafe(_fastMoveKey))
            speed *= _fastMoveMultiplier;

        // 카메라 방향 기준으로 이동 (Y축 회전만 적용)
        var forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        var right = transform.right;
        right.y = 0f;
        right.Normalize();

        var moveDirection = forward * input.z + right * input.x;
        transform.position += moveDirection.normalized * (speed * Time.deltaTime);
    }

    private bool IsKeyPressedSafe(Key key)
    {
        if (Keyboard == null)
            return false;

        var index = (int)key - 1;
        if (index < 0 || index >= Keyboard.allKeys.Count)
            return false;

        return Keyboard[key].isPressed;
    }

    private void OnValidate()
    {
        var index = (int)_fastMoveKey - 1;
        if (index < 0 || index >= Keyboard.KeyCount)
            _fastMoveKey = Key.LeftShift;
    }

    private void HandleEdgeScrolling()
    {
        if (!_enableEdgeScrolling)
            return;

        if (_isDragging)
            return;

        var mousePos = Mouse.position.ReadValue();
        var moveDirection = Vector3.zero;

        // 화면 경계 체크
        if (mousePos.x < _edgeScrollThreshold)
            moveDirection.x -= 1f;
        else if (mousePos.x > Screen.width - _edgeScrollThreshold)
            moveDirection.x += 1f;

        if (mousePos.y < _edgeScrollThreshold)
            moveDirection.z -= 1f;
        else if (mousePos.y > Screen.height - _edgeScrollThreshold)
            moveDirection.z += 1f;

        if (moveDirection == Vector3.zero)
            return;

        var forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        var right = transform.right;
        right.y = 0f;
        right.Normalize();

        var worldDirection = forward * moveDirection.z + right * moveDirection.x;
        transform.position += worldDirection.normalized * (_edgeScrollSpeed * Time.deltaTime);
    }

    private void HandleZoom()
    {
        var scroll = Mouse.scroll.ReadValue().y;

        // TilePlacementController가 스크롤을 타일 선택에 사용하므로
        // Ctrl 키를 누른 상태에서만 줌 작동
        if (scroll != 0f && Keyboard.leftCtrlKey.isPressed)
        {
            _targetZoom -= scroll * _zoomSpeed;
            _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
        }

        // 부드러운 줌 적용
        _cameraHeight = Mathf.SmoothDamp(_cameraHeight, _targetZoom, ref _zoomVelocity, _zoomSmoothTime);
        ApplyCameraAngle();
    }

    private void HandleDragPan()
    {
        if (!_enableDragPan)
            return;

        // 중클릭 드래그
        if (Mouse.middleButton.wasPressedThisFrame)
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = _camera.ScreenPointToRay(Mouse.position.ReadValue());

            if (plane.Raycast(ray, out var enter))
            {
                _dragStartPosition = ray.GetPoint(enter);
                _isDragging = true;
            }
        }

        if (Mouse.middleButton.isPressed && _isDragging)
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = _camera.ScreenPointToRay(Mouse.position.ReadValue());

            if (plane.Raycast(ray, out var enter))
            {
                _dragCurrentPosition = ray.GetPoint(enter);
                var delta = _dragStartPosition - _dragCurrentPosition;
                transform.position += delta;
            }
        }

        if (Mouse.middleButton.wasReleasedThisFrame)
        {
            _isDragging = false;
        }
    }

    private void ApplyCameraAngle()
    {
        var pos = transform.position;
        pos.y = _cameraHeight;
        transform.position = pos;

        transform.rotation = Quaternion.Euler(_cameraAngle, 0f, 0f);
    }

    private void ApplyBounds()
    {
        if (!_useBounds)
            return;

        var pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, _boundsMin.x, _boundsMax.x);
        pos.z = Mathf.Clamp(pos.z, _boundsMin.y, _boundsMax.y);
        transform.position = pos;
    }

    /// <summary>
    /// 특정 월드 위치로 카메라 이동
    /// </summary>
    public void MoveTo(Vector3 worldPosition)
    {
        var pos = transform.position;
        pos.x = worldPosition.x;
        pos.z = worldPosition.z;
        transform.position = pos;
    }

    /// <summary>
    /// 줌 레벨 설정
    /// </summary>
    public void SetZoom(float zoom)
    {
        _targetZoom = Mathf.Clamp(zoom, _minZoom, _maxZoom);
    }

    /// <summary>
    /// 특정 플레이어의 카메라 위치 가져오기
    /// </summary>
    public static Vector3? GetPlayerCameraPosition(PlayerRef playerRef)
    {
        if (_allCameras.TryGetValue(playerRef, out var camera))
            return camera.NetworkedCameraPosition;
        return null;
    }

    /// <summary>
    /// 현재 등록된 모든 플레이어 카메라 목록
    /// </summary>
    public static IReadOnlyDictionary<PlayerRef, RTSCameraController> AllCameras => _allCameras;
}
