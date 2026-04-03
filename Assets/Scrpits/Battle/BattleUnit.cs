using UnityEngine;
using System;
using System.Collections.Generic;

public enum UnitType { Player, Enemy }
public enum AttackType { None, Sword, Fire, Ice, Thunder, Strike, Pierce }

public class BattleUnit : MonoBehaviour
{
    [Header("基础属性")]
    public string unitName;
    public UnitType unitType;

    [Header("头像")]
    public Sprite portrait;   // 队列图标用头像（菱形裁切）

    public int level = 1;

    public int maxHP = 100;
    public int currentHP;
    public int maxSP = 50;
    public int currentSP;

    public int physicalAttack = 20;
    public int magicAttack = 15;
    public int physicalDefense = 10;
    public int magicDefense = 8;

    public int accuracy = 95;    // 命中率 %
    public int speed = 10;       // 行动速度
    public int critRate = 10;    // 暴击 %
    public int evasion = 5;      // 回避 %

    [Header("攻击类型")]
    public AttackType normalAttackType = AttackType.Sword;

    [Header("玩家成长")]
    public int currentExp = 0;
    public int expToNextLevel = 100;

    [Header("敌人护盾")]
    public int maxShield = 0;
    public int currentShield = 0;
    public List<AttackType> weaknessTypes = new List<AttackType>();

    [Header("技能管理")]
    public List<Skill> artsList = new List<Skill>();   // 战技列表（Arts）
    public List<Skill> skillList = new List<Skill>();  // 角色技能列表（Skill）

    public event Action<int, int> OnHPChanged;
    public event Action<int, int> OnSPChanged;
    public event Action<int, int> OnEXPChanged;
    public event Action<int, int> OnShieldChanged;

    private int breakSkipTurnCount = 0;

    void Awake()
    {
        InitializeBattleState();
    }

    public void InitializeBattleState()
    {
        currentHP = maxHP;
        currentSP = maxSP;
        ResetShield();
        isBreak = false;
        breakSkipTurnCount = 0;
    }

    // =========================
    // 受伤
    // =========================
    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        currentHP = Mathf.Max(currentHP, 0);

        Debug.Log($"{unitName} 受到伤害: {damage}");

        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // =========================
    // 消耗SP
    // =========================
    public void UseSP(int cost)
    {
        currentSP -= cost;
        currentSP = Mathf.Max(currentSP, 0);

        OnSPChanged?.Invoke(currentSP, maxSP);
    }

    // =========================
    // 升级
    // =========================
    public void LevelUp()
    {
        level++;
        maxHP += 20;        // 简单示例
        maxSP += 10;
        physicalAttack += 5;
        magicAttack += 5;
        physicalDefense += 3;
        magicDefense += 3;

        // 升级时全回复
        currentHP = maxHP;
        currentSP = maxSP;

        OnHPChanged?.Invoke(currentHP, maxHP);
        OnSPChanged?.Invoke(currentSP, maxSP);
        OnEXPChanged?.Invoke(currentExp, expToNextLevel);

        Debug.Log($"{unitName} 升级到 {level} 级！");
    }

    public void AddExperience(int amount)
    {
        if (unitType != UnitType.Player || amount <= 0)
            return;

        currentExp += amount;
        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
            expToNextLevel = CalculateNextLevelExp(level);
        }

