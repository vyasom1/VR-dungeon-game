using UnityEngine;

public class TimedSound : MonoBehaviour
{
    public string soundName;
    public float  delay = 3f;

    private void Start()
    {
        Invoke(nameof(PlaySound), delay);
    }

    private void PlaySound()
    {
        SoundManager.Instance.Play(soundName);
    }
}