using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 로딩 씬 관리자. 프로그레스 바 표시 및 Fusion 동기화 후 Stage 씬 전환.
public class LoadingManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string _stageScenePath = "Assets/_Scenes/Stage.unity";
    [SerializeField] private float _minimumDisplayTime = 0.5f;

    [Header("Progress UI")]
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private bool _smoothProgress = true;
    [SerializeField] private float _smoothSpeed = 3f;

    private float _localProgress;
    private float _displayProgress;
    private bool _transitionStarted;

    public float Progress => _displayProgress;

    // 프로그레스 변경 시 이벤트. 커스텀 UI 연동용.
    public event Action<float> OnProgressChanged;

    private void Awake()
    {
        if (_progressSlider != null)
            _progressSlider.value = 0f;
    }

    private void Start()
    {
        StartCoroutine(LoadingRoutine());
    }

    private void Update()
    {
        UpdateDisplayProgress();
        UpdateUI();
    }

    private IEnumerator LoadingRoutine()
    {
        _localProgress = 0f;
        _displayProgress = 0f;
        _transitionStarted = false;

        // 최소 표시 시간 동안 프로그레스 바 채우기
        if (_minimumDisplayTime > 0f)
        {
            var elapsed = 0f;
            while (elapsed < _minimumDisplayTime)
            {
                elapsed += Time.deltaTime;
                _localProgress = Mathf.Clamp01(elapsed / _minimumDisplayTime) * 0.9f;
                yield return null;
            }
        }

        _localProgress = 0.9f;

        var runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;

        if (runner != null)
        {
            // Fusion Runner가 실행 중일 때까지 대기
            while (!runner.IsRunning)
            {
                yield return null;
            }

            _localProgress = 1f;

            // Shared Mode에서 MasterClient(호스트)만 씬 전환 트리거
            // 호스트가 LoadScene 호출하면 모든 클라이언트가 자동으로 씬 전환됨
            if (runner.IsSharedModeMasterClient && !_transitionStarted)
            {
                _transitionStarted = true;
                Debug.Log("[LoadingManager] Host triggering scene transition");
                runner.LoadScene(_stageScenePath, LoadSceneMode.Single, LocalPhysicsMode.None, true);
            }
        }
        else
        {
            // 네트워크 없으면 일반 씬 로드
            _localProgress = 1f;
            yield return null;
            SceneManager.LoadScene(_stageScenePath);
        }
    }

    // 부드러운 프로그레스 바 애니메이션 처리.
    private void UpdateDisplayProgress()
    {
        if (_smoothProgress)
        {
            _displayProgress = Mathf.MoveTowards(_displayProgress, _localProgress, _smoothSpeed * Time.deltaTime);
        }
        else
        {
            _displayProgress = _localProgress;
        }

        OnProgressChanged?.Invoke(_displayProgress);
    }

    private void UpdateUI()
    {
        if (_progressSlider != null)
            _progressSlider.value = _displayProgress;
    }
}
