#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在当前场景中创建/刷新敌方布局模板层级：
/// SpawnPoints/EnemyLayoutTemplates/...
///
/// 使用方法：
/// 1. 打开 BattleScene；
/// 2. 在 Unity 顶部菜单点击 Tools/Battle/Create Enemy Layout Templates；
/// 3. 脚本会自动创建模板空对象并写入推荐的初始 localPosition。
///
/// 坐标约定：
/// - 以战斗舞台中心附近为 (0,0,0)
/// - 敌方区域位于左半屏，因此 X 多为负值
/// - Z 用于前后层次错位，Y 默认保持 0
/// </summary>
public static class EnemyLayoutTemplateGenerator
{
    private struct SlotDefinition
    {
        public readonly string Name;
        public readonly Vector3 LocalPosition;

        public SlotDefinition(string name, Vector3 localPosition)
        {
            Name = name;
            LocalPosition = localPosition;
        }
    }

    private static readonly Dictionary<string, SlotDefinition[]> TemplateDefinitions = new Dictionary<string, SlotDefinition[]>
    {
        {
            "Normal_1",
            new[]
            {
                new SlotDefinition("Slot_01", new Vector3(-4.20f, 0f,  0.00f))
            }
        },
        {
            "Normal_2",
            new[]
            {
                new SlotDefinition("Slot_01", new Vector3(-5.00f, 0f,  0.75f)),
                new SlotDefinition("Slot_02", new Vector3(-3.60f, 0f, -0.60f))
            }
        },
        {
            "Normal_3",
            new[]
            {
                new SlotDefinition("Slot_01", new Vector3(-5.10f, 0f,  1.00f)),
                new SlotDefinition("Slot_02", new Vector3(-3.90f, 0f,  0.05f)),
                new SlotDefinition("Slot_03", new Vector3(-5.15f, 0f, -0.95f))
            }
        },
        {
            "Normal_4",
            new[]
            {
                new SlotDefinition("Slot_01", new Vector3(-5.40f, 0f,  1.15f)),
                new SlotDefinition("Slot_02", new Vector3(-3.75f, 0f,  0.55f)),
                new SlotDefinition("Slot_03", new Vector3(-5.00f, 0f, -0.45f)),
                new SlotDefinition("Slot_04", new Vector3(-3.20f, 0f, -1.10f))
            }
        },
        {
            "Normal_5",
            new[]
            {
                new SlotDefinition("Slot_01", new Vector3(-5.90f, 0f,  1.35f)),
                new SlotDefinition("Slot_02", new Vector3(-4.35f, 0f,  0.85f)),
                new SlotDefinition("Slot_03", new Vector3(-5.00f, 0f,  0.00f)),
                new SlotDefinition("Slot_04", new Vector3(-3.75f, 0f, -0.85f)),
                new SlotDefinition("Slot_05", new Vector3(-5.55f, 0f, -1.55f))
            }
        },
        {
            "Normal_6",
            new[]
            {
                new SlotDefinition("Slot_01", new Vector3(-6.10f, 0f,  1.50f)),
                new SlotDefinition("Slot_02", new Vector3(-4.70f, 0f,  0.95f)),
                new SlotDefinition("Slot_03", new Vector3(-3.45f, 0f,  0.20f)),
                new SlotDefinition("Slot_04", new Vector3(-5.45f, 0f, -0.45f)),
                new SlotDefinition("Slot_05", new Vector3(-4.10f, 0f, -1.05f)),
                new SlotDefinition("Slot_06", new Vector3(-5.90f, 0f, -1.75f))
            }
        },
        {
            "Boss_1",
            new[]
            {
                new SlotDefinition("BossSlot", new Vector3(-5.40f, 0f,  0.00f))
            }
        },
        {
            "Boss_2",
            new[]
            {
                new SlotDefinition("BossSlot",   new Vector3(-5.55f, 0f,  0.00f)),
                new SlotDefinition("AddSlot_01", new Vector3(-2.85f, 0f, -0.80f))
            }
        },
        {
            "Boss_3",
            new[]
            {
                new SlotDefinition("BossSlot",   new Vector3(-5.55f, 0f,  0.00f)),
                new SlotDefinition("AddSlot_01", new Vector3(-2.85f, 0f,  0.95f)),
                new SlotDefinition("AddSlot_02", new Vector3(-2.85f, 0f, -0.95f))
            }
        },
        {
            "Boss_4",
            new[]
            {
                new SlotDefinition("BossSlot",   new Vector3(-5.80f, 0f,  0.00f)),
                new SlotDefinition("AddSlot_01", new Vector3(-2.95f, 0f,  1.15f)),
                new SlotDefinition("AddSlot_02", new Vector3(-2.45f, 0f,  0.00f)),
                new SlotDefinition("AddSlot_03", new Vector3(-2.95f, 0f, -1.15f))
            }
        },
        {
            "Elite_2",
            new[]
            {
                new SlotDefinition("EliteSlot_01", new Vector3(-5.30f, 0f,  0.85f)),
                new SlotDefinition("EliteSlot_02", new Vector3(-4.00f, 0f, -0.70f))
            }
        },
        {
            "Mixed_3",
            new[]
            {
                new SlotDefinition("MainSlot",     new Vector3(-4.90f, 0f,  0.00f)),
                new SlotDefinition("SideSlot_Up",  new Vector3(-2.95f, 0f,  0.95f)),
                new SlotDefinition("SideSlot_Down",new Vector3(-2.95f, 0f, -0.95f))
            }
        }
    };

    [MenuItem("Tools/Battle/Create Enemy Layout Templates")]
    public static void CreateEnemyLayoutTemplates()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("[EnemyLayoutTemplateGenerator] 当前没有有效场景，无法创建模板。");
            return;
        }

        GameObject spawnPointsRoot = FindOrCreateRoot("SpawnPoints");
        GameObject enemyLayoutsRoot = FindOrCreateChild(spawnPointsRoot.transform, "EnemyLayoutTemplates");
        ResetLocalTransform(enemyLayoutsRoot.transform);

        int slotCount = 0;

        foreach (KeyValuePair<string, SlotDefinition[]> entry in TemplateDefinitions)
        {
            GameObject templateRoot = FindOrCreateChild(enemyLayoutsRoot.transform, entry.Key);
            ResetLocalTransform(templateRoot.transform);

            foreach (SlotDefinition slot in entry.Value)
            {
                GameObject slotObject = FindOrCreateChild(templateRoot.transform, slot.Name);
                slotObject.transform.localPosition = slot.LocalPosition;
                slotObject.transform.localRotation = Quaternion.identity;
                slotObject.transform.localScale = Vector3.one;
                slotCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        Selection.activeGameObject = enemyLayoutsRoot;

        Debug.Log($"[EnemyLayoutTemplateGenerator] 已在场景 `{activeScene.name}` 中创建/刷新 EnemyLayoutTemplates，共 {TemplateDefinitions.Count} 套模板，{slotCount} 个站位槽。", enemyLayoutsRoot);
    }

    [MenuItem("Tools/Battle/Create Enemy Layout Templates", true)]
    private static bool ValidateCreateEnemyLayoutTemplates()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static GameObject FindOrCreateRoot(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
            return existing;

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
        ResetWorldTransform(created.transform);
        return created;
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child.gameObject;

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
        created.transform.SetParent(parent, false);
        ResetLocalTransform(created.transform);
        return created;
    }

    private static void ResetLocalTransform(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static void ResetWorldTransform(Transform target)
    {
        target.position = Vector3.zero;
        target.rotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }
}
#endif
