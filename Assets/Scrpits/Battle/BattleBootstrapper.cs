using System.Collections.Generic;
using UnityEngine;

public enum EnemySpawnPointFilter
{
    All,
    NormalSlot,
    AddSlot,
    BossSlot
}

[DefaultExecutionOrder(-500)]
public class BattleBootstrapper : MonoBehaviour
{
    [Header("核心引用")]
    public BattleManager battleManager;
    public PartyManager partyManager;
    public BattleEncounterData defaultEncounter;

    [Header("运行时根节点")]
    public Transform runtimeRoot;
    public Transform backgroundRoot;
    public Transform playerUnitRoot;
    public Transform enemyUnitRoot;

    [Header("站位根节点")]
    public Transform playerSpawnPointsRoot;
    public Transform enemyLayoutTemplatesRoot;

    [Header("行为")]
    public bool autoPrepareOnAwake = true;
    public bool usePendingEncounterFromPartyManager = true;
    public bool clearRuntimeRootsBeforeSpawn = true;

    [Header("战斗音乐")]
    public AudioSource battleBgmSource;
    [Range(0f, 1f)] public float battleBgmVolume = 1f;
    public bool stopBattleBgmOnBattleEnd = true;

    [Header("调试可视化")]
    public bool drawSpawnPointGizmos = true;
    [Min(0.05f)] public float spawnPointGizmoRadius = 0.25f;
    public bool drawAllEnemyLayouts = false;
    public EnemySpawnPointFilter enemySpawnPointFilter = EnemySpawnPointFilter.All;
    public Color playerSpawnColor = new Color(0.2f, 0.8f, 1f, 1f);
    public Color enemySpawnColor = new Color(1f, 0.35f, 0.25f, 1f);

    private bool battlePrepared = false;
    private bool resultHandled = false;

    void Awake()
    {
        ResolveReferences();

        if (autoPrepareOnAwake)
            PrepareBattle();
    }

    [ContextMenu("Prepare Battle Now")]
    public void PrepareBattleNow()
    {
        PrepareBattle(true);
    }

    [ContextMenu("Debug/Log Spawn Points")]
    public void LogSpawnPointsForDebug()
    {
        ResolveReferences();

        List<Transform> playerSpawns = GetOrderedChildren(playerSpawnPointsRoot);
        if (playerSpawns.Count == 0)
        {
            Debug.LogWarning("[BattleBootstrapper] 未找到玩家出生点。", this);
        }
        else
        {
            for (int i = 0; i < playerSpawns.Count; i++)
            {
                Transform point = playerSpawns[i];
                Debug.Log($"[BattleBootstrapper] PlayerSpawn[{i}] {point.name} pos={point.position}", point);
            }
        }

        List<Transform> enemyLayouts = GetOrderedChildren(enemyLayoutTemplatesRoot);
        if (enemyLayouts.Count == 0)
        {
            Debug.LogWarning("[BattleBootstrapper] 未找到敌方布局根节点。", this);
            return;
        }

        for (int i = 0; i < enemyLayouts.Count; i++)
        {
            Transform layoutRoot = enemyLayouts[i];
            List<Transform> slots = GetOrderedChildren(layoutRoot);

            if (slots.Count == 0)
            {
                Debug.LogWarning($"[BattleBootstrapper] 布局 {layoutRoot.name} 下没有槽位。", layoutRoot);
                continue;
            }

            for (int j = 0; j < slots.Count; j++)
            {
                Transform slot = slots[j];
                if (!ShouldDrawEnemySlot(slot))
                    continue;

                Debug.Log($"[BattleBootstrapper] EnemyLayout={layoutRoot.name} Slot[{j}] {slot.name} pos={slot.position}", slot);
            }
        }
    }

