using UnityEngine;

public class KeyShine : MonoBehaviour
{
    public Light skullLight;
    public float maxRange = 3f;
    public float shineSpeed = 2f;

    private Renderer keyRenderer;
    private float currentShine = 0f;

    void Start()
    {
        keyRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        float targetShine = 0f;

        if (skullLight != null && skullLight.enabled && 
            skullLight.gameObject.activeSelf && skullLight.intensity > 0f)
        {
            float distance = Vector3.Distance(
                skullLight.transform.position,
                transform.position
            );

            targetShine = Mathf.Clamp01(1f - (distance / maxRange));
        }

        currentShine = Mathf.MoveTowards(currentShine, targetShine, 
            shineSpeed * Time.deltaTime);

        keyRenderer.material.SetFloat("_ShineIntensity", currentShine);
    }
}