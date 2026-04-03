using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStatus : MonoBehaviour
{
    [Header("UI组件")]
    public TextMeshProUGUI hpText;
    public Image hpFill;

    public TextMeshProUGUI spText;
    public Image spFill;

    [Header("绑定单位")]
    public BattleUnit unit;

    void OnEnable()
    {
        if (unit != null)
            Bind(unit);
    }

    void OnDisable()
    {
        Unbind();
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

    void Bind(BattleUnit targetUnit)
    {
        if (targetUnit == null)
        {
            Debug.LogError("PlayerStatus 未绑定 BattleUnit！");
            return;
        }

        UpdateHP(targetUnit.currentHP, targetUnit.maxHP);
        UpdateSP(targetUnit.currentSP, targetUnit.maxSP);

        targetUnit.OnHPChanged -= UpdateHP;
        targetUnit.OnHPChanged += UpdateHP;
        targetUnit.OnSPChanged -= UpdateSP;
        targetUnit.OnSPChanged += UpdateSP;
    }

    void Unbind()
    {
        if (unit == null)
            return;

        unit.OnHPChanged -= UpdateHP;
        unit.OnSPChanged -= UpdateSP;
    }
}