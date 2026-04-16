using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EquipmentCategory
{
    Sword,
    Lance,
    Dagger,
    Axe,
    Bow,
    Staff,
    Head,
    Body,
    Accessory
}

[DisallowMultipleComponent]
public class EquipmentBagUIController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private EquipPageUIController equipPageController;
    [SerializeField] private TextMeshProUGUI typeTitleText;
    [SerializeField] private TextMeshProUGUI bannerNameText;
    [SerializeField] private TextMeshProUGUI bannerCountText;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private GameObject itemPrototype;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private EquipmentCategory defaultCategory = EquipmentCategory.Sword;
    [SerializeField] private Color normalBackgroundColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color selectedBackgroundColor = new Color(0.88f, 0.94f, 1f, 0.35f);
    [SerializeField] private KeyCode upKey = KeyCode.W;
    [SerializeField] private KeyCode downKey = KeyCode.S;
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;

    private readonly List<EquipmentEntry> entries = new List<EquipmentEntry>();
    private readonly List<EquipmentBagItemView> itemViews = new List<EquipmentBagItemView>();
    private EquipmentCategory currentCategory;
    private int selectedIndex;
    private string currentMemberId;

    public EquipmentCategory CurrentCategory => currentCategory;

    private void Awake()
    {
        currentCategory = defaultCategory;
        if (itemPrototype != null)
            itemPrototype.SetActive(false);

        if (bannerNameText != null)
            bannerNameText.text = "名称";
        if (bannerCountText != null)
            bannerCountText.text = "持有数";

        if (scrollRect == null && contentRoot != null)
            scrollRect = contentRoot.GetComponentInParent<ScrollRect>();
    }

    private void OnEnable()
    {
        RefreshEntries();
    }

    private void Update()
    {
        if (equipPageController == null || !equipPageController.IsPageOpened)
            return;

        if (HasMemberChanged())
            RefreshEntries();

        if (entries.Count == 0)
            return;

        if (Input.GetKeyDown(upKey))
        {
            selectedIndex = WrapIndex(selectedIndex - 1, entries.Count);
            RefreshSelection();
        }
        else if (Input.GetKeyDown(downKey))
        {
            selectedIndex = WrapIndex(selectedIndex + 1, entries.Count);
            RefreshSelection();
        }
        else if (Input.GetKeyDown(confirmKey))
        {
            EquipSelectedItem();
        }
    }

    public void SetCategory(EquipmentCategory category)
    {
        if (currentCategory == category)
            return;

        currentCategory = category;
        RefreshEntries();
    }

    public void RefreshEntries()
    {
        if (typeTitleText != null)
            typeTitleText.text = GetCategoryLabel(currentCategory);

        entries.Clear();
        Inventory inventory = Inventory.Instance;
        BattleUnit runtimeUnit = equipPageController != null ? equipPageController.GetRuntimeUnitForSelectedMember() : null;

        if (inventory != null)
        {
            Dictionary<EquipmentData, int> counts = new Dictionary<EquipmentData, int>();
            foreach (EquipmentData equipment in inventory.GetAllEquipments())
            {
                if (equipment == null || !MatchesCategory(equipment, currentCategory))
                    continue;

                if (runtimeUnit != null && equipment is WeaponData weapon && !runtimeUnit.CanEquipWeapon(weapon))
                    continue;

                if (!counts.TryGetValue(equipment, out int count))
                    count = 0;
                counts[equipment] = count + 1;
            }

            foreach (var pair in counts)
            {
                EquipmentEntry entry = new EquipmentEntry(pair.Key, pair.Value);
                if (runtimeUnit != null)
                    entry.IsEquipped = IsEquipmentCurrentlyEquipped(pair.Key, runtimeUnit);
                entries.Add(entry);
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.EquipmentName, b.EquipmentName));
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, entries.Count - 1));
        currentMemberId = equipPageController != null && equipPageController.SelectedPartyMember != null ? equipPageController.SelectedPartyMember.characterId : string.Empty;
        RebuildItemViews();
        RefreshSelection();
    }

    private bool HasMemberChanged()
    {
        if (equipPageController == null)
            return false;

        string memberId = equipPageController.SelectedPartyMember != null ? equipPageController.SelectedPartyMember.characterId : string.Empty;
        if (memberId != currentMemberId)
        {
            currentMemberId = memberId;
            return true;
        }

        return false;
    }

    private void RebuildItemViews()
    {
        if (scrollRect != null)
        {
            scrollRect.enabled = false;
            scrollRect.content = contentRoot;
        }

        for (int i = itemViews.Count - 1; i >= 0; i--)
        {
            if (itemViews[i] != null)
                Destroy(itemViews[i].gameObject);
        }

        itemViews.Clear();

        if (itemPrototype == null || contentRoot == null)
        {
            if (scrollRect != null)
                scrollRect.enabled = true;
            return;
        }

        if (entries.Count == 0)
        {
            CreatePlaceholderItem("当前无可用装备");
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
            {
                GameObject clone = Instantiate(itemPrototype, contentRoot);
                clone.SetActive(true);

                EquipmentBagItemView view = clone.GetComponent<EquipmentBagItemView>();
                if (view == null)
                    view = clone.AddComponent<EquipmentBagItemView>();

                string displayName = entries[i].EquipmentName;
                int count = entries[i].Count;
                bool isEquipped = entries[i].IsEquipped;
                view.Initialize(i, OnItemClicked, normalBackgroundColor, selectedBackgroundColor);
                view.SetData(displayName, count, false, isEquipped);
                itemViews.Add(view);
            }
        }

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.enabled = true;
        }
    }

    private void CreatePlaceholderItem(string text)
    {
        if (itemPrototype == null || contentRoot == null)
            return;

        GameObject clone = Instantiate(itemPrototype, contentRoot);
        clone.SetActive(true);

        EquipmentBagItemView view = clone.GetComponent<EquipmentBagItemView>();
        if (view == null)
            view = clone.AddComponent<EquipmentBagItemView>();

        view.Initialize(-1, null, normalBackgroundColor, selectedBackgroundColor);
        view.SetEmpty(text);
        itemViews.Add(view);
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < itemViews.Count; i++)
        {
            bool selected = i == selectedIndex;
            itemViews[i].SetSelected(selected);
        }
    }

    private void OnItemClicked(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, entries.Count - 1));
        RefreshSelection();
        EquipSelectedItem();
    }

    private void EquipSelectedItem()
    {
        if (equipPageController == null || entries.Count == 0 || selectedIndex < 0 || selectedIndex >= entries.Count)
            return;

        EquipmentEntry entry = entries[selectedIndex];
        if (entry.Equipment == null)
            return;

        BattleUnit runtimeUnit = equipPageController.GetRuntimeUnitForSelectedMember();
        if (runtimeUnit == null)
        {
            Debug.LogWarning("[EquipmentBagUIController] 当前没有可装备的角色。");
            return;
        }

        Inventory inventory = Inventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[EquipmentBagUIController] 未找到 Inventory。");
            return;
        }

        bool success = false;
        if (entry.Equipment is WeaponData weapon)
        {
            success = inventory.TryEquipWeapon(runtimeUnit, weapon);
        }
        else if (entry.Equipment is ArmorData armor)
        {
            success = inventory.TryEquipArmor(runtimeUnit, armor);
        }
        else if (entry.Equipment is AccessoryData accessory)
        {
            int slot = runtimeUnit.equippedAccessories != null && runtimeUnit.equippedAccessories.Length > 0 && runtimeUnit.equippedAccessories[0] == null ? 0 : 1;
            success = inventory.TryEquipAccessory(runtimeUnit, accessory, slot);
        }

        if (!success)
        {
            Debug.LogWarning($"[EquipmentBagUIController] 装备失败：{entry.EquipmentName}");
            return;
        }

        equipPageController.SyncSelectedMemberFromRuntimeUnit();
        equipPageController.RefreshSelectedMemberUI();
        RefreshEntries();
    }

    private bool MatchesCategory(EquipmentData equipment, EquipmentCategory category)
    {
        if (equipment == null)
            return false;

        switch (category)
        {
            case EquipmentCategory.Sword:
                return equipment is WeaponData weapon && weapon.weaponType == WeaponType.Sword;
            case EquipmentCategory.Lance:
                return equipment is WeaponData weapon1 && weapon1.weaponType == WeaponType.Lance;
            case EquipmentCategory.Dagger:
                return equipment is WeaponData weapon2 && weapon2.weaponType == WeaponType.Dagger;
            case EquipmentCategory.Axe:
                return equipment is WeaponData weapon3 && weapon3.weaponType == WeaponType.Axe;
            case EquipmentCategory.Bow:
                return equipment is WeaponData weapon4 && weapon4.weaponType == WeaponType.Bow;
            case EquipmentCategory.Staff:
                return equipment is WeaponData weapon5 && weapon5.weaponType == WeaponType.Staff;
            case EquipmentCategory.Head:
                return equipment is ArmorData armor && armor.slot == ArmorSlot.Head;
            case EquipmentCategory.Body:
                return equipment is ArmorData body && body.slot == ArmorSlot.Body;
            case EquipmentCategory.Accessory:
                return equipment is AccessoryData;
            default:
                return false;
        }
    }

    private string GetCategoryLabel(EquipmentCategory category)
    {
        switch (category)
        {
            case EquipmentCategory.Sword: return "剑";
            case EquipmentCategory.Lance: return "枪";
            case EquipmentCategory.Dagger: return "短剑";
            case EquipmentCategory.Axe: return "斧头";
            case EquipmentCategory.Bow: return "弓";
            case EquipmentCategory.Staff: return "杖";
            case EquipmentCategory.Head: return "头部";
            case EquipmentCategory.Body: return "身体";
            case EquipmentCategory.Accessory: return "饰品";
            default: return "装备";
        }
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible)
            return;

        RefreshEntries();
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        int result = value % count;
        return result < 0 ? result + count : result;
    }

    [Serializable]
    private class EquipmentEntry
    {
        public EquipmentData Equipment { get; }
        public int Count { get; set; }
        public bool IsEquipped { get; set; }
        public string EquipmentName => Equipment != null ? (string.IsNullOrWhiteSpace(Equipment.equipmentName) ? Equipment.name : Equipment.equipmentName) : "";

        public EquipmentEntry(EquipmentData equipment, int count)
        {
            Equipment = equipment;
            Count = count;
        }
    }

    private bool IsEquipmentCurrentlyEquipped(EquipmentData equipment, BattleUnit runtimeUnit)
    {
        if (equipment == null || runtimeUnit == null)
            return false;

        if (equipment is WeaponData weapon)
            return runtimeUnit.equippedWeapon == weapon;

        if (equipment is ArmorData armor)
        {
            return (armor.slot == ArmorSlot.Head && runtimeUnit.equippedHead == armor)
                || (armor.slot == ArmorSlot.Body && runtimeUnit.equippedBody == armor);
        }

        if (equipment is AccessoryData accessory)
        {
            if (runtimeUnit.equippedAccessories == null)
                return false;
            foreach (var acc in runtimeUnit.equippedAccessories)
            {
                if (acc == accessory)
                    return true;
            }
        }

        return false;
    }
}
