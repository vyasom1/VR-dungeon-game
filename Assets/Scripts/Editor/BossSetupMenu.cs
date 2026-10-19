using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// The two setup commands rebuild one clean encounter and save it to its scene.
/// </summary>
public static class BossSetupMenu
{
    private const string Level2Path = "Assets/Scenes/Level 2.unity";
    private const string Level4Path = "Assets/Scenes/Level 4.unity";
    private const string MutantFolder = "Assets/animations/Creature Pack";
    private const string MutantModelPath = MutantFolder + "/Mutant.fbx";
    private const string MutantControllerPath = MutantFolder + "/MutantAnimatorController.controller";
    private const string MinotaurAnimationFolder = "Assets/animations/Minotaur/animation";
    private const string MinotaurModelPath = "Assets/animations/Minotaur/mesh/Mesh.FBX";
    private const string MinotaurControllerPath = "Assets/animations/Minotaur/MinotaurAnimatorController.controller";

    private static readonly string[] TriggerParameters =
    {
        "AttackMelee", "AttackThrow", "Hurt", "Death", "Punch", "Swipe",
        "JumpAttack", "Roar", "Die"
    };

    private static readonly Dictionary<string, string> MutantClips = new Dictionary<string, string>
    {
        { "Idle", MutantFolder + "/mutant breathing idle.fbx" },
        { "Walk", MutantFolder + "/mutant walking.fbx" },
        { "Attack_Melee", MutantFolder + "/mutant punch.fbx" },
        { "Punch", MutantFolder + "/mutant punch.fbx" },
        { "Swipe", MutantFolder + "/mutant swiping.fbx" },
        { "Attack_Throw", MutantFolder + "/mutant roaring.fbx" },
        { "JumpAttack", MutantFolder + "/mutant jump attack.fbx" },
        { "Hurt", MutantFolder + "/mutant swiping.fbx" },
        { "Death", MutantFolder + "/mutant dying.fbx" },
    };

    private static readonly Dictionary<string, string> MinotaurClips = new Dictionary<string, string>
    {
        { "Idle", MinotaurAnimationFolder + "/Minotaur@Idle.FBX" },
        { "Walk", MinotaurAnimationFolder + "/Minotaur@Walk.FBX" },
        { "Attack_Melee", MinotaurAnimationFolder + "/Minotaur@Attack.FBX" },
        { "Punch", MinotaurAnimationFolder + "/Minotaur@Attack.FBX" },
        { "Swipe", MinotaurAnimationFolder + "/Minotaur@Attack.FBX" },
        { "Attack_Throw", MinotaurAnimationFolder + "/Minotaur@Shout.FBX" },
        { "Hurt", MinotaurAnimationFolder + "/Minotaur@Get_Hit.FBX" },
        { "Death", MinotaurAnimationFolder + "/Minotaur@Death1.FBX" },
        { "JumpAttack", MinotaurAnimationFolder + "/Minotaur@Jump.FBX" },
    };

    [MenuItem("VR Boss/Boss 1 Setup (Level 2)", false, 1)]
    public static void SetupBoss1()
    {
        if (SetupBoss(Level2Path, "[BOSS 1] The Mutant Behemoth", "Mutant.fbx model",
            MutantModelPath, MutantControllerPath, MutantClips, false, 2.8f, "Level 3"))
            Debug.Log("Boss 1 (Level 2) setup complete");
    }

    [MenuItem("VR Boss/Boss 2 Setup (Level 4)", false, 2)]
    public static void SetupBoss2()
    {
        if (SetupBoss(Level4Path, "[BOSS 2] The Minotaur Warlord", "Mesh.FBX model",
            MinotaurModelPath, MinotaurControllerPath, MinotaurClips, true, 3.0f, "End Screen"))
            Debug.Log("Boss 2 (Level 4) setup complete");
    }

