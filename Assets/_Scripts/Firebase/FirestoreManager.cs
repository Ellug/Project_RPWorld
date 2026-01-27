using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

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
            _db = FirebaseFirestore.DefaultInstance;
            _isInitialized = true;
            Debug.Log("Firestore initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Firestore initialization failed: {e.Message}");
        }
    }

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

public class UserData
{
    public string Uid { get; set; }
    public string Email { get; set; }
    public string Nickname { get; set; }
}
