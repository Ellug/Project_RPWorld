using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        // 최소 표시 시간 대기
        if (_minimumDisplayTime > 0f)
        {
            var elapsed = 0f;
            while (elapsed < _minimumDisplayTime)
            {
                elapsed += Time.deltaTime;
                // 최소 표시 시간 동안 프로그레스 증가
                _localProgress = Mathf.Clamp01(elapsed / _minimumDisplayTime) * 0.9f;
                yield return null;
            }
        }

        _localProgress = 0.9f;

        // NetworkRunner 대기
        var runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;

        if (runner != null)
        {
            // Runner가 실행 중일 때까지 대기
            while (!runner.IsRunning)
            {
                yield return null;
            }

            _localProgress = 1f;

            // 호스트만 씬 전환 수행
            if (runner.IsSharedModeMasterClient && !_transitionStarted)
            {
                _transitionStarted = true;
                Debug.Log("[LoadingManager] Host triggering scene transition");
                runner.LoadScene(_stageScenePath, LoadSceneMode.Single, LocalPhysicsMode.None, true);
            }
            // 클라이언트는 호스트가 씬 전환을 트리거할 때까지 대기
            // Fusion이 자동으로 모든 클라이언트의 씬을 전환함
        }
        else
        {
            // 네트워크 없으면 일반 씬 로드
            _localProgress = 1f;
            yield return null;
            SceneManager.LoadScene(_stageScenePath);
        }
    }

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
