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
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("Scene name is empty! Please set it in the Inspector.");
        }
    }
}
