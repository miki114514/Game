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

    void Start()
    {
        if (unit == null)
        {
            Debug.LogError("PlayerStatus 未绑定 BattleUnit！");
            return;
        }

        // 初始化UI
        UpdateHP(unit.currentHP, unit.maxHP);
        UpdateSP(unit.currentSP, unit.maxSP);

        // 监听事件
        unit.OnHPChanged += UpdateHP;
        unit.OnSPChanged += UpdateSP;
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
        this.unit = unit;

        if (unit == null)
        {
            Debug.LogError("PlayerStatus 未绑定 BattleUnit！");
            return;
        }

        // 初始化UI
        UpdateHP(unit.currentHP, unit.maxHP);
        UpdateSP(unit.currentSP, unit.maxSP);

        // 监听事件
        unit.OnHPChanged += UpdateHP;
        unit.OnSPChanged += UpdateSP;
    }
}