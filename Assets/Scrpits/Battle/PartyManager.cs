using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PartyMemberState
{
    public string characterId;
    public string displayName;
    public CharacterDefinition definition;
    public bool isUnlocked;
    public bool isInActiveParty;
    [Min(0)] public int formationIndex;

    [Header("运行时状态")]
    [Range(1, BattleUnit.MaxLevel)] public int level = 1;
    [Min(0)] public int currentHP = 1;
    [Min(0)] public int currentSP = 0;
    [Min(0)] public int currentExp = 0;
    [Min(0)] public int expToNextLevel = 100;
    [Min(0)] public int currentJP = 0;

    [Header("已学习技能")]
    public List<Skill> learnedArts = new List<Skill>();
    public List<Skill> learnedCharacterSkills = new List<Skill>();

    [Header("装备状态")]
    public WeaponData equippedWeapon;
    public ArmorData equippedHead;
    public ArmorData equippedBody;
    public AccessoryData[] equippedAccessories = new AccessoryData[2];

    public bool IsAvailableForBattle => definition != null && isUnlocked && isInActiveParty && definition.battlePrefab != null;

    public void InitializeFromDefinition(CharacterDefinition characterDefinition)
    {
        definition = characterDefinition;
        if (definition == null)
            return;

        characterId = string.IsNullOrWhiteSpace(definition.characterId) ? definition.name : definition.characterId;
        displayName = string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
        isUnlocked = definition.startsUnlocked;
        isInActiveParty = definition.startsInActiveParty;
        formationIndex = Mathf.Max(0, definition.defaultFormationIndex);

        BattleUnit template = definition.BattleUnitTemplate;
        if (template != null)
        {
            level = Mathf.Clamp(template.level, 1, BattleUnit.MaxLevel);
            currentHP = Mathf.Max(0, template.maxHP);
            currentSP = Mathf.Clamp(template.maxSP, 0, template.maxSP);
            currentExp = Mathf.Max(0, template.currentExp);
            expToNextLevel = Mathf.Max(0, template.expToNextLevel);
            currentJP = Mathf.Max(0, template.currentJP);

            CharacterClassDefinition classDefinition = template.classDefinition;
            learnedArts = BuildSafeSkillList(template.artsList, classDefinition);
            learnedCharacterSkills = BuildSafeSkillList(template.skillList, classDefinition);

            equippedWeapon = template.equippedWeapon;
            equippedHead = template.equippedHead;
            equippedBody = template.equippedBody;
            if (template.equippedAccessories != null)
            {
                equippedAccessories = new AccessoryData[template.equippedAccessories.Length];
                for (int j = 0; j < template.equippedAccessories.Length; j++)
                    equippedAccessories[j] = template.equippedAccessories[j];
            }
        }
        else
        {
            level = 1;
            currentHP = 1;
            currentSP = 0;
            currentExp = 0;
            expToNextLevel = 100;
            currentJP = 0;
            learnedArts = new List<Skill>();
            learnedCharacterSkills = new List<Skill>();
        }
    }

    public void RefreshMetadataFromDefinition()
    {
        if (definition == null)
            return;

        if (string.IsNullOrWhiteSpace(characterId))
            characterId = string.IsNullOrWhiteSpace(definition.characterId) ? definition.name : definition.characterId;

        displayName = string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
    }

    public void ApplyToBattleUnit(BattleUnit unit, bool initializeState = true)
    {
        if (unit == null || definition == null)
            return;

        definition.ApplyTo(unit, initializeState);
        unit.level = Mathf.Clamp(level, 1, BattleUnit.MaxLevel);
        unit.currentHP = Mathf.Clamp(currentHP, 0, unit.maxHP);
        unit.currentSP = Mathf.Clamp(currentSP, 0, unit.maxSP);
        unit.currentExp = Mathf.Max(0, currentExp);
        unit.expToNextLevel = Mathf.Max(0, expToNextLevel);
        unit.currentJP = Mathf.Max(0, currentJP);

        CharacterClassDefinition classDefinition = unit.classDefinition;
        unit.artsList = BuildSafeSkillList(learnedArts, classDefinition);
        unit.skillList = BuildSafeSkillList(learnedCharacterSkills, classDefinition);

        unit.equippedWeapon = equippedWeapon;
        unit.equippedHead = equippedHead;
        unit.equippedBody = equippedBody;

        if (unit.equippedAccessories == null || unit.equippedAccessories.Length != equippedAccessories.Length)
            unit.equippedAccessories = new AccessoryData[equippedAccessories.Length];

        for (int i = 0; i < equippedAccessories.Length; i++)
            unit.equippedAccessories[i] = equippedAccessories[i];

        unit.RecalculateEquipmentBonuses();
        unit.SanitizeSkillListsByClass();
    }

    public void SyncFromBattleUnit(BattleUnit unit)
    {
        if (unit == null)
            return;

        if (!string.IsNullOrWhiteSpace(unit.unitName))
            displayName = unit.unitName;

        level = Mathf.Clamp(unit.level, 1, BattleUnit.MaxLevel);
        currentHP = Mathf.Clamp(unit.currentHP, 0, Mathf.Max(1, unit.maxHP));
        currentSP = Mathf.Clamp(unit.currentSP, 0, Mathf.Max(0, unit.maxSP));
        currentExp = Mathf.Max(0, unit.currentExp);
        expToNextLevel = Mathf.Max(0, unit.expToNextLevel);
        currentJP = Mathf.Max(0, unit.currentJP);

        CharacterClassDefinition classDefinition = unit.classDefinition;
        learnedArts = BuildSafeSkillList(unit.artsList, classDefinition);
        learnedCharacterSkills = BuildSafeSkillList(unit.skillList, classDefinition);

        equippedWeapon = unit.equippedWeapon;
        equippedHead = unit.equippedHead;
        equippedBody = unit.equippedBody;

        if (unit.equippedAccessories != null)
        {
            if (equippedAccessories == null || equippedAccessories.Length != unit.equippedAccessories.Length)
                equippedAccessories = new AccessoryData[unit.equippedAccessories.Length];

            for (int i = 0; i < unit.equippedAccessories.Length; i++)
                equippedAccessories[i] = unit.equippedAccessories[i];
        }
        else
        {
            equippedAccessories = new AccessoryData[2];
        }
    }

    public CharacterClassDefinition GetClassDefinition()
    {
        BattleUnit template = definition != null ? definition.BattleUnitTemplate : null;
        return template != null ? template.classDefinition : null;
    }

    private static List<Skill> BuildSafeSkillList(List<Skill> source, CharacterClassDefinition classDefinition)
    {
        List<Skill> result = new List<Skill>();
        if (source == null || source.Count == 0)
            return result;

        HashSet<Skill> unique = new HashSet<Skill>();
        for (int i = 0; i < source.Count; i++)
        {
            Skill skill = source[i];
            if (skill == null)
                continue;

            if (classDefinition != null && !skill.CanBeLearnedByClass(classDefinition))
                continue;

            if (unique.Add(skill))
                result.Add(skill);
        }

        return result;
    }
}

