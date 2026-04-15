using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 饰品特殊效果类型
/// 饰品是装备系统的"规则扩展点"，可突破普通属性加成的限制。
/// </summary>
public enum AccessoryEffectType
{
    None = 0,

    /// <summary>战斗开始时额外赋予指定 BP 值（value = BP 数量）</summary>
    StartBPBonus = 1,

    /// <summary>
    /// 破盾（Break）期间对该目标的伤害倍率额外提升
    /// 叠加于基础 ×2 之上：value = 额外倍率（如 0.5 → ×2.5）
    /// </summary>
    BreakDamageBonus = 2,

    /// <summary>
    /// 全局攻击/技能伤害倍率加成（乘算）
    /// value = 额外倍率（如 0.2 → 所有伤害 ×1.2）
    /// </summary>
    DamageMultiplierBonus = 3,

    /// <summary>
    /// 战斗开始时对自身施加指定 Buff（使用 buffType + buffDuration 字段）
    /// </summary>
    OpeningBuff = 4,

    /// <summary>
    /// 命中弱点时额外削减护盾次数
    /// value = 额外削盾次数（整数）
    /// </summary>
    ExtraShieldDamage = 5,
}

/// <summary>
/// 饰品特殊效果条目（可叠加多个不同类型的效果）
/// </summary>
[Serializable]
public class AccessorySpecialEffect
{
    [Tooltip("特殊效果类型")]
    public AccessoryEffectType effectType = AccessoryEffectType.None;

    [Tooltip("效果数值：StartBPBonus 填整数，DamageMultiplierBonus 填 0.2=+20%，ExtraShieldDamage 填次数")]
    public float value = 0f;

    [Header("OpeningBuff 专用")]
    [Tooltip("开局自动施加的增益/减益类型（仅 OpeningBuff 有效）")]
    public StatusEffectType buffType = StatusEffectType.None;

    [Min(1), Tooltip("Buff 持续回合数（仅 OpeningBuff 有效）")]
    public int buffDuration = 1;
}

/// <summary>
/// 饰品装备数据（每个角色可携带 2 枚）
///
/// 饰品设计特点：
///  - 可提供 HP / SP / P.Atk / E.Atk / 状态抗性等普通属性
///  - 也可通过 specialEffects 提供系统级效果（BP增加、破盾增强、开局Buff 等）
///  - 是装备系统中唯一的"规则破坏器"
/// </summary>
[CreateAssetMenu(fileName = "NewAccessory", menuName = "Equipment/Accessory")]
public class AccessoryData : EquipmentData
{
    [Header("特殊效果（可叠加多条）")]
    public List<AccessorySpecialEffect> specialEffects = new List<AccessorySpecialEffect>();

    // ──────────────────────────────────────────
    // 查询辅助方法
    // ──────────────────────────────────────────

    /// <summary>返回指定类型所有效果条目的数值总和（支持同一类型多次叠加）</summary>
    public float GetEffectValue(AccessoryEffectType type)
    {
        float total = 0f;
        foreach (var fx in specialEffects)
            if (fx != null && fx.effectType == type)
                total += fx.value;
        return total;
    }

    /// <summary>是否包含至少一个指定类型的特殊效果</summary>
    public bool HasEffect(AccessoryEffectType type)
    {
        foreach (var fx in specialEffects)
            if (fx != null && fx.effectType == type)
                return true;
        return false;
    }

    /// <summary>返回所有 OpeningBuff 效果条目列表（战斗初始化时遍历施加）</summary>
    public List<AccessorySpecialEffect> GetOpeningBuffEffects()
    {
        var results = new List<AccessorySpecialEffect>();
        foreach (var fx in specialEffects)
            if (fx != null && fx.effectType == AccessoryEffectType.OpeningBuff)
                results.Add(fx);
        return results;
    }
}
