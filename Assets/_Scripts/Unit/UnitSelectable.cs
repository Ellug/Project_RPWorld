using System.Collections.Generic;
using UnityEngine;

// 선택 가능한 유닛. Collider 필수. UnitSelectionManager에서 관리.
[RequireComponent(typeof(Collider))]
public class UnitSelectable : MonoBehaviour
{
    // 모든 활성화된 선택 가능 유닛 (정적 목록)
    private static readonly HashSet<UnitSelectable> AllUnits = new();

    [Header("Selection Ring")]
    [SerializeField] private bool _showSelectionRing = true;
    [SerializeField] private UnitSelectionIndicator _selectionRing;
    [SerializeField] private float _selectionRingYOffset = 0.02f;
    [SerializeField] private bool _autoScaleRingToUnit = true;
    [SerializeField] private float _ringScaleMultiplier = 1.4f;
    [SerializeField] private float _ringMinRadius = 0.35f;
    [SerializeField] private float _ringMaxRadius = 5f;

    private bool _isSelected;

    public bool IsSelected => _isSelected;
    public static IReadOnlyCollection<UnitSelectable> All => AllUnits;

    private void Awake()
    {
        EnsureSelectionRing();
    }

    private void OnEnable()
    {
        EnsureSelectionRing();
        AllUnits.Add(this); // 정적 목록에 등록
    }

    private void OnDisable()
    {
        AllUnits.Remove(this); // 정적 목록에서 제거
        if (_selectionRing != null)
            _selectionRing.SetVisible(false);
    }

    private void OnDestroy()
    {
        AllUnits.Remove(this);
    }

    // 선택 상태 설정
    public void SetSelected(bool selected)
    {
        if (_isSelected == selected)
            return;

        _isSelected = selected;
        ApplySelectionVisual();
    }

    // 선택 링 표시/숨김
    private void ApplySelectionVisual()
    {
        if (_selectionRing != null)
            _selectionRing.SetVisible(_isSelected);
    }

    // 선택 링 초기화 (프리팹에 포함된 링 사용)
    private void EnsureSelectionRing()
    {
        if (!_showSelectionRing)
        {
            if (_selectionRing != null)
                _selectionRing.SetVisible(false);
            return;
        }

        if (_selectionRing == null)
            _selectionRing = GetComponentInChildren<UnitSelectionIndicator>(true);

        if (_selectionRing != null)
        {
            PositionSelectionRing();
            _selectionRing.SetVisible(_isSelected);
        }
        else
        {
            Debug.LogWarning($"[UnitSelectable] SelectionRing is missing on {name}. Add a ring child to the unit prefab.");
        }
    }

    // 선택 링 위치 및 크기 조정
    private void PositionSelectionRing()
    {
        if (_selectionRing == null)
            return;

        // 콜라이더 바닥에 배치
        var localY = 0f;
        var collider = GetComponent<Collider>();
        if (collider != null)
            localY = collider.bounds.min.y - transform.position.y;

        _selectionRing.transform.localPosition = new Vector3(0f, localY + _selectionRingYOffset, 0f);
        _selectionRing.transform.localRotation = Quaternion.identity;
        _selectionRing.transform.localScale = GetRingScale();
    }

    // 유닛 크기에 맞게 링 스케일 계산
    private Vector3 GetRingScale()
    {
        if (!_autoScaleRingToUnit || _selectionRing == null)
            return Vector3.one;

        var radius = GetUnitRadius();
        if (radius <= 0f)
            return Vector3.one;

        var targetRadius = Mathf.Clamp(radius * _ringScaleMultiplier, _ringMinRadius, _ringMaxRadius);
        var baseRadius = Mathf.Max(0.01f, _selectionRing.BaseRadius);
        var scale = targetRadius / baseRadius;
        return new Vector3(scale, 1f, scale);
    }

    // 유닛의 XZ 평면 반지름 계산 (콜라이더 또는 렌더러 기반)
    private float GetUnitRadius()
    {
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            var extents = collider.bounds.extents;
            return Mathf.Max(extents.x, extents.z);
        }

        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            var extents = renderer.bounds.extents;
            return Mathf.Max(extents.x, extents.z);
        }

        return 0f;
    }

}
