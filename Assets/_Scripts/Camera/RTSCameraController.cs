using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// RTS 스타일 탑다운 카메라 컨트롤러 (로컬 전용)
/// - WASD/화살표: 카메라 이동
/// - 마우스 휠: 줌 인/아웃
/// - 화면 가장자리: 스크롤 (옵션)
/// - 마우스 드래그: 카메라 패닝 (중클릭)
/// </summary>
public class RTSCameraController : MonoBehaviour
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

    [Header("Bounds (Optional)")]
    [SerializeField] private bool _useBounds;
    [SerializeField] private Vector2 _boundsMin = new(-100f, -100f);
    [SerializeField] private Vector2 _boundsMax = new(100f, 100f);

    [Header("Camera Angle")]
    [SerializeField] private float _cameraAngle = 60f;
    [SerializeField] private float _cameraHeight = 10f;

    private Camera _camera;
    private float _targetZoom;
    private float _zoomVelocity;
    private Vector3 _dragStartPosition;
    private Vector3 _dragCurrentPosition;
    private bool _isDragging;

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

    private void Update()
    {
        if (Keyboard == null || Mouse == null)
            return;

        HandleKeyboardMovement();
        HandleEdgeScrolling();
        HandleZoom();
        HandleDragPan();
        ApplyBounds();
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
}
