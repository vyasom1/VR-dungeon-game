using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using Unity.Profiling;

/// <summary>
/// Additive presentation layer for boss arenas, menus, and the victory screen.
/// It observes existing health/attack events and does not own combat or scene flow.
/// </summary>
[DefaultExecutionOrder(-5000)]
public sealed class BossFightPolish : MonoBehaviour
{
    private static BossFightPolish _active;
    private static int _contactFrame = -1;
    private static BossHealth _contactBoss;
    private static Vector3 _contactPosition;
    private static bool _contactCritical;
    private const string ComboKey = "VRPolish.Combo";
    private const string HitsKey = "VRPolish.Hits";
    private const string TimeKey = "VRPolish.Time";
    private const string LastLevelKey = "VRPolish.LastBossLevel";
    private const string HapticScaleKey = "VRPolish.HapticScale";
    private const string DominantHandKey = "VRPolish.DominantHand";

    private readonly List<GameObject> _ownedObjects = new List<GameObject>();
    private BossHealth _boss;
    private BossController _controller;
    private PlayerHealth _player;
    private Transform _cameraTransform;
    private Transform _leftHand;
    private Renderer[] _bossRenderers;
    private MaterialPropertyBlock[] _savedBossBlocks;
    private MaterialPropertyBlock _flashBlock;
    private Material _particleMaterial;
    private Material _ringMaterial;
    private int _baseColorId;
    private Color _originalBossColor = Color.white;
    private Coroutine _bossFlashRoutine;
    private Coroutine _vignetteRoutine;
    private Coroutine _introRoutine;
    private Coroutine _deathRoutine;
    private Coroutine _shadowRoutine;
    private bool _bossDied;
    private bool _playerDied;
    private bool _introShown;
    private BossController.BossState _previousBossState;
    private int _combo;
    private int _hits;
    private float _lastPlayerHealth;
    private float _nextMissCheck;
    private int _lastMissSwingFrame = -1;
    private readonly List<int> _pendingHitFrames = new List<int>();
    private float _elapsed;
    private float _projectileScanTime;
    private bool _ownsSlowMotion;
    private float _previousTimeScale;
    private Mesh _ringMesh;

    private Canvas _screenCanvas;
    private RectTransform _screenRoot;
    private TextMeshProUGUI _bossNameText;
    private TextMeshProUGUI _bossHpText;
    private TextMeshProUGUI _playerHpText;
    private TextMeshProUGUI _comboText;
    private TextMeshProUGUI _introText;
    private TextMeshProUGUI _criticalText;
    private TextMeshProUGUI _debugText;
    private TextMeshProUGUI _deathText;
    private TextMeshProUGUI _menuTitle;
    private TextMeshProUGUI _menuSubtitle;
    private TextMeshProUGUI _menuControls;
    private TextMeshProUGUI _victoryStats;
    private TextMeshProUGUI _hapticValueText;
    private TextMeshProUGUI _dominantHandText;
    private Slider _hapticSlider;
    private RectTransform _hpFill;
    private Image _hpFillImage;
    private Image _vignette;
    private Image _blackFade;
    private Image _topLetterbox;
    private Image _bottomLetterbox;
    private RectTransform _bossHudPanel;
    private RectTransform _playerHudPanel;
    private RectTransform _comboHudPanel;
    private CanvasGroup _introGroup;
    private CanvasGroup _deathGroup;
    private CanvasGroup _criticalGroup;
    private CanvasGroup _settingsGroup;
    private RectTransform _worldHud;
    private Slider _worldBossSlider;
    private Slider _worldPlayerSlider;
    private TextMeshProUGUI _worldBossText;
    private TextMeshProUGUI _worldPlayerText;
    private TextMeshProUGUI _worldComboText;
    private AudioSource _audio;
    private AudioSource _ambientAudio;
    private RectTransform _vrFeedback;
    private AudioClip _impactClip;
    private AudioClip _criticalClip;
    private AudioClip _roarClip;
    private AudioClip _deathClip;
    private AudioClip _chimeClip;
    private AudioClip _victoryClip;
    private AudioClip _ambientClip;
    private GameObject _telegraphLight;
    private GameObject _projectileLight;
    private bool _showDebug;
    private float _fpsSmooth;
    private float _statsTimer;
    private int _lastDrawCalls;
    private int _lastTriangles;
    private int _lastColliders;
    private string _sceneName;
    private ProfilerRecorder _drawRecorder;
    private ProfilerRecorder _triangleRecorder;

#if UNITY_EDITOR
    private float _captureTimer;
    private bool _editorShotTaken;
    private float _csvTimer;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterForSceneLoads()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool arena = scene.name == "Level 2" || scene.name == "Level 4";
        bool menu = scene.name == "Main Menu";
        bool victory = scene.name == "End Screen";
        if (!arena && !menu && !victory) return;

