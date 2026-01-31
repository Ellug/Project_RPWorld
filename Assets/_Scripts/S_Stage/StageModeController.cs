using UnityEngine;

// Stage 인터랙션 모드 컨트롤러 베이스 클래스
public abstract class StageModeController : MonoBehaviour
{
    // 이 컨트롤러가 담당하는 모드
    public abstract StageInteractionMode Mode { get; }

    public bool IsActive { get; private set; }

    // StageInteractionModeManager에서 호출
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

    // 모드 진입 시 호출 (서브클래스에서 오버라이드)
    protected virtual void OnModeEnter()
    {
    }

    // 모드 퇴장 시 호출 (서브클래스에서 오버라이드)
    protected virtual void OnModeExit()
    {
    }

    private void OnEnable()
    {
        if (!IsActive)
            enabled = false;
    }
}
