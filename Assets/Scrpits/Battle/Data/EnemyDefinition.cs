using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Battle/Data/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("基础信息")]
    public string enemyId = "enemy_new";
    public string displayName = "New Enemy";
    [TextArea(2, 5)] public string description;

    [Tooltip("拖入挂有 BattleUnit 的敌人战斗预制体。敌人的战斗属性、弱点和技能将直接读取该预制体，Portrait 将自动从 BattleUnit 的立绘裁剪生成。")]
    public GameObject battlePrefab;

    [Header("分类")]
    public bool isBoss = false;
    public EnemySizeType sizeType = EnemySizeType.Medium;

    [Header("奖励")]
    [Min(0)] public int expReward = 10;
    [Min(0)] public int moneyReward = 0;
    public List<Item> guaranteedDrops = new List<Item>();

    public BattleUnit BattleUnitTemplate => BattleUnitDefinitionUtility.GetTemplate(battlePrefab);
    public Sprite ResolvedPortrait => BattleUnitTemplate?.portrait;

    public void ApplyTo(BattleUnit unit, bool initializeState = true)
    {
        if (unit == null)
            return;

        string resolvedName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        BattleUnitDefinitionUtility.ApplyFromPrefabTemplate(battlePrefab, unit, UnitType.Enemy, resolvedName, null, initializeState);
    }
}
