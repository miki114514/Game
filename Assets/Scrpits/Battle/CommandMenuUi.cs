using UnityEngine;
using System.Collections.Generic;
using BattleSystem;
using PlayerCommand;
using TMPro;

public class CommandMenuUi : MonoBehaviour
{
    [Header("UI元素")]
    public List<GameObject> baseList;
    public List<GameObject> selectList;
    public RectTransform arrow;

    [Header("箭头偏移")]
    public Vector2 arrowOffset = new Vector2(-50f, 0f);
    public Vector2 subMenuArrowOffset = new Vector2(200f, 0f);

    [Header("命令列表")]
    public List<BattleCommand> commandList;
    public BattleManager battleManager;
    public CanvasGroup canvasGroup;

    [Header("次级菜单")]
    public SubCommandPanelUi subCommandPanelUi;
    public ConfirmSubCommandPanelUi confirmSubCommandPanelUi;

    [Header("次级菜单位置")]
    public Vector2 subMenuOverlapOffset = new Vector2(72f, -8f);
    public float subMenuSafeMargin = 16f;

    private int currentIndex = 0;
    private bool inSubMenu = false;

    private int mainIndex = 0;
    private List<BattleCommand> mainCommandList;
    private List<SubCommand> subCommands;
    private int subIndex = 0;

    void Start()
    {
        mainCommandList = new List<BattleCommand>(commandList);

        // ✅ 自动查找 SubCommandPanelUi（如果未在Inspector中配置）
        if (subCommandPanelUi == null)
        {
            subCommandPanelUi = GetComponentInParent<Canvas>()
                ?.transform.Find("SubCommandPanel")
                ?.GetComponent<SubCommandPanelUi>();

            if (subCommandPanelUi == null)
            {
                subCommandPanelUi = FindObjectOfType<SubCommandPanelUi>();
            }

            if (subCommandPanelUi == null)
            {
                Debug.LogWarning("[CommandMenuUi] 无法自动查找到SubCommandPanelUi，请在Inspector中手动配置");
            }
            else
            {
                Debug.Log("[CommandMenuUi] ✅ 自动找到SubCommandPanelUi");
            }
        }

        if (confirmSubCommandPanelUi == null)
        {
            confirmSubCommandPanelUi = FindObjectOfType<ConfirmSubCommandPanelUi>();
            if (confirmSubCommandPanelUi != null)
                Debug.Log("[CommandMenuUi] ✅ 自动找到ConfirmSubCommandPanelUi");
            else
                Debug.LogWarning("[CommandMenuUi] 无法自动查找到ConfirmSubCommandPanelUi，请在Inspector中手动配置");
        }

        UpdateUI();
    }

    void Update()
    {
        if (!inSubMenu)
        {
            HandleMainMenuInput();
            UpdateUI();
            UpdateArrowPosition();
        }
        else
        {
            // 子菜单激活期间持续跟随主菜单位置
            PositionSubMenuNearMain();
        }
    }

    #region 主菜单输入
    void HandleMainMenuInput()
    {
        if (Input.GetKeyDown(KeyCode.W))
            currentIndex = (currentIndex - 1 + commandList.Count) % commandList.Count;

        if (Input.GetKeyDown(KeyCode.S))
            currentIndex = (currentIndex + 1) % commandList.Count;

        if (Input.GetKeyDown(KeyCode.Return))
            ConfirmMainSelection();
    }

    void ConfirmMainSelection()
    {
        BattleCommand selectedCommand = commandList[currentIndex];

        if (selectedCommand == BattleCommand.Skill ||
            selectedCommand == BattleCommand.Arts ||
            selectedCommand == BattleCommand.Item ||
            selectedCommand == BattleCommand.Defend ||
            selectedCommand == BattleCommand.Run)
        {
            OpenSubMenu(selectedCommand); // ✅ 使用统一方法
        }
        else
        {
            battleManager.OnCommandSelected(selectedCommand);
        }
    }
    #endregion

