using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Senior Unity Animation Importer & Builder.
/// Imports, configures Humanoid rigs/avatars, textures, materials,
/// builds AnimatorControllers, and wires animations for:
/// - Boss 1 (Mutant): Assets/animations/Creature Pack/
/// - Boss 2 (Minotaur): Assets/animations/Minotaur/
/// </summary>
public static class MutantRigAndAnimationBuilder
{
    private const string MUTANT_CREATURE_PACK_DIR = "Assets/animations/Creature Pack/";
    private const string MUTANT_FALLBACK_DIR = "Assets/Prefabs and 3d assets/Bosses/Mutant/";
    private const string MINOTAUR_ANIM_DIR = "Assets/animations/Minotaur/";
    private const string MINOTAUR_FALLBACK_DIR = "Assets/Prefabs and 3d assets/Bosses/Minotaur/";

    public static string GetMutantDirectory()
    {
        if (File.Exists(MUTANT_CREATURE_PACK_DIR + "Mutant.fbx"))
            return MUTANT_CREATURE_PACK_DIR;
        if (File.Exists("Assets/Animations/Creature Pack/Mutant.fbx"))
            return "Assets/Animations/Creature Pack/";
        return MUTANT_FALLBACK_DIR;
    }

    public static string GetMinotaurDirectory()
    {
        if (File.Exists(MINOTAUR_ANIM_DIR + "mesh/Mesh.FBX"))
            return MINOTAUR_ANIM_DIR;
        if (File.Exists("Assets/Animations/Minotaur/mesh/Mesh.FBX"))
            return "Assets/Animations/Minotaur/";
        return MINOTAUR_FALLBACK_DIR;
    }

    public static void RunFullSetup()
    {
        PrintFullInventory();
        BuildMutantSetup();
        BuildMinotaurSetup();
    }

    // ==============================================================
    // STEP 1: SCAN & INVENTORY
    // ==============================================================
    public static void PrintFullInventory()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=================================================");
        sb.AppendLine("📋 BOSS ANIMATION & ASSET INVENTORY REPORT");
        sb.AppendLine("=================================================");

        // Scan Boss 1 (Mutant)
        string mutantDir = GetMutantDirectory();
        sb.AppendLine($"\n[BOSS 1: MUTANT] Directory: {mutantDir}");
        ScanDirectoryAssets(mutantDir, sb);

        // Scan Boss 2 (Minotaur)
        string minotaurDir = GetMinotaurDirectory();
        sb.AppendLine($"\n[BOSS 2: MINOTAUR] Directory: {minotaurDir}");
        ScanDirectoryAssets(minotaurDir, sb);

