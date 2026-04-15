using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class InspectorSelectionGuard
{
    static InspectorSelectionGuard()
    {
        Selection.selectionChanged += SanitizeSelection;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.hierarchyChanged += SanitizeSelection;
    }

    [MenuItem("Tools/Project/Fix Inspector Null Selection")]
    public static void SanitizeSelection()
    {
        int[] ids = Selection.instanceIDs;
        if (ids == null || ids.Length == 0)
            return;

        List<int> validIds = new List<int>(ids.Length);
        for (int i = 0; i < ids.Length; i++)
        {
            UnityEngine.Object obj = EditorUtility.InstanceIDToObject(ids[i]);
            if (obj != null)
                validIds.Add(ids[i]);
        }

        if (validIds.Count == ids.Length)
            return;

        ApplySelectionByIds(validIds);
    }

    [MenuItem("Tools/Project/Reset Inspector State")]
    public static void ResetInspectorState()
    {
        ActiveEditorTracker.sharedTracker.isLocked = false;
        Selection.activeObject = null;
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
            EditorApplication.delayCall += SanitizeSelection;
            return;
        }

        EditorApplication.delayCall += SanitizeSelection;
    }

    private static void ApplySelectionByIds(List<int> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            Selection.activeObject = null;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            return;
        }

        Selection.instanceIDs = ids.ToArray();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }
}