using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(SkillPageLogicController))]
public class SkillPageUIController : MonoBehaviour
{
    [Header("核心引用")]
    [Tooltip("留空时会自动获取同节点上的 SkillPageLogicController")]
    [SerializeField] private SkillPageLogicController logicController;
    [SerializeField] private CanvasGroup pageCanvasGroup;

    [Header("左侧角色")]
    [SerializeField] private RectTransform selectArrow;
    [SerializeField] private List<Image> portraitSlots = new List<Image>(4);
    [SerializeField] private Color portraitActiveColor = Color.white;
    [SerializeField] private Color portraitInactiveColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("中间顶部文本")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI classNameText;
    [SerializeField] private TextMeshProUGUI currentJpText;
    [SerializeField] private TextMeshProUGUI nextCostJpText;

    [Header("战技列表")]
    [SerializeField] private RectTransform skillListArrow;
    [SerializeField] private Vector2 skillListArrowOffset = new Vector2(-28f, 0f);
    [SerializeField] private ScrollRect artsScrollRect;
    [SerializeField] private RectTransform artsContentRoot;
    [SerializeField] private GameObject skillRowPrefab;
    [SerializeField] private string emptyListText = "当前无可学习战技";

    private readonly List<SkillArtRowView> rowViews = new List<SkillArtRowView>();
    private int lastMemberIndex = -1;
    private int lastSkillIndex = -1;
    private SkillPageLogicController.SkillPageMode lastMode = SkillPageLogicController.SkillPageMode.CharacterSelect;

    private void Awake()
    {
        AutoBindReferences();
        EnsureArtsLayoutComponents();

        if (logicController == null)
            logicController = GetComponent<SkillPageLogicController>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (logicController == null)
            logicController = GetComponent<SkillPageLogicController>();
    }
#endif

    private void OnEnable()
    {
        RegisterEvents();
        RefreshAll();
    }

    private void Update()
    {
        if (logicController == null)
            return;

        if (lastMemberIndex != logicController.SelectedMemberIndex ||
            lastSkillIndex != logicController.SelectedSkillIndex ||
            lastMode != logicController.CurrentMode)
        {
            RefreshAll();
        }
        else
        {
            // Keep arrow positions stable even if layout updates after rebuild.
            RefreshModeArrows();
            RefreshPortraits();
        }
    }

    private void OnDisable()
    {
        UnregisterEvents();
    }

    private void RegisterEvents()
    {
        if (logicController == null)
            return;

        logicController.onSelectionChanged.RemoveListener(RefreshAll);
        logicController.onPageStateChanged.RemoveListener(RefreshAll);
        logicController.onSelectionChanged.AddListener(RefreshAll);
        logicController.onPageStateChanged.AddListener(RefreshAll);
    }

    private void UnregisterEvents()
    {
        if (logicController == null)
            return;

        logicController.onSelectionChanged.RemoveListener(RefreshAll);
        logicController.onPageStateChanged.RemoveListener(RefreshAll);
    }

    [ContextMenu("Auto Bind References")]
    private void AutoBindReferences()
    {
        if (logicController == null)
            logicController = GetComponent<SkillPageLogicController>();

        if (pageCanvasGroup == null)
        {
            pageCanvasGroup = GetComponent<CanvasGroup>();
            if (pageCanvasGroup == null)
                pageCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (selectArrow == null)
            selectArrow = FindDeepChildByName(transform, "SelectArrow") as RectTransform;
        if (selectArrow == null)
            selectArrow = FindDeepChildByName(transform, "SeclctArrow") as RectTransform;

        if (portraitSlots.Count == 0)
        {
            Transform listRoot = FindDeepChildByName(transform, "Left_CharacterList");
            if (listRoot != null)
            {
                for (int i = 1; i <= 4; i++)
                {
                    Transform t = listRoot.Find($"Portrait_0{i}");
                    if (t != null)
                    {
                        Image image = t.GetComponent<Image>();
                        if (image != null)
                            portraitSlots.Add(image);
                    }
                }

                if (portraitSlots.Count == 0)
                {
                    Image[] images = listRoot.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < images.Length; i++)
                    {
                        Image image = images[i];
                        if (image == null)
                            continue;

                        string lower = image.name.ToLowerInvariant();
                        if (lower.Contains("arrow") || lower.Contains("bg"))
                            continue;

                        if (image.transform == listRoot)
                            continue;

                        if (image.GetComponentInParent<Button>() != null || lower.Contains("portrait"))
                            portraitSlots.Add(image);
                    }
                }
            }
        }

        if (playerNameText == null)
            playerNameText = FindDeepChildByName(transform, "PlayerName")?.GetComponentInChildren<TextMeshProUGUI>(true);

        if (classNameText == null)
            classNameText = FindDeepChildByName(transform, "ClassName")?.GetComponentInChildren<TextMeshProUGUI>(true);

        if (currentJpText == null)
        {
            Transform jpPanel = FindDeepChildByName(transform, "Right_JpInfo");
            if (jpPanel != null)
                currentJpText = FindValueTextInPanel(jpPanel);
        }

        if (nextCostJpText == null)
        {
            Transform nextPanel = FindDeepChildByName(transform, "Right_NextSkillJpInfo");
            if (nextPanel != null)
                nextCostJpText = FindValueTextInPanel(nextPanel);
        }

        if (skillListArrow == null)
        {
            Transform artsRows = FindDeepChildByName(transform, "ArtsRows");
            if (artsRows == null)
                artsRows = FindDeepChildByName(transform, "SkillRows");
            if (artsRows != null)
                skillListArrow = FindDeepChildByName(artsRows, "SlotArrow") as RectTransform;
        }

        if (artsScrollRect == null)
        {
            Transform artsRows = FindDeepChildByName(transform, "ArtsRows");
            if (artsRows == null)
                artsRows = FindDeepChildByName(transform, "SkillRows");
            if (artsRows != null)
                artsScrollRect = FindDeepChildByName(artsRows, "Scroll View")?.GetComponent<ScrollRect>();
        }

        if (artsContentRoot == null && artsScrollRect != null)
            artsContentRoot = FindDeepChildByName(artsScrollRect.transform, "Content") as RectTransform;
    }

