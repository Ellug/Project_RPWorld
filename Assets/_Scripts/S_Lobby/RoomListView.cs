using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomListView : MonoBehaviour
{
    [SerializeField] private RectTransform _contentRoot;
    [SerializeField] private RoomUnitView _roomUnitPrefab;
    [SerializeField] private TextMeshProUGUI _emptyText;

    private readonly List<RoomUnitView> _items = new();

    private void Awake()
    {
        AutoWireIfNeeded();
    }

    public void Render(IReadOnlyList<RoomListItemData> rooms, Action<RoomListItemData> onJoin)
    {
        AutoWireIfNeeded();

        if (_roomUnitPrefab == null || _contentRoot == null)
        {
            Debug.LogWarning("RoomListView is missing prefab or content root.");
            return;
        }

        var count = rooms?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            if (i >= _items.Count)
            {
                var instance = Instantiate(_roomUnitPrefab, _contentRoot);
                _items.Add(instance);
            }

            var item = _items[i];
            var data = rooms[i];
            var canJoin = data.SessionInfo.IsOpen && (data.SessionInfo.MaxPlayers <= 0 || data.SessionInfo.PlayerCount < data.SessionInfo.MaxPlayers);
            item.gameObject.SetActive(true);
            item.SetData(
                data.SessionInfo.Name,
                data.SessionInfo.PlayerCount,
                data.SessionInfo.MaxPlayers,
                data.HasPassword,
                data.HostName,
                canJoin,
                () => onJoin?.Invoke(data));
        }

        for (int i = count; i < _items.Count; i++)
            _items[i].gameObject.SetActive(false);

        if (_emptyText != null)
            _emptyText.gameObject.SetActive(count == 0);
    }

    private void AutoWireIfNeeded()
    {
        if (_contentRoot == null)
        {
            var scroll = GetComponent<ScrollRect>();
            if (scroll != null)
                _contentRoot = scroll.content;
        }

        if (_emptyText == null)
        {
            var texts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                if (text != null && text.gameObject.name == "EmptyText")
                {
                    _emptyText = text;
                    break;
                }
            }
        }
    }
}
