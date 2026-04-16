using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EncounterEnemyEntry
{
    public EnemyDefinition enemyDefinition;
    public EncounterEnemyRole role = EncounterEnemyRole.Auto;
    public string slotNameOverride = string.Empty;
    public Vector3 localPositionOffset = Vector3.zero;
    public int levelOverride = -1;
    [Min(0.1f)] public float hpMultiplier = 1f;
    [Min(0.1f)] public float attackMultiplier = 1f;
    [Min(0.1f)] public float scaleMultiplier = 1f;

    public bool IsBossEntry => role == EncounterEnemyRole.Boss || (enemyDefinition != null && enemyDefinition.isBoss);

    public string GetSuggestedSlotName(int slotIndex, bool isBossEncounter)
    {
        if (!string.IsNullOrWhiteSpace(slotNameOverride))
            return slotNameOverride;

        if (IsBossEntry)
            return "BossSlot";

        if (isBossEncounter)
            return $"AddSlot_{Mathf.Max(1, slotIndex):00}";

        return $"Slot_{Mathf.Max(1, slotIndex):00}";
    }
}

[Serializable]
public class BattleRewardItemEntry
{
    public Item item;
    [Min(1)] public int quantity = 1;
}

[CreateAssetMenu(fileName = "BattleEncounterData", menuName = "Battle/Data/Battle Encounter")]
public class BattleEncounterData : ScriptableObject
{
    [Header("基础信息")]
    public string encounterId = "encounter_new";
    public string displayName = "New Encounter";
    [TextArea(2, 6)] public string notes;

    [Header("遭遇设置")]
    public BattleEncounterType encounterType = BattleEncounterType.Normal;
    public BattleLayoutTemplateType layoutTemplateType = BattleLayoutTemplateType.Normal_1;
    public bool canEscape = true;
    public bool isStoryBattle = false;

    [Header("战斗环境")]
    public GameObject backgroundPrefab;
    public AudioClip battleBgm;

    [Header("敌人编成")]
    public List<EncounterEnemyEntry> enemyEntries = new List<EncounterEnemyEntry>();

    [Header("额外奖励")]
    [Min(0)] public int bonusExp = 0;
    [Min(0)] public int bonusJP = 0;
    [Min(0)] public int bonusMoney = 0;
    public List<BattleRewardItemEntry> guaranteedRewardItems = new List<BattleRewardItemEntry>();

    public int TotalEnemyCount => CountAssignedEnemies();
    public bool IsBossEncounter => encounterType == BattleEncounterType.Boss;

    public int CountAssignedEnemies()
    {
        if (enemyEntries == null)
            return 0;

        int count = 0;
        for (int i = 0; i < enemyEntries.Count; i++)
        {
            if (enemyEntries[i] != null && enemyEntries[i].enemyDefinition != null)
                count++;
        }

        return count;
    }

    public int CountBossEntries()
    {
        if (enemyEntries == null)
            return 0;

        int count = 0;
        for (int i = 0; i < enemyEntries.Count; i++)
        {
            EncounterEnemyEntry entry = enemyEntries[i];
            if (entry != null && entry.enemyDefinition != null && entry.IsBossEntry)
                count++;
        }

        return count;
    }

    public int GetExpectedEnemyCount()
    {
        switch (layoutTemplateType)
        {
            case BattleLayoutTemplateType.Normal_1:
            case BattleLayoutTemplateType.Boss_1:
                return 1;
            case BattleLayoutTemplateType.Normal_2:
            case BattleLayoutTemplateType.Boss_2:
                return 2;
            case BattleLayoutTemplateType.Normal_3:
            case BattleLayoutTemplateType.Boss_3:
                return 3;
            case BattleLayoutTemplateType.Normal_4:
            case BattleLayoutTemplateType.Boss_4:
                return 4;
            default:
                return 0;
        }
    }

    public bool IsConfigurationValid(out string message)
    {
        int expectedCount = GetExpectedEnemyCount();
        int assignedCount = CountAssignedEnemies();
        int bossCount = CountBossEntries();
        bool usingBossLayout = layoutTemplateType == BattleLayoutTemplateType.Boss_1
            || layoutTemplateType == BattleLayoutTemplateType.Boss_2
            || layoutTemplateType == BattleLayoutTemplateType.Boss_3
            || layoutTemplateType == BattleLayoutTemplateType.Boss_4;

        if (assignedCount != expectedCount)
        {
            message = $"当前布局 {layoutTemplateType} 需要 {expectedCount} 个敌人，但实际配置了 {assignedCount} 个。";
            return false;
        }

        if (usingBossLayout)
        {
            if (encounterType != BattleEncounterType.Boss)
            {
                message = "Boss 布局模板应搭配 Boss 类型遭遇。";
                return false;
            }

            if (bossCount != 1)
            {
                message = $"Boss 类型遭遇必须恰好有 1 个 Boss 敌人，当前为 {bossCount} 个。";
                return false;
            }
        }
        else
        {
            if (encounterType != BattleEncounterType.Normal)
            {
                message = "Normal 布局模板应搭配 Normal 类型遭遇。";
                return false;
            }

            if (bossCount > 0)
            {
                message = "普通杂兵遭遇中不应包含 Boss 敌人。";
                return false;
            }
        }

        message = "配置有效。";
        return true;
    }
}
