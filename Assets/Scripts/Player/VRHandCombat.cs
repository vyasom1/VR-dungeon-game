using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// A fast or gripped, fresh fist contact damages BossHealth.
/// Each tracked hand owns its Rigidbody, grip input, and punch cooldown.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
[DefaultExecutionOrder(10000)]
public sealed class VRHandCombat : MonoBehaviour
{
    [Header("Punch")]
    [SerializeField, Min(1)] private int handDamage = 5;
    [SerializeField, Min(0f)] private float minimumPunchVelocity = 1f;
    [SerializeField, Min(0f)] private float punchCooldown = 0.3f;

    [Header("Tracking and Feedback")]
    [SerializeField] private Transform trackingOrigin;
    [SerializeField] private bool isLeftHand;
    [Tooltip("Haptics on the OTHER hand's controller.")]
    [SerializeField] private HapticImpulsePlayer otherHandHaptics;
    [Tooltip("The existing hand animation component supplies this hand's Grip action.")]
    [SerializeField] private animateHandInteractions handInteractions;

    private Vector3 _previousPosition;
    private float _nextPunchTime;
    private float _handSpeed;

    private void Reset()
    {
        ConfigureHitbox();
        isLeftHand = name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void Awake()
    {
        ConfigureHitbox();
        FindGripSource();
        if (trackingOrigin == null)
        {
            XROrigin origin = GetComponentInParent<XROrigin>();
            if (origin != null) trackingOrigin = origin.transform;
        }
    }

    private void OnEnable()
    {
        _previousPosition = TrackingPosition();
        _handSpeed = 0f;
        _nextPunchTime = float.NegativeInfinity;
    }

    private void OnDisable()
    {
        _handSpeed = 0f;
    }

    private void Update()
    {
        Vector3 currentPosition = TrackingPosition();
        Vector3 movement = currentPosition - _previousPosition;
        if (trackingOrigin != null) movement = trackingOrigin.TransformVector(movement);
        _handSpeed = Time.deltaTime > 0f ? movement.magnitude / Time.deltaTime : 0f;
        _previousPosition = currentPosition;
    }

    private Vector3 TrackingPosition()
    {
        return trackingOrigin != null
            ? trackingOrigin.InverseTransformPoint(transform.position)
            : transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        ApplyHandDamage(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ApplyHandDamage(collision.collider);
    }

    private void ApplyHandDamage(Collider bossCollider)
    {
        if (bossCollider == null) return;
        BossHealth boss = bossCollider.GetComponentInParent<BossHealth>();
        bool gripping = IsGripping();
        Debug.Log($"[VRHandCombat:{name}] Hand collided with: {bossCollider.gameObject.name}", this);
        Debug.Log($"[VRHandCombat:{name}] Found BossHealth: {boss != null}", this);
        Debug.Log($"[VRHandCombat:{name}] Hand velocity: {_handSpeed:F3} m/s; grip held: {gripping}", this);

        if (!isActiveAndEnabled || boss == null || boss.IsDead) return;
        // Attack-range trigger volumes belong to the boss too, but are not its body.
        if (bossCollider.isTrigger && bossCollider.GetComponent<BossDamageHitbox>() != null) return;
        if (Time.time < _nextPunchTime) return;
        // Small tolerance keeps a configured 1 m/s keyboard movement from being
        // rejected as 0.99999 due to transform/float precision.
        if (!gripping && _handSpeed + 0.01f < minimumPunchVelocity) return;

        // Consume the hand's contact even on a shielded hit. BossHealth decides
        // damage and performs its existing red/cyan flash and death events.
        _nextPunchTime = Time.time + punchCooldown;
        bool damaged = boss.TakeDamage(handDamage);
        Debug.Log($"[VRHandCombat:{name}] {(damaged ? "Punch hit" : "Punch blocked")}; boss HP: {boss.CurrentHealth}/{boss.MaxHealth}", this);
        SendOtherHandHaptics();
        PlayExistingHitSound();
    }

    private void ConfigureHitbox()
    {
        SphereCollider fist = GetComponent<SphereCollider>();
        fist.radius = 0.15f;
        fist.isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void FindGripSource()
    {
        if (handInteractions != null) return;
        handInteractions = GetComponent<animateHandInteractions>() ??
            GetComponentInParent<animateHandInteractions>(true) ??
            GetComponentInChildren<animateHandInteractions>(true);
    }

    private bool IsGripping()
    {
        if (handInteractions == null) FindGripSource();
        var action = handInteractions != null ? handInteractions.handGrip.action : null;
        // The rig owns shared action enable/disable; do not change its input lifecycle.
        return action != null && action.enabled && action.ReadValue<float>() > 0.5f;
    }

    /// <summary>Called by the existing boss setup tool; references serialize into builds.</summary>
    public void Configure(Transform origin, bool leftHand, HapticImpulsePlayer oppositeHaptics)
    {
        trackingOrigin = origin;
        isLeftHand = leftHand;
        otherHandHaptics = oppositeHaptics;
        handDamage = 5;
        minimumPunchVelocity = 1f;
        FindGripSource();
        ConfigureHitbox();
    }

    private void SendOtherHandHaptics()
    {
        if (otherHandHaptics != null && otherHandHaptics.SendHapticImpulse(0.5f, 0.15f))
            return;

        InputDevice otherDevice = InputDevices.GetDeviceAtXRNode(
            isLeftHand ? XRNode.RightHand : XRNode.LeftHand);
        if (otherDevice.isValid && otherDevice.TryGetHapticCapabilities(out HapticCapabilities capabilities) &&
            capabilities.supportsImpulse)
            otherDevice.SendHapticImpulse(0, 0.5f, 0.15f);
    }

    private static void PlayExistingHitSound()
    {
        SoundManager manager = SoundManager.Instance;
        if (manager == null || manager.sounds == null) return;
        foreach (SoundManager.Sound sound in manager.sounds)
        {
            if (sound != null && sound.clip != null &&
                (string.Equals(sound.name, "hit", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sound.name, "hurt", StringComparison.OrdinalIgnoreCase)))
            {
                manager.Play(sound.name);
                return;
            }
        }
    }
}
