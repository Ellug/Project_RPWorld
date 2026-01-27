using TMPro;
using UnityEngine;

public class CreateRoomPanelView : MonoBehaviour
{
    [SerializeField] private TMP_InputField _roomNameInput;
    [SerializeField] private TMP_InputField _passwordInput;
    [SerializeField] private LobbyManager _lobbyManager;

    private void Awake()
    {
        AutoWireIfNeeded();
    }

    public void SetLobbyManager(LobbyManager lobbyManager)
    {
        _lobbyManager = lobbyManager;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        ClearInputs();
        _roomNameInput?.Select();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void OnClickApply()
    {
        AutoWireIfNeeded();

        var roomName = _roomNameInput != null ? _roomNameInput.text.Trim() : string.Empty;
        var password = _passwordInput != null ? _passwordInput.text.Trim() : string.Empty;

        _lobbyManager?.RequestCreateRoom(roomName, password);
    }

    public void OnClickCancel()
    {
        _lobbyManager?.CancelCreateRoom();
    }

    public void ClearInputs()
    {
        if (_roomNameInput != null)
            _roomNameInput.text = string.Empty;
        if (_passwordInput != null)
            _passwordInput.text = string.Empty;
    }

    private void AutoWireIfNeeded()
    {
        if (_lobbyManager == null)
            _lobbyManager = FindFirstObjectByType<LobbyManager>();

        if (_roomNameInput == null)
            _roomNameInput = FindInputField("RoomName");

        if (_passwordInput == null)
            _passwordInput = FindInputField("RoomPW");
    }

    private TMP_InputField FindInputField(string objectName)
    {
        var inputs = GetComponentsInChildren<TMP_InputField>(true);
        foreach (var input in inputs)
        {
            if (input != null && input.gameObject.name == objectName)
                return input;
        }

        return null;
    }
}
