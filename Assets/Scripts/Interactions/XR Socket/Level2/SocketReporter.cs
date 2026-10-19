using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SocketReporter : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int socketNumber = 1; // set 1 or 2 in Inspector
    [SerializeField] private DualSocketSceneLoader manager; // drag parent here

    private XRSocketInteractor socketInteractor;

    private void Awake()
    {
        socketInteractor = GetComponent<XRSocketInteractor>();
    }

    private void OnEnable()
    {
        socketInteractor.selectEntered.AddListener(OnObjectInserted);
        socketInteractor.selectExited.AddListener(OnObjectRemoved);
    }

    private void OnDisable()
    {
        socketInteractor.selectEntered.RemoveListener(OnObjectInserted);
        socketInteractor.selectExited.RemoveListener(OnObjectRemoved);
    }

    private void OnObjectInserted(SelectEnterEventArgs args)
    {
        if (socketNumber == 1) manager.ReportSocket1Filled();
        else manager.ReportSocket2Filled();
    }

    private void OnObjectRemoved(SelectExitEventArgs args)
    {
        if (socketNumber == 1) manager.ReportSocket1Emptied();
        else manager.ReportSocket2Emptied();
    }
}