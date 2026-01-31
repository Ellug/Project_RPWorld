using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TileQuickSlotController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldMapState _worldMapState;
    [SerializeField] private TilePalette _tilePalette;
    [SerializeField] private TilePlacementController _tilePlacementController;
    [SerializeField] private StageInteractionModeManager _modeManager;
    [SerializeField] private RectTransform _slotContainer;

    [Header("Slots")]
    [SerializeField] private bool _showSlotKeyLabels = true;
    [SerializeField] private bool _updateSlotLabelText = true;

    [Header("Tile List")]
    [SerializeField] private Key _toggleListKey = Key.I;
    [SerializeField] private bool _onlyInTileMode = true;
    [SerializeField] private bool _hideListWhenNotInTileMode = true;
    [SerializeField] private bool _hideSlotsWhenNotInTileMode = true;
    [SerializeField] private TilePaletteItem _tileListItemPrefab;
    [SerializeField] private bool _showTileListLabels = true;

    [Header("Input")]
    [SerializeField] private bool _enableNumberKeys = true;
    [SerializeField] private bool _ignoreWhenPointerOverUI = false;

    [Header("Fallback Assets")]
    [SerializeField] private Sprite _fallbackSprite;

    private readonly List<TileQuickSlotSlot> _slots = new();
    private readonly Dictionary<Texture, Sprite> _spriteCache = new();
    private int _selectedSlotIndex = -1;
    private bool _listVisible;

    private Canvas _rootCanvas;
    [SerializeField] private CanvasGroup _slotCanvasGroup;
    [SerializeField] private GameObject _tileListPanel;
    [SerializeField] private RectTransform _tileListContent;
    private Sprite _defaultSprite;

    public int SelectedTileId { get; private set; } = -1;

    public event Action<int> SelectedTileChanged;

    private Keyboard Keyboard => Keyboard.current;

    private void Awake()
    {
        AutoWireIfNeeded();
        BuildSlotsIfNeeded();
        BuildTileListIfNeeded();
    }

    private void OnEnable()
    {
        if (_modeManager != null)
            _modeManager.ModeChanged += HandleModeChanged;

        if (_modeManager != null)
            ApplyModeVisibility(_modeManager.CurrentMode);
    }

    private void OnDisable()
    {
        if (_modeManager != null)
            _modeManager.ModeChanged -= HandleModeChanged;
    }

    private void Start()
    {
        if (_tileListPanel == null)
            BuildTileListIfNeeded();

        if (_modeManager != null)
            ApplyModeVisibility(_modeManager.CurrentMode);
    }

    private void Update()
    {
        if (_ignoreWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (_onlyInTileMode && _modeManager != null && _modeManager.CurrentMode != StageInteractionMode.TilePlacement)
        {
            if (_hideListWhenNotInTileMode)
                SetTileListVisible(false);
            if (_hideSlotsWhenNotInTileMode)
                SetSlotPanelVisible(false);
            return;
        }

        if (_enableNumberKeys)
            HandleNumberKeySelection();

        if (Keyboard != null && Keyboard[_toggleListKey].wasPressedThisFrame)
        {
            BuildTileListIfNeeded();

            SetTileListVisible(!_listVisible);
        }
    }

    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return;

        _selectedSlotIndex = slotIndex;
        for (var i = 0; i < _slots.Count; i++)
            _slots[i].SetSelected(i == _selectedSlotIndex);

        var slot = _slots[slotIndex];
        SetSelectedTile(slot.TileId);
    }

    public void AssignTile(int slotIndex, int tileId)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return;

        if (_tilePalette == null || _tilePalette.Count == 0)
            return;

        if (tileId >= 0)
            tileId = Mathf.Clamp(tileId, 0, _tilePalette.Count - 1);

        var slot = _slots[slotIndex];
        if (tileId < 0)
        {
            slot.Clear();
        }
        else
        {
            var sprite = GetTileSprite(tileId);
            if (sprite != null)
                slot.SetTile(tileId, sprite, Color.white);
            else
                slot.SetTile(tileId, DefaultSprite, GetTileColor(tileId));
        }

        if (_selectedSlotIndex == slotIndex)
            SetSelectedTile(slot.TileId);
    }

    public void SetTileListVisible(bool visible)
    {
        _listVisible = visible;
        if (_tileListPanel != null)
            _tileListPanel.SetActive(visible);
    }

    private void SetSlotPanelVisible(bool visible)
    {
        if (_slotContainer == null)
            return;

        if (_slotCanvasGroup == null)
            _slotCanvasGroup = _slotContainer.GetComponent<CanvasGroup>();
        if (_slotCanvasGroup == null)
        {
            Debug.LogWarning("[TileQuickSlotController] Slot Panel is missing CanvasGroup.");
            return;
        }

        _slotCanvasGroup.alpha = visible ? 1f : 0f;
        _slotCanvasGroup.blocksRaycasts = visible;
        _slotCanvasGroup.interactable = visible;
    }

    private void SetSelectedTile(int tileId)
    {
        SelectedTileId = tileId;
        if (_tilePlacementController != null)
            _tilePlacementController.SetSelectedTileId(tileId);

        SelectedTileChanged?.Invoke(tileId);
    }

    private void HandleNumberKeySelection()
    {
        if (Keyboard == null || _slots.Count == 0)
            return;

        if (Keyboard.digit1Key.wasPressedThisFrame) SelectSlot(0);
        else if (Keyboard.digit2Key.wasPressedThisFrame) SelectSlot(1);
        else if (Keyboard.digit3Key.wasPressedThisFrame) SelectSlot(2);
        else if (Keyboard.digit4Key.wasPressedThisFrame) SelectSlot(3);
        else if (Keyboard.digit5Key.wasPressedThisFrame) SelectSlot(4);
        else if (Keyboard.digit6Key.wasPressedThisFrame) SelectSlot(5);
        else if (Keyboard.digit7Key.wasPressedThisFrame) SelectSlot(6);
        else if (Keyboard.digit8Key.wasPressedThisFrame) SelectSlot(7);
        else if (Keyboard.digit9Key.wasPressedThisFrame) SelectSlot(8);
        else if (Keyboard.digit0Key.wasPressedThisFrame) SelectSlot(9);
    }

    private void HandleModeChanged(StageInteractionMode mode)
    {
        ApplyModeVisibility(mode);
    }

    private void ApplyModeVisibility(StageInteractionMode mode)
    {
        if (!_onlyInTileMode)
            return;

        var visible = mode == StageInteractionMode.TilePlacement;
        if (_hideSlotsWhenNotInTileMode)
            SetSlotPanelVisible(visible);

        if (!visible && _hideListWhenNotInTileMode)
            SetTileListVisible(false);
    }

    private void AutoWireIfNeeded()
    {
        if (_slotContainer == null)
            _slotContainer = GetComponent<RectTransform>();

        if (_worldMapState == null)
            _worldMapState = FindFirstObjectByType<WorldMapState>();

        if (_tilePalette == null && _worldMapState != null)
            _tilePalette = _worldMapState.TilePalette;

        if (_tilePlacementController == null)
            _tilePlacementController = FindFirstObjectByType<TilePlacementController>();

        if (_modeManager == null)
            _modeManager = FindFirstObjectByType<StageInteractionModeManager>();

        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();

        if (_slotCanvasGroup == null && _slotContainer != null)
            _slotCanvasGroup = _slotContainer.GetComponent<CanvasGroup>();
    }

    private void BuildSlotsIfNeeded()
    {
        if (_slotContainer == null)
            return;

        _slots.Clear();
        _slotContainer.GetComponentsInChildren(true, _slots);
        if (_slots.Count == 0)
        {
            Debug.LogWarning("[TileQuickSlotController] No TileQuickSlotSlot found under Slot Container.");
            return;
        }

        CacheSlotControllerReferences();
    }

    private void CacheSlotControllerReferences()
    {
        for (var i = 0; i < _slots.Count; i++)
            _slots[i].Initialize(this, i, _rootCanvas, _slots[i].Background, _slots[i].Icon);

        EnsureSlotLabels();
    }

    private void EnsureSlotLabels()
    {
        if (_slots.Count == 0)
            return;

        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot == null)
                continue;

            var labelTransform = slot.transform.Find("KeyLabel");
            if (labelTransform == null)
                continue;

            labelTransform.gameObject.SetActive(_showSlotKeyLabels);
            if (!_showSlotKeyLabels)
                continue;

            var label = labelTransform.GetComponent<Text>();
            if (_updateSlotLabelText && label != null)
                label.text = GetSlotKeyLabel(slot.SlotIndex);
        }
    }

    private static string GetSlotKeyLabel(int slotIndex)
    {
        if (slotIndex == 9)
            return "0";
        return (slotIndex + 1).ToString();
    }

    private void BuildTileListIfNeeded()
    {
        if (_rootCanvas == null || _tilePalette == null)
            return;

        if (_tileListPanel == null)
        {
            var existing = _rootCanvas.transform.Find("TileListPanel");
            if (existing != null)
                _tileListPanel = existing.gameObject;
        }

        if (_tileListPanel == null)
        {
            Debug.LogWarning("[TileQuickSlotController] TileListPanel is not assigned or found in the Canvas.");
            return;
        }

        if (_tileListContent == null)
            TryFindTileListContent();

        if (_tileListContent == null)
        {
            Debug.LogWarning("[TileQuickSlotController] TileListContent is not assigned or found under TileListPanel.");
            return;
        }

        if (_tileListContent.childCount == 0)
            BuildTileListItems();
    }

    private void BuildTileListItems()
    {
        if (_tileListContent == null || _tilePalette == null)
            return;
        if (_tileListItemPrefab == null)
        {
            Debug.LogWarning("[TileQuickSlotController] Tile list item prefab is not assigned.");
            return;
        }

        for (var i = 0; i < _tilePalette.Count; i++)
        {
            var item = Instantiate(_tileListItemPrefab, _tileListContent);
            item.name = $"Tile {i}";
            item.AutoWireIfNeeded();

            var icon = item.Icon;
            var text = item.Label;
            if (icon == null)
            {
                Debug.LogWarning($"[TileQuickSlotController] Tile list item prefab is missing Icon Image. TileId={i}");
                Destroy(item.gameObject);
                continue;
            }

            var sprite = GetTileSprite(i);
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color = Color.white;
            }
            else
            {
                icon.sprite = DefaultSprite;
                icon.color = GetTileColor(i);
            }

            if (text != null)
            {
                text.text = GetTileDisplayName(i);
                text.gameObject.SetActive(_showTileListLabels);
            }
            else if (_showTileListLabels)
            {
                Debug.LogWarning($"[TileQuickSlotController] Tile list item prefab is missing Label Text. TileId={i}");
            }

            item.Initialize(this, i, _rootCanvas, icon, text);
        }
    }

    private void TryFindTileListContent()
    {
        if (_tileListPanel == null)
            return;

        var content = _tileListPanel.transform.Find("Scroll/Viewport/Content");
        if (content == null)
            content = _tileListPanel.transform.Find("Viewport/Content");
        if (content == null)
            content = _tileListPanel.transform.Find("Content");
        if (content != null)
            _tileListContent = content as RectTransform;
    }

    private Color GetTileColor(int tileId)
    {
        if (_tilePalette == null)
            return Color.white;

        var material = _tilePalette.GetMaterial(tileId);
        if (material == null)
            return Color.white;

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");

        return Color.white;
    }

    private string GetTileDisplayName(int tileId)
    {
        if (_tilePalette == null)
            return $"Tile {tileId}";

        var material = _tilePalette.GetMaterial(tileId);
        if (material == null)
            return $"Tile {tileId}";

        var name = material.name;
        if (string.IsNullOrWhiteSpace(name))
            return $"Tile {tileId}";

        return name.Replace(" (Instance)", string.Empty);
    }

    private Sprite GetTileSprite(int tileId)
    {
        if (_tilePalette == null)
            return null;

        var material = _tilePalette.GetMaterial(tileId);
        if (material == null)
            return null;

        Texture texture = null;
        if (material.HasProperty("_BaseMap"))
            texture = material.GetTexture("_BaseMap");
        if (texture == null && material.HasProperty("_MainTex"))
            texture = material.GetTexture("_MainTex");
        if (texture == null)
            return null;

        if (_spriteCache.TryGetValue(texture, out var cached))
            return cached;

        if (texture is not Texture2D tex2D)
            return null;

        var sprite = Sprite.Create(tex2D, new Rect(0f, 0f, tex2D.width, tex2D.height), new Vector2(0.5f, 0.5f), 100f);
        _spriteCache[texture] = sprite;
        return sprite;
    }
    private Sprite DefaultSprite
    {
        get
        {
            if (_defaultSprite == null)
                _defaultSprite = ResolveDefaultSprite();
            return _defaultSprite;
        }
    }

    private Sprite ResolveDefaultSprite()
    {
        if (_fallbackSprite != null)
            return _fallbackSprite;

        var panelImage = _slotContainer != null ? _slotContainer.GetComponent<Image>() : null;
        if (panelImage != null && panelImage.sprite != null)
            return panelImage.sprite;

        for (var i = 0; i < _slots.Count; i++)
        {
            var background = _slots[i] != null ? _slots[i].Background : null;
            if (background != null && background.sprite != null)
                return background.sprite;
        }

        return CreateFallbackSprite();
    }

    private Sprite CreateFallbackSprite()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[]
        {
            Color.white, Color.white,
            Color.white, Color.white
        });
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
    }
}
