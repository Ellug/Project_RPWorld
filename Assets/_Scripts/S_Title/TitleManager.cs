using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// 타이틀 씬 관리자. 로그인/회원가입 UI 및 Photon 로비 연결 처리.
public class TitleManager : MonoBehaviour
{
    [Header("Press Start")]
    [SerializeField] private TextMeshProUGUI _pressStartText;
    [SerializeField] private float _blinkInterval = 0.5f;

    [Header("Sign In Panel")]
    [SerializeField] private GameObject _signInPanel;
    [SerializeField] private TMP_InputField _signInEmailInput;
    [SerializeField] private TMP_InputField _signInPasswordInput;
    [SerializeField] private Button _signInButton;
    [SerializeField] private Button _goToSignUpButton;

    [Header("Sign Up Panel")]
    [SerializeField] private GameObject _signUpPanel;
    [SerializeField] private TMP_InputField _signUpEmailInput;
    [SerializeField] private TMP_InputField _signUpPasswordInput;
    [SerializeField] private TMP_InputField _signUpPasswordConfirmInput;
    [SerializeField] private TMP_InputField _signUpNicknameInput;
    [SerializeField] private Button _signUpButton;
    [SerializeField] private Button _cancelButton;

    [Header("Message")]
    [SerializeField] private TextMeshProUGUI _messageText;

    private bool _waitingForInput = true;
    private Coroutine _blinkCoroutine;

    void Start()
    {
        _signInPanel.SetActive(false);
        _signUpPanel.SetActive(false);

        if (_messageText != null)
            _messageText.gameObject.SetActive(false);

        SetupButtonListeners();
        SetupTabNavigation();

        _blinkCoroutine = StartCoroutine(BlinkPressStart());
    }

    void Update()
    {
        // "Press Start" 대기 상태에서 아무 키 입력 감지
        if (_waitingForInput)
        {
            bool keyPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool mousePressed = Mouse.current != null &&
                (Mouse.current.leftButton.wasPressedThisFrame ||
                 Mouse.current.rightButton.wasPressedThisFrame ||
                 Mouse.current.middleButton.wasPressedThisFrame);

            if (keyPressed || mousePressed)
            {
                OnAnyKeyPressed();
            }
        }
        else
        {
            HandleTabNavigation();
        }
    }

    private void OnAnyKeyPressed()
    {
        _waitingForInput = false;

        if (_blinkCoroutine != null)
            StopCoroutine(_blinkCoroutine);

        _pressStartText.gameObject.SetActive(false);
        ShowSignInPanel();
    }

    private IEnumerator BlinkPressStart()
    {
        while (_waitingForInput)
        {
            _pressStartText.enabled = !_pressStartText.enabled;
            yield return new WaitForSeconds(_blinkInterval);
        }
    }

    private void SetupButtonListeners()
    {
        _signInButton.onClick.AddListener(OnSignInClicked);
        _goToSignUpButton.onClick.AddListener(ShowSignUpPanel);
        _signUpButton.onClick.AddListener(OnSignUpClicked);
        _cancelButton.onClick.AddListener(ShowSignInPanel);

        // IME 조합 완료 후 Enter 처리를 위해 onSubmit 사용
        _signInEmailInput.onSubmit.AddListener(_ => OnSignInClicked());
        _signInPasswordInput.onSubmit.AddListener(_ => OnSignInClicked());

        _signUpEmailInput.onSubmit.AddListener(_ => OnSignUpClicked());
        _signUpPasswordInput.onSubmit.AddListener(_ => OnSignUpClicked());
        _signUpPasswordConfirmInput.onSubmit.AddListener(_ => OnSignUpClicked());
        _signUpNicknameInput.onSubmit.AddListener(_ => OnSignUpClicked());
    }

    // Tab 키로 다음/이전 입력 필드 이동 설정.
    private void SetupTabNavigation()
    {
        // Sign In Panel Navigation
        SetupSelectableNavigation(_signInEmailInput, null, _signInPasswordInput);
        SetupSelectableNavigation(_signInPasswordInput, _signInEmailInput, _signInButton);
        SetupSelectableNavigation(_signInButton, _signInPasswordInput, _goToSignUpButton);
        SetupSelectableNavigation(_goToSignUpButton, _signInButton, null);

        // Sign Up Panel Navigation
        SetupSelectableNavigation(_signUpEmailInput, null, _signUpPasswordInput);
        SetupSelectableNavigation(_signUpPasswordInput, _signUpEmailInput, _signUpPasswordConfirmInput);
        SetupSelectableNavigation(_signUpPasswordConfirmInput, _signUpPasswordInput, _signUpNicknameInput);
        SetupSelectableNavigation(_signUpNicknameInput, _signUpPasswordConfirmInput, _signUpButton);
        SetupSelectableNavigation(_signUpButton, _signUpNicknameInput, _cancelButton);
        SetupSelectableNavigation(_cancelButton, _signUpButton, null);
    }

    private void SetupSelectableNavigation(Selectable selectable, Selectable up, Selectable down)
    {
        var nav = selectable.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = up;
        nav.selectOnDown = down;
        selectable.navigation = nav;
    }

