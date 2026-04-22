using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[CustomEditor(typeof(BattleUnit))]
public class BattleUnitEditor : Editor
{
    // 战斗入场动画区块内需要手动绘制的字段名列表
    static readonly string[] enterAnimFieldNames = new[]
    {
        "playBattleEnterAnimation",
        "enterAnimationStateName",
        "randomizeEnterAnimationStartTime",
        "waitForEnterAnimationToFinish",
        "enterAnimationFallbackDuration",
        "playBattleEnterMove",
        "enterMoveDistance",
        "enterMoveDuration",
    };

    // 战斗待机动画区块内需要手动绘制的字段名列表（按字段声明顺序）
    static readonly string[] idleAnimFieldNames = new[]
    {
        "idleAnimationSource",
        "battleAnimator",
        "idleAnimationStateName",          // 由搜索 UI 替代，不单独 DrawProperty
        "randomizeAnimatorIdleStartTime",
        "idleAnimationFrames",
        "idleAnimationFrameInterval",
        "idleAnimationLoop",
        "randomizeIdleStartFrame",
        "battleSpriteRenderer",
    };

    SerializedProperty idleAnimationSourceProp;
    SerializedProperty battleAnimatorProp;
    SerializedProperty idleAnimationStateNameProp;
    SerializedProperty playBattleEnterAnimationProp;
    SerializedProperty enterAnimationStateNameProp;

    string idleStateSearchText = string.Empty;
    string enterStateSearchText = string.Empty;

