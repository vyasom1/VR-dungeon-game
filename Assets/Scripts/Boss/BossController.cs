using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls boss movement, state machine, animations, telegraphing,
/// melee and ranged attacks, hitboxes, and reactions to the VR player.
/// Fully animated with Idle, Walk, Punch, Swipe, JumpAttack, Roar, and Die.
/// </summary>
public class BossController : MonoBehaviour
{
    public enum BossState
    {
        Idle,
        Moving,
        Attack,
        Stunned,
        Defeated
    }

    public enum AttackType
    {
        MeleePunch,         // Forward devastating punch
        WideSwipe,          // Sweeping claw strike
        JumpAttackSlam,     // Leaping ground slam + falling ceiling boulders
        RoarBoulderHurl     // Roar and throw energy boulder
    }

    [Header("Player Target")]
    [Tooltip("Transform of the player/VR camera. If empty, automatically finds Camera.main.")]
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private float rotationSpeed = 3.5f;
    [SerializeField] private float moveSpeed = 1.4f;
    [SerializeField] private float idealCombatDistance = 4.0f;
    [SerializeField] private float maxChaseDistance = 8.0f;

    [Header("Attack Settings")]
    [Tooltip("Prefab of projectile to shoot at player (optional).")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Transform where projectiles spawn.")]
    [SerializeField] private Transform projectileSpawnPoint;
    [Tooltip("Seconds between attacks.")]
    [SerializeField] private float attackInterval = 4.5f;
    [Tooltip("Projectile movement speed.")]
    [SerializeField] private float projectileSpeed = 4.5f;
    [Tooltip("Melee damage dealt per hit.")]
    [SerializeField] private int meleeDamage = 10;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [Tooltip("Standard melee trigger matching Attack_Melee state.")]
    [SerializeField] private string attackMeleeTrigger = "AttackMelee";
    [Tooltip("Standard throw trigger matching Attack_Throw state.")]
    [SerializeField] private string attackThrowTrigger = "AttackThrow";
    [Tooltip("Standard hurt trigger matching Hurt state.")]
    [SerializeField] private string hurtTrigger = "Hurt";
    [Tooltip("Standard death trigger matching Death state.")]
    [SerializeField] private string deathTrigger = "Death";
    [SerializeField] private string punchTrigger = "Punch";
    [SerializeField] private string swipeTrigger = "Swipe";
    [SerializeField] private string jumpAttackTrigger = "JumpAttack";
    [SerializeField] private string roarTrigger = "Roar";
    [SerializeField] private string dieTrigger = "Die";
    [SerializeField] private string movingBool = "IsMoving";
    [SerializeField] private string stunBool = "IsStunned";

    [Header("Audio")]
    [SerializeField] private string attackSoundName = "";
    [SerializeField] private string roarSoundName = "";

    private BossHealth _health;
    private BossState _currentState = BossState.Idle;
    private float _attackTimer;
    private int _attackPatternIndex = 0;
    private int _meleeAttackIndex;
    private BossDamageHitbox _meleeHitbox;

    public BossState CurrentState => _currentState;

    private void Awake()
    {
        _health = GetComponent<BossHealth>();
        if (_health == null) _health = GetComponentInParent<BossHealth>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) animator = GetComponentInParent<Animator>();
        }

        // Ensure AnimatorController and Avatar are assigned so boss never stands in T-pose
        EnsureAnimatorSetup();