[DefaultExecutionOrder(-1000)]
public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    [Header("角色数据库")]
    public List<CharacterDefinition> characterDefinitions = new List<CharacterDefinition>();

    [Header("运行设置")]
    public bool dontDestroyOnLoad = true;
    public bool autoInitializeFromDefinitions = true;
    [Range(1, 4)] public int maxActivePartySize = 4;

    [Header("战斗流程")]
    public BattleEncounterData pendingEncounter;

    [Header("队伍运行时数据（调试）")]
    [SerializeField] private int partyMoney = 0;
    [SerializeField] private List<PartyMemberState> partyMembers = new List<PartyMemberState>();

    public int PartyMoney => partyMoney;
    public IReadOnlyList<PartyMemberState> PartyMembers => partyMembers;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (autoInitializeFromDefinitions)
            BuildPartyFromDefinitions(partyMembers.Count == 0);
        else
            RefreshDefinitionsOnly();
    }

    [ContextMenu("Build Party From Definitions")]
    public void BuildPartyFromDefinitions()
    {
        BuildPartyFromDefinitions(true);
    }

    public void BuildPartyFromDefinitions(bool overwriteExisting)
    {
        if (characterDefinitions == null)
            return;

        foreach (CharacterDefinition definition in characterDefinitions)
        {
            if (definition == null)
                continue;

            string id = string.IsNullOrWhiteSpace(definition.characterId) ? definition.name : definition.characterId;
            PartyMemberState existing = partyMembers.Find(member => member != null && member.characterId == id);

            if (existing == null)
            {
                existing = new PartyMemberState();
                existing.InitializeFromDefinition(definition);
                partyMembers.Add(existing);
                continue;
            }

            existing.definition = definition;
            existing.RefreshMetadataFromDefinition();

            if (overwriteExisting)
                existing.InitializeFromDefinition(definition);
        }

        SortPartyMembers();
    }

    public void RefreshDefinitionsOnly()
    {
        foreach (PartyMemberState member in partyMembers)
        {
            if (member == null)
                continue;

            if (member.definition == null && characterDefinitions != null)
            {
                member.definition = characterDefinitions.Find(def => def != null && def.characterId == member.characterId);
            }

            member.RefreshMetadataFromDefinition();
        }

        SortPartyMembers();
    }

    public List<PartyMemberState> GetActivePartyMembers(int maxMembers = -1)
    {
        List<PartyMemberState> result = new List<PartyMemberState>();
        int limit = maxMembers > 0 ? maxMembers : maxActivePartySize;

        foreach (PartyMemberState member in partyMembers)
        {
            if (member != null && member.IsAvailableForBattle)
                result.Add(member);
        }

        result.Sort((a, b) => a.formationIndex.CompareTo(b.formationIndex));

        if (limit > 0 && result.Count > limit)
            result.RemoveRange(limit, result.Count - limit);

        return result;
    }

    public PartyMemberState GetPartyMember(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        return partyMembers.Find(member => member != null && member.characterId == characterId);
    }

    public void SetPendingEncounter(BattleEncounterData encounter)
    {
        pendingEncounter = encounter;
    }

    public void ClearPendingEncounter()
    {
        pendingEncounter = null;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
            return;

        partyMoney = Mathf.Max(0, partyMoney + amount);
    }

    public void SyncPartyStateFromBattle(IEnumerable<BattleUnit> playerUnits)
    {
        if (playerUnits == null)
            return;

        foreach (BattleUnit unit in playerUnits)
        {
            if (unit == null)
                continue;

            BattleUnitRuntimeLink link = unit.GetComponent<BattleUnitRuntimeLink>();
            string characterId = link != null ? link.actorId : string.Empty;
            CharacterDefinition definition = link != null ? link.characterDefinition : null;

            PartyMemberState state = definition != null
                ? partyMembers.Find(member => member != null && member.definition == definition)
                : GetPartyMember(characterId);

            if (state == null && definition != null)
            {
                state = new PartyMemberState();
                state.InitializeFromDefinition(definition);
                partyMembers.Add(state);
            }

            state?.SyncFromBattleUnit(unit);
        }

        SortPartyMembers();
    }

    void SortPartyMembers()
    {
        partyMembers.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.formationIndex.CompareTo(b.formationIndex);
        });
    }
}
