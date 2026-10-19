using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

/// <summary>
/// Manages player health, post-hit invulnerability window,
/// VR damage feedback, HUD display, death sequence, and automatic level restart.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Health Settings")]
    [Tooltip("Maximum player health. Default: 100")]
    [SerializeField] private int maxHealth = 100;
    [Tooltip("Damage dealt per valid boss hit. Default: 10")]
    [SerializeField] private int damagePerHit = 10;
    [Tooltip("Post-hit invulnerability period (seconds) to prevent accidental instant deaths from overlapping collisions.")]
    [SerializeField] private float invulnerabilityDuration = 0.8f;

    [Header("Audio (Optional)")]
    [SerializeField] private string hurtSoundName = "";
    [SerializeField] private string deathSoundName = "";

    [Header("Events")]
    public UnityEvent<int, int> OnHealthChanged; // (currentHealth, maxHealth)
    public UnityEvent OnPlayerDied;

    private int _currentHealth;
    private bool _isDead = false;
    private bool _isInvulnerable = false;
    private float _invulnerableTimer = 0f;

    // VR UI Elements
    private GameObject _hudCanvasObj;
    private Slider _healthSlider;
    private TextMeshProUGUI _healthText;
    private Image _damageFlashVignette;

    // Death UI Elements
    private GameObject _deathScreenObj;
    private TextMeshProUGUI _deathTitleText;
    private TextMeshProUGUI _deathSubtitleText;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public int DamagePerHit => damagePerHit;
    public bool IsDead => _isDead;
    public bool IsInvulnerable => _isInvulnerable;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePlayerHealthExists()
    {
        if (Instance != null) return;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            GameObject playerRoot = mainCam.transform.root.gameObject;
            if (playerRoot.GetComponent<PlayerHealth>() == null)
            {
                playerRoot.AddComponent<PlayerHealth>();
                Debug.Log("[PlayerHealth] Automatically attached PlayerHealth to " + playerRoot.name);
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _currentHealth = maxHealth;
        _isDead = false;
        _isInvulnerable = false;

        // Ensure player root or camera has a tag or collider
        if (!gameObject.CompareTag("Player"))
        {
            gameObject.tag = "Player";
        }
    }

    private void Start()
    {
        CreateVRHealthHUD();
        CreateDeathScreenUI();
        UpdateHUD();
    }

    private void Update()
    {
        if (_isInvulnerable)
        {
            _invulnerableTimer -= Time.deltaTime;
            if (_invulnerableTimer <= 0f)
            {
                _isInvulnerable = false;
                if (_damageFlashVignette != null)
                {
                    _damageFlashVignette.color = new Color(1f, 0f, 0f, 0f);
                }
            }
        }

        // Position HUD in VR view (comfortably anchored to camera)
        PositionVRUI();
    }

    /// <summary>
    /// Applies damage to the player if not dead or in post-hit invulnerability.
    /// </summary>
    public bool TakeDamage(int amount = -1)
    {
        if (_isDead) return false;
        if (_isInvulnerable) return false;

        int finalDamage = (amount > 0) ? amount : damagePerHit;

        _currentHealth = Mathf.Max(0, _currentHealth - finalDamage);
        _isInvulnerable = true;
        _invulnerableTimer = invulnerabilityDuration;

        SendDamageHaptics();

        Debug.Log($"[PlayerHealth] Player took {finalDamage} damage! Health: {_currentHealth}/{maxHealth}");

        // Flash red vignette
        if (_damageFlashVignette != null)
        {
            _damageFlashVignette.color = new Color(0.8f, 0f, 0f, 0.45f);
        }

        // Play audio if available
        if (!string.IsNullOrEmpty(hurtSoundName) && SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(hurtSoundName);
        }

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        UpdateHUD();

        if (_currentHealth <= 0)
        {
            Die();
        }

        return true;
    }

    private void SendDamageHaptics()
    {
        XRBaseController[] controllers = GetComponentsInChildren<XRBaseController>(true);
        foreach (XRBaseController controller in controllers)
        {
            if (controller != null)
                controller.SendHapticImpulse(0.6f, 0.3f);
        }
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        Debug.Log("[PlayerHealth] Player has DIED! Displaying 'You Are Dead' screen and restarting current level...");

        // Play death sound
        if (!string.IsNullOrEmpty(deathSoundName) && SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(deathSoundName);
        }

        OnPlayerDied?.Invoke();

        // 1. Disable player movement controls & locomoation
        DisablePlayerControls();

        // 2. Hide normal HUD, show Death Overlay
        if (_hudCanvasObj != null) _hudCanvasObj.SetActive(false);
        if (_deathScreenObj != null) _deathScreenObj.SetActive(true);

        // 3. Restart level after ~2.5 seconds (using unscaled time so it works even if timeScale is modified)
        StartCoroutine(RestartLevelRoutine(2.5f));
    }

    private void DisablePlayerControls()
    {
        // Disable CharacterController if present
        CharacterController cc = GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Disable XR movement / locomotion scripts
        MonoBehaviour[] scripts = GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null || script == this) continue;
            string typeName = script.GetType().Name;
            if (typeName.Contains("Locomotion") ||
                typeName.Contains("MoveProvider") ||
                typeName.Contains("TurnProvider") ||
                typeName.Contains("TeleportationProvider"))
            {
                script.enabled = false;
            }
        }
    }

    private IEnumerator RestartLevelRoutine(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);

        Scene currentScene = SceneManager.GetActiveScene();
        Debug.Log($"[PlayerHealth] Restarting current level: {currentScene.name}");
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private void UpdateHUD()
    {
        if (_healthSlider != null)
        {
            _healthSlider.maxValue = maxHealth;
            _healthSlider.value = _currentHealth;
        }

        if (_healthText != null)
        {
            _healthText.text = $"HP: {_currentHealth} / {maxHealth}";
        }
    }

    private void PositionVRUI()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Position HUD in lower peripheral view
        if (_hudCanvasObj != null && !_isDead)
        {
            Transform camT = cam.transform;
            Vector3 targetPos = camT.position + (camT.forward * 1.4f) + (camT.up * -0.45f) + (camT.right * -0.4f);
            _hudCanvasObj.transform.position = Vector3.Lerp(_hudCanvasObj.transform.position, targetPos, Time.deltaTime * 10f);
            _hudCanvasObj.transform.rotation = Quaternion.LookRotation(_hudCanvasObj.transform.position - camT.position);
        }

        // Position Death screen right in front of player
        if (_deathScreenObj != null && _isDead)
        {
            Transform camT = cam.transform;
            Vector3 targetPos = camT.position + (camT.forward * 1.5f);
            _deathScreenObj.transform.position = targetPos;
            _deathScreenObj.transform.rotation = Quaternion.LookRotation(_deathScreenObj.transform.position - camT.position);
        }
    }

    // ==========================================
    // UI CREATION HELPERS (Zero Dependencies)
    // ==========================================

    private void CreateVRHealthHUD()
    {
        _hudCanvasObj = new GameObject("Player_Health_HUD");
        Canvas canvas = _hudCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _hudCanvasObj.AddComponent<CanvasScaler>();

        RectTransform rt = _hudCanvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(320, 90);
        rt.localScale = Vector3.one * 0.0018f;

        // Background panel
        GameObject bgPanel = new GameObject("Background");
        bgPanel.transform.SetParent(_hudCanvasObj.transform, false);
        Image bgImg = bgPanel.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.75f);
        RectTransform bgRT = bgPanel.GetComponent<RectTransform>();
        bgRT.sizeDelta = new Vector2(320, 90);

        // Health Slider
        GameObject sliderObj = new GameObject("HealthSlider");
        sliderObj.transform.SetParent(_hudCanvasObj.transform, false);
        _healthSlider = sliderObj.AddComponent<Slider>();
        RectTransform sliderRT = sliderObj.GetComponent<RectTransform>();
        sliderRT.sizeDelta = new Vector2(280, 24);
        sliderRT.anchoredPosition = new Vector2(0, 10);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1, 0.75f);
        fillAreaRT.sizeDelta = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.15f, 0.85f, 0.25f); // Vibrant Green
        RectTransform fillRT = fillObj.GetComponent<RectTransform>();
        fillRT.sizeDelta = Vector2.zero;

        _healthSlider.targetGraphic = fillImg;
        _healthSlider.fillRect = fillRT;
        _healthSlider.minValue = 0;
        _healthSlider.maxValue = maxHealth;
        _healthSlider.value = _currentHealth;

        // Health Text
        GameObject textObj = new GameObject("HealthText");
        textObj.transform.SetParent(_hudCanvasObj.transform, false);
        _healthText = textObj.AddComponent<TextMeshProUGUI>();
        _healthText.text = $"HP: {maxHealth} / {maxHealth}";
        _healthText.fontSize = 20;
        _healthText.fontStyle = FontStyles.Bold;
        _healthText.alignment = TextAlignmentOptions.Center;
        _healthText.color = Color.white;
        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.sizeDelta = new Vector2(280, 30);
        textRT.anchoredPosition = new Vector2(0, -20);

        // Red screen flash vignette (attached to camera)
        Camera cam = Camera.main;
        if (cam != null)
        {
            GameObject vignetteObj = new GameObject("DamageFlashVignette");
            vignetteObj.transform.SetParent(cam.transform, false);
            vignetteObj.transform.localPosition = new Vector3(0, 0, 0.35f);
            Canvas vCanvas = vignetteObj.AddComponent<Canvas>();
            vCanvas.renderMode = RenderMode.WorldSpace;
            RectTransform vRT = vignetteObj.GetComponent<RectTransform>();
            vRT.sizeDelta = new Vector2(100, 100);
            vRT.localScale = Vector3.one * 0.01f;

            GameObject vImgObj = new GameObject("FlashImage");
            vImgObj.transform.SetParent(vignetteObj.transform, false);
            _damageFlashVignette = vImgObj.AddComponent<Image>();
            _damageFlashVignette.color = new Color(1f, 0f, 0f, 0f);
            RectTransform viRT = vImgObj.GetComponent<RectTransform>();
            viRT.sizeDelta = new Vector2(120, 120);
        }
    }

    private void CreateDeathScreenUI()
    {
        _deathScreenObj = new GameObject("Player_Death_Overlay");
        Canvas canvas = _deathScreenObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _deathScreenObj.AddComponent<CanvasScaler>();

        RectTransform rt = _deathScreenObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(600, 350);
        rt.localScale = Vector3.one * 0.0025f;

        // Dark Backdrop
        GameObject bgPanel = new GameObject("DeathBackground");
        bgPanel.transform.SetParent(_deathScreenObj.transform, false);
        Image bgImg = bgPanel.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0f, 0f, 0.92f);
        RectTransform bgRT = bgPanel.GetComponent<RectTransform>();
        bgRT.sizeDelta = new Vector2(600, 350);

        // Title: "YOU ARE DEAD"
        GameObject titleObj = new GameObject("DeathTitle");
        titleObj.transform.SetParent(_deathScreenObj.transform, false);
        _deathTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        _deathTitleText.text = "YOU ARE DEAD";
        _deathTitleText.fontSize = 54;
        _deathTitleText.fontStyle = FontStyles.Bold;
        _deathTitleText.alignment = TextAlignmentOptions.Center;
        _deathTitleText.color = new Color(1f, 0.15f, 0.15f); // Ominous Crimson
        RectTransform titleRT = titleObj.GetComponent<RectTransform>();
        titleRT.sizeDelta = new Vector2(560, 80);
        titleRT.anchoredPosition = new Vector2(0, 50);

        // Subtitle: "Restarting Level..."
        GameObject subObj = new GameObject("DeathSubtitle");
        subObj.transform.SetParent(_deathScreenObj.transform, false);
        _deathSubtitleText = subObj.AddComponent<TextMeshProUGUI>();
        _deathSubtitleText.text = "Restarting Level...";
        _deathSubtitleText.fontSize = 28;
        _deathSubtitleText.alignment = TextAlignmentOptions.Center;
        _deathSubtitleText.color = Color.white;
        RectTransform subRT = subObj.GetComponent<RectTransform>();
        subRT.sizeDelta = new Vector2(560, 50);
        subRT.anchoredPosition = new Vector2(0, -30);

        _deathScreenObj.SetActive(false);
    }
}
