using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SpoonSwapInteraction : MonoBehaviour
{
    [Header("References")]
    public XRGrabInteractable Object;
    public GameObject objectToDeactivate;
    public GameObject objectToActivate;
    public NearFarInteractor otherHandInteractor;

    [Header("Input")]
    public InputActionReference triggerAction;

    [Header("Settings")]
    public float triggerRange = 0.2f;

    private bool spoonIsGrabbed = false;

    void OnEnable()
    {
        Object.selectEntered.AddListener(OnSpoonGrabbed);
        Object.selectExited.AddListener(OnSpoonReleased);
        triggerAction?.action.Enable();
    }

    void OnDisable()
    {
        Object.selectEntered.RemoveListener(OnSpoonGrabbed);
        Object.selectExited.RemoveListener(OnSpoonReleased);
        triggerAction?.action.Disable();
    }

    void OnSpoonGrabbed(SelectEnterEventArgs args) => spoonIsGrabbed = true;
    void OnSpoonReleased(SelectExitEventArgs args) => spoonIsGrabbed = false;

    void Update()
    {
        if (!spoonIsGrabbed) return;

        float dist = Vector3.Distance(
            otherHandInteractor.transform.position,
            objectToDeactivate.transform.position
        );

        bool triggerPressed = triggerAction != null &&
                              triggerAction.action.IsPressed();

        if (dist <= triggerRange && triggerPressed)
{
    objectToActivate.transform.position = Object.transform.position;
    objectToActivate.transform.rotation = Object.transform.rotation;
    objectToDeactivate.SetActive(false);
    objectToActivate.SetActive(true);
}   
    }
}