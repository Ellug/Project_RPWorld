using UnityEngine;

// 타일 배치 모드 컨트롤러. 핫키: T
public class TilePlacementModeController : StageModeController
{
    [SerializeField] private TilePlacementController _tilePlacementController;

    public override StageInteractionMode Mode => StageInteractionMode.TilePlacement;

    private void Awake()
    {
        AutoWireIfNeeded();
        if (_tilePlacementController != null)
            _tilePlacementController.enabled = IsActive;
    }

    protected override void OnModeEnter()
    {
        if (_tilePlacementController != null)
            _tilePlacementController.enabled = true;
    }

    protected override void OnModeExit()
    {
        if (_tilePlacementController != null)
            _tilePlacementController.enabled = false;
    }

    private void AutoWireIfNeeded()
    {
        if (_tilePlacementController == null)
            _tilePlacementController = GetComponent<TilePlacementController>();

        if (_tilePlacementController == null)
            _tilePlacementController = FindFirstObjectByType<TilePlacementController>();
    }
}
