using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 타일 기반 월드 맵 상태 관리. Fusion NetworkBehaviour로 네트워크 동기화.
/// 타일 배치/삭제, 맵 저장/로드, 맵 전환 기능 제공.
/// </summary>
public class WorldMapState : NetworkBehaviour
{
    private const string SessionMapKey = "map";

    public enum MapOriginMode
    {
        Center,
        BottomLeft
    }

    [Header("Map")]
    [SerializeField] private string _defaultMapName = "default";
    [SerializeField] private float _tileSize = 1f;
    [SerializeField] private float _tileHeight = 0f;

    [Header("Bounds")]
    [SerializeField] private int _mapWidthTiles = 512;
    [SerializeField] private int _mapHeightTiles = 512;
    [SerializeField] private MapOriginMode _originMode = MapOriginMode.Center;

    [Header("Prefabs")]
    // TODO: Replace per-tile NetworkObject spawning with chunked mesh/instancing for large maps.
    [SerializeField] private NetworkObject _tilePrefab;
    [SerializeField] private string _tilePrefabGuid;
    [SerializeField] private TilePalette _tilePalette;

    [Header("Map Switch")]
    [SerializeField] private bool _allowNonHostMapSwitch = false;
    [SerializeField] private bool _autoSaveBeforeMapSwitch = true;

    [Header("Saving")]
    [SerializeField] private float _autoSaveInterval = 60f;
    [SerializeField] private bool _saveOnApplicationQuit = true;
    [SerializeField] private bool _logDebug = false;

    // [Networked]: Fusion이 네트워크 전체에 동기화하는 프로퍼티
    [Networked]
    public NetworkString<_64> MapName { get; private set; }

    private readonly Dictionary<Vector2Int, WorldTile> _tileLookup = new();
    private readonly HashSet<Vector2Int> _pendingSpawns = new();
    private Coroutine _autoSaveRoutine;
    private bool _isLoading;
    private string _lastMapName;
    private int _debugLogFrame;
    private bool _prefabRegistrationAttempted;

    public float TileSize => _tileSize;
    public float TileHeight => _tileHeight;
    public TilePalette TilePalette => _tilePalette;
    public int MapWidthTiles => _mapWidthTiles;
    public int MapHeightTiles => _mapHeightTiles;

    public event Action<string> MapLoadStarted;
    public event Action<string, int> MapLoadCompleted;
    public event Action<Vector2Int> PlacementOutOfBounds;

    public override void Spawned()
    {
        EnsureTilePrefabRegistered();
        LogTilePrefabDiagnostics();

        // HasStateAuthority: Shared Mode에서 이 오브젝트의 상태를 수정할 권한이 있는지
        if (!HasStateAuthority)
            return;

        var mapName = ResolveInitialMapName();
        SetMapName(mapName);
        StartAutoSave();

        LogDebug($"Spawned (StateAuthority) map={mapName} runner={Runner?.name}");
    }

    public override void FixedUpdateNetwork()
    {
        var current = MapName.ToString();
        if (string.Equals(_lastMapName, current, StringComparison.Ordinal))
            return;

        var previous = _lastMapName;
        _lastMapName = current;
        HandleMapNameChanged(previous);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        StopAutoSave();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_tilePrefab == null)
            return;

        var path = AssetDatabase.GetAssetPath(_tilePrefab);
        if (string.IsNullOrWhiteSpace(path))
            return;

        var guid = AssetDatabase.AssetPathToGUID(path);
        if (!string.IsNullOrWhiteSpace(guid))
            _tilePrefabGuid = guid;
    }
