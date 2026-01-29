using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEngine;

/// <summary>
/// 타일 기반 월드 맵 상태 관리. Fusion NetworkBehaviour로 네트워크 동기화.
/// 타일 배치/삭제, 맵 저장/로드, 맵 전환 기능 제공.
/// </summary>
public class WorldMapState : NetworkBehaviour
{
    private const string SessionMapKey = "map";

    [Header("Map")]
    [SerializeField] private string _defaultMapName = "default";
    [SerializeField] private float _tileSize = 1f;
    [SerializeField] private float _tileHeight = 0f;

    [Header("Prefabs")]
    [SerializeField] private NetworkObject _tilePrefab;
    [SerializeField] private TilePalette _tilePalette;

    [Header("Saving")]
    [SerializeField] private float _autoSaveInterval = 60f;
    [SerializeField] private bool _saveOnApplicationQuit = true;
    [SerializeField] private bool _logDebug = false;

    // [Networked]: Fusion이 네트워크 전체에 동기화하는 프로퍼티
    [Networked]
    public NetworkString<_64> MapName { get; private set; }

    private readonly Dictionary<Vector2Int, WorldTile> _tileLookup = new();
    private Coroutine _autoSaveRoutine;
    private bool _isLoading;

    public float TileSize => _tileSize;
    public float TileHeight => _tileHeight;
    public TilePalette TilePalette => _tilePalette;

    public override void Spawned()
    {
        // HasStateAuthority: Shared Mode에서 이 오브젝트의 상태를 수정할 권한이 있는지
        if (!HasStateAuthority)
            return;

        var mapName = ResolveInitialMapName();
        SetMapName(mapName);
        StartAutoSave();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        StopAutoSave();
    }

    private void OnApplicationQuit()
    {
        if (_saveOnApplicationQuit && HasStateAuthority)
            SaveCurrentMap();
    }

    #region Public API (클라이언트에서 호출 가능)

    /// <summary>
    /// 타일 배치 요청. StateAuthority 없으면 RPC로 호스트에 요청.
    /// </summary>
    public void RequestPlaceTile(Vector3 worldPosition, int tileId)
    {
        if (HasStateAuthority)
            PlaceTileInternal(worldPosition, tileId);
        else
            RPC_RequestPlaceTile(worldPosition, tileId);
    }

    /// <summary>
    /// 맵 변경 요청. 기존 타일 제거 후 새 맵 로드.
    /// </summary>
    public void RequestChangeMap(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
            return;

        if (HasStateAuthority)
            SetMapName(mapName);
        else
            RPC_RequestChangeMap(mapName);
    }

    /// <summary>
    /// 타일 제거 요청.
    /// </summary>
    public void RequestRemoveTile(Vector3 worldPosition)
    {
        if (HasStateAuthority)
            RemoveTileInternal(worldPosition);
        else
            RPC_RequestRemoveTile(worldPosition);
    }

    public bool HasTileAt(Vector3 worldPosition)
    {
        var snapped = SnapPosition(worldPosition);
        var key = PositionToKey(snapped);
        return _tileLookup.TryGetValue(key, out var tile) && tile != null;
    }

    #endregion

    #region Fusion RPCs

