using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SocketAnimationTrigger : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Name of the animation state to play (must match the Animator state)")]
    [SerializeField] private string animationStateName;

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
        GameObject insertedObject = args.interactableObject.transform.gameObject;

        // Get Animator directly from the inserted object
        Animator animator = insertedObject.GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogWarning($"Inserted object '{insertedObject.name}' has no Animator component.");
            return;
        }

        if (string.IsNullOrEmpty(animationStateName))
        {
            Debug.LogWarning("Animation state name is not set in the Inspector.");
            return;
        }

        // Play animation on layer 0
        animator.Play(animationStateName, 0);
        Debug.Log($"Playing animation '{animationStateName}' on '{insertedObject.name}'");
    }
}