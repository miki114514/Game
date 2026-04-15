using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Battle/Data/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    [Header("基础信息")]
    public string characterId = "hero_main";
    public string displayName = "New Character";
    [TextArea(2, 5)] public string description;

    [Tooltip("拖入挂有 BattleUnit 的角色战斗预制体。角色的战斗属性将直接读取该预制体，Portrait 将自动从 BattleUnit 的立绘裁剪生成。")]
    public GameObject battlePrefab;

    [Header("队伍默认状态")]
    public bool startsUnlocked = false;
    public bool startsInActiveParty = false;
    [Min(0)] public int defaultFormationIndex = 0;

    public BattleUnit BattleUnitTemplate => BattleUnitDefinitionUtility.GetTemplate(battlePrefab);
    public Sprite ResolvedPortrait => BattleUnitTemplate?.portrait;

    public void ApplyTo(BattleUnit unit, bool initializeState = true)
    {
        if (unit == null)
            return;

        string resolvedName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        BattleUnitDefinitionUtility.ApplyFromPrefabTemplate(battlePrefab, unit, UnitType.Player, resolvedName, null, initializeState);
    }
}
