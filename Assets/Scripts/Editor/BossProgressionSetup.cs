using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Configures the two boss encounters and the complete game scene flow.
/// </summary>
public static class BossProgressionSetup
{
    private const string Level2Path = "Assets/Scenes/Level 2.unity";
    private const string Level3Path = "Assets/Scenes/Level 3.unity";
    private const string Level4Path = "Assets/Scenes/Level 4.unity";
    private const string MinotaurModelPath = "Assets/animations/Minotaur/mesh/Mesh.FBX";
    private const string MinotaurControllerPath = "Assets/animations/Minotaur/MinotaurAnimatorController.controller";

    public static void SetupBossProgression()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[BossProgressionSetup] Exit Play Mode before running the progression setup.");
            return;
        }

        if (!ValidateRequiredAssets())
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene originalScene = SceneManager.GetActiveScene();
        string originalScenePath = originalScene.path;

        FixMutantAnimator.FixMutantAnimatorClips();
        FixMutantAnimator.FixMinotaurAnimatorClips();

        ConfigureMutantVictory();
        ConfigureLevel3Exit();
        ConfigureMinotaurScene();
        ConfigureBuildSettings();

        if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        Debug.Log(
            "[BossProgressionSetup] Complete. Scene flow: Main Menu -> Level 1 -> Level 2 " +
            "(Mutant) -> Level 3 -> Level 4 (Minotaur) -> End Screen.");
    }

    private static bool ValidateRequiredAssets()
    {
        bool valid = true;

        if (!File.Exists(Level4Path))
        {
            Debug.LogError("[BossProgressionSetup] Level 4 scene is missing: " + Level4Path);
            valid = false;
        }

        if (!File.Exists(MinotaurModelPath))
        {
            Debug.LogError(
                "[BossProgressionSetup] Missing Minotaur model. Copy exactly:\n" +
                @"Source: C:\Users\Yato\Downloads\Minotaur\Minotaur\mesh\Mesh.FBX" + "\n" +
                "Destination: " + MinotaurModelPath);
            valid = false;
        }

        if (!File.Exists(MinotaurControllerPath))
        {
            Debug.LogError("[BossProgressionSetup] Missing controller: " + MinotaurControllerPath);
            valid = false;
        }

        return valid;
    }

    private static void ConfigureMutantVictory()
    {
        Scene scene = EditorSceneManager.OpenScene(Level2Path, OpenSceneMode.Single);
        BossHealth[] bosses = Object.FindObjectsByType<BossHealth>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (BossHealth boss in bosses)
        {
            SetSerializedBool(boss, "isInvulnerable", false);

            BossDefeatTrigger trigger = boss.GetComponent<BossDefeatTrigger>();
            if (trigger == null)
                trigger = boss.gameObject.AddComponent<BossDefeatTrigger>();

            SerializedObject triggerObject = new SerializedObject(trigger);
            triggerObject.FindProperty("boss").objectReferenceValue = boss;
            triggerObject.FindProperty("loadNextSceneOnDefeat").boolValue = true;
            triggerObject.FindProperty("nextSceneName").stringValue = "Level 3";
            triggerObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(trigger);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureLevel3Exit()
    {
        Scene scene = EditorSceneManager.OpenScene(Level3Path, OpenSceneMode.Single);
        LoadSceneOnInsert[] exits = Object.FindObjectsByType<LoadSceneOnInsert>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (exits.Length == 0)
        {
            Debug.LogError("[BossProgressionSetup] No LoadSceneOnInsert exit was found in Level 3.");
            return;
        }

        foreach (LoadSceneOnInsert exit in exits)
        {
            SerializedObject exitObject = new SerializedObject(exit);
            SerializedProperty sceneName = exitObject.FindProperty("sceneName");
            if (sceneName != null && sceneName.stringValue == "End Screen")
            {
                sceneName.stringValue = "Level 4";
                exitObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(exit);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureMinotaurScene()
    {
        Scene scene = EditorSceneManager.OpenScene(Level4Path, OpenSceneMode.Single);
        GameObject root = GameObject.Find("[BOSS 2] The Minotaur Warlord");
        if (root == null)
            root = CreateMinotaurBoss();

        BossHealth health = GetOrAddComponent<BossHealth>(root);
        BossController controller = GetOrAddComponent<BossController>(root);
        BossDefeatTrigger defeatTrigger = GetOrAddComponent<BossDefeatTrigger>(root);
        SetSerializedBool(health, "isInvulnerable", false);

        Animator animator = root.GetComponentInChildren<Animator>(true);
        Transform projectileSpawn = EnsureProjectileSpawn(root.transform);
        BossDamageHitbox meleeHitbox = EnsureMeleeHitbox(root.transform);

        SerializedObject controllerObject = new SerializedObject(controller);
        controllerObject.FindProperty("animator").objectReferenceValue = animator;
        controllerObject.FindProperty("projectileSpawnPoint").objectReferenceValue = projectileSpawn;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject defeatObject = new SerializedObject(defeatTrigger);
        defeatObject.FindProperty("boss").objectReferenceValue = health;
        defeatObject.FindProperty("loadNextSceneOnDefeat").boolValue = true;
        defeatObject.FindProperty("nextSceneName").stringValue = "End Screen";
        defeatObject.ApplyModifiedPropertiesWithoutUndo();

        EnsureThrowableOrb();

        EditorUtility.SetDirty(health);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(defeatTrigger);
        EditorUtility.SetDirty(meleeHitbox);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject CreateMinotaurBoss()
    {
        GameObject root = new GameObject("[BOSS 2] The Minotaur Warlord");
        root.transform.position = new Vector3(0f, 0f, 5.5f);

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MinotaurModelPath);
        GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset, root.transform) as GameObject;
        if (visual == null)
            visual = Object.Instantiate(modelAsset, root.transform);

        visual.name = "Minotaur Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        visual.transform.localScale = Vector3.one * 1.3f;

        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = visual.AddComponent<Animator>();

        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MinotaurControllerPath);
        animator.avatar = LoadFirstValidAvatar(MinotaurModelPath);
        animator.applyRootMotion = false;

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 1.9f, 0f);
        collider.radius = 1.3f;
        collider.height = 3.8f;

        root.AddComponent<BossHealth>();
        root.AddComponent<BossController>();
        root.AddComponent<BossDefeatTrigger>();
        return root;
    }

    private static Transform EnsureProjectileSpawn(Transform root)
    {
        Transform existing = root.Find("ProjectileSpawnPoint");
        if (existing != null)
            return existing;

        GameObject spawn = new GameObject("ProjectileSpawnPoint");
        spawn.transform.SetParent(root, false);
        spawn.transform.localPosition = new Vector3(0f, 2.5f, 0.8f);
        return spawn.transform;
    }

    private static BossDamageHitbox EnsureMeleeHitbox(Transform root)
    {
        Transform existing = root.Find("MeleeHitbox");
        GameObject hitboxObject;

        if (existing == null)
        {
            hitboxObject = new GameObject("MeleeHitbox");
            hitboxObject.transform.SetParent(root, false);
            hitboxObject.transform.localPosition = new Vector3(0f, 1.8f, 1.6f);
            BoxCollider collider = hitboxObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.2f, 2.2f, 2.2f);
            collider.isTrigger = true;
        }
        else
        {
            hitboxObject = existing.gameObject;
        }

        BossDamageHitbox hitbox = GetOrAddComponent<BossDamageHitbox>(hitboxObject);
        hitbox.SetDamage(10);
        hitbox.IsActive = false;
        return hitbox;
    }

    private static void EnsureThrowableOrb()
    {
        if (GameObject.Find("Throwable_Minotaur_EnergyOrb") != null)
            return;

        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Throwable_Minotaur_EnergyOrb";
        orb.transform.position = new Vector3(0.5f, 0.9f, 2f);
        orb.transform.localScale = Vector3.one * 0.35f;

        Renderer renderer = orb.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = new Color(0.15f, 0.75f, 1f, 1f);
            renderer.sharedMaterial = material;
        }

        Rigidbody rigidbody = orb.AddComponent<Rigidbody>();
        rigidbody.mass = 2f;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        orb.AddComponent<XRGrabInteractable>();
        orb.AddComponent<ThrowableBossDamage>();
    }

    private static Avatar LoadFirstValidAvatar(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object asset in assets)
        {
            Avatar avatar = asset as Avatar;
            if (avatar != null && avatar.isValid)
                return avatar;
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetSerializedBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Main Menu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Level 1.unity", true),
            new EditorBuildSettingsScene(Level2Path, true),
            new EditorBuildSettingsScene(Level3Path, true),
            new EditorBuildSettingsScene(Level4Path, true),
            new EditorBuildSettingsScene("Assets/Scenes/End Screen.unity", true),
        };
    }
}