#endif

    private void OnApplicationQuit()
    {
        if (_saveOnApplicationQuit && HasStateAuthority && !_isLoading)
            SaveCurrentMap();
    }

    #region Public API (클라이언트에서 호출 가능)

    /// <summary>
    /// 타일 배치 요청. StateAuthority 없으면 RPC로 호스트에 요청.
    /// </summary>
    public void RequestPlaceTile(Vector3 worldPosition, int tileId)
    {
        if (_isLoading)
            return;

        var key = PositionToKey(worldPosition);
        if (!IsInsideBounds(key))
        {
            PlacementOutOfBounds?.Invoke(key);
            return;
        }

        if (HasStateAuthority)
            PlaceTileInternal(key, tileId);
        else
            RPC_RequestPlaceTileKey(key.x, key.y, tileId);
    }

    /// <summary>
    /// 맵 변경 요청. 기존 타일 제거 후 새 맵 로드.
    /// </summary>
    public void RequestChangeMap(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
            return;

        if (_isLoading)
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
        if (_isLoading)
            return;

        var key = PositionToKey(worldPosition);
        if (!IsInsideBounds(key))
        {
            PlacementOutOfBounds?.Invoke(key);
            return;
        }

        if (HasStateAuthority)
            RemoveTileInternal(key);
        else
            RPC_RequestRemoveTileKey(key.x, key.y);
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
    private void RPC_RequestPlaceTileKey(int x, int y, int tileId, RpcInfo info = default)
    {
        if (_isLoading)
            return;

        var key = new Vector2Int(x, y);
        if (!IsInsideBounds(key))
        {
            PlacementOutOfBounds?.Invoke(key);
            LogDebug($"RPC place rejected (out of bounds) key={key} from={info.Source}");
            return;
        }

        LogDebug($"RPC place received key={key} tileId={tileId} from={info.Source}");
        PlaceTileInternal(key, tileId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRemoveTileKey(int x, int y, RpcInfo info = default)
    {
        if (_isLoading)
            return;

        var key = new Vector2Int(x, y);
        if (!IsInsideBounds(key))
        {
            PlacementOutOfBounds?.Invoke(key);
            LogDebug($"RPC remove rejected (out of bounds) key={key} from={info.Source}");
            return;
        }

        LogDebug($"RPC remove received key={key} from={info.Source}");
        RemoveTileInternal(key);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestChangeMap(string mapName, RpcInfo info = default)
    {
        if (_isLoading)
            return;

        if (!_allowNonHostMapSwitch)
        {
            // HasStateAuthority는 호스트이므로, 요청자 정보가 있다면 차단
            if (Runner != null && info.Source != Runner.LocalPlayer)
                return;
        }

        SetMapName(mapName);
    }

    #endregion

    #region Internal Tile Operations

    private void RemoveTileInternal(Vector2Int key)
    {
        if (Runner == null)
            return;

        if (!IsInsideBounds(key))
            return;

        if (!_tileLookup.TryGetValue(key, out var tile) || tile == null)
        {
            LogDebug($"Remove ignored (not found) key={key}");
            return;
        }

        _tileLookup.Remove(key);
        _pendingSpawns.Remove(key);

        // Fusion 네트워크 오브젝트면 Despawn, 아니면 Destroy
        if (tile.Object != null && tile.HasStateAuthority)
            Runner.Despawn(tile.Object);
        else
            Destroy(tile.gameObject);

        LogDebug($"Removed tile key={key}");
    }

    private void PlaceTileInternal(Vector2Int key, int tileId)
    {
        EnsureTilePrefabRegistered();

        if (_tilePrefab == null || Runner == null)
            return;

        if (!IsInsideBounds(key))
            return;

        // 이미 타일이 있으면 ID만 변경
        if (_tileLookup.TryGetValue(key, out var existing) && existing != null)
        {
            existing.TileId = tileId;
            LogDebug($"Tile exists, updated TileId key={key} tileId={tileId}");
            return;
        }

        if (TryFindExistingTile(key, out var fallback))
        {
            _tileLookup[key] = fallback;
            _pendingSpawns.Remove(key);
            fallback.TileId = tileId;
            LogDebug($"Tile exists (fallback), updated TileId key={key} tileId={tileId}");
            return;
        }

        if (_pendingSpawns.Contains(key))
        {
            LogDebug($"Spawn pending, ignored key={key}");
            return;
        }

        // 새 타일 스폰 (Fusion Runner.Spawn으로 네트워크 동기화)
        _pendingSpawns.Add(key);
        var snapped = KeyToPosition(key);
        var spawned = Runner.Spawn(_tilePrefab, snapped, Quaternion.identity, null, (_, obj) =>
        {
            var tile = obj.GetComponent<WorldTile>();
            if (tile != null)
            {
                tile.SetPalette(_tilePalette);
                tile.KeyX = key.x;
                tile.KeyY = key.y;
                tile.TileId = tileId;
            }
        });

        if (spawned == null)
        {
            _pendingSpawns.Remove(key);
            LogDebug($"Spawn failed (null) key={key} tileId={tileId}");
        }
        else
        {
            LogDebug($"Spawn requested key={key} tileId={tileId}");
        }
    }

    #endregion

    #region Prefab Diagnostics

    private void EnsureTilePrefabRegistered()
    {
        if (_prefabRegistrationAttempted)
            return;

        if (Runner == null || _tilePrefab == null)
            return;

        _prefabRegistrationAttempted = true;

        var typeId = _tilePrefab.NetworkTypeId;
        if (typeId.Kind == NetworkTypeIdKind.Prefab)
            return;

        if (string.IsNullOrWhiteSpace(_tilePrefabGuid))
        {
            LogDebug("Tile prefab GUID is missing. Cannot register in prefab table.");
            return;
        }

        if (!NetworkObjectGuid.TryParse(_tilePrefabGuid, out var guid) || !guid.IsValid)
        {
            LogDebug($"Tile prefab GUID is invalid: '{_tilePrefabGuid}'");
            return;
        }

        var prefabId = Runner.Prefabs.GetId(guid);
        if (!prefabId.IsValid)
        {
            var source = new NetworkPrefabSourceStatic
            {
                AssetGuid = guid,
                Object = _tilePrefab
            };

            if (Runner.Prefabs.TryAddSource(source, out prefabId))
            {
                LogDebug($"Tile prefab registered at runtime. prefabId={prefabId.RawValue} guid={_tilePrefabGuid}");
            }
            else
            {
                LogDebug($"Tile prefab registration failed. guid={_tilePrefabGuid}");
            }
        }
    }

    private void LogTilePrefabDiagnostics()
    {
        if (!_logDebug || Runner == null || _tilePrefab == null)
            return;

        var typeId = _tilePrefab.NetworkTypeId;
        var kindName = typeId.Kind.ToString();
        var prefabIdText = "n/a";
        if (typeId.Kind == NetworkTypeIdKind.Prefab)
        {
            var prefabId = typeId.AsPrefabId;
            prefabIdText = $"{prefabId.RawValue}";
        }

        var tableIdText = "n/a";
        if (!string.IsNullOrWhiteSpace(_tilePrefabGuid) && NetworkObjectGuid.TryParse(_tilePrefabGuid, out var guid) && guid.IsValid)
        {
            var tableId = Runner.Prefabs.GetId(guid);
            if (tableId.IsValid)
                tableIdText = $"{tableId.RawValue}";
        }

        LogDebug($"TilePrefab diag kind={kindName} prefabId={prefabIdText} tableId={tableIdText} AOI={Runner.Config.Simulation.AreaOfInterestEnabled}");
    }

    #endregion

    #region Map Management

    private void SetMapName(string mapName)
    {
        var sanitized = SanitizeMapName(mapName);
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = string.IsNullOrWhiteSpace(_defaultMapName) ? "default" : _defaultMapName;

        MapName = sanitized;
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

    private void HandleMapNameChanged(string oldMapName)
    {
        var mapName = MapName.ToString();

        if (!HasStateAuthority)
            return;

        if (_autoSaveBeforeMapSwitch && !_isLoading && !string.IsNullOrWhiteSpace(oldMapName) && oldMapName != mapName)
            SaveMapToDisk(oldMapName);

        LogDebug($"MapName changed {oldMapName} -> {mapName}");
        LoadMapInternal(mapName);
    }

    private void LoadMapInternal(string mapName)
    {
        if (!HasStateAuthority)
            return;

        if (_isLoading)
            return;

        _isLoading = true;
        MapLoadStarted?.Invoke(mapName);
        if (_logDebug)
            Debug.Log($"[WorldMapState] Map load started: {mapName}");

        var loadedCount = 0;

        try
        {
            ClearTiles();

            var data = LoadMapFromDisk(mapName);
            if (data != null && data.Tiles != null)
            {
                foreach (var tile in data.Tiles)
                {
                    var key = new Vector2Int(tile.X, tile.Y);
                    if (!IsInsideBounds(key))
                        continue;

                    PlaceTileInternal(key, tile.TileId);
                    loadedCount++;
                }
            }
        }
        finally
        {
            _isLoading = false;
            MapLoadCompleted?.Invoke(mapName, loadedCount);
            if (_logDebug)
                Debug.Log($"[WorldMapState] Map load completed: {mapName} ({loadedCount} tiles)");
        }
    }

    private void ClearTiles()
    {
        var tilesToClear = new List<WorldTile>(_tileLookup.Values);
        _tileLookup.Clear();
        _pendingSpawns.Clear();

        foreach (var tile in tilesToClear)
        {
            if (tile == null)
                continue;

            if (tile.Object != null && tile.HasStateAuthority)
                Runner.Despawn(tile.Object);
            else
                Destroy(tile.gameObject);
        }

        // 혹시 누락된 타일이 있을 경우 정리
        if (Runner != null)
        {
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
    }

    #endregion

    #region Save/Load

    private void SaveCurrentMap()
    {
        if (_isLoading)
            return;

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
            Version = 1,
            Tiles = new List<WorldMapTileData>()
        };

        foreach (var pair in _tileLookup)
        {
            var tile = pair.Value;
            if (tile == null)
                continue;

            data.Tiles.Add(new WorldMapTileData
            {
                TileId = tile.TileId,
                X = pair.Key.x,
                Y = pair.Key.y
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
            return new WorldMapSaveData { MapName = mapName, Version = 1, Tiles = new List<WorldMapTileData>() };

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<WorldMapSaveData>(json);
            if (data == null)
                return new WorldMapSaveData { MapName = mapName, Version = 1, Tiles = new List<WorldMapTileData>() };

            if (data.Tiles == null)
                data.Tiles = new List<WorldMapTileData>();

            if (data.Version <= 0)
            {
                var legacy = JsonUtility.FromJson<LegacyWorldMapSaveData>(json);
                if (legacy != null && legacy.Tiles != null && legacy.Tiles.Count > 0)
                {
                    var converted = new WorldMapSaveData
                    {
                        MapName = string.IsNullOrWhiteSpace(legacy.MapName) ? mapName : legacy.MapName,
                        Version = 1,
                        Tiles = new List<WorldMapTileData>(legacy.Tiles.Count)
                    };

                    foreach (var tile in legacy.Tiles)
                    {
                        var key = PositionToKey(tile.Position);
                        converted.Tiles.Add(new WorldMapTileData
                        {
                            TileId = tile.TileId,
                            X = key.x,
                            Y = key.y
                        });
                    }

                    data = converted;
                }
                else
                {
                    data.Version = 1;
                }
            }

            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WorldMapState] Failed to load map '{mapName}': {e.Message}");
            return new WorldMapSaveData { MapName = mapName, Version = 1, Tiles = new List<WorldMapTileData>() };
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
        var key = PositionToKey(worldPosition);
        return KeyToPosition(key);
    }

    public Vector2Int PositionToKey(Vector3 worldPosition)
    {
        if (_tileSize <= 0f)
            return new Vector2Int(Mathf.RoundToInt(worldPosition.x), Mathf.RoundToInt(worldPosition.z));

        var x = Mathf.RoundToInt(worldPosition.x / _tileSize);
        var z = Mathf.RoundToInt(worldPosition.z / _tileSize);
        return new Vector2Int(x, z);
    }

    public Vector3 KeyToPosition(Vector2Int key)
    {
        if (_tileSize <= 0f)
            return new Vector3(key.x, _tileHeight, key.y);

        var x = key.x * _tileSize;
        var z = key.y * _tileSize;
        return new Vector3(x, _tileHeight, z);
    }

    public bool IsInsideBounds(Vector2Int key)
    {
        if (_mapWidthTiles <= 0 || _mapHeightTiles <= 0)
            return true;

        var minX = _originMode == MapOriginMode.Center ? -_mapWidthTiles / 2 : 0;
        var minY = _originMode == MapOriginMode.Center ? -_mapHeightTiles / 2 : 0;
        var maxX = minX + _mapWidthTiles - 1;
        var maxY = minY + _mapHeightTiles - 1;

        return key.x >= minX && key.x <= maxX && key.y >= minY && key.y <= maxY;
    }

    public void RegisterTile(Vector2Int key, WorldTile tile)
    {
        if (tile == null)
            return;

        _pendingSpawns.Remove(key);

        if (_tilePalette != null)
            tile.SetPalette(_tilePalette);

        if (_tileLookup.TryGetValue(key, out var existing))
        {
            if (existing == tile)
                return;

            if (existing == null)
            {
                _tileLookup[key] = tile;
                return;
            }

            if (_logDebug)
                Debug.LogWarning($"[WorldMapState] Duplicate tile at {key}. Keeping existing.");

            existing.TileId = tile.TileId;
            DespawnOrDestroyTile(tile);
            return;
        }

        _tileLookup[key] = tile;
        LogDebug($"Register tile key={key} obj={tile.name}");
    }

    public void UnregisterTile(Vector2Int key, WorldTile tile)
    {
        if (tile == null)
            return;

        if (_tileLookup.TryGetValue(key, out var existing))
        {
            if (existing == null || existing == tile)
                _tileLookup.Remove(key);
        }

        LogDebug($"Unregister tile key={key} obj={tile.name}");
    }

    private void LogDebug(string message)
    {
        if (!_logDebug)
            return;

        // 간단한 스팸 방지 (같은 프레임 다량 로그 제한)
        if (_debugLogFrame != Time.frameCount)
            _debugLogFrame = Time.frameCount;

        Debug.Log($"[WorldMapState] {message}");
    }

    private bool TryFindExistingTile(Vector2Int key, out WorldTile tile)
    {
        tile = null;

        var tiles = FindObjectsByType<WorldTile>(FindObjectsSortMode.None);
        if (tiles == null || tiles.Length == 0)
            return false;

        var targetPos = KeyToPosition(key);
        foreach (var candidate in tiles)
        {
            if (candidate == null)
                continue;

            if (candidate.KeyX == key.x && candidate.KeyY == key.y)
            {
                tile = candidate;
                return true;
            }

            var pos = candidate.transform.position;
            if (Mathf.Abs(pos.x - targetPos.x) <= 0.01f && Mathf.Abs(pos.z - targetPos.z) <= 0.01f)
            {
                tile = candidate;
                return true;
            }
        }

        return false;
    }

    private void DespawnOrDestroyTile(WorldTile tile)
    {
        if (tile == null)
            return;

        if (tile.Object != null && tile.HasStateAuthority && Runner != null)
            Runner.Despawn(tile.Object);
        else
            Destroy(tile.gameObject);
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
        public int Version;
        public List<WorldMapTileData> Tiles;
    }

    [Serializable]
    private struct WorldMapTileData
    {
        public int TileId;
        public int X;
        public int Y;
    }

    [Serializable]
    private class LegacyWorldMapSaveData
    {
        public string MapName;
        public List<LegacyWorldMapTileData> Tiles;
    }

    [Serializable]
    private struct LegacyWorldMapTileData
    {
        public int TileId;
        public Vector3 Position;
    }

    #endregion
}
