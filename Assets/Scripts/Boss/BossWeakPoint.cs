using UnityEngine;

/// <summary>
/// Place on a specific child collider (such as a glowing chest crystal or head) 
/// so hitting this specific spot registers as boss damage.
/// </summary>
public class BossWeakPoint : MonoBehaviour
{
    [SerializeField] private BossHealth parentBossHealth;
    [SerializeField] private int damageMultiplier = 1;
    [SerializeField] private GameObject hitEffect;

    private void Awake()
    {
        if (parentBossHealth == null)
            parentBossHealth = GetComponentInParent<BossHealth>();
    }

    public void OnHitByWeapon(int baseDamage)
    {
        if (parentBossHealth == null) return;

        if (hitEffect != null)
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }

        parentBossHealth.TakeDamage(baseDamage * damageMultiplier);
    }
}
