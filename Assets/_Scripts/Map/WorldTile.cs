using Fusion;
using UnityEngine;

public class WorldTile : NetworkBehaviour
{
    [SerializeField] private TilePalette _palette;
    [SerializeField] private MeshRenderer _renderer;

    [Networked]
    public int TileId { get; set; }

    private NetworkBehaviour.ChangeDetector _changeDetector;

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

        if (_palette == null)
        {
            var mapState = FindFirstObjectByType<WorldMapState>();
            if (mapState != null)
                _palette = mapState.TilePalette;
        }

        ApplyMaterial();
    }

    public override void Render()
    {
        if (_changeDetector == null)
            return;

        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(TileId))
            {
                ApplyMaterial();
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
}
