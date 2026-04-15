using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MissingScriptScanner
{
    [MenuItem("Tools/Project/Scan Missing Scripts")]
    public static void ScanMissingScripts()
    {
        int sceneCount = 0;
        int prefabCount = 0;
        int missingCount = 0;

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                missingCount += CountMissingScriptsRecursive(root);
                sceneCount++;
            }
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            int prefabMissing = CountMissingScriptsRecursive(prefab);
            if (prefabMissing > 0)
            {
                Debug.LogWarning($"[MissingScriptScanner] Prefab has missing scripts: {path} ({prefabMissing})", prefab);
                missingCount += prefabMissing;
            }

            prefabCount++;
        }

        if (missingCount == 0)
            Debug.Log($"[MissingScriptScanner] Scan complete. No missing scripts found. Loaded scene roots: {sceneCount}, prefabs: {prefabCount}.");
        else
            Debug.LogError($"[MissingScriptScanner] Scan complete. Missing scripts found: {missingCount}. Check Console warnings for details.");
    }

    [MenuItem("Tools/Project/Clean Missing Scripts In Loaded Scenes")]
    public static void CleanMissingScriptsInLoadedScenes()
    {
        int removed = 0;

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            bool sceneDirty = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                int removedInRoot = RemoveMissingScriptsRecursive(root);
                if (removedInRoot > 0)
                {
                    removed += removedInRoot;
                    sceneDirty = true;
                }
            }

            if (sceneDirty)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"[MissingScriptScanner] Removed missing scripts in loaded scenes: {removed}");
    }

    private static int CountMissingScriptsRecursive(GameObject go)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (count > 0)
        {
            Debug.LogWarning($"[MissingScriptScanner] Missing scripts on: {GetHierarchyPath(go)} ({count})", go);
        }

        foreach (Transform child in go.transform)
            count += CountMissingScriptsRecursive(child.gameObject);

        return count;
    }

    private static int RemoveMissingScriptsRecursive(GameObject go)
    {
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        foreach (Transform child in go.transform)
            removed += RemoveMissingScriptsRecursive(child.gameObject);
        return removed;
    }

    private static string GetHierarchyPath(GameObject go)
    {
        if (go == null)
            return "<null>";

        List<string> parts = new List<string>();
        Transform current = go.transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}