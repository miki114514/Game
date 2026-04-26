using System;
using System.Collections;
using System.Collections.Generic;
using Conditional = System.Diagnostics.ConditionalAttribute;
using UnityEngine;
using BattleSystem;
using PlayerCommand;

[DefaultExecutionOrder(100)]
public class BattleManager : MonoBehaviour
{
    [Header("战斗单位")]
    public List<BattleUnit> players;
    public List<BattleUnit> enemies;

    [Header("战斗启动 / 结算")]
    public BattleBootstrapper battleBootstrapper;
    public bool preservePlayerResourcesBetweenBattles = true;

    [Header("战斗状态")]
    public BattleState state;

    [Header("UI")]
    public Canvas uiCanvas;
    public BattleStatusUIManager statusUIManager;
    public TurnOrderUIManager turnOrderUIManager;
    public GameObject commandMenuPrefab;
    public GameObject subCommandPanelPrefab;
    public GameObject confirmSubCommandPanelPrefab;

    [Header("玩家回合位移")]
    public bool enablePlayerTurnStepMove = true;
    [Min(0f)] public float playerTurnStepDistance = 0.45f;
    [Min(0.01f)] public float playerTurnStepDuration = 0.18f;
    [Min(0.01f)] public float playerTurnReturnDuration = 0.16f;
    [Min(0f)] public float commandActionEndDelay = 0.02f;
    [Min(0.1f)] public float actionAnimationWaitTimeout = 8f;

    private CommandMenuUi currentCommandMenuUi;
    private SubCommandPanelUi currentSubCommandPanelUi;
    private ConfirmSubCommandPanelUi currentConfirmSubCommandPanelUi;

    [Header("当前行动单位 (只读)")]
    [SerializeField] private BattleUnit currentUnit;
    public BattleUnit CurrentUnit => currentUnit;
    public BattleUnit CurrentSelectedTarget
    {
        get
        {
            if (state != BattleState.TargetSelect || selectableEnemies == null || selectableEnemies.Count == 0)
                return null;

            if (targetIndex < 0 || targetIndex >= selectableEnemies.Count)
                return null;

            return selectableEnemies[targetIndex];
        }
    }

    [Header("BP / Boost 预览")]
    [SerializeField, Range(0, BattleFormula.MaxBoostLevel)] private int selectedBoostLevel = 0;
    public int SelectedBoostLevel => selectedBoostLevel;
    public event Action<BattleUnit, int, int> OnBoostSelectionChanged;

    private TurnOrderSystem turnOrderSystem = new TurnOrderSystem();
    private int currentTurnIndexInRound = 0;

    // ── TurnOrderUIManager 需要的对外属性 ──────────────
    public int CurrentTurnIndexInRound => currentTurnIndexInRound;
    public IReadOnlyList<BattleUnit> CurrentOrderList => turnOrderSystem.CurrentOrder;
    public IReadOnlyList<BattleUnit> NextOrderList => turnOrderSystem.NextOrder;

    [Header("目标选择")]
    public GameObject enemySelectArrowPrefab;
    private GameObject currentArrow;
    private List<BattleUnit> selectableEnemies = new List<BattleUnit>();
    private int targetIndex = 0;

    private BattleCommand pendingCommand;
    private Skill pendingSkill;
    private Item pendingItem;

    private bool waitConfirmRelease = false;
    private bool hasReportedBattleEnd = false;
    private readonly Dictionary<BattleUnit, Vector3> playerTurnStepAnchorPositions = new Dictionary<BattleUnit, Vector3>();
    private readonly Dictionary<BattleUnit, HashSet<Skill>> oncePerBattleSkillUsage = new Dictionary<BattleUnit, HashSet<Skill>>();
    private bool isExecutingPlayerCommandFlow = false;

    void Start()
    {
        if (battleBootstrapper == null)
            battleBootstrapper = FindObjectOfType<BattleBootstrapper>();

        if ((players == null || players.Count == 0 || enemies == null || enemies.Count == 0) && battleBootstrapper != null)
            battleBootstrapper.PrepareBattle();

        Debug.Log("[BattleManager] 战斗开始");
        StartBattle();
    }

    void Update()
    {
        if (state == BattleState.TargetSelect)
            HandleTargetInput();
    }

    void LateUpdate()
    {
        if (state == BattleState.CommandSelect)
            UpdateCommandMenuPosition();

        if (state == BattleState.TargetSelect && currentArrow != null)
            UpdateArrowPosition();
    }

    void StartBattle()
    {
        if (players == null)
            players = new List<BattleUnit>();
        if (enemies == null)
            enemies = new List<BattleUnit>();

        if (players.Count == 0 || enemies.Count == 0)
        {
            Debug.LogWarning("[BattleManager] 玩家或敌人列表为空，已中止战斗初始化。请先通过 BattleBootstrapper 准备战斗单位。", this);
            return;
        }

        hasReportedBattleEnd = false;
        state = BattleState.Start;
        EnsureStatusUIManager();
        ResetBattle();
        statusUIManager?.InitStatusUI(this, players);
        statusUIManager?.InitEnemyBreakUI(this, enemies);
        StartCoroutine(BeginTurnFlowAfterPresentationCoroutine());
        Debug.Log("[BattleManager] 战斗初始化完成");
    }

    IEnumerator BeginTurnFlowAfterPresentationCoroutine()
    {
        yield return WaitForBattlePresentationCompletionCoroutine();

        InitTurnOrder();
        turnOrderUIManager?.Init(this);
    }

    IEnumerator WaitForBattlePresentationCompletionCoroutine()
    {
        // 真实等待所有单位入场结束，避免首回合位移被入场协程覆盖。
        const float timeoutSeconds = 8f;
        float elapsed = 0f;

        while (IsAnyBattlePresentationPlaying())
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeoutSeconds)
            {
                UnityEngine.Debug.LogWarning("[BattleManager] 等待入场演出超时，已强制进入回合流程");
                break;
            }

            yield return null;
        }
    }

    bool IsAnyBattlePresentationPlaying()
    {
        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                BattleUnit player = players[i];
                if (player != null && player.IsBattlePresentationPlaying)
                    return true;
            }
        }

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                BattleUnit enemy = enemies[i];
                if (enemy != null && enemy.IsBattlePresentationPlaying)
                    return true;
            }
        }

        return false;
    }

    void EnsureStatusUIManager()
    {
        if (statusUIManager == null)
            statusUIManager = FindObjectOfType<BattleStatusUIManager>();

        if (statusUIManager == null)
            Debug.LogWarning("[BattleManager] 未找到 BattleStatusUIManager，人物状态UI不会显示");
    }

    void InitTurnOrder()
    {
        List<BattleUnit> allUnits = new List<BattleUnit>(players);
        allUnits.AddRange(enemies);
        turnOrderSystem.Initialize(allUnits);

        if (turnOrderSystem.CurrentOrder == null || turnOrderSystem.CurrentOrder.Count == 0)
        {
            Debug.LogWarning("[BattleManager] 当前没有可行动单位，无法初始化行动顺序。", this);
            return;
        }

        currentTurnIndexInRound = 0;
        currentUnit = turnOrderSystem.CurrentOrder[0];
        turnOrderUIManager?.RefreshAll();
        StartTurn();
    }

    void StartTurn()
    {
        if (currentUnit == null || currentUnit.currentHP <= 0)
        {
            AdvanceTurn();
            return;
        }

        currentUnit.BeginTurn();
        currentUnit.HandleTurnStartBP();
        ResetSelectedBoost();

        // ── Break 状态：按剩余次数跳过行动 ──
        if (currentUnit.ConsumeBreakActionSkip())
        {
            Debug.Log($"[BattleManager] {currentUnit.unitName} 处于 Break 状态，跳过行动");
            AdvanceTurn();
            return;
        }

        // ── 回合开始处理 DoT（Poison / Burn）──
        if (!ProcessTurnStartEffects(currentUnit))
        {
            CheckBattleEnd();
            if (state != BattleState.End)
                AdvanceTurn();
            return;
        }

        // ── 无法行动：Sleep / Freeze ──
        if (!currentUnit.CanAct)
        {
            string reason = currentUnit.HasStatus(StatusEffectType.Sleep) ? "Sleep" : "Freeze";
            Debug.Log($"[BattleManager] {currentUnit.unitName} 无法行动（{reason}）");
            AdvanceTurn();
            return;
        }

        // ── Shock：50% 概率行动失败 ──
        if (currentUnit.IsShocked && UnityEngine.Random.value < 0.5f)
        {
            Debug.Log($"[BattleManager] {currentUnit.unitName} 受 Shock 影响，行动失败！");
            AdvanceTurn();
            return;
        }

        // ── Confuse：随机攻击己方 ──
        if (currentUnit.IsConfused)
        {
            StartCoroutine(ConfusedActionCoroutine(currentUnit));
            return;
        }

        Debug.Log("[BattleManager] 开始回合: " + currentUnit.unitName);

        if (currentUnit.unitType == UnitType.Player)
            StartCoroutine(PlayerTurnEnterCommandSelectCoroutine(currentUnit));
        else
            StartCoroutine(EnemyTurnCoroutine(currentUnit));
    }

    IEnumerator PlayerTurnEnterCommandSelectCoroutine(BattleUnit playerUnit)
    {
        if (playerUnit == null)
            yield break;

        // 每次玩家行动前先回到锚点，避免位移在多回合中累积。
        Vector3 anchorPosition = GetOrCreatePlayerTurnStepAnchorPosition(playerUnit);
        playerUnit.transform.position = anchorPosition;
        DevLogTurnStepMeta($"TurnEnter | Unit={playerUnit.unitName} | enablePlayerTurnStepMove={enablePlayerTurnStepMove} | playerTurnStepDistance={playerTurnStepDistance:0.###} | playerTurnStepDuration={playerTurnStepDuration:0.###}");

        if (!enablePlayerTurnStepMove)
        {
            DevLogTurnStepMeta($"ForwardSkip | Unit={playerUnit.unitName} | Reason=enablePlayerTurnStepMove is false");
            EnterCommandSelect();
            yield break;
        }

        float distance = playerTurnStepDistance > 0.001f ? playerTurnStepDistance : 0.45f;
        float duration = playerTurnStepDuration > 0.001f ? playerTurnStepDuration : 0.18f;
        if (distance <= 0f)
        {
            DevLogTurnStepMeta($"ForwardSkip | Unit={playerUnit.unitName} | Reason=distance <= 0 | resolvedDistance={distance:0.###}");
            EnterCommandSelect();
            yield break;
        }

        state = BattleState.Busy;

        Vector3 startPos = playerUnit.transform.position;
        Vector3 moveDir = ResolvePlayerTurnStepDirection(playerUnit);
        Vector3 targetPos = startPos + moveDir * distance;
        DevLogTurnStep("ForwardStart", playerUnit, startPos, targetPos, anchorPosition);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            playerUnit.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        playerUnit.transform.position = targetPos;
        DevLogTurnStep("ForwardEnd", playerUnit, startPos, playerUnit.transform.position, anchorPosition);

        if (state != BattleState.End && currentUnit == playerUnit)
            EnterCommandSelect();
    }

    /// <summary>处理回合开始的持续伤害（Poison / Burn），返回存活否</summary>
    bool ProcessTurnStartEffects(BattleUnit unit)
    {
        if (unit.HasStatus(StatusEffectType.Poison))
        {
            int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.maxHP * 0.05f));
            unit.TakeDamage(dmg);
            Debug.Log($"[Status] {unit.unitName} 中毒，损失 {dmg} HP");
            if (unit.currentHP <= 0) return false;
        }
        if (unit.HasStatus(StatusEffectType.Burn))
        {
            int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.maxHP * 0.03f));
            unit.TakeDamage(dmg);
            Debug.Log($"[Status] {unit.unitName} 灸烧，损失 {dmg} HP");
            if (unit.currentHP <= 0) return false;
        }
        return true;
    }

    /// <summary>混乱状态：随机攻击己方随机目标</summary>
    IEnumerator ConfusedActionCoroutine(BattleUnit confusedUnit)
    {
        state = BattleState.Busy;
        yield return new WaitForSeconds(0.5f);

        List<BattleUnit> friendlyTargets = confusedUnit.unitType == UnitType.Player
            ? players.FindAll(p => p.currentHP > 0)
            : enemies.FindAll(e => e.currentHP > 0);

        if (friendlyTargets.Count > 0)
        {
            BattleUnit randomTarget = friendlyTargets[UnityEngine.Random.Range(0, friendlyTargets.Count)];
            Debug.Log($"[Status] {confusedUnit.unitName} 陷入混乱，随机攻击 {randomTarget.unitName}！");
            BasicAttack(confusedUnit, randomTarget);
        }

        yield return new WaitForSeconds(0.5f);
        CheckBattleEnd();
        if (state != BattleState.End)
            AdvanceTurn();
    }

    void EnterCommandSelect(bool resetBoost = true)
    {
        state = BattleState.CommandSelect;

        if (resetBoost)
            ResetSelectedBoost();
        else
            NotifyBoostSelectionChanged();

        if (currentCommandMenuUi == null && commandMenuPrefab != null && currentUnit != null)
        {
            // ✅ 创建 CommandPanel
            GameObject uiObj = Instantiate(commandMenuPrefab, uiCanvas.transform);
            currentCommandMenuUi = uiObj.GetComponent<CommandMenuUi>();
            currentCommandMenuUi.battleManager = this;
            Debug.Log("[BattleManager] 创建指令菜单 UI");

            // ✅ 创建 SubCommandPanel（但默认隐藏）
            if (subCommandPanelPrefab != null)
            {
                GameObject subPanelObj = Instantiate(subCommandPanelPrefab, uiCanvas.transform);
                currentSubCommandPanelUi = subPanelObj.GetComponent<SubCommandPanelUi>();

                if (currentSubCommandPanelUi != null)
                {
                    currentSubCommandPanelUi.gameObject.SetActive(false);
                    currentSubCommandPanelUi.isActive = false;
                    currentSubCommandPanelUi.battleManager = this;
                    currentCommandMenuUi.subCommandPanelUi = currentSubCommandPanelUi;
                    Debug.Log("[BattleManager] 创建次级菜单 UI 并关联（初始隐藏）");
                }
                else
                {
                    Debug.LogError("[BattleManager] SubCommandPanel 未能获取 SubCommandPanelUi 组件");
                }
            }
            else
            {
                Debug.LogWarning("[BattleManager] subCommandPanelPrefab 未配置");
            }

            // 创建 ConfirmSubCommandPanel（初始隐藏）
            if (confirmSubCommandPanelPrefab != null)
            {
                GameObject confirmPanelObj = Instantiate(confirmSubCommandPanelPrefab, uiCanvas.transform);
                currentConfirmSubCommandPanelUi = confirmPanelObj.GetComponent<ConfirmSubCommandPanelUi>();
                if (currentConfirmSubCommandPanelUi != null)
                {
                    currentConfirmSubCommandPanelUi.gameObject.SetActive(false);
                    currentConfirmSubCommandPanelUi.isActive = false;
                    currentConfirmSubCommandPanelUi.battleManager = this;
                    currentCommandMenuUi.confirmSubCommandPanelUi = currentConfirmSubCommandPanelUi;
                    Debug.Log("[BattleManager] 创建确认次级菜单 UI 并关联（初始隐藏）");
                }
                else
                {
                    Debug.LogError("[BattleManager] ConfirmSubCommandPanel 未能获取 ConfirmSubCommandPanelUi 组件");
                }
            }
            else
            {
                Debug.LogWarning("[BattleManager] confirmSubCommandPanelPrefab 未配置");
            }
        }
        else if (currentCommandMenuUi != null)
        {
            currentCommandMenuUi.gameObject.SetActive(true);
            if (currentSubCommandPanelUi != null)
            {
                currentSubCommandPanelUi.gameObject.SetActive(false);
                currentSubCommandPanelUi.isActive = false;
            }
            if (currentConfirmSubCommandPanelUi != null)
            {
                currentConfirmSubCommandPanelUi.gameObject.SetActive(false);
                currentConfirmSubCommandPanelUi.isActive = false;
            }
            Debug.Log("[BattleManager] 指令菜单 UI 激活");
        }
    }

    void UpdateCommandMenuPosition()
    {
        if (currentCommandMenuUi == null || currentUnit == null) return;

        Vector3 worldPos = currentUnit.transform.position;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        float offsetX = screenPos.x > Screen.width / 2 ? -220f : 220f;
        float offsetY = 50f;

        Vector3 offsetWorld = new Vector3(offsetX / 100f, offsetY / 100f, 0f);
        Vector3 finalWorldPos = currentUnit.transform.position + offsetWorld;
        Vector3 finalScreenPos = Camera.main.WorldToScreenPoint(finalWorldPos);

        RectTransform canvasRect = uiCanvas.GetComponent<RectTransform>();
        RectTransform uiRect = currentCommandMenuUi.GetComponent<RectTransform>();

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            finalScreenPos,
            uiCanvas.worldCamera,
            out localPos
        );

        uiRect.anchoredPosition = localPos;
    }

    public void OnCommandSelected(BattleCommand cmd, Skill skill = null, Item item = null)
    {
        if (state == BattleState.Busy || state == BattleState.TargetSelect)
            return;

        if (cmd == BattleCommand.Skill && skill != null && !CanUseCharacterSkillThisBattle(currentUnit, skill))
        {
            Debug.Log($"[BattleManager] {skill.skillName} 为每场一次技能，当前战斗已使用");
            EnterCommandSelect(false);
            return;
        }

        pendingCommand = cmd;
        pendingSkill = skill;
        pendingItem = item;

        Debug.Log("[BattleManager] 玩家选择命令: " + cmd);

        if ((cmd == BattleCommand.Skill || cmd == BattleCommand.Arts) && skill != null && skill.targetType == SkillTargetType.Self)
        {
            ExecuteCommand(currentUnit, cmd, skill, item, currentUnit);
            return;
        }

        if (cmd == BattleCommand.Attack || cmd == BattleCommand.Skill || cmd == BattleCommand.Arts || cmd == BattleCommand.Item)
        {
            StartCoroutine(StartTargetSelectionFlow());
        }
        else
        {
            ExecuteCommand(currentUnit, cmd, skill, item, null);
        }
    }

    /// <summary>
    /// 次级菜单关闭回调（用户按下Escape返回主菜单）
    /// </summary>
    public void OnSubMenuClosed()
    {
        Debug.Log("[BattleManager] 次级菜单已关闭");
        // 这里可以添加其他逻辑，如果需要
    }

    IEnumerator StartTargetSelectionFlow()
    {
        state = BattleState.TargetSelect;

        if (currentCommandMenuUi != null)
            currentCommandMenuUi.gameObject.SetActive(false);

        // 决定可选目标
        if (pendingCommand == BattleCommand.Item && pendingItem != null)
            selectableEnemies = players.FindAll(p => p.currentHP > 0);  // 道具作用玩家
        else if ((pendingCommand == BattleCommand.Skill || pendingCommand == BattleCommand.Arts) && pendingSkill != null)
            selectableEnemies = GetSelectableTargetsForSkill(currentUnit, pendingSkill);
        else
            selectableEnemies = currentUnit.unitType == UnitType.Player
                ? enemies.FindAll(e => e.currentHP > 0)
                : players.FindAll(p => p.currentHP > 0);

        if (selectableEnemies.Count == 0)
        {
            Debug.Log("[BattleManager] 没有可选目标");

            if (pendingCommand == BattleCommand.Skill
                && pendingSkill != null
                && pendingSkill.characterSkillMechanic == CharacterSkillMechanic.ForceBreakOnDamagedShieldEnemy)
            {
                Debug.Log($"[BattleManager] {pendingSkill.skillName} 当前无已削盾目标，按空效果执行以结束回合");
                ExecuteCommand(currentUnit, pendingCommand, pendingSkill, pendingItem, null);
                yield break;
            }

            state = BattleState.CommandSelect;
            yield break;
        }

        targetIndex = 0;
        CreateArrow();
        UpdateArrowPosition();

        waitConfirmRelease = true;
        yield return new WaitUntil(() => !Input.GetKey(KeyCode.Return));
        waitConfirmRelease = false;
    }

    void HandleTargetInput()
    {
        if (waitConfirmRelease) return;

        if (Input.GetKeyDown(KeyCode.W))
        {
            targetIndex = (targetIndex - 1 + selectableEnemies.Count) % selectableEnemies.Count;
            UpdateArrowPosition();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            targetIndex = (targetIndex + 1) % selectableEnemies.Count;
            UpdateArrowPosition();
        }

        if (Input.GetKeyDown(KeyCode.E))
            TryAdjustBoostLevel(1, pendingCommand, pendingSkill);

        if (Input.GetKeyDown(KeyCode.Q))
            TryAdjustBoostLevel(-1, pendingCommand, pendingSkill);

        if (Input.GetKeyDown(KeyCode.Return))
            ConfirmTarget();

        if (Input.GetKeyDown(KeyCode.Escape))
            CancelTargetSelection();
    }

    void ConfirmTarget()
    {
        BattleUnit target = selectableEnemies[targetIndex];
        Destroy(currentArrow);
        Debug.Log("[BattleManager] 目标确认: " + target.name);
        ExecuteCommand(currentUnit, pendingCommand, pendingSkill, pendingItem, target);
    }

    void CancelTargetSelection()
    {
        Destroy(currentArrow);
        Debug.Log("[BattleManager] 目标选择取消");
        EnterCommandSelect(false);
    }

    public void ExecuteCommand(BattleUnit unit, BattleCommand cmd, Skill skill, Item item, BattleUnit target)
    {
        if (unit == null)
            return;

        if (unit.unitType == UnitType.Player && isExecutingPlayerCommandFlow)
            return;

        StartCoroutine(ExecuteCommandFlowCoroutine(unit, cmd, skill, item, target));
    }

    IEnumerator ExecuteCommandFlowCoroutine(BattleUnit unit, BattleCommand cmd, Skill skill, Item item, BattleUnit target)
    {
        bool isPlayerUnit = unit != null && unit.unitType == UnitType.Player;
        if (isPlayerUnit)
            isExecutingPlayerCommandFlow = true;

        state = BattleState.Busy;

        Debug.Log($"[BattleManager] 执行命令: {cmd}, 施法者: {unit.name}, 目标: {(target != null ? target.name : "无")}");

        int runtimeBoostLevel = 0;
        float actionWaitSeconds = 0f;

        switch (cmd)
        {
            case BattleCommand.Attack:
                runtimeBoostLevel = ConsumeSelectedBoost(unit, cmd, skill);
                BasicAttack(unit, target, runtimeBoostLevel);
                actionWaitSeconds = EstimateAttackActionDuration(unit, runtimeBoostLevel);
                break;
            case BattleCommand.Skill:
            case BattleCommand.Arts:
                if (!unit.CanUseSkill)
                {
                    Debug.Log($"[Status] {unit.unitName} 处于沉默状态，无法使用技能");
                    break;
                }
                if (skill == null)
                {
                    Debug.LogWarning($"[BattleManager] {unit.unitName} 没有选择技能，命令取消");
                    break;
                }
                if (cmd == BattleCommand.Skill && !CanUseCharacterSkillThisBattle(unit, skill))
                {
                    Debug.Log($"[BattleManager] {unit.unitName} 的专属技能 {skill.skillName} 本战已使用，命令取消");
                    break;
                }
                if (!unit.HasEnoughSP(skill.costSP))
                {
                    Debug.Log($"[BattleManager] {unit.unitName} 的 SP 不足，无法使用 {skill.skillName}（需要 {skill.costSP}，当前 {unit.currentSP}）");
                    break;
                }
                runtimeBoostLevel = ConsumeSelectedBoost(unit, cmd, skill);
                if (cmd == BattleCommand.Skill)
                    RegisterCharacterSkillUsage(unit, skill);
                unit.PlaySkillAnimationWithFallback(skill);
                skill.Execute(this, unit, target, runtimeBoostLevel);
                actionWaitSeconds = EstimateSkillActionDuration(unit, skill);
                break;
            case BattleCommand.Item:
                UseItem(unit, item, target);
                actionWaitSeconds = 0.08f;
                break;
            case BattleCommand.Defend:
                unit.SetDefending(true);
                Debug.Log($"[BattleManager] {unit.unitName} 进入防御姿态");
                actionWaitSeconds = 0.08f;
                break;
            case BattleCommand.Run:
                actionWaitSeconds = 0.08f;
                break;
        }

        if (!IsBoostableCommand(cmd, skill))
            ResetSelectedBoost();

        yield return WaitForCommandActionCompletionCoroutine(unit, cmd, skill, actionWaitSeconds);

        if (isPlayerUnit)
            yield return ReturnPlayerToAnchorCoroutine(unit);

        CheckBattleEnd();

        if (state != BattleState.End)
            AdvanceTurn();

        if (isPlayerUnit)
            isExecutingPlayerCommandFlow = false;
    }

    float EstimateAttackActionDuration(BattleUnit attacker, int boostLevel)
    {
        if (attacker == null || !attacker.playBattleActionAnimation)
            return 0f;

        int totalHits = BattleFormula.GetBoostedAttackHitCount(boostLevel);
        float perHit = Mathf.Max(0f, attacker.normalAttackAnimationFallbackDuration);
        return Mathf.Max(0f, totalHits) * perHit;
    }

    float EstimateSkillActionDuration(BattleUnit unit, Skill skill)
    {
        if (unit == null || !unit.playBattleActionAnimation)
            return 0f;

        if (skill != null && !string.IsNullOrWhiteSpace(skill.animationStateName))
            return Mathf.Max(0f, skill.animationFallbackDuration);

        return Mathf.Max(0f, unit.normalAttackAnimationFallbackDuration);
    }

    IEnumerator WaitForCommandActionCompletionCoroutine(BattleUnit unit, BattleCommand cmd, Skill skill, float estimatedWaitSeconds)
    {
        float extraDelay = Mathf.Max(0f, commandActionEndDelay);
        if (unit == null)
        {
            float noUnitWait = Mathf.Max(0f, estimatedWaitSeconds) + extraDelay;
            if (noUnitWait > 0f)
                yield return new WaitForSeconds(noUnitWait);
            yield break;
        }

        bool isActionCommand = cmd == BattleCommand.Attack || cmd == BattleCommand.Skill || cmd == BattleCommand.Arts;
        bool shouldTrackActionAnimation = isActionCommand && unit.playBattleActionAnimation;

        if (shouldTrackActionAnimation)
        {
            float timeout = Mathf.Max(0.1f, actionAnimationWaitTimeout);
            float elapsed = 0f;

            // 等一帧，确保 Animator 播放状态与标记已同步。
            yield return null;

            while (unit != null && unit.IsActionAnimationPlaying)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= timeout)
                {
                    Debug.LogWarning($"[BattleManager] 等待 {unit.unitName} 行动动画结束超时（{timeout:0.##}s），已继续流程");
                    break;
                }

                yield return null;
            }

            if (extraDelay > 0f)
                yield return new WaitForSeconds(extraDelay);

            yield break;
        }

        float fallbackWait = Mathf.Max(0f, estimatedWaitSeconds) + extraDelay;
        if (fallbackWait > 0f)
            yield return new WaitForSeconds(fallbackWait);
    }

    IEnumerator ReturnPlayerToAnchorCoroutine(BattleUnit playerUnit)
    {
        if (playerUnit == null)
            yield break;

        Vector3 targetAnchor = GetOrCreatePlayerTurnStepAnchorPosition(playerUnit);
        float duration = Mathf.Max(0.01f, playerTurnReturnDuration);
        Vector3 startPos = playerUnit.transform.position;
        DevLogTurnStep("ReturnStart", playerUnit, startPos, targetAnchor, targetAnchor);

        if (Vector3.Distance(startPos, targetAnchor) <= 0.0001f)
        {
            playerUnit.transform.position = targetAnchor;
            DevLogTurnStep("ReturnEnd", playerUnit, startPos, playerUnit.transform.position, targetAnchor);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            playerUnit.transform.position = Vector3.Lerp(startPos, targetAnchor, t);
            yield return null;
        }

        playerUnit.transform.position = targetAnchor;
        DevLogTurnStep("ReturnEnd", playerUnit, startPos, playerUnit.transform.position, targetAnchor);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    void DevLogTurnStep(string phase, BattleUnit unit, Vector3 startPos, Vector3 endPos, Vector3 anchorPos)
    {
        if (unit == null)
            return;

        UnityEngine.Debug.Log(
            $"[TurnStep] {phase} | Unit={unit.unitName} | Start={startPos} | End={endPos} | Anchor={anchorPos} | CurrentState={state}");
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    void DevLogTurnStepMeta(string message)
    {
        UnityEngine.Debug.Log($"[TurnStep] {message}");
    }

    void UseItem(BattleUnit user, Item item, BattleUnit target)
    {
        if (item == null || target == null) return;
        item.Use(user, target);
        Inventory.Instance.RemoveItem(item, 1);
        Debug.Log("[BattleManager] 使用道具: " + item.itemName + " 目标: " + target.name);
    }

    void BasicAttack(BattleUnit attacker, BattleUnit target, int boostLevel = 0)
    {
        if (attacker == null || target == null)
            return;

        int totalHits = BattleFormula.GetBoostedAttackHitCount(boostLevel);
        attacker.PlayNormalAttackAnimation(totalHits);

        int successHits = 0;
        int totalDamage = 0;

        for (int i = 0; i < totalHits; i++)
        {
            if (target.currentHP <= 0)
                break;

            if (!attacker.CheckHit(target))
            {
                Debug.Log($"[BattleManager] {attacker.unitName} 的普通攻击第 {i + 1}/{totalHits} 段未命中 {target.unitName}");
                continue;
            }

            int damage = BattleFormula.CalculateNormalAttackDamage(attacker, target);

            if (attacker.CheckCrit())
                damage = Mathf.Min(9999, Mathf.RoundToInt(damage * 1.5f));

            target.TakeDamage(damage);
            TryApplyShieldDamage(
                target,
                attacker.GetResolvedNormalAttackWeaponType(),
                attacker.GetResolvedNormalAttackElementType(),
                1);

            successHits++;
            totalDamage += damage;
            Debug.Log($"[BattleManager] {attacker.unitName} 的普通攻击第 {i + 1}/{totalHits} 段命中 {target.unitName}，造成 {damage} 伤害");
        }

        if (successHits == 0)
            Debug.Log($"[BattleManager] {attacker.unitName} 的普通攻击全部未命中 {target.unitName}");
        else
            Debug.Log($"[BattleManager] {attacker.unitName} 攻击 {target.unitName}，共命中 {successHits}/{totalHits} 段，总伤害 {totalDamage}（Boost {boostLevel}）");
    }

    void AdvanceTurn()
    {
        // 回合结束：递减当前单位的状态持续时间
        currentUnit?.TickStatusEffects();

        currentTurnIndexInRound++;

        // 跳过本轮中已死亡的单位
        while (currentTurnIndexInRound < turnOrderSystem.CurrentOrder.Count
               && turnOrderSystem.CurrentOrder[currentTurnIndexInRound].currentHP <= 0)
        {
            currentTurnIndexInRound++;
        }

        // 本轮所有单位已行动完毕，进入下一轮
        if (currentTurnIndexInRound >= turnOrderSystem.CurrentOrder.Count)
        {
            List<BattleUnit> alive = GetAllAliveUnits();
            if (alive.Count == 0) return;

            turnOrderSystem.AdvanceToNextRound(alive);
            currentTurnIndexInRound = 0;

            // 跳过新轮头部已死亡单位（极端情况保护）
            while (currentTurnIndexInRound < turnOrderSystem.CurrentOrder.Count
                   && turnOrderSystem.CurrentOrder[currentTurnIndexInRound].currentHP <= 0)
            {
                currentTurnIndexInRound++;
            }
        }

        if (currentTurnIndexInRound >= turnOrderSystem.CurrentOrder.Count)
        {
            Debug.LogWarning("[BattleManager] 没有可行动的存活单位");
            return;
        }

        currentUnit = turnOrderSystem.CurrentOrder[currentTurnIndexInRound];
        Debug.Log("[BattleManager] 下一行动单位: " + currentUnit.unitName);
        turnOrderUIManager?.RefreshCurrentOrder(currentTurnIndexInRound);
        StartTurn();
    }

    Vector3 GetOrCreatePlayerTurnStepAnchorPosition(BattleUnit playerUnit)
    {
        if (playerUnit == null)
            return Vector3.zero;

        if (playerTurnStepAnchorPositions.TryGetValue(playerUnit, out Vector3 anchorPosition))
            return anchorPosition;

        anchorPosition = playerUnit.transform.position;
        playerTurnStepAnchorPositions[playerUnit] = anchorPosition;
        return anchorPosition;
    }

    Vector3 ResolvePlayerTurnStepDirection(BattleUnit playerUnit)
    {
        if (playerUnit == null)
            return Vector3.right;

        List<BattleUnit> aliveEnemies = enemies != null
            ? enemies.FindAll(e => e != null && e.currentHP > 0)
            : null;

        if (aliveEnemies != null && aliveEnemies.Count > 0)
        {
            float avgEnemyX = 0f;
            for (int i = 0; i < aliveEnemies.Count; i++)
                avgEnemyX += aliveEnemies[i].transform.position.x;

            avgEnemyX /= aliveEnemies.Count;
            float deltaX = avgEnemyX - playerUnit.transform.position.x;
            if (Mathf.Abs(deltaX) > 0.001f)
                return deltaX > 0f ? Vector3.right : Vector3.left;
        }

        return Vector3.right;
    }

    List<BattleUnit> GetAllAliveUnits()
    {
        var result = new List<BattleUnit>();
        foreach (var p in players) if (p != null && p.currentHP > 0) result.Add(p);
        foreach (var e in enemies) if (e != null && e.currentHP > 0) result.Add(e);
        return result;
    }

    /// <summary>旧接口兼容：基于单一 AttackType 的弱点判定</summary>
    public void TryApplyShieldDamage(BattleUnit target, AttackType attackType, int hitCount)
    {
        TryApplyShieldDamage(
            target,
            BattleFormula.ToWeaponType(attackType),
            BattleFormula.ToElementType(attackType),
            hitCount,
            false);
    }

    /// <summary>命中后尝试按武器/属性弱点削减护盾并触发 Break</summary>
    public void TryApplyShieldDamage(BattleUnit target, WeaponType weaponType, ElementType elementType, int hitCount, bool countBothWeaknessesSeparately = false)
    {
        if (target == null)
            return;

        bool didBreak = target.ApplyShieldDamage(weaponType, elementType, hitCount, countBothWeaknessesSeparately);
        if (!didBreak)
            return;

        int skipTurns = CalculateBreakSkipTurns(target);
        ApplyBreak(target, skipTurns);
    }

    int CalculateBreakSkipTurns(BattleUnit target)
    {
        if (target == null)
            return 1;

        int targetIndexInCurrentRound = -1;
        for (int i = 0; i < turnOrderSystem.CurrentOrder.Count; i++)
        {
            if (turnOrderSystem.CurrentOrder[i] == target)
            {
                targetIndexInCurrentRound = i;
                break;
            }
        }

        if (targetIndexInCurrentRound < 0)
            return 1;

        // 本轮未行动：本轮立即打断 + 下一轮跳过；已行动：仅下一轮跳过
        bool notActedYet = targetIndexInCurrentRound > currentTurnIndexInRound;
        return notActedYet ? 2 : 1;
    }

    /// <summary>外部调用：使目标进入 Break 状态，自动重算下轮顺序</summary>
    public void ApplyBreak(BattleUnit unit, int skipTurns = 1)
    {
        if (unit == null)
            return;

        unit.EnterBreak(skipTurns);
        Debug.Log($"[BattleManager] {unit.unitName} 进入 Break 状态！");
        turnOrderSystem.RecalculateNextRound(GetAllAliveUnits());
        turnOrderUIManager?.RefreshNextOrder();
        turnOrderUIManager?.RefreshStateMarks();
    }

    /// <summary>外部调用：为目标施加异常状态，自动重算下轮顺序</summary>
    public void ApplyStatus(BattleUnit target, StatusEffectType type, int rounds)
    {
        if (target == null || type == StatusEffectType.None)
            return;

        target.ApplyStatusEffect(new StatusEffect(type, rounds));
        turnOrderSystem.RecalculateNextRound(GetAllAliveUnits());
        turnOrderUIManager?.RefreshNextOrder();
        turnOrderUIManager?.RefreshStateMarks();
    }

    public bool IsBoostableCommand(BattleCommand cmd, Skill skill = null)
    {
        bool commandSupportsBoost = cmd == BattleCommand.Attack || cmd == BattleCommand.Arts;
        if (!commandSupportsBoost)
            return false;

        return skill == null || skill.CanBoost;
    }

    public bool TryAdjustBoostLevel(int delta, BattleCommand cmd, Skill skill = null)
    {
        if (!IsBoostableCommand(cmd, skill) || currentUnit == null)
        {
            if (selectedBoostLevel != 0)
                selectedBoostLevel = 0;

            NotifyBoostSelectionChanged();
            return false;
        }

        int oldBoostLevel = selectedBoostLevel;
        selectedBoostLevel = Mathf.Clamp(selectedBoostLevel + delta, 0, currentUnit.GetMaxAvailableBoostLevel());

        if (oldBoostLevel != selectedBoostLevel)
            Debug.Log($"[BP] {currentUnit.unitName} 当前设定 Boost {selectedBoostLevel}（BP {currentUnit.CurrentBP}/{currentUnit.MaxBP}）");

        NotifyBoostSelectionChanged();
        return oldBoostLevel != selectedBoostLevel;
    }

    public string GetBoostPreviewLabel(BattleCommand cmd, Skill skill = null)
    {
        if (!IsBoostableCommand(cmd, skill) || currentUnit == null)
            return string.Empty;

        string boostCore = skill != null
            ? skill.GetBoostPreviewText(selectedBoostLevel)
            : $"Boost {selectedBoostLevel}";

        return string.IsNullOrEmpty(boostCore)
            ? string.Empty
            : $"{boostCore}  BP {currentUnit.CurrentBP}/{currentUnit.MaxBP}";
    }

    void ResetSelectedBoost(bool notify = true)
    {
        selectedBoostLevel = 0;

        if (notify)
            NotifyBoostSelectionChanged();
    }

    void NotifyBoostSelectionChanged()
    {
        int currentBP = currentUnit != null ? currentUnit.CurrentBP : 0;
        OnBoostSelectionChanged?.Invoke(currentUnit, selectedBoostLevel, currentBP);
    }

    int ConsumeSelectedBoost(BattleUnit unit, BattleCommand cmd, Skill skill = null)
    {
        if (unit == null || !IsBoostableCommand(cmd, skill))
        {
            ResetSelectedBoost();
            return 0;
        }

        int actualBoostLevel = unit.ConsumeBoostLevel(selectedBoostLevel);
        ResetSelectedBoost();
        return actualBoostLevel;
    }

    List<BattleUnit> GetSelectableTargetsForSkill(BattleUnit actingUnit, Skill skill)
    {
        if (actingUnit == null || skill == null)
            return new List<BattleUnit>();

        if (skill.targetType == SkillTargetType.Self)
            return new List<BattleUnit> { actingUnit };

        bool targetAllies = skill.targetType == SkillTargetType.AllySingle;
        bool actingPlayer = actingUnit.unitType == UnitType.Player;

        List<BattleUnit> result;

        if (targetAllies)
        {
            result = actingPlayer
                ? players.FindAll(unit => unit.currentHP > 0)
                : enemies.FindAll(unit => unit.currentHP > 0);
        }
        else
        {
            result = actingPlayer
                ? enemies.FindAll(unit => unit.currentHP > 0)
                : players.FindAll(unit => unit.currentHP > 0);
        }

        if (skill.characterSkillMechanic == CharacterSkillMechanic.ForceBreakOnDamagedShieldEnemy)
            result = result.FindAll(IsForceBreakEligibleTarget);

        return result;
    }

    public bool CanUseCharacterSkillThisBattle(BattleUnit user, Skill skill)
    {
        if (user == null || skill == null)
            return false;

        if (!skill.limitUseOncePerBattle)
            return true;

        if (!oncePerBattleSkillUsage.TryGetValue(user, out HashSet<Skill> usedSkills) || usedSkills == null)
            return true;

        return !usedSkills.Contains(skill);
    }

    void RegisterCharacterSkillUsage(BattleUnit user, Skill skill)
    {
        if (user == null || skill == null || !skill.limitUseOncePerBattle)
            return;

        if (!oncePerBattleSkillUsage.TryGetValue(user, out HashSet<Skill> usedSkills) || usedSkills == null)
        {
            usedSkills = new HashSet<Skill>();
            oncePerBattleSkillUsage[user] = usedSkills;
        }

        usedSkills.Add(skill);
    }

    public bool TryExecuteCharacterSkillMechanic(Skill skill, BattleUnit user, BattleUnit target, int runtimeBoostLevel)
    {
        if (skill == null || user == null)
            return false;

        switch (skill.characterSkillMechanic)
        {
            case CharacterSkillMechanic.NextRoundAlliesActFirst:
                return TryApplyNextRoundFactionPriority(user);

            case CharacterSkillMechanic.ForceBreakOnDamagedShieldEnemy:
                return TryApplyForceBreakOnDamagedShieldEnemy(target);

            default:
                return false;
        }
    }

    bool TryApplyNextRoundFactionPriority(BattleUnit user)
    {
        if (user == null)
            return false;

        List<BattleUnit> aliveUnits = GetAllAliveUnits();
        if (aliveUnits.Count == 0)
            return false;

        turnOrderSystem.ForceNextRoundFactionPriority(aliveUnits, user.unitType);
        turnOrderUIManager?.RefreshNextOrder();
        Debug.Log($"[BattleManager] {user.unitName} 触发专属机制：下回合 {user.unitType} 阵营优先行动");
        return true;
    }

    bool TryApplyForceBreakOnDamagedShieldEnemy(BattleUnit target)
    {
        if (!IsForceBreakEligibleTarget(target))
            return false;

        int skipTurns = CalculateBreakSkipTurns(target);
        bool forced = target.ForceBreakNow(skipTurns);
        if (!forced)
            return false;

        Debug.Log($"[BattleManager] {target.unitName} 被专属机制强制 Break（无视剩余护盾）");
        turnOrderSystem.RecalculateNextRound(GetAllAliveUnits());
        turnOrderUIManager?.RefreshNextOrder();
        turnOrderUIManager?.RefreshStateMarks();
        return true;
    }

    public bool IsForceBreakEligibleTarget(BattleUnit target)
    {
        if (target == null)
            return false;

        if (target.unitType != UnitType.Enemy)
            return false;

        if (target.currentHP <= 0 || target.isBreak || target.maxShield <= 0)
            return false;

        // 仅允许“已被削盾但尚未 Break”的目标。
        return target.currentShield > 0 && target.currentShield < target.maxShield;
    }

    void CheckBattleEnd()
    {
        bool playersDefeated = players == null || players.Count == 0 || players.TrueForAll(p => p == null || p.currentHP <= 0);
        bool enemiesDefeated = enemies == null || enemies.Count == 0 || enemies.TrueForAll(e => e == null || e.currentHP <= 0);

        if (!playersDefeated && !enemiesDefeated)
            return;

        state = BattleState.End;

        if (hasReportedBattleEnd)
            return;

        hasReportedBattleEnd = true;
        bool isVictory = !playersDefeated && enemiesDefeated;

        Debug.Log(isVictory ? "[BattleManager] 战斗结束：玩家胜利" : "[BattleManager] 战斗结束：玩家败北");
        battleBootstrapper?.HandleBattleEnded(this, isVictory);
    }

    void CreateArrow()
    {
        if (enemySelectArrowPrefab != null && selectableEnemies.Count > 0)
        {
            if (currentArrow != null) Destroy(currentArrow);
            currentArrow = Instantiate(enemySelectArrowPrefab, uiCanvas.transform); // 确保在Canvas下
            RectTransform rect = currentArrow.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            UpdateArrowPosition();
            Debug.Log("[BattleManager] 创建敌人选择箭头");
        }
    }

    void UpdateArrowPosition()
    {
        if (currentArrow == null || selectableEnemies.Count == 0 || targetIndex < 0 || targetIndex >= selectableEnemies.Count)
            return;

        BattleUnit target = selectableEnemies[targetIndex];
        if (target == null)
            return;

        Camera sourceCamera = ResolveCanvasEventCamera();
        Vector3 worldPos = GetTargetArrowWorldPosition(target);
        Vector3 screenPos = (sourceCamera != null ? sourceCamera : Camera.main).WorldToScreenPoint(worldPos);

        RectTransform canvasRect = uiCanvas.GetComponent<RectTransform>();
        RectTransform arrowRect = currentArrow.GetComponent<RectTransform>();

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            sourceCamera,
            out localPos
        );

        arrowRect.anchoredPosition = localPos;
    }

    Camera ResolveCanvasEventCamera()
    {
        if (uiCanvas == null || uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return uiCanvas.worldCamera != null ? uiCanvas.worldCamera : Camera.main;
    }

    Vector3 GetTargetArrowWorldPosition(BattleUnit target)
    {
        if (target == null)
            return Vector3.zero;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds bounds = new Bounds(target.transform.position, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
            return new Vector3(bounds.center.x, bounds.max.y + 0.35f, bounds.center.z);

        return target.transform.position + Vector3.up * 2f;
    }

    public void ResetBattle()
    {
        ResetSelectedBoost();
        playerTurnStepAnchorPositions.Clear();
        oncePerBattleSkillUsage.Clear();

        foreach (var p in players)
        {
            if (p == null)
                continue;

            ResetUnitForBattleStart(p, preservePlayerResourcesBetweenBattles);
        }

        foreach (var e in enemies)
        {
            if (e == null)
                continue;

            ResetUnitForBattleStart(e, false);
        }

        Debug.Log("[BattleManager] 重置所有单位状态");
    }

    void ResetUnitForBattleStart(BattleUnit unit, bool preserveCurrentResources)
    {
        if (unit == null)
            return;

        if (!unit.gameObject.activeSelf)
            unit.gameObject.SetActive(true);

        int savedHP = unit.currentHP;
        int savedSP = unit.currentSP;
        int savedExp = unit.currentExp;
        int savedExpToNextLevel = unit.expToNextLevel;

        unit.InitializeBattleState();
        unit.ClearAllStatusEffects();
        unit.PlayBattlePresentation(true);

        if (unit.unitType == UnitType.Player)
            playerTurnStepAnchorPositions[unit] = unit.SpawnPoint;

        if (!preserveCurrentResources)
            return;

        unit.currentHP = Mathf.Clamp(savedHP, 0, unit.maxHP);
        unit.currentSP = Mathf.Clamp(savedSP, 0, unit.maxSP);
        unit.currentExp = Mathf.Max(0, savedExp);
        unit.expToNextLevel = Mathf.Max(0, savedExpToNextLevel);
    }

    public List<Item> GetUsableItems()
    {
        return Inventory.Instance.GetAllItems();
    }

    private IEnumerator EnemyTurnCoroutine(BattleUnit enemy)
    {
        state = BattleState.Busy;
        yield return new WaitForSeconds(0.5f);
        BattleUnit target = players.Find(p => p.currentHP > 0);
        BasicAttack(enemy, target);
        yield return new WaitForSeconds(0.5f);
        CheckBattleEnd();
        if (state != BattleState.End)
            AdvanceTurn();
    }
}