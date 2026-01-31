using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UnitSelectionManager : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private float _dragThreshold = 8f;
    [SerializeField] private LayerMask _selectableMask = ~0;
    [SerializeField] private bool _clearSelectionOnEmptyClick = true;

    [Header("Movement")]
    [SerializeField] private LayerMask _groundMask = ~0;
    [SerializeField] private bool _useGroundMask = false;
    [SerializeField] private float _groundHeight = 0f;
    [SerializeField] private float _formationSpacing = 1.5f;
    [SerializeField] private bool _useFormation = true;

    [Header("UI")]
    [SerializeField] private bool _ignoreWhenPointerOverUI = true;

    [Header("Debug")]
    [SerializeField] private bool _drawSelectionRect = true;
    [SerializeField] private Color _selectionFill = new(0.2f, 0.8f, 1f, 0.2f);
    [SerializeField] private Color _selectionBorder = new(0.2f, 0.8f, 1f, 0.9f);

    private readonly List<UnitSelectable> _selection = new();
    private Camera _camera;
    private Vector2 _dragStart;
    private Vector2 _dragEnd;
    private bool _isDragging;

    private Keyboard Keyboard => Keyboard.current;
    private Mouse Mouse => Mouse.current;

    private void Update()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null || Mouse == null)
            return;

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

    private void HandleSelectionInput()
    {
        if (Mouse.leftButton.wasPressedThisFrame)
        {
            _dragStart = Mouse.position.ReadValue();
            _dragEnd = _dragStart;
            _isDragging = false;
        }

        if (Mouse.leftButton.isPressed)
        {
            _dragEnd = Mouse.position.ReadValue();
            if (!_isDragging && (_dragEnd - _dragStart).sqrMagnitude > _dragThreshold * _dragThreshold)
                _isDragging = true;
        }

        if (Mouse.leftButton.wasReleasedThisFrame)
        {
            if (_isDragging)
            {
                SelectByDrag(_dragStart, _dragEnd);
            }
            else
            {
                SelectByClick();
            }

            _isDragging = false;
        }
    }

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

    private void SelectByClick()
    {
        var add = IsShiftPressed();

        if (!add && _clearSelectionOnEmptyClick)
            ClearSelection();

        if (!TryGetSelectableUnderMouse(out var selectable))
            return;

        if (!add)
            ClearSelection();

        AddToSelection(selectable);
    }

    private void SelectByDrag(Vector2 start, Vector2 end)
    {
        var add = IsShiftPressed();
        if (!add)
            ClearSelection();

        var rect = GetScreenRect(start, end);
        foreach (var selectable in UnitSelectable.All)
        {
            if (selectable == null)
                continue;

            var screen = _camera.WorldToScreenPoint(selectable.transform.position);
            if (screen.z < 0f)
                continue;

            if (rect.Contains(new Vector2(screen.x, screen.y)))
                AddToSelection(selectable);
        }
    }

    private bool TryGetSelectableUnderMouse(out UnitSelectable selectable)
    {
        selectable = null;
        var ray = _camera.ScreenPointToRay(Mouse.position.ReadValue());
        var hits = Physics.RaycastAll(ray, 500f, _selectableMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
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

    private bool TryGetGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;

        var ray = _camera.ScreenPointToRay(Mouse.position.ReadValue());
        if (_useGroundMask && Physics.Raycast(ray, out var hit, 1000f, _groundMask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        var plane = new Plane(Vector3.up, new Vector3(0f, _groundHeight, 0f));
        if (plane.Raycast(ray, out var enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    private void IssueMoveOrder(Vector3 target)
    {
        if (_selection.Count == 0)
            return;

        if (_useFormation && _selection.Count > 1)
        {
            var gridSize = Mathf.CeilToInt(Mathf.Sqrt(_selection.Count));
            var half = (gridSize - 1) * 0.5f;

            for (var i = 0; i < _selection.Count; i++)
            {
                var selectable = _selection[i];
                if (selectable == null)
                    continue;

                var x = i % gridSize;
                var y = i / gridSize;
                var offset = new Vector3((x - half) * _formationSpacing, 0f, (y - half) * _formationSpacing);
                IssueMoveTo(selectable, target + offset);
            }
        }
        else
        {
            foreach (var selectable in _selection)
                IssueMoveTo(selectable, target);
        }
    }

    private void IssueMoveTo(UnitSelectable selectable, Vector3 target)
    {
        if (selectable == null)
            return;

        var mover = selectable.GetComponent<UnitMover>();
        if (mover == null)
            return;

        var position = target;
        position.y = selectable.transform.position.y;
        mover.SetDestination(position);
    }

    private void AddToSelection(UnitSelectable selectable)
    {
        if (selectable == null || _selection.Contains(selectable))
            return;

        _selection.Add(selectable);
        selectable.SetSelected(true);
    }

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

    private static Rect GetScreenRect(Vector2 start, Vector2 end)
    {
        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void OnGUI()
    {
        if (!_drawSelectionRect || !_isDragging)
            return;

        var rect = GetGuiRect(_dragStart, _dragEnd);
        DrawScreenRect(rect, _selectionFill);
        DrawScreenRectBorder(rect, 2f, _selectionBorder);
    }

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
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }
}
