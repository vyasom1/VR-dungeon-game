using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

public class LoadSceneOnInsert : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string sceneName;


    [Header("Scene Data")]
    [SerializeField] private SceneData sceneData;

    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;

    private void Awake()
    {
        socketInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
    }

    private void OnEnable()
    {
        socketInteractor.selectEntered.AddListener(OnObjectInserted);
    }

    private void OnDisable()
    {
        socketInteractor.selectEntered.RemoveListener(OnObjectInserted);
    }

    private void OnObjectInserted(SelectEnterEventArgs args)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("Scene name is not set in the Inspector.");
            return;
        }

        if (sceneData != null)
            sceneData.sceneIndex = sceneName;

        Debug.Log($"Object inserted. Loading scene '{sceneName}'...");
        SceneManager.LoadScene(sceneName);
    }
}