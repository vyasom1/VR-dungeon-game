using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach to any XRGrabInteractable object (bombs, crystals, rocks, spears) 
/// so when thrown by the VR player, it deals damage or stuns the boss.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class ThrowableBossDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Amount of damage dealt to the boss upon impact.")]
    [SerializeField] private int damageAmount = 1;
    [Tooltip("Minimum throw velocity required to deal damage.")]
    [SerializeField] private float minimumImpactVelocity = 1.5f;
    [Tooltip("Does hitting the boss stun it instead of / in addition to dealing damage?")]
    [SerializeField] private bool stunOnHit = false;
    [SerializeField] private float stunDuration = 5f;

    [Header("After Impact")]
    [Tooltip("If true, object respawns at original position after impact.")]
    [SerializeField] private bool respawnAfterHit = true;
    [SerializeField] private float respawnDelay = 2f;
    [Tooltip("If true, destroy this GameObject after hitting the boss.")]
    [SerializeField] private bool destroyOnHit = false;

    [Header("Effects & Audio")]
    [SerializeField] private GameObject hitImpactEffect;
    [SerializeField] private string hitSoundName;

    private XRGrabInteractable _grabInteractable;
    private Rigidbody _rigidbody;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private bool _isHeld = false;
    private bool _isArmed = false;
    private XRBaseController _lastHoldingController;

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _rigidbody = GetComponent<Rigidbody>();
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
    }

    private void OnEnable()
    {
        _grabInteractable.selectEntered.AddListener(OnGrabbed);
        _grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        _grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _isHeld = true;
        _isArmed = false;
        if (args.interactorObject != null)
        {
            _lastHoldingController = args.interactorObject.transform
                .GetComponentInParent<XRBaseController>();
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        _isHeld = false;
        // Arm the weapon when released by the player
        _isArmed = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_isArmed || _isHeld) return;

        // Check if collision velocity is sufficient
        if (collision.relativeVelocity.magnitude < minimumImpactVelocity) return;

        // Check for specific weak point first
        BossWeakPoint weakPoint = collision.gameObject.GetComponent<BossWeakPoint>();
        if (weakPoint != null)
        {
            ApplyWeakPointHit(weakPoint);
            return;
        }

        BossHealth boss = collision.gameObject.GetComponentInParent<BossHealth>();
        if (boss != null && !boss.IsDead)
        {
            ApplyHit(boss);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isArmed || _isHeld) return;

        BossWeakPoint weakPoint = other.GetComponent<BossWeakPoint>();
        if (weakPoint != null)
        {
            ApplyWeakPointHit(weakPoint);
            return;
        }

        BossHealth boss = other.GetComponentInParent<BossHealth>();
        if (boss != null && !boss.IsDead)
        {
            ApplyHit(boss);
        }
    }

    private void ApplyWeakPointHit(BossWeakPoint weakPoint)
    {
        _isArmed = false;
        if (hitImpactEffect != null) Instantiate(hitImpactEffect, transform.position, Quaternion.identity);
        if (SoundManager.Instance != null && !string.IsNullOrEmpty(hitSoundName)) SoundManager.Instance.Play(hitSoundName);
        weakPoint.OnHitByWeapon(damageAmount);
        SendImpactHaptics();
        HandlePostHit();
    }

    private void ApplyHit(BossHealth boss)
    {
        _isArmed = false;

        // Spawn hit effect
        if (hitImpactEffect != null)
        {
            Instantiate(hitImpactEffect, transform.position, Quaternion.identity);
        }

        if (SoundManager.Instance != null && !string.IsNullOrEmpty(hitSoundName))
        {
            SoundManager.Instance.Play(hitSoundName);
        }

        if (stunOnHit)
        {
            boss.StunForSeconds(stunDuration);
        }
        else
        {
            boss.TakeDamage(damageAmount);
        }

        SendImpactHaptics();
        HandlePostHit();
    }

    private void SendImpactHaptics()
    {
        if (_lastHoldingController != null)
            _lastHoldingController.SendHapticImpulse(0.45f, 0.18f);
    }

    private void HandlePostHit()
    {
        if (destroyOnHit)
        {
            Destroy(gameObject, 0.1f);
        }
        else if (respawnAfterHit)
        {
            StartCoroutine(RespawnRoutine());
        }
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        transform.position = _originalPosition;
        transform.rotation = _originalRotation;
        _isArmed = false;
    }
}