    // Tab/Shift+Tab 키로 포커스 이동 처리.
    private void HandleTabNavigation()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            var current = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (current != null)
            {
                var selectable = current.GetComponent<Selectable>();
                if (selectable != null)
                {
                    var next = Keyboard.current.shiftKey.isPressed
                        ? selectable.FindSelectableOnUp()
                        : selectable.FindSelectableOnDown();

                    if (next != null)
                        next.Select();
                }
            }
        }
    }


    private void ShowSignInPanel()
    {
        _signInPanel.SetActive(true);
        _signUpPanel.SetActive(false);
        ClearSignInInputs();
        _signInEmailInput.Select();
    }

    private void ShowSignUpPanel()
    {
        _signInPanel.SetActive(false);
        _signUpPanel.SetActive(true);
        ClearSignUpInputs();
        _signUpEmailInput.Select();
    }

    private void ClearSignInInputs()
    {
        _signInEmailInput.text = "";
        _signInPasswordInput.text = "";
    }

    private void ClearSignUpInputs()
    {
        _signUpEmailInput.text = "";
        _signUpPasswordInput.text = "";
        _signUpPasswordConfirmInput.text = "";
        _signUpNicknameInput.text = "";
    }

    // 로그인 버튼 클릭. Firebase 인증 → Firestore 유저 조회 → Photon 로비 연결 → Lobby 씬 이동.
    private async void OnSignInClicked()
    {
        var email = _signInEmailInput.text.Trim();
        var password = _signInPasswordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowMessage("이메일과 비밀번호를 입력해주세요.");
            return;
        }

        SetButtonsInteractable(false);
        ShowMessage("초기화 대기 중...");

        try
        {
            await AuthManager.Instance.WaitForInitialization();

            if (!AuthManager.Instance.IsInitialized)
            {
                ShowMessage("Firebase 초기화 실패. 다시 시도해주세요.");
                SetButtonsInteractable(true);
                return;
            }

            ShowMessage("로그인 중...");
            var user = await AuthManager.Instance.SignInAsync(email, password);

            if (user != null)
            {
                // Firestore에서 닉네임 등 추가 정보 조회
                var userData = await FirestoreManager.Instance.GetUserDocument(user.UserId);

                if (userData != null)
                {
                    AuthManager.Instance.SetUserData(userData.Uid, userData.Email, userData.Nickname);
                    ShowMessage("로그인 성공! 로비 연결 중...");

                    // Photon 로비 연결
                    var lobbyJoined = await NetworkManager.Instance.JoinLobby(userData.Nickname);
                    if (lobbyJoined)
                    {
                        SceneManager.LoadScene("Lobby");
                    }
                    else
                    {
                        ShowMessage("로비 연결 실패. 다시 시도해주세요.");
                        SetButtonsInteractable(true);
                    }
                }
                else
                {
                    ShowMessage("유저 데이터를 찾을 수 없습니다.");
                    SetButtonsInteractable(true);
                }
            }
        }
        catch (Exception e)
        {
            ShowMessage($"로그인 실패: {e.Message}");
            SetButtonsInteractable(true);
        }
    }

    // 회원가입 버튼 클릭. Firebase 계정 생성 → Firestore 문서 생성 → Photon 로비 연결.
    private async void OnSignUpClicked()
    {
        var email = _signUpEmailInput.text.Trim();
        var password = _signUpPasswordInput.text;
        var passwordConfirm = _signUpPasswordConfirmInput.text;
        var nickname = _signUpNicknameInput.text.Trim();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(passwordConfirm) || string.IsNullOrEmpty(nickname))
        {
            ShowMessage("모든 필드를 입력해주세요.");
            return;
        }

        if (password != passwordConfirm)
        {
            ShowMessage("비밀번호가 일치하지 않습니다.");
            return;
        }

        if (password.Length < 6)
        {
            ShowMessage("비밀번호는 6자 이상이어야 합니다.");
            return;
        }

        SetButtonsInteractable(false);
        ShowMessage("초기화 대기 중...");

        try
        {
            await AuthManager.Instance.WaitForInitialization();

            if (!AuthManager.Instance.IsInitialized)
            {
                ShowMessage("Firebase 초기화 실패. 다시 시도해주세요.");
                SetButtonsInteractable(true);
                return;
            }

            ShowMessage("회원가입 중...");
            var user = await AuthManager.Instance.SignUpAsync(email, password);

            if (user != null)
            {
                // Firestore에 유저 문서 생성
                await FirestoreManager.Instance.CreateUserDocument(user.UserId, email, password, nickname);
                AuthManager.Instance.SetUserData(user.UserId, email, nickname);

                ShowMessage("회원가입 성공! 로비 연결 중...");

                var lobbyJoined = await NetworkManager.Instance.JoinLobby(nickname);
                if (lobbyJoined)
                {
                    SceneManager.LoadScene("Lobby");
                }
                else
                {
                    ShowMessage("로비 연결 실패. 다시 시도해주세요.");
                    SetButtonsInteractable(true);
                }
            }
        }
        catch (Exception e)
        {
            ShowMessage($"회원가입 실패: {e.Message}");
            SetButtonsInteractable(true);
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        _signInButton.interactable = interactable;
        _goToSignUpButton.interactable = interactable;
        _signUpButton.interactable = interactable;
        _cancelButton.interactable = interactable;
    }

    private void ShowMessage(string message)
    {
        if (_messageText != null)
        {
            _messageText.gameObject.SetActive(true);
            _messageText.text = message;
        }
        Debug.Log(message);
    }
}
