using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PokeSoundDelayed : MonoBehaviour
{
    public string soundName;
    public float  delay    = 1f;
    public bool   playOnce = true;

    private bool _played;

    private void Awake()
    {
        GetComponent<XRBaseInteractable>().hoverEntered.AddListener(OnPoke);
    }

    private void OnPoke(HoverEnterEventArgs args)
    {
        if (playOnce && _played) return;
        Invoke(nameof(PlaySound), delay);
        _played = true;
    }

    private void PlaySound()
    {
        SoundManager.Instance.Play(soundName);
    }
}