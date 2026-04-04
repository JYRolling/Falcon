using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButton : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Enter Scene's name you want to link (MUST BE IN Build Settings)")]
    public string sceneName;

    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            // Prefer LevelLoader if present so transitions are used consistently.
            if (LevelLoader.Instance != null)
                LevelLoader.Instance.LoadSceneByName(sceneName);
            else
                SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("Scene name is empty! Please set it in the Inspector.");
        }
    }
}
