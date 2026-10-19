using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages boss health, phases, invulnerability states, and damage feedback.
/// Designed for VR puzzle-boss encounters.
/// </summary>
public class BossHealth : MonoBehaviour
{
    [Header("Health & Phases")]
    [Tooltip("Total phases or health hits required to defeat the boss (e.g., 3 hits/phases).")]
    [SerializeField] private int maxHealth = 3;
    [Tooltip("If true, the boss is invulnerable until a puzzle condition unlocks vulnerability.")]
    [SerializeField] private bool isInvulnerable = false;

    [Header("Visual Feedback")]
    [Tooltip("Renderers to flash when the boss takes damage.")]
    [SerializeField] private Renderer[] meshRenderers;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float flashDuration = 0.2f;

    [Header("Audio Feedback")]
    [Tooltip("Sound name registered in SoundManager when damaged.")]
    [SerializeField] private string hurtSoundName;
    [Tooltip("Sound name registered in SoundManager when stunned/exposed.")]
    [SerializeField] private string stunSoundName;
    [Tooltip("Sound name registered in SoundManager when defeated.")]
    [SerializeField] private string defeatSoundName;

    [Header("Events")]
    public UnityEvent<int, int> OnHealthChanged = new UnityEvent<int, int>();
    public UnityEvent OnDamaged = new UnityEvent();
    public UnityEvent OnStunned = new UnityEvent();
    public UnityEvent OnVulnerabilityOpened = new UnityEvent();
    public UnityEvent OnInvulnerabilityRestored = new UnityEvent();
    public UnityEvent OnDefeated = new UnityEvent();

    private int _currentHealth;
    private bool _isDead;
    private Coroutine _flashRoutine;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsInvulnerable => isInvulnerable;
    public bool IsDead => _isDead;

    private void Awake()
    {
        // Bosses begin vulnerable. Puzzle sockets may still call SetVulnerable later
        // to add an optional shield or stun phase.
        isInvulnerable = false;
        _currentHealth = maxHealth;
        if (meshRenderers == null || meshRenderers.Length == 0)
        {
            meshRenderers = GetComponentsInChildren<Renderer>();
        }
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>
    /// Makes the boss vulnerable so attacks or thrown objects can deal damage.
    /// </summary>
    public void SetVulnerable(bool vulnerable)
    {
        if (_isDead) return;

        isInvulnerable = !vulnerable;
        if (vulnerable)
        {
            Debug.Log($"[BossHealth] {gameObject.name} is now VULNERABLE!");
            PlaySound(stunSoundName);
            OnVulnerabilityOpened?.Invoke();
            OnStunned?.Invoke();
        }
        else
        {
            Debug.Log($"[BossHealth] {gameObject.name} has restored its SHIELD/INVULNERABILITY.");
            OnInvulnerabilityRestored?.Invoke();
        }
    }

    /// <summary>
    /// Stuns the boss and opens vulnerability for a limited duration before shielding up again.
    /// </summary>
    public void StunForSeconds(float duration)
    {
        if (_isDead) return;
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        SetVulnerable(true);
        yield return new WaitForSeconds(duration);
        if (!_isDead)
        {
            SetVulnerable(false);
        }
    }

    /// <summary>
    /// Deals damage to the boss if not invulnerable.
    /// </summary>
    public bool TakeDamage(int amount = 1)
    {
        if (_isDead) return false;

        if (isInvulnerable)
        {
            Debug.Log($"[BossHealth] {gameObject.name} is INVULNERABLE! Solve the room puzzle to expose it!");
            FlashColor(Color.cyan);
            return false;
        }

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        Debug.Log($"[BossHealth] {gameObject.name} took {amount} damage! Health remaining: {_currentHealth}/{maxHealth}");

        FlashColor(damageFlashColor);
        PlaySound(hurtSoundName);

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnDamaged?.Invoke();

        if (_currentHealth <= 0)
        {
            Die();
        }
        return true;
    }

    /// <summary>
    /// Forces damage even if invulnerable (e.g. from a room trap mechanism).
    /// </summary>
    public void ForceDamage(int amount = 1)
    {
        bool wasInvulnerable = isInvulnerable;
        isInvulnerable = false;
        TakeDamage(amount);
        if (!_isDead && wasInvulnerable)
        {
            isInvulnerable = true;
        }
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;
        Debug.Log($"[BossHealth] {gameObject.name} DEFEATED!");
        PlaySound(defeatSoundName);
        OnDefeated?.Invoke();
    }

    private void FlashColor(Color color)
    {
        if (meshRenderers == null || meshRenderers.Length == 0) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashColorRoutine(color));
    }

    private IEnumerator FlashColorRoutine(Color flashColor)
    {
        // Simple color flash support
        foreach (var r in meshRenderers)
        {
            if (r != null && r.material.HasProperty("_BaseColor"))
                r.material.color = flashColor;
        }

        yield return new WaitForSeconds(flashDuration);

        foreach (var r in meshRenderers)
        {
            if (r != null && r.material.HasProperty("_BaseColor"))
                r.material.color = Color.white;
        }
    }

    private void PlaySound(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(soundName);
        }
    }
}