        // Create or locate melee hitbox
        SetupMeleeHitbox();
    }

    private void EnsureAnimatorSetup()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null) return;
        animator.enabled = true;
        animator.speed = 1f;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // Controller/avatar references are saved by BossSetupMenu. Never substitute
        // the Mutant rig on another boss or accept a fallback Generic avatar.
        if (animator.runtimeAnimatorController == null || animator.avatar == null ||
            !animator.avatar.isValid || !animator.avatar.isHuman)
            Debug.LogError("[BossController] Invalid saved boss Animator/Avatar on " + name +
                ". Run this boss's Setup command in Edit Mode.");
    }
    public void TriggerAnimation(string paramName)
    {
        if (HasAnimatorParameter(paramName, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(paramName);
    }

    public void ConfigureRuntime(
        Animator bossAnimator,
        Transform playerTarget,
        Transform spawnPoint,
        BossDamageHitbox meleeHitbox)
    {
        if (bossAnimator != null)
            animator = bossAnimator;
        if (playerTarget != null)
            targetPlayer = playerTarget;
        if (spawnPoint != null)
            projectileSpawnPoint = spawnPoint;
        if (meleeHitbox != null)
            _meleeHitbox = meleeHitbox;

        EnsureAnimatorSetup();
        if (_meleeHitbox == null)
            SetupMeleeHitbox();
    }

    private bool HasAnimatorParameter(string paramName, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(paramName) || animator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == paramName && parameter.type == type)
                return true;
        }

        return false;
    }

    private void SetAnimatorBool(string paramName, bool value)
    {
        if (HasAnimatorParameter(paramName, AnimatorControllerParameterType.Bool))
            animator.SetBool(paramName, value);
    }

    private void SetupMeleeHitbox()
    {
        Transform existingHitbox = transform.Find("MeleeHitbox");
        GameObject hitboxObj;

        if (existingHitbox != null)
        {
            hitboxObj = existingHitbox.gameObject;
        }
        else
        {
            hitboxObj = new GameObject("MeleeHitbox");
            hitboxObj.transform.SetParent(transform);
            hitboxObj.transform.localPosition = new Vector3(0, 1.8f, 1.6f);

            BoxCollider bCol = hitboxObj.AddComponent<BoxCollider>();
            bCol.size = new Vector3(2.2f, 2.2f, 2.2f);
            bCol.isTrigger = true;
        }

        _meleeHitbox = hitboxObj.GetComponent<BossDamageHitbox>();
        if (_meleeHitbox == null)
        {
            _meleeHitbox = hitboxObj.AddComponent<BossDamageHitbox>();
        }
        _meleeHitbox.SetDamage(meleeDamage);
        _meleeHitbox.IsActive = false; // Inactive until attack active frames
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnDamaged.AddListener(OnDamaged);
            _health.OnStunned.AddListener(OnStunned);
            _health.OnInvulnerabilityRestored.AddListener(OnStunRecovered);
            _health.OnDefeated.AddListener(OnDefeated);
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDamaged.RemoveListener(OnDamaged);
            _health.OnStunned.RemoveListener(OnStunned);
            _health.OnInvulnerabilityRestored.RemoveListener(OnStunRecovered);
            _health.OnDefeated.RemoveListener(OnDefeated);
        }
    }

    private void OnDamaged()
    {
        if (_currentState == BossState.Defeated) return;
        Debug.Log("[BossController] Boss took damage -> Triggering Hurt animation!");
        TriggerAnimation(hurtTrigger);
    }

    private void Start()
    {
        if (targetPlayer == null && Camera.main != null)
        {
            targetPlayer = Camera.main.transform;
        }

        _attackTimer = attackInterval * 0.6f; // Initial attack slightly faster
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            SetMovingAnimation(false);
            SetAnimatorBool(stunBool, false);
            animator.Play("Idle", 0, 0f);
        }
    }

    private void Update()
    {
        if (_currentState == BossState.Defeated) return;

        // Auto-find player if not assigned yet
        if (targetPlayer == null && Camera.main != null)
        {
            targetPlayer = Camera.main.transform;
        }

        if (_currentState == BossState.Stunned)
        {
            SetMovingAnimation(false);
            return;
        }

        // Always face player
        FacePlayer();

        if (_currentState == BossState.Attack)
        {
            // Do not move or start another attack during active attack sequence
            return;
        }

        if (targetPlayer == null) return;
        Vector3 planarOffset = targetPlayer.position - transform.position;
        planarOffset.y = 0f;
        float distanceToPlayer = planarOffset.magnitude;

        // Check if boss should reposition/advance toward player
        // Seven metres is inside the old eight-metre chase threshold, leaving
        // the boss stationary forever. Approach until the melee hitbox can reach.
        if (distanceToPlayer > Mathf.Min(idealCombatDistance, 2.6f))
        {
            AdvanceTowardPlayer();
        }
        else
        {
            if (_currentState == BossState.Moving)
            {
                _currentState = BossState.Idle;
                SetMovingAnimation(false);
            }

        }

        // Cooldown also advances while chasing, so distant players still get
        // ranged attacks while the boss closes the gap.
        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            _attackTimer = attackInterval;
            ExecuteNextAttack(distanceToPlayer);
        }
    }

    private void FacePlayer()
    {
        if (targetPlayer == null) return;

        Vector3 targetDirection = targetPlayer.position - transform.position;
        targetDirection.y = 0; // Keep upright

        if (targetDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private void AdvanceTowardPlayer()
    {
        _currentState = BossState.Moving;
        SetMovingAnimation(true);

        Vector3 moveDir = (targetPlayer.position - transform.position);
        moveDir.y = 0;
        moveDir.Normalize();

        transform.position += moveDir * (moveSpeed * Time.deltaTime);
    }

    private void SetMovingAnimation(bool isMoving)
    {
        SetAnimatorBool(movingBool, isMoving);
    }

    private void ExecuteNextAttack(float distanceToPlayer)
    {
        _currentState = BossState.Attack;
        SetMovingAnimation(false);

        // Select attack based on distance and rotation
        AttackType attack;

        if (distanceToPlayer <= idealCombatDistance)
        {
            // Close range: alternate Punch and Swipe
            attack = (_meleeAttackIndex++ % 2 == 0) ? AttackType.MeleePunch : AttackType.WideSwipe;
        }
        else
        {
            // Medium/Long range: alternate Jump Attack Slam and Roar Boulder Hurl
            attack = (_attackPatternIndex % 2 == 0) ? AttackType.RoarBoulderHurl : AttackType.JumpAttackSlam;
        }

        _attackPatternIndex++;

        switch (attack)
        {
            case AttackType.MeleePunch:
                StartCoroutine(PunchRoutine());
                break;

            case AttackType.WideSwipe:
                StartCoroutine(SwipeRoutine());
                break;

            case AttackType.JumpAttackSlam:
                StartCoroutine(JumpAttackRoutine());
                break;

            case AttackType.RoarBoulderHurl:
                StartCoroutine(RoarAndBoulderRoutine());
                break;
        }
    }

    // ==========================================
    // ATTACK 1: MELEE PUNCH (Telegraphed Heavy Strike)
    // ==========================================
    private IEnumerator PunchRoutine()
    {
        Debug.Log("[BossController] Boss winding up HEAVY PUNCH!");

        // Telegraph: Flash eyes / wind up sound
        PlaySound(attackSoundName);

        // Trigger animations
        TriggerAnimation(punchTrigger);

        // Windup phase (waiting for fist to extend)
        yield return new WaitForSeconds(0.45f);

        // Active damage hitbox window
        if (_meleeHitbox != null) _meleeHitbox.IsActive = true;

        yield return new WaitForSeconds(0.4f);

        // Recovery phase: deactivate hitbox
        if (_meleeHitbox != null) _meleeHitbox.IsActive = false;

        yield return new WaitForSeconds(0.6f);

        ResetToIdle();
    }

    // ==========================================
    // ATTACK 2: WIDE SWIPE (Frontal Arc Claw Strike)
    // ==========================================
    private IEnumerator SwipeRoutine()
    {
        Debug.Log("[BossController] Boss winding up WIDE SWIPE!");

        PlaySound(attackSoundName);

        TriggerAnimation(swipeTrigger);

        yield return new WaitForSeconds(0.4f);

        if (_meleeHitbox != null) _meleeHitbox.IsActive = true;

        yield return new WaitForSeconds(0.45f);

        if (_meleeHitbox != null) _meleeHitbox.IsActive = false;

        yield return new WaitForSeconds(0.5f);

        ResetToIdle();
    }

    // ==========================================
    // ATTACK 3: JUMP ATTACK SLAM & FALLING CEILING BOULDERS
    // ==========================================
    private IEnumerator JumpAttackRoutine()
    {
        Debug.Log("[BossController] Boss preparing JUMP ATTACK & GROUND SLAM!");

        PlaySound(roarSoundName);

        TriggerAnimation(jumpAttackTrigger);

        // Coiling legs windup
        yield return new WaitForSeconds(0.5f);

        // Leap forward slightly
        if (targetPlayer != null)
        {
            Vector3 leapDir = (targetPlayer.position - transform.position);
            leapDir.y = 0;
            leapDir.Normalize();
            transform.position += leapDir * 1.5f;
        }

        // Impact on floor: spawn expanding shockwave and drop falling ceiling boulders
        yield return new WaitForSeconds(0.5f);

        SpawnGroundShockwave();
        SpawnFallingBouldersNearPlayer();

        yield return new WaitForSeconds(1.0f);

        ResetToIdle();
    }

    private void SpawnGroundShockwave()
    {
        GameObject shockwave = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shockwave.name = "GroundShockwaveRing";
        shockwave.transform.position = transform.position + new Vector3(0, 0.05f, 0);
        shockwave.transform.localScale = new Vector3(1f, 0.05f, 1f);

        Collider col = shockwave.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Renderer rend = shockwave.GetComponent<Renderer>();
        if (rend != null) rend.material.color = new Color(1f, 0.3f, 0.1f, 0.7f);

        BossDamageHitbox hitbox = shockwave.AddComponent<BossDamageHitbox>();
        hitbox.SetDamage(10);
        hitbox.IsActive = true;

        StartCoroutine(ExpandShockwaveRoutine(shockwave));
    }

    private IEnumerator ExpandShockwaveRoutine(GameObject shockwave)
    {
        float duration = 1.2f;
        float elapsed = 0f;
        Vector3 initialScale = new Vector3(1f, 0.05f, 1f);
        Vector3 maxScale = new Vector3(12f, 0.05f, 12f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (shockwave != null)
            {
                shockwave.transform.localScale = Vector3.Lerp(initialScale, maxScale, elapsed / duration);
            }
            yield return null;
        }

        if (shockwave != null) Destroy(shockwave);
    }

    private void SpawnFallingBouldersNearPlayer()
    {
        if (targetPlayer == null) return;

        Vector3 pPos = targetPlayer.position;
        Vector3[] offsets = new Vector3[]
        {
            new Vector3(-1.8f, 0, 0.8f),
            new Vector3(1.8f, 0, -0.8f)
        };

        foreach (var off in offsets)
        {
            Vector3 groundTarget = pPos + off;
            groundTarget.y = 0.05f;

            // Warning red decal
            GameObject warning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            warning.name = "BoulderWarning";
            warning.transform.position = groundTarget;
            warning.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
            Collider wCol = warning.GetComponent<Collider>();
            if (wCol != null) Destroy(wCol);
            Renderer wRend = warning.GetComponent<Renderer>();
            if (wRend != null) wRend.material.color = new Color(1f, 0f, 0f, 0.5f);

            StartCoroutine(DropBoulderAfterDelay(groundTarget + Vector3.up * 6f, warning));
        }
    }

    private IEnumerator DropBoulderAfterDelay(Vector3 skyPos, GameObject warningObj)
    {
        yield return new WaitForSeconds(1.1f);
        if (warningObj != null) Destroy(warningObj);

        GameObject boulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        boulder.name = "Falling_Boulder";
        boulder.transform.position = skyPos;
        boulder.transform.localScale = Vector3.one * 0.6f;

        Renderer rend = boulder.GetComponent<Renderer>();
        if (rend != null) rend.material.color = new Color(0.35f, 0.3f, 0.25f);

        Rigidbody rb = boulder.AddComponent<Rigidbody>();
        rb.mass = 4f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Damage hitbox on falling debris
        BossDamageHitbox hitbox = boulder.AddComponent<BossDamageHitbox>();
        hitbox.SetDamage(10);
        hitbox.IsActive = true;

        // Player can pick it up and throw back at boss!
        if (boulder.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() == null)
            boulder.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        boulder.AddComponent<ThrowableBossDamage>();

        Destroy(boulder, 15f);
    }

    // ==========================================
    // ATTACK 4: ROAR & ENERGY BOULDER HURL (Ranged Attack)
    // ==========================================
    private IEnumerator RoarAndBoulderRoutine()
    {
        Debug.Log("[BossController] Boss ROARING and launching ENERGY BOULDER!");

        PlaySound(roarSoundName);

        TriggerAnimation(attackThrowTrigger);

        // Roar windup duration
        yield return new WaitForSeconds(0.7f);

        // Launch projectile towards VR Player
        if (targetPlayer != null)
        {
            Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + Vector3.up * 2.2f;
            Vector3 direction = (targetPlayer.position - spawnPos).normalized;

            GameObject proj;
            if (projectilePrefab != null)
            {
                proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                proj.name = "Boss_EnergyBoulder";
                proj.transform.position = spawnPos;
                proj.transform.localScale = Vector3.one * 0.55f;

                Collider col = proj.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                Renderer rend = proj.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(1f, 0.35f, 0.05f); // Fiery orange/magma
            }

            BossProjectile pComp = proj.GetComponent<BossProjectile>();
            if (pComp == null) pComp = proj.AddComponent<BossProjectile>();
            pComp.Launch(direction, projectileSpeed);
        }

        yield return new WaitForSeconds(0.8f);

        ResetToIdle();
    }

    private void ResetToIdle()
    {
        if (_currentState == BossState.Defeated) return;
        _currentState = BossState.Idle;
    }

    private void PlaySound(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(soundName);
        }
    }

    private void OnStunned()
    {
        _currentState = BossState.Stunned;
        SetMovingAnimation(false);
        if (_meleeHitbox != null) _meleeHitbox.IsActive = false;

        SetAnimatorBool(stunBool, true);
    }

    private void OnStunRecovered()
    {
        if (_currentState == BossState.Defeated) return;

        _currentState = BossState.Idle;
        _attackTimer = attackInterval * 0.75f;

        SetAnimatorBool(stunBool, false);
    }

    private void OnDefeated()
    {
        _currentState = BossState.Defeated;
        SetMovingAnimation(false);
        if (_meleeHitbox != null) _meleeHitbox.IsActive = false;
        StopAllCoroutines();

        Debug.Log("[BossController] Boss defeated -> Playing Death animation, stopping attacks and disabling AI!");
        TriggerAnimation(deathTrigger);

        this.enabled = false;
    }
}

