using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public static LevelLoader Instance { get; private set; }

    public Animator transition;
    public float transitionTime = 1f;

    private void Awake()
    {
        // Simple singleton so other classes can call LevelLoader.Instance safely.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Optional: keep across scenes if you want the same transition object persistent
        // DontDestroyOnLoad(gameObject);
    }

    // NOTE: removed the previous Update() that triggered LoadNextLevel by mouse and removed the build-index based LoadNextLevel.
    // Scene changes should now be requested via LoadSceneByName / LoadSceneWithDelay.

    // Public API: load by scene name using the animated transition.
    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("LevelLoader.LoadSceneByName: sceneName is empty.");
            return;
        }

        if (transition != null)
            StartCoroutine(LoadlevelByName(sceneName));
        else
            SceneManager.LoadScene(sceneName);
    }

    // Public API: load by name after a delay (useful for enemy death delays).
    public void LoadSceneWithDelay(string sceneName, float delay)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("LevelLoader.LoadSceneWithDelay: sceneName is empty.");
            return;
        }

        if (transition != null)
            StartCoroutine(LoadlevelByName(sceneName, delay));
        else
            StartCoroutine(ImmediateLoadWithDelay(sceneName, delay));
    }

    private IEnumerator ImmediateLoadWithDelay(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

    // Coroutine used to load by name, with optional pre-delay.
    private IEnumerator LoadlevelByName(string sceneName, float preDelay = 0f)
    {
        if (preDelay > 0f)
            yield return new WaitForSeconds(preDelay);

        if (transition != null)
            transition.SetTrigger("Start");

        yield return new WaitForSeconds(transitionTime);

        SceneManager.LoadScene(sceneName);
    }
}
