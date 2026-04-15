using UnityEngine;

/// <summary>
/// 运行时单位与数据定义之间的绑定信息。
/// 主要用于战斗结束后把 HP/SP/EXP 同步回 PartyManager。
/// </summary>
public class BattleUnitRuntimeLink : MonoBehaviour
{
    public string actorId;
    public bool isPartyMember;
    public CharacterDefinition characterDefinition;
    public EnemyDefinition enemyDefinition;
    public int formationIndex = -1;

    public void Bind(CharacterDefinition definition, int formationOrder)
    {
        characterDefinition = definition;
        enemyDefinition = null;
        isPartyMember = true;
        formationIndex = formationOrder;
        actorId = definition != null && !string.IsNullOrWhiteSpace(definition.characterId)
            ? definition.characterId
            : string.Empty;
    }

    public void Bind(EnemyDefinition definition)
    {
        characterDefinition = null;
        enemyDefinition = definition;
        isPartyMember = false;
        formationIndex = -1;
        actorId = definition != null && !string.IsNullOrWhiteSpace(definition.enemyId)
            ? definition.enemyId
            : string.Empty;
    }
}
