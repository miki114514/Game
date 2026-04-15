using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EquipPageUIController : MonoBehaviour
{
    [Header("可选：主菜单控制器（用于判断是否处于装备页）")]
    [SerializeField] private ExplorationMainMenuController mainMenuController;
    [SerializeField] private int equipPageIndex = 0;
    [SerializeField] private bool syncVisibilityFromMainMenu = true;
    [SerializeField] private CanvasGroup pageCanvasGroup;

    [Header("队伍数据来源")]
    [SerializeField] private bool useFallbackPartyWhenNoPartyManager = true;
    [SerializeField] private List<CharacterDefinition> fallbackPartyDefinitions = new List<CharacterDefinition>(4);

    [Header("左侧角色列表")]
    [SerializeField] private RectTransform selectArrow;
    [SerializeField] private List<Image> portraitSlots = new List<Image>(4);
    [SerializeField] private Color portraitActiveColor = Color.white;
    [SerializeField] private Color portraitInactiveColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private KeyCode upKey = KeyCode.W;
    [SerializeField] private KeyCode downKey = KeyCode.S;

    [Header("角色信息")]
    [SerializeField] private TextMeshProUGUI playerNameText;

    [Header("装备文本")]
    [SerializeField] private TextMeshProUGUI swordText;
    [SerializeField] private TextMeshProUGUI lanceText;
    [SerializeField] private TextMeshProUGUI daggerText;
    [SerializeField] private TextMeshProUGUI axeText;
    [SerializeField] private TextMeshProUGUI bowText;
    [SerializeField] private TextMeshProUGUI staffText;
    [SerializeField] private TextMeshProUGUI headText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI accessory1Text;
    [SerializeField] private TextMeshProUGUI accessory2Text;

    [Header("属性文本")]
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI pAtkText;
    [SerializeField] private TextMeshProUGUI pDefText;
    [SerializeField] private TextMeshProUGUI hitText;
    [SerializeField] private TextMeshProUGUI critText;
    [SerializeField] private TextMeshProUGUI spText;
    [SerializeField] private TextMeshProUGUI eAtkText;
    [SerializeField] private TextMeshProUGUI eDefText;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI evaText;

    [Header("显示文本")]
    [SerializeField] private string emptyText = "无装备";
    [SerializeField] private string unavailableText = "-";

    private readonly List<PartyMemberState> activeMembers = new List<PartyMemberState>();
    private int selectedIndex;

    private void Awake()
    {
        AutoBindReferences();
    }

    private void OnEnable()
    {
        RefreshPartyAndUI(true);
        SyncPageVisibility();
    }

    private void Update()
    {
        SyncPageVisibility();

        if (!IsEquipPageActive())
            return;

        if (activeMembers.Count <= 0)
            return;

        if (Input.GetKeyDown(upKey))
        {
            selectedIndex = WrapIndex(selectedIndex - 1, activeMembers.Count);
            RefreshCurrentMemberUI();
        }
        else if (Input.GetKeyDown(downKey))
        {
            selectedIndex = WrapIndex(selectedIndex + 1, activeMembers.Count);
            RefreshCurrentMemberUI();
        }
    }

    [ContextMenu("Auto Bind References")]
    private void AutoBindReferences()
    {
        if (mainMenuController == null)
            mainMenuController = FindObjectOfType<ExplorationMainMenuController>();

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
                    for (int i = 0; i < listRoot.childCount; i++)
                    {
                        Image image = listRoot.GetChild(i).GetComponent<Image>();
                        if (image != null)
                            portraitSlots.Add(image);

                        if (portraitSlots.Count >= 4)
                            break;
                    }
                }
            }
        }

        if (playerNameText == null)
            playerNameText = FindTextByPath("Right_DetailInfo/PlayerName/Text (TMP)");

        swordText = swordText != null ? swordText : FindRowValueText("Sword");
        lanceText = lanceText != null ? lanceText : FindRowValueText("Lance");
        daggerText = daggerText != null ? daggerText : FindRowValueText("Dagger");
        axeText = axeText != null ? axeText : FindRowValueText("Axe");
        bowText = bowText != null ? bowText : FindRowValueText("Bow");
        staffText = staffText != null ? staffText : FindRowValueText("Staff");
        headText = headText != null ? headText : FindRowValueText("Head");
        bodyText = bodyText != null ? bodyText : FindRowValueText("Body");
        accessory1Text = accessory1Text != null ? accessory1Text : FindRowValueText("Accessory1");
        accessory2Text = accessory2Text != null ? accessory2Text : FindRowValueText("Accessory2");

        hpText = hpText != null ? hpText : FindStatValueText("Stat_HP");
        pAtkText = pAtkText != null ? pAtkText : FindStatValueText("Stat_PAtk");
        pDefText = pDefText != null ? pDefText : FindStatValueText("Stat_PDef");
        hitText = hitText != null ? hitText : FindStatValueText("Stat_Hit");
        critText = critText != null ? critText : FindStatValueText("Stat_Crit");
        spText = spText != null ? spText : FindStatValueText("Stat_SP");
        eAtkText = eAtkText != null ? eAtkText : FindStatValueText("Stat_EAtk");
        eDefText = eDefText != null ? eDefText : FindStatValueText("Stat_EDef");
        speedText = speedText != null ? speedText : FindStatValueText("Stat_Speed");
        evaText = evaText != null ? evaText : FindStatValueText("Stat_Eva");
    }

    private void RefreshPartyAndUI(bool resetSelection)
    {
        activeMembers.Clear();

        PartyManager partyManager = PartyManager.Instance;
        if (partyManager != null)
            activeMembers.AddRange(partyManager.GetActivePartyMembers(4));

        if (activeMembers.Count == 0 && useFallbackPartyWhenNoPartyManager)
            BuildFallbackPartyMembers();

        if (resetSelection)
            selectedIndex = 0;

        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, activeMembers.Count - 1));
        RefreshCurrentMemberUI();
    }

    private void BuildFallbackPartyMembers()
    {
        for (int i = 0; i < fallbackPartyDefinitions.Count && i < 4; i++)
        {
            CharacterDefinition definition = fallbackPartyDefinitions[i];
            if (definition == null)
                continue;

            var member = new PartyMemberState
            {
                characterId = string.IsNullOrWhiteSpace(definition.characterId) ? definition.name : definition.characterId,
                displayName = string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName,
                definition = definition,
                isUnlocked = true,
                isInActiveParty = true,
                formationIndex = i
            };

            activeMembers.Add(member);
        }
    }

    private void RefreshCurrentMemberUI()
    {
        RefreshPortraitSlots();

        if (activeMembers.Count <= 0)
        {
            SetText(playerNameText, unavailableText);
            ClearEquipTexts();
            ClearStatTexts();
            if (selectArrow != null)
                selectArrow.gameObject.SetActive(false);
            return;
        }

        PartyMemberState state = activeMembers[selectedIndex];
        BattleUnit unit = state != null && state.definition != null ? state.definition.BattleUnitTemplate : null;

        if (selectArrow != null)
            selectArrow.gameObject.SetActive(true);

        UpdateArrowPosition();
        UpdateName(state);
        UpdateEquipTexts(unit);
        UpdateStatTexts(unit);
    }

    private void RefreshPortraitSlots()
    {
        for (int i = 0; i < portraitSlots.Count; i++)
        {
            Image slot = portraitSlots[i];
            if (slot == null)
                continue;

            if (i < activeMembers.Count)
            {
                Sprite portrait = activeMembers[i].definition != null ? activeMembers[i].definition.ResolvedPortrait : null;
                slot.sprite = portrait;
                slot.color = (i == selectedIndex) ? portraitActiveColor : portraitInactiveColor;
                slot.enabled = portrait != null;
            }
            else
            {
                slot.sprite = null;
                slot.color = portraitInactiveColor;
                slot.enabled = false;
            }
        }
    }

    private void UpdateArrowPosition()
    {
        if (selectArrow == null || selectedIndex < 0 || selectedIndex >= portraitSlots.Count)
            return;

        Image slot = portraitSlots[selectedIndex];
        if (slot == null)
            return;

        RectTransform target = slot.rectTransform;
        Vector3 worldCenter = target.TransformPoint(target.rect.center);
        Vector3 newPos = selectArrow.position;
        newPos.y = worldCenter.y;
        selectArrow.position = newPos;
    }

    private void UpdateName(PartyMemberState state)
    {
        string displayName = unavailableText;
        if (state != null)
        {
            if (!string.IsNullOrWhiteSpace(state.displayName))
                displayName = state.displayName;
            else if (state.definition != null && !string.IsNullOrWhiteSpace(state.definition.displayName))
                displayName = state.definition.displayName;
        }

        SetText(playerNameText, displayName);
    }

    private void UpdateEquipTexts(BattleUnit unit)
    {
        string sword = emptyText;
        string lance = emptyText;
        string dagger = emptyText;
        string axe = emptyText;
        string bow = emptyText;
        string staff = emptyText;

        if (unit != null && unit.equippedWeapon != null)
        {
            string weaponName = ResolveEquipmentName(unit.equippedWeapon);
            switch (unit.equippedWeapon.weaponType)
            {
                case WeaponType.Sword: sword = weaponName; break;
                case WeaponType.Lance: lance = weaponName; break;
                case WeaponType.Dagger: dagger = weaponName; break;
                case WeaponType.Axe: axe = weaponName; break;
                case WeaponType.Bow: bow = weaponName; break;
                case WeaponType.Staff: staff = weaponName; break;
            }
        }

        SetText(swordText, sword);
        SetText(lanceText, lance);
        SetText(daggerText, dagger);
        SetText(axeText, axe);
        SetText(bowText, bow);
        SetText(staffText, staff);

        SetText(headText, unit != null ? ResolveEquipmentName(unit.equippedHead) : unavailableText);
        SetText(bodyText, unit != null ? ResolveEquipmentName(unit.equippedBody) : unavailableText);

        AccessoryData acc1 = unit != null && unit.equippedAccessories != null && unit.equippedAccessories.Length > 0 ? unit.equippedAccessories[0] : null;
        AccessoryData acc2 = unit != null && unit.equippedAccessories != null && unit.equippedAccessories.Length > 1 ? unit.equippedAccessories[1] : null;
        SetText(accessory1Text, unit != null ? ResolveEquipmentName(acc1) : unavailableText);
        SetText(accessory2Text, unit != null ? ResolveEquipmentName(acc2) : unavailableText);
    }

    private void UpdateStatTexts(BattleUnit unit)
    {
        if (unit == null)
        {
            ClearStatTexts();
            return;
        }

        unit.RecalculateEquipmentBonuses();

        SetText(hpText, unit.EffectiveMaxHP.ToString());
        SetText(spText, unit.EffectiveMaxSP.ToString());

        int resolvedPAtk = unit.GetCombatAttackValue(DamageType.Physical, unit.GetResolvedNormalAttackWeaponType());
        int resolvedEAtk = unit.GetCombatAttackValue(DamageType.Elemental, WeaponType.None);
        int resolvedPDef = unit.GetCombatDefenseValue(DamageType.Physical);
        int resolvedEDef = unit.GetCombatDefenseValue(DamageType.Elemental);

        SetText(pAtkText, resolvedPAtk.ToString());
        SetText(eAtkText, resolvedEAtk.ToString());
        SetText(pDefText, resolvedPDef.ToString());
        SetText(eDefText, resolvedEDef.ToString());

        SetText(hitText, unit.FinalAccuracy.ToString());
        SetText(critText, unit.FinalCritRate.ToString());
        SetText(speedText, unit.FinalSpeed.ToString());
        SetText(evaText, unit.evasion.ToString());
    }

    private void ClearEquipTexts()
    {
        SetText(swordText, unavailableText);
        SetText(lanceText, unavailableText);
        SetText(daggerText, unavailableText);
        SetText(axeText, unavailableText);
        SetText(bowText, unavailableText);
        SetText(staffText, unavailableText);
        SetText(headText, unavailableText);
        SetText(bodyText, unavailableText);
        SetText(accessory1Text, unavailableText);
        SetText(accessory2Text, unavailableText);
    }

    private void ClearStatTexts()
    {
        SetText(hpText, unavailableText);
        SetText(spText, unavailableText);
        SetText(pAtkText, unavailableText);
        SetText(eAtkText, unavailableText);
        SetText(pDefText, unavailableText);
        SetText(eDefText, unavailableText);
        SetText(hitText, unavailableText);
        SetText(critText, unavailableText);
        SetText(speedText, unavailableText);
        SetText(evaText, unavailableText);
    }

    private bool IsEquipPageActive()
    {
        if (!gameObject.activeInHierarchy)
            return false;

        if (mainMenuController == null)
            return true;

        return mainMenuController.IsPageOpened && mainMenuController.OpenedIndex == equipPageIndex;
    }

    private void SyncPageVisibility()
    {
        if (!syncVisibilityFromMainMenu || pageCanvasGroup == null)
            return;

        bool visible = mainMenuController == null || (mainMenuController.IsPageOpened && mainMenuController.OpenedIndex == equipPageIndex);
        pageCanvasGroup.alpha = visible ? 1f : 0f;
        pageCanvasGroup.interactable = visible;
        pageCanvasGroup.blocksRaycasts = visible;
    }

    private string ResolveEquipmentName(EquipmentData equipment)
    {
        if (equipment == null)
            return emptyText;

        if (!string.IsNullOrWhiteSpace(equipment.equipmentName))
            return equipment.equipmentName;

        return equipment.name;
    }

    private TextMeshProUGUI FindRowValueText(string rowName)
    {
        Transform rows = FindDeepChildByName(transform, "EquipRows");
        if (rows == null)
            return null;

        Transform row = rows.Find(rowName);
        if (row == null)
            return null;

        Transform value = row.Find("Real_Label");
        if (value != null)
            return value.GetComponent<TextMeshProUGUI>();

        TextMeshProUGUI[] labels = row.GetComponentsInChildren<TextMeshProUGUI>(true);
        return labels.Length > 0 ? labels[labels.Length - 1] : null;
    }

    private TextMeshProUGUI FindStatValueText(string statNode)
    {
        Transform root = FindDeepChildByName(transform, statNode);
        if (root == null)
            return null;

        Transform value = root.Find("Real_Label");
        if (value != null)
            return value.GetComponent<TextMeshProUGUI>();

        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        return labels.Length > 0 ? labels[labels.Length - 1] : null;
    }

    private TextMeshProUGUI FindTextByPath(string path)
    {
        Transform t = transform.Find(path);
        if (t == null)
            return null;

        return t.GetComponent<TextMeshProUGUI>();
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

    private static void SetText(TextMeshProUGUI textComp, string value)
    {
        if (textComp != null)
            textComp.text = value;
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        int result = value % count;
        return result < 0 ? result + count : result;
    }
}