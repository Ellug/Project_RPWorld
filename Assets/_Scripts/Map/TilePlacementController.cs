using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TilePlacementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldMapState _worldMapState;
    [SerializeField] private TilePalette _tilePalette;

    [Header("Placement")]
    [SerializeField] private float _tileSizeOverride = 0f;
    [SerializeField] private float _tileHeightOverride = 0f;
    [SerializeField] private Key _nextTileKey = Key.E;
    [SerializeField] private Key _prevTileKey = Key.Q;
    [SerializeField] private float _previewYOffset = 0.02f;

    private GameObject _preview;
    private MeshRenderer _previewRenderer;
    private Camera _camera;
    private int _selectedTileId;

    private Keyboard Keyboard => Keyboard.current;
    private Mouse Mouse => Mouse.current;

    private void Awake()
    {
        AutoWireIfNeeded();
    }

    private void Start()
    {
        SetupPreview();
        UpdatePreviewMaterial();
    }

    private void Update()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null || _worldMapState == null)
        {
            SetPreviewVisible(false);
            return;
        }

        if (Keyboard == null || Mouse == null)
        {
            SetPreviewVisible(false);
            return;
        }

        if (_tilePalette == null)
            _tilePalette = _worldMapState.TilePalette;

        HandleTileSelectionInput();

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetPreviewVisible(false);
            return;
        }

        var mousePosition = Mouse.position.ReadValue();
        var plane = new Plane(Vector3.up, new Vector3(0f, GetTileHeight(), 0f));
        var ray = _camera.ScreenPointToRay(mousePosition);
        if (!plane.Raycast(ray, out var enter))
        {
            SetPreviewVisible(false);
            return;
        }

        var hit = ray.GetPoint(enter);
        var key = _worldMapState.PositionToKey(hit);
        if (!_worldMapState.IsInsideBounds(key))
        {
            SetPreviewVisible(false);
            return;
        }

        var snapped = _worldMapState.KeyToPosition(key);
        UpdatePreviewPosition(snapped);

        // 좌클릭: 타일 배치
        if (Mouse.leftButton.wasPressedThisFrame)
            _worldMapState.RequestPlaceTile(snapped, _selectedTileId);

        // 우클릭: 타일 삭제
        if (Mouse.rightButton.wasPressedThisFrame)
            _worldMapState.RequestRemoveTile(snapped);
    }

    private void HandleTileSelectionInput()
    {
        var maxId = GetMaxTileId();
        if (maxId <= 0)
            return;

        var changed = false;

        if (Keyboard[_nextTileKey].wasPressedThisFrame)
        {
            _selectedTileId = (_selectedTileId + 1) % (maxId + 1);
            changed = true;
        }
        else if (Keyboard[_prevTileKey].wasPressedThisFrame)
        {
            _selectedTileId--;
            if (_selectedTileId < 0)
                _selectedTileId = maxId;
            changed = true;
        }

        var scroll = Mouse.scroll.ReadValue().y;
        if (scroll > 0f)
        {
            _selectedTileId = (_selectedTileId + 1) % (maxId + 1);
            changed = true;
        }
        else if (scroll < 0f)
        {
            _selectedTileId--;
            if (_selectedTileId < 0)
                _selectedTileId = maxId;
            changed = true;
        }

        if (changed)
            UpdatePreviewMaterial();
    }

    private void SetupPreview()
    {
        if (_preview != null)
            return;

        _preview = GameObject.CreatePrimitive(PrimitiveType.Plane);
        _preview.name = "TilePreview";
        _preview.transform.SetParent(transform, false);
        _previewRenderer = _preview.GetComponent<MeshRenderer>();

        var collider = _preview.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        ApplyPreviewScale();
    }

    private void ApplyPreviewScale()
    {
        if (_preview == null)
            return;

        var tileSize = GetTileSize();
        var scale = tileSize > 0f ? tileSize / 10f : 0.1f;
        _preview.transform.localScale = new Vector3(scale, 1f, scale);
    }

    private void UpdatePreviewPosition(Vector3 snappedPosition)
    {
        if (_preview == null)
            return;

        _preview.transform.position = snappedPosition + Vector3.up * _previewYOffset;
        SetPreviewVisible(true);
    }

    private void UpdatePreviewMaterial()
    {
        if (_previewRenderer == null || _tilePalette == null)
            return;

        var material = _tilePalette.GetMaterial(_selectedTileId);
        if (material != null)
            _previewRenderer.sharedMaterial = material;
    }

    private void SetPreviewVisible(bool visible)
    {
        if (_preview != null)
            _preview.SetActive(visible);
    }

    private float GetTileSize()
    {
        if (_tileSizeOverride > 0f)
            return _tileSizeOverride;

        return _worldMapState != null ? _worldMapState.TileSize : 1f;
    }

    private float GetTileHeight()
    {
        if (_tileHeightOverride != 0f)
            return _tileHeightOverride;

        return _worldMapState != null ? _worldMapState.TileHeight : 0f;
    }

    private int GetMaxTileId()
    {
        if (_tilePalette == null)
            return 0;

        return Mathf.Max(0, _tilePalette.Count - 1);
    }

    private void AutoWireIfNeeded()
    {
        if (_worldMapState == null)
            _worldMapState = FindFirstObjectByType<WorldMapState>();
    }
}
