using Fusion;
using UnityEngine;

public class WorldTile : NetworkBehaviour
{
    [SerializeField] private TilePalette _palette;
    [SerializeField] private MeshRenderer _renderer;

    [Networked]
    public int TileId { get; set; }
    [Networked]
    public int KeyX { get; set; }
    [Networked]
    public int KeyY { get; set; }

    private NetworkBehaviour.ChangeDetector _changeDetector;
    private WorldMapState _mapState;
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

        if (_mapState == null)
            _mapState = FindFirstObjectByType<WorldMapState>();

        if (_mapState == null)
            return;

        _key = new Vector2Int(KeyX, KeyY);
        ApplyPositionFromKey();
        _mapState.RegisterTile(_key, this);
        _isRegistered = true;
    }

    private void TryResolveMapState()
    {
        if (_mapState == null)
            _mapState = FindFirstObjectByType<WorldMapState>();

        if (_mapState == null)
            return;

        if (_palette == null)
            _palette = _mapState.TilePalette;

        if (!_isRegistered)
            RegisterWithMapState();
    }

    private void UpdateKeyAndPosition()
    {
        var newKey = new Vector2Int(KeyX, KeyY);
        if (newKey == _key && _isRegistered)
            return;

        if (_mapState == null)
            _mapState = FindFirstObjectByType<WorldMapState>();

        if (_mapState == null)
            return;

        if (_isRegistered)
            _mapState.UnregisterTile(_key, this);

        _key = newKey;
        ApplyPositionFromKey();
        _mapState.RegisterTile(_key, this);
        _isRegistered = true;
    }

    private void ApplyPositionFromKey()
    {
        if (_mapState == null)
            return;

        transform.position = _mapState.KeyToPosition(_key);
    }

    private void UnregisterFromMapState()
    {
        if (!_isRegistered || _mapState == null)
            return;

        _mapState.UnregisterTile(_key, this);
        _isRegistered = false;
    }
}
