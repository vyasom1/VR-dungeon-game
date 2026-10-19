using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PokeSoundTrigger : MonoBehaviour
{
    public string audioName        = "NaratorLvl3-3";
    public string delayedAudioName = "NaratorLvl3-4";
    public float  delay            = 2f;

    private XRBaseInteractable _interactable;

    private void Awake()    => _interactable = GetComponent<XRBaseInteractable>();
    private void OnEnable()  => _interactable.selectEntered.AddListener(OnPoked);
    private void OnDisable() => _interactable.selectEntered.RemoveListener(OnPoked);

    private void OnPoked(SelectEnterEventArgs args)
    {
        if (args.interactorObject is not UnityEngine.XR.Interaction.Toolkit.Interactors.XRPokeInteractor) return;
        SoundManager.Instance.Play(audioName);
        Invoke(nameof(PlayDelayed), delay);
    }

    private void PlayDelayed()
    {
        SoundManager.Instance.Play(delayedAudioName);
    }
}