    #region 次级菜单输入
    void HandleSubMenuInput()
    {
        if (Input.GetKeyDown(KeyCode.W))
            subIndex = (subIndex - 1 + subCommands.Count) % subCommands.Count;

        if (Input.GetKeyDown(KeyCode.S))
            subIndex = (subIndex + 1) % subCommands.Count;

        UpdateSubMenuUI();

        if (Input.GetKeyDown(KeyCode.Return))
            ConfirmSubSelection();

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseSubMenu();
    }

    void ConfirmSubSelection()
    {
        SubCommand selected = subCommands[subIndex];

        if (selected.isConfirm && selected.name == "取消")
        {
            CloseSubMenu();
            return;
        }

        battleManager.OnCommandSelected(
            mainCommandList[mainIndex],
            selected.skill,
            selected.item
        );

        CloseSubMenu();
    }
    #endregion

    #region 次级菜单逻辑
    void OpenSubMenu(BattleCommand cmd)
    {
        inSubMenu = true;
        mainIndex = currentIndex;

        if (canvasGroup != null)
            canvasGroup.alpha = 0.5f;

        if (cmd == BattleCommand.Defend || cmd == BattleCommand.Run)
        {
            if (confirmSubCommandPanelUi != null)
            {
                confirmSubCommandPanelUi.battleManager = battleManager;
                confirmSubCommandPanelUi.commandMenuUi = this;
                confirmSubCommandPanelUi.isActive = true;
                confirmSubCommandPanelUi.ShowConfirmPanel(cmd);
                PositionSubMenuNearMain();
                Debug.Log($"[CommandMenuUi] 打开确认面板: {cmd}");
            }
            else
            {
                Debug.LogError("[CommandMenuUi] ConfirmSubCommandPanelUi 未配置");
            }
        }
        else
        {
            subCommands = GenerateSubCommands(cmd);
            subIndex = 0;

            if (subCommandPanelUi != null)
            {
                subCommandPanelUi.battleManager = battleManager;
                subCommandPanelUi.commandMenuUi = this;
                subCommandPanelUi.isActive = true;
                subCommandPanelUi.ShowSubCommands(subCommands, cmd);
                PositionSubMenuNearMain();
                Debug.Log($"[CommandMenuUi] 打开次级菜单: {cmd}");
            }
            else
            {
                Debug.LogError("[CommandMenuUi] SubCommandPanelUi 未配置");
            }
        }
    }

    void CloseSubMenu()
    {
        inSubMenu = false;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (subCommandPanelUi != null)
        {
            subCommandPanelUi.isActive = false;
            subCommandPanelUi.HideSubCommands();
        }

        if (confirmSubCommandPanelUi != null)
        {
            confirmSubCommandPanelUi.isActive = false;
            confirmSubCommandPanelUi.HideConfirmPanel();
        }

        Debug.Log("[CommandMenuUi] 关闭次级菜单");
        currentIndex = mainIndex;
        UpdateUI();
    }

    /// <summary>
    /// ✅ 公开方法：供 SubCommandPanelUi 在按下Escape时调用
    /// </summary>
    public void RequestCloseSubMenu()
    {
        CloseSubMenu();
    }

    List<SubCommand> GenerateSubCommands(BattleCommand cmd)
    {
        List<SubCommand> list = new List<SubCommand>();
        BattleUnit unit = battleManager.CurrentUnit;

        switch (cmd)
        {
            case BattleCommand.Arts:
                foreach (var sk in unit.artsList)
                    list.Add(new SubCommand(sk.skillName, sk));
                break;
            case BattleCommand.Skill:
                foreach (var sk in unit.skillList)
                    list.Add(new SubCommand(sk.skillName, sk));
                break;
            case BattleCommand.Item:
                foreach (var item in battleManager.GetUsableItems())
                    list.Add(new SubCommand(item.itemName, null, item));
                break;
            case BattleCommand.Defend:
            case BattleCommand.Run:
                list.Add(new SubCommand("确认", null, null, true));
                list.Add(new SubCommand("取消", null, null, true));
                break;
        }

        return list;
    }
    #endregion

