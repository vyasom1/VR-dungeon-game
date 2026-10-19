using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Observes valid fist contacts for sparks, critical callouts, and combo presentation.
/// VRHandCombat remains the only component that applies boss damage.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(9999)]
public sealed class HandCombatPolish : MonoBehaviour
{
    private Transform _trackingOrigin;
    private animateHandInteractions _handInput;
    private BossController _bossController;
    private Vector3 _previousPosition;
    private float _speed;
    private bool _wasPunchSpeed;
    private bool _leftHand;

    public void Initialize()
    {
        _previousPosition = TrackingPosition();
        _leftHand = name.ToLowerInvariant().Contains("left");
        _trackingOrigin = GetComponentInParent<Unity.XR.CoreUtils.XROrigin>(true)?.transform;
        _handInput = GetComponent<animateHandInteractions>() ??
                     GetComponentInParent<animateHandInteractions>(true) ??
                     GetComponentInChildren<animateHandInteractions>(true);
        BossHealth[] bosses = FindObjectsByType<BossHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (BossHealth health in bosses)
        {
            if (health.gameObject.scene != gameObject.scene) continue;
            _bossController = health.GetComponent<BossController>();
            if (_bossController != null) break;
        }
    }

    private void OnEnable()
    {
        _previousPosition = TrackingPosition();
        _speed = 0f;
        _wasPunchSpeed = false;
    }

    private void Update()
    {
        Vector3 current = TrackingPosition();
        Vector3 movement = current - _previousPosition;
        if (_trackingOrigin != null) movement = _trackingOrigin.TransformVector(movement);
        _speed = Time.deltaTime > 0f ? movement.magnitude / Time.deltaTime : 0f;
        _previousPosition = current;

        bool punching = _speed >= 1f;
        if (punching && !_wasPunchSpeed)
            BossFightPolish.RegisterSwingMiss(Time.frameCount);
        _wasPunchSpeed = punching;
    }

    private Vector3 TrackingPosition() => _trackingOrigin != null
        ? _trackingOrigin.InverseTransformPoint(transform.position) : transform.position;

    private void OnTriggerEnter(Collider other) => ObserveContact(other);

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null) ObserveContact(collision.collider);
    }

    private void ObserveContact(Collider other)
    {
        if (other == null) return;
        BossHealth health = other.GetComponentInParent<BossHealth>();
        if (health == null || health.IsDead) return;
        BossDamageHitbox hitbox = other.GetComponent<BossDamageHitbox>();
        if (other.isTrigger && hitbox != null) return;

        InputAction gripAction = _handInput != null ? _handInput.handGrip.action : null;
        bool gripping = gripAction != null && gripAction.enabled && gripAction.ReadValue<float>() > 0.5f;
        if (!gripping && _speed < 0.99f) return;

        bool critical = _bossController != null && _bossController.CurrentState == BossController.BossState.Attack;
        Vector3 point = other.ClosestPoint(transform.position);
        if ((point - transform.position).sqrMagnitude < 0.0001f) point = transform.position;
        BossFightPolish.RegisterPunchContactFromHand(health, point, critical);
    }
}


