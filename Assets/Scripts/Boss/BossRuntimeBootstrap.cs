using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

/// <summary>
/// Play-mode safety net for missing components. Scene placement and rig assets
/// belong to BossSetupMenu; this never spawns, moves, scales, or rebinds a boss.
/// </summary>
public sealed class BossRuntimeBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying || (scene.name != "Level 2" && scene.name != "Level 4"))
            return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponent<BossRuntimeBootstrap>() != null) return;
        GameObject helper = new GameObject("Boss_Runtime_Bootstrap");
        SceneManager.MoveGameObjectToScene(helper, scene);
        helper.AddComponent<BossRuntimeBootstrap>();
    }

    private IEnumerator Start()
    {
        Camera playerCamera = null;
        for (int frame = 0; frame < 120 && playerCamera == null; frame++)
        {
            playerCamera = Camera.main;
            yield return null;
        }
        Scene scene = gameObject.scene;
        string marker = scene.name == "Level 4" ? "[BOSS 2]" : "[BOSS 1]";
        GameObject boss = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name.StartsWith(marker)) boss = child.gameObject;

        if (boss == null)
        {
            Debug.LogError("[BossRuntimeBootstrap] Saved boss is missing in " + scene.name +
                ". Run its existing Boss Setup command once in Edit Mode.");
            Destroy(gameObject);
            yield break;
        }
        if (playerCamera != null && PlayerHealth.Instance == null)
        {
            XROrigin origin = playerCamera.GetComponentInParent<XROrigin>();
            GameObject player = origin != null ? origin.gameObject : playerCamera.transform.root.gameObject;
            if (player.GetComponent<PlayerHealth>() == null) player.AddComponent<PlayerHealth>();
        }

        BossHealth health = boss.GetComponent<BossHealth>();
        if (health == null) health = boss.AddComponent<BossHealth>();
        if (boss.GetComponent<CapsuleCollider>() == null)
        {
            CapsuleCollider capsule = boss.AddComponent<CapsuleCollider>();
            capsule.height = scene.name == "Level 4" ? 4.5f : 4.2f;
            capsule.center = Vector3.up * capsule.height * 0.5f;
            capsule.radius = 1.4f;
        }
        if (boss.GetComponent<Rigidbody>() == null)
        {
            Rigidbody body = boss.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }
        Transform spawn = boss.transform.Find("ProjectileSpawnPoint");
        if (spawn == null)
        {
            spawn = new GameObject("ProjectileSpawnPoint").transform;
            spawn.SetParent(boss.transform, false);
            spawn.localPosition = new Vector3(0f, 2.8f, 0.8f);
        }
        Transform hitboxRoot = boss.transform.Find("MeleeHitbox");
        if (hitboxRoot == null)
        {
            hitboxRoot = new GameObject("MeleeHitbox").transform;
            hitboxRoot.SetParent(boss.transform, false);
            hitboxRoot.localPosition = new Vector3(0f, 1.9f, 1.6f);
        }
        if (hitboxRoot.GetComponent<BoxCollider>() == null)
        {
            BoxCollider collider = hitboxRoot.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(2.4f, 2.2f, 3f);
        }
        BossDamageHitbox hitbox = hitboxRoot.GetComponent<BossDamageHitbox>();
        if (hitbox == null)
        {
            hitbox = hitboxRoot.gameObject.AddComponent<BossDamageHitbox>();
            hitbox.SetDamage(10);
            hitbox.IsActive = false;
        }
        if (boss.GetComponent<BossController>() == null)
        {
            BossController controller = boss.AddComponent<BossController>();
            controller.ConfigureRuntime(boss.GetComponentInChildren<Animator>(true),
                playerCamera != null ? playerCamera.transform : null, spawn, hitbox);
        }
        if (boss.GetComponent<BossDefeatTrigger>() == null)
            boss.AddComponent<BossDefeatTrigger>().Configure(health,
                scene.name == "Level 4" ? "End Screen" : "Level 3");
        if (boss.GetComponentInChildren<BossHealthBarUI>(true) == null)
            BossHealthBarUI.EnsureForBoss(health);

        Destroy(gameObject);
    }
}
