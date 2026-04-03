using System;

/// <summary>
/// 异常状态类型枚举
/// </summary>
public enum StatusEffectType
{
    Poison,   // 每回合扣 maxHP × 5%
    Burn,     // 每回合扣 maxHP × 3%
    Sleep,    // 无法行动
    Silence,  // 无法使用技能（Arts / Skill）
    Blind,    // 命中率 × 0.5
    Terror,   // 攻击力 × 0.7
    Confuse,  // 本回合随机行动（攻击己方随机目标）
    Freeze,   // 无法行动 + 受到伤害 × 1.5
    Shock,    // 行动时有 50% 概率行动失败
    Break     // 破防状态：受到伤害提升并跳过行动
}

/// <summary>
/// 异常状态实例，记录类型与剩余持续回合数
/// </summary>
[Serializable]
public class StatusEffect
{
    public StatusEffectType type;

    /// <summary>剩余持续回合；-1 表示永久（需主动解除）</summary>
    public int remainingRounds;

    public StatusEffect(StatusEffectType type, int rounds)
    {
        this.type            = type;
        this.remainingRounds = rounds;
    }

    /// <summary>
    /// 回合结束时递减计时，返回 true 表示状态已到期应被移除
    /// </summary>
    public bool Tick()
    {
        if (remainingRounds < 0) return false; // 永久状态，不自动消除
        remainingRounds--;
        return remainingRounds <= 0;
    }
}
