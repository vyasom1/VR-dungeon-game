using UnityEngine;

/// <summary>
/// Projectile launched by the boss towards the VR player.
/// Can be physically dodged, blocked, or destroyed on impact.
/// </summary>
public class BossProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 4f;
    [SerializeField] private float lifetime = 8f;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private string hitSoundName;

    [SerializeField] private int damage = 10;

    private Vector3 _moveDirection;
    private bool _hasLaunched = false;
    private bool _hasExploded = false;
    private Rigidbody _body;

    public void Launch(Vector3 direction, float launchSpeed)
    {
        _moveDirection = direction.normalized;
        speed = launchSpeed;
        _hasLaunched = true;
        _body = GetComponent<Rigidbody>();
        if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
        _body.isKinematic = true;
        _body.useGravity = false;
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (!_hasLaunched) return;
        _body.MovePosition(_body.position + _moveDirection * (speed * Time.fixedDeltaTime));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasExploded) return;
        if (other.GetComponentInParent<BossHealth>() != null) return;

        CheckPlayerDamage(other.gameObject);
        Explode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasExploded) return;
        if (collision.gameObject.GetComponentInParent<BossHealth>() != null) return;

        CheckPlayerDamage(collision.gameObject);
        Explode();
    }

    private void CheckPlayerDamage(GameObject hitObj)
    {
        PlayerHealth player = hitObj.GetComponentInParent<PlayerHealth>() ?? hitObj.GetComponent<PlayerHealth>();
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

        if (player != null && !player.IsDead)
        {
            player.TakeDamage(damage);
        }
    }

    private void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        if (SoundManager.Instance != null && !string.IsNullOrEmpty(hitSoundName))
        {
            SoundManager.Instance.Play(hitSoundName);
        }

        Destroy(gameObject);
    }
}
