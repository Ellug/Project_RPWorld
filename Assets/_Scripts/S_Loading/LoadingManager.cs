using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    [SerializeField] private string _stageScenePath = "Assets/_Scenes/Stage.unity";
    [SerializeField] private float _minimumDisplayTime = 0.5f;

    private void Start()
    {
        StartCoroutine(LoadStageRoutine());
    }

    private IEnumerator LoadStageRoutine()
    {
        if (_minimumDisplayTime > 0f)
            yield return new WaitForSeconds(_minimumDisplayTime);

        var runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;
        if (runner == null)
        {
            SceneManager.LoadScene(_stageScenePath);
            yield break;
        }

        while (!runner.IsRunning)
            yield return null;

        if (runner.IsSharedModeMasterClient)
            runner.LoadScene(_stageScenePath, LoadSceneMode.Single, LocalPhysicsMode.None, true);
    }
}
