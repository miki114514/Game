using UnityEngine;
using BattleSystem;
using PlayerCommand;

/// <summary>
/// 确认/取消次级面板脚本
/// 挂载在 ConfirmSubCommandPanel 节点上
/// 供防御（Defend）和逃跑（Run）指令使用
/// UI结构: ConfirmSubCommandPanel > Arrow, CommandFrame, Option_Confirm(Base/Select/Text), Option_Cancel(Base/Select/Text)
/// </summary>
public class ConfirmSubCommandPanelUi : MonoBehaviour
{
    [Header("固定槽位")]
    public GameObject optionConfirm;    // Option_Confirm 节点
    public GameObject optionCancel;     // Option_Cancel 节点
    public RectTransform arrow;         // Arrow 节点

    [Header("箭头偏移")]
    public Vector2 arrowOffset = new Vector2(-50f, 0f);

    [Header("引用")]
    public BattleManager battleManager;
    public CommandMenuUi commandMenuUi;

    private int selectedIndex = 0;      // 0 = 确认, 1 = 取消
    public bool isActive = false;
    private BattleCommand parentCommand;

    // -------------------------------------------------------
    // 公开接口
    // -------------------------------------------------------

    /// <summary>
    /// 显示确认面板，绑定父指令（Defend 或 Run）
    /// </summary>
    public void ShowConfirmPanel(BattleCommand cmd)
    {
        parentCommand = cmd;
        selectedIndex = 0;
        isActive = true;
        gameObject.SetActive(true);

        Canvas.ForceUpdateCanvases();
        UpdateSelectionUI();
        UpdateArrowPosition();

        Debug.Log($"[ConfirmSubCommandPanelUi] 显示确认面板: {cmd}");
    }

    /// <summary>
    /// 隐藏确认面板
    /// </summary>
    public void HideConfirmPanel()
    {
        isActive = false;
        gameObject.SetActive(false);
        Debug.Log("[ConfirmSubCommandPanelUi] 隐藏确认面板");
    }

    // -------------------------------------------------------
    // 每帧逻辑
    // -------------------------------------------------------

    void Update()
    {
        if (!isActive) return;
        HandleInput();
        UpdateArrowPosition();
    }

    // -------------------------------------------------------
    // 输入处理
    // -------------------------------------------------------

    void HandleInput()
    {
        // W / S 在两个选项间切换
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S))
        {
            selectedIndex = 1 - selectedIndex;
            UpdateSelectionUI();
        }

        if (Input.GetKeyDown(KeyCode.Return))
            ConfirmSelection();

        if (Input.GetKeyDown(KeyCode.Escape))
            ClosePanel();
    }

    void ConfirmSelection()
    {
        if (selectedIndex == 1)
        {
            // 选择"取消"：仅关闭面板，不执行命令
            ClosePanel();
            return;
        }

        // 选择"确认"：执行父指令后关闭面板
        if (battleManager != null)
            battleManager.OnCommandSelected(parentCommand);

        ClosePanel();
    }

    void ClosePanel()
    {
        if (commandMenuUi != null)
            commandMenuUi.RequestCloseSubMenu();
        else
            HideConfirmPanel();
    }

    // -------------------------------------------------------
    // UI 刷新
    // -------------------------------------------------------

    void UpdateSelectionUI()
    {
        if (optionConfirm == null || optionCancel == null)
        {
            Debug.LogWarning("[ConfirmSubCommandPanelUi] optionConfirm 或 optionCancel 未配置");
            return;
        }

        bool confirmSelected = (selectedIndex == 0);

        optionConfirm.transform.Find("Base")?.gameObject.SetActive(!confirmSelected);
        optionConfirm.transform.Find("Select")?.gameObject.SetActive(confirmSelected);
        optionCancel.transform.Find("Base")?.gameObject.SetActive(confirmSelected);
        optionCancel.transform.Find("Select")?.gameObject.SetActive(!confirmSelected);

        Canvas.ForceUpdateCanvases();
        UpdateArrowPosition();

        Debug.Log($"[ConfirmSubCommandPanelUi] 选中: {(confirmSelected ? "确认" : "取消")}");
    }

    RectTransform GetSelectedOptionRect()
    {
        GameObject selectedOption = (selectedIndex == 0) ? optionConfirm : optionCancel;
        if (selectedOption == null)
            return null;

        RectTransform selectRect = selectedOption.transform.Find("Select") as RectTransform;
        if (selectRect != null && selectRect.rect.size.sqrMagnitude > 0f)
            return selectRect;

        RectTransform baseRect = selectedOption.transform.Find("Base") as RectTransform;
        if (baseRect != null && baseRect.rect.size.sqrMagnitude > 0f)
            return baseRect;

        return selectedOption.GetComponent<RectTransform>();
    }

    void UpdateArrowPosition()
    {
        if (arrow == null)
            return;

        RectTransform selectedRect = GetSelectedOptionRect();
        RectTransform arrowParent = arrow.parent as RectTransform;
        if (selectedRect == null || arrowParent == null)
            return;

        Vector3 targetWorldCenter = selectedRect.TransformPoint(selectedRect.rect.center);
        Vector2 targetLocalCenter = arrowParent.InverseTransformPoint(targetWorldCenter);

        arrow.anchoredPosition = new Vector2(
            targetLocalCenter.x + arrowOffset.x,
            targetLocalCenter.y + arrowOffset.y);
    }
}