    private static bool SetupBoss(
        string scenePath, string bossName, string visualName, string modelPath,
        string controllerPath, Dictionary<string, string> stateClips, bool isMinotaur,
        float targetHeight, string nextScene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running boss setup; runtime objects cannot be saved to the scene.");
        if (!File.Exists(scenePath))
            throw new FileNotFoundException("Boss scene was not found.", scenePath);
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        ConfigureModelImports(modelPath, isMinotaur);
        ConfigureAnimatorController(controllerPath, stateClips);

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        RemoveExistingEncounter(scene);
        Transform playerOrigin = FindPlayerOrigin(scene);
        Transform playerView = FindPlayerView(scene, playerOrigin);
        if (playerOrigin != null)
            GetOrAdd<PlayerHealth>(playerOrigin.gameObject);

        GameObject boss = new GameObject(bossName);
        SceneManager.MoveGameObjectToScene(boss, scene);
        boss.name = bossName;
        boss.SetActive(true);

        GameObject visual = EnsureVisual(boss, visualName, modelPath);
        Animator animator = EnsureAnimator(visual, controllerPath, modelPath);
        EnableVisuals(visual);
        EnsureMaterials(boss, isMinotaur);
        ScaleVisualToHeight(boss, visual.transform, targetHeight);
        PlaceOnFloorInFrontOfPlayer(boss, playerOrigin, playerView, 7f);

        CapsuleCollider bodyCollider = GetOrAdd<CapsuleCollider>(boss);
        bodyCollider.isTrigger = false;
        bodyCollider.height = targetHeight;
        bodyCollider.center = new Vector3(0f, targetHeight * 0.5f, 0f);
        bodyCollider.radius = isMinotaur ? 1.5f : 1.4f;
        Rigidbody body = GetOrAdd<Rigidbody>(boss);
        body.isKinematic = true;
        body.useGravity = false;

        BossHealth health = GetOrAdd<BossHealth>(boss);
        SetInt(health, "maxHealth", 50);
        SetBool(health, "isInvulnerable", false);
        SetRendererArray(health, "meshRenderers", boss.GetComponentsInChildren<Renderer>(true));

        Transform projectileSpawn = EnsureProjectileSpawn(boss.transform, targetHeight);
        BossDamageHitbox meleeHitbox = EnsureMeleeHitbox(boss.transform, targetHeight);
        EnsureWeakPoint(boss.transform, health, targetHeight);

        BossController controller = GetOrAdd<BossController>(boss);
        SerializedObject serializedController = new SerializedObject(controller);
        SetReference(serializedController, "targetPlayer", playerView);
        SetReference(serializedController, "animator", animator);
        SetReference(serializedController, "projectileSpawnPoint", projectileSpawn);
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        BossDefeatTrigger defeat = GetOrAdd<BossDefeatTrigger>(boss);
        SerializedObject serializedDefeat = new SerializedObject(defeat);
        SetReference(serializedDefeat, "boss", health);
        SetValue(serializedDefeat, "loadNextSceneOnDefeat", true);
        SetValue(serializedDefeat, "nextSceneName", nextScene);
        serializedDefeat.ApplyModifiedPropertiesWithoutUndo();

        BossHealthBarUI healthBar = BossHealthBarUI.EnsureForBoss(health);
        if (healthBar != null)
            healthBar.transform.localPosition = new Vector3(0f, targetHeight + 0.5f, 0f);
        CreateThrowable(scene, playerOrigin, playerView);
        SetupHandCombat(playerOrigin);

        EditorUtility.SetDirty(boss);
        EditorUtility.SetDirty(health);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(defeat);
        EditorUtility.SetDirty(meleeHitbox);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()))
            throw new IOException("Boss setup could not save " + scenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = boss;
        SceneView.lastActiveSceneView?.FrameSelected();
        return true;
    }

    private static void SetupHandCombat(Transform playerOrigin)
    {
        if (playerOrigin == null)
            throw new InvalidOperationException("XR Origin is missing; cannot configure hand combat.");

        Transform[] hierarchy = playerOrigin.GetComponentsInChildren<Transform>(true);
        Transform leftHand = hierarchy.FirstOrDefault(item => item.name == "Left Hand Model");
        Transform rightHand = hierarchy.FirstOrDefault(item => item.name == "Right Hand Model");
        if (leftHand == null || rightHand == null)
            throw new InvalidOperationException("Both Left Hand Model and Right Hand Model must exist under XR Origin.");

        HapticImpulsePlayer leftHaptics = leftHand.GetComponentInParent<HapticImpulsePlayer>(true);
        HapticImpulsePlayer rightHaptics = rightHand.GetComponentInParent<HapticImpulsePlayer>(true);
        ConfigureHandCombat(leftHand, playerOrigin, true, rightHaptics);
        ConfigureHandCombat(rightHand, playerOrigin, false, leftHaptics);
    }

    private static void ConfigureHandCombat(
        Transform hand, Transform origin, bool isLeftHand, HapticImpulsePlayer otherHaptics)
    {
        SphereCollider fist = GetOrAdd<SphereCollider>(hand.gameObject);
        fist.radius = 0.15f;
        fist.isTrigger = true;
        Rigidbody body = GetOrAdd<Rigidbody>(hand.gameObject);
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        VRHandCombat combat = GetOrAdd<VRHandCombat>(hand.gameObject);
        combat.Configure(origin, isLeftHand, otherHaptics);
        PrefabUtility.RecordPrefabInstancePropertyModifications(fist);
        PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        PrefabUtility.RecordPrefabInstancePropertyModifications(combat);
        EditorUtility.SetDirty(fist);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(combat);
    }

    private static void ConfigureModelImports(string modelPath, bool isMinotaur)
    {
        List<string> paths = new List<string> { modelPath };
        string folder = isMinotaur ? MinotaurAnimationFolder : MutantFolder;
        paths.AddRange(Directory.GetFiles(folder, isMinotaur ? "Minotaur@*.FBX" : "*.fbx")
            .Select(path => path.Replace('\\', '/')));

        foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("Missing or invalid boss FBX: " + path);
                continue;
            }
            bool changed = importer.animationType != ModelImporterAnimationType.Human ||
                           importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            // Mutant.fbx was marked Humanoid but its mapping omitted the required
            // LeftHand bone. Merely checking isValid accepts the fallback Generic avatar.
            HumanDescription description = importer.humanDescription;
            if (!isMinotaur && description.skeleton != null &&
                description.skeleton.Any(bone => bone.name == "mixamorig:LeftHand") &&
                !description.human.Any(bone => bone.humanName == "LeftHand" && bone.boneName == "mixamorig:LeftHand"))
            {
                List<HumanBone> bones = description.human.Where(bone => bone.humanName != "LeftHand").ToList();
                bones.Add(new HumanBone
                {
                    humanName = "LeftHand", boneName = "mixamorig:LeftHand",
                    limit = new HumanLimit { useDefaultValues = true }
                });
                description.human = bones.ToArray();
                importer.humanDescription = description;
                changed = true;
            }
            if (isMinotaur && importer.animationRotationError < 0.5f)
            {
                importer.animationRotationError = 0.5f;
                changed = true;
            }
            if (changed)
                importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>()
                .FirstOrDefault(item => item.isValid && item.isHuman);
            if (avatar == null)
                throw new InvalidOperationException("FBX has no valid Humanoid avatar after import: " + path);
        }
    }

    private static void ConfigureAnimatorController(
        string controllerPath, Dictionary<string, string> stateClips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            throw new FileNotFoundException("Animator Controller was not found.", controllerPath);

        EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "IsStunned", AnimatorControllerParameterType.Bool);
        foreach (string parameter in TriggerParameters)
            EnsureParameter(controller, parameter, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>();
        foreach (KeyValuePair<string, string> binding in stateClips)
        {
            SetClipLoop(binding.Value, binding.Key == "Idle" || binding.Key == "Walk");
            AnimationClip clip = LoadAnimationClip(binding.Value);
            if (clip == null)
                throw new FileNotFoundException("No animation clip was found in " + binding.Value);
            AnimatorState state = FindState(machine, binding.Key) ?? machine.AddState(binding.Key);
            state.motion = clip;
            states[binding.Key] = state;
            EditorUtility.SetDirty(state);
        }

        machine.defaultState = states["Idle"];
        EnsureMovementTransitions(states["Idle"], states["Walk"]);
        EnsureTriggerTransition(machine, states["Attack_Melee"], "AttackMelee");
        EnsureTriggerTransition(machine, states["Punch"], "Punch");
        EnsureTriggerTransition(machine, states["Swipe"], "Swipe");
        EnsureTriggerTransition(machine, states["Attack_Throw"], "AttackThrow");
        EnsureTriggerTransition(machine, states["JumpAttack"], "JumpAttack");
        EnsureTriggerTransition(machine, states["Hurt"], "Hurt");
        EnsureTriggerTransition(machine, states["Death"], "Death");
        foreach (string stateName in new[] { "Attack_Melee", "Punch", "Swipe", "Attack_Throw", "JumpAttack", "Hurt" })
            EnsureReturnToIdle(states[stateName], states["Idle"]);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.SaveAssetIfDirty(controller);
        string savedYaml = File.ReadAllText(controllerPath);
        if (savedYaml.Contains("m_Motion: {fileID: 0}"))
            throw new InvalidOperationException("Controller still has an empty state after saving: " + controllerPath);
        foreach (KeyValuePair<string, AnimatorState> state in states)
            if (!(state.Value.motion is AnimationClip))
                throw new InvalidOperationException("Missing saved clip for " + state.Key);
        Debug.Log("[BossSetupMenu] Verified saved controller with " + states.Count + " non-empty states: " + controllerPath);
    }

    private static GameObject EnsureVisual(GameObject boss, string visualName, string modelPath)
    {
        Renderer existing = boss.GetComponentInChildren<Renderer>(true);
        if (existing != null)
        {
            Transform visualRoot = DirectChildOf(boss.transform, existing.transform);
            if (visualRoot != boss.transform)
            {
                visualRoot.name = visualName;
                return visualRoot.gameObject;
            }
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
            throw new FileNotFoundException("Boss model was not found.", modelPath);
        GameObject visual = PrefabUtility.InstantiatePrefab(model, boss.transform) as GameObject;
        if (visual == null)
            visual = UnityEngine.Object.Instantiate(model, boss.transform);
        visual.name = visualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        return visual;
    }

    private static Animator EnsureAnimator(GameObject visual, string controllerPath, string modelPath)
    {
        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = visual.AddComponent<Animator>();
        animator.enabled = true;
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>()
            .FirstOrDefault(avatar => avatar.isValid && avatar.isHuman);
        if (animator.avatar == null)
            throw new InvalidOperationException("Boss requires a valid Humanoid avatar: " + modelPath);
        animator.speed = 1f;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);
        EditorUtility.SetDirty(animator);
        return animator;
    }

    private static void EnableVisuals(GameObject visual)
    {
        visual.SetActive(true);
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
            if (renderer is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = true;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void EnsureMaterials(GameObject boss, bool isMinotaur)
    {
        Material mutant = AssetDatabase.LoadAssetAtPath<Material>(MutantFolder + "/Mutant_Material.mat");
        Material body = AssetDatabase.LoadAssetAtPath<Material>("Assets/animations/Minotaur/materials/Minotaur_Body_Mat.mat");
        Material pants = AssetDatabase.LoadAssetAtPath<Material>("Assets/animations/Minotaur/materials/Minotaur_Pants_Mat.mat");
        foreach (Renderer renderer in boss.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0)
                materials = new Material[1];
            for (int index = 0; index < materials.Length; index++)
            {
                if (materials[index] != null && !materials[index].name.Contains("Default"))
                    continue;
                materials[index] = isMinotaur && index > 0 ? pants : isMinotaur ? body : mutant;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static void ScaleVisualToHeight(GameObject boss, Transform visual, float targetHeight)
    {
        Bounds bounds = CalculateRendererBounds(boss);
        if (bounds.size.y < 0.01f)
            throw new InvalidOperationException("Boss model has no visible renderer bounds.");
        visual.localScale *= targetHeight / bounds.size.y;
        Physics.SyncTransforms();
    }

    private static void PlaceOnFloorInFrontOfPlayer(
        GameObject boss, Transform playerOrigin, Transform playerView, float distance)
    {
        Vector3 playerPosition = playerOrigin != null ? playerOrigin.position : Vector3.zero;
        Vector3 forward = playerView != null ? playerView.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        forward.Normalize();
        forward = FindClearDirection(playerPosition, playerView, forward, distance, boss);
        Vector3 target = playerPosition + forward * distance;
        float eyeY = playerView != null ? playerView.position.y : playerPosition.y + 1.36f;
        float playerFloor = FindFloorHeight(playerPosition, eyeY, playerPosition.y, boss);
        float floorY = FindFloorHeight(target, eyeY, playerFloor, boss);
        boss.transform.position = new Vector3(target.x, floorY, target.z);
        Vector3 look = playerPosition - boss.transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            boss.transform.rotation = Quaternion.LookRotation(look.normalized);
        Physics.SyncTransforms();
        Bounds bounds = CalculateRendererBounds(boss);
        Transform visual = boss.GetComponentInChildren<SkinnedMeshRenderer>(true).transform;
        visual = DirectChildOf(boss.transform, visual);
        // Keep the gameplay root on the floor; correct only the model's foot offset.
        visual.position += Vector3.up * (floorY - bounds.min.y);
        Physics.SyncTransforms();
    }

    private static Vector3 FindClearDirection(
        Vector3 playerPosition, Transform playerView, Vector3 preferred, float distance, GameObject boss)
    {
        Vector3 eyePosition = playerView != null
            ? playerView.position
            : playerPosition + Vector3.up * 1.6f;
        float[] angles = { 0f, -30f, 30f, -60f, 60f, -90f, 90f, 180f };

        foreach (float angle in angles)
        {
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * preferred;
            Vector3 chestTarget = playerPosition + direction * distance + Vector3.up * 1.8f;
            RaycastHit[] blockingHits = Physics.RaycastAll(
                eyePosition, (chestTarget - eyePosition).normalized,
                Vector3.Distance(eyePosition, chestTarget), Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            bool blocked = blockingHits.Any(hit =>
                !hit.transform.IsChildOf(boss.transform) &&
                (playerView == null || !hit.transform.IsChildOf(playerView.root)));
            if (!blocked)
                return direction.normalized;
        }

        Debug.LogWarning("No completely clear seven-metre boss path was found; using the player's forward direction.");
        return preferred;
    }

    private static float FindFloorHeight(Vector3 target, float eyeY, float fallbackY, GameObject boss)
    {
        Physics.SyncTransforms();
        // Start inside the room at player height, never above the ceiling.
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(target.x, eyeY, target.z),
            Vector3.down, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits.OrderBy(hit => hit.distance))
            if (hit.normal.y > 0.65f && !hit.transform.IsChildOf(boss.transform) &&
                hit.collider.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() == null &&
                hit.point.y <= fallbackY + 0.5f)
                return hit.point.y;
        return fallbackY;
    }

    private static Transform EnsureProjectileSpawn(Transform boss, float height)
    {
        Transform spawn = boss.Find("ProjectileSpawnPoint");
        if (spawn == null)
        {
            spawn = new GameObject("ProjectileSpawnPoint").transform;
            spawn.SetParent(boss, false);
        }
        spawn.localPosition = new Vector3(0f, height * 0.68f, 0.8f);
        return spawn;
    }

    private static BossDamageHitbox EnsureMeleeHitbox(Transform boss, float height)
    {
        Transform child = boss.Find("MeleeHitbox");
        if (child == null)
        {
            child = new GameObject("MeleeHitbox").transform;
            child.SetParent(boss, false);
        }
        child.localPosition = new Vector3(0f, height * 0.45f, 1.6f);
        BoxCollider collider = GetOrAdd<BoxCollider>(child.gameObject);
        collider.isTrigger = true;
        collider.size = new Vector3(2.4f, 2.2f, 3f);
        BossDamageHitbox hitbox = GetOrAdd<BossDamageHitbox>(child.gameObject);
        hitbox.SetDamage(10);
        hitbox.IsActive = false;
        return hitbox;
    }

    private static void EnsureWeakPoint(Transform boss, BossHealth health, float height)
    {
        Transform child = boss.Find("Chest_WeakPoint");
        if (child == null)
        {
            child = new GameObject("Chest_WeakPoint").transform;
            child.SetParent(boss, false);
        }
        child.localPosition = new Vector3(0f, height * 0.62f, 0.45f);
        SphereCollider collider = GetOrAdd<SphereCollider>(child.gameObject);
        collider.isTrigger = false;
        collider.radius = 0.35f;
        BossWeakPoint weakPoint = GetOrAdd<BossWeakPoint>(child.gameObject);
        SerializedObject serialized = new SerializedObject(weakPoint);
        SetReference(serialized, "parentBossHealth", health);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindPlayerOrigin(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name.IndexOf("XR Origin", StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
        Camera camera = GetSceneComponents<Camera>(scene).FirstOrDefault(item => item.CompareTag("MainCamera"));
        return camera != null ? camera.transform.root : null;
    }

    private static Transform FindPlayerView(Scene scene, Transform fallback)
    {
        Camera camera = GetSceneComponents<Camera>(scene).FirstOrDefault(item => item.CompareTag("MainCamera")) ??
                        GetSceneComponents<Camera>(scene).FirstOrDefault();
        return camera != null ? camera.transform : fallback;
    }

    private static GameObject FindBoss(Scene scene, string bossName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == bossName ||
                (bossName.Contains("BOSS 1") && child.name.Contains("BOSS 1")) ||
                (bossName.Contains("BOSS 2") && child.name.Contains("BOSS 2") && !child.name.Contains("Shadow")))
                return child.gameObject;
        return null;
    }

    private static void RemoveExistingEncounter(Scene scene)
    {
        string[] names = { "Boss", "Mutant", "Minotaur", "Boulder", "Projectile",
            "Throwable", "EnergyOrb", "WeakPoint", "Hitbox", "Shockwave", "Colossus", "Shadow Arch-Mage" };
        List<GameObject> obsolete = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (names.Any(name => child.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                obsolete.Add(child.gameObject);
        // Destroy topmost matching objects so a whole stale encounter is removed once.
        foreach (GameObject item in obsolete.Where(item => !obsolete.Any(parent =>
                     parent != item && item.transform.IsChildOf(parent.transform))).ToArray())
            if (item != null)
                UnityEngine.Object.DestroyImmediate(item);
    }

    private static void CreateThrowable(Scene scene, Transform playerOrigin, Transform playerView)
    {
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Throwable_EnergyOrb";
        SceneManager.MoveGameObjectToScene(orb, scene);
        Vector3 origin = playerOrigin != null ? playerOrigin.position : Vector3.zero;
        Vector3 forward = playerOrigin != null ? playerOrigin.forward : Vector3.forward;
        orb.transform.position = origin + forward * 1.2f + Vector3.up * 0.9f;
        orb.transform.localScale = Vector3.one * 0.35f;
        Rigidbody body = orb.AddComponent<Rigidbody>();
        body.mass = 2f;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        orb.AddComponent<XRGrabInteractable>();
        orb.AddComponent<ThrowableBossDamage>();
    }

    private static int HierarchyDepth(Transform transform)
    {
        int depth = 0;
        while (transform.parent != null) { depth++; transform = transform.parent; }
        return depth;
    }

    private static void EnsureParameter(AnimatorController controller, string name,
        AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter existing = controller.parameters.FirstOrDefault(parameter => parameter.name == name);
        if (existing != null && existing.type == type)
            return;
        if (existing != null)
            controller.RemoveParameter(existing);
        controller.AddParameter(name, type);
    }

    private static void EnsureMovementTransitions(AnimatorState idle, AnimatorState walk)
    {
        if (!HasTransition(idle.transitions, walk, "IsMoving", AnimatorConditionMode.If))
        {
            AnimatorStateTransition transition = idle.AddTransition(walk);
            transition.hasExitTime = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        }
        if (!HasTransition(walk.transitions, idle, "IsMoving", AnimatorConditionMode.IfNot))
        {
            AnimatorStateTransition transition = walk.AddTransition(idle);
            transition.hasExitTime = false;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        }
    }

    private static void EnsureTriggerTransition(AnimatorStateMachine machine, AnimatorState state, string trigger)
    {
        if (HasTransition(machine.anyStateTransitions, state, trigger, AnimatorConditionMode.If))
            return;
        AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
        transition.hasExitTime = false;
        transition.canTransitionToSelf = false;
        transition.duration = 0.08f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static bool HasTransition(AnimatorStateTransition[] transitions, AnimatorState destination,
        string parameter, AnimatorConditionMode mode)
    {
        return transitions.Any(transition => transition.destinationState == destination &&
            transition.conditions.Any(condition => condition.parameter == parameter && condition.mode == mode));
    }

    private static void EnsureReturnToIdle(AnimatorState state, AnimatorState idle)
    {
        if (state.transitions.Any(transition => transition.destinationState == idle))
            return;
        AnimatorStateTransition transition = state.AddTransition(idle);
        transition.hasExitTime = true;
        transition.exitTime = 0.95f;
        transition.duration = 0.1f;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (child.state != null && child.state.name == name)
                return child.state;
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            AnimatorState found = FindState(child.stateMachine, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void SetClipLoop(string path, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip.loopTime == loop) continue;
            clip.loopTime = loop;
            changed = true;
        }
        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip LoadAnimationClip(string path)
    {
        return AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    private static Transform DirectChildOf(Transform root, Transform descendant)
    {
        Transform current = descendant;
        while (current.parent != null && current.parent != root) current = current.parent;
        return current;
    }

    private static T[] GetSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            components.AddRange(root.GetComponentsInChildren<T>(true));
        return components.ToArray();
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetInt(UnityEngine.Object target, string name, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(UnityEngine.Object target, string name, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRendererArray(UnityEngine.Object target, string name, Renderer[] renderers)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.arraySize = renderers.Length;
            for (int index = 0; index < renderers.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(SerializedObject serialized, string name, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }

    private static void SetValue(SerializedObject serialized, string name, bool value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private static void SetValue(SerializedObject serialized, string name, string value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.stringValue = value;
    }
}



