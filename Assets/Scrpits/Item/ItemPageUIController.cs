using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ItemPageUIController : MonoBehaviour
{
    private enum ItemPageMode
    {
        CategorySelect,
        ListSelect
    }

    private enum ItemPageCategory
    {
        AcquireOrder,
        Item,
        Weapon,
        Armor,
        Accessory
    }

    [Header("可选：主菜单控制器")]
    [SerializeField] private ExplorationMainMenuController mainMenuController;
    [SerializeField] private int itemPageIndex = 1;
    [SerializeField] private bool syncVisibilityFromMainMenu = true;
    [SerializeField] private CanvasGroup pageCanvasGroup;

    [Header("输入")]
    [SerializeField] private KeyCode upKey = KeyCode.W;
    [SerializeField] private KeyCode downKey = KeyCode.S;
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private KeyCode backKey = KeyCode.Escape;

    [Header("左侧分类")]
    [SerializeField] private RectTransform categoryArrow;
    [SerializeField] private Vector2 categoryArrowOffset = new Vector2(-28f, 0f);
    [SerializeField] private List<RectTransform> categoryRows = new List<RectTransform>();

    [Header("中间列表")]
    [SerializeField] private TextMeshProUGUI pageTitleText;
    [SerializeField] private RectTransform listArrow;
    [SerializeField] private Vector2 listArrowOffset = new Vector2(-28f, 0f);
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private GameObject itemRowPrefab;

    [Header("文本")]
    [SerializeField] private string noEntryText = "当前无可用道具";

    private readonly List<ItemRowEntry> entries = new List<ItemRowEntry>();
    private readonly List<ItemRowView> rowViews = new List<ItemRowView>();

    private ItemPageMode mode = ItemPageMode.CategorySelect;
    private int selectedCategoryIndex;
    private int selectedRowIndex;

    public bool IsPageOpened => mainMenuController != null && mainMenuController.IsPageOpened && mainMenuController.OpenedIndex == itemPageIndex;

    private void Awake()
    {
        AutoBindReferences();
        EnsureListLayoutComponents();

        // Keep scene templates hidden if one is accidentally assigned.
        if (itemRowPrefab != null && itemRowPrefab.scene.IsValid())
            itemRowPrefab.SetActive(false);
    }

    private void OnEnable()
    {
        EnsureListLayoutComponents();
        BuildCategoryRowsIfNeeded();
        selectedCategoryIndex = Mathf.Clamp(selectedCategoryIndex, 0, Mathf.Max(0, categoryRows.Count - 1));
        selectedRowIndex = 0;
        mode = ItemPageMode.CategorySelect;

        RefreshCategoryVisual();
        RefreshEntries();
        SyncPageVisibility();
    }

    private void Update()
    {
        SyncPageVisibility();

        if (!IsItemPageActive())
            return;

        if (Input.GetKeyDown(backKey))
        {
            if (mode == ItemPageMode.ListSelect)
            {
                mode = ItemPageMode.CategorySelect;
                RefreshCategoryVisual();
                return;
            }
        }

        if (mode == ItemPageMode.CategorySelect)
        {
            if (Input.GetKeyDown(upKey))
            {
                selectedCategoryIndex = WrapIndex(selectedCategoryIndex - 1, categoryRows.Count);
                selectedRowIndex = 0;
                RefreshCategoryVisual();
                RefreshEntries();
            }
            else if (Input.GetKeyDown(downKey))
            {
                selectedCategoryIndex = WrapIndex(selectedCategoryIndex + 1, categoryRows.Count);
                selectedRowIndex = 0;
                RefreshCategoryVisual();
                RefreshEntries();
            }
            else if (Input.GetKeyDown(confirmKey) && entries.Count > 0)
            {
                mode = ItemPageMode.ListSelect;
                RefreshCategoryVisual();
                RefreshSelectionVisual();
            }
        }
        else
        {
            if (entries.Count == 0)
                return;

            if (Input.GetKeyDown(upKey))
            {
                selectedRowIndex = WrapIndex(selectedRowIndex - 1, entries.Count);
                RefreshSelectionVisual();
            }
            else if (Input.GetKeyDown(downKey))
            {
                selectedRowIndex = WrapIndex(selectedRowIndex + 1, entries.Count);
                RefreshSelectionVisual();
            }
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

        if (categoryRows.Count == 0)
            BuildCategoryRowsIfNeeded();

        if (pageTitleText == null)
        {
            Transform itemNameRoot = FindDeepChildByName(transform, "ItemName");
            if (itemNameRoot != null)
                pageTitleText = itemNameRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (pageTitleText == null)
            pageTitleText = FindDeepChildByName(transform, "Text (TMP)")?.GetComponent<TextMeshProUGUI>();

        if (scrollRect == null)
            scrollRect = FindDeepChildByName(transform, "Scroll View")?.GetComponent<ScrollRect>();

        if (contentRoot == null)
            contentRoot = FindDeepChildByName(transform, "Content") as RectTransform;

        if (listArrow == null)
            listArrow = FindDeepChildByName(transform, "SlotArrow") as RectTransform;

        // Intentionally do not auto-bind from Content children.
        // This field is meant to reference a prefab asset in Project view.

        if (categoryArrow == null)
            categoryArrow = FindDeepChildByName(transform, "SelectArrow") as RectTransform;
    }

    private void BuildCategoryRowsIfNeeded()
    {
        if (categoryRows.Count > 0)
            return;

        Transform leftList = FindDeepChildByName(transform, "Left_List");
        if (leftList == null)
            return;

        foreach (Transform child in leftList)
        {
            if (child == null)
                continue;

            string lower = child.name.ToLowerInvariant();
            if (lower.Contains("selectarrow"))
                continue;

            if (child is RectTransform rect)
                categoryRows.Add(rect);
        }

        // Keep hierarchy order so icon-only category rows still map correctly.
    }

    private void RefreshEntries()
    {
        entries.Clear();
        UpdateTitle();

        Inventory inventory = Inventory.Instance;
        if (inventory == null)
        {
            RebuildRows();
            RefreshSelectionVisual();
            return;
        }

        ItemPageCategory category = GetCategoryByIndex(selectedCategoryIndex);
        switch (category)
        {
            case ItemPageCategory.AcquireOrder:
                BuildAcquireOrderEntries(inventory);
                break;
            case ItemPageCategory.Item:
                BuildItemEntries(inventory);
                break;
            case ItemPageCategory.Weapon:
                BuildEquipmentEntries(inventory, EquipmentFilterType.Weapon);
                break;
            case ItemPageCategory.Armor:
                BuildEquipmentEntries(inventory, EquipmentFilterType.Armor);
                break;
            case ItemPageCategory.Accessory:
                BuildEquipmentEntries(inventory, EquipmentFilterType.Accessory);
                break;
        }

        selectedRowIndex = Mathf.Clamp(selectedRowIndex, 0, Mathf.Max(0, entries.Count - 1));

        RebuildRows();
        RefreshSelectionVisual();
    }

    private void BuildAcquireOrderEntries(Inventory inventory)
    {
        if (inventory.items != null)
        {
            foreach (InventoryItem inv in inventory.items)
            {
                if (inv == null || inv.item == null || inv.amount <= 0)
                    continue;

                entries.Add(new ItemRowEntry(inv.item.itemName, inv.amount, inv.item.icon));
            }
        }

        if (inventory.equipments != null)
        {
            Dictionary<EquipmentData, int> countByEquipment = new Dictionary<EquipmentData, int>();
            List<EquipmentData> order = new List<EquipmentData>();

            foreach (EquipmentData eq in inventory.equipments)
            {
                if (eq == null)
                    continue;

                if (!countByEquipment.ContainsKey(eq))
                {
                    countByEquipment[eq] = 0;
                    order.Add(eq);
                }

                countByEquipment[eq]++;
            }

            foreach (EquipmentData eq in order)
            {
                int amount = countByEquipment[eq];
                entries.Add(new ItemRowEntry(ResolveEquipmentName(eq), amount, eq.icon));
            }
        }
    }

    private void BuildItemEntries(Inventory inventory)
    {
        if (inventory.items == null)
            return;

        foreach (InventoryItem inv in inventory.items)
        {
            if (inv == null || inv.item == null || inv.amount <= 0)
                continue;

            entries.Add(new ItemRowEntry(inv.item.itemName, inv.amount, inv.item.icon));
        }
    }

    private void BuildEquipmentEntries(Inventory inventory, EquipmentFilterType type)
    {
        if (inventory.equipments == null)
            return;

        Dictionary<EquipmentData, int> counts = new Dictionary<EquipmentData, int>();
        foreach (EquipmentData eq in inventory.equipments)
        {
            if (eq == null || !IsMatch(eq, type))
                continue;

            if (!counts.TryGetValue(eq, out int count))
                count = 0;

            counts[eq] = count + 1;
        }

        foreach (var pair in counts)
            entries.Add(new ItemRowEntry(ResolveEquipmentName(pair.Key), pair.Value, pair.Key.icon));

        entries.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
    }

    private void RebuildRows()
    {
        for (int i = rowViews.Count - 1; i >= 0; i--)
        {
            if (rowViews[i] != null)
                Destroy(rowViews[i].gameObject);
        }
        rowViews.Clear();

        if (itemRowPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("[ItemPageUIController] Item Row Prefab 未绑定，请在 Inspector 中拖入预制体资产。", this);
            return;
        }

        if (entries.Count == 0)
        {
            GameObject clone = Instantiate(itemRowPrefab, contentRoot);
            clone.SetActive(true);
            EnsureRowLayoutElement(clone);

            ItemRowView row = clone.GetComponent<ItemRowView>();
            if (row == null)
                row = clone.AddComponent<ItemRowView>();

            row.Initialize(-1, null);
            row.SetData(noEntryText, 0, null);
            row.SetSelected(false);
            rowViews.Add(row);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            GameObject clone = Instantiate(itemRowPrefab, contentRoot);
            clone.SetActive(true);
            EnsureRowLayoutElement(clone);

            ItemRowView row = clone.GetComponent<ItemRowView>();
            if (row == null)
                row = clone.AddComponent<ItemRowView>();

            ItemRowEntry entry = entries[i];
            row.Initialize(i, OnRowClicked);
            row.SetData(entry.displayName, entry.amount, entry.icon);
            row.SetSelected(false);
            rowViews.Add(row);
        }

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void EnsureListLayoutComponents()
    {
        if (contentRoot == null)
            return;

        VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnsureRowLayoutElement(GameObject row)
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

    private void OnRowClicked(int index)
    {
        if (index < 0 || index >= entries.Count)
            return;

        selectedRowIndex = index;
        mode = ItemPageMode.ListSelect;
        RefreshCategoryVisual();
        RefreshSelectionVisual();
    }

    private void RefreshSelectionVisual()
    {
        for (int i = 0; i < rowViews.Count; i++)
        {
            bool selected = mode == ItemPageMode.ListSelect && i == selectedRowIndex && entries.Count > 0;
            rowViews[i].SetSelected(selected);
        }

        UpdateListArrowPosition();
    }

    private void RefreshCategoryVisual()
    {
        UpdateCategoryArrowPosition();

        if (listArrow != null)
            listArrow.gameObject.SetActive(mode == ItemPageMode.ListSelect && entries.Count > 0);
    }

    private void UpdateCategoryArrowPosition()
    {
        if (categoryArrow == null || categoryRows.Count == 0)
            return;

        RectTransform target = categoryRows[Mathf.Clamp(selectedCategoryIndex, 0, categoryRows.Count - 1)];
        if (target == null)
            return;

        Vector3 worldCenter = target.TransformPoint(target.rect.center);
        Vector3 pos = categoryArrow.position;
        pos.x = worldCenter.x + categoryArrowOffset.x;
        pos.y = worldCenter.y + categoryArrowOffset.y;
        categoryArrow.position = pos;
        categoryArrow.gameObject.SetActive(mode == ItemPageMode.CategorySelect);
    }

    private void UpdateListArrowPosition()
    {
        if (listArrow == null || mode != ItemPageMode.ListSelect || selectedRowIndex < 0 || selectedRowIndex >= rowViews.Count)
            return;

        RectTransform rowRect = rowViews[selectedRowIndex].RectTransform;
        if (rowRect == null)
            return;

        Vector3 worldCenter = rowRect.TransformPoint(rowRect.rect.center);
        Vector3 pos = listArrow.position;
        pos.x = worldCenter.x + listArrowOffset.x;
        pos.y = worldCenter.y + listArrowOffset.y;
        listArrow.position = pos;
        listArrow.gameObject.SetActive(true);
    }

    private void UpdateTitle()
    {
        if (pageTitleText == null)
            return;

        pageTitleText.text = GetCategoryTitle(GetCategoryByIndex(selectedCategoryIndex));
    }

    private ItemPageCategory GetCategoryByIndex(int index)
    {
        return (ItemPageCategory)Mathf.Clamp(index, 0, 4);
    }

    private int GetCategoryIndexFromName(string rowName)
    {
        if (string.IsNullOrWhiteSpace(rowName))
            return 0;

        string lower = rowName.ToLowerInvariant();
        if (lower.Contains("order") || lower.Contains("sort") || lower.Contains("!") || lower.Contains("get"))
            return 0;
        if (lower.Contains("item") || lower.Contains("potion") || lower.Contains("道具"))
            return 1;
        if (lower.Contains("weapon") || lower.Contains("sword") || lower.Contains("枪") || lower.Contains("剑"))
            return 2;
        if (lower.Contains("armor") || lower.Contains("head") || lower.Contains("body") || lower.Contains("防"))
            return 3;
        if (lower.Contains("accessory") || lower.Contains("ring") || lower.Contains("饰"))
            return 4;
        return 0;
    }

    private string GetCategoryTitle(ItemPageCategory category)
    {
        switch (category)
        {
            case ItemPageCategory.AcquireOrder: return "获取顺序";
            case ItemPageCategory.Item: return "道具";
            case ItemPageCategory.Weapon: return "武器";
            case ItemPageCategory.Armor: return "防具";
            case ItemPageCategory.Accessory: return "饰品";
            default: return "道具";
        }
    }

    private static bool IsMatch(EquipmentData equipment, EquipmentFilterType type)
    {
        switch (type)
        {
            case EquipmentFilterType.Weapon: return equipment is WeaponData;
            case EquipmentFilterType.Armor: return equipment is ArmorData;
            case EquipmentFilterType.Accessory: return equipment is AccessoryData;
            default: return false;
        }
    }

    private static string ResolveEquipmentName(EquipmentData equipment)
    {
        if (equipment == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(equipment.equipmentName) ? equipment.name : equipment.equipmentName;
    }

    private bool IsItemPageActive()
    {
        if (!gameObject.activeInHierarchy)
            return false;

        if (mainMenuController == null)
            return true;

        return mainMenuController.IsPageOpened && mainMenuController.OpenedIndex == itemPageIndex;
    }

    private void SyncPageVisibility()
    {
        if (!syncVisibilityFromMainMenu)
            return;

        bool visible = mainMenuController == null || (mainMenuController.IsPageOpened && mainMenuController.OpenedIndex == itemPageIndex);

        if (pageCanvasGroup != null)
        {
            pageCanvasGroup.alpha = visible ? 1f : 0f;
            pageCanvasGroup.interactable = visible;
            pageCanvasGroup.blocksRaycasts = visible;
        }
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

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        int result = value % count;
        return result < 0 ? result + count : result;
    }

    private enum EquipmentFilterType
    {
        Weapon,
        Armor,
        Accessory
    }

    private struct ItemRowEntry
    {
        public readonly string displayName;
        public readonly int amount;
        public readonly Sprite icon;

        public ItemRowEntry(string displayName, int amount, Sprite icon)
        {
            this.displayName = displayName;
            this.amount = amount;
            this.icon = icon;
        }
    }
}