        GameObject host = new GameObject("Boss_Fight_Presentation");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<BossFightPolish>();
    }

    private void Awake()
    {
        if (_active != null && _active != this)
        {
            Destroy(gameObject);
            return;
        }
        _active = this;
        _sceneName = gameObject.scene.name;
        bool bossArena = _sceneName == "Level 2" || _sceneName == "Level 4";
        _combo = bossArena ? 0 : PlayerPrefs.GetInt(ComboKey, 0);
        _hits = bossArena ? 0 : PlayerPrefs.GetInt(HitsKey, 0);
        _elapsed = bossArena ? 0f : PlayerPrefs.GetFloat(TimeKey, 0f);
        if (bossArena)
        {
            PlayerPrefs.SetInt(ComboKey, 0);
            PlayerPrefs.SetInt(HitsKey, 0);
            PlayerPrefs.SetFloat(TimeKey, 0f);
        }
        _baseColorId = Shader.PropertyToID("_BaseColor");
        _flashBlock = new MaterialPropertyBlock();
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.volume = 0.85f;
        _particleMaterial = CreateGlowMaterial(Color.white);
        _ringMaterial = CreateGlowMaterial(Color.white);
        _drawRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        _triangleRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        BuildProceduralAudio();
        BuildScreenCanvas();
        ApplyHapticScale(PlayerPrefs.GetFloat(HapticScaleKey, 1f));

        if (_sceneName == "Main Menu") { BuildMainMenu(); ConfigureVrMenuCanvas(); }
        else if (_sceneName == "End Screen") { BuildVictoryScreen(); ConfigureVrMenuCanvas(); }
        else StartCoroutine(InitializeArena());
    }

    private void OnDestroy()
    {
        if (_boss != null)
        {
            _boss.OnHealthChanged.RemoveListener(OnBossHealthChanged);
            _boss.OnDamaged.RemoveListener(OnBossDamaged);
            _boss.OnDefeated.RemoveListener(OnBossDefeated);
        }
        if (_player != null)
        {
            _player.OnHealthChanged.RemoveListener(OnPlayerHealthChanged);
            _player.OnPlayerDied.RemoveListener(OnPlayerDied);
        }
        if (_ownsSlowMotion) Time.timeScale = _previousTimeScale;
        if (_ringMesh != null) Destroy(_ringMesh);
        if (_particleMaterial != null) Destroy(_particleMaterial);
        if (_ringMaterial != null) Destroy(_ringMaterial);
        foreach (AudioClip clip in new[] { _impactClip, _criticalClip, _roarClip, _deathClip, _chimeClip, _victoryClip, _ambientClip })
            if (clip != null) Destroy(clip);
        if (_vignette != null && _vignette.sprite != null)
        {
            Texture texture = _vignette.sprite.texture;
            Destroy(_vignette.sprite);
            Destroy(texture);
        }
        _drawRecorder.Dispose();
        _triangleRecorder.Dispose();
        if (_active == this) _active = null;
    }

    private IEnumerator InitializeArena()
    {
        float wait = 0f;
        while (wait < 3f && (Camera.main == null || FindArenaBoss() == null))
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        _cameraTransform = Camera.main != null ? Camera.main.transform : null;
        _boss = FindArenaBoss();
        _controller = _boss != null ? _boss.GetComponent<BossController>() : null;
        _player = PlayerHealth.Instance;
        if (_player == null) _player = FindFirstObjectByType<PlayerHealth>();
        if (_boss != null)
        {
            _boss.OnHealthChanged.AddListener(OnBossHealthChanged);
            _boss.OnDamaged.AddListener(OnBossDamaged);
            _boss.OnDefeated.AddListener(OnBossDefeated);
            _bossRenderers = _boss.GetComponentsInChildren<Renderer>(true);
            _savedBossBlocks = new MaterialPropertyBlock[_bossRenderers.Length];
            for (int i = 0; i < _bossRenderers.Length; i++)
            {
                _savedBossBlocks[i] = new MaterialPropertyBlock();
                if (_bossRenderers[i] != null) _bossRenderers[i].GetPropertyBlock(_savedBossBlocks[i]);
            }
            if (_bossRenderers.Length > 0 && _bossRenderers[0] != null)
            {
                Material material = _bossRenderers[0].sharedMaterial;
                if (material != null && material.HasProperty(_baseColorId))
                    _originalBossColor = material.GetColor(_baseColorId);
            }
            if (_controller != null) _previousBossState = _controller.CurrentState;
        }
        if (_player != null)
        {
            _player.OnHealthChanged.AddListener(OnPlayerHealthChanged);
            _player.OnPlayerDied.AddListener(OnPlayerDied);
            _lastPlayerHealth = _player.CurrentHealth;
        }

        BuildArenaHud();
        BuildWorldHud();
        BuildVrFeedback();
        BuildStartIntro();
        StartAmbient();
        VRHandCombat[] combatHands = FindObjectsByType<VRHandCombat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (VRHandCombat combatHand in combatHands)
        {
            if (combatHand.gameObject.scene != gameObject.scene) continue;
            HandCombatPolish hand = combatHand.GetComponent<HandCombatPolish>();
            if (hand == null) hand = combatHand.gameObject.AddComponent<HandCombatPolish>();
            hand.Initialize();
        }
        if (_boss != null) OnBossHealthChanged(_boss.CurrentHealth, _boss.MaxHealth);
        if (_player != null) OnPlayerHealthChanged(_player.CurrentHealth, _player.MaxHealth);
        Debug.Log("[BossFightPolish] Presentation initialized for " + _sceneName);
    }

    private BossHealth FindArenaBoss()
    {
        BossHealth[] all = FindObjectsByType<BossHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (BossHealth item in all)
            if (item.gameObject.scene == gameObject.scene) return item;
        return null;
    }

    private void Update()
    {
        if (_sceneName != "Level 2" && _sceneName != "Level 4")
        {
#if UNITY_EDITOR
            SaveEditorScreenshot();
#endif
            return;
        }
        if (!_bossDied && !_playerDied) _elapsed += Time.unscaledDeltaTime;
        _fpsSmooth = Mathf.Lerp(_fpsSmooth, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.15f);
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
        {
            _showDebug = !_showDebug;
            if (_debugText != null) _debugText.transform.parent.gameObject.SetActive(_showDebug);
        }
        if (_boss != null && !_boss.IsDead)
        {
            UpdateBossBar();
            UpdateAttackTelegraph();
            UpdateProjectileWarning();
        }
        UpdateWorldHud();
        UpdateSwingMisses();
        if (_showDebug) UpdatePerformanceOverlay();
#if UNITY_EDITOR
        SaveEditorScreenshot();
#endif
    }

    private void LateUpdate()
    {
        ResolvePunchFeedback();
        PositionVrFeedback();
        if (_worldHud != null && _cameraTransform != null)
            _worldHud.rotation = _cameraTransform.rotation;
    }

    private void BuildVrFeedback()
    {
        if (!XRSettings.isDeviceActive || _cameraTransform == null) return;
        GameObject host = new GameObject("VR_Combat_Feedback", typeof(RectTransform), typeof(Canvas));
        host.transform.SetParent(transform, false);
        Canvas canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 150;
        _vrFeedback = host.GetComponent<RectTransform>();
        _vrFeedback.sizeDelta = new Vector2(1920f, 1080f);
        _vrFeedback.localScale = Vector3.one * 0.0009f;
        foreach (Transform element in new Transform[] { _introGroup.transform, _criticalGroup.transform, _vignette.transform, _blackFade.transform })
            element.SetParent(_vrFeedback, false);
        PositionVrFeedback();
    }

    private void PositionVrFeedback()
    {
        if (_vrFeedback == null || _cameraTransform == null) return;
        _vrFeedback.SetPositionAndRotation(_cameraTransform.position + _cameraTransform.forward * 0.8f, _cameraTransform.rotation);
    }
    private void BuildScreenCanvas()
    {
        GameObject canvasObject = new GameObject("Project_Polish_Monitor_HUD");
        _ownedObjects.Add(canvasObject);
        _screenCanvas = canvasObject.AddComponent<Canvas>();
        _screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _screenCanvas.sortingOrder = 120;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        _screenRoot = canvasObject.GetComponent<RectTransform>();

        _vignette = AddImage("Player_Damage_Vignette", _screenRoot, new Color(1f, 0.015f, 0.015f, 0f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color[] pixels = new Color[96 * 96];
        for (int y = 0; y < 96; y++)
        for (int x = 0; x < 96; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(47.5f, 47.5f)) / 67.2f;
            float alpha = Mathf.Pow(Mathf.Clamp01((distance - 0.38f) / 0.42f), 1.7f) * 0.72f;
            pixels[y * 96 + x] = new Color(1f, 0.008f, 0.012f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite radial = Sprite.Create(texture, new Rect(0f, 0f, 96f, 96f), Vector2.one * 0.5f);
        _vignette.sprite = radial;
        _vignette.type = Image.Type.Simple;
        _vignette.preserveAspect = false;
        _vignette.raycastTarget = false;

        _blackFade = AddImage("Death_Fade", _screenRoot, new Color(0f, 0f, 0f, 0f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        _blackFade.raycastTarget = false;
        _deathGroup = _blackFade.gameObject.AddComponent<CanvasGroup>();
        _deathGroup.alpha = 0f;
        _deathGroup.gameObject.SetActive(false);
        _deathText = AddText("YOU ARE DEAD", _blackFade.transform, 68f, Color.white,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(800f, 140f));
        _deathText.fontStyle = FontStyles.Bold;

        _bossHudPanel = CreatePanel("Boss_HUD", _screenRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(690f, 132f), new Color(0.025f, 0.035f, 0.055f, 0.9f));
        _bossNameText = AddText("BOSS_NAME", _bossHudPanel, 30f, new Color(1f, 0.78f, 0.39f), TextAlignmentOptions.Center, new Vector2(0.03f, 0.55f), new Vector2(0.97f, 0.98f), Vector2.zero);
        _bossHpText = AddText("BOSS_HP", _bossHudPanel, 21f, Color.white, TextAlignmentOptions.Center, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.32f), Vector2.zero);
        RectTransform barBack = CreatePanel("Boss_HP_Back", _bossHudPanel, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.51f), Vector2.zero, Vector2.zero, new Color(0.12f, 0.025f, 0.035f, 1f));
        _hpFillImage = AddImage("Boss_HP_Fill", barBack, new Color(0.18f, 0.88f, 0.42f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        _hpFill = _hpFillImage.rectTransform;
        _hpFill.anchorMax = new Vector2(1f, 1f);
        _hpFill.pivot = new Vector2(0f, 0.5f);
        _hpFillImage.raycastTarget = false;

        _playerHudPanel = CreatePanel("Player_HUD", _screenRoot, Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(295f, 94f), new Color(0.025f, 0.035f, 0.055f, 0.88f));
        _playerHpText = AddText("PLAYER_HP", _playerHudPanel, 28f, Color.white, TextAlignmentOptions.Left, new Vector2(0.08f, 0.12f), new Vector2(0.95f, 0.9f), Vector2.zero);
        _comboHudPanel = CreatePanel("Combo_HUD", _screenRoot, new Vector2(1f, 0.52f), new Vector2(1f, 0.52f), new Vector2(-24f, 0f), new Vector2(220f, 118f), new Color(0.025f, 0.035f, 0.055f, 0.85f));
        _comboText = AddText("COMBO", _comboHudPanel, 40f, new Color(1f, 0.75f, 0.23f), TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero);

        RectTransform debugPanel = CreatePanel("Performance_Debug_Panel", _screenRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -28f), new Vector2(420f, 205f), new Color(0.005f, 0.008f, 0.015f, 0.86f));
        _debugText = AddText("Performance_Data", debugPanel, 22f, new Color(0.48f, 0.96f, 1f), TextAlignmentOptions.TopLeft, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), Vector2.zero);
        _debugText.text = "PERFORMANCE • F3";
        debugPanel.gameObject.SetActive(false);

        _introGroup = CreateGroup("Level_Start_Intro", _screenRoot);
        _introText = AddText("Level_Intro_Text", _introGroup.transform, 52f, Color.white, TextAlignmentOptions.Center, new Vector2(0.22f, 0.39f), new Vector2(0.78f, 0.63f), Vector2.zero);
        _introText.fontStyle = FontStyles.Bold;
        _topLetterbox = AddImage("Cinematic_Top_Bar", _screenRoot, new Color(0.005f, 0.006f, 0.012f, 0.9f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -108f), new Vector2(0f, 0f), false);
        _bottomLetterbox = AddImage("Cinematic_Bottom_Bar", _screenRoot, new Color(0.005f, 0.006f, 0.012f, 0.9f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 108f), false);
        _topLetterbox.gameObject.SetActive(false);
        _bottomLetterbox.gameObject.SetActive(false);
        _criticalGroup = CreateGroup("Critical_Popup", _screenRoot);
        _criticalText = AddText("Critical_Text", _criticalGroup.transform, 57f, new Color(1f, 0.9f, 0.3f), TextAlignmentOptions.Center, new Vector2(0.34f, 0.61f), new Vector2(0.66f, 0.72f), Vector2.zero);
        _criticalText.fontStyle = FontStyles.Bold;
        _criticalText.text = "CRITICAL!";
        _criticalGroup.alpha = 0f;
        _criticalGroup.gameObject.SetActive(false);
    }

    private void BuildArenaHud()
    {
        if (_boss == null)
        {
            if (_bossHudPanel != null) _bossHudPanel.gameObject.SetActive(false);
            return;
        }
        string title = _sceneName == "Level 4" ? "THE MINOTAUR WARLORD" : "THE MUTANT BEHEMOTH";
        if (_bossNameText != null) _bossNameText.text = title;
        if (_comboText != null) _comboText.text = _combo > 1 ? "" + _combo + "x\nCOMBO" : "COMBO\n" + _combo;
        if (_comboHudPanel != null) _comboHudPanel.gameObject.SetActive(true);
    }

    private void BuildWorldHud()
    {
        string preferredHand = PlayerPrefs.GetString(DominantHandKey, "Left");
        string handName = preferredHand == "Right" ? "Right Hand Model" : "Left Hand Model";
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform item in transforms)
            if (item.name == handName && item.gameObject.scene == gameObject.scene) { _leftHand = item; break; }
        if (_leftHand == null) return;

        GameObject canvasObject = new GameObject("VR_Combat_Wrist_HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(_leftHand, false);
        canvasObject.transform.localPosition = new Vector3(0.015f, 0.15f, 0.055f);
        canvasObject.transform.localRotation = Quaternion.identity;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(430f, 190f);
        Vector3 handScale = _leftHand.lossyScale;
        rect.localScale = new Vector3(0.00065f / Mathf.Max(0.0001f, Mathf.Abs(handScale.x)), 0.00065f / Mathf.Max(0.0001f, Mathf.Abs(handScale.y)), 0.00065f / Mathf.Max(0.0001f, Mathf.Abs(handScale.z)));
        rect.position = _leftHand.position + Vector3.up * 0.14f;
        _worldHud = rect;
        AddImage("Wrist_HUD_Background", rect, new Color(0.012f, 0.018f, 0.03f, 0.84f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        _worldBossText = AddText("Wrist_Boss_HP", rect, 23f, Color.white, TextAlignmentOptions.Left, new Vector2(0.06f, 0.56f), new Vector2(0.94f, 0.95f), Vector2.zero);
        _worldPlayerText = AddText("Wrist_Player_HP", rect, 22f, new Color(0.52f, 0.95f, 0.78f), TextAlignmentOptions.Left, new Vector2(0.06f, 0.28f), new Vector2(0.94f, 0.61f), Vector2.zero);
        _worldPlayerSlider = CreateWorldSlider(rect);
        _worldPlayerSlider.name = "Wrist_Player_Bar";
        RectTransform playerBar = _worldPlayerSlider.GetComponent<RectTransform>();
        playerBar.anchorMin = new Vector2(0.06f, 0.21f);
        playerBar.anchorMax = new Vector2(0.94f, 0.27f);
        _worldComboText = AddText("Wrist_Combo", rect, 22f, new Color(1f, 0.78f, 0.34f), TextAlignmentOptions.Left, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.3f), Vector2.zero);
        if (_boss != null)
        {
            _worldBossSlider = CreateWorldSlider(rect);
            _worldBossSlider.transform.localPosition = new Vector3(0f, 0f, 0f);
        }
    }

    private Slider CreateWorldSlider(RectTransform parent)
    {
        GameObject background = new GameObject("Wrist_Boss_Bar", typeof(RectTransform), typeof(Image), typeof(Slider));
        background.transform.SetParent(parent, false);
        RectTransform rt = background.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.06f, 0.43f);
        rt.anchorMax = new Vector2(0.94f, 0.53f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.12f, 0.02f, 0.025f);
        Slider slider = background.GetComponent<Slider>();
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(background.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        Image image = fill.GetComponent<Image>();
        image.color = new Color(0.15f, 0.92f, 0.44f);
        slider.fillRect = fillRect;
        slider.targetGraphic = image;
        return slider;
    }

    private void UpdateWorldHud()
    {
        if (_worldHud == null) return;
        if (_boss != null && _worldBossSlider != null)
        {
            float amount = _boss.MaxHealth > 0 ? (float)_boss.CurrentHealth / _boss.MaxHealth : 0f;
            _worldBossSlider.value = Mathf.MoveTowards(_worldBossSlider.value, amount, Time.unscaledDeltaTime * 1.6f);
            Image fill = _worldBossSlider.fillRect != null ? _worldBossSlider.fillRect.GetComponent<Image>() : null;
            if (fill != null) fill.color = HealthColor(amount);
            _worldBossText.text = "BOSS  " + _boss.CurrentHealth + " / " + _boss.MaxHealth;
        }
        if (_player != null)
        {
            _worldPlayerText.text = "YOU   " + _player.CurrentHealth + " / " + _player.MaxHealth;
            if (_worldPlayerSlider != null)
                _worldPlayerSlider.value = Mathf.MoveTowards(_worldPlayerSlider.value, (float)_player.CurrentHealth / Mathf.Max(1, _player.MaxHealth), Time.unscaledDeltaTime * 1.6f);
        }
        if (_worldComboText != null) _worldComboText.text = "COMBO   " + _combo + "x";
    }

    private void BuildStartIntro()
    {
        if (_introShown || _boss == null) return;
        _introShown = true;
        _introText.text = _sceneName == "Level 4"
            ? "THE MINOTAUR\n<size=28>DEFEAT THE BOSS</size>"
            : "THE MUTANT\n<size=28>DEFEAT THE BOSS</size>";
        _topLetterbox.gameObject.SetActive(true);
        _bottomLetterbox.gameObject.SetActive(true);
        if (_controller != null) _controller.enabled = false;
        _introRoutine = StartCoroutine(PlayLevelIntro());
    }

    private IEnumerator PlayLevelIntro()
    {
        _introGroup.gameObject.SetActive(true);
        _introGroup.alpha = 0f;
        RectTransform introRect = _introGroup.GetComponent<RectTransform>();
        introRect.localScale = Vector3.one * 0.75f;
        float elapsed = 0f;
        while (elapsed < 2f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 2f);
            _introGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 2.8f));
            introRect.localScale = Vector3.one * Mathf.Lerp(0.75f, 1f, Mathf.SmoothStep(0f, 1f, t));
            float barHeight = Mathf.Lerp(108f, 70f, t);
            _topLetterbox.rectTransform.sizeDelta = new Vector2(0f, barHeight);
            _bottomLetterbox.rectTransform.sizeDelta = new Vector2(0f, barHeight);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.65f);
        elapsed = 0f;
        while (elapsed < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.9f);
            _introGroup.alpha = 1f - t;
            float barHeight = Mathf.Lerp(70f, 0f, t);
            _topLetterbox.rectTransform.sizeDelta = new Vector2(0f, barHeight);
            _bottomLetterbox.rectTransform.sizeDelta = new Vector2(0f, barHeight);
            yield return null;
        }
        _introGroup.gameObject.SetActive(false);
        _topLetterbox.gameObject.SetActive(false);
        _bottomLetterbox.gameObject.SetActive(false);
        if (_controller != null && !_controller.GetComponent<BossHealth>().IsDead) _controller.enabled = true;
        _introRoutine = null;
        StartCoroutine(ShowObjectiveBanner());
    }

    private IEnumerator ShowObjectiveBanner()
    {
        RectTransform banner = CreatePanel("Level_Objective", _screenRoot,
            new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), Vector2.zero,
            new Vector2(900f, 64f), new Color(0.015f, 0.025f, 0.04f, 0.88f));
        AddText("Objective", banner, 27f, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero).text = _sceneName == "Level 4"
            ? "Level 4: Boss Fight — Defeat the Minotaur"
            : "Level 2: Boss Fight — Defeat the Mutant";
        CanvasGroup group = banner.gameObject.AddComponent<CanvasGroup>();
        yield return new WaitForSecondsRealtime(2.5f);
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = 1f - elapsed / 0.5f;
            yield return null;
        }
        Destroy(banner.gameObject);
    }
    private void OnBossHealthChanged(int current, int maximum)
    {
        if (_bossHpText != null) _bossHpText.text = "HP   " + current + " / " + maximum;
        if (_bossNameText != null && _boss != null) _bossNameText.text = _sceneName == "Level 4" ? "THE MINOTAUR WARLORD" : "THE MUTANT BEHEMOTH";
        UpdateBossBar();
    }

    private void UpdateBossBar()
    {
        if (_boss == null || _hpFill == null) return;
        float max = Mathf.Max(1f, _boss.MaxHealth);
        float target = Mathf.Clamp01((float)_boss.CurrentHealth / max);
        float current = Mathf.MoveTowards(_hpFill.anchorMax.x, target, Time.unscaledDeltaTime * 0.75f);
        _hpFill.anchorMax = new Vector2(current, 1f);
        if (_hpFillImage != null) _hpFillImage.color = HealthColor(current);
    }

    private static Color HealthColor(float amount)
    {
        if (amount > 0.5f) return Color.Lerp(new Color(0.95f, 0.78f, 0.12f), new Color(0.18f, 0.9f, 0.44f), (amount - 0.5f) * 2f);
        return Color.Lerp(new Color(0.96f, 0.12f, 0.12f), new Color(0.96f, 0.78f, 0.12f), amount * 2f);
    }

    private void OnPlayerHealthChanged(int current, int maximum)
    {
        if (_playerHpText != null) _playerHpText.text = "PLAYER\n" + current + " / " + maximum;
        if (_lastPlayerHealth > current && _vignette != null)
        {
            if (_vignetteRoutine != null) StopCoroutine(_vignetteRoutine);
            _vignetteRoutine = StartCoroutine(FlashVignette());
            Play(_impactClip, 0.62f);
            SendPlayerHitHaptics();
        }
        _lastPlayerHealth = current;
    }

    private void SendPlayerHitHaptics()
    {
        HapticImpulsePlayer[] players = _player != null
            ? _player.GetComponentsInChildren<HapticImpulsePlayer>(true)
            : Array.Empty<HapticImpulsePlayer>();
        bool sent = false;
        foreach (HapticImpulsePlayer player in players)
            if (player != null) sent |= player.SendHapticImpulse(0.6f, 0.18f);
        if (sent) return;

        foreach (XRNode node in new[] { XRNode.LeftHand, XRNode.RightHand })
        {
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
                device.SendHapticImpulse(0, 0.6f * PlayerPrefs.GetFloat(HapticScaleKey, 1f), 0.18f);
        }
    }

    private IEnumerator FlashVignette()
    {
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = 0.72f * (1f - Mathf.Clamp01(elapsed / 0.2f));
            _vignette.color = new Color(1f, 0.015f, 0.015f, alpha);
            yield return null;
        }
        _vignette.color = new Color(1f, 0.015f, 0.015f, 0f);
        _vignetteRoutine = null;
    }

    private void OnPlayerDied()
    {
        if (_playerDied) return;
        _playerDied = true;
        if (_deathRoutine != null) StopCoroutine(_deathRoutine);
        _deathRoutine = StartCoroutine(FadePlayerDeath());
        Play(_deathClip, 0.95f);
    }

    private IEnumerator FadePlayerDeath()
    {
        _blackFade.color = Color.black;
        _blackFade.transform.SetAsLastSibling();
        _deathGroup.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < 0.75f)
        {
            elapsed += Time.unscaledDeltaTime;
            _deathGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / 0.75f) * 1f;
            yield return null;
        }
        _deathRoutine = null;
    }

    private void OnBossDamaged()
    {
        if (_boss == null || _bossDied) return;
        Play(_impactClip, 0.8f);
        Vector3 center = BossCenter();
        SpawnParticles(center, 38, new Color(1f, 0.14f, 0.08f), new Color(1f, 0.78f, 0.26f), 0.16f, 2.4f, 0.45f);
        if (_bossFlashRoutine != null) StopCoroutine(_bossFlashRoutine);
        _bossFlashRoutine = StartCoroutine(FlashBossRed());

        _pendingHitFrames.Add(Time.frameCount);
        _hits++;
        _combo++;
        PlayerPrefs.SetInt(HitsKey, _hits);
        PlayerPrefs.SetInt(ComboKey, _combo);
        if (_combo % 5 == 0) Play(_chimeClip, 0.78f);
        UpdateComboHud();
        _nextMissCheck = 0f;
    }

    private void ResolvePunchFeedback()
    {
        foreach (int frame in _pendingHitFrames)
        {
            if (_contactFrame != frame || _contactBoss != _boss) continue;
            SpawnParticles(_contactPosition, 22, Color.white, new Color(1f, 0.76f, 0.25f), 0.07f, 1.45f, 0.28f);
            if (_contactCritical) { ShowCritical(); Play(_criticalClip, 0.95f); }
        }
        _pendingHitFrames.Clear();
    }
    private void OnBossDefeated()
    {
        if (_bossDied) return;
        _bossDied = true;
        PlayerPrefs.SetInt(LastLevelKey, _sceneName == "Level 4" ? 4 : 2);
        PlayerPrefs.SetInt(HitsKey, _hits);
        PlayerPrefs.SetFloat(TimeKey, _elapsed);
        PlayerPrefs.Save();
        Play(_deathClip, 1f);
        Play(_victoryClip, 0.75f);
        SpawnParticles(BossCenter(), 170, new Color(1f, 0.48f, 0.12f), new Color(1f, 0.93f, 0.58f), 0.4f, 8f, 1.35f);
        StartCoroutine(DeathSlowMotionAndShake());
    }

    private IEnumerator DeathSlowMotionAndShake()
    {
        float oldScale = Time.timeScale;
        _previousTimeScale = oldScale;
        _ownsSlowMotion = true;
        Time.timeScale = 0.22f;
        Vector3 start = _screenRoot.localPosition;
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.unscaledDeltaTime;
            float falloff = 1f - elapsed / 0.3f;
            _screenRoot.localPosition = start + new Vector3(UnityEngine.Random.Range(-9f, 9f), UnityEngine.Random.Range(-8f, 8f), 0f) * falloff;
            yield return null;
        }
        _screenRoot.localPosition = start;
        Time.timeScale = oldScale;
        _ownsSlowMotion = false;
    }

    private IEnumerator FlashBossRed()
    {
        if (_bossRenderers == null) yield break;
        float elapsed = 0f;
        while (elapsed < 0.15f)
        {
            elapsed += Time.unscaledDeltaTime;
            float blend = 1f - elapsed / 0.15f;
            for (int i = 0; i < _bossRenderers.Length; i++)
            {
                Renderer renderer = _bossRenderers[i];
                if (renderer == null) continue;
                _flashBlock.Clear();
                renderer.GetPropertyBlock(_flashBlock);
                _flashBlock.SetColor(_baseColorId, Color.Lerp(_originalBossColor, new Color(1f, 0.035f, 0.025f), blend * 0.9f));
                renderer.SetPropertyBlock(_flashBlock);
            }
            yield return null;
        }
        for (int i = 0; i < _bossRenderers.Length; i++)
            if (_bossRenderers[i] != null) _bossRenderers[i].SetPropertyBlock(_savedBossBlocks[i]);
        _bossFlashRoutine = null;
    }

    private void UpdateAttackTelegraph()
    {
        if (_controller == null) return;
        BossController.BossState state = _controller.CurrentState;
        if (state == BossController.BossState.Attack && _previousBossState != BossController.BossState.Attack)
        {
            Play(_roarClip, 0.66f);
            StartCoroutine(TelegraphLimb());
        }
        _previousBossState = state;
        Animator animator = _boss != null ? _boss.GetComponentInChildren<Animator>(true) : null;
        if (animator != null && animator.isActiveAndEnabled)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if ((info.IsName("JumpAttack") || info.IsName("Jump") || info.IsName("Attack_Jump")) && info.normalizedTime < 0.18f && _shadowRoutine == null)
                _shadowRoutine = StartCoroutine(ShowLandingShadow());
        }
    }

    private IEnumerator TelegraphLimb()
    {
        yield return null;
        Transform limb = null;
        Animator animator = _boss != null ? _boss.GetComponentInChildren<Animator>(true) : null;
        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            limb = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (limb == null && _boss != null)
        {
            foreach (Transform part in _boss.GetComponentsInChildren<Transform>(true))
                if (part.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0 || part.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0)
                { limb = part; break; }
        }
        bool ranged = false;
        if (animator != null && animator.isActiveAndEnabled)
        {
            AnimatorStateInfo attack = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            ranged = attack.IsName("Attack_Throw") || attack.IsName("Roar");
            if (ranged && _boss != null)
            {
                Transform spawn = _boss.transform.Find("ProjectileSpawnPoint");
                if (spawn != null) limb = spawn;
            }
        }
        if (limb == null) limb = _boss != null ? _boss.transform : transform;
        if (_telegraphLight == null)
        {
            _telegraphLight = new GameObject("Attack_Telegraph_Red_Glow");
            _telegraphLight.transform.SetParent(limb, false);
            _telegraphLight.transform.localPosition = Vector3.zero;
            Light light = _telegraphLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.035f, 0.01f);
            light.range = 2.2f;
            light.intensity = 0f;
        }
        _telegraphLight.transform.SetParent(limb, false);
        _telegraphLight.transform.localPosition = Vector3.zero;
        Light telegraph = _telegraphLight.GetComponent<Light>();
        if (telegraph == null) yield break;
        telegraph.enabled = true;
        float duration = ranged ? 0.68f : 0.44f;
        if (animator != null && animator.isActiveAndEnabled)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("Attack_Throw") || info.IsName("Roar") || info.IsName("Throw")) duration = 0.64f;
        }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            telegraph.intensity = 2.8f * Mathf.Sin(Mathf.PI * elapsed / duration);
            yield return null;
        }
        telegraph.intensity = 0f;
    }

    private IEnumerator ShowLandingShadow()
    {
        yield return new WaitForSeconds(0.45f);
        GameObject shadow = CreateGroundRing("Jump_Attack_Landing_Shadow");
        Transform target = _cameraTransform;
        if (target != null)
        {
            Vector3 origin = target.position;
            if (Physics.Raycast(origin + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 50f))
                shadow.transform.position = hit.point + Vector3.up * 0.018f;
            else shadow.transform.position = new Vector3(origin.x, 0.025f, origin.z);
        }
        Renderer renderer = shadow.GetComponent<Renderer>();
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        float elapsed = 0f;
        while (elapsed < 0.55f && shadow != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = 0.58f * (1f - elapsed / 0.55f);
            block.SetColor(_baseColorId, new Color(1f, 0.025f, 0.012f, alpha));
            renderer.SetPropertyBlock(block);
            yield return null;
        }
        if (shadow != null) Destroy(shadow);
        _shadowRoutine = null;
    }

    private GameObject CreateGroundRing(string name)
    {
        GameObject ring = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        ring.transform.SetParent(transform, true);
        Mesh mesh = _ringMesh != null ? _ringMesh : new Mesh { name = "Telegraph_Ring_Mesh" };
        const int segments = 48;
        Vector3[] vertices = new Vector3[segments * 2];
        int[] indices = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(x * 0.48f, 0f, z * 0.48f);
            vertices[i * 2 + 1] = new Vector3(x * 0.35f, 0f, z * 0.35f);
            int next = (i + 1) % segments;
            int idx = i * 6;
            indices[idx] = i * 2; indices[idx + 1] = next * 2; indices[idx + 2] = next * 2 + 1;
            indices[idx + 3] = i * 2; indices[idx + 4] = next * 2 + 1; indices[idx + 5] = i * 2 + 1;
        }
        mesh.vertices = vertices;
        mesh.triangles = indices;
        Color[] colors = new Color[vertices.Length];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        _ringMesh = mesh;
        ring.GetComponent<MeshFilter>().sharedMesh = mesh;
        ring.GetComponent<MeshRenderer>().sharedMaterial = _ringMaterial;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(_baseColorId, new Color(1f, 0.025f, 0.015f, 0.58f));
        ring.GetComponent<MeshRenderer>().SetPropertyBlock(block);
        ring.transform.localScale = Vector3.one * 2.15f;
        return ring;
    }

    private void UpdateProjectileWarning()
    {
        if (Time.unscaledTime < _projectileScanTime) return;
        _projectileScanTime = Time.unscaledTime + 0.15f;
        BossProjectile[] projectiles = FindObjectsByType<BossProjectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (BossProjectile projectile in projectiles)
        {
            if (projectile.gameObject.scene != gameObject.scene || projectile.transform.Find("Warning_Glow") != null) continue;
            GameObject glow = new GameObject("Warning_Glow");
            glow.transform.SetParent(projectile.transform, false);
            Light light = glow.AddComponent<Light>();
            light.color = new Color(1f, 0.32f, 0.055f);
            light.range = 3.1f;
            light.intensity = 3.5f;
            _projectileLight = glow;
        }
    }

    private void RegisterPunchContact(BossHealth boss, Vector3 position, bool critical)
    {
        _contactFrame = Time.frameCount;
        _contactBoss = boss;
        _contactPosition = position;
        _contactCritical = critical;
    }

    internal static void RegisterPunchContactFromHand(BossHealth boss, Vector3 position, bool critical)
    {
        if (_active != null) _active.RegisterPunchContact(boss, position, critical);
    }

    internal static void RegisterSwingMiss(int frame)
    {
        if (_active == null) return;
        _active._lastMissSwingFrame = frame;
        _active._nextMissCheck = Time.unscaledTime + 0.55f;
    }

    private void UpdateSwingMisses()
    {
        if (_nextMissCheck > 0f && Time.unscaledTime > _nextMissCheck && _lastMissSwingFrame != _contactFrame)
        {
            _combo = 0;
            PlayerPrefs.SetInt(ComboKey, 0);
            UpdateComboHud();
            _nextMissCheck = 0f;
        }
    }

    private void ShowCritical()
    {
        if (_criticalGroup == null) return;
        _criticalGroup.gameObject.SetActive(true);
        _criticalGroup.alpha = 1f;
        _criticalText.rectTransform.localScale = Vector3.one * 0.72f;
        StartCoroutine(AnimateCritical());
    }

    private IEnumerator AnimateCritical()
    {
        float elapsed = 0f;
        while (elapsed < 0.65f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / 0.65f;
            _criticalGroup.alpha = 1f - t;
            _criticalText.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.15f, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        _criticalGroup.gameObject.SetActive(false);
    }

    private Vector3 BossCenter()
    {
        if (_boss == null) return transform.position;
        if (_bossRenderers != null)
            foreach (Renderer renderer in _bossRenderers)
                if (renderer != null && renderer.bounds.size.sqrMagnitude > 0.01f) return renderer.bounds.center;
        return _boss.transform.position + Vector3.up * 1.5f;
    }

    private void SpawnParticles(Vector3 position, int count, Color first, Color second, float size, float speed, float lifetime)
    {
        GameObject host = new GameObject("Combat_Particle_Burst");
        host.transform.position = position;
        ParticleSystem particles = host.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = lifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.45f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(first, second);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;
        main.gravityModifier = 0.18f;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
        ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _particleMaterial;
        particles.Emit(count);
        Destroy(host, lifetime + 0.4f);
    }

    private Material CreateGlowMaterial(Color color)
    {
        Shader shader = Resources.Load<Shader>("BossPolishEffects") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        material.renderQueue = 3000;
        return material;
    }

    private void BuildMainMenu()
    {
        _bossHudPanel.gameObject.SetActive(false);
        _playerHudPanel.gameObject.SetActive(false);
        _comboHudPanel.gameObject.SetActive(false);
        _introGroup.gameObject.SetActive(false);
        RectTransform titlePanel = CreatePanel("Boss_Fight_VR_Title", _screenRoot, new Vector2(0.5f, 0.87f), new Vector2(0.5f, 0.87f), Vector2.zero, new Vector2(980f, 170f), new Color(0.018f, 0.026f, 0.043f, 0.72f));
        _menuTitle = AddText("Game_Title", titlePanel, 60f, new Color(1f, 0.79f, 0.39f), TextAlignmentOptions.Center, new Vector2(0.03f, 0.48f), new Vector2(0.97f, 0.97f), Vector2.zero);
        _menuTitle.text = "ESCAPE THE PRISON";
        _menuTitle.fontStyle = FontStyles.Bold;
        _menuSubtitle = AddText("Game_Subtitle", titlePanel, 29f, Color.white, TextAlignmentOptions.Center, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.5f), Vector2.zero);
        _menuSubtitle.text = "BOSS FIGHT  •  VIRTUAL REALITY";

        Button begin = null;
        Button resume = null;
        Button quit = null;
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button.gameObject.scene != gameObject.scene) continue;
            if (button.name == "Start Button") begin = button;
            if (button.name == "Continue") resume = button;
            if (button.name == "Quit Button") quit = button;
        }
        HideLegacyMenuCanvases();
        if (begin != null) CreateButton("Begin", _screenRoot, "BEGIN", new Vector2(0.5f, 0.52f), new Vector2(430f, 90f), begin.onClick.Invoke);
        if (resume != null) CreateButton("Continue", _screenRoot, "CONTINUE", new Vector2(0.5f, 0.38f), new Vector2(430f, 76f), resume.onClick.Invoke);
        if (quit != null) CreateButton("Quit", _screenRoot, "QUIT", new Vector2(0.5f, 0.25f), new Vector2(430f, 76f), quit.onClick.Invoke);
        CreateButton("Settings_Button", _screenRoot, "SETTINGS", new Vector2(0.82f, 0.105f), new Vector2(260f, 74f), ShowSettings);
        _screenCanvas.sortingOrder = 20;
        _settingsGroup = CreateGroup("Settings_Card", _screenRoot);
        _settingsGroup.gameObject.SetActive(false);
        RectTransform card = CreatePanel("Settings_Background", _settingsGroup.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.018f, 0.025f, 0.042f, 0.97f));
        card.GetComponent<Image>().raycastTarget = true;
        _menuControls = AddText("Settings_Information", card, 26f, Color.white, TextAlignmentOptions.Center, new Vector2(0.1f, 0.22f), new Vector2(0.9f, 0.82f), Vector2.zero);
        _menuControls.text = "XR SETTINGS\n\nHAPTIC INTENSITY";
        _hapticSlider = CreateSettingsSlider(card, new Vector2(0.5f, 0.61f));
        _hapticSlider.value = PlayerPrefs.GetFloat(HapticScaleKey, 1f);
        _hapticValueText = AddText("Haptic_Intensity_Value", card, 22f, new Color(1f, 0.8f, 0.42f), TextAlignmentOptions.Center, new Vector2(0.32f, 0.48f), new Vector2(0.68f, 0.59f), Vector2.zero);
        UpdateHapticLabel(_hapticSlider.value);
        _hapticSlider.onValueChanged.AddListener(OnHapticScaleChanged);
        AddText("Dominant_Hand_Label", card, 24f, Color.white, TextAlignmentOptions.Center, new Vector2(0.12f, 0.33f), new Vector2(0.88f, 0.44f), Vector2.zero).text = "DOMINANT HAND";
        CreateButton("Dominant_Left", card, "LEFT", new Vector2(0.34f, 0.24f), new Vector2(230f, 58f), () => SetDominantHand("Left"));
        CreateButton("Dominant_Right", card, "RIGHT", new Vector2(0.66f, 0.24f), new Vector2(230f, 58f), () => SetDominantHand("Right"));
        _dominantHandText = AddText("Dominant_Hand_Value", card, 20f, new Color(0.7f, 0.87f, 1f), TextAlignmentOptions.Center, new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.23f), Vector2.zero);
        SetDominantHand(PlayerPrefs.GetString(DominantHandKey, "Left"));
        CreateButton("Settings_Close", card, "CLOSE", new Vector2(0.5f, 0.1f), new Vector2(240f, 58f), HideSettings);
        StartAmbient();
    }

    private Slider CreateSettingsSlider(RectTransform parent, Vector2 center)
    {
        GameObject sliderObject = new GameObject("Haptic_Intensity_Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = center;
        rect.sizeDelta = new Vector2(590f, 42f);
        sliderObject.GetComponent<Image>().color = new Color(0.16f, 0.19f, 0.24f);
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(sliderObject.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.15f);
        fillRect.anchorMax = new Vector2(1f, 0.85f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = new Color(1f, 0.48f, 0.13f);
        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;
        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(sliderObject.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(28f, 52f);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = Color.white;
        slider.handleRect = handleRect;
        slider.transition = Selectable.Transition.ColorTint;
        return slider;
    }

    private void OnHapticScaleChanged(float value)
    {
        PlayerPrefs.SetFloat(HapticScaleKey, value);
        PlayerPrefs.Save();
        ApplyHapticScale(value);
        UpdateHapticLabel(value);
    }

    private static void ApplyHapticScale(float value)
    {
        HapticImpulsePlayer[] players = FindObjectsByType<HapticImpulsePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (HapticImpulsePlayer player in players) player.amplitudeMultiplier = Mathf.Clamp01(value);
    }

    private void UpdateHapticLabel(float value)
    {
        if (_hapticValueText != null) _hapticValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void SetDominantHand(string hand)
    {
        PlayerPrefs.SetString(DominantHandKey, hand);
        PlayerPrefs.Save();
        if (_dominantHandText != null) _dominantHandText.text = "CURRENT: " + hand.ToUpperInvariant() + "  •  WRIST HUD FOLLOWS";
    }

    private void ShowSettings() { if (_settingsGroup != null) _settingsGroup.gameObject.SetActive(true); }
    private void HideSettings() { if (_settingsGroup != null) _settingsGroup.gameObject.SetActive(false); }

    private void BuildVictoryScreen()
    {
        HideLegacyMenuCanvases();
        _bossHudPanel.gameObject.SetActive(false);
        _playerHudPanel.gameObject.SetActive(false);
        _comboHudPanel.gameObject.SetActive(false);
        _introGroup.gameObject.SetActive(false);
        StartAmbient();
        RectTransform panel = CreatePanel("Victory_Card", _screenRoot, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(980f, 630f), new Color(0.012f, 0.021f, 0.038f, 0.94f));
        TextMeshProUGUI title = AddText("Victory_Title", panel, 94f, new Color(1f, 0.79f, 0.36f), TextAlignmentOptions.Center, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.95f), Vector2.zero);
        title.text = "VICTORY";
        title.fontStyle = FontStyles.Bold;
        StartCoroutine(AnimateVictory(title.rectTransform));
        _victoryStats = AddText("Victory_Stats", panel, 34f, Color.white, TextAlignmentOptions.Center, new Vector2(0.1f, 0.44f), new Vector2(0.9f, 0.67f), Vector2.zero);
        _victoryStats.text = "TIME   " + FormatTime(PlayerPrefs.GetFloat(TimeKey, 0f)) + "\nHITS LANDED   " + PlayerPrefs.GetInt(HitsKey, 0);
        CreateButton("Next_Level_Button", panel, "NEXT LEVEL", new Vector2(0.5f, 0.28f), new Vector2(390f, 72f), LoadNextLevel);
        CreateButton("Main_Menu_Button", panel, "MAIN MENU", new Vector2(0.5f, 0.13f), new Vector2(390f, 66f), ReturnToMenu);
        Play(_victoryClip, 0.82f);
        _deathGroup.gameObject.SetActive(false);
    }

    public void LoadNextLevel()
    {
        int lastLevel = PlayerPrefs.GetInt(LastLevelKey, 4);
        string next = lastLevel == 2 ? "Level 3" : "Main Menu";
        if (!Application.CanStreamedLevelBeLoaded(next)) next = "Main Menu";
        SceneManager.LoadScene(next);
    }

    public void ReturnToMenu()
    {
        if (Application.CanStreamedLevelBeLoaded("Main Menu")) SceneManager.LoadScene("Main Menu");
    }

    private void HideLegacyMenuCanvases()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (canvas.gameObject.scene != gameObject.scene || canvas == _screenCanvas || canvas.transform.IsChildOf(_screenRoot)) continue;
            if (canvas.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) continue;
            canvas.gameObject.SetActive(false);
        }
    }
    private void ConfigureVrMenuCanvas()
    {
        if (!XRSettings.isDeviceActive || Camera.main == null) return;
        _screenCanvas.renderMode = RenderMode.WorldSpace;
        _screenCanvas.worldCamera = Camera.main;
        _screenRoot.sizeDelta = new Vector2(1920f, 1080f);
        _screenRoot.localScale = Vector3.one * 0.0015f;
        _screenRoot.position = Camera.main.transform.position + Camera.main.transform.forward * 2.5f;
        _screenRoot.rotation = Camera.main.transform.rotation;
    }

    private IEnumerator AnimateVictory(RectTransform title)
    {
        CanvasGroup group = title.gameObject.AddComponent<CanvasGroup>();
        float elapsed = 0f;
        while (elapsed < 0.8f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.8f);
            title.localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, t);
            group.alpha = t;
            yield return null;
        }
    }
    private void StartAmbient()
    {
        if (_ambientClip == null) return;
        if (_ambientAudio == null) _ambientAudio = gameObject.AddComponent<AudioSource>();
        _ambientAudio.clip = _ambientClip;
        _ambientAudio.loop = true;
        _ambientAudio.spatialBlend = 0f;
        _ambientAudio.volume = _sceneName == "Main Menu" ? 0.12f : 0.085f;
        _ambientAudio.Play();
    }

    private void BuildProceduralAudio()
    {
        _impactClip = MakeClip("Polish_Impact", 0.24f, 0);
        _criticalClip = MakeClip("Polish_Critical", 0.36f, 1);
        _roarClip = MakeClip("Polish_Roar", 0.95f, 2);
        _deathClip = MakeClip("Polish_Death_Groan", 1.25f, 3);
        _chimeClip = MakeClip("Polish_Combo_Chime", 0.55f, 4);
        _victoryClip = MakeClip("Polish_Victory_Fanfare", 1.8f, 5);
        _ambientClip = MakeClip("Polish_Arena_Ambient", 3f, 6);
    }

    private static AudioClip MakeClip(string clipName, float duration, int kind)
    {
        const int sampleRate = 22050;
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[count];
        uint seed = 0x1234567u + (uint)(kind * 977);
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = kind == 6 ? 0.58f + 0.08f * Mathf.Sin(t * Mathf.PI * 2f / duration) : Mathf.Pow(1f - t / duration, kind == 4 || kind == 5 ? 1.15f : 2.2f);
            float value;
            if (kind == 0 || kind == 1)
            {
                seed = 1664525u * seed + 1013904223u;
                float noise = ((seed >> 8) / 16777216f) * 2f - 1f;
                float freq = kind == 1 ? Mathf.Lerp(2600f, 280f, t / duration) : Mathf.Lerp(1800f, 95f, t / duration);
                phase += freq / sampleRate;
                value = noise * (kind == 1 ? 0.28f : 0.52f) + Mathf.Sin(phase * Mathf.PI * 2f) * 0.25f;
            }
            else if (kind == 2 || kind == 3)
            {
                float sweep = kind == 2 ? Mathf.Lerp(190f, 68f, t / duration) : Mathf.Lerp(145f, 48f, t / duration);
                phase += sweep / sampleRate;
                float growl = Mathf.Sin(phase * Mathf.PI * 2f) + 0.38f * Mathf.Sin(phase * Mathf.PI * 4.08f);
                float rasp = Mathf.Sin(t * 41f) * 0.14f;
                value = (growl * 0.42f + rasp) * envelope;
            }
            else if (kind == 4 || kind == 5)
            {
                float note = kind == 4 ? (t < 0.18f ? 880f : 1174.66f) : t < 0.58f ? 523.25f : t < 1.12f ? 659.25f : 783.99f;
                phase += note / sampleRate;
                value = (Mathf.Sin(phase * Mathf.PI * 2f) + Mathf.Sin(phase * Mathf.PI * 4f) * 0.24f) * envelope * 0.35f;
            }
            else
            {
                value = Mathf.Sin(t * Mathf.PI * 2f * 54f) * 0.55f + Mathf.Sin(t * Mathf.PI * 2f * 81f) * 0.27f + Mathf.Sin(t * Mathf.PI * 2f * 108f) * 0.18f;
            }
            samples[i] = Mathf.Clamp(value * envelope * 0.55f, -0.92f, 0.92f);
        }
        AudioClip clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void Play(AudioClip clip, float volume)
    {
        if (_audio != null && clip != null) _audio.PlayOneShot(clip, volume);
    }

    private void UpdatePerformanceOverlay()
    {
        _statsTimer -= Time.unscaledDeltaTime;
        if (_statsTimer > 0f) return;
        _statsTimer = 0.65f;
        _lastColliders = 0;
        foreach (Collider collider in FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (collider.enabled) _lastColliders++;
        _lastDrawCalls = _drawRecorder.Valid ? (int)_drawRecorder.LastValue : 0;
        _lastTriangles = _triangleRecorder.Valid ? (int)_triangleRecorder.LastValue : 0;
        _debugText.text = "ARENA PERFORMANCE  •  F3\n" +
                          "FPS          " + Mathf.RoundToInt(_fpsSmooth) + "\n" +
                          "DRAW CALLS   " + (_lastDrawCalls > 0 ? _lastDrawCalls.ToString("N0") : "sampling") + "\n" +
                          "TRIANGLES    " + (_lastTriangles > 0 ? _lastTriangles.ToString("N0") : "sampling") + "\n" +
                          "COLLIDERS    " + _lastColliders.ToString("N0") + "\n" +
                          "SCENE        " + _sceneName;
#if UNITY_EDITOR
        WritePerformanceSample();
#endif
    }

#if UNITY_EDITOR
    private void WritePerformanceSample()
    {
        // Sample against unscaled time because the panel refreshes less often than Update.
        if (Time.unscaledTime < _csvTimer) return;
        _csvTimer = Time.unscaledTime + 1f;
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "boss-arena-performance.csv");
        if (!File.Exists(path)) File.AppendAllText(path, "utc,scene,fps,draw_calls,triangles,active_colliders\n");
        File.AppendAllText(path, DateTime.UtcNow.ToString("o") + "," + _sceneName + "," + Mathf.RoundToInt(_fpsSmooth) + "," + _lastDrawCalls + "," + _lastTriangles + "," + _lastColliders + "\n");
    }
