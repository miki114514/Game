using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ExplorationMainMenuController : MonoBehaviour
{
    [Serializable]
    public class IntEvent : UnityEvent<int>
    {
    }

    private enum MenuState
    {
        Closed,
        Selecting,
        Opened
    }

    [Header("菜单根节点")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private Transform leftMenuRoot;
    [SerializeField] private Transform baseButtonRoot;
    [SerializeField] private Transform selectButtonRoot;
    [SerializeField] private Transform otherButtonRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private RectTransform arrowRect;
    [SerializeField] private Vector2 arrowOffset = new Vector2(-36f, 0f);
    [SerializeField] private bool snapArrowImmediately = true;

    [Header("右侧内容面板（顺序对应装备/道具/技能）")]
    [SerializeField] private List<GameObject> contentPanels = new List<GameObject>();

    [Header("显示设置")]
    [SerializeField] private string defaultTitle = "菜单";
    [SerializeField] private bool hideMenuOnStart = true;
    [SerializeField] private bool resetSelectionOnOpen = true;

    [Header("输入")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
    [SerializeField] private KeyCode upKey = KeyCode.W;
    [SerializeField] private KeyCode downKey = KeyCode.S;
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;

    [Header("角色控制")]
    [SerializeField] private bool lockPlayerMovement = true;
    [SerializeField] private PlayerController playerController;

    [Header("事件")]
    public UnityEvent onMenuOpened;
    public UnityEvent onMenuClosed;
    public IntEvent onPageOpened;
    public UnityEvent onPageClosed;

    private readonly List<GameObject> baseButtons = new List<GameObject>();
    private readonly List<GameObject> selectButtons = new List<GameObject>();
    private readonly List<GameObject> otherButtons = new List<GameObject>();

    private MenuState menuState = MenuState.Closed;
    private int currentIndex;
    private int openedIndex = -1;
    private bool cachedPlayerMoveState;
    private bool hasCachedPlayerMoveState;

    public bool IsMenuOpen => menuState != MenuState.Closed;
    public bool IsPageOpened => menuState == MenuState.Opened;
    public int CurrentIndex => currentIndex;
    public int OpenedIndex => openedIndex;

    private void Awake()
    {
        ResolveReferences();
        CacheButtons();

        if (hideMenuOnStart)
        {
            menuState = MenuState.Closed;
            openedIndex = -1;
            ApplyMenuVisibility(false);
            SetContentPanelsActive(-1);
            UpdateTitle(defaultTitle);
        }
        else
        {
            menuState = MenuState.Selecting;
            ApplyMenuVisibility(true);
            RefreshVisualState();
        }
    }

    private void OnDisable()
    {
        RestorePlayerMovement();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            HandleEscape();
            return;
        }

        if (menuState != MenuState.Selecting)
            return;

        if (Input.GetKeyDown(upKey))
        {
            MoveSelection(-1);
        }
        else if (Input.GetKeyDown(downKey))
        {
            MoveSelection(1);
        }
        else if (IsConfirmPressed())
        {
            OpenCurrentPage();
        }

        UpdateArrowVisual();
    }

    public void ToggleMenu()
    {
        if (menuState == MenuState.Closed)
        {
            OpenMenu();
            return;
        }

        if (menuState == MenuState.Opened)
        {
            CloseCurrentPage();
            return;
        }

        CloseMenu();
    }

    public void OpenMenu()
    {
        if (ButtonCount == 0)
        {
            Debug.LogWarning("[ExplorationMainMenuController] 未找到任何菜单按钮，无法打开主菜单。");
            return;
        }

        if (resetSelectionOnOpen)
            currentIndex = 0;
        else
            currentIndex = Mathf.Clamp(currentIndex, 0, ButtonCount - 1);

        openedIndex = -1;
        menuState = MenuState.Selecting;
        CachePlayerMovementState();
        ApplyMenuVisibility(true);
        RefreshVisualState();
        onMenuOpened?.Invoke();
    }

    public void CloseMenu()
    {
        bool wasOpen = menuState != MenuState.Closed;

        menuState = MenuState.Closed;
        openedIndex = -1;
        ApplyMenuVisibility(false);
        SetContentPanelsActive(-1);
        UpdateTitle(defaultTitle);
        RestorePlayerMovement();

        if (wasOpen)
            onMenuClosed?.Invoke();
    }

    public void MoveSelection(int direction)
    {
        if (menuState != MenuState.Selecting || ButtonCount == 0)
            return;

        currentIndex = WrapIndex(currentIndex + direction, ButtonCount);
        RefreshVisualState();
    }

    public void SelectIndex(int index)
    {
        if (ButtonCount == 0)
            return;

        currentIndex = Mathf.Clamp(index, 0, ButtonCount - 1);
        if (menuState == MenuState.Selecting)
            RefreshVisualState();
    }

    public void OpenCurrentPage()
    {
        if (menuState != MenuState.Selecting || ButtonCount == 0)
            return;

        openedIndex = currentIndex;
        menuState = MenuState.Opened;
        RefreshVisualState();
        onPageOpened?.Invoke(openedIndex);
    }

    public void OpenPageByIndex(int index)
    {
        if (ButtonCount == 0)
            return;

        currentIndex = Mathf.Clamp(index, 0, ButtonCount - 1);

        if (menuState == MenuState.Closed)
            OpenMenu();

        OpenCurrentPage();
    }

    public void CloseCurrentPage()
    {
        if (menuState != MenuState.Opened)
            return;

        openedIndex = -1;
        menuState = MenuState.Selecting;
        RefreshVisualState();
        onPageClosed?.Invoke();
    }

    [ContextMenu("Auto Resolve References")]
    private void ResolveReferences()
    {
        if (menuRoot == null)
            menuRoot = gameObject;

        if (menuCanvasGroup == null && menuRoot != null)
        {
            menuCanvasGroup = menuRoot.GetComponent<CanvasGroup>();
            if (menuCanvasGroup == null)
                menuCanvasGroup = menuRoot.AddComponent<CanvasGroup>();
        }

        if (leftMenuRoot == null)
            leftMenuRoot = transform.Find("Panel_LeftMenu");

        if (baseButtonRoot == null && leftMenuRoot != null)
            baseButtonRoot = leftMenuRoot.Find("Base_Button");

        if (selectButtonRoot == null && leftMenuRoot != null)
            selectButtonRoot = leftMenuRoot.Find("Seclect_Button");

        if (otherButtonRoot == null && leftMenuRoot != null)
            otherButtonRoot = leftMenuRoot.Find("Other_Button");

        if (titleText == null && leftMenuRoot != null)
            titleText = leftMenuRoot.Find("Text_Title")?.GetComponent<TextMeshProUGUI>();

        if (arrowRect == null && leftMenuRoot != null)
            arrowRect = leftMenuRoot.Find("Btn_Arrow") as RectTransform;

        AutoResolveContentPanels();

        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
    }

    private void AutoResolveContentPanels()
    {
        if (contentPanels.Count > 0)
            return;

        Transform centerInfoRoot = transform.Find("Panel_CentetInfo");
        if (centerInfoRoot == null)
            centerInfoRoot = transform.Find("Panel_CenterInfo");

        if (centerInfoRoot == null)
            return;

        TryAddPanelByName(centerInfoRoot, "EquipPage");
        TryAddPanelByName(centerInfoRoot, "ItemPage");
        TryAddPanelByName(centerInfoRoot, "SkillPage");

        if (contentPanels.Count == 0)
        {
            for (int i = 0; i < centerInfoRoot.childCount; i++)
                contentPanels.Add(centerInfoRoot.GetChild(i).gameObject);
        }
    }

    private void TryAddPanelByName(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        if (child != null)
            contentPanels.Add(child.gameObject);
    }

    private void CacheButtons()
    {
        baseButtons.Clear();
        selectButtons.Clear();
        otherButtons.Clear();

        int count = Mathf.Max(GetChildCount(baseButtonRoot), GetChildCount(selectButtonRoot), GetChildCount(otherButtonRoot));
        for (int i = 0; i < count; i++)
        {
            baseButtons.Add(GetChildObject(baseButtonRoot, i));
            selectButtons.Add(GetChildObject(selectButtonRoot, i));
            otherButtons.Add(GetChildObject(otherButtonRoot, i));
        }
    }

    private void RefreshVisualState()
    {
        if (menuState == MenuState.Closed)
        {
            ApplyMenuVisibility(false);
            SetContentPanelsActive(-1);
            UpdateTitle(defaultTitle);
            SetArrowVisible(false);
            return;
        }

        ApplyMenuVisibility(true);

        if (menuState == MenuState.Selecting)
        {
            for (int i = 0; i < ButtonCount; i++)
            {
                bool isSelected = i == currentIndex;
                SetButtonActive(baseButtons, i, !isSelected);
                SetButtonActive(selectButtons, i, isSelected);
                SetButtonActive(otherButtons, i, false);
            }

            SetContentPanelsActive(-1);
            UpdateTitle(defaultTitle);
            SetArrowVisible(true);
            UpdateArrowPosition();
            return;
        }

        for (int i = 0; i < ButtonCount; i++)
        {
            SetButtonActive(baseButtons, i, false);
            SetButtonActive(selectButtons, i, false);
            SetButtonActive(otherButtons, i, true);
        }

        SetContentPanelsActive(openedIndex);
        UpdateTitle(GetPageTitle(openedIndex));
        SetArrowVisible(false);
    }

    private void HandleEscape()
    {
        if (menuState == MenuState.Closed)
        {
            OpenMenu();
            return;
        }

        if (menuState == MenuState.Opened)
        {
            CloseCurrentPage();
            return;
        }

        CloseMenu();
    }

    private void ApplyMenuVisibility(bool visible)
    {
        if (menuRoot == null)
            return;

        if (menuCanvasGroup == null)
        {
            menuCanvasGroup = menuRoot.GetComponent<CanvasGroup>();
            if (menuCanvasGroup == null)
                menuCanvasGroup = menuRoot.AddComponent<CanvasGroup>();
        }

        menuCanvasGroup.alpha = visible ? 1f : 0f;
        menuCanvasGroup.interactable = visible;
        menuCanvasGroup.blocksRaycasts = visible;
    }

    private void SetContentPanelsActive(int activeIndex)
    {
        for (int i = 0; i < contentPanels.Count; i++)
        {
            if (contentPanels[i] != null)
                contentPanels[i].SetActive(i == activeIndex);
        }
    }

    private void CachePlayerMovementState()
    {
        if (!lockPlayerMovement)
            return;

        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();

        if (playerController == null)
            return;

        cachedPlayerMoveState = playerController.canMove;
        hasCachedPlayerMoveState = true;
        playerController.canMove = false;
    }

    private void RestorePlayerMovement()
    {
        if (!lockPlayerMovement)
            return;

        if (!hasCachedPlayerMoveState || playerController == null)
            return;

        playerController.canMove = cachedPlayerMoveState;
        hasCachedPlayerMoveState = false;
    }

    private void UpdateTitle(string value)
    {
        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(value) ? defaultTitle : value;
    }

    private void UpdateArrowVisual()
    {
        if (menuState != MenuState.Selecting)
            return;

        UpdateArrowPosition();
    }

    private void UpdateArrowPosition()
    {
        if (arrowRect == null)
            return;

        RectTransform targetRect = GetArrowTarget(currentIndex);
        if (targetRect == null)
            return;

        Vector3 worldTarget = targetRect.TransformPoint(targetRect.rect.center) + (Vector3)arrowOffset;
        if (snapArrowImmediately)
            arrowRect.position = worldTarget;
        else
            arrowRect.position = Vector3.Lerp(arrowRect.position, worldTarget, Time.unscaledDeltaTime * 18f);
    }

    private RectTransform GetArrowTarget(int index)
    {
        GameObject target = GetButtonObject(selectButtons, index);
        if (target == null)
            target = GetButtonObject(baseButtons, index);

        return target != null ? target.transform as RectTransform : null;
    }

    private GameObject GetButtonObject(List<GameObject> source, int index)
    {
        if (index < 0 || index >= source.Count)
            return null;

        return source[index];
    }

    private void SetArrowVisible(bool visible)
    {
        if (arrowRect != null)
            arrowRect.gameObject.SetActive(visible);
    }

    private string GetPageTitle(int index)
    {
        string label = GetButtonLabel(baseButtons, index);
        if (string.IsNullOrWhiteSpace(label))
            label = GetButtonLabel(selectButtons, index);
        if (string.IsNullOrWhiteSpace(label))
            label = GetButtonLabel(otherButtons, index);

        return string.IsNullOrWhiteSpace(label) ? defaultTitle : label;
    }

    private string GetButtonLabel(List<GameObject> source, int index)
    {
        if (index < 0 || index >= source.Count)
            return null;

        GameObject buttonObject = source[index];
        if (buttonObject == null)
            return null;

        TextMeshProUGUI label = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null && !string.IsNullOrWhiteSpace(label.text))
            return label.text.Trim();

        string objectName = buttonObject.name;
        return objectName.Replace("Button_", string.Empty).Replace("_", string.Empty).Trim();
    }

    private bool IsConfirmPressed()
    {
        return Input.GetKeyDown(confirmKey) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private void SetButtonActive(List<GameObject> source, int index, bool active)
    {
        if (index < 0 || index >= source.Count)
            return;

        GameObject target = source[index];
        if (target != null)
            target.SetActive(active);
    }

    private static int GetChildCount(Transform root)
    {
        return root == null ? 0 : root.childCount;
    }

    private static GameObject GetChildObject(Transform root, int index)
    {
        if (root == null || index < 0 || index >= root.childCount)
            return null;

        return root.GetChild(index).gameObject;
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        int result = value % count;
        return result < 0 ? result + count : result;
    }

    private int ButtonCount => Mathf.Max(baseButtons.Count, selectButtons.Count, otherButtons.Count);
}