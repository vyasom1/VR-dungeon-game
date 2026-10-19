using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class EnableRotationOnInsert : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;

    private void Awake()
    {
        socketInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
    }

    private void OnEnable()
    {
        socketInteractor.selectEntered.AddListener(OnObjectInserted);
    }

    private void OnDisable()
    {
        socketInteractor.selectEntered.RemoveListener(OnObjectInserted);
    }

    private void OnObjectInserted(SelectEnterEventArgs args)
    {
        GameObject insertedObject = args.interactableObject.transform.gameObject;

        Rigidbody rb = insertedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Allow physics rotation
            rb.constraints = RigidbodyConstraints.None;
            Debug.Log($"Rotation enabled on {insertedObject.name}");
        }
        else
        {
            Debug.LogWarning($"Inserted object '{insertedObject.name}' has no Rigidbody to rotate.");
        }
    }
}