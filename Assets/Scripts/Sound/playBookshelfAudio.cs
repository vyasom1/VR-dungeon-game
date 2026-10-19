using UnityEngine;

public class Bookshelf : MonoBehaviour
{
    private bool _played;

    private void OnTriggerEnter(Collider other)
    {
        if (_played) return;
        if (other.CompareTag("Player"))
        {
            SoundManager.Instance.Play("NaratorLvl1-2");
            _played = true;
        }
    }
}