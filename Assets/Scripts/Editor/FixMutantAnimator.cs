using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Repairs the Mutant and Minotaur animator controllers from their FBX clips.
/// </summary>
public static class FixMutantAnimator
{
    private const string AutoRepairSessionKey = "VRBoss.AnimatorAutoRepairComplete";
    private const string MutantFolder = "Assets/animations/Creature Pack";
    private const string MutantControllerPath = MutantFolder + "/MutantAnimatorController.controller";
    private const string MutantDownloadFolder = @"C:\Users\Yato\Downloads\Creature Pack";

    private const string MinotaurFolder = "Assets/animations/Minotaur/animation";
    private const string MinotaurControllerPath = "Assets/animations/Minotaur/MinotaurAnimatorController.controller";
    private const string MinotaurDownloadFolder = @"C:\Users\Yato\Downloads\Minotaur\Minotaur";

    private static readonly Dictionary<string, string> MutantClips = new Dictionary<string, string>
    {
        { "Idle", MutantFolder + "/mutant breathing idle.fbx" },
        { "Walk", MutantFolder + "/mutant walking.fbx" },
        { "Attack_Melee", MutantFolder + "/mutant punch.fbx" },
        { "Attack_Throw", MutantFolder + "/mutant roaring.fbx" },
        { "JumpAttack", MutantFolder + "/mutant jump attack.fbx" },
        { "Hurt", MutantFolder + "/mutant swiping.fbx" },
        { "Death", MutantFolder + "/mutant dying.fbx" },
    };

    private static readonly Dictionary<string, string> MinotaurClips = new Dictionary<string, string>
    {
        { "Idle", MinotaurFolder + "/Minotaur@Idle.FBX" },
        { "Walk", MinotaurFolder + "/Minotaur@Walk.FBX" },
        { "Attack_Melee", MinotaurFolder + "/Minotaur@Attack.FBX" },
        { "Attack_Throw", MinotaurFolder + "/Minotaur@Shout.FBX" },
        { "JumpAttack", MinotaurFolder + "/Minotaur@Jump.FBX" },
        { "Hurt", MinotaurFolder + "/Minotaur@Get_Hit.FBX" },
        { "Death", MinotaurFolder + "/Minotaur@Death1.FBX" },
    };

    private static readonly string[] GenericMutantFbxFiles =
    {
        "jump attack.fbx",
        "left turn 45.fbx",
        "mutant flexing muscles.fbx",
        "mutant idle (2).fbx",
        "mutant jumping (2).fbx",
        "mutant jumping.fbx",
        "mutant left turn 45.fbx",
        "mutant right turn 45.fbx",
        "mutant right turn 45 (2).fbx",
        "mutant right turn 90.fbx",
    };

