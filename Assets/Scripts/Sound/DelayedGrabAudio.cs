using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class GrabSoundDelayed : MonoBehaviour
{
    public string soundName;
    public float  delay = 2f;

    private void Awake()
    {
        GetComponent<XRGrabInteractable>().selectEntered.AddListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        Invoke(nameof(PlaySound), delay);
    }

    private void PlaySound()
    {
        SoundManager.Instance.Play(soundName);
    }
}