    // RpcSources.All: 모든 클라이언트에서 호출 가능
    // RpcTargets.StateAuthority: StateAuthority를 가진 클라이언트에서만 실행
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPlaceTile(Vector3 worldPosition, int tileId, RpcInfo info = default)
    {
        PlaceTileInternal(worldPosition, tileId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRemoveTile(Vector3 worldPosition, RpcInfo info = default)
    {
        RemoveTileInternal(worldPosition);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestChangeMap(string mapName, RpcInfo info = default)
    {
        SetMapName(mapName);
    }

    #endregion

    #region Internal Tile Operations

    private void RemoveTileInternal(Vector3 worldPosition)
    {
        if (Runner == null)
            return;

        var snapped = SnapPosition(worldPosition);
        var key = PositionToKey(snapped);

        if (!_tileLookup.TryGetValue(key, out var tile) || tile == null)
            return;

        _tileLookup.Remove(key);

        // Fusion 네트워크 오브젝트면 Despawn, 아니면 Destroy
        if (tile.Object != null && tile.HasStateAuthority)
            Runner.Despawn(tile.Object);
        else
            Destroy(tile.gameObject);
    }

    private void PlaceTileInternal(Vector3 worldPosition, int tileId)
    {
        if (_tilePrefab == null || Runner == null)
            return;

        var snapped = SnapPosition(worldPosition);
        var key = PositionToKey(snapped);

        // 이미 타일이 있으면 ID만 변경
        if (_tileLookup.TryGetValue(key, out var existing) && existing != null)
        {
            existing.TileId = tileId;
            return;
        }

        // 새 타일 스폰 (Fusion Runner.Spawn으로 네트워크 동기화)
        Runner.Spawn(_tilePrefab, snapped, Quaternion.identity, null, (_, obj) =>
        {
            var tile = obj.GetComponent<WorldTile>();
            if (tile != null)
            {
                tile.SetPalette(_tilePalette);
                tile.TileId = tileId;
                _tileLookup[key] = tile;
            }
        });
    }

    #endregion

    #region Map Management

    private void SetMapName(string mapName)
    {
        var sanitized = SanitizeMapName(mapName);
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = string.IsNullOrWhiteSpace(_defaultMapName) ? "default" : _defaultMapName;

        MapName = sanitized;
        HandleMapNameChanged();
    }

    /// <summary>
    /// 초기 맵 이름 결정. 우선순위: GameManager pending → 세션 프로퍼티 → 기본값
    /// </summary>
    private string ResolveInitialMapName()
    {
        // GameManager에서 대기 중인 맵 이름 (씬 전환으로 전달)
        var pending = GameManager.Instance != null ? GameManager.Instance.ConsumePendingMapName() : null;
        if (!string.IsNullOrWhiteSpace(pending))
            return pending;

        // Fusion 세션 프로퍼티에서 맵 이름
        if (Runner != null && Runner.SessionInfo != null && Runner.SessionInfo.Properties != null &&
            Runner.SessionInfo.Properties.TryGetValue(SessionMapKey, out var mapProperty))
        {
            var fromSession = (string)mapProperty;
            if (!string.IsNullOrWhiteSpace(fromSession))
                return fromSession;
        }

        if (!string.IsNullOrWhiteSpace(_defaultMapName))
            return _defaultMapName;

        var managerDefault = GameManager.Instance != null ? GameManager.Instance.DefaultMapName : null;
        if (!string.IsNullOrWhiteSpace(managerDefault))
            return managerDefault;

        return "default";
    }

    private void HandleMapNameChanged()
    {
        var mapName = MapName.ToString();

        if (!HasStateAuthority)
            return;

        LoadMapInternal(mapName);
    }

    private void LoadMapInternal(string mapName)
    {
        if (!HasStateAuthority)
            return;

        _isLoading = true;
        ClearTiles();

        var data = LoadMapFromDisk(mapName);
        if (data != null && data.Tiles != null)
        {
            foreach (var tile in data.Tiles)
                PlaceTileInternal(tile.Position, tile.TileId);
        }

        _isLoading = false;
    }

    private void ClearTiles()
    {
        foreach (var tile in _tileLookup.Values)
        {
            if (tile == null)
                continue;

            if (tile.Object != null && tile.HasStateAuthority)
                Runner.Despawn(tile.Object);
            else
                Destroy(tile.gameObject);
        }

        _tileLookup.Clear();

        // 혹시 누락된 타일이 있을 경우 정리
        var remainingTiles = FindObjectsByType<WorldTile>(FindObjectsSortMode.None);
        foreach (var tile in remainingTiles)
        {
            if (tile == null)
                continue;

            if (tile.Object != null && tile.HasStateAuthority)
                Runner.Despawn(tile.Object);
            else
                Destroy(tile.gameObject);
        }
    }

    #endregion

    #region Save/Load

    private void SaveCurrentMap()
    {
        var mapName = MapName.ToString();
        if (string.IsNullOrWhiteSpace(mapName))
            mapName = _defaultMapName;

        SaveMapToDisk(mapName);
    }

    /// <summary>
    /// 맵을 JSON 파일로 저장. 경로: Application.persistentDataPath/Maps/{mapName}.json
    /// </summary>
    private void SaveMapToDisk(string mapName)
    {
        var data = new WorldMapSaveData
        {
            MapName = mapName,
            Tiles = new List<WorldMapTileData>()
        };

        var tiles = FindObjectsByType<WorldTile>(FindObjectsSortMode.None);
        foreach (var tile in tiles)
        {
            if (tile == null)
                continue;

            data.Tiles.Add(new WorldMapTileData
            {
                TileId = tile.TileId,
                Position = tile.transform.position
            });
        }

        var json = JsonUtility.ToJson(data, true);
        var path = GetMapPath(mapName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);

        if (_logDebug)
            Debug.Log($"[WorldMapState] Saved map '{mapName}' ({data.Tiles.Count} tiles) to {path}");
    }

    private WorldMapSaveData LoadMapFromDisk(string mapName)
    {
        var path = GetMapPath(mapName);
        if (!File.Exists(path))
            return new WorldMapSaveData { MapName = mapName, Tiles = new List<WorldMapTileData>() };

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<WorldMapSaveData>(json);
            if (data == null)
                return new WorldMapSaveData { MapName = mapName, Tiles = new List<WorldMapTileData>() };

            if (data.Tiles == null)
                data.Tiles = new List<WorldMapTileData>();

            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WorldMapState] Failed to load map '{mapName}': {e.Message}");
            return new WorldMapSaveData { MapName = mapName, Tiles = new List<WorldMapTileData>() };
        }
    }

    private void StartAutoSave()
    {
        if (_autoSaveInterval <= 0f)
            return;

        if (_autoSaveRoutine != null)
            StopCoroutine(_autoSaveRoutine);

        _autoSaveRoutine = StartCoroutine(AutoSaveRoutine());
    }

    private void StopAutoSave()
    {
        if (_autoSaveRoutine == null)
            return;

        StopCoroutine(_autoSaveRoutine);
        _autoSaveRoutine = null;
    }

    private IEnumerator AutoSaveRoutine()
    {
        var wait = new WaitForSeconds(_autoSaveInterval);
        while (true)
        {
            yield return wait;

            if (!_isLoading)
                SaveCurrentMap();
        }
    }

    #endregion

    #region Utility

    /// <summary>
    /// 월드 좌표를 타일 그리드에 스냅.
    /// </summary>
    private Vector3 SnapPosition(Vector3 worldPosition)
    {
        if (_tileSize <= 0f)
            return new Vector3(worldPosition.x, _tileHeight, worldPosition.z);

        var x = Mathf.Round(worldPosition.x / _tileSize) * _tileSize;
        var z = Mathf.Round(worldPosition.z / _tileSize) * _tileSize;
        return new Vector3(x, _tileHeight, z);
    }

    private Vector2Int PositionToKey(Vector3 worldPosition)
    {
        if (_tileSize <= 0f)
            return new Vector2Int(Mathf.RoundToInt(worldPosition.x), Mathf.RoundToInt(worldPosition.z));

        var x = Mathf.RoundToInt(worldPosition.x / _tileSize);
        var z = Mathf.RoundToInt(worldPosition.z / _tileSize);
        return new Vector2Int(x, z);
    }

    private string SanitizeMapName(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
            return null;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = mapName.Trim();
        foreach (var c in invalidChars)
            sanitized = sanitized.Replace(c, '_');

        return sanitized;
    }

    private string GetMapPath(string mapName)
    {
        var sanitized = SanitizeMapName(mapName);
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "default";

        var fileName = $"{sanitized}.json";
        var directory = Path.Combine(Application.persistentDataPath, "Maps");
        return Path.Combine(directory, fileName);
    }

    #endregion

    #region Data Classes

    [Serializable]
    private class WorldMapSaveData
    {
        public string MapName;
        public List<WorldMapTileData> Tiles;
    }

    [Serializable]
    private struct WorldMapTileData
    {
        public int TileId;
        public Vector3 Position;
    }

    #endregion
}
