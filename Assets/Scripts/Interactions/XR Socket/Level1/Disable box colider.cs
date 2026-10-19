using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class DisableColliderOnInsert : MonoBehaviour
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

        // Get the BoxCollider on the object
        BoxCollider boxCollider = insertedObject.GetComponent<BoxCollider>();

        if (boxCollider != null)
        {
            boxCollider.enabled = false; // Disable it
            Debug.Log($"BoxCollider disabled on {insertedObject.name}");
        }
        else
        {
            Debug.LogWarning($"Inserted object '{insertedObject.name}' has no BoxCollider to disable.");
        }
    }
}