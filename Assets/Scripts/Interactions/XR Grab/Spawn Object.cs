using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SpawnAtHandOnActivate : MonoBehaviour
{
    public GameObject itemPrefab;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable != null)
            grabInteractable.activated.AddListener(OnActivate);
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
            grabInteractable.activated.RemoveListener(OnActivate);
    }

    private void OnActivate(ActivateEventArgs args)
    {
        if (itemPrefab == null) return;

        // get the interactor that activated (i.e., held the object)
        var interactor = args.interactorObject as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor;
        if (interactor != null)
        {
            // Unity’s attachTransform is the “hand” grab point for that interactor
            Transform handAttach = interactor.attachTransform;
            if (handAttach != null)
            {
                Instantiate(itemPrefab, handAttach.position, handAttach.rotation);
                SoundManager.Instance.Play("NaratorLvl1-4");
                return;
            }
        }

        // fallback: use this object’s own transform
        Instantiate(itemPrefab, transform.position, transform.rotation);
    }
}