#endif

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    private CanvasGroup CreateGroup(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        CanvasGroup group = obj.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        return group;
    }

    private RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = (anchorMin + anchorMax) * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private Image AddImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, bool raycast)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private TextMeshProUGUI AddText(string name, Transform parent, float fontSize, Color color,
        TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        if (anchorMin == anchorMax) rect.sizeDelta = size;
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.margin = new Vector4(10f, 4f, 10f, 4f);
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 anchor,
        Vector2 size, UnityAction action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.55f, 0.12f, 0.07f, 0.96f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.86f, 0.31f, 0.11f);
        colors.pressedColor = new Color(0.3f, 0.07f, 0.04f);
        button.colors = colors;
        button.onClick.AddListener(action);
        TextMeshProUGUI text = AddText(name + "_Label", buttonObject.transform, 28f, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        return button;
    }

    private void UpdateComboHud()
    {
        string value = _combo > 1 ? _combo + "x\nCOMBO" : "COMBO\n" + _combo;
        if (_comboText != null) _comboText.text = value;
        if (_worldComboText != null) _worldComboText.text = "COMBO   " + _combo + "x";
    }

    private void SaveEditorScreenshot()
    {
#if UNITY_EDITOR
        if (_editorShotTaken || (_sceneName != "Level 2" && _sceneName != "Level 4" && _sceneName != "Main Menu" && _sceneName != "End Screen")) return;
        _captureTimer += Time.unscaledDeltaTime;
        if (_captureTimer < 4f) return;
        _editorShotTaken = true;
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
        Directory.CreateDirectory(folder);
        string fileName = _sceneName == "Level 2" ? "BossFight-Mutant.png" :
                          _sceneName == "Level 4" ? "BossFight-Minotaur.png" :
                          _sceneName == "Main Menu" ? "MainMenu.png" : "Victory.png";
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, fileName));
#endif
    }

    private void OnEnable()
    {
        if (_sceneName == "Level 2" || _sceneName == "Level 4")
            _elapsed = 0f;
    }

    private void OnApplicationQuit()
    {
        if (_sceneName == "Level 2" || _sceneName == "Level 4")
        {
            PlayerPrefs.SetFloat(TimeKey, _elapsed);
            PlayerPrefs.Save();
        }
    }

}












