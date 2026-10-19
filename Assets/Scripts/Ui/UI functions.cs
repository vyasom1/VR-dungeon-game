using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public SceneData sceneData;

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void LoadSceneFromData()
    {
        if (sceneData == null)
        {
            Debug.LogError("SceneData is not assigned in GameManager!");
            return;
        }

        SceneManager.LoadScene(sceneData.sceneIndex);
    }
}