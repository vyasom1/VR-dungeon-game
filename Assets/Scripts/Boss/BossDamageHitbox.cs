using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to boss attack hitboxes (fists, claws, shockwaves, hazard debris, beams)
/// to apply damage to the player only when active and connected.
/// Prevents multiple hits in a single swing.
/// </summary>
public class BossDamageHitbox : MonoBehaviour
{
    [Tooltip("Damage dealt to the player on a valid hit.")]
    [SerializeField] private int damageAmount = 10;
    [Tooltip("If true, this hitbox is currently active and can deal damage.")]
    [SerializeField] private bool isActive = true;
    [Tooltip("Automatically disable this hitbox after hitting the player once during an attack.")]
    [SerializeField] private bool deactivateOnHit = false;

    private readonly HashSet<PlayerHealth> _alreadyHit = new HashSet<PlayerHealth>();

    public bool IsActive
    {
        get => isActive;
        set
        {
            isActive = value;
            if (isActive)
            {
                _alreadyHit.Clear();
            }
        }
    }

    public void SetDamage(int dmg) => damageAmount = dmg;

    private void OnEnable()
    {
        _alreadyHit.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckAndApplyDamage(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        // The player may already overlap this trigger when the active swing
        // window begins. The per-swing set still allows only one successful hit.
        CheckAndApplyDamage(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckAndApplyDamage(collision.gameObject);
    }

    private void CheckAndApplyDamage(GameObject hitObj)
    {
        if (!isActive) return;

        // Ignore boss self
        if (hitObj.GetComponentInParent<BossHealth>() != null) return;

        PlayerHealth player = hitObj.GetComponentInParent<PlayerHealth>();
        if (player == null)
        {
            player = hitObj.GetComponent<PlayerHealth>();
        }

        // If not directly found on collider, check if it's the VR player camera or rig
        if (player == null && PlayerHealth.Instance != null)
        {
            if (hitObj.CompareTag("Player") || hitObj.CompareTag("MainCamera") ||
                hitObj.GetComponentInChildren<Camera>() != null ||
                hitObj.GetComponentInParent<CharacterController>() != null ||
                hitObj.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null)
            {
                player = PlayerHealth.Instance;
            }
        }

        if (player != null && !player.IsDead && !_alreadyHit.Contains(player))
        {
            bool damaged = player.TakeDamage(damageAmount);
            if (damaged)
            {
                _alreadyHit.Add(player);

                if (deactivateOnHit)
                {
                    isActive = false;
                }
            }
        }
    }
}
