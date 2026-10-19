using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

public class ResetPlayerPosition : MonoBehaviour 
{
    private XROrigin xrOrigin;

    private void Awake()
    {
        xrOrigin = GetComponent<XROrigin>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(DelayedReset());
    }

    private IEnumerator DelayedReset()
    {
        yield return null;
        ResetPosition();
    }

    public void ResetPosition()
    {
        if (xrOrigin == null)
        {
            Debug.LogWarning("XROrigin not found.");
            return;
        }

        InputTracking.Recenter();
        xrOrigin.MoveCameraToWorldLocation(new Vector3(0f, 1.36144f, 0f));

        Debug.Log("Player reset to spawn position.");
    }
}