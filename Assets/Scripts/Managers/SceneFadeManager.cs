using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    public Image  overlay;
    public float  fadeSpeed = 1f;
    public string transitionSound;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void FadeToScene(string sceneName)
    {
        StartCoroutine(Transition(sceneName));
    }

    private IEnumerator Transition(string sceneName)
    {
        // Fade to black
        for (float t = 0; t < 1; t += Time.deltaTime * fadeSpeed)
        {
            overlay.color = new Color(0, 0, 0, t);
            yield return null;
        }

        SoundManager.Instance.Play(transitionSound);

        SceneManager.LoadScene(sceneName);
        yield return null;

        // Fade back in
        for (float t = 1; t > 0; t -= Time.deltaTime * fadeSpeed)
        {
            overlay.color = new Color(0, 0, 0, t);
            yield return null;
        }
    }
}