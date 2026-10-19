using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [System.Serializable]
    public class Sound
    {
        public string    name;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float     volume = 1f;
    }

    public Sound[] sounds;

    private AudioSource _source;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _source = gameObject.AddComponent<AudioSource>();
    }

    public void Play(string name)
    {
        Sound s = System.Array.Find(sounds, x => x.name == name);
        if (s == null) { Debug.LogWarning($"Sound '{name}' not found!"); return; }
        _source.PlayOneShot(s.clip, s.volume);
    }
}