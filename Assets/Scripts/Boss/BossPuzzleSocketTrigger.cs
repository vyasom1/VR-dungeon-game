using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Links VR Sockets (XRSocketInteractor) to Boss mechanics.
/// Triggers damage, stuns, or shield drops when puzzle objects are inserted.
/// </summary>
public class BossPuzzleSocketTrigger : MonoBehaviour
{
    public enum SocketAction
    {
        DealDamage,
        StunBoss,
        RemoveInvulnerability
    }

    [Header("Boss Target")]
    [SerializeField] private BossHealth targetBoss;

    [Header("Action On Solved")]
    [SerializeField] private SocketAction actionOnSolved = SocketAction.DealDamage;
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private float stunDuration = 8f;

    [Header("Sockets Required")]
    [Tooltip("List of XRSocketInteractors that must ALL be filled to trigger the action. If empty, uses the socket on this GameObject.")]
    [SerializeField] private List<XRSocketInteractor> requiredSockets = new List<XRSocketInteractor>();

    [Header("Visual & Audio")]
    [SerializeField] private GameObject activationVFX;
    [SerializeField] private string activationSoundName;

    [Header("Events")]
    public UnityEvent OnPuzzleActivated;

    private HashSet<XRSocketInteractor> _filledSockets = new HashSet<XRSocketInteractor>();

    private void Awake()
    {
        // If no sockets assigned, check this GameObject
        if (requiredSockets.Count == 0)
        {
            XRSocketInteractor selfSocket = GetComponent<XRSocketInteractor>();
            if (selfSocket != null) requiredSockets.Add(selfSocket);
        }
    }

    private void OnEnable()
    {
        foreach (var socket in requiredSockets)
        {
            if (socket != null)
            {
                socket.selectEntered.AddListener(OnSocketEntered);
                socket.selectExited.AddListener(OnSocketExited);
            }
        }
    }

    private void OnDisable()
    {
        foreach (var socket in requiredSockets)
        {
            if (socket != null)
            {
                socket.selectEntered.RemoveListener(OnSocketEntered);
                socket.selectExited.RemoveListener(OnSocketExited);
            }
        }
    }

    private void OnSocketEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor socket)
        {
            _filledSockets.Add(socket);
            CheckAllSockets();
        }
    }

    private void OnSocketExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor socket)
        {
            _filledSockets.Remove(socket);
        }
    }

    private void CheckAllSockets()
    {
        if (_filledSockets.Count >= requiredSockets.Count && requiredSockets.Count > 0)
        {
            TriggerBossAction();
        }
    }

    private void TriggerBossAction()
    {
        if (targetBoss == null || targetBoss.IsDead) return;

        Debug.Log("[BossPuzzleSocketTrigger] All sockets filled! Triggering boss action...");

        if (activationVFX != null)
        {
            Instantiate(activationVFX, transform.position, transform.rotation);
        }

        if (SoundManager.Instance != null && !string.IsNullOrEmpty(activationSoundName))
        {
            SoundManager.Instance.Play(activationSoundName);
        }

        switch (actionOnSolved)
        {
            case SocketAction.DealDamage:
                targetBoss.ForceDamage(damageAmount);
                break;

            case SocketAction.StunBoss:
                targetBoss.StunForSeconds(stunDuration);
                break;

            case SocketAction.RemoveInvulnerability:
                targetBoss.SetVulnerable(true);
                break;
        }

        OnPuzzleActivated?.Invoke();
    }
}