    public void RefreshAll()
    {
        RefreshPortraits();
        RefreshHeaderTexts();
        RebuildArtsRows();
        RefreshModeArrows();

        if (logicController != null)
        {
            lastMemberIndex = logicController.SelectedMemberIndex;
            lastSkillIndex = logicController.SelectedSkillIndex;
            lastMode = logicController.CurrentMode;
        }
    }

    private void RefreshPortraits()
    {
        if (logicController == null)
            return;

        IReadOnlyList<PartyMemberState> members = logicController.ActiveMembers;
        int selectedIndex = logicController.SelectedMemberIndex;

        for (int i = 0; i < portraitSlots.Count; i++)
        {
            Image slot = portraitSlots[i];
            if (slot == null)
                continue;

            if (i < members.Count)
            {
                PartyMemberState member = members[i];
                Sprite portrait = null;
                if (member != null && member.definition != null)
                {
                    portrait = member.definition.ResolvedPortrait;
                    if (portrait == null)
                        portrait = member.definition.BattleUnitTemplate != null ? member.definition.BattleUnitTemplate.tachie : null;
                }

                if (portrait != null)
                    slot.sprite = portrait;

                slot.enabled = true;
                slot.color = i == selectedIndex ? portraitActiveColor : portraitInactiveColor;
            }
            else
            {
                slot.sprite = null;
                slot.enabled = false;
                slot.color = portraitInactiveColor;
            }
        }

        if (selectArrow != null)
        {
            bool visible = logicController.CurrentMode == SkillPageLogicController.SkillPageMode.CharacterSelect
                           && selectedIndex >= 0
                           && selectedIndex < members.Count;

            selectArrow.gameObject.SetActive(visible);
            if (visible)
            {
                if (selectedIndex < portraitSlots.Count && portraitSlots[selectedIndex] != null)
                {
                    RectTransform target = portraitSlots[selectedIndex].rectTransform;
                    Vector3 worldCenter = target.TransformPoint(target.rect.center);
                    Vector3 pos = selectArrow.position;
                    pos.y = worldCenter.y;
                    selectArrow.position = pos;
                }
            }
        }
    }

    private void RefreshHeaderTexts()
    {
        PartyMemberState member = logicController != null ? logicController.SelectedMember : null;
        CharacterClassDefinition classDefinition = member != null ? member.GetClassDefinition() : null;

        SetText(playerNameText, ResolveMemberName(member, "-"));
        SetText(classNameText, classDefinition != null ? classDefinition.GetDisplayNameOrFallback() : "无职业");
        SetText(currentJpText, (member != null ? Mathf.Max(0, member.currentJP) : 0).ToString());
        SetText(nextCostJpText, (logicController != null ? logicController.NextLearningCostJP : 0).ToString());
    }

