using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 방 목록의 개별 항목 UI. 방 이름/인원/잠금 상태 표시
public class RoomUnitView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _roomName;
    [SerializeField] private TextMeshProUGUI _playerCount;
    [SerializeField] private Image _lockIcon;
    [SerializeField] private Button _joinButton;
    [SerializeField] private TextMeshProUGUI _hostName;

    private Action _onJoin;

    public void SetData(
        string roomName,
        int playerCount,
        int maxPlayers,
        bool isLocked,
        string hostName,
        bool canJoin,
        Action onJoin)
    {
        if (_hostName == null)
        {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                if (text != null && text.gameObject.name == "Host Text (TMP)")
                {
                    _hostName = text;
                    break;
                }
            }
        }

        _onJoin = onJoin;

        if (_roomName != null)
            _roomName.text = roomName;

        if (_playerCount != null)
        {
            _playerCount.text = maxPlayers > 0
                ? $"{playerCount}/{maxPlayers}"
                : playerCount.ToString();
        }

        if (_hostName != null)
            _hostName.text = string.IsNullOrEmpty(hostName) ? "Host" : hostName;

        if (_lockIcon != null)
            _lockIcon.gameObject.SetActive(isLocked);

        if (_joinButton != null)
        {
            _joinButton.onClick.RemoveListener(HandleJoinClicked);
            _joinButton.onClick.AddListener(HandleJoinClicked);
            _joinButton.interactable = canJoin;
        }
    }

    private void HandleJoinClicked()
    {
        _onJoin?.Invoke();
    }
}
