using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EquipmentBagItemView : MonoBehaviour
{
    [Header("UI 组件")]
    [SerializeField] private Image arrowImage;
    [SerializeField] private Image equippedMarkerImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Button button;

    private int itemIndex;
    private Action<int> clickCallback;
    private Color normalColor;
    private Color selectedColor;

    private void Awake()
    {
        AutoBindUIComponents();
    }

    public void Initialize(int index, Action<int> clickCallback, Color normalColor, Color selectedColor)
    {
        AutoBindUIComponents();

        this.itemIndex = index;
        this.clickCallback = clickCallback;
        this.normalColor = normalColor;
        this.selectedColor = selectedColor;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked());
        }
    }

    private void AutoBindUIComponents()
    {
        if (arrowImage == null)
            arrowImage = FindChildImageByNames("Arrow", "ArrowIcon", "SelectIcon");

        if (equippedMarkerImage == null)
            equippedMarkerImage = FindChildImageByNames("KeyTag", "EquipTag", "EquippedMarker", "EIcon");

        if (backgroundImage == null)
            backgroundImage = FindChildImageByNames("BG_Normal", "Background", "BG");

        if (nameText == null)
            nameText = FindChildTextByNames("Text_Name", "NameText", "TextLabel");

        if (countText == null)
            countText = FindChildTextByNames("Text_Count", "CountText", "AmountText");

        if (button == null)
            button = GetComponent<Button>();
    }

    private Image FindChildImageByNames(params string[] names)
    {
        foreach (var name in names)
        {
            Transform child = FindDeepChild(transform, name);
            if (child != null)
            {
                Image image = child.GetComponent<Image>();
                if (image != null)
                    return image;
            }
        }
        return null;
    }

    private TextMeshProUGUI FindChildTextByNames(params string[] names)
    {
        foreach (var name in names)
        {
            Transform child = FindDeepChild(transform, name);
            if (child != null)
            {
                TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
                if (text != null)
                    return text;
            }
        }
        return null;
    }

    private Transform FindDeepChild(Transform root, string name)
    {
        if (root == null)
            return null;

        foreach (Transform child in root)
        {
            if (child.name == name)
                return child;

            Transform found = FindDeepChild(child, name);
            if (found != null)
                return found;
        }

        return null;
    }

    public void SetData(string itemName, int itemCount, bool selected, bool equipped = false)
    {
        if (nameText != null)
            nameText.text = itemName;

        if (countText != null)
            countText.text = itemCount > 0 ? itemCount.ToString() : "0";

        SetSelected(selected);
        SetEquippedMarker(equipped);
    }

    public void SetEmpty(string label)
    {
        if (nameText != null)
            nameText.text = label;

        if (countText != null)
            countText.text = string.Empty;

        if (arrowImage != null)
            arrowImage.gameObject.SetActive(false);

        SetEquippedMarker(false);

        if (backgroundImage != null)
            backgroundImage.color = normalColor;

        if (button != null)
            button.interactable = false;
    }

    public void SetSelected(bool selected)
    {
        if (arrowImage != null)
            arrowImage.gameObject.SetActive(selected);

        if (backgroundImage != null)
            backgroundImage.color = selected ? selectedColor : normalColor;
    }

    public void SetEquippedMarker(bool equipped)
    {
        if (equippedMarkerImage != null)
            equippedMarkerImage.gameObject.SetActive(equipped);
    }

    private void OnClicked()
    {
        clickCallback?.Invoke(itemIndex);
    }
}
