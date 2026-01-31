using UnityEngine;

public class UnitControlModeController : StageModeController
{
    [SerializeField] private UnitSelectionManager _selectionManager;
    [SerializeField] private bool _logDebug = false;

    public override StageInteractionMode Mode => StageInteractionMode.UnitControl;

    private void Awake()
    {
        AutoWireIfNeeded();
    }

    protected override void OnModeEnter()
    {
        if (_selectionManager != null)
            _selectionManager.enabled = true;

        if (_logDebug)
            Debug.Log("[UnitControlModeController] Mode entered.");
    }

    protected override void OnModeExit()
    {
        if (_selectionManager != null)
            _selectionManager.enabled = false;

        if (_logDebug)
            Debug.Log("[UnitControlModeController] Mode exited.");
    }

    private void AutoWireIfNeeded()
    {
        if (_selectionManager == null)
            _selectionManager = GetComponent<UnitSelectionManager>();

        if (_selectionManager == null)
            _selectionManager = FindFirstObjectByType<UnitSelectionManager>();
    }
}