    void PositionSubMenuNearMain()
    {
        RectTransform subRect = null;
        if (subCommandPanelUi != null && subCommandPanelUi.gameObject.activeSelf)
            subRect = subCommandPanelUi.transform as RectTransform;
        else if (confirmSubCommandPanelUi != null && confirmSubCommandPanelUi.gameObject.activeSelf)
            subRect = confirmSubCommandPanelUi.transform as RectTransform;

        if (subRect == null) return;

        RectTransform mainRect = transform as RectTransform;
        RectTransform canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();

        if (mainRect == null || subRect == null || canvasRect == null)
            return;

        Vector2 mainPos = mainRect.anchoredPosition;
        float canvasHalfWidth = canvasRect.rect.width * 0.5f;
        float canvasHalfHeight = canvasRect.rect.height * 0.5f;
        float subHalfWidth = subRect.rect.width * 0.5f;
        float subHalfHeight = subRect.rect.height * 0.5f;

        float minX = -canvasHalfWidth + subHalfWidth + subMenuSafeMargin;
        float maxX = canvasHalfWidth - subHalfWidth - subMenuSafeMargin;
        float minY = -canvasHalfHeight + subHalfHeight + subMenuSafeMargin;
        float maxY = canvasHalfHeight - subHalfHeight - subMenuSafeMargin;

        bool preferRight = mainPos.x <= 0f;
        float offsetX = preferRight ? subMenuOverlapOffset.x : -subMenuOverlapOffset.x;
        float candidateX = mainPos.x + offsetX;
        float candidateY = mainPos.y + subMenuOverlapOffset.y;

        bool outOnPreferredSide = (candidateX < minX) || (candidateX > maxX);
        if (outOnPreferredSide)
        {
            float oppositeOffsetX = -offsetX;
            float oppositeCandidateX = mainPos.x + oppositeOffsetX;
            if (oppositeCandidateX >= minX && oppositeCandidateX <= maxX)
            {
                candidateX = oppositeCandidateX;
            }
        }

        float targetX = Mathf.Clamp(candidateX, minX, maxX);
        float targetY = Mathf.Clamp(candidateY, minY, maxY);
        subRect.anchoredPosition = new Vector2(targetX, targetY);
    }

    #region UI刷新
    void UpdateUI()
    {
        for (int i = 0; i < baseList.Count; i++)
        {
            bool isSelected = (i == currentIndex);
            bool isActive = i < commandList.Count;

            baseList[i].SetActive(isActive && !isSelected);
            selectList[i].SetActive(isActive && isSelected);

            if (i < commandList.Count)
            {
                var textComp = baseList[i].GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null)
                {
                    textComp.text = commandList[i].ToString();
                    textComp.alpha = 1f; // 确保文字可见
                }

                var selText = selectList[i].GetComponentInChildren<TextMeshProUGUI>();
                if (selText != null)
                    selText.text = commandList[i].ToString();
            }
        }
    }

    void UpdateSubMenuUI()
    {
        for (int i = 0; i < baseList.Count; i++)
        {
            bool isSelected = (i == subIndex);
            bool isActive = i < subCommands.Count;

            baseList[i].SetActive(isActive && !isSelected);
            selectList[i].SetActive(isActive && isSelected);

            if (i < subCommands.Count)
            {
                var textComp = baseList[i].GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null)
                {
                    textComp.text = subCommands[i].name;
                    textComp.alpha = 1f;
                }

                var selText = selectList[i].GetComponentInChildren<TextMeshProUGUI>();
                if (selText != null)
                    selText.text = subCommands[i].name;
            }
        }
    }
    #endregion

    void UpdateArrowPosition()
    {
        if (!inSubMenu && currentIndex < selectList.Count)
        {
            Vector3 targetPos = selectList[currentIndex].transform.position + (Vector3)arrowOffset;
            arrow.position = Vector3.Lerp(arrow.position, targetPos, Time.deltaTime * 10f);
        }
    }
}