using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

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

    public Task WaitForInitialization() => _initializationTask.Task;

    private async void InitializeFirebase()
    {
        try
        {
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

    public void SignOut()
    {
        if (_auth != null)
            _auth.SignOut();

        ClearUserData();
    }
}
