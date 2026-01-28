using TMPro;
using UnityEngine;

public class JoinPwPanelView : MonoBehaviour
{
    [SerializeField] private TMP_InputField _passwordInput;
    [SerializeField] private TextMeshProUGUI _wrongText;
    [SerializeField] private LobbyManager _lobbyManager;

    void Awake()
    {
        AutoWireIfNeeded();
        HideError();
    }

    public void SetLobbyManager(LobbyManager lobbyManager)
    {
        _lobbyManager = lobbyManager;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        HideError();
        if (_passwordInput != null)
        {
            _passwordInput.text = string.Empty;
            _passwordInput.Select();
        }
    }

    public void Hide()
    {
        HideError();
        gameObject.SetActive(false);
    }

    public void OnClickApply()
    {
        AutoWireIfNeeded();
        var password = _passwordInput != null ? _passwordInput.text.Trim() : string.Empty;
        _lobbyManager?.SubmitJoinPassword(password);
    }

    public void OnClickCancel()
    {
        _lobbyManager?.CancelJoinPassword();
    }

    public void ShowError(string message)
    {
        if (_wrongText != null)
        {
            _wrongText.text = string.IsNullOrEmpty(message) ? _wrongText.text : message;
            _wrongText.gameObject.SetActive(true);
        }
    }

    public void HideError()
    {
        if (_wrongText != null)
            _wrongText.gameObject.SetActive(false);
    }

    private void AutoWireIfNeeded()
    {
        if (_lobbyManager == null)
            _lobbyManager = FindFirstObjectByType<LobbyManager>();

        if (_passwordInput == null)
            _passwordInput = FindInputField("RoomPW");

        if (_wrongText == null)
            _wrongText = FindText("PWWrong Text (TMP)");
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

    private TextMeshProUGUI FindText(string objectName)
    {
        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in texts)
        {
            if (text != null && text.gameObject.name == objectName)
                return text;
        }

        return null;
    }
}
