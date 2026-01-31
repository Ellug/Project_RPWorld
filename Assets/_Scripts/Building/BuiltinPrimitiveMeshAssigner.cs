using UnityEngine;

// Unity 내장 프리미티브 메시(Cube, Capsule)를 자동 할당. 에디터/런타임 모두 동작
[ExecuteAlways]
public class BuiltinPrimitiveMeshAssigner : MonoBehaviour
{
    public enum PrimitiveKind
    {
        Cube,
        Capsule
    }

    [SerializeField] private PrimitiveKind _primitive = PrimitiveKind.Cube;
    [SerializeField] private Material _material;
    [SerializeField] private bool _applyMaterial = true;

    private void Awake() => Apply();

    private void Reset() => Apply();

    private void OnValidate() => Apply();

    // MeshFilter, MeshRenderer 자동 생성 및 메시/머티리얼 적용
    private void Apply()
    {
        var filter = GetComponent<MeshFilter>();
        if (filter == null)
            filter = gameObject.AddComponent<MeshFilter>();

        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<MeshRenderer>();

        var meshName = _primitive == PrimitiveKind.Cube ? "Cube.fbx" : "Capsule.fbx";
        var mesh = Resources.GetBuiltinResource<Mesh>(meshName);
        if (mesh != null && filter.sharedMesh != mesh)
            filter.sharedMesh = mesh;

        if (_applyMaterial && _material != null && renderer.sharedMaterial != _material)
            renderer.sharedMaterial = _material;
    }
}
