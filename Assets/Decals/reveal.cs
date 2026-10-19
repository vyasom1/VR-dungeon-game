using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DecalReveal : MonoBehaviour
{
    public float fadeSpeed = 2f;
    private DecalProjector decalProjector;
    private float targetFade = 0f;

    void Start()
    {
        decalProjector = GetComponent<DecalProjector>();
        decalProjector.fadeFactor = 0f;
    }

    void Update()
    {
        decalProjector.fadeFactor = Mathf.MoveTowards(
            decalProjector.fadeFactor,
            targetFade,
            fadeSpeed * Time.deltaTime
        );
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<SpotlightCollider>() != null)
        {
            targetFade = 1f;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<SpotlightCollider>() != null)
        {
            targetFade = 0f;
        }
    }
}