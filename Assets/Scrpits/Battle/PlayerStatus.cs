using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStatus : MonoBehaviour
{
    [Header("UI组件")]
    public TextMeshProUGUI playerNameText;
    public Text legacyPlayerNameText;
    public TextMeshProUGUI hpText;
    public Image hpFill;

    public TextMeshProUGUI spText;
    public Image spFill;

    [Header("BP显示（可选）")]
    public TextMeshProUGUI bpText;
    public bool updateBpTextValue = false;
    public string bpTextPrefix = "BP";
    public List<GameObject> bpPips = new List<GameObject>();
    public List<Image> bpPipImages = new List<Image>();
    public Sprite bpEmptySprite;
    public Sprite bpFilledSprite;
    public Color bpEmptyColor = new Color(1f, 1f, 1f, 0.28f);
    public Color bpFilledColor = Color.white;

    [Header("绑定单位")]
    public BattleUnit unit;

    void Awake()
    {
        AutoAssignOptionalReferences();
    }

    void OnEnable()
    {
        AutoAssignOptionalReferences();

        if (unit != null)
            Bind(unit);
    }

    void OnDisable()
    {
        Unbind();
    }

    // =========================
    // 名字更新
    // =========================
    void UpdateName(string displayName)
    {
        string safeName = string.IsNullOrWhiteSpace(displayName) ? "-" : displayName;

        if (playerNameText != null)
            playerNameText.text = safeName;

        if (legacyPlayerNameText != null)
            legacyPlayerNameText.text = safeName;
    }

    // =========================
    // HP更新
    // =========================
    void UpdateHP(int current, int max)
    {
        // 1️⃣ 更新文字（sprite数字）
        hpText.text = FormatNumber(current) + " / " + FormatNumber(max);

        // 2️⃣ 更新血条（0~1）
        hpFill.fillAmount = (float)current / max;
    }

    // =========================
    // SP更新
    // =========================
    void UpdateSP(int current, int max)
    {
        spText.text = FormatNumber(current) + " / " + FormatNumber(max);
        spFill.fillAmount = (float)current / max;
    }

    // =========================
    // BP更新
    // =========================
    void UpdateBP(int current, int max)
    {
        if (bpText != null)
        {
            if (updateBpTextValue)
                bpText.text = string.IsNullOrEmpty(bpTextPrefix)
                    ? FormatNumber(current)
                    : $"{bpTextPrefix} {FormatNumber(current)}";
            else if (!string.IsNullOrEmpty(bpTextPrefix))
                bpText.text = bpTextPrefix;
        }

        for (int i = 0; i < bpPips.Count; i++)
        {
            if (bpPips[i] != null)
                bpPips[i].SetActive(i < Mathf.Max(1, max));
        }

        for (int i = 0; i < bpPipImages.Count; i++)
        {
            Image pipImage = bpPipImages[i];
            if (pipImage == null)
                continue;

            bool isFilled = i < current;
            pipImage.enabled = i < Mathf.Max(1, max);

            if (isFilled && bpFilledSprite != null)
                pipImage.sprite = bpFilledSprite;
            else if (!isFilled && bpEmptySprite != null)
                pipImage.sprite = bpEmptySprite;

            pipImage.color = isFilled ? bpFilledColor : bpEmptyColor;
        }
    }

    // =========================
    // 数字转sprite格式
    // =========================
    string FormatNumber(int value)
    {
        string str = value.ToString();
        string result = "";

        foreach (char c in str)
        {
            // <sprite=数字>
            result += $"<sprite={c}>";
        }

        return result;
    }

    public void Init(BattleUnit unit)
    {
        if (this.unit != unit)
            Unbind();

        this.unit = unit;
        Bind(unit);
    }

    void AutoAssignOptionalReferences()
    {
        Transform nameNode = transform.Find("PlayerName");
        if (nameNode == null)
            return;

        if (playerNameText == null)
            playerNameText = nameNode.GetComponent<TextMeshProUGUI>();

        if (legacyPlayerNameText == null)
            legacyPlayerNameText = nameNode.GetComponent<Text>();
    }

    void Bind(BattleUnit targetUnit)
    {
        if (targetUnit == null)
        {
            Debug.LogError("PlayerStatus 未绑定 BattleUnit！");
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(targetUnit.unitName)
            ? targetUnit.gameObject.name
            : targetUnit.unitName;

        UpdateName(displayName);
        UpdateHP(targetUnit.currentHP, targetUnit.maxHP);
        UpdateSP(targetUnit.currentSP, targetUnit.maxSP);
        UpdateBP(targetUnit.CurrentBP, targetUnit.MaxBP);

        targetUnit.OnHPChanged -= UpdateHP;
        targetUnit.OnHPChanged += UpdateHP;
        targetUnit.OnSPChanged -= UpdateSP;
        targetUnit.OnSPChanged += UpdateSP;
        targetUnit.OnBPChanged -= UpdateBP;
        targetUnit.OnBPChanged += UpdateBP;
    }

    void Unbind()
    {
        if (unit == null)
            return;

        unit.OnHPChanged -= UpdateHP;
        unit.OnSPChanged -= UpdateSP;
        unit.OnBPChanged -= UpdateBP;
    }
}