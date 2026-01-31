using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class UnitSelectable : MonoBehaviour
{
    private static readonly HashSet<UnitSelectable> AllUnits = new();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Renderer _renderer;
    [SerializeField] private Color _selectedColor = new(0.2f, 0.95f, 0.3f, 1f);
    [SerializeField] private bool _tintOnSelect = true;

    private MaterialPropertyBlock _propertyBlock;
    private Color _originalColor = Color.white;
    private bool _usesBaseColor = true;
    private bool _hasColorProperty;
    private bool _isSelected;

    public bool IsSelected => _isSelected;

    public static IReadOnlyCollection<UnitSelectable> All => AllUnits;

    private void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();

        CacheOriginalColor();
    }

    private void OnEnable()
    {
        CacheOriginalColor();
        AllUnits.Add(this);
    }

    private void OnDisable()
    {
        AllUnits.Remove(this);
    }

    private void OnDestroy()
    {
        AllUnits.Remove(this);
    }

    public void SetSelected(bool selected)
    {
        if (_isSelected == selected)
            return;

        _isSelected = selected;
        ApplySelectionVisual();
    }

    private void CacheOriginalColor()
    {
        if (_renderer == null || _renderer.sharedMaterial == null)
            return;

        var material = _renderer.sharedMaterial;
        if (material.HasProperty(BaseColorId))
        {
            _originalColor = material.GetColor(BaseColorId);
            _usesBaseColor = true;
            _hasColorProperty = true;
        }
        else if (material.HasProperty(ColorId))
        {
            _originalColor = material.GetColor(ColorId);
            _usesBaseColor = false;
            _hasColorProperty = true;
        }
        else
        {
            _hasColorProperty = false;
        }
    }

    private void ApplySelectionVisual()
    {
        if (!_tintOnSelect || _renderer == null)
            return;

        if (!_hasColorProperty)
            return;

        _propertyBlock ??= new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(_propertyBlock);

        var color = _isSelected ? _selectedColor : _originalColor;
        if (_usesBaseColor)
            _propertyBlock.SetColor(BaseColorId, color);
        else
            _propertyBlock.SetColor(ColorId, color);

        _renderer.SetPropertyBlock(_propertyBlock);
    }
}
