using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PokeAnimationToggle : MonoBehaviour
{
    public Animator animator;
    public AnimationClip lightsOffClip;  // plays on first poke
    public AnimationClip lightsOnClip;   // plays on second poke

    private XRBaseInteractable _interactable;
    private bool _isPokedState = false;

    private void Awake() => _interactable = GetComponent<XRBaseInteractable>();
    private void OnEnable() => _interactable.selectEntered.AddListener(OnPoked);
    private void OnDisable() => _interactable.selectEntered.RemoveListener(OnPoked);

    private void OnPoked(SelectEnterEventArgs args)
    {
        if (args.interactorObject is not UnityEngine.XR.Interaction.Toolkit.Interactors.XRPokeInteractor) return;

        _isPokedState = !_isPokedState;
        animator.Play(_isPokedState ? lightsOffClip.name : lightsOnClip.name);
    }
}

