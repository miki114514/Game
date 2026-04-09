using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleStatusUIManager : MonoBehaviour
{
    [Header("UI容器")]
    public RectTransform container; // Canvas 下的空物体，用于垂直排列
    public GameObject playerStatusPrefab;

    [Header("敌人护盾栏（可选）")]
    public EnemyBreakPanelUI enemyBreakPanelPrefab;
    public RectTransform enemyBreakPanelParent;
    public bool autoCreateEnemyBreakPanels = true;

    private readonly List<PlayerStatus> statusUIs = new List<PlayerStatus>();
    private readonly List<EnemyBreakPanelUI> enemyBreakPanelInstances = new List<EnemyBreakPanelUI>();

    void Awake()
    {
        if (container == null)
            container = transform as RectTransform;

        if (enemyBreakPanelParent == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                enemyBreakPanelParent = parentCanvas.transform as RectTransform;
        }
    }

    // 初始化战斗UI
    public void InitStatusUI(List<BattleUnit> players)
    {
        if (container == null)
        {
            Debug.LogError("[BattleStatusUIManager] 未配置 container，无法生成人物状态UI");
            return;
        }

        if (playerStatusPrefab == null)
        {
            Debug.LogError("[BattleStatusUIManager] 未配置 playerStatusPrefab，无法生成人物状态UI");
            return;
        }

        ResolveEnemyBreakPanelPrefab();

        // 清理旧UI
        foreach (var ui in statusUIs)
        {
            if (ui != null) Destroy(ui.gameObject);
        }
        statusUIs.Clear();

        // 动态生成
        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("[BattleStatusUIManager] 玩家列表为空，未生成人物状态UI");
            return;
        }

        int count = Mathf.Min(players.Count, 4);
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(playerStatusPrefab, container);
            PlayerStatus ps = go.GetComponent<PlayerStatus>();
            if (ps == null)
            {
                Debug.LogError("[BattleStatusUIManager] playerStatusPrefab 缺少 PlayerStatus 组件");
                Destroy(go);
                continue;
            }

            ps.Init(players[i]);  // 公共初始化方法
            statusUIs.Add(ps);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(container);

        foreach (var panel in enemyBreakPanelInstances)
            panel?.ForceRefresh();
    }

    public void InitEnemyBreakUI(BattleManager battleManager, List<BattleUnit> enemies)
    {
        ClearEnemyBreakPanels();

        if (!autoCreateEnemyBreakPanels)
            return;

        EnemyBreakPanelUI panelPrefab = ResolveEnemyBreakPanelPrefab();
        if (panelPrefab == null)
            return;

        if (enemies == null || enemies.Count == 0)
            return;

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        RectTransform parent = enemyBreakPanelParent != null
            ? enemyBreakPanelParent
            : (rootCanvas != null ? rootCanvas.transform as RectTransform : transform as RectTransform);

        foreach (BattleUnit enemy in enemies)
        {
            if (enemy == null || enemy.unitType != UnitType.Enemy || enemy.maxShield <= 0)
                continue;

            EnemyBreakPanelUI panel = Instantiate(panelPrefab, parent);
            panel.name = $"{panelPrefab.name}_{enemy.unitName}";
            panel.rootCanvas = rootCanvas;
            panel.visibilityMode = EnemyBreakPanelUI.PanelVisibilityMode.BoundUnitAlwaysVisible;
            panel.BindToUnit(enemy, battleManager);
            enemyBreakPanelInstances.Add(panel);
        }
    }

    EnemyBreakPanelUI ResolveEnemyBreakPanelPrefab()
    {
        if (enemyBreakPanelPrefab != null)
            return enemyBreakPanelPrefab;

        GameObject loadedPrefab = Resources.Load<GameObject>("Prefabs/EnemyBreakPanel");
        if (loadedPrefab != null)
            enemyBreakPanelPrefab = loadedPrefab.GetComponent<EnemyBreakPanelUI>();

        if (enemyBreakPanelPrefab == null)
            Debug.LogWarning("[BattleStatusUIManager] 未找到 EnemyBreakPanel 预制体，敌方护盾栏不会自动生成");

        return enemyBreakPanelPrefab;
    }

    void ClearEnemyBreakPanels()
    {
        for (int i = 0; i < enemyBreakPanelInstances.Count; i++)
        {
            if (enemyBreakPanelInstances[i] != null)
                Destroy(enemyBreakPanelInstances[i].gameObject);
        }

        enemyBreakPanelInstances.Clear();
    }
}