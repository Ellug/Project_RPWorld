using Fusion;
using UnityEngine;

// 네트워크 동기화되는 개별 타일. WorldMapState에 자동 등록/해제
public class WorldTile : NetworkBehaviour
{
    [SerializeField] private TilePalette _palette;
    [SerializeField] private MeshRenderer _renderer;
    [SerializeField] private WorldMapState _worldMapState;

    // 네트워크 동기화 속성
    [Networked] public int TileId { get; set; }
    [Networked] public int KeyX { get; set; }
    [Networked] public int KeyY { get; set; }

    private ChangeDetector _changeDetector;
    private Vector2Int _key;
    private bool _isRegistered;

    public void SetPalette(TilePalette palette)
    {
        _palette = palette;
        ApplyMaterial();
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(NetworkBehaviour.ChangeDetector.Source.SimulationState);

        if (_renderer == null)
            _renderer = GetComponentInChildren<MeshRenderer>();

        TryResolveMapState();

        ApplyMaterial();
    }

    public override void Render()
    {
        TryResolveMapState();

        if (_changeDetector == null)
            return;

        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(TileId))
            {
                ApplyMaterial();
                break;
            }

            if (change == nameof(KeyX) || change == nameof(KeyY))
            {
                UpdateKeyAndPosition();
                break;
            }
        }
    }

    private void ApplyMaterial()
    {
        if (_renderer == null || _palette == null)
            return;

        var material = _palette.GetMaterial(TileId);
        if (material != null)
            _renderer.sharedMaterial = material;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterFromMapState();
    }

    private void OnDisable()
    {
        UnregisterFromMapState();
    }

    private void RegisterWithMapState()
    {
        if (_isRegistered)
            return;

        if (_worldMapState == null)
            _worldMapState = FindFirstObjectByType<WorldMapState>();

        if (_worldMapState == null)
            return;

        _key = new Vector2Int(KeyX, KeyY);
        ApplyPositionFromKey();
        _worldMapState.RegisterTile(_key, this);
        _isRegistered = true;
    }

    private void TryResolveMapState()
    {
        if (_worldMapState == null)
            _worldMapState = FindFirstObjectByType<WorldMapState>();

        if (_worldMapState == null)
            return;

        if (_palette == null)
            _palette = _worldMapState.TilePalette;

        if (!_isRegistered)
            RegisterWithMapState();
    }

    private void UpdateKeyAndPosition()
    {
        var newKey = new Vector2Int(KeyX, KeyY);
        if (newKey == _key && _isRegistered)
            return;

        if (_worldMapState == null)
            _worldMapState = FindFirstObjectByType<WorldMapState>();

        if (_worldMapState == null)
            return;

        if (_isRegistered)
            _worldMapState.UnregisterTile(_key, this);

        _key = newKey;
        ApplyPositionFromKey();
        _worldMapState.RegisterTile(_key, this);
        _isRegistered = true;
    }

    private void ApplyPositionFromKey()
    {
        if (_worldMapState == null)
            return;

        transform.position = _worldMapState.KeyToPosition(_key);
    }

    private void UnregisterFromMapState()
    {
        if (!_isRegistered || _worldMapState == null)
            return;

        _worldMapState.UnregisterTile(_key, this);
        _isRegistered = false;
    }
}