        OnEXPChanged?.Invoke(currentExp, expToNextLevel);
    }

    int CalculateNextLevelExp(int targetLevel)
    {
        return 100 + (targetLevel - 1) * 30;
    }
    
    // =========================
    // 治疗
    // =========================
    public void Heal(int amount)
    {
        currentHP += amount;
        currentHP = Mathf.Min(currentHP, maxHP);

        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // =========================
    // 攻击命中判定
    // =========================
    public bool CheckHit(BattleUnit target)
    {
        // Blind 状态：命中率 × 0.5
        float effectiveAccuracy = accuracy * (HasStatus(StatusEffectType.Blind) ? 0.5f : 1.0f);
        int hitChance = Mathf.RoundToInt(effectiveAccuracy) - target.evasion;
        hitChance = Mathf.Clamp(hitChance, 5, 100);
        return UnityEngine.Random.Range(0, 100) < hitChance;
    }

    // =========================
    // 是否暴击
    // =========================
    public bool CheckCrit()
    {
        return UnityEngine.Random.Range(0, 100) < critRate;
    }

    // =========================
    // 学习新技能
    // =========================
    public void LearnSkill(Skill newSkill, bool isArts = true)
    {
        if (isArts)
        {
            if (!artsList.Contains(newSkill))
            {
                artsList.Add(newSkill);
                Debug.Log($"{unitName} 学会了战技：{newSkill.skillName}");
            }
        }
        else
        {
            if (!skillList.Contains(newSkill))
            {
                skillList.Add(newSkill);
                Debug.Log($"{unitName} 学会了角色技能：{newSkill.skillName}");
            }
        }
    }

    // =========================
    // 忘记技能
    // =========================
    public void ForgetSkill(Skill skillToRemove, bool isArts = true)
    {
        if (isArts)
        {
            if (artsList.Contains(skillToRemove))
            {
                artsList.Remove(skillToRemove);
                Debug.Log($"{unitName} 忘记了战技：{skillToRemove.skillName}");
            }
        }
        else
        {
            if (skillList.Contains(skillToRemove))
            {
                skillList.Remove(skillToRemove);
                Debug.Log($"{unitName} 忘记了角色技能：{skillToRemove.skillName}");
            }
        }
    }

    // =========================
    // 异常状态系统
    // =========================
    [Header("战斗状态")]
    public bool isBreak = false;                            // 破防状态
    public System.Collections.Generic.List<StatusEffect> activeEffects =
        new System.Collections.Generic.List<StatusEffect>();

    // ── 伤害倍率属性（供 BattleManager 查询）──
    /// <summary>受到伤害倍率：Freeze 时 ×1.5，Break 时 ×2.0</summary>
    public float IncomingDamageMultiplier
    {
        get
        {
            float multiplier = 1.0f;
            if (HasStatus(StatusEffectType.Freeze)) multiplier *= 1.5f;
            if (isBreak) multiplier *= 2.0f;
            return multiplier;
        }
    }
    /// <summary>攻击力倍率：Terror 时 ×0.7</summary>
    public float AttackMultiplier => HasStatus(StatusEffectType.Terror) ? 0.7f : 1.0f;

    // ── 行动能力属性 ──
    /// <summary>是否可以行动（Sleep / Freeze 会阻止行动）</summary>
    public bool CanAct    => !HasStatus(StatusEffectType.Sleep) && !HasStatus(StatusEffectType.Freeze);
    /// <summary>是否可以使用技能或战技</summary>
    public bool CanUseSkill => !HasStatus(StatusEffectType.Silence);
    /// <summary>是否处于混乱状态</summary>
    public bool IsConfused  => HasStatus(StatusEffectType.Confuse);
    /// <summary>是否处于震荡状态</summary>
    public bool IsShocked   => HasStatus(StatusEffectType.Shock);

    public bool HasStatus(StatusEffectType type)
    {
        return activeEffects.Exists(e => e.type == type);
    }

    /// <summary>施加异常状态；若已存在则取持续时间较长值（不叠层）</summary>
    public void ApplyStatusEffect(StatusEffect effect)
    {
        var existing = activeEffects.Find(e => e.type == effect.type);
        if (existing != null)
        {
            existing.remainingRounds = Mathf.Max(existing.remainingRounds, effect.remainingRounds);
            Debug.Log($"[Status] {unitName} 的 {effect.type} 持续时间刷新");
        }
        else
        {
            activeEffects.Add(effect);
            Debug.Log($"[Status] {unitName} 获得异常状态：{effect.type}");
        }
    }

    /// <summary>移除指定类型的异常状态</summary>
    public void RemoveStatusEffect(StatusEffectType type)
    {
        int count = activeEffects.RemoveAll(e => e.type == type);
        if (count > 0) Debug.Log($"[Status] {unitName} 解除了 {type} 状态");
    }

    /// <summary>清除所有异常状态与 Break 标记（战斗结束或复活时调用）</summary>
    public void ClearAllStatusEffects()
    {
        activeEffects.Clear();
        isBreak = false;
        breakSkipTurnCount = 0;
        ResetShield();
    }

    /// <summary>每回合结束时调用：递减持续时间并移除到期状态</summary>
    public void TickStatusEffects()
    {
        activeEffects.RemoveAll(e =>
        {
            bool expired = e.Tick();
            if (expired) Debug.Log($"[Status] {unitName} 的 {e.type} 状态已解除");
            return expired;
        });
    }

    public bool IsWeakTo(AttackType attackType)
    {
        return weaknessTypes.Contains(attackType);
    }

    public bool ApplyShieldDamage(AttackType attackType, int hitCount)
    {
        if (unitType != UnitType.Enemy || maxShield <= 0 || isBreak)
            return false;

        if (attackType == AttackType.None || !IsWeakTo(attackType))
            return false;

        int shieldDamage = Mathf.Max(1, hitCount);
        currentShield = Mathf.Max(0, currentShield - shieldDamage);
        OnShieldChanged?.Invoke(currentShield, maxShield);
        Debug.Log($"[Shield] {unitName} 弱点命中 {attackType}，护盾 -{shieldDamage}，当前 {currentShield}/{maxShield}");

        return currentShield <= 0;
    }

    public void EnterBreak(int skipTurns)
    {
        if (unitType != UnitType.Enemy)
            return;

        isBreak = true;
        breakSkipTurnCount = Mathf.Max(breakSkipTurnCount, Mathf.Max(1, skipTurns));
        ApplyStatusEffect(new StatusEffect(StatusEffectType.Break, -1));
        Debug.Log($"[Break] {unitName} 进入 Break，需跳过 {breakSkipTurnCount} 次行动");
    }

    public bool ConsumeBreakActionSkip()
    {
        if (!isBreak || breakSkipTurnCount <= 0)
            return false;

        breakSkipTurnCount--;
        Debug.Log($"[Break] {unitName} 被跳过一次行动，剩余跳过次数: {breakSkipTurnCount}");

        if (breakSkipTurnCount <= 0)
            ExitBreak();

        return true;
    }

    void ExitBreak()
    {
        isBreak = false;
        RemoveStatusEffect(StatusEffectType.Break);
        ResetShield();
        Debug.Log($"[Break] {unitName} Break 结束，护盾重置为 {currentShield}/{maxShield}");
    }

    public void ResetShield()
    {
        if (unitType == UnitType.Enemy && maxShield > 0)
            currentShield = maxShield;
        else
            currentShield = 0;

        OnShieldChanged?.Invoke(currentShield, maxShield);
    }
}