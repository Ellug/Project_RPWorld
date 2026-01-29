using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// Firebase 인증 관리자. 이메일/비밀번호 로그인 및 회원가입 처리.
/// </summary>
public class AuthManager : Singleton<AuthManager>
{
    public string CurrentUserUid { get; private set; }
    public string CurrentUserEmail { get; private set; }
    public string CurrentUserNickname { get; private set; }

    private FirebaseAuth _auth;
    private bool _isInitialized;
    private TaskCompletionSource<bool> _initializationTask;

    public bool IsInitialized => _isInitialized;

    protected override void OnSingletonAwake()
    {
        _initializationTask = new TaskCompletionSource<bool>();
        InitializeFirebase();
    }

    /// <summary>
    /// Firebase 초기화 완료까지 대기. 로그인 전에 반드시 await.
    /// </summary>
    public Task WaitForInitialization() => _initializationTask.Task;

    private async void InitializeFirebase()
    {
        try
        {
            // Firebase 의존성 확인 및 자동 수정
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus == DependencyStatus.Available)
            {
                _auth = FirebaseAuth.DefaultInstance;
                _isInitialized = true;
                _initializationTask.TrySetResult(true);
                Debug.Log("Firebase Auth initialized successfully");
            }
            else
            {
                Debug.LogError($"Could not resolve Firebase dependencies: {dependencyStatus}");
                _initializationTask.TrySetResult(false);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Firebase initialization failed: {e.Message}");
            _initializationTask.TrySetResult(false);
        }
    }

    /// <summary>
    /// 이메일/비밀번호로 로그인. 성공 시 FirebaseUser 반환.
    /// </summary>
    public async Task<FirebaseUser> SignInAsync(string email, string password)
    {
        if (!_isInitialized)
        {
            Debug.LogError("Firebase Auth is not initialized");
            return null;
        }

        try
        {
            var authResult = await _auth.SignInWithEmailAndPasswordAsync(email, password);
            Debug.Log($"Sign in successful: {authResult.User.Email}");
            return authResult.User;
        }
        catch (FirebaseException e)
        {
            Debug.LogError($"Sign in failed: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 이메일/비밀번호로 회원가입. 성공 시 FirebaseUser 반환.
    /// </summary>
    public async Task<FirebaseUser> SignUpAsync(string email, string password)
    {
        if (!_isInitialized)
        {
            Debug.LogError("Firebase Auth is not initialized");
            return null;
        }

        try
        {
            var authResult = await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
            Debug.Log($"Sign up successful: {authResult.User.Email}");
            return authResult.User;
        }
        catch (FirebaseException e)
        {
            Debug.LogError($"Sign up failed: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 로그인 성공 후 유저 정보 저장. Firestore에서 가져온 닉네임 포함.
    /// </summary>
    public void SetUserData(string uid, string email, string nickname)
    {
        CurrentUserUid = uid;
        CurrentUserEmail = email;
        CurrentUserNickname = nickname;
    }

    public void ClearUserData()
    {
        CurrentUserUid = null;
        CurrentUserEmail = null;
        CurrentUserNickname = null;
    }

    /// <summary>
    /// 로그아웃 처리. Firebase 세션 종료 및 로컬 데이터 초기화.
    /// </summary>
    public void SignOut()
    {
        if (_auth != null)
            _auth.SignOut();

        ClearUserData();
    }
}
