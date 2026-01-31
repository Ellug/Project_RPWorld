using UnityEngine;

// 유닛 컨트롤 모드 컨트롤러. 핫키: U
public class UnitControlModeController : StageModeController
{
    [SerializeField] private UnitSelectionManager _selectionManager;
    [SerializeField] private bool _logDebug = false;

    public override StageInteractionMode Mode => StageInteractionMode.UnitControl;

    private void Awake()
    {
        AutoWireIfNeeded();
        if (_selectionManager != null)
            _selectionManager.enabled = IsActive;
    }

    // 모드 진입 시 UnitSelectionManager 활성화
    protected override void OnModeEnter()
    {
        if (_selectionManager != null)
            _selectionManager.enabled = true;

        if (_logDebug)
            Debug.Log("[UnitControlModeController] Mode entered.");
    }

    // 모드 퇴장 시 UnitSelectionManager 비활성화
    protected override void OnModeExit()
    {
        if (_selectionManager != null)
            _selectionManager.enabled = false;

        if (_logDebug)
            Debug.Log("[UnitControlModeController] Mode exited.");
    }

    // 참조 자동 탐색
    private void AutoWireIfNeeded()
    {
        if (_selectionManager == null)
            _selectionManager = GetComponent<UnitSelectionManager>();

        if (_selectionManager == null)
            _selectionManager = FindFirstObjectByType<UnitSelectionManager>();
    }
}
