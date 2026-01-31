using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class UnitSelectionIndicator : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Ring")]
    [SerializeField] private float _radius = 1.2f;
    [SerializeField] private float _thickness = 0.08f;
    [SerializeField] private int _segments = 64;
    [SerializeField] private float _yOffset = 0.02f;
    [SerializeField] private Color _color = new(0.2f, 0.95f, 0.3f, 1f);
    [SerializeField] private bool _autoRebuild = true;

    [Header("Rendering")]
    [SerializeField] private Material _material;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private MaterialPropertyBlock _propertyBlock;

    public float BaseRadius => _radius;

    private void Awake()
    {
        EnsureComponents();
        BuildMesh();
        ApplyMaterial();
    }

    private void OnValidate()
    {
        if (!_autoRebuild)
            return;

        EnsureComponents();
        BuildMesh();
        ApplyMaterial();
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    private void EnsureComponents()
    {
        if (_meshFilter == null)
            _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();
    }

    private void BuildMesh()
    {
        var segments = Mathf.Max(3, _segments);
        var outerRadius = Mathf.Max(0.02f, _radius);
        var innerRadius = Mathf.Clamp(_radius - _thickness, 0.01f, outerRadius - 0.01f);

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "SelectionRing" };
            _meshFilter.sharedMesh = _mesh;
        }
        else
        {
            _mesh.Clear();
        }

        var vertexCount = segments * 2;
        var vertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var triangles = new int[segments * 12];

        for (var i = 0; i < segments; i++)
        {
            var angle = i * Mathf.PI * 2f / segments;
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);

            var outer = new Vector3(cos * outerRadius, _yOffset, sin * outerRadius);
            var inner = new Vector3(cos * innerRadius, _yOffset, sin * innerRadius);

            var vi = i * 2;
            vertices[vi] = outer;
            vertices[vi + 1] = inner;
            uvs[vi] = new Vector2((cos + 1f) * 0.5f, (sin + 1f) * 0.5f);
            uvs[vi + 1] = uvs[vi];
        }

        var ti = 0;
        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            var outerI = i * 2;
            var innerI = outerI + 1;
            var outerN = next * 2;
            var innerN = outerN + 1;

            triangles[ti++] = outerI;
            triangles[ti++] = outerN;
            triangles[ti++] = innerN;
            triangles[ti++] = outerI;
            triangles[ti++] = innerN;
            triangles[ti++] = innerI;
        }

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            var outerI = i * 2;
            var innerI = outerI + 1;
            var outerN = next * 2;
            var innerN = outerN + 1;

            triangles[ti++] = innerN;
            triangles[ti++] = outerN;
            triangles[ti++] = outerI;
            triangles[ti++] = innerI;
            triangles[ti++] = innerN;
            triangles[ti++] = outerI;
        }

        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    private void ApplyMaterial()
    {
        if (_meshRenderer == null)
            return;

        if (_material != null)
            _meshRenderer.sharedMaterial = _material;

        ApplyColor();
    }

    private void ApplyColor()
    {
        var material = _meshRenderer != null ? _meshRenderer.sharedMaterial : null;
        if (material == null)
            return;

        _propertyBlock ??= new MaterialPropertyBlock();
        _meshRenderer.GetPropertyBlock(_propertyBlock);

        if (material.HasProperty(BaseColorId))
            _propertyBlock.SetColor(BaseColorId, _color);
        else if (material.HasProperty(ColorId))
            _propertyBlock.SetColor(ColorId, _color);

        _meshRenderer.SetPropertyBlock(_propertyBlock);
    }

}
