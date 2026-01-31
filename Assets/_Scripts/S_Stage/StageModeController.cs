using UnityEngine;

public abstract class StageModeController : MonoBehaviour
{
    public abstract StageInteractionMode Mode { get; }

    public bool IsActive { get; private set; }

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;

        if (active)
        {
            enabled = true;
            OnModeEnter();
        }
        else
        {
            OnModeExit();
            enabled = false;
        }
    }

    protected virtual void OnModeEnter()
    {
    }

    protected virtual void OnModeExit()
    {
    }

    private void OnEnable()
    {
        if (!IsActive)
            enabled = false;
    }
}
