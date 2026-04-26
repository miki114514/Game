using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[CustomEditor(typeof(Skill))]
public class SkillEditor : Editor
{
    SerializedProperty animationStateSearchControllerProp;
    SerializedProperty animationStateNameProp;

    string animationStateSearchText = string.Empty;

    void OnEnable()
    {
        animationStateSearchControllerProp = serializedObject.FindProperty("animationStateSearchController");
        animationStateNameProp = serializedObject.FindProperty("animationStateName");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("技能动画状态搜索", EditorStyles.boldLabel);
        DrawAnimationStatePicker();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawAnimationStatePicker()
    {
        if (animationStateSearchControllerProp == null || animationStateNameProp == null)
            return;

        RuntimeAnimatorController runtimeController = animationStateSearchControllerProp.objectReferenceValue as RuntimeAnimatorController;
        AnimatorController controller = ResolveAnimatorController(runtimeController);

        if (runtimeController == null)
        {
            EditorGUILayout.HelpBox("请先在 Animation State Search Controller 中指定 AnimatorController。", MessageType.Info);
            return;
        }

        if (controller == null)
        {
            EditorGUILayout.HelpBox("当前控制器不是 AnimatorController，无法列出状态名。", MessageType.Warning);
            return;
        }

        List<string> stateNames = CollectAnimatorStateNames(controller);
        if (stateNames.Count == 0)
        {
            EditorGUILayout.HelpBox("AnimatorController 中未找到可用状态。", MessageType.Warning);
            return;
        }

        animationStateSearchText = EditorGUILayout.TextField("搜索技能动画名", animationStateSearchText);

        List<string> filtered = FilterStateNames(stateNames, animationStateSearchText);
        string[] options = BuildPopupOptions(filtered);
        int currentIndex = ResolveCurrentSelectionIndex(filtered, animationStateNameProp.stringValue);
        int nextIndex = EditorGUILayout.Popup("匹配技能动画", currentIndex, options);

        if (nextIndex != currentIndex)
            animationStateNameProp.stringValue = nextIndex <= 0 ? string.Empty : filtered[nextIndex - 1];

        if (!string.IsNullOrWhiteSpace(animationStateNameProp.stringValue) &&
            !stateNames.Contains(animationStateNameProp.stringValue))
        {
            EditorGUILayout.HelpBox("当前技能动画名不在控制器中，请通过搜索重新选择。", MessageType.Warning);
        }
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
