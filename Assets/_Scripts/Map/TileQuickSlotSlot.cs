using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TileQuickSlotSlot : MonoBehaviour, IPointerClickHandler, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, ITileDragSource
{
    [SerializeField] private int _slotIndex;
    [SerializeField] private Image _background;
    [SerializeField] private Image _icon;
    [Header("Visuals")]
    [SerializeField] private Color _selectedColor = new(1f, 0.85f, 0.2f, 0.8f);
    [SerializeField] private Color _unselectedColor = new(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color _emptyIconColor = new(1f, 1f, 1f, 0f);
    [SerializeField] private Vector2 _dragIconSize = new(40f, 40f);

    private TileQuickSlotController _controller;
    private Canvas _rootCanvas;
    private GameObject _dragIcon;

    public int SlotIndex => _slotIndex;
    public Image Background => _background;
    public Image Icon => _icon;

    public int TileId { get; private set; } = -1;

    public void Initialize(TileQuickSlotController controller, int slotIndex, Canvas rootCanvas, Image background, Image icon)
    {
        _controller = controller;
        _slotIndex = slotIndex;
        _rootCanvas = rootCanvas;
        _background = background != null ? background : GetComponent<Image>();
        if (icon != null)
        {
            _icon = icon;
        }
        else
        {
            var iconTransform = transform.Find("Icon");
            _icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        }
    }

    public void SetTile(int tileId, Sprite sprite, Color color)
    {
        TileId = tileId;
        if (_icon != null)
        {
            _icon.sprite = sprite;
            _icon.color = color;
        }
    }

    public void Clear()
    {
        TileId = -1;
        if (_icon != null)
            _icon.color = _emptyIconColor;
    }

    public void SetSelected(bool selected)
    {
        if (_background == null)
            return;

        _background.color = selected ? _selectedColor : _unselectedColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _controller?.SelectSlot(_slotIndex);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        var source = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ITileDragSource>() : null;
        if (source == null)
            return;

        _controller?.AssignTile(_slotIndex, source.TileId);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (TileId < 0 || _rootCanvas == null)
            return;

        _dragIcon = new GameObject("DragIcon");
        _dragIcon.layer = gameObject.layer;
        var rect = _dragIcon.AddComponent<RectTransform>();
        rect.SetParent(_rootCanvas.transform, false);
        rect.sizeDelta = _dragIconSize;

        var image = _dragIcon.AddComponent<Image>();
        if (_icon != null)
        {
            image.sprite = _icon.sprite;
            image.color = _icon.color;
        }

        var group = _dragIcon.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIcon == null)
            return;

        _dragIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragIcon == null)
            return;

        Destroy(_dragIcon);
        _dragIcon = null;
    }
}
