using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BattleSystem;
using PlayerCommand;

public class BattleManager : MonoBehaviour
{
    [Header("战斗单位")]
    public List<BattleUnit> players;
    public List<BattleUnit> enemies;

    [Header("战斗状态")]
    public BattleState state;

    [Header("UI")]
    public Canvas uiCanvas;
    public BattleStatusUIManager statusUIManager;
    public TurnOrderUIManager turnOrderUIManager;
    public GameObject commandMenuPrefab;
    public GameObject subCommandPanelPrefab;
    public GameObject confirmSubCommandPanelPrefab;

    private CommandMenuUi currentCommandMenuUi;
    private SubCommandPanelUi currentSubCommandPanelUi;
    private ConfirmSubCommandPanelUi currentConfirmSubCommandPanelUi;

    [Header("当前行动单位 (只读)")]
    [SerializeField] private BattleUnit currentUnit;
    public BattleUnit CurrentUnit => currentUnit;

    private TurnOrderSystem turnOrderSystem = new TurnOrderSystem();
    private int currentTurnIndexInRound = 0;

    // ── TurnOrderUIManager 需要的对外属性 ──────────────
    public int CurrentTurnIndexInRound => currentTurnIndexInRound;
    public IReadOnlyList<BattleUnit> CurrentOrderList => turnOrderSystem.CurrentOrder;
    public IReadOnlyList<BattleUnit> NextOrderList    => turnOrderSystem.NextOrder;

    [Header("目标选择")]
    public GameObject enemySelectArrowPrefab;
    private GameObject currentArrow;
    private List<BattleUnit> selectableEnemies = new List<BattleUnit>();
    private int targetIndex = 0;

    private BattleCommand pendingCommand;
    private Skill pendingSkill;
    private Item pendingItem;

    private bool waitConfirmRelease = false;

    void Start()
    {
        Debug.Log("[BattleManager] 战斗开始");
        StartBattle();
    }

    void Update()
    {
        if (state == BattleState.CommandSelect)
            UpdateCommandMenuPosition();

        if (state == BattleState.TargetSelect)
            HandleTargetInput();
    }

    void StartBattle()
    {
        state = BattleState.Start;
        EnsureStatusUIManager();
        ResetBattle();
        statusUIManager?.InitStatusUI(players);
        InitTurnOrder();
        turnOrderUIManager?.Init(this);
        Debug.Log("[BattleManager] 战斗初始化完成");
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
            EnterCommandSelect();
        else
            StartCoroutine(EnemyTurnCoroutine(currentUnit));
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

    void EnterCommandSelect()
    {
        state = BattleState.CommandSelect;

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

        pendingCommand = cmd;
        pendingSkill = skill;
        pendingItem = item;

        Debug.Log("[BattleManager] 玩家选择命令: " + cmd);

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
        else
            selectableEnemies = enemies.FindAll(e => e.currentHP > 0);  // 攻击/技能作用敌人

        if (selectableEnemies.Count == 0)
        {
            Debug.Log("[BattleManager] 没有可选目标");
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
        EnterCommandSelect();
    }

    public void ExecuteCommand(BattleUnit unit, BattleCommand cmd, Skill skill, Item item, BattleUnit target)
    {
        state = BattleState.Busy;

        Debug.Log($"[BattleManager] 执行命令: {cmd}, 施法者: {unit.name}, 目标: {(target != null ? target.name : "无")}");

        switch (cmd)
        {
            case BattleCommand.Attack:
                BasicAttack(unit, target);
                break;
            case BattleCommand.Skill:
            case BattleCommand.Arts:
                if (!unit.CanUseSkill)
                {
                    Debug.Log($"[Status] {unit.unitName} 处于沉默状态，无法使用技能");
                    break;
                }
                skill?.Execute(this, unit, target);
                break;
            case BattleCommand.Item:
                UseItem(unit, item, target);
                break;
            case BattleCommand.Defend:
                break;
            case BattleCommand.Run:
                break;
        }

        CheckBattleEnd();

        if (state != BattleState.End)
            AdvanceTurn();
    }

    void UseItem(BattleUnit user, Item item, BattleUnit target)
    {
        if (item == null || target == null) return;
        item.Use(user, target);
        Inventory.Instance.RemoveItem(item, 1);
        Debug.Log("[BattleManager] 使用道具: " + item.itemName + " 目标: " + target.name);
    }

    void BasicAttack(BattleUnit attacker, BattleUnit target)
    {
        if (target == null) return;

        if (!attacker.CheckHit(target))
        {
            Debug.Log($"[BattleManager] {attacker.unitName} 的普通攻击未命中 {target.unitName}");
            return;
        }

        // Terror 降低攻击方攻击力，Freeze 增加被攻方受伤
        int baseAtk = Mathf.RoundToInt(attacker.physicalAttack * attacker.AttackMultiplier);
        int damage  = Mathf.Max(baseAtk - target.physicalDefense, 0);

        if (attacker.CheckCrit())
            damage = Mathf.RoundToInt(damage * 1.5f);

        damage = Mathf.RoundToInt(damage * target.IncomingDamageMultiplier);
        target.TakeDamage(damage);

        TryApplyShieldDamage(target, attacker.normalAttackType, 1);
        Debug.Log($"[BattleManager] {attacker.unitName} 攻击 {target.unitName}, 造成 {damage} 伤害");
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

    List<BattleUnit> GetAllAliveUnits()
    {
        var result = new List<BattleUnit>();
        foreach (var p in players) if (p != null && p.currentHP > 0) result.Add(p);
        foreach (var e in enemies) if (e != null && e.currentHP > 0) result.Add(e);
        return result;
    }

    /// <summary>命中后尝试削减护盾并触发 Break</summary>
    public void TryApplyShieldDamage(BattleUnit target, AttackType attackType, int hitCount)
    {
        if (target == null)
            return;

        bool didBreak = target.ApplyShieldDamage(attackType, hitCount);
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
        target.ApplyStatusEffect(new StatusEffect(type, rounds));
        turnOrderSystem.RecalculateNextRound(GetAllAliveUnits());
        turnOrderUIManager?.RefreshNextOrder();
        turnOrderUIManager?.RefreshStateMarks();
    }

    void CheckBattleEnd()
    {
        if (players.TrueForAll(p => p.currentHP <= 0) || enemies.TrueForAll(e => e.currentHP <= 0))
        {
            state = BattleState.End;
            Debug.Log("[BattleManager] 战斗结束");
        }
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
        if (currentArrow != null && selectableEnemies.Count > 0)
        {
            Vector3 worldPos = selectableEnemies[targetIndex].transform.position + Vector3.up * 2f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            RectTransform canvasRect = uiCanvas.GetComponent<RectTransform>();
            RectTransform arrowRect = currentArrow.GetComponent<RectTransform>();

            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                uiCanvas.worldCamera,
                out localPos
            );

            arrowRect.anchoredPosition = localPos;
        }
    }

    public void ResetBattle()
    {
        foreach (var p in players)
        {
            p.InitializeBattleState();
            p.ClearAllStatusEffects();
        }

        foreach (var e in enemies)
        {
            e.InitializeBattleState();
            e.ClearAllStatusEffects();
        }

        Debug.Log("[BattleManager] 重置所有单位状态");
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