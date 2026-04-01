using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using BattleSystem;
using PlayerCommand;

/// <summary>
/// 次级指令面板脚本
/// 挂载在 SubCommandPanel 节点上
/// 与 BattleManager + CommandMenuUi 配合使用
/// </summary>
public class SubCommandPanelUi : MonoBehaviour
{
    [Header("基础UI元素")]
    public RectTransform content;              // Content RectTransform
    public GameObject itemPrefab;              // Item预制体 (从Inspector中直接拖入)
    public RectTransform arrow;                // 箭头
    public ScrollRect scrollRect;              // ✅ 新增：ScrollRect 用于瀑布流

    [Header("箭头偏移")]
    public Vector2 arrowOffset = new Vector2(-50f, 0f);

    [Header("引用")]
    public BattleManager battleManager;
    public CommandMenuUi commandMenuUi;  // ✅ 新增：用于返回主菜单

    private List<SubCommand> currentSubCommands = new List<SubCommand>();
    private List<GameObject> itemInstances = new List<GameObject>();
    private int selectedIndex = 0;
    public bool isActive = false;  // ✅ 改为 public，便于 CommandMenuUi 控制
    private BattleCommand parentCommand;

    /// <summary>
    /// 显示次级菜单（带父命令）
    /// </summary>
    public void ShowSubCommands(List<SubCommand> commands, BattleCommand parent)
    {
        parentCommand = parent;
        ShowSubCommands(commands);
    }

    /// <summary>
    /// 显示次级菜单
    /// </summary>
    public void ShowSubCommands(List<SubCommand> commands)
    {
        if (commands == null || commands.Count == 0)
        {
            Debug.LogError("[SubCommandPanelUi] 命令列表为空或null");
            return;
        }

        if (itemPrefab == null)
        {
            Debug.LogError("[SubCommandPanelUi] itemPrefab 未配置！请在Inspector中拖入Item预制体");
            return;
        }

        if (content == null)
        {
            Debug.LogError("[SubCommandPanelUi] content 未配置！请在Inspector中设置");
            return;
        }

        ClearItems();
        currentSubCommands = new List<SubCommand>(commands);
        selectedIndex = 0;
        ResetScrollPosition();

        Debug.Log($"[SubCommandPanelUi] 开始生成 {commands.Count} 个Item...");

        for (int i = 0; i < commands.Count; i++)
        {
            try
            {
                // ✅ 先实例化（不指定parent），再设置parent
                GameObject itemObj = Instantiate(itemPrefab);
                if (itemObj == null)
                {
                    Debug.LogError($"[SubCommandPanelUi] Instantiate 返回 null (索引: {i})");
                    continue;
                }

                itemObj.name = $"Item_{i:D2}";
                itemObj.transform.SetParent(content, false);
                itemObj.transform.localScale = Vector3.one;
                itemObj.transform.localPosition = Vector3.zero;
                NormalizeItemLayout(itemObj);
                itemInstances.Add(itemObj);

                // ✅ 设置文字（Text 在 Item 根目录下）
                var text = itemObj.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = commands[i].name;
                    Debug.Log($"[SubCommandPanelUi] Item_{i:D2} 文字已设置: {commands[i].name}");
                }
                else
                {
                    Debug.LogWarning($"[SubCommandPanelUi] Item_{i:D2} 未找到 Text 组件");
                }

                // ✅ 控制 Base 和 Select 的显示（Select 下有 HighLight）
                var baseObj = itemObj.transform.Find("Base")?.gameObject;
                var selectObj = itemObj.transform.Find("Select")?.gameObject;

                if (baseObj != null) baseObj.SetActive(true);   // 默认显示 Base
                if (selectObj != null) selectObj.SetActive(false); // 默认隐藏 Select

                Debug.Log($"[SubCommandPanelUi] 生成Item_{i:D2}: {commands[i].name}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SubCommandPanelUi] 生成Item时出错: {ex.Message}\n{ex.StackTrace}");
            }
        }

        if (itemInstances.Count == 0)
        {
            Debug.LogError("[SubCommandPanelUi] 未成功生成任何Item！");
            isActive = false;
            return;
        }

        // ✅ 不要在这里调用 UpdateSelectionUI()，等待延迟初始化
        isActive = true;
        gameObject.SetActive(true);

        // ✅ 延迟初始化滚动位置，等待 VerticalLayoutGroup 计算完毕
        StartCoroutine(InitializeScrollingWithDelay());

