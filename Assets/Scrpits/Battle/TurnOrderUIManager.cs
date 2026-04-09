using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 行动队列 HUD 管理器。
///
/// ── 场景 Canvas 层级 ─────────────────────────────────────────────
///
///  Canvas (Screen Space - Overlay / Camera)
///  └─ TurnOrderHUD                   [RectTransform] 全屏 Stretch（锚点 0,0 → 1,1）
///      ├─ CurrentTurnPanel           [RectTransform]
///      │   锚点：Left-Top (0,1)  Pivot (0,1)  Pos (20, -20)
///      │   ├─ Title                 TextMeshPro  "本回合"（可选）
///      │   └─ CurrentTurnList       HorizontalLayoutGroup
///      │       Spacing=6  ChildControlSize=OFF  ChildForceExpand=OFF
///      │       ContentSizeFitter: Horizontal=Preferred
///      │       └─ [TurnOrderItem × N]（动态生成）
///      │
///      └─ NextTurnPanel             [RectTransform]
///          锚点：Right-Top (1,1)  Pivot (1,1)  Pos (-20, -20)
///          ├─ Title                TextMeshPro  "下一回合"（可选）
///          └─ NextTurnList         HorizontalLayoutGroup
///              Spacing=4  ChildControlSize=OFF  ChildForceExpand=OFF
///              ContentSizeFitter: Horizontal=Preferred
///              全部项目 alpha=0.72（挂 CanvasGroup）
///              └─ [TurnOrderItem × N]（动态生成）
///
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class TurnOrderUIManager : MonoBehaviour
{
    [Header("容器")]
    public RectTransform currentTurnList;   // HorizontalLayoutGroup 节点
    public RectTransform nextTurnList;      // HorizontalLayoutGroup 节点

    [Header("预制体")]
    public TurnOrderItem turnOrderItemPrefab;

    [Header("布局参数")]
    public bool useManualHorizontalPack = true;
    [Range(-40f, 30f)] public float currentSpacing = -4f;
    [Range(-40f, 30f)] public float nextSpacing = -14f;
    public Vector2 iconSize = new Vector2(56f, 56f);
    public float currentSlotWidth = 44f;
    public float nextSlotWidth = 30f;
    public bool forceRebuildLayoutEachRefresh = true;

    [Header("下一回合整体透明度")]
    [Range(0.3f, 1f)]
    public float currentTurnAlpha = 1f;

    [Range(0.3f, 1f)]
    public float nextTurnAlpha = 0.72f;

    [Header("透明度调试")]
    public bool logAlphaDiagnostics = true;
    public bool isolateFromParentCanvasGroups = false;

    // ── 对象池（当前轮 / 下一轮分开）──────────────
    private readonly List<TurnOrderItem> _currentItems = new List<TurnOrderItem>();
    private readonly List<TurnOrderItem> _nextItems    = new List<TurnOrderItem>();

    private CanvasGroup _currentListCanvasGroup;
    private CanvasGroup _nextListCanvasGroup;

    // ── BattleManager 引用 ────────────────────────
    private BattleManager _battleManager;

    void Awake()
    {
        AutoAssignReferences();
    }

    void AutoAssignReferences()
    {
        if (currentTurnList == null)
            currentTurnList = FindListRect("CurrentTurnPanel/CurrentTurnList", "CurrentTurnList");

        if (nextTurnList == null)
            nextTurnList = FindListRect("NextTurnPanel/NextTurnList", "NextTurnList");
    }

    RectTransform FindListRect(params string[] candidatePaths)
    {
        foreach (string path in candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            Transform direct = transform.Find(path);
            if (direct is RectTransform directRect)
                return directRect;

            string name = path.Contains("/")
                ? path.Substring(path.LastIndexOf('/') + 1)
                : path;

            RectTransform[] allRects = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform rect in allRects)
            {
                if (rect != null && rect != transform && rect.name == name)
                    return rect;
            }
        }

        return null;
    }

    bool EnsureRuntimeReferences()
    {
        AutoAssignReferences();

        if (currentTurnList == null || nextTurnList == null)
        {
            Debug.LogError("[TurnOrderUI] CurrentTurnList / NextTurnList 未绑定，无法刷新行动顺序 UI。", this);
            return false;
        }

        if (turnOrderItemPrefab == null)
        {
            turnOrderItemPrefab = Resources.Load<TurnOrderItem>("Prefabs/TurnOrderItem");
            if (turnOrderItemPrefab != null)
            {
                Debug.LogWarning("[TurnOrderUI] `turnOrderItemPrefab` 未在 Inspector 绑定，已自动加载 `Resources/Prefabs/TurnOrderItem`。", this);
            }
            else
            {
                turnOrderItemPrefab = FindExistingItemTemplate(currentTurnList);
                if (turnOrderItemPrefab == null)
                    turnOrderItemPrefab = FindExistingItemTemplate(nextTurnList);

                if (turnOrderItemPrefab != null)
                {
                    turnOrderItemPrefab.gameObject.SetActive(false);
                    Debug.LogWarning("[TurnOrderUI] `turnOrderItemPrefab` 未在 Inspector 绑定，已自动使用列表中的 TurnOrderItem 模板。", this);
                }
                else
                {
                    turnOrderItemPrefab = CreateRuntimeFallbackItemPrefab();
                    Debug.LogWarning("[TurnOrderUI] `turnOrderItemPrefab` 为空，已创建简易运行时图标以避免报错。建议仍在 Inspector 中绑定正式的 `TurnOrderItem` 预制体。", this);
                }
            }
        }

        return turnOrderItemPrefab != null;
    }

    TurnOrderItem FindExistingItemTemplate(RectTransform container)
    {
        if (container == null)
            return null;

        TurnOrderItem[] candidates = container.GetComponentsInChildren<TurnOrderItem>(true);
        foreach (TurnOrderItem candidate in candidates)
        {
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    TurnOrderItem CreateRuntimeFallbackItemPrefab()
    {
        GameObject root = new GameObject("TurnOrderItem_RuntimeFallback", typeof(RectTransform), typeof(LayoutElement), typeof(TurnOrderItem));
        root.hideFlags = HideFlags.DontSave;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = iconSize;

        TurnOrderItem item = root.GetComponent<TurnOrderItem>();
        item.defaultGroup = CreatePortraitGroup("DefaultGroup", root.transform, out Image defaultPortrait, new Color(1f, 1f, 1f, 0.92f));
        item.activeGroup = CreatePortraitGroup("ActiveGroup", root.transform, out Image activePortrait, Color.white);
        item.defaultPortrait = defaultPortrait;
        item.activePortrait = activePortrait;

        if (item.activeGroup != null)
            item.activeGroup.SetActive(false);

        root.SetActive(false);
        return item;
    }

    GameObject CreatePortraitGroup(string groupName, Transform parent, out Image portrait, Color tint)
    {
        GameObject group = new GameObject(groupName, typeof(RectTransform));
        RectTransform groupRect = group.GetComponent<RectTransform>();
        groupRect.SetParent(parent, false);
        groupRect.anchorMin = Vector2.zero;
        groupRect.anchorMax = Vector2.one;
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;

        GameObject portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.SetParent(group.transform, false);
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;

        portrait = portraitObject.GetComponent<Image>();
        portrait.color = tint;
        portrait.raycastTarget = false;
        portrait.preserveAspect = true;

        return group;
    }

    // ==========================================================
    // 初始化（由 BattleManager.StartBattle 调用）
    // ==========================================================
    public void Init(BattleManager battleManager)
    {
        _battleManager = battleManager;

        if (!EnsureRuntimeReferences())
            return;

        ApplyLayoutSettings();

        // 当前回合：始终保持实体显示
        _currentListCanvasGroup = currentTurnList.GetComponent<CanvasGroup>();
        if (_currentListCanvasGroup == null)
            _currentListCanvasGroup = currentTurnList.gameObject.AddComponent<CanvasGroup>();
        _currentListCanvasGroup.alpha = currentTurnAlpha;
        _currentListCanvasGroup.ignoreParentGroups = isolateFromParentCanvasGroups;

        // 确保 NextTurnList 挂有 CanvasGroup，用于整体控制透明度
        _nextListCanvasGroup = nextTurnList.GetComponent<CanvasGroup>();
        if (_nextListCanvasGroup == null)
            _nextListCanvasGroup = nextTurnList.gameObject.AddComponent<CanvasGroup>();
        _nextListCanvasGroup.alpha = nextTurnAlpha;
        _nextListCanvasGroup.ignoreParentGroups = isolateFromParentCanvasGroups;

        if (logAlphaDiagnostics)
            LogAlphaDiagnostics();

        RefreshAll();
    }

    void LogAlphaDiagnostics()
    {
        if (currentTurnList == null || nextTurnList == null)
            return;

        float currentEffective = GetEffectiveAlpha(currentTurnList);
        float nextEffective = GetEffectiveAlpha(nextTurnList);

        Debug.Log($"[TurnOrderUI] Alpha检查 Current={currentEffective:F2}, Next={nextEffective:F2} (currentTurnAlpha={currentTurnAlpha:F2}, nextTurnAlpha={nextTurnAlpha:F2})", this);

        if (currentEffective < 0.99f)
        {
            Debug.LogWarning("[TurnOrderUI] 当前回合队列有效透明度 < 1。可能存在父级 CanvasGroup.alpha < 1 影响。", this);
        }
    }

    float GetEffectiveAlpha(RectTransform target)
    {
        float alpha = 1f;
        Transform t = target;
        while (t != null)
        {
            CanvasGroup cg = t.GetComponent<CanvasGroup>();
            if (cg != null)
                alpha *= cg.alpha;
            t = t.parent;
        }
        return alpha;
    }

    void ApplyLayoutSettings()
    {
        ConfigureContainerForLeftStart(currentTurnList);
        ConfigureContainerForLeftStart(nextTurnList);
        ApplySpacing(currentTurnList, currentSpacing);
        ApplySpacing(nextTurnList, nextSpacing);
    }

    void ConfigureContainerForLeftStart(RectTransform listRoot)
    {
        if (listRoot == null) return;
        listRoot.anchorMin = new Vector2(0f, 0.5f);
        listRoot.anchorMax = new Vector2(0f, 0.5f);
        listRoot.pivot = new Vector2(0f, 0.5f);
        listRoot.localScale = Vector3.one;
    }

    void ApplySpacing(RectTransform listRoot, float spacing)
    {
        if (listRoot == null) return;

        HorizontalLayoutGroup layout = listRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = listRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

        ContentSizeFitter fitter = listRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = listRoot.gameObject.AddComponent<ContentSizeFitter>();

        if (useManualHorizontalPack)
        {
            layout.enabled = false;
            fitter.enabled = false;
            return;
        }

        layout.enabled = true;
        fitter.enabled = true;

        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        // 让 LayoutGroup 统一接管子项尺寸，避免节点各自尺寸造成“斜向错位”
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childScaleWidth = false;
        layout.childScaleHeight = false;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // ==========================================================
    // 刷新接口（BattleManager 在以下时机调用）
    //   1. AdvanceTurn 后        → RefreshCurrentOrder
    //   2. ApplyBreak/ApplyStatus → RefreshNextOrder（+RefreshStateMarks）
    //   3. 单位死亡后             → RefreshAll
    //   4. 新一轮开始后           → RefreshAll
    // ==========================================================

    /// <summary>刷新整个 HUD（当前轮 + 下一轮）</summary>
    public void RefreshAll()
    {
        if (_battleManager == null) return;
        RefreshCurrentOrder(_battleManager.CurrentTurnIndexInRound);
        RefreshNextOrder();
    }

    /// <summary>仅刷新当前回合队列（高亮移动到 activeIndex）</summary>
    public void RefreshCurrentOrder(int activeIndex)
    {
        if (_battleManager == null || !EnsureRuntimeReferences()) return;
        var order = _battleManager.CurrentOrderList;
        SyncItemList(_currentItems, currentTurnList, order, activeIndex);
    }

    /// <summary>仅刷新下一回合队列（全部为默认状态）</summary>
    public void RefreshNextOrder()
    {
        if (_battleManager == null || !EnsureRuntimeReferences()) return;
        var order = _battleManager.NextOrderList;
        SyncItemList(_nextItems, nextTurnList, order, activeIndex: -1);
    }

    /// <summary>刷新所有已生成节点的状态标记（Break/Status 变化后调用）</summary>
    public void RefreshStateMarks()
    {
        foreach (var item in _currentItems) item.RefreshStateMark();
        foreach (var item in _nextItems)    item.RefreshStateMark();
    }

    // ==========================================================
    // 内部同步
    // ==========================================================

    /// <summary>
    /// 将 itemList 与 order 数量对齐，按需增删节点，然后逐个 SetUnit。
    /// activeIndex < 0 表示全部为非激活状态（下一轮列表使用）。
    /// </summary>
    void SyncItemList(
        List<TurnOrderItem> itemList,
        RectTransform container,
        IReadOnlyList<BattleUnit> order,
        int activeIndex)
    {
        if (container == null || order == null)
            return;

        if (turnOrderItemPrefab == null)
        {
            Debug.LogError("[TurnOrderUI] `turnOrderItemPrefab` 为空，无法生成人物行动顺序节点。", this);
            return;
        }

        // 扩容：按需实例化
        while (itemList.Count < order.Count)
        {
            var go   = Instantiate(turnOrderItemPrefab, container);
            go.name  = $"TurnItem_{itemList.Count}";
            ConfigureLayoutElement(go.gameObject, GetSlotWidth(container));
            itemList.Add(go);
        }

        // 激活 / 配置已有节点
        for (int i = 0; i < order.Count; i++)
        {
            itemList[i].gameObject.SetActive(true);
            // 关键：每次刷新都重设槽位宽度，确保 Inspector 改值立即生效
            ConfigureLayoutElement(itemList[i].gameObject, GetSlotWidth(container));
            itemList[i].SetUnit(order[i], i == activeIndex);
        }

        // 隐藏多余节点（单位死亡后列表变短）
        for (int i = order.Count; i < itemList.Count; i++)
            itemList[i].gameObject.SetActive(false);

        if (useManualHorizontalPack)
        {
            PackItemsHorizontally(itemList, container, order.Count, activeIndex, GetListSpacing(container));
        }

        if (forceRebuildLayoutEachRefresh)
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);
    }

    float GetListSpacing(RectTransform container)
    {
        if (container == currentTurnList)
            return currentSpacing;
        if (container == nextTurnList)
            return nextSpacing;
        return 0f;
    }

    void PackItemsHorizontally(
        List<TurnOrderItem> itemList,
        RectTransform container,
        int visibleCount,
        int activeIndex,
        float spacing)
    {
        ConfigureContainerForLeftStart(container);

        float x = 0f;
        float prevWidth = 0f;
        float maxHeight = 0f;

        for (int i = 0; i < visibleCount; i++)
        {
            bool isActive = (i == activeIndex);
            TurnOrderItem item = itemList[i];
            RectTransform rt = item.GetComponent<RectTransform>();
            if (rt == null) continue;

            float visualWidth = item.GetVisualWidth(isActive, iconSize.x);
            float visualHeight = iconSize.y;
            maxHeight = Mathf.Max(maxHeight, visualHeight);

            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localEulerAngles = Vector3.zero;

            if (i == 0)
                x = visualWidth * 0.5f;
            else
                x += (prevWidth * 0.5f) + (visualWidth * 0.5f) + spacing;

            rt.anchoredPosition = new Vector2(x, 0f);
            prevWidth = visualWidth;
        }

        float totalWidth = visibleCount > 0 ? (x + prevWidth * 0.5f) : 0f;
        container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
        container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxHeight);
    }

    float GetSlotWidth(RectTransform container)
    {
        if (container == currentTurnList)
            return currentSlotWidth;
        if (container == nextTurnList)
            return nextSlotWidth;
        return iconSize.x;
    }

    void ConfigureLayoutElement(GameObject item, float slotWidth)
    {
        RectTransform rt = item.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localEulerAngles = Vector3.zero;
            rt.localScale = Vector3.one;
            rt.sizeDelta = iconSize;
        }

        LayoutElement element = item.GetComponent<LayoutElement>();
        if (element == null)
            element = item.AddComponent<LayoutElement>();

        // 0 或负值会导致节点被压成 1px，视觉上像“只有前几个叠压”
        float width = Mathf.Max(1f, slotWidth <= 0f ? iconSize.x : slotWidth);
        float height = Mathf.Max(1f, iconSize.y);

        element.minWidth = width;
        element.minHeight = height;
        element.preferredWidth = width;
        element.preferredHeight = height;
        element.flexibleWidth = 0;
        element.flexibleHeight = 0;
    }
}
