using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PokeObjectToggle : MonoBehaviour
{
    public List<GameObject> objectsToDisable = new List<GameObject>();
    public List<GameObject> objectsToEnable = new List<GameObject>();

    private XRBaseInteractable _interactable;
    private bool _isPokedState = false;

    private void Awake() => _interactable = GetComponent<XRBaseInteractable>();
    private void OnEnable() => _interactable.selectEntered.AddListener(OnPoked);
    private void OnDisable() => _interactable.selectEntered.RemoveListener(OnPoked);

    private void OnPoked(SelectEnterEventArgs args)
    {
        if (args.interactorObject is not UnityEngine.XR.Interaction.Toolkit.Interactors.XRPokeInteractor) return;

        _isPokedState = !_isPokedState;
        SetObjects(objectsToDisable, !_isPokedState);
        SetObjects(objectsToEnable, _isPokedState);
    }

    private void SetObjects(List<GameObject> objects, bool active)
    {
        foreach (GameObject obj in objects)
            if (obj != null) obj.SetActive(active);
    }
}