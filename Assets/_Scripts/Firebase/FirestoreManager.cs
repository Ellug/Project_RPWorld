using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Firestore 데이터베이스 관리자. 유저 문서 생성 및 조회.
/// </summary>
public class FirestoreManager : Singleton<FirestoreManager>
{
    private FirebaseFirestore _db;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;

    protected override void OnSingletonAwake()
    {
        InitializeFirestore();
    }

    private void InitializeFirestore()
    {
        try
        {
            FirebaseFirestore.DefaultInstance.Settings.PersistenceEnabled = false;
            _db = FirebaseFirestore.DefaultInstance;
            _isInitialized = true;
            Debug.Log("Firestore initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Firestore initialization failed: {e.Message}");
        }
    }

    /// <summary>
    /// 신규 유저 문서 생성. 회원가입 시 호출. 경로: users/{uid}
    /// </summary>
    public async Task CreateUserDocument(string uid, string email, string password, string nickname)
    {
        if (!_isInitialized)
        {
            Debug.LogError("Firestore is not initialized");
            return;
        }

        try
        {
            var userData = new Dictionary<string, object>
            {
                { "email", email },
                { "password", password },
                { "nickname", nickname }
            };

            await _db.Collection("users").Document(uid).SetAsync(userData);
            Debug.Log($"User document created for: {uid}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create user document: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 유저 문서 조회. 로그인 후 닉네임 등 추가 정보 가져올 때 사용.
    /// </summary>
    public async Task<UserData> GetUserDocument(string uid)
    {
        if (!_isInitialized)
        {
            Debug.LogError("Firestore is not initialized");
            return null;
        }

        try
        {
            var snapshot = await _db.Collection("users").Document(uid).GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var data = snapshot.ToDictionary();
                return new UserData
                {
                    Uid = uid,
                    Email = data.ContainsKey("email") ? data["email"].ToString() : "",
                    Nickname = data.ContainsKey("nickname") ? data["nickname"].ToString() : ""
                };
            }

            Debug.LogWarning($"User document not found for: {uid}");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to get user document: {e.Message}");
            throw;
        }
    }
}

/// <summary>
/// Firestore에서 조회한 유저 데이터 DTO.
/// </summary>
public class UserData
{
    public string Uid { get; set; }
    public string Email { get; set; }
    public string Nickname { get; set; }
}