        sb.AppendLine("=================================================");
        Debug.Log(sb.ToString());
    }

    private static void ScanDirectoryAssets(string dir, StringBuilder sb)
    {
        if (!Directory.Exists(dir))
        {
            sb.AppendLine($"  ⚠️ Directory not found: {dir}");
            return;
        }

        string[] fbxFiles = Directory.GetFiles(dir, "*.fbx", SearchOption.AllDirectories);
        string[] fbxUpper = Directory.GetFiles(dir, "*.FBX", SearchOption.AllDirectories);
        HashSet<string> allFbxs = new HashSet<string>(fbxFiles);
        foreach (var f in fbxUpper) allFbxs.Add(f);

        sb.AppendLine($"  Models & Animation FBX files ({allFbxs.Count}):");
        foreach (var fbx in allFbxs)
        {
            string unityPath = fbx.Replace("\\", "/");
            sb.AppendLine($"    • File: {unityPath}");
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(unityPath);
            List<string> clipNames = new List<string>();
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    clipNames.Add(clip.name);
                }
            }
            if (clipNames.Count > 0)
            {
                sb.AppendLine($"      Clips found: {string.Join(", ", clipNames)}");
            }
            else
            {
                sb.AppendLine("      Clips found: (None embedded or un-imported)");
            }
        }

        string[] textures = Directory.GetFiles(dir, "*.tga", SearchOption.AllDirectories);
        sb.AppendLine($"  Textures ({textures.Length}):");
        foreach (var t in textures)
        {
            sb.AppendLine($"    • {t.Replace("\\", "/")}");
        }

        string[] materials = Directory.GetFiles(dir, "*.mat", SearchOption.AllDirectories);
        sb.AppendLine($"  Materials ({materials.Length}):");
        foreach (var m in materials)
        {
            sb.AppendLine($"    • {m.Replace("\\", "/")}");
        }
    }

    // ==============================================================
    // STEP 2 & 3: CONFIGURE RIGS & BUILD ANIMATOR CONTROLLER (MUTANT)
    // ==============================================================
    public static void BuildMutantSetup()
    {
        string mutantDir = GetMutantDirectory();
        string fbxPath = mutantDir + "Mutant.fbx";

        if (!File.Exists(fbxPath))
        {
            Debug.LogWarning("[MutantBuilder] Mutant.fbx not found at " + fbxPath);
            return;
        }

        // 1. Setup Humanoid Rig on Mutant.fbx
        SetupModelRig(fbxPath);

        // Load generated Avatar from Mutant.fbx
        Avatar mutantAvatar = LoadAvatarFromFBX(fbxPath);
        if (mutantAvatar == null)
        {
            Debug.LogWarning("[MutantBuilder] Regenerating Avatar on Mutant.fbx...");
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            mutantAvatar = LoadAvatarFromFBX(fbxPath);
        }

        // 2. Setup Animation FBXs with matching Humanoid Avatar & Loop settings
        SetupAnimationFBX(mutantDir + "mutant breathing idle.fbx", mutantAvatar, "Idle", true);
        SetupAnimationFBX(mutantDir + "mutant walking.fbx", mutantAvatar, "Walk", true);
        SetupAnimationFBX(mutantDir + "mutant run.fbx", mutantAvatar, "Run", true);
        SetupAnimationFBX(mutantDir + "mutant idle.fbx", mutantAvatar, "Idle_Alt", true);
        SetupAnimationFBX(mutantDir + "mutant punch.fbx", mutantAvatar, "Punch", false);
        SetupAnimationFBX(mutantDir + "mutant swiping.fbx", mutantAvatar, "Swipe", false);
        SetupAnimationFBX(mutantDir + "mutant jump attack.fbx", mutantAvatar, "JumpAttack", false);
        SetupAnimationFBX(mutantDir + "mutant roaring.fbx", mutantAvatar, "Roar", false);
        SetupAnimationFBX(mutantDir + "mutant flexing muscles.fbx", mutantAvatar, "Hurt", false);
        SetupAnimationFBX(mutantDir + "mutant dying.fbx", mutantAvatar, "Death", false);

        // 3. Create or Update AnimatorController
        AnimatorController controller = CreateOrUpdateMutantAnimatorController(mutantDir);

        // 4. Create proper URP Material for Mutant
        Material mutantMat = CreateOrGetMutantMaterial(mutantDir);

        // 5. Apply to any active boss in the current open scene
        ApplyToSceneBoss(controller, mutantAvatar, mutantMat);

        Debug.Log("✅ [MutantBuilder] Boss 1 (Mutant) rigs, avatar, animations, materials & AnimatorController configured successfully!");
    }

    private static void SetupModelRig(string fbxPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        bool needsReimport = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            needsReimport = true;
        }

        if (needsReimport)
        {
            importer.SaveAndReimport();
        }
    }

    private static Avatar LoadAvatarFromFBX(string fbxPath)
    {
        if (!File.Exists(fbxPath)) return null;
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (var obj in assets)
        {
            if (obj is Avatar avatar && avatar.isValid)
            {
                return avatar;
            }
        }
        return null;
    }

    private static void SetupAnimationFBX(string fbxPath, Avatar sourceAvatar, string clipName, bool loop)
    {
        if (!File.Exists(fbxPath)) return;

        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        bool modified = false;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            if (sourceAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sourceAvatar;
            }
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            modified = true;
        }

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            foreach (var clip in clips)
            {
                clip.name = clipName;
                clip.loopTime = loop;
            }
            importer.clipAnimations = clips;
            modified = true;
        }

        if (modified)
        {
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip LoadClipFromFBX(string fbxPath)
    {
        if (!File.Exists(fbxPath)) return null;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (var obj in assets)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }

    private static AnimatorController CreateOrUpdateMutantAnimatorController(string mutantDir)
    {
        string controllerPath = mutantDir + "MutantAnimatorController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        // Required Parameters matching specifications
        EnsureParameter(controller, "AttackMelee", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "AttackThrow", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Punch", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Swipe", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "JumpAttack", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Roar", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "IsStunned", AnimatorControllerParameterType.Bool);

        // Load Clips
        AnimationClip idleClip = LoadClipFromFBX(mutantDir + "mutant breathing idle.fbx");
        AnimationClip walkClip = LoadClipFromFBX(mutantDir + "mutant walking.fbx");
        AnimationClip punchClip = LoadClipFromFBX(mutantDir + "mutant punch.fbx");
        AnimationClip swipeClip = LoadClipFromFBX(mutantDir + "mutant swiping.fbx");
        AnimationClip jumpClip = LoadClipFromFBX(mutantDir + "mutant jump attack.fbx");
        AnimationClip roarClip = LoadClipFromFBX(mutantDir + "mutant roaring.fbx");
        AnimationClip hurtClip = LoadClipFromFBX(mutantDir + "mutant flexing muscles.fbx") ?? swipeClip;
        AnimationClip dieClip = LoadClipFromFBX(mutantDir + "mutant dying.fbx");

        AnimatorStateMachine rootSM = controller.layers[0].stateMachine;

        // Clear existing states to ensure clean, deterministic rebuild
        var states = rootSM.states;
        foreach (var s in states)
        {
            rootSM.RemoveState(s.state);
        }

        // 1. Entry -> Idle State (Default, fixes T-pose)
        AnimatorState idleState = rootSM.AddState("Idle");
        idleState.motion = idleClip;
        rootSM.defaultState = idleState;

        // 2. Walk State (Idle <-> Walk via IsMoving)
        AnimatorState walkState = rootSM.AddState("Walk");
        walkState.motion = walkClip;

        var toWalk = idleState.AddTransition(walkState);
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        toWalk.hasExitTime = false;
        toWalk.duration = 0.2f;

        var toIdle = walkState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.2f;

        // 3. Attack_Melee (triggered by AttackMelee, Punch, or Swipe)
        AnimatorState meleeState = rootSM.AddState("Attack_Melee");
        meleeState.motion = punchClip ?? swipeClip;
        AddTriggerTransition(rootSM, meleeState, "AttackMelee");
        AddTriggerTransition(rootSM, meleeState, "Punch");
        AddTriggerTransition(rootSM, meleeState, "Swipe");
        var meleeReturn = meleeState.AddTransition(idleState);
        meleeReturn.hasExitTime = true;
        meleeReturn.exitTime = 0.9f;
        meleeReturn.duration = 0.2f;

        // 4. Attack_Throw (triggered by AttackThrow or Roar)
        AnimatorState throwState = rootSM.AddState("Attack_Throw");
        throwState.motion = roarClip;
        AddTriggerTransition(rootSM, throwState, "AttackThrow");
        AddTriggerTransition(rootSM, throwState, "Roar");
        var throwReturn = throwState.AddTransition(idleState);
        throwReturn.hasExitTime = true;
        throwReturn.exitTime = 0.9f;
        throwReturn.duration = 0.2f;

        // 5. Hurt (triggered by Hurt or IsStunned)
        AnimatorState hurtState = rootSM.AddState("Hurt");
        hurtState.motion = hurtClip;
        AddTriggerTransition(rootSM, hurtState, "Hurt");
        var stunTrans = rootSM.AddAnyStateTransition(hurtState);
        stunTrans.AddCondition(AnimatorConditionMode.If, 0, "IsStunned");
        stunTrans.hasExitTime = false;
        stunTrans.duration = 0.15f;
        var hurtReturn = hurtState.AddTransition(idleState);
        hurtReturn.hasExitTime = true;
        hurtReturn.exitTime = 0.85f;
        hurtReturn.duration = 0.2f;

        // 6. Death (triggered by Death or Die)
        AnimatorState deathState = rootSM.AddState("Death");
        deathState.motion = dieClip;
        AddTriggerTransition(rootSM, deathState, "Death");
        AddTriggerTransition(rootSM, deathState, "Die");

        // 7. JumpAttack State
        AnimatorState jumpState = rootSM.AddState("JumpAttack");
        jumpState.motion = jumpClip;
        AddTriggerTransition(rootSM, jumpState, "JumpAttack");
        var jumpReturn = jumpState.AddTransition(idleState);
        jumpReturn.hasExitTime = true;
        jumpReturn.exitTime = 0.9f;
        jumpReturn.duration = 0.25f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        return controller;
    }

    private static void AddTriggerTransition(AnimatorStateMachine sm, AnimatorState targetState, string triggerName)
    {
        var trans = sm.AddAnyStateTransition(targetState);
        trans.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        trans.hasExitTime = false;
        trans.duration = 0.15f;
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name) return;
        }
        controller.AddParameter(name, type);
    }

    private static Material CreateOrGetMutantMaterial(string mutantDir)
    {
        string materialPath = mutantDir + "Mutant_Material.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(litShader);
            mat.name = "Mutant_Material";

            // Dark monstrous creature coloration
            mat.color = new Color(0.28f, 0.32f, 0.35f); // Ash/Demon Flesh
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);

            AssetDatabase.CreateAsset(mat, materialPath);
        }
        return mat;
    }

    // ==============================================================
    // STEP 5: SECOND BOSS (MINOTAUR) PREP
    // ==============================================================
    public static void BuildMinotaurSetup()
    {
        string minotaurDir = GetMinotaurDirectory();
        string meshPath = minotaurDir + "mesh/Mesh.FBX";

        if (!File.Exists(meshPath))
        {
            Debug.LogWarning("[MinotaurBuilder] Minotaur mesh not found at " + meshPath);
            return;
        }

        // 1. Setup Rig on Mesh.FBX
        SetupModelRig(meshPath);
        Avatar minotaurAvatar = LoadAvatarFromFBX(meshPath);
        if (minotaurAvatar == null)
        {
            AssetDatabase.ImportAsset(meshPath, ImportAssetOptions.ForceUpdate);
            minotaurAvatar = LoadAvatarFromFBX(meshPath);
        }

        // 2. Setup Animation FBXs with matching Avatar & Loop settings
        string animDir = minotaurDir + "animation/";
        SetupAnimationFBX(animDir + "Minotaur@Idle.FBX", minotaurAvatar, "Idle", true);
        SetupAnimationFBX(animDir + "Minotaur@Walk.FBX", minotaurAvatar, "Walk", true);
        SetupAnimationFBX(animDir + "Minotaur@Run.FBX", minotaurAvatar, "Run", true);
        SetupAnimationFBX(animDir + "Minotaur@Attack.FBX", minotaurAvatar, "Attack_Melee", false);
        SetupAnimationFBX(animDir + "Minotaur@Shout.FBX", minotaurAvatar, "Attack_Throw", false);
        SetupAnimationFBX(animDir + "Minotaur@Get_Hit.FBX", minotaurAvatar, "Hurt", false);
        SetupAnimationFBX(animDir + "Minotaur@Death1.FBX", minotaurAvatar, "Death", false);
        SetupAnimationFBX(animDir + "Minotaur@Death2.FBX", minotaurAvatar, "Death2", false);
        SetupAnimationFBX(animDir + "Minotaur@Jump.FBX", minotaurAvatar, "Jump", false);

        // 3. Setup Textures (Normal Maps)
        SetupNormalMap(minotaurDir + "texture/Minotaur_normals.tga");
        SetupNormalMap(minotaurDir + "texture/pants_normals.tga");

        // 4. Create URP Materials for Minotaur
        CreateMinotaurMaterials(minotaurDir);

        // 5. Build Minotaur Animator Controller
        CreateOrUpdateMinotaurAnimatorController(minotaurDir);

        Debug.Log("✅ [MinotaurBuilder] Boss 2 (Minotaur) rigs, avatar, animations, materials & AnimatorController prepared successfully!");
    }

    private static void SetupNormalMap(string texPath)
    {
        if (!File.Exists(texPath)) return;
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }

    private static void CreateMinotaurMaterials(string minotaurDir)
    {
        string matDir = minotaurDir + "materials/";
        if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Body Material
        string bodyMatPath = matDir + "Minotaur_Body_Mat.mat";
        Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>(bodyMatPath);
        if (bodyMat == null)
        {
            bodyMat = new Material(litShader);
            bodyMat.name = "Minotaur_Body_Mat";
            Texture2D diff = AssetDatabase.LoadAssetAtPath<Texture2D>(minotaurDir + "texture/Minotaur_diffuse.tga");
            Texture2D norm = AssetDatabase.LoadAssetAtPath<Texture2D>(minotaurDir + "texture/Minotaur_normals.tga");
            if (diff != null) bodyMat.mainTexture = diff;
            if (norm != null && bodyMat.HasProperty("_BumpMap")) bodyMat.SetTexture("_BumpMap", norm);
            AssetDatabase.CreateAsset(bodyMat, bodyMatPath);
        }

        // Pants Material
        string pantsMatPath = matDir + "Minotaur_Pants_Mat.mat";
        Material pantsMat = AssetDatabase.LoadAssetAtPath<Material>(pantsMatPath);
        if (pantsMat == null)
        {
            pantsMat = new Material(litShader);
            pantsMat.name = "Minotaur_Pants_Mat";
            Texture2D diff = AssetDatabase.LoadAssetAtPath<Texture2D>(minotaurDir + "texture/Pants_diffuse.tga");
            Texture2D norm = AssetDatabase.LoadAssetAtPath<Texture2D>(minotaurDir + "texture/pants_normals.tga");
            if (diff != null) pantsMat.mainTexture = diff;
            if (norm != null && pantsMat.HasProperty("_BumpMap")) pantsMat.SetTexture("_BumpMap", norm);
            AssetDatabase.CreateAsset(pantsMat, pantsMatPath);
        }
    }

    private static AnimatorController CreateOrUpdateMinotaurAnimatorController(string minotaurDir)
    {
        string controllerPath = minotaurDir + "MinotaurAnimatorController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        EnsureParameter(controller, "AttackMelee", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "AttackThrow", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);

        string animDir = minotaurDir + "animation/";
        AnimationClip idleClip = LoadClipFromFBX(animDir + "Minotaur@Idle.FBX");
        AnimationClip walkClip = LoadClipFromFBX(animDir + "Minotaur@Walk.FBX");
        AnimationClip attackClip = LoadClipFromFBX(animDir + "Minotaur@Attack.FBX");
        AnimationClip shoutClip = LoadClipFromFBX(animDir + "Minotaur@Shout.FBX");
        AnimationClip hitClip = LoadClipFromFBX(animDir + "Minotaur@Get_Hit.FBX");
        AnimationClip deathClip = LoadClipFromFBX(animDir + "Minotaur@Death1.FBX");

        AnimatorStateMachine rootSM = controller.layers[0].stateMachine;

        var states = rootSM.states;
        foreach (var s in states) rootSM.RemoveState(s.state);

        // 1. Idle (Default, Entry -> Idle)
        AnimatorState idleState = rootSM.AddState("Idle");
        idleState.motion = idleClip;
        rootSM.defaultState = idleState;

        // 2. Walk
        AnimatorState walkState = rootSM.AddState("Walk");
        walkState.motion = walkClip;
        var toWalk = idleState.AddTransition(walkState);
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        toWalk.hasExitTime = false;
        toWalk.duration = 0.2f;

        var toIdle = walkState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.2f;

        // 3. Attack_Melee
        AnimatorState meleeState = rootSM.AddState("Attack_Melee");
        meleeState.motion = attackClip;
        AddTriggerTransition(rootSM, meleeState, "AttackMelee");
        var toIdleMelee = meleeState.AddTransition(idleState);
        toIdleMelee.hasExitTime = true;
        toIdleMelee.exitTime = 0.9f;
        toIdleMelee.duration = 0.2f;

        // 4. Attack_Throw (Shout / Roar)
        AnimatorState throwState = rootSM.AddState("Attack_Throw");
        throwState.motion = shoutClip;
        AddTriggerTransition(rootSM, throwState, "AttackThrow");
        var toIdleThrow = throwState.AddTransition(idleState);
        toIdleThrow.hasExitTime = true;
        toIdleThrow.exitTime = 0.9f;
        toIdleThrow.duration = 0.2f;

        // 5. Hurt
        AnimatorState hurtState = rootSM.AddState("Hurt");
        hurtState.motion = hitClip;
        AddTriggerTransition(rootSM, hurtState, "Hurt");
        var toIdleHurt = hurtState.AddTransition(idleState);
        toIdleHurt.hasExitTime = true;
        toIdleHurt.exitTime = 0.85f;
        toIdleHurt.duration = 0.2f;

        // 6. Death
        AnimatorState deathState = rootSM.AddState("Death");
        deathState.motion = deathClip;
        AddTriggerTransition(rootSM, deathState, "Death");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        return controller;
    }

    // ==============================================================
    // STEP 6: APPLY TO ACTIVE BOSSES IN SCENE
    // ==============================================================
    public static void ApplyToSceneBoss(AnimatorController controller, Avatar avatar, Material mat)
    {
        // 1. Check all BossControllers
        BossController[] bosses = UnityEngine.Object.FindObjectsByType<BossController>(FindObjectsSortMode.None);
        foreach (var boss in bosses)
        {
            ApplyToTarget(boss.gameObject, controller, avatar, mat);
        }

        // 2. Check all BossHealth
        BossHealth[] healths = UnityEngine.Object.FindObjectsByType<BossHealth>(FindObjectsSortMode.None);
        foreach (var h in healths)
        {
            ApplyToTarget(h.gameObject, controller, avatar, mat);
        }

        // 3. Check GameObjects named [BOSS or Mutant
        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go.name.Contains("[BOSS") || go.name.Contains("Mutant"))
            {
                ApplyToTarget(go, controller, avatar, mat);
            }
        }

        // Permanently mark scene dirty if not in Play mode
        if (!EditorApplication.isPlaying)
        {
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }
    }

    public static void ApplyToSceneBossManual()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[MutantBuilder] Unity is currently in PLAY MODE! Stop Play mode first (click the Play button to stop), then run this to permanently save your scene!");
            return;
        }

        string mutantDir = GetMutantDirectory();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(mutantDir + "MutantAnimatorController.controller");
        Avatar avatar = LoadAvatarFromFBX(mutantDir + "Mutant.fbx");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(mutantDir + "Mutant_Material.mat");

        ApplyToSceneBoss(controller, avatar, mat);

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("✅ [MutantBuilder] Successfully applied AnimatorController, Avatar, and Material to the scene boss and PERMANENTLY SAVED the open scene file!");
    }

    private static void ApplyToTarget(GameObject root, AnimatorController controller, Avatar avatar, Material mat)
    {
        Animator anim = root.GetComponentInChildren<Animator>();
        if (anim == null) anim = root.GetComponentInParent<Animator>();

        if (anim != null)
        {
            if (controller != null) anim.runtimeAnimatorController = controller;
            if (avatar != null) anim.avatar = avatar;
            anim.applyRootMotion = false;
            EditorUtility.SetDirty(anim.gameObject);
        }

        SkinnedMeshRenderer smr = root.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null && mat != null)
        {
            smr.sharedMaterial = mat;
            EditorUtility.SetDirty(smr.gameObject);
        }
    }
}
