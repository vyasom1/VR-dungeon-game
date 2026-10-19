using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles victory events when a boss is defeated:
/// opening exit doors, activating chests/rewards, or fading to the next scene.
/// </summary>
public class BossDefeatTrigger : MonoBehaviour
{
    [Header("Boss Reference")]
    [SerializeField] private BossHealth boss;

    [Header("Environment Changes On Defeat")]
    [Tooltip("GameObjects to activate upon victory (e.g. Exit Door, Win Portal, Reward Chest).")]
    [SerializeField] private List<GameObject> objectsToEnable = new List<GameObject>();
    [Tooltip("GameObjects to deactivate upon victory (e.g. Forcefields, Boss Barriers, Hazard Spawners).")]
    [SerializeField] private List<GameObject> objectsToDisable = new List<GameObject>();

    [Header("Scene Transition (Optional)")]
    [SerializeField] private bool loadNextSceneOnDefeat = false;
    [SerializeField] private string nextSceneName = "End Screen";
    [SerializeField] private float sceneTransitionDelay = 3f;

    private bool _subscribed;

    private void Awake()
    {
        if (boss == null) boss = GetComponentInParent<BossHealth>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Configure(BossHealth bossHealth, string victoryScene, bool loadScene = true)
    {
        Unsubscribe();
        boss = bossHealth;
        nextSceneName = victoryScene;
        loadNextSceneOnDefeat = loadScene;
        Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed || boss == null) return;
        boss.OnDefeated.AddListener(OnBossDefeated);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || boss == null) return;
        boss.OnDefeated.RemoveListener(OnBossDefeated);
        _subscribed = false;
    }

    private void OnBossDefeated()
    {
        Debug.Log("[BossDefeatTrigger] Handling victory sequence...");

        foreach (var obj in objectsToEnable)
        {
            if (obj != null) obj.SetActive(true);
        }

        foreach (var obj in objectsToDisable)
        {
            if (obj != null) obj.SetActive(false);
        }

        if (loadNextSceneOnDefeat && !string.IsNullOrEmpty(nextSceneName))
        {
            StartCoroutine(DelayedSceneLoad());
        }
    }

    private IEnumerator DelayedSceneLoad()
    {
        yield return new WaitForSeconds(sceneTransitionDelay);

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene(nextSceneName);
        }
        else
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
