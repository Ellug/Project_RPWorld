using UnityEngine;

[CreateAssetMenu(menuName = "RPWorld/Tile Palette")]
public class TilePalette : ScriptableObject
{
    [SerializeField] private Material[] _materials;

    public int Count => _materials != null ? _materials.Length : 0;

    public Material GetMaterial(int tileId)
    {
        if (_materials == null || _materials.Length == 0)
            return null;

        var index = Mathf.Clamp(tileId, 0, _materials.Length - 1);
        return _materials[index];
    }
}
