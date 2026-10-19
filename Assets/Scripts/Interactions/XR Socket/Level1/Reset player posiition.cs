using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MovePlayerOnInsert : MonoBehaviour
{
    [Header("Player Reference")]
    [Tooltip("Drag your XR Origin (player root) here")]
    [SerializeField] private Transform playerRoot;

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
        if (playerRoot == null)
        {
            Debug.LogWarning("Player Root is not assigned in the Inspector.");
            return;
        }

        // Move player to world position (0,0,0)
        playerRoot.position = Vector3.zero;

        Debug.Log("Player moved to (0,0,0)");
    }
}