using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ItemRowView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Image selectedBackground;
    [SerializeField] private Image selectedArrow;
    [SerializeField] private Button button;

    private int rowIndex;
    private Action<int> onClick;

    private void Awake()
    {
        AutoBind();
    }

    public void Initialize(int index, Action<int> clickHandler)
    {
        AutoBind();

        rowIndex = index;
        onClick = clickHandler;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }
    }

    public void SetData(string displayName, int count, Sprite icon)
    {
        if (nameText != null)
            nameText.text = displayName;

        if (countText != null)
            countText.text = count.ToString();

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(selected);

        if (selectedArrow != null)
            selectedArrow.gameObject.SetActive(selected);
    }

    public RectTransform RectTransform => transform as RectTransform;

    private void OnClicked()
    {
        onClick?.Invoke(rowIndex);
    }

    private void AutoBind()
    {
        if (iconImage == null)
            iconImage = FindDeepChild("Main_Icon")?.GetComponent<Image>();

        if (nameText == null)
            nameText = FindDeepChild("Txt_Name")?.GetComponent<TextMeshProUGUI>();

        if (countText == null)
            countText = FindDeepChild("Txt_Count")?.GetComponent<TextMeshProUGUI>();

        if (selectedBackground == null)
            selectedBackground = FindDeepChild("BG_Selected")?.GetComponent<Image>();

        if (selectedArrow == null)
            selectedArrow = FindDeepChild("Arrow")?.GetComponent<Image>();

        if (button == null)
            button = GetComponent<Button>();
    }

    private Transform FindDeepChild(string name)
    {
        return FindDeepChild(transform, name);
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null)
            return null;

        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
