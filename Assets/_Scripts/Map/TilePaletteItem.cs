using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 타일 팔레트 UI 항목. 드래그하여 퀵슬롯에 등록 가능
public class TilePaletteItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ITileDragSource
{
    [SerializeField] private int _tileId;
    [SerializeField] private Image _icon;
    [SerializeField] private Text _label;
    [SerializeField] private Vector2 _dragIconSize = new(40f, 40f);

    private TileQuickSlotController _controller;
    private Canvas _rootCanvas;
    private GameObject _dragIcon;

    public int TileId => _tileId;
    public Image Icon => _icon;
    public Text Label => _label;

    public void Initialize(TileQuickSlotController controller, int tileId, Canvas rootCanvas, Image icon, Text label)
    {
        _controller = controller;
        _tileId = tileId;
        _rootCanvas = rootCanvas;
        _icon = icon;
        _label = label;
    }

    public void AutoWireIfNeeded()
    {
        if (_icon == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                _icon = iconTransform.GetComponent<Image>();
        }

        if (_label == null)
        {
            var labelTransform = transform.Find("Label");
            if (labelTransform != null)
                _label = labelTransform.GetComponent<Text>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_rootCanvas == null || _icon == null)
            return;

        _dragIcon = new GameObject("DragIcon");
        _dragIcon.layer = gameObject.layer;
        var rect = _dragIcon.AddComponent<RectTransform>();
        rect.SetParent(_rootCanvas.transform, false);
        rect.sizeDelta = _dragIconSize;

        var image = _dragIcon.AddComponent<Image>();
        image.sprite = _icon.sprite;
        image.color = _icon.color;

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