    private static void ScheduleAutomaticRepair()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += RunAutomaticRepairWhenReady;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += RunAutomaticRepairWhenReady;
    }

    private static void RunAutomaticRepairWhenReady()
    {
        if (SessionState.GetBool(AutoRepairSessionKey, false) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunAutomaticRepairWhenReady;
            return;
        }

        SessionState.SetBool(AutoRepairSessionKey, true);

        try
        {
            FixGenericFbxFilesToHumanoid();
            FixMutantAnimatorClips();
            FixMinotaurAnimatorClips();
            Debug.Log("[FixMutantAnimator] Automatic boss animation repair completed.");
        }
        catch (System.Exception exception)
        {
            SessionState.SetBool(AutoRepairSessionKey, false);
            Debug.LogException(exception);
        }
    }

    public static void FixMutantAnimatorClips()
    {
        FixController(MutantControllerPath, MutantClips, MutantDownloadFolder);
    }

    public static void FixGenericFbxFilesToHumanoid()
    {
        int fixedCount = 0;

        foreach (string fileName in GenericMutantFbxFiles)
        {
            string assetPath = MutantFolder + "/" + fileName;
            if (!File.Exists(assetPath))
            {
                LogMissingFile(fileName, MutantDownloadFolder, MutantFolder);
                continue;
            }

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[FixMutantAnimator] The asset is not a model importer: " + assetPath);
                continue;
            }

            if (importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
                fixedCount++;
            }
        }

        Debug.Log($"[FixMutantAnimator] Reimported {fixedCount} Creature Pack FBX files as Humanoid.");
    }

    public static void FixMinotaurAnimatorClips()
    {
        FixController(MinotaurControllerPath, MinotaurClips, MinotaurDownloadFolder);
    }

    private static void FixController(
        string controllerPath,
        Dictionary<string, string> stateClipPaths,
        string downloadFolder)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError("[FixMutantAnimator] Animator Controller not found: " + controllerPath);
            return;
        }

        EnsureParameters(controller);
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
        if (FindState(rootStateMachine, "Idle") == null)
            rootStateMachine.AddState("Idle");

        int assignedCount = 0;

        foreach (KeyValuePair<string, string> binding in stateClipPaths)
        {
            string fileName = Path.GetFileName(binding.Value);
            if (!File.Exists(binding.Value))
            {
                string destinationFolder = Path.GetDirectoryName(binding.Value).Replace('\\', '/');
                LogMissingFile(fileName, downloadFolder, destinationFolder);
                continue;
            }

            bool shouldLoop = binding.Key == "Idle" || binding.Key == "Walk";
            SetClipLoopTime(binding.Value, shouldLoop);

            AnimationClip clip = LoadAnimationClip(binding.Value);
            if (clip == null)
            {
                Debug.LogError("[FixMutantAnimator] No AnimationClip sub-asset found in " + binding.Value);
                continue;
            }

            AnimatorState state = FindState(rootStateMachine, binding.Key);
            bool stateWasCreated = state == null;
            if (stateWasCreated)
                state = rootStateMachine.AddState(binding.Key);

            state.motion = clip;
            EditorUtility.SetDirty(state);
            assignedCount++;

            if (stateWasCreated)
                AddTransitionsForNewState(rootStateMachine, state, binding.Key);
        }

        AnimatorState idleState = FindState(rootStateMachine, "Idle");
        if (idleState != null)
            rootStateMachine.defaultState = idleState;

        EditorUtility.SetDirty(rootStateMachine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FixMutantAnimator] Assigned {assignedCount} clips in {controllerPath}.");
    }

    private static AnimationClip LoadAnimationClip(string assetPath)
    {
        Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
        foreach (Object subAsset in subAssets)
        {
            AnimationClip clip = subAsset as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        return null;
    }

    private static void SetClipLoopTime(string assetPath, bool shouldLoop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip.loopTime == shouldLoop)
                continue;

            clip.loopTime = shouldLoop;
            changed = true;
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state != null && childState.state.name == stateName)
                return childState.state;
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            AnimatorState state = FindState(childMachine.stateMachine, stateName);
            if (state != null)
                return state;
        }

        return null;
    }

    private static void EnsureParameters(AnimatorController controller)
    {
        EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "IsStunned", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "AttackMelee", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "AttackThrow", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Punch", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Swipe", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "JumpAttack", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Roar", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
            {
                if (parameter.type == parameterType)
                    return;

                controller.RemoveParameter(parameter);
                break;
            }
        }

        controller.AddParameter(parameterName, parameterType);
    }

    private static void AddTransitionsForNewState(
        AnimatorStateMachine stateMachine,
        AnimatorState state,
        string stateName)
    {
        AnimatorState idle = FindState(stateMachine, "Idle");

        if (stateName == "Idle")
            return;

        if (stateName == "Walk")
        {
            if (idle != null)
            {
                AnimatorStateTransition toWalk = idle.AddTransition(state);
                toWalk.hasExitTime = false;
                toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

                AnimatorStateTransition toIdle = state.AddTransition(idle);
                toIdle.hasExitTime = false;
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            }
            return;
        }

        string triggerName = StateNameToTrigger(stateName);
        if (!string.IsNullOrEmpty(triggerName))
        {
            AnimatorStateTransition attackTransition = stateMachine.AddAnyStateTransition(state);
            attackTransition.hasExitTime = false;
            attackTransition.duration = 0.08f;
            attackTransition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        if (idle != null && stateName != "Death")
        {
            AnimatorStateTransition returnToIdle = state.AddTransition(idle);
            returnToIdle.hasExitTime = true;
            returnToIdle.exitTime = 0.95f;
            returnToIdle.duration = 0.1f;
        }
    }

    private static string StateNameToTrigger(string stateName)
    {
        switch (stateName)
        {
            case "Attack_Melee": return "AttackMelee";
            case "Attack_Throw": return "AttackThrow";
            case "JumpAttack": return "JumpAttack";
            case "Hurt": return "Hurt";
            case "Death": return "Death";
            default: return string.Empty;
        }
    }

    private static void LogMissingFile(string fileName, string sourceFolder, string destinationFolder)
    {
        string source = Path.Combine(sourceFolder, fileName);
        string destination = destinationFolder.TrimEnd('/', '\\') + "/" + fileName;
        Debug.LogError(
            "[FixMutantAnimator] Missing animation file. Copy exactly:\n" +
            "Source: " + source + "\n" +
            "Destination: " + destination);
    }
}
