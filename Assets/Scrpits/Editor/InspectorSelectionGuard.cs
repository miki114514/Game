using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class InspectorSelectionGuard
{
    private static bool sanitizeQueued;

    static InspectorSelectionGuard()
    {
        Selection.selectionChanged += QueueSanitizeSelection;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.hierarchyChanged += QueueSanitizeSelection;
    }

    private static void QueueSanitizeSelection()
    {
        if (sanitizeQueued)
            return;

        sanitizeQueued = true;
        EditorApplication.delayCall += () =>
        {
            sanitizeQueued = false;
            SanitizeSelection();
        };
    }

    [MenuItem("Tools/Project/Fix Inspector Null Selection")]
    public static void SanitizeSelection()
    {
        UnityEngine.Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
            return;

        List<UnityEngine.Object> validObjects = new List<UnityEngine.Object>(selected.Length);
        for (int i = 0; i < selected.Length; i++)
        {
            UnityEngine.Object obj = selected[i];
            if (obj != null)
                validObjects.Add(obj);
        }

        if (validObjects.Count == selected.Length)
            return;

        ApplySelection(validObjects);
    }

    [MenuItem("Tools/Project/Reset Inspector State")]
    public static void ResetInspectorState()
    {
        ActiveEditorTracker.sharedTracker.isLocked = false;
        Selection.objects = Array.Empty<UnityEngine.Object>();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
        Debug.Log("[InspectorSelectionGuard] Inspector state has been reset.");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredEditMode ||
            state == PlayModeStateChange.ExitingPlayMode)
        {
            ResetInspectorState();
            QueueSanitizeSelection();
            return;
        }

        QueueSanitizeSelection();
    }

    private static void ApplySelection(List<UnityEngine.Object> objects)
    {
        if (objects == null || objects.Count == 0)
        {
            Selection.objects = Array.Empty<UnityEngine.Object>();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            return;
        }

        Selection.objects = objects.ToArray();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }
}