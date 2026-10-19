using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Applies one-time boss asset and scene repairs automatically after Unity loads.
/// Scene files are opened additively so the user's active scene is not replaced.
/// </summary>
public static class AutomaticBossEditorSetup
{
    private const string SessionKey = "VRBoss.AutomaticEditorSetupComplete";
    private const string Level2Path = "Assets/Scenes/Level 2.unity";
    private const string Level3Path = "Assets/Scenes/Level 3.unity";
    private const string Level4Path = "Assets/Scenes/Level 4.unity";
    private const string MutantModelPath = "Assets/animations/Creature Pack/Mutant.fbx";
    private const string MutantControllerPath = "Assets/animations/Creature Pack/MutantAnimatorController.controller";
    private const string MinotaurModelPath = "Assets/animations/Minotaur/mesh/Mesh.FBX";
    private const string MinotaurControllerPath = "Assets/animations/Minotaur/MinotaurAnimatorController.controller";
    private const string RuntimeMinotaurPrefabPath = "Assets/Resources/Bosses/MinotaurRuntime.prefab";

    private static void ScheduleSetup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += RunWhenReady;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += RunWhenReady;
    }

    private static void RunWhenReady()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunWhenReady;
            return;
        }

        try
        {
            ConfigureScene(Level2Path, ConfigureMutantScene);
            ConfigureScene(Level3Path, ConfigureLevel3Exit);
            ConfigureScene(Level4Path, ConfigureMinotaurScene);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[AutomaticBossEditorSetup] Boss scenes, visibility, scale, and progression repaired automatically.");
        }
        catch (Exception exception)
        {
            SessionState.SetBool(SessionKey, false);
            Debug.LogException(exception);
        }
    }

    private static void ConfigureScene(string path, Action<Scene> configure)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("[AutomaticBossEditorSetup] Scene is missing: " + path);
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedBySetup = !scene.IsValid() || !scene.isLoaded;
        if (openedBySetup)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        configure(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (openedBySetup)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static void ConfigureMutantScene(Scene scene)
    {
        BossHealth[] bosses = GetSceneComponents<BossHealth>(scene);
        foreach (BossHealth health in bosses)
        {
            GameObject root = health.gameObject;
            if (!root.name.Contains("BOSS 1") && !root.name.ToLowerInvariant().Contains("mutant"))
                continue;

            ConfigureBoss(root, false, "Level 3", 4.2f, scene);
        }
    }

    private static void ConfigureLevel3Exit(Scene scene)
    {
        LoadSceneOnInsert[] exits = GetSceneComponents<LoadSceneOnInsert>(scene);
        foreach (LoadSceneOnInsert exit in exits)
        {
            SerializedObject serializedExit = new SerializedObject(exit);
            SerializedProperty sceneName = serializedExit.FindProperty("sceneName");
            if (sceneName != null && sceneName.stringValue == "End Screen")
            {
                sceneName.stringValue = "Level 4";
                serializedExit.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(exit);
            }
        }
    }

    private static void ConfigureMinotaurScene(Scene scene)
    {
        GameObject root = FindSceneObject(scene, "[BOSS 2]");
        if (root == null)
        {
            root = new GameObject("[BOSS 2] The Minotaur Warlord");
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        ConfigureBoss(root, true, "End Screen", 4.6f, scene);
        EnsureThrowableOrb(scene);
        EnsureFolder("Assets/Resources/Bosses");
        PrefabUtility.SaveAsPrefabAsset(root, RuntimeMinotaurPrefabPath);
    }

    private static void ConfigureBoss(
        GameObject root,
        bool isMinotaur,
        string nextScene,
        float targetHeight,
        Scene scene)
    {
        root.SetActive(true);
        GameObject visual = EnsureVisual(root, isMinotaur);
        EnableVisualHierarchy(visual);

        string controllerPath = isMinotaur ? MinotaurControllerPath : MutantControllerPath;
        string modelPath = isMinotaur ? MinotaurModelPath : MutantModelPath;
        Animator animator = EnsureAnimator(root, visual, controllerPath, modelPath);
        EnsureMaterials(root, isMinotaur);
        SetVisualHeight(root, targetHeight);
        PositionInFrontOfPlayer(root, scene, 7f);

        CapsuleCollider capsule = GetOrAdd<CapsuleCollider>(root);
        capsule.center = new Vector3(0f, targetHeight * 0.5f, 0f);
        capsule.height = targetHeight;
        capsule.radius = isMinotaur ? 1.45f : 1.4f;
        capsule.isTrigger = false;

        BossHealth health = GetOrAdd<BossHealth>(root);
        SetSerializedBool(health, "isInvulnerable", false);

        Transform projectileSpawn = EnsureProjectileSpawn(root.transform, targetHeight);
        BossDamageHitbox meleeHitbox = EnsureMeleeHitbox(root.transform);
        BossController controller = GetOrAdd<BossController>(root);
        SerializedObject serializedController = new SerializedObject(controller);
        SetObjectReference(serializedController, "animator", animator);
        SetObjectReference(serializedController, "projectileSpawnPoint", projectileSpawn);
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        BossDefeatTrigger defeat = GetOrAdd<BossDefeatTrigger>(root);
        SerializedObject serializedDefeat = new SerializedObject(defeat);
        SetObjectReference(serializedDefeat, "boss", health);
        SetBool(serializedDefeat, "loadNextSceneOnDefeat", true);
        SetString(serializedDefeat, "nextSceneName", nextScene);
        serializedDefeat.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(health);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(defeat);
        EditorUtility.SetDirty(meleeHitbox);
    }

    private static GameObject EnsureVisual(GameObject root, bool isMinotaur)
    {
        Renderer renderer = root.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
            return GetDirectChildOf(root.transform, renderer.transform).gameObject;

        string modelPath = isMinotaur ? MinotaurModelPath : MutantModelPath;
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Debug.LogError("[AutomaticBossEditorSetup] Boss model is missing: " + modelPath);
            return root;
        }

        GameObject visual = PrefabUtility.InstantiatePrefab(model, root.transform) as GameObject;
        if (visual == null)
            visual = UnityEngine.Object.Instantiate(model, root.transform);

        visual.name = isMinotaur ? "Minotaur Visual" : "Mutant Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        visual.transform.localScale = Vector3.one;
        return visual;
    }

    private static Animator EnsureAnimator(
        GameObject root,
        GameObject visual,
        string controllerPath,
        string modelPath)
    {
        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = visual.AddComponent<Animator>();

        animator.enabled = true;
        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        animator.avatar = LoadFirstValidAvatar(modelPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(animator);
        return animator;
    }

    private static void EnableVisualHierarchy(GameObject visual)
    {
        if (visual == null)
            return;

        visual.SetActive(true);
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
                skinned.updateWhenOffscreen = true;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void EnsureMaterials(GameObject root, bool isMinotaur)
    {
        Material mutant = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/animations/Creature Pack/Mutant_Material.mat");
        Material body = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/animations/Minotaur/materials/Minotaur_Body_Mat.mat");
        Material pants = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/animations/Minotaur/materials/Minotaur_Pants_Mat.mat");

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0)
                materials = new Material[1];

            for (int index = 0; index < materials.Length; index++)
            {
                bool needsMaterial = materials[index] == null ||
                    materials[index].name.Contains("Default");
                if (!needsMaterial)
                    continue;

                if (!isMinotaur)
                {
                    materials[index] = mutant;
                }
                else
                {
                    bool pantsSlot = renderer.name.ToLowerInvariant().Contains("pant") || index > 0;
                    materials[index] = pantsSlot ? pants : body;
                }
            }

            renderer.sharedMaterials = materials;
        }
    }

    private static void SetVisualHeight(GameObject root, float targetHeight)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        if (bounds.size.y < 0.01f)
            return;

        Transform visualRoot = GetDirectChildOf(root.transform, renderers[0].transform);
        float factor = Mathf.Clamp(targetHeight / bounds.size.y, 0.1f, 20f);
        visualRoot.localScale *= factor;
        EditorUtility.SetDirty(visualRoot);
    }

    private static void PositionInFrontOfPlayer(GameObject root, Scene scene, float distance)
    {
        Camera[] cameras = GetSceneComponents<Camera>(scene);
        Camera camera = cameras.Length > 0 ? cameras[0] : null;
        if (camera == null)
        {
            root.transform.position = new Vector3(0f, 0f, 7f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            return;
        }

        Transform playerRoot = camera.transform.root;
        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = playerRoot.forward;
        forward.Normalize();

        Vector3 position = playerRoot.position + forward * distance;
        position.y = playerRoot.position.y;
        root.transform.position = position;

        Vector3 lookDirection = playerRoot.position - root.transform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.01f)
            root.transform.rotation = Quaternion.LookRotation(lookDirection.normalized);
    }

    private static Transform EnsureProjectileSpawn(Transform root, float height)
    {
        Transform spawn = root.Find("ProjectileSpawnPoint");
        if (spawn == null)
        {
            GameObject spawnObject = new GameObject("ProjectileSpawnPoint");
            spawnObject.transform.SetParent(root, false);
            spawn = spawnObject.transform;
        }

        spawn.localPosition = new Vector3(0f, height * 0.68f, 0.8f);
        return spawn;
    }

    private static BossDamageHitbox EnsureMeleeHitbox(Transform root)
    {
        Transform existing = root.Find("MeleeHitbox");
        GameObject hitboxObject = existing != null ? existing.gameObject : new GameObject("MeleeHitbox");
        hitboxObject.transform.SetParent(root, false);
        hitboxObject.transform.localPosition = new Vector3(0f, 2f, 1.7f);

        BoxCollider collider = GetOrAdd<BoxCollider>(hitboxObject);
        collider.size = new Vector3(2.4f, 2.5f, 2.4f);
        collider.isTrigger = true;

        BossDamageHitbox hitbox = GetOrAdd<BossDamageHitbox>(hitboxObject);
        hitbox.SetDamage(10);
        hitbox.IsActive = false;
        return hitbox;
    }

    private static void EnsureThrowableOrb(Scene scene)
    {
        if (FindSceneObject(scene, "Throwable_Minotaur_EnergyOrb") != null)
            return;

        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Throwable_Minotaur_EnergyOrb";
        SceneManager.MoveGameObjectToScene(orb, scene);
        orb.transform.position = new Vector3(0.5f, 0.9f, 2f);
        orb.transform.localScale = Vector3.one * 0.35f;

        Rigidbody rigidbody = orb.AddComponent<Rigidbody>();
        rigidbody.mass = 2f;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        orb.AddComponent<XRGrabInteractable>();
        orb.AddComponent<ThrowableBossDamage>();
    }

    private static Avatar LoadFirstValidAvatar(string assetPath)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            Avatar avatar = asset as Avatar;
            if (avatar != null && avatar.isValid)
                return avatar;
        }

        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string nameFragment)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains(nameFragment))
                    return child.gameObject;
            }
        }

        return null;
    }

    private static T[] GetSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> results = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));
        return results.ToArray();
    }

    private static Transform GetDirectChildOf(Transform root, Transform descendant)
    {
        Transform current = descendant;
        while (current.parent != null && current.parent != root)
            current = current.parent;
        return current;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetSerializedBool(UnityEngine.Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SetBool(serializedObject, propertyName, value);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectReference(SerializedObject target, string name, UnityEngine.Object value)
    {
        SerializedProperty property = target.FindProperty(name);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject target, string name, bool value)
    {
        SerializedProperty property = target.FindProperty(name);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetString(SerializedObject target, string name, string value)
    {
        SerializedProperty property = target.FindProperty(name);
        if (property != null)
            property.stringValue = value;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
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