        Debug.Log($"[SubCommandPanelUi] ✅ 次级菜单已显示，共 {itemInstances.Count} 项，isActive={isActive}");
    }

    /// <summary>
    /// 隐藏次级菜单
    /// </summary>
    public void HideSubCommands()
    {
        isActive = false;
        gameObject.SetActive(false);
        ResetScrollPosition();
        ClearItems();
        Debug.Log("[SubCommandPanelUi] 次级菜单已隐藏");
    }

    void ResetScrollPosition()
    {
        if (content != null)
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.velocity = Vector2.zero;
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    void NormalizeItemLayout(GameObject itemObj)
    {
        RectTransform itemRect = itemObj.GetComponent<RectTransform>();
        if (itemRect == null)
        {
            return;
        }

        LayoutElement layoutElement = itemObj.GetComponent<LayoutElement>();
        float preferredHeight = 30f;
        if (layoutElement != null && layoutElement.preferredHeight > 0f)
        {
            preferredHeight = layoutElement.preferredHeight;
        }

        itemRect.anchorMin = new Vector2(0.5f, 1f);
        itemRect.anchorMax = new Vector2(0.5f, 1f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x <= 0f ? 100f : itemRect.sizeDelta.x, preferredHeight);

        CenterChildRect(itemObj.transform.Find("Base") as RectTransform);
        CenterChildRect(itemObj.transform.Find("Select") as RectTransform);
        CenterChildRect(itemObj.transform.Find("Text") as RectTransform);
    }

    void CenterChildRect(RectTransform childRect)
    {
        if (childRect == null)
        {
            return;
        }

        childRect.anchorMin = new Vector2(0.5f, 0.5f);
        childRect.anchorMax = new Vector2(0.5f, 0.5f);
        childRect.pivot = new Vector2(0.5f, 0.5f);
        childRect.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// ✅ 延迟初始化滚动位置，等待 VerticalLayoutGroup 完成布局计算
    /// </summary>
    private System.Collections.IEnumerator InitializeScrollingWithDelay()
    {
        // 等待一帧，让 Canvas 和 Layout Group 完成计算
        yield return new WaitForEndOfFrame();

        if (scrollRect != null && itemInstances.Count > 0 && content != null)
        {
            // 📌 强制更新布局
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            ResetScrollPosition();

            Debug.Log($"[SubCommandPanelUi] 初始化前 - selectedIndex={selectedIndex}, Content大小={content.rect.size}, Content位置={content.anchoredPosition}");

            // 📌 现在调用 UpdateSelectionUI，这会驱动 ScrollToSelected()
            // 此时 Item 高度已经计算完成
            UpdateSelectionUI();

            yield return null;

            // 📌 再次更新箭头位置以确保准确
            UpdateArrowPosition();

            Debug.Log($"[SubCommandPanelUi] 初始化完成 - Content位置={content.anchoredPosition}");
        }
    }

    /// <summary>
    /// 清理已生成Item
    /// </summary>
    void ClearItems()
    {
        foreach (var go in itemInstances)
            Destroy(go);
        itemInstances.Clear();
        Debug.Log("[SubCommandPanelUi] 已清空所有Item");
    }

    void Update()
    {
        if (!isActive) return;

        HandleInput();
        UpdateArrowPosition();
    }

    void HandleInput()
    {
        // ✅ 防守：如果菜单为空，不处理任何输入
        if (currentSubCommands.Count == 0 || itemInstances.Count == 0)
        {
            Debug.LogWarning("[SubCommandPanelUi] 菜单项为空，无法处理输入");
            return;
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            selectedIndex = (selectedIndex - 1 + currentSubCommands.Count) % currentSubCommands.Count;
            UpdateSelectionUI();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            selectedIndex = (selectedIndex + 1) % currentSubCommands.Count;
            UpdateSelectionUI();
        }
        if (Input.GetKeyDown(KeyCode.Return))
        {
            ConfirmSelection();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ✅ 调用 CommandMenuUi 的方法返回主菜单
            if (commandMenuUi != null)
            {
                commandMenuUi.RequestCloseSubMenu();
            }
            else
            {
                Debug.LogWarning("[SubCommandPanelUi] CommandMenuUi 未配置，无法返回主菜单");
                HideSubCommands();
            }
        }
    }

    void UpdateSelectionUI()
    {
        for (int i = 0; i < itemInstances.Count; i++)
        {
            bool selected = (i == selectedIndex);
            itemInstances[i].transform.Find("Base")?.gameObject.SetActive(!selected);
            itemInstances[i].transform.Find("Select")?.gameObject.SetActive(selected);
        }

        // ✅ 自动滚动到选中项
        ScrollToSelected();
    }

    /// <summary>
    /// ✅ 瀑布流滚动：选中项始终在可见区域的顶部位置
    /// 支持 VerticalLayoutGroup 的间距配置
    /// 直接计算 Content 的位置，而不依赖 verticalNormalizedPosition
    /// </summary>
    void ScrollToSelected()
    {
        if (scrollRect == null || itemInstances.Count == 0 || selectedIndex < 0 || selectedIndex >= itemInstances.Count)
            return;

        Canvas.ForceUpdateCanvases();  // 确保 Layout 已更新
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        RectTransform selectedItem = itemInstances[selectedIndex].GetComponent<RectTransform>();
        RectTransform viewportRect = scrollRect.viewport;
        RectTransform contentRect = content;

        if (selectedItem == null || viewportRect == null || contentRect == null)
            return;

        // 📌 获取单个Item的高度
        float itemHeight = LayoutUtility.GetPreferredHeight(selectedItem);
        if (itemHeight <= 0f)
        {
            itemHeight = selectedItem.rect.height;
        }

        float viewportHeight = viewportRect.rect.height;

        // 如果 Item 高度还没计算好，直接返回
        if (itemHeight <= 0)
        {
            Debug.LogWarning("[SubCommandPanelUi] Item高度未计算完成，跳过滚动");
            return;
        }

        // 📌 获取 VerticalLayoutGroup 的间距
        float spacing = 0;
        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            spacing = vlg.spacing;
        }

        // 📌 单个Item加上间距的总高度
        float itemTotalHeight = itemHeight + spacing;

        // 📌 计算能显示的Items数量
        int visibleCount = Mathf.FloorToInt(viewportHeight / itemTotalHeight);
        if (visibleCount <= 0) visibleCount = 1;
        if (visibleCount > itemInstances.Count) visibleCount = itemInstances.Count;

        Vector3[] selectedCorners = new Vector3[4];
        selectedItem.GetWorldCorners(selectedCorners);

        float selectedTopInContent = contentRect.InverseTransformPoint(selectedCorners[1]).y;
        float targetScrollDistance = Mathf.Max(0f, -selectedTopInContent);

        float maxScrollDistance = Mathf.Max(0f, contentRect.rect.height - viewportHeight);
        targetScrollDistance = Mathf.Clamp(targetScrollDistance, 0f, maxScrollDistance);

        // 📌 Content Pivot 为顶部时，向下滚动使用正的 anchoredPosition.y
        contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, targetScrollDistance);

        int scrollableCount = Mathf.Max(0, itemInstances.Count - visibleCount);
        Debug.Log($"[SubCommandPanelUi] Item高度={itemHeight:F1}, spacing={spacing:F1}, Viewport={viewportHeight:F1}, 可见数={visibleCount}, 可滚动数={scrollableCount}, 选中={selectedIndex}, 目标滚动距离={targetScrollDistance:F1}, Content位置={contentRect.anchoredPosition}");
    }

    void UpdateArrowPosition()
    {
        if (arrow == null || itemInstances.Count == 0)
        {
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= itemInstances.Count)
        {
            return;
        }

        // ✅ 获取选中Item的 RectTransform
        RectTransform selectedItem = itemInstances[selectedIndex].GetComponent<RectTransform>();

        if (selectedItem == null)
        {
            Debug.LogWarning("[SubCommandPanelUi] 未找到选中项的 RectTransform");
            return;
        }

        // ✅ 直接使用Item的锚点位置（Item在Content中的本地位置）
        // 加上Content由于滚动的位移
        Vector3 itemPosInViewport;

        // 方法：获取Item世界坐标，再转换为箭头所在的坐标系
        Vector3 itemWorldPos = selectedItem.TransformPoint(new Vector3(-selectedItem.rect.width / 2, 0, 0));

        RectTransform arrowParent = arrow.parent as RectTransform;
        if (arrowParent == null)
        {
            Debug.LogWarning("[SubCommandPanelUi] 箭头parent为null");
            return;
        }

        // 转换为箭头父级的本地坐标
        itemPosInViewport = arrowParent.InverseTransformPoint(itemWorldPos);

        // 箭头放在Item左边，考虑箭头宽度
        float arrowWidth = arrow.rect.width;
        float arrowX = itemPosInViewport.x - arrowWidth / 2;
        float arrowY = itemPosInViewport.y;

        arrow.anchoredPosition = new Vector2(arrowX, arrowY);

        Debug.Log($"[SubCommandPanelUi] 箭头自动定位: Item_{selectedIndex} 世界坐标={itemWorldPos}, 本地坐标={itemPosInViewport}, 箭头位置=({arrowX}, {arrowY})");
    }

    void ConfirmSelection()
    {
        if (selectedIndex < 0 || selectedIndex >= currentSubCommands.Count) return;

        SubCommand chosen = currentSubCommands[selectedIndex];

        if (battleManager != null)
        {
            battleManager.OnCommandSelected(
                parentCommand,
                chosen.skill,
                chosen.item
            );
        }

        // ✅ 通过 CommandMenuUi 隐藏次级菜单
        if (commandMenuUi != null)
        {
            commandMenuUi.RequestCloseSubMenu();
        }
        else
        {
            HideSubCommands();
        }
    }
}