using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SkillArtRowView : MonoBehaviour
{
    [SerializeField] private Image mainIcon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject learnedTag;
    [SerializeField] private GameObject selectedArrow;
    [SerializeField] private Image selectedBackground;
    [SerializeField] private Button button;

    private int rowIndex;
    private Action<int> clickCallback;

    public RectTransform RectTransform => transform as RectTransform;

    private void Awake()
    {
        AutoBind();
    }

    public void Initialize(int index, Action<int> onClick)
    {
        AutoBind();

        rowIndex = index;
        clickCallback = onClick;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }
    }

    public void SetData(string skillName, bool isLearned, Sprite icon = null)
    {
        if (nameText != null)
            nameText.text = skillName;

        if (learnedTag != null)
            learnedTag.SetActive(isLearned);

        if (mainIcon != null)
        {
            mainIcon.sprite = icon;
            mainIcon.enabled = icon != null;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedArrow != null)
            selectedArrow.SetActive(selected);

        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(selected);
    }

    private void OnClicked()
    {
        clickCallback?.Invoke(rowIndex);
    }

    private void AutoBind()
    {
        if (mainIcon == null)
            mainIcon = FindDeepChildByName(transform, "Main_Icon")?.GetComponent<Image>();

        if (nameText == null)
            nameText = FindDeepChildByName(transform, "Txt_Name")?.GetComponent<TextMeshProUGUI>();

        if (learnedTag == null)
            learnedTag = FindDeepChildByName(transform, "LearnedTag")?.gameObject;

        if (selectedArrow == null)
            selectedArrow = FindDeepChildByName(transform, "Arrow")?.gameObject;

        if (selectedBackground == null)
            selectedBackground = FindDeepChildByName(transform, "BG_Selected")?.GetComponent<Image>();

        if (button == null)
            button = GetComponent<Button>();
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
