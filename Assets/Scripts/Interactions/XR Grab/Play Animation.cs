using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class PlayAnimationOnActivate : MonoBehaviour
{
    public Animator animator;
    public string triggerName = "";

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable != null)
            grabInteractable.activated.AddListener(OnActivate);
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
            grabInteractable.activated.RemoveListener(OnActivate);
    }

    private void OnActivate(ActivateEventArgs args)
    {
        if (animator != null)
            animator.SetTrigger(triggerName);
    }
}