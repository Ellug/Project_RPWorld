using UnityEngine;

public class BuildingPlacementModeController : StageModeController
{
    [SerializeField] private bool _logDebug = false;

    public override StageInteractionMode Mode => StageInteractionMode.BuildingPlacement;

    protected override void OnModeEnter()
    {
        if (_logDebug)
            Debug.Log("[BuildingPlacementModeController] Mode entered.");
    }

    protected override void OnModeExit()
    {
        if (_logDebug)
            Debug.Log("[BuildingPlacementModeController] Mode exited.");
    }
}
