using UnityEngine;

// 타일 ID와 Material 매핑. Assets > Create > RPWorld > Tile Palette
[CreateAssetMenu(menuName = "RPWorld/Tile Palette")]
public class TilePalette : ScriptableObject
{
    [SerializeField] private Material[] _materials;

    // 등록된 Material 개수
    public int Count => _materials != null ? _materials.Length : 0;

    // tileId에 해당하는 Material 반환 (범위 벗어나면 clamp)
    public Material GetMaterial(int tileId)
    {
        if (_materials == null || _materials.Length == 0)
            return null;

        var index = Mathf.Clamp(tileId, 0, _materials.Length - 1);
        return _materials[index];
    }
}