    private void RebuildArtsRows()
    {
        for (int i = rowViews.Count - 1; i >= 0; i--)
        {
            if (rowViews[i] != null)
                Destroy(rowViews[i].gameObject);
        }
        rowViews.Clear();

        if (artsContentRoot == null || skillRowPrefab == null)
            return;

        IReadOnlyList<Skill> skills = logicController != null ? logicController.CurrentSkillPool : null;
        PartyMemberState member = logicController != null ? logicController.SelectedMember : null;

        if (skills == null || skills.Count == 0)
        {
            GameObject clone = Instantiate(skillRowPrefab, artsContentRoot);
            clone.SetActive(true);
            EnsureRowLayoutElement(clone);

            SkillArtRowView view = clone.GetComponent<SkillArtRowView>();
            if (view == null)
                view = clone.AddComponent<SkillArtRowView>();

            view.Initialize(-1, null);
            view.SetData(emptyListText, false, null);
            view.SetSelected(false);
            rowViews.Add(view);

            if (artsScrollRect != null)
                artsScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        int selectedSkillIndex = logicController.SelectedSkillIndex;
        bool inSkillSelectMode = logicController.CurrentMode == SkillPageLogicController.SkillPageMode.SkillSelect;

        for (int i = 0; i < skills.Count; i++)
        {
            Skill skill = skills[i];
            bool learned = SkillLearningService.IsArtLearned(member, skill);

            GameObject clone = Instantiate(skillRowPrefab, artsContentRoot);
            clone.SetActive(true);
            EnsureRowLayoutElement(clone);

            SkillArtRowView view = clone.GetComponent<SkillArtRowView>();
            if (view == null)
                view = clone.AddComponent<SkillArtRowView>();

            view.Initialize(i, OnSkillRowClicked);
            view.SetData(skill != null ? skill.skillName : "-", learned, null);
            view.SetSelected(inSkillSelectMode && i == selectedSkillIndex);
            rowViews.Add(view);
        }

        if (artsScrollRect != null)
            artsScrollRect.verticalNormalizedPosition = 1f;
    }

    private void RefreshModeArrows()
    {
        if (logicController == null)
            return;

        bool inSkillSelectMode = logicController.CurrentMode == SkillPageLogicController.SkillPageMode.SkillSelect;
        if (skillListArrow != null)
        {
            bool valid = inSkillSelectMode && rowViews.Count > 0 && logicController.CurrentSkillPool.Count > 0;
            skillListArrow.gameObject.SetActive(valid);

            if (valid)
            {
                int index = Mathf.Clamp(logicController.SelectedSkillIndex, 0, rowViews.Count - 1);
                RectTransform rowRect = rowViews[index].RectTransform;
                if (rowRect != null)
                {
                    Vector3 worldPoint = rowRect.TransformPoint(new Vector3(rowRect.rect.xMin, rowRect.rect.center.y, 0f));
                    Vector3 pos = skillListArrow.position;
                    pos.x = worldPoint.x + skillListArrowOffset.x;
                    pos.y = worldPoint.y + skillListArrowOffset.y;
                    skillListArrow.position = pos;
                }
            }
        }
    }

    private void OnSkillRowClicked(int index)
    {
        if (logicController == null || index < 0 || index >= logicController.CurrentSkillPool.Count)
            return;

        // Align logic selected index to clicked row.
        int delta = index - logicController.SelectedSkillIndex;
        if (delta != 0)
            logicController.MoveSkillSelection(delta);

        if (logicController.CurrentMode == SkillPageLogicController.SkillPageMode.CharacterSelect)
        {
            logicController.EnterSkillSelectMode();
            return;
        }

        logicController.TryLearnSelectedSkill();
    }

    private void EnsureArtsLayoutComponents()
    {
        if (artsContentRoot == null)
            return;

        VerticalLayoutGroup layout = artsContentRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = artsContentRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = artsContentRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = artsContentRoot.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void EnsureRowLayoutElement(GameObject row)
    {
        if (row == null)
            return;

        RectTransform rowRect = row.transform as RectTransform;
        if (rowRect != null)
        {
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.offsetMin = new Vector2(0f, rowRect.offsetMin.y);
            rowRect.offsetMax = new Vector2(0f, rowRect.offsetMax.y);
            rowRect.anchoredPosition = new Vector2(0f, rowRect.anchoredPosition.y);
        }

        LayoutElement layoutElement = row.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = row.AddComponent<LayoutElement>();

        float preferredHeight = rowRect != null ? Mathf.Max(1f, rowRect.rect.height) : 30f;
        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleHeight = 0f;
    }

    private static string ResolveMemberName(PartyMemberState member, string fallback)
    {
        if (member == null)
            return fallback;

        if (!string.IsNullOrWhiteSpace(member.displayName))
            return member.displayName;

        if (member.definition != null && !string.IsNullOrWhiteSpace(member.definition.displayName))
            return member.definition.displayName;

        return fallback;
    }

    private static void SetText(TextMeshProUGUI textComp, string value)
    {
        if (textComp != null)
            textComp.text = value;
    }

    private static TextMeshProUGUI FindValueTextInPanel(Transform panel)
    {
        if (panel == null)
            return null;

        string[] preferred = { "Real_Label", "Value", "ValueText", "Text_Value", "Txt_Value", "JP_Value" };
        for (int i = 0; i < preferred.Length; i++)
        {
            Transform found = FindDeepChildByName(panel, preferred[i]);
            if (found != null)
            {
                TextMeshProUGUI text = found.GetComponent<TextMeshProUGUI>();
                if (text != null)
                    return text;
            }
        }

        TextMeshProUGUI[] all = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (all.Length == 0)
            return null;

        return all[all.Length - 1];
    }

    private static Transform FindDeepChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChildByName(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