    public bool PrepareBattle(bool forceRebuild = false)
    {
        ResolveReferences();

        if (battlePrepared && !forceRebuild)
            return true;

        if (battleManager == null)
        {
            Debug.LogError("[BattleBootstrapper] 未找到 BattleManager，无法准备战斗。", this);
            return false;
        }

        BattleEncounterData encounter = ResolveEncounter();
        if (encounter == null)
        {
            Debug.LogError("[BattleBootstrapper] 未配置 BattleEncounterData，也没有来自 PartyManager 的 pendingEncounter。", this);
            return false;
        }

        PlayEncounterBgm(encounter);

        List<PartyMemberState> activeParty = ResolveActivePartyMembers();
        if (activeParty.Count == 0)
        {
            Debug.LogError("[BattleBootstrapper] 当前没有可上阵的角色。请先在 PartyManager 中配置并激活队伍。", this);
            return false;
        }

        EnsureRuntimeRoots();

        if (clearRuntimeRootsBeforeSpawn)
            ClearRuntimeRoots();

        SpawnBackground(encounter);

        List<BattleUnit> spawnedPlayers = SpawnPlayers(activeParty);
        List<BattleUnit> spawnedEnemies = SpawnEnemies(encounter);

        battleManager.players = spawnedPlayers;
        battleManager.enemies = spawnedEnemies;
        battleManager.battleBootstrapper = this;

        battlePrepared = spawnedPlayers.Count > 0 && spawnedEnemies.Count > 0;
        resultHandled = false;

        Debug.Log($"[BattleBootstrapper] 已生成战斗：玩家 {spawnedPlayers.Count} 人，敌人 {spawnedEnemies.Count} 人，布局 {encounter.layoutTemplateType}。", this);
        return battlePrepared;
    }

    public void HandleBattleEnded(BattleManager sourceManager, bool isVictory)
    {
        if (resultHandled)
            return;

        resultHandled = true;

        if (sourceManager == null)
            return;

        BattleEncounterData encounter = ResolveEncounter();

        if (isVictory)
        {
            ApplyVictoryRewards(sourceManager, encounter);

            if (partyManager != null)
                partyManager.ReturnFromBattleToExploration();
        }

        if (partyManager != null)
            partyManager.SyncPartyStateFromBattle(sourceManager.players);

        if (stopBattleBgmOnBattleEnd)
            StopEncounterBgm();

        Debug.Log(isVictory
            ? "[BattleBootstrapper] 已同步胜利结算结果回 PartyManager。"
            : "[BattleBootstrapper] 已同步战败后的队伍状态回 PartyManager。", this);
    }

    BattleEncounterData ResolveEncounter()
    {
        if (usePendingEncounterFromPartyManager && partyManager != null && partyManager.pendingEncounter != null)
            return partyManager.pendingEncounter;

        return defaultEncounter;
    }

    List<PartyMemberState> ResolveActivePartyMembers()
    {
        if (partyManager == null)
            partyManager = PartyManager.Instance != null ? PartyManager.Instance : FindObjectOfType<PartyManager>();

        return partyManager != null
            ? partyManager.GetActivePartyMembers()
            : new List<PartyMemberState>();
    }

    void ResolveReferences()
    {
        if (battleManager == null)
            battleManager = FindObjectOfType<BattleManager>();

        if (partyManager == null)
            partyManager = PartyManager.Instance != null ? PartyManager.Instance : FindObjectOfType<PartyManager>();

        if (playerSpawnPointsRoot == null)
            playerSpawnPointsRoot = FindScenePath("SpawnPoints/PlayerSpawnPoints");

        if (enemyLayoutTemplatesRoot == null)
            enemyLayoutTemplatesRoot = FindScenePath("SpawnPoints/EnemyLayoutTemplates");
    }

    void EnsureRuntimeRoots()
    {
        if (runtimeRoot == null)
            runtimeRoot = FindOrCreateRoot("BattleRuntime");

        if (backgroundRoot == null)
            backgroundRoot = FindOrCreateChild(runtimeRoot, "BackgroundRoot");

        if (playerUnitRoot == null)
            playerUnitRoot = FindOrCreateChild(runtimeRoot, "PlayerUnits");

        if (enemyUnitRoot == null)
            enemyUnitRoot = FindOrCreateChild(runtimeRoot, "EnemyUnits");
    }

    void OnValidate()
    {
        battleBgmVolume = Mathf.Clamp01(battleBgmVolume);

        if (battleBgmSource != null)
            ApplyBattleBgmSettings();
    }

    public void SetBattleBgmVolume(float volume)
    {
        battleBgmVolume = Mathf.Clamp01(volume);
        ApplyBattleBgmSettings();
    }

    void PlayEncounterBgm(BattleEncounterData encounter)
    {
        if (encounter == null || encounter.battleBgm == null)
            return;

        if (battleBgmSource == null)
            battleBgmSource = GetComponent<AudioSource>();

        if (battleBgmSource == null)
            battleBgmSource = gameObject.AddComponent<AudioSource>();

        ApplyBattleBgmSettings();

        if (battleBgmSource.clip == encounter.battleBgm && battleBgmSource.isPlaying)
            return;

        battleBgmSource.Stop();
        battleBgmSource.clip = encounter.battleBgm;
        battleBgmSource.Play();

        Debug.Log($"[BattleBootstrapper] 播放遭遇战斗音乐：{encounter.battleBgm.name}", this);
    }

    void ApplyBattleBgmSettings()
    {
        if (battleBgmSource == null)
            return;

        battleBgmSource.playOnAwake = false;
        battleBgmSource.loop = true;
        battleBgmSource.spatialBlend = 0f;
        battleBgmSource.volume = battleBgmVolume;
    }

    void StopEncounterBgm()
    {
        if (battleBgmSource == null)
            return;

        if (battleBgmSource.isPlaying)
            battleBgmSource.Stop();
    }

    void ClearRuntimeRoots()
    {
        ClearChildren(backgroundRoot);
        ClearChildren(playerUnitRoot);
        ClearChildren(enemyUnitRoot);
    }

    List<BattleUnit> SpawnPlayers(List<PartyMemberState> activeParty)
    {
        List<BattleUnit> result = new List<BattleUnit>();
        List<Transform> spawnPoints = GetOrderedChildren(playerSpawnPointsRoot);

        if (spawnPoints.Count == 0)
        {
            Debug.LogError("[BattleBootstrapper] 未找到玩家出生点，请检查 SpawnPoints/PlayerSpawnPoints。", this);
            return result;
        }

        for (int i = 0; i < activeParty.Count; i++)
        {
            PartyMemberState state = activeParty[i];
            if (state == null || state.definition == null || state.definition.battlePrefab == null)
                continue;

            Transform spawnPoint = i < spawnPoints.Count ? spawnPoints[i] : spawnPoints[spawnPoints.Count - 1];
            GameObject instance = Instantiate(state.definition.battlePrefab, spawnPoint.position, spawnPoint.rotation, playerUnitRoot);
            instance.name = state.definition.displayName;

            BattleUnit unit = instance.GetComponent<BattleUnit>();
            if (unit == null)
            {
                Debug.LogError($"[BattleBootstrapper] 玩家预制体 `{state.definition.battlePrefab.name}` 缺少 BattleUnit 组件。", state.definition.battlePrefab);
                Destroy(instance);
                continue;
            }

            state.ApplyToBattleUnit(unit, true);
            unit.unitType = UnitType.Player;

            BattleUnitRuntimeLink link = instance.GetComponent<BattleUnitRuntimeLink>();
            if (link == null)
                link = instance.AddComponent<BattleUnitRuntimeLink>();
            link.Bind(state.definition, state.formationIndex);

            result.Add(unit);
        }

        return result;
    }

    List<BattleUnit> SpawnEnemies(BattleEncounterData encounter)
    {
        List<BattleUnit> result = new List<BattleUnit>();
        if (encounter == null)
            return result;

        Transform layoutRoot = FindLayoutRoot(encounter.layoutTemplateType);
        if (layoutRoot == null)
        {
            Debug.LogError($"[BattleBootstrapper] 未找到敌方布局模板 `{encounter.layoutTemplateType}`。", this);
            return result;
        }

        int normalSlotIndex = 0;
        int addSlotIndex = 0;

        for (int i = 0; i < encounter.enemyEntries.Count; i++)
        {
            EncounterEnemyEntry entry = encounter.enemyEntries[i];
            if (entry == null || entry.enemyDefinition == null || entry.enemyDefinition.battlePrefab == null)
                continue;

            string slotName;
            if (entry.IsBossEntry)
            {
                slotName = string.IsNullOrWhiteSpace(entry.slotNameOverride) ? "BossSlot" : entry.slotNameOverride;
            }
            else if (encounter.IsBossEncounter)
            {
                addSlotIndex++;
                slotName = entry.GetSuggestedSlotName(addSlotIndex, true);
            }
            else
            {
                normalSlotIndex++;
                slotName = entry.GetSuggestedSlotName(normalSlotIndex, false);
            }

            Transform slot = FindSlot(layoutRoot, slotName);
            if (slot == null)
            {
                Debug.LogWarning($"[BattleBootstrapper] 布局 `{layoutRoot.name}` 中未找到槽位 `{slotName}`，将回退使用模板根节点。", this);
                slot = layoutRoot;
            }

            Vector3 spawnPosition = slot.position + entry.localPositionOffset;
            GameObject instance = Instantiate(entry.enemyDefinition.battlePrefab, spawnPosition, slot.rotation, enemyUnitRoot);
            instance.name = entry.enemyDefinition.displayName;

            BattleUnit unit = instance.GetComponent<BattleUnit>();
            if (unit == null)
            {
                Debug.LogError($"[BattleBootstrapper] 敌人预制体 `{entry.enemyDefinition.battlePrefab.name}` 缺少 BattleUnit 组件。", entry.enemyDefinition.battlePrefab);
                Destroy(instance);
                continue;
            }

            entry.enemyDefinition.ApplyTo(unit, true);
            unit.unitType = UnitType.Enemy;
            ApplyEncounterOverrides(entry, unit, instance.transform);

            BattleUnitRuntimeLink link = instance.GetComponent<BattleUnitRuntimeLink>();
            if (link == null)
                link = instance.AddComponent<BattleUnitRuntimeLink>();
            link.Bind(entry.enemyDefinition);

            result.Add(unit);
        }

        return result;
    }

    void ApplyEncounterOverrides(EncounterEnemyEntry entry, BattleUnit unit, Transform unitTransform)
    {
        if (entry == null || unit == null)
            return;

        if (entry.levelOverride > 0)
            unit.level = Mathf.Clamp(entry.levelOverride, 1, BattleUnit.MaxLevel);

        if (!Mathf.Approximately(entry.hpMultiplier, 1f))
        {
            unit.maxHP = Mathf.Max(1, Mathf.RoundToInt(unit.maxHP * entry.hpMultiplier));
            unit.currentHP = unit.maxHP;
        }

        if (!Mathf.Approximately(entry.attackMultiplier, 1f))
        {
            unit.physicalAttack = Mathf.Max(1, Mathf.RoundToInt(unit.physicalAttack * entry.attackMultiplier));
            unit.magicAttack = Mathf.Max(1, Mathf.RoundToInt(unit.magicAttack * entry.attackMultiplier));
        }

        if (!Mathf.Approximately(entry.scaleMultiplier, 1f) && unitTransform != null)
            unitTransform.localScale *= entry.scaleMultiplier;
    }

    void ApplyVictoryRewards(BattleManager sourceManager, BattleEncounterData encounter)
    {
        if (sourceManager == null)
            return;

        int totalExp = encounter != null ? encounter.bonusExp : 0;
        int totalJP = encounter != null ? encounter.bonusJP : 0;
        int totalMoney = encounter != null ? encounter.bonusMoney : 0;

        foreach (BattleUnit enemy in sourceManager.enemies)
        {
            if (enemy == null)
                continue;

            BattleUnitRuntimeLink link = enemy.GetComponent<BattleUnitRuntimeLink>();
            EnemyDefinition definition = link != null ? link.enemyDefinition : null;
            if (definition == null)
                continue;

            totalExp += Mathf.Max(0, definition.expReward);
            totalJP += Mathf.Max(0, definition.jpReward);
            totalMoney += Mathf.Max(0, definition.moneyReward);
        }

        foreach (BattleUnit player in sourceManager.players)
        {
            if (player != null)
            {
                player.AddExperience(totalExp);
                player.AddJP(totalJP);
            }
        }

        if (partyManager != null)
            partyManager.AddMoney(totalMoney);

        Debug.Log($"[BattleBootstrapper] 胜利奖励：全队 EXP +{totalExp}，JP +{totalJP}，金钱 +{totalMoney}。", this);
    }

    void SpawnBackground(BattleEncounterData encounter)
    {
        if (backgroundRoot == null || encounter == null || encounter.backgroundPrefab == null)
            return;

        GameObject background = Instantiate(encounter.backgroundPrefab, backgroundRoot);
        background.name = encounter.backgroundPrefab.name;
        background.transform.localPosition = Vector3.zero;
        background.transform.localRotation = Quaternion.identity;
        background.transform.localScale = Vector3.one;
    }

    Transform FindLayoutRoot(BattleLayoutTemplateType templateType)
    {
        if (enemyLayoutTemplatesRoot == null)
            return null;

        return enemyLayoutTemplatesRoot.Find(templateType.ToString());
    }

    Transform FindSlot(Transform layoutRoot, string slotName)
    {
        if (layoutRoot == null || string.IsNullOrWhiteSpace(slotName))
            return null;

        Transform direct = layoutRoot.Find(slotName);
        if (direct != null)
            return direct;

        foreach (Transform child in layoutRoot)
        {
            if (child.name == slotName)
                return child;
        }

        return null;
    }

    List<Transform> GetOrderedChildren(Transform root)
    {
        List<Transform> result = new List<Transform>();
        if (root == null)
            return result;

        foreach (Transform child in root)
            result.Add(child);

        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return result;
    }

    Transform FindScenePath(string path)
    {
        GameObject root = GameObject.Find(path);
        return root != null ? root.transform : null;
    }

    Transform FindOrCreateRoot(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
            return existing.transform;

        return new GameObject(name).transform;
    }

    Transform FindOrCreateChild(Transform parent, string name)
    {
        if (parent == null)
            return FindOrCreateRoot(name);

        Transform existing = parent.Find(name);
        if (existing != null)
            return existing;

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    void OnDrawGizmos()
    {
        if (!drawSpawnPointGizmos)
            return;

        ResolveReferences();

        DrawPlayerSpawnGizmos();
        DrawEnemySpawnGizmos();
    }

    void DrawPlayerSpawnGizmos()
    {
        List<Transform> playerSpawns = GetOrderedChildren(playerSpawnPointsRoot);
        if (playerSpawns.Count == 0)
            return;

        Color oldColor = Gizmos.color;
        Gizmos.color = playerSpawnColor;

        for (int i = 0; i < playerSpawns.Count; i++)
        {
            Transform point = playerSpawns[i];
            if (point == null)
                continue;

            Gizmos.DrawSphere(point.position, spawnPointGizmoRadius);
            Gizmos.DrawLine(point.position, point.position + point.forward * (spawnPointGizmoRadius * 2f));
        }

        Gizmos.color = oldColor;
    }

    void DrawEnemySpawnGizmos()
    {
        if (enemyLayoutTemplatesRoot == null)
            return;

        List<Transform> layoutsToDraw = new List<Transform>();

        if (drawAllEnemyLayouts)
        {
            layoutsToDraw = GetOrderedChildren(enemyLayoutTemplatesRoot);
        }
        else
        {
            BattleEncounterData encounter = ResolveEncounter();
            Transform activeLayout = encounter != null ? FindLayoutRoot(encounter.layoutTemplateType) : null;
            if (activeLayout != null)
                layoutsToDraw.Add(activeLayout);
            else
                layoutsToDraw = GetOrderedChildren(enemyLayoutTemplatesRoot);
        }

        if (layoutsToDraw.Count == 0)
            return;

        Color oldColor = Gizmos.color;
        Gizmos.color = enemySpawnColor;

        for (int i = 0; i < layoutsToDraw.Count; i++)
        {
            Transform layout = layoutsToDraw[i];
            if (layout == null)
                continue;

            List<Transform> slots = GetOrderedChildren(layout);
            for (int j = 0; j < slots.Count; j++)
            {
                Transform slot = slots[j];
                if (slot == null)
                    continue;

                if (!ShouldDrawEnemySlot(slot))
                    continue;

                Gizmos.DrawWireSphere(slot.position, spawnPointGizmoRadius);
                Gizmos.DrawLine(slot.position, slot.position + slot.forward * (spawnPointGizmoRadius * 2f));
            }
        }

        Gizmos.color = oldColor;
    }

    bool ShouldDrawEnemySlot(Transform slot)
    {
        if (slot == null)
            return false;

        if (enemySpawnPointFilter == EnemySpawnPointFilter.All)
            return true;

        string slotName = slot.name;
        if (string.IsNullOrWhiteSpace(slotName))
            return false;

        switch (enemySpawnPointFilter)
        {
            case EnemySpawnPointFilter.NormalSlot:
                return slotName.StartsWith("Slot_", System.StringComparison.OrdinalIgnoreCase);
            case EnemySpawnPointFilter.AddSlot:
                return slotName.StartsWith("AddSlot_", System.StringComparison.OrdinalIgnoreCase);
            case EnemySpawnPointFilter.BossSlot:
                return slotName.Equals("BossSlot", System.StringComparison.OrdinalIgnoreCase);
            default:
                return true;
        }
    }
}
