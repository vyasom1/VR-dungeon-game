using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space VR health indicator for bosses. A health bar is created
/// automatically for every active BossHealth after a scene loads.
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    [Tooltip("Health segments/icons (optional).")]
    [SerializeField] private GameObject[] healthGems;

    [Header("Facing Settings")]
    [SerializeField] private bool faceCamera = true;

    private Transform _mainCamTransform;
    private UnityEngine.UI.Image _fillImage;
    private float _targetHealth;
    private bool _subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateHealthBarsForActiveBosses()
    {
        BossHealth[] bosses = Object.FindObjectsByType<BossHealth>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (BossHealth boss in bosses)
        {
            EnsureForBoss(boss);
        }
    }

    public static BossHealthBarUI EnsureForBoss(BossHealth boss)
    {
        if (boss == null)
            return null;

        BossHealthBarUI existing = boss.GetComponentInChildren<BossHealthBarUI>(true);
        return existing != null ? existing : CreateHealthBar(boss);
    }

    private static BossHealthBarUI CreateHealthBar(BossHealth boss)
    {
        GameObject canvasObject = new GameObject("Boss_Health_Bar");
        canvasObject.transform.SetParent(boss.transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 4.5f, 0f);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;
        canvasObject.AddComponent<CanvasScaler>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600f, 120f);
        canvasRect.localScale = Vector3.one * 0.005f;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.035f, 0.045f, 0.92f);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject sliderObject = new GameObject("HealthSlider");
        sliderObject.transform.SetParent(canvasObject.transform, false);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.interactable = false;
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.05f, 0.42f);
        sliderRect.anchorMax = new Vector2(0.95f, 0.72f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        Image sliderBackground = sliderObject.AddComponent<Image>();
        sliderBackground.color = new Color(0.16f, 0.02f, 0.02f, 1f);

        GameObject fillAreaObject = new GameObject("Fill Area");
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillArea = fillAreaObject.AddComponent<RectTransform>();
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(4f, 4f);
        fillArea.offsetMax = new Vector2(-4f, -4f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        Image fill = fillObject.AddComponent<Image>();
        fill.color = new Color(0.82f, 0.04f, 0.04f, 1f);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        slider.fillRect = fillRect;
        slider.targetGraphic = fill;

        GameObject textObject = new GameObject("HealthText");
        textObject.transform.SetParent(canvasObject.transform, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 32f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0.38f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        BossHealthBarUI healthBar = canvasObject.AddComponent<BossHealthBarUI>();
        healthBar.Initialize(boss, slider, label);
        return healthBar;
    }

    public void Initialize(BossHealth boss, Slider slider, TextMeshProUGUI label)
    {
        Unsubscribe();
        bossHealth = boss;
        healthSlider = slider;
        healthText = label;
        _fillImage = slider != null && slider.fillRect != null ? slider.fillRect.GetComponent<UnityEngine.UI.Image>() : null;
        Subscribe();

        if (bossHealth != null)
            UpdateHealthDisplay(bossHealth.CurrentHealth, bossHealth.MaxHealth);
    }

    private void Awake()
    {
        if (healthSlider != null && healthSlider.fillRect != null)
            _fillImage = healthSlider.fillRect.GetComponent<UnityEngine.UI.Image>();
        if (bossHealth == null)
            bossHealth = GetComponentInParent<BossHealth>();

        if (Camera.main != null)
            _mainCamTransform = Camera.main.transform;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed || bossHealth == null)
            return;

        bossHealth.OnHealthChanged.AddListener(UpdateHealthDisplay);
        bossHealth.OnDefeated.AddListener(DestroyHealthBar);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || bossHealth == null)
            return;

        bossHealth.OnHealthChanged.RemoveListener(UpdateHealthDisplay);
        bossHealth.OnDefeated.RemoveListener(DestroyHealthBar);
        _subscribed = false;
    }

    private void LateUpdate()
    {
        if (healthSlider != null)
        {
            healthSlider.value = Mathf.MoveTowards(healthSlider.value, _targetHealth,
                Time.unscaledDeltaTime * Mathf.Max(1f, healthSlider.maxValue) * 2.2f);
            float fraction = healthSlider.maxValue > 0f ? healthSlider.value / healthSlider.maxValue : 0f;
            if (_fillImage != null)
            {
                _fillImage.color = fraction > 0.5f
                    ? Color.Lerp(new Color(0.94f, 0.76f, 0.12f), new Color(0.18f, 0.9f, 0.43f), (fraction - 0.5f) * 2f)
                    : Color.Lerp(new Color(0.95f, 0.1f, 0.12f), new Color(0.94f, 0.76f, 0.12f), fraction * 2f);
            }
        }
        if (!faceCamera)
            return;

        if (_mainCamTransform == null && Camera.main != null)
            _mainCamTransform = Camera.main.transform;

        if (_mainCamTransform != null)
        {
            transform.LookAt(transform.position + _mainCamTransform.rotation * Vector3.forward,
                _mainCamTransform.rotation * Vector3.up);
        }
    }

    private void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = maxHealth;
            _targetHealth = currentHealth;
            if (healthSlider.value > healthSlider.maxValue || healthSlider.value == 0f)
                healthSlider.value = currentHealth;
        }

        if (healthText != null)
            healthText.text = $"HP: {currentHealth} / {maxHealth}";

        if (healthGems == null)
            return;

        for (int i = 0; i < healthGems.Length; i++)
        {
            if (healthGems[i] != null)
                healthGems[i].SetActive(i < currentHealth);
        }
    }

    private void DestroyHealthBar()
    {
        Destroy(gameObject);
    }
}
