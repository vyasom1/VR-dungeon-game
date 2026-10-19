using UnityEngine;

public class KeyReveal : MonoBehaviour
{
    public Color hiddenColor;
    public Color revealedColor;
    public float transitionSpeed = 3f;

    private Renderer rend;
    private Color targetColor;

    void Start()
    {
        rend = GetComponent<Renderer>();

        if (rend == null)
        {
            Debug.LogError("KeyReveal: No Renderer found on " + gameObject.name);
            return;
        }

        if (rend.material == null)
        {
            Debug.LogError("KeyReveal: No Material found on " + gameObject.name);
            return;
        }

        Debug.Log("KeyReveal started fine on: " + gameObject.name);
        rend.material.color = hiddenColor;
        targetColor = hiddenColor;
    }

    public void Reveal()
    {
        Debug.Log("Reveal called!");
        targetColor = revealedColor;
    }

    public void Hide()
    {
        Debug.Log("Hide called!");
        targetColor = hiddenColor;
    }

    void Update()
    {
        if (rend == null) return;
        rend.material.color = Color.Lerp(rend.material.color, targetColor, Time.deltaTime * transitionSpeed);
    }
}