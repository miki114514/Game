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
    public GameObject commandMenuPrefab;
    public GameObject subCommandPanelPrefab;  // ✅ 新增：次级菜单预制体

    private CommandMenuUi currentCommandMenuUi;
    private SubCommandPanelUi currentSubCommandPanelUi;  // ✅ 新增：缓存次级菜单

    [Header("当前行动单位 (只读)")]
    [SerializeField] private BattleUnit currentUnit;
    public BattleUnit CurrentUnit => currentUnit;

    private List<BattleUnit> turnOrder = new List<BattleUnit>();
    private int currentTurnIndex = 0;

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
        ResetBattle();
        InitTurnOrder();
        Debug.Log("[BattleManager] 战斗初始化完成");
    }

    void InitTurnOrder()
    {
        turnOrder.Clear();
        turnOrder.AddRange(players);
        turnOrder.AddRange(enemies);
        turnOrder.Sort((a, b) => b.speed.CompareTo(a.speed));

        currentTurnIndex = 0;
        currentUnit = turnOrder[currentTurnIndex];
        Debug.Log("[BattleManager] 当前回合单位: " + currentUnit.name);

        StartTurn();
    }

    void StartTurn()
    {
        if (currentUnit.currentHP <= 0)
        {
            AdvanceTurn();
            return;
        }

        Debug.Log("[BattleManager] 开始回合: " + currentUnit.name);

        if (currentUnit.unitType == UnitType.Player)
            EnterCommandSelect();
        else
            StartCoroutine(EnemyTurnCoroutine(currentUnit));
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
                    // ✅ 确保初始时隐藏
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
        }
        else if (currentCommandMenuUi != null)
        {
            currentCommandMenuUi.gameObject.SetActive(true);
            if (currentSubCommandPanelUi != null)
            {
                // ✅ 确保次级菜单隐藏
                currentSubCommandPanelUi.gameObject.SetActive(false);
                currentSubCommandPanelUi.isActive = false;
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
                skill?.Execute(unit, target);
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
        int damage = Mathf.Max(attacker.physicalAttack - target.physicalDefense, 0);
        target.TakeDamage(damage);
        Debug.Log($"[BattleManager] {attacker.name} 攻击 {target.name}, 造成 {damage} 伤害");
    }

    void AdvanceTurn()
    {
        do
        {
            currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
            currentUnit = turnOrder[currentTurnIndex];
        } while (currentUnit.currentHP <= 0);

        Debug.Log("[BattleManager] 下一回合单位: " + currentUnit.name);
        StartTurn();
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
            p.currentHP = p.maxHP;
            p.currentSP = p.maxSP;
        }

        foreach (var e in enemies)
        {
            e.currentHP = e.maxHP;
            e.currentSP = e.maxSP;
        }

        Debug.Log("[BattleManager] 重置所有单位状态");
    }

    public List<Item> GetUsableItems()
    {
        return Inventory.Instance.GetAllItems();
    }

    private IEnumerator EnemyTurnCoroutine(BattleUnit enemy)
    {
        yield return new WaitForSeconds(0.5f);
        BattleUnit target = players.Find(p => p.currentHP > 0);
        BasicAttack(enemy, target);
        yield return new WaitForSeconds(0.5f);
        AdvanceTurn();
    }
}