using UnityEngine;

public class FlashlightTrigger : MonoBehaviour
{
    private KeyReveal currentKey;

    void OnTriggerEnter(Collider other)
    {
        KeyReveal key = other.GetComponentInChildren<KeyReveal>();
        if (key != null)
        {
            currentKey = key;
            currentKey.Reveal();
        }
    }

    void OnTriggerExit(Collider other)
    {
        KeyReveal key = other.GetComponentInChildren<KeyReveal>();
        if (key != null && key == currentKey)
        {
            currentKey.Hide();
            currentKey = null;
        }
    }
}