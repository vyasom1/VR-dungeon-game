using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


public class GrabSound : MonoBehaviour
{
    public string soundName;
    public bool   playOnce = true;

    private bool _played;

    private void Awake()
    {
        GetComponent<XRGrabInteractable>().selectEntered.AddListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (playOnce && _played) return;
        SoundManager.Instance.Play(soundName);
        _played = true;
    }
}