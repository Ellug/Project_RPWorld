using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// RTS 스타일 유닛 선택 및 이동 명령 관리자.
/// 클릭 선택, 드래그 박스 다중 선택, 우클릭 이동 명령 지원.
/// </summary>
public class UnitSelectionManager : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private float _dragThreshold = 8f;           // 드래그로 인식할 최소 픽셀 거리
    [SerializeField] private LayerMask _selectableMask = ~0;      // 선택 가능한 레이어
    [SerializeField] private bool _clearSelectionOnEmptyClick = true; // 빈 공간 클릭 시 선택 해제

    [Header("Movement")]
    [SerializeField] private LayerMask _groundMask = ~0;          // 바닥 레이캐스트용 레이어
    [SerializeField] private bool _useGroundMask = false;         // true: 레이캐스트, false: 평면 사용
    [SerializeField] private float _groundHeight = 0f;            // _useGroundMask=false 시 바닥 높이
    [SerializeField] private float _formationSpacing = 1.5f;      // 포메이션 내 유닛 간격
    [SerializeField] private bool _useFormation = true;           // 다중 유닛 그리드 포메이션 사용

    [Header("UI")]
    [SerializeField] private bool _ignoreWhenPointerOverUI = true; // UI 위에서는 입력 무시

    [Header("Debug")]
    [SerializeField] private bool _drawSelectionRect = true;      // 드래그 선택 박스 표시
    [SerializeField] private Color _selectionFill = new(0.2f, 0.8f, 1f, 0.2f);
    [SerializeField] private Color _selectionBorder = new(0.2f, 0.8f, 1f, 0.9f);

    [Header("Camera")]
    [SerializeField] private Camera _camera;

    private readonly List<UnitSelectable> _selection = new();     // 현재 선택된 유닛 목록
    private Vector2 _dragStart;                                   // 드래그 시작 스크린 좌표
    private Vector2 _dragEnd;                                     // 드래그 현재/종료 스크린 좌표
    private bool _isDragging;                                     // 드래그 중인지 여부

    private Keyboard Keyboard => Keyboard.current;
    private Mouse Mouse => Mouse.current;

    private void Update()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null || Mouse == null)
            return;

        // UI 위에서는 선택/이동 입력 무시
        if (_ignoreWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        HandleSelectionInput();
        HandleMoveInput();
    }

    private void OnDisable()
    {
        _isDragging = false;
        ClearSelection();
    }

    #region Selection Input

    /// <summary>
    /// 좌클릭 선택 입력 처리. 클릭 vs 드래그 판별.
    /// </summary>
    private void HandleSelectionInput()
    {
        // 좌클릭 시작: 드래그 시작점 기록
        if (Mouse.leftButton.wasPressedThisFrame)
        {
            _dragStart = Mouse.position.ReadValue();
            _dragEnd = _dragStart;
            _isDragging = false;
        }

        // 좌클릭 유지: 드래그 거리 체크
        if (Mouse.leftButton.isPressed)
        {
            _dragEnd = Mouse.position.ReadValue();
            if (!_isDragging && (_dragEnd - _dragStart).sqrMagnitude > _dragThreshold * _dragThreshold)
                _isDragging = true;
        }

        // 좌클릭 해제: 드래그 선택 또는 클릭 선택 실행
        if (Mouse.leftButton.wasReleasedThisFrame)
        {
            if (_isDragging)
                SelectByDrag(_dragStart, _dragEnd);
            else
                SelectByClick();

            _isDragging = false;
        }
    }

    /// <summary>
    /// 클릭으로 단일 유닛 선택. Shift 누르면 기존 선택에 추가.
    /// </summary>
    private void SelectByClick()
    {
        var add = IsShiftPressed();

        // Shift 없이 빈 공간 클릭 시 선택 해제
        if (!add && _clearSelectionOnEmptyClick)
            ClearSelection();

        if (!TryGetSelectableUnderMouse(out var selectable))
            return;

        // Shift 없으면 기존 선택 해제 후 새로 선택
        if (!add)
            ClearSelection();

        AddToSelection(selectable);
    }

    /// <summary>
    /// 드래그 박스로 다중 유닛 선택. 박스 내 모든 유닛 선택.
    /// </summary>
    private void SelectByDrag(Vector2 start, Vector2 end)
    {
        var add = IsShiftPressed();
        if (!add)
            ClearSelection();

        var rect = GetScreenRect(start, end);

        // 모든 선택 가능한 유닛 중 박스 내부에 있는 것 선택
        foreach (var selectable in UnitSelectable.All)
        {
            if (selectable == null)
                continue;

            var screen = _camera.WorldToScreenPoint(selectable.transform.position);
            if (screen.z < 0f) // 카메라 뒤에 있으면 스킵
                continue;

            if (rect.Contains(new Vector2(screen.x, screen.y)))
                AddToSelection(selectable);
        }
    }

    /// <summary>
    /// 마우스 아래의 선택 가능한 유닛 탐색.
    /// </summary>
    private bool TryGetSelectableUnderMouse(out UnitSelectable selectable)
    {
        selectable = null;
        var ray = _camera.ScreenPointToRay(Mouse.position.ReadValue());

        // 레이캐스트로 모든 히트 검출 후 거리순 정렬
        var hits = Physics.RaycastAll(ray, 500f, _selectableMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // 가장 가까운 UnitSelectable 반환
        foreach (var hit in hits)
        {
            if (hit.collider == null)
                continue;

            selectable = hit.collider.GetComponentInParent<UnitSelectable>();
            if (selectable != null)
                return true;
        }

        return false;
    }

    #endregion

    #region Move Input

    /// <summary>
    /// 우클릭 이동 명령 처리.
    /// </summary>
    private void HandleMoveInput()
    {
        if (!Mouse.rightButton.wasPressedThisFrame)
            return;

        if (_selection.Count == 0)
            return;

        if (!TryGetGroundPoint(out var target))
            return;

        IssueMoveOrder(target);
    }

    /// <summary>
    /// 바닥 위치 계산. _useGroundMask에 따라 레이캐스트 또는 평면 사용.
    /// </summary>
    private bool TryGetGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;

        var ray = _camera.ScreenPointToRay(Mouse.position.ReadValue());

        // 옵션 1: 레이어 마스크로 바닥 레이캐스트
        if (_useGroundMask && Physics.Raycast(ray, out var hit, 1000f, _groundMask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        // 옵션 2: 고정 높이의 수평 평면과 교차점 계산
        var plane = new Plane(Vector3.up, new Vector3(0f, _groundHeight, 0f));
        if (plane.Raycast(ray, out var enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 선택된 유닛들에게 이동 명령. 다중 유닛은 그리드 포메이션으로 배치.
    /// </summary>
    private void IssueMoveOrder(Vector3 target)
    {
        if (_selection.Count == 0)
            return;

        // 다중 유닛 포메이션 이동
        if (_useFormation && _selection.Count > 1)
        {
            // 정사각형 그리드 크기 계산 (예: 4유닛 → 2x2, 5유닛 → 3x3)
            var gridSize = Mathf.CeilToInt(Mathf.Sqrt(_selection.Count));
            var half = (gridSize - 1) * 0.5f;

            for (var i = 0; i < _selection.Count; i++)
            {
                var selectable = _selection[i];
                if (selectable == null)
                    continue;

                // 그리드 내 위치 계산
                var x = i % gridSize;
                var y = i / gridSize;
                var offset = new Vector3((x - half) * _formationSpacing, 0f, (y - half) * _formationSpacing);
                IssueMoveTo(selectable, target + offset);
            }
        }
        else
        {
            // 단일 유닛 또는 포메이션 비활성화 시 같은 위치로 이동
            foreach (var selectable in _selection)
                IssueMoveTo(selectable, target);
        }
    }

    /// <summary>
    /// 개별 유닛에게 이동 명령 전달.
    /// </summary>
    private void IssueMoveTo(UnitSelectable selectable, Vector3 target)
    {
        if (selectable == null)
            return;

        var mover = selectable.GetComponent<UnitMover>();
        if (mover == null)
            return;

        // Y축은 유닛의 현재 높이 유지
        var position = target;
        position.y = selectable.transform.position.y;
        mover.SetDestination(position);
    }

    #endregion

    #region Selection Management

    /// <summary>
    /// 유닛을 선택 목록에 추가.
    /// </summary>
    private void AddToSelection(UnitSelectable selectable)
    {
        if (selectable == null || _selection.Contains(selectable))
            return;

        _selection.Add(selectable);
        selectable.SetSelected(true);
    }

    /// <summary>
    /// 모든 선택 해제.
    /// </summary>
    private void ClearSelection()
    {
        foreach (var unit in _selection)
        {
            if (unit != null)
                unit.SetSelected(false);
        }

        _selection.Clear();
    }

    private bool IsShiftPressed()
    {
        if (Keyboard == null)
            return false;

        return Keyboard.leftShiftKey.isPressed || Keyboard.rightShiftKey.isPressed;
    }

    #endregion

    #region UI Drawing

    /// <summary>
    /// 스크린 좌표 두 점으로 Rect 생성.
    /// </summary>
    private static Rect GetScreenRect(Vector2 start, Vector2 end)
    {
        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>
    /// 드래그 선택 박스 OnGUI 렌더링.
    /// </summary>
    private void OnGUI()
    {
        if (!_drawSelectionRect || !_isDragging)
            return;

        var rect = GetGuiRect(_dragStart, _dragEnd);
        DrawScreenRect(rect, _selectionFill);
        DrawScreenRectBorder(rect, 2f, _selectionBorder);
    }

    /// <summary>
    /// 스크린 좌표를 GUI 좌표로 변환 (Y축 반전).
    /// </summary>
    private static Rect GetGuiRect(Vector2 start, Vector2 end)
    {
        var rect = GetScreenRect(start, end);
        rect.y = Screen.height - rect.y - rect.height;
        return rect;
    }

    private static void DrawScreenRect(Rect rect, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);              // 상단
        DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);  // 하단
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);             // 좌측
        DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color); // 우측
    }

    #endregion
}