    void OnEnable()
    {
        idleAnimationSourceProp = serializedObject.FindProperty("idleAnimationSource");
        battleAnimatorProp      = serializedObject.FindProperty("battleAnimator");
        idleAnimationStateNameProp = serializedObject.FindProperty("idleAnimationStateName");
        playBattleEnterAnimationProp = serializedObject.FindProperty("playBattleEnterAnimation");
        enterAnimationStateNameProp = serializedObject.FindProperty("enterAnimationStateName");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 排除脚本字段和整个待机动画区块，在合适位置手动插入
        var excludes = new List<string>(enterAnimFieldNames.Length + idleAnimFieldNames.Length + 1) { "m_Script" };
        excludes.AddRange(enterAnimFieldNames);
        excludes.AddRange(idleAnimFieldNames);
        DrawPropertiesExcluding(serializedObject, excludes.ToArray());

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("战斗入场动画", EditorStyles.boldLabel);
        DrawEnterAnimationBlock();

        // 手动绘制战斗待机动画区块
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("战斗待机动画", EditorStyles.boldLabel);
        DrawIdleAnimationBlock();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawEnterAnimationBlock()
    {
        EditorGUILayout.PropertyField(playBattleEnterAnimationProp, new GUIContent("Play Battle Enter Animation"));

        if (playBattleEnterAnimationProp == null || !playBattleEnterAnimationProp.boolValue)
            return;

        DrawAnimatorStatePicker(
            enterAnimationStateNameProp,
            ref enterStateSearchText,
            "Enter State Name",
            "搜索入场动画名",
            "匹配入场动画");

        SerializedProperty randomStartProp = serializedObject.FindProperty("randomizeEnterAnimationStartTime");
        if (randomStartProp != null)
            EditorGUILayout.PropertyField(randomStartProp, new GUIContent("Randomize Enter Animation Start Time"));

        SerializedProperty waitForFinishProp = serializedObject.FindProperty("waitForEnterAnimationToFinish");
        if (waitForFinishProp != null)
            EditorGUILayout.PropertyField(waitForFinishProp, new GUIContent("Wait For Enter Animation To Finish"));

        SerializedProperty fallbackDurationProp = serializedObject.FindProperty("enterAnimationFallbackDuration");
        if (fallbackDurationProp != null)
            EditorGUILayout.PropertyField(fallbackDurationProp, new GUIContent("Enter Animation Fallback Duration"));

        SerializedProperty playEnterMoveProp = serializedObject.FindProperty("playBattleEnterMove");
        if (playEnterMoveProp != null)
            EditorGUILayout.PropertyField(playEnterMoveProp, new GUIContent("Play Battle Enter Move"));

        if (playEnterMoveProp != null && playEnterMoveProp.boolValue)
        {
            SerializedProperty enterMoveDistanceProp = serializedObject.FindProperty("enterMoveDistance");
            if (enterMoveDistanceProp != null)
                EditorGUILayout.PropertyField(enterMoveDistanceProp, new GUIContent("Enter Move Distance"));

            SerializedProperty enterMoveDurationProp = serializedObject.FindProperty("enterMoveDuration");
            if (enterMoveDurationProp != null)
                EditorGUILayout.PropertyField(enterMoveDurationProp, new GUIContent("Enter Move Duration"));
        }
    }

    // 在待机动画区块里绘制所有相关字段，并将搜索 UI 嵌入 idleAnimationStateName 处
    void DrawIdleAnimationBlock()
    {
        EditorGUILayout.PropertyField(idleAnimationSourceProp, new GUIContent("Idle Animation Source"));
        EditorGUILayout.PropertyField(battleAnimatorProp,      new GUIContent("Battle Animator"));

        // --- 状态名 + 搜索下拉（嵌入在此处）---
        DrawAnimatorStatePicker(
            idleAnimationStateNameProp,
            ref idleStateSearchText,
            "Idle State Name",
            "搜索待机动画名",
            "匹配待机动画");

        bool isAnimatorMode = idleAnimationSourceProp != null &&
                              idleAnimationSourceProp.enumValueIndex != (int)BattleIdleAnimationSource.SpriteFrames;
        if (isAnimatorMode)
        {
            SerializedProperty randProp = serializedObject.FindProperty("randomizeAnimatorIdleStartTime");
            if (randProp != null)
                EditorGUILayout.PropertyField(randProp, new GUIContent("Randomize Animator Idle Start Time"));
        }

        // 逐帧相关字段始终显示（SpriteFrames 或 Auto 回退时有用）
        string[] spriteFrameFields = new[]
        {
            "idleAnimationFrames",
            "idleAnimationFrameInterval",
            "idleAnimationLoop",
            "randomizeIdleStartFrame",
            "battleSpriteRenderer",
        };
        foreach (string fieldName in spriteFrameFields)
        {
            SerializedProperty p = serializedObject.FindProperty(fieldName);
            if (p != null)
                EditorGUILayout.PropertyField(p, true);
        }
    }

    void DrawAnimatorStatePicker(
        SerializedProperty stateNameProp,
        ref string searchText,
        string fieldLabel,
        string searchLabel,
        string popupLabel)
    {
        // 先画原始文本字段，保留手填路径的能力
        if (stateNameProp == null)
            return;

        EditorGUILayout.PropertyField(stateNameProp, new GUIContent(fieldLabel));

        if (idleAnimationSourceProp == null || idleAnimationSourceProp.enumValueIndex == (int)BattleIdleAnimationSource.SpriteFrames)
        {
            if (stateNameProp == idleAnimationStateNameProp)
                return;
        }

        Animator animator = ResolveAnimatorReference();
        if (animator == null)
        {
            EditorGUILayout.HelpBox("未找到 Animator。可在 Battle Animator 字段手动指定，或挂在同物体/子物体上。", MessageType.Info);
            return;
        }

        AnimatorController controller = ResolveAnimatorController(animator.runtimeAnimatorController);
        if (controller == null)
        {
            EditorGUILayout.HelpBox("当前 Animator 未绑定 AnimatorController，无法列出状态名。", MessageType.Warning);
            return;
        }

        List<string> stateNames = CollectAnimatorStateNames(controller);
        if (stateNames.Count == 0)
        {
            EditorGUILayout.HelpBox("AnimatorController 中未找到可用状态。", MessageType.Warning);
            return;
        }

        searchText = EditorGUILayout.TextField(searchLabel, searchText);

        List<string> filtered = FilterStateNames(stateNames, searchText);
        string[] options = BuildPopupOptions(filtered);
        int currentIndex = ResolveCurrentSelectionIndex(filtered, stateNameProp.stringValue);
        int nextIndex = EditorGUILayout.Popup(popupLabel, currentIndex, options);

        if (nextIndex != currentIndex)
        {
            stateNameProp.stringValue = nextIndex <= 0 ? string.Empty : filtered[nextIndex - 1];
        }

        if (!string.IsNullOrWhiteSpace(stateNameProp.stringValue) &&
            !stateNames.Contains(stateNameProp.stringValue))
        {
            EditorGUILayout.HelpBox("当前状态名不在控制器中，请通过搜索重新选择。", MessageType.Warning);
        }
    }

    Animator ResolveAnimatorReference()
    {
        Animator animator = battleAnimatorProp != null
            ? battleAnimatorProp.objectReferenceValue as Animator
            : null;

        if (animator != null)
            return animator;

        BattleUnit unit = target as BattleUnit;
        if (unit == null)
            return null;

        return unit.GetComponentInChildren<Animator>(true);
    }

    static AnimatorController ResolveAnimatorController(RuntimeAnimatorController runtimeController)
    {
        if (runtimeController == null)
            return null;

        AnimatorController controller = runtimeController as AnimatorController;
        if (controller != null)
            return controller;

        AnimatorOverrideController overrideController = runtimeController as AnimatorOverrideController;
        if (overrideController != null)
            return ResolveAnimatorController(overrideController.runtimeAnimatorController);

        return null;
    }

    static List<string> CollectAnimatorStateNames(AnimatorController controller)
    {
        List<string> names = new List<string>();
        if (controller == null)
            return names;

        for (int i = 0; i < controller.layers.Length; i++)
        {
            AnimatorControllerLayer layer = controller.layers[i];
            if (layer.stateMachine == null)
                continue;

            string layerRoot = string.IsNullOrEmpty(layer.name) ? "Base Layer" : layer.name;
            CollectStateNamesRecursive(layer.stateMachine, layerRoot, names);
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);

        List<string> deduped = new List<string>(names.Count);
        for (int i = 0; i < names.Count; i++)
        {
            if (i == 0 || !string.Equals(names[i], names[i - 1], StringComparison.OrdinalIgnoreCase))
                deduped.Add(names[i]);
        }

        return deduped;
    }

    static void CollectStateNamesRecursive(AnimatorStateMachine stateMachine, string path, List<string> output)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state == null)
                continue;

            output.Add(path + "." + state.name);
        }

        ChildAnimatorStateMachine[] childMachines = stateMachine.stateMachines;
        for (int i = 0; i < childMachines.Length; i++)
        {
            AnimatorStateMachine child = childMachines[i].stateMachine;
            if (child == null)
                continue;

            string childPath = path + "." + child.name;
            CollectStateNamesRecursive(child, childPath, output);
        }
    }

    static List<string> FilterStateNames(List<string> source, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<string>(source);

        List<string> filtered = new List<string>();
        string trimmed = query.Trim();

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                filtered.Add(source[i]);
        }

        return filtered;
    }

    static string[] BuildPopupOptions(List<string> filtered)
    {
        string[] options = new string[filtered.Count + 1];
        options[0] = "<None>";

        for (int i = 0; i < filtered.Count; i++)
            options[i + 1] = filtered[i];

        return options;
    }

    static int ResolveCurrentSelectionIndex(List<string> filtered, string currentValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue))
            return 0;

        for (int i = 0; i < filtered.Count; i++)
        {
            if (string.Equals(filtered[i], currentValue, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return 0;
    }
}
