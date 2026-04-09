using UnityEngine;
using System;
using System.Collections.Generic;

public enum UnitType { Player, Enemy }
public enum DamageType { Physical, Elemental }

public enum WeaponType
{
    None,
    Sword,
    Lance,
    Dagger,
    Axe,
    Bow,
    Staff
}

public enum ElementType
{
    None,
    Fire,
    Ice,
    Thunder,
    Wind,
    Light,
    Dark
}

/// <summary>
/// 扁平化弱点枚举：供敌人弱点列表与 Break UI 映射使用。
/// 保留 Strike / Pierce 作为旧数据兼容项。
/// </summary>
public enum AttackType
{
    None = 0,
    Sword = 1,
    Fire = 2,
    Ice = 3,
    Thunder = 4,
    Strike = 5,
    Pierce = 6,
    Lance = 7,
    Dagger = 8,
    Axe = 9,
    Bow = 10,
    Staff = 11,
    Wind = 12,
    Light = 13,
    Dark = 14
}

[Serializable]
public class WeaponAttackEntry
{
    public WeaponType weaponType = WeaponType.None;
    public AttackType attackType = AttackType.None; // 旧字段兼容
    public int attackPower = 0;

    public WeaponType ResolveWeaponType()
    {
        if (weaponType != WeaponType.None)
            return weaponType;

        return BattleFormula.ToWeaponType(attackType);
    }
}

public class BattleUnit : MonoBehaviour
{
    public const int MaxLevel = 60;

    [Header("基础属性")]
    public string unitName;
    public UnitType unitType;

    [Header("头像")]
    public Sprite portrait;   // 队列图标用头像（菱形裁切）

    [Range(1, MaxLevel)]
    public int level = 1;

    public int maxHP = 100;
    public int currentHP;
    public int maxSP = 50;
    public int currentSP;

    [Header("BP系统")]
    public int maxBP = BattleFormula.DefaultMaxBP;
    [SerializeField] private int currentBP = 0;
    public int startBattleBP = 0;
    public int bpRecoveryPerTurn = 1;

    public int physicalAttack = 20;
    public int magicAttack = 15;
    public int physicalDefense = 10;
    public int magicDefense = 8;

    public int accuracy = 95;    // 命中率 %
    public int speed = 10;       // 行动速度
    public int critRate = 10;    // 暴击 %
    public int evasion = 5;      // 回避 %

    [Header("普通攻击类型")]
    public DamageType normalAttackDamageType = DamageType.Physical;
    public WeaponType normalAttackWeaponType = WeaponType.Sword;
    public ElementType normalAttackElementType = ElementType.None;
    public AttackType normalAttackType = AttackType.Sword; // 旧字段兼容

    [Header("武器攻击力")]
    public List<WeaponAttackEntry> weaponAttackEntries = new List<WeaponAttackEntry>();

    [Header("玩家成长")]
    public int currentExp = 0;
    public int expToNextLevel = 100;

    [Header("敌人护盾")]
    public int maxShield = 0;
    public int currentShield = 0;
    public List<AttackType> weaknessTypes = new List<AttackType>();
    public List<StatusEffectType> statusImmunities = new List<StatusEffectType>();

    [Header("战技倍率")]
    public float artsAbilityMultiplier = 1f;
    public float forcedDamageReductionMultiplier = 1f;

    [Header("技能管理")]
    public List<Skill> artsList = new List<Skill>();   // 战技列表（Arts）
    public List<Skill> skillList = new List<Skill>();  // 角色技能列表（Skill）

    public event Action<int, int> OnHPChanged;
    public event Action<int, int> OnSPChanged;
    public event Action<int, int> OnBPChanged;
    public event Action<int, int> OnEXPChanged;
    public event Action<int, int> OnShieldChanged;
    public event Action<AttackType> OnWeaknessRevealed;
    public event Action OnWeaknessStateChanged;

    private readonly HashSet<AttackType> revealedWeaknessTypes = new HashSet<AttackType>();
    private int breakSkipTurnCount = 0;
    private bool isDefending = false;
    private bool hasEnteredTurnOnce = false;

    public bool IsDefending => isDefending;
    public int CurrentBP => currentBP;
    public int MaxBP => maxBP;
    public float LevelMultiplier => BattleFormula.GetLevelMultiplier(level);

    void Awake()
    {
        InitializeBattleState();
    }

    public void InitializeBattleState()
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        maxBP = Mathf.Max(0, maxBP);
        startBattleBP = Mathf.Clamp(startBattleBP, 0, maxBP);
        bpRecoveryPerTurn = Mathf.Max(0, bpRecoveryPerTurn);
        currentHP = maxHP;
        currentSP = maxSP;
        ResetBP();
        isBreak = false;
        breakSkipTurnCount = 0;
        ResetRevealedWeaknesses();
        ResetShield();
        isDefending = false;
        forcedDamageReductionMultiplier = Mathf.Max(0f, forcedDamageReductionMultiplier);
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

    public bool HasEnoughSP(int cost)
    {
        int safeCost = Mathf.Max(0, cost);
        return currentSP >= safeCost;
    }

    // =========================
    // BP 系统
    // =========================
    public void ResetBP()
    {
        currentBP = Mathf.Clamp(startBattleBP, 0, maxBP);
        hasEnteredTurnOnce = false;
        OnBPChanged?.Invoke(currentBP, maxBP);
    }

    public void HandleTurnStartBP()
    {
        if (!hasEnteredTurnOnce)
        {
            hasEnteredTurnOnce = true;
            OnBPChanged?.Invoke(currentBP, maxBP);
            return;
        }

        GainBP(bpRecoveryPerTurn);
    }

    public void GainBP(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        int oldBP = currentBP;
        currentBP = Mathf.Clamp(currentBP + safeAmount, 0, maxBP);
        hasEnteredTurnOnce = true;

        if (currentBP != oldBP)
            Debug.Log($"[BP] {unitName} 回复了 {currentBP - oldBP} BP，当前 {currentBP}/{maxBP}");

        OnBPChanged?.Invoke(currentBP, maxBP);
    }

    public bool SpendBP(int amount)
    {
        int spend = Mathf.Clamp(amount, 0, currentBP);

        if (spend <= 0)
        {
            OnBPChanged?.Invoke(currentBP, maxBP);
            return amount <= 0;
        }

        currentBP -= spend;
        OnBPChanged?.Invoke(currentBP, maxBP);
        Debug.Log($"[BP] {unitName} 消耗了 {spend} BP，当前 {currentBP}/{maxBP}");
        return spend == amount;
    }

    public int GetMaxAvailableBoostLevel()
    {
        return Mathf.Min(BattleFormula.MaxBoostLevel, currentBP);
    }

    public int ClampBoostLevel(int requestedBoostLevel)
    {
        return Mathf.Clamp(requestedBoostLevel, 0, GetMaxAvailableBoostLevel());
    }

    public int ConsumeBoostLevel(int requestedBoostLevel)
    {
        int actualBoostLevel = ClampBoostLevel(requestedBoostLevel);
        SpendBP(actualBoostLevel);
        return actualBoostLevel;
    }

    // =========================
    // 升级
    // =========================
    public void LevelUp()
    {
        if (level >= MaxLevel)
        {
            level = MaxLevel;
            currentExp = 0;
            expToNextLevel = 0;
            return;
        }

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

        if (level >= MaxLevel)
        {
            level = MaxLevel;
            currentExp = 0;
            expToNextLevel = 0;
        }
    }

    public void AddExperience(int amount)
    {
        if (unitType != UnitType.Player || amount <= 0)
            return;

        if (level >= MaxLevel)
        {
            level = MaxLevel;
            currentExp = 0;
            expToNextLevel = 0;
            return;
        }

        currentExp += amount;
        while (level < MaxLevel && currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
            if (level < MaxLevel)
                expToNextLevel = CalculateNextLevelExp(level);
        }

        if (level >= MaxLevel)
        {
            level = MaxLevel;
            currentExp = 0;
            expToNextLevel = 0;
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

    public void SetDefending(bool defending)
    {
        isDefending = defending;
    }

    public void BeginTurn()
    {
        if (isDefending)
        {
            isDefending = false;
            Debug.Log($"[Battle] {unitName} 的防御姿态结束");
        }
    }

    public bool IsImmuneTo(StatusEffectType type)
    {
        return statusImmunities.Contains(type);
    }

    public WeaponType GetResolvedNormalAttackWeaponType()
    {
        return normalAttackWeaponType != WeaponType.None
            ? normalAttackWeaponType
            : BattleFormula.ToWeaponType(normalAttackType);
    }

    public ElementType GetResolvedNormalAttackElementType()
    {
        return normalAttackElementType != ElementType.None
            ? normalAttackElementType
            : BattleFormula.ToElementType(normalAttackType);
    }

    public DamageType GetResolvedNormalAttackDamageType()
    {
        if (normalAttackDamageType == DamageType.Elemental)
            return DamageType.Elemental;

        ElementType resolvedElementType = GetResolvedNormalAttackElementType();
        WeaponType resolvedWeaponType = GetResolvedNormalAttackWeaponType();
        return (resolvedElementType != ElementType.None && resolvedWeaponType == WeaponType.None)
            ? DamageType.Elemental
            : DamageType.Physical;
    }

    public int GetWeaponAttack(WeaponType weaponType)
    {
        if (weaponType == WeaponType.None)
            return 0;

        WeaponAttackEntry entry = weaponAttackEntries.Find(item => item != null && item.ResolveWeaponType() == weaponType);
        return entry != null ? Mathf.Max(0, entry.attackPower) : 0;
    }

    public int GetWeaponAttack(AttackType attackType)
    {
        return GetWeaponAttack(BattleFormula.ToWeaponType(attackType));
    }

    public int GetCombatAttackValue(DamageType damageType, WeaponType requiredWeaponType)
    {
        bool usesElementalFormula = damageType == DamageType.Elemental;
        int attributeValue = usesElementalFormula ? magicAttack : physicalAttack;
        float adjustedAttributeValue = attributeValue * AttackMultiplier;
        int weaponValue = usesElementalFormula ? 0 : GetWeaponAttack(requiredWeaponType);
        return Mathf.Max(0, Mathf.RoundToInt(adjustedAttributeValue) + weaponValue);
    }

    public int GetCombatAttackValue(bool useMagicAttack, AttackType requiredWeaponType)
    {
        return GetCombatAttackValue(
            useMagicAttack ? DamageType.Elemental : DamageType.Physical,
            BattleFormula.ToWeaponType(requiredWeaponType));
    }

    public int GetCombatDefenseValue(DamageType damageType)
    {
        return Mathf.Max(0, damageType == DamageType.Elemental ? magicDefense : physicalDefense);
    }

    public int GetCombatDefenseValue(bool useMagicDefense)
    {
        return GetCombatDefenseValue(useMagicDefense ? DamageType.Elemental : DamageType.Physical);
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
        if (effect == null || effect.type == StatusEffectType.None)
            return;

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
        isDefending = false;
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

    public bool IsWeakTo(WeaponType weaponType)
    {
        AttackType weaknessType = BattleFormula.ToAttackType(weaponType);
        return weaknessType != AttackType.None && IsWeakTo(weaknessType);
    }

    public bool IsWeakTo(ElementType elementType)
    {
        AttackType weaknessType = BattleFormula.ToAttackType(elementType);
        return weaknessType != AttackType.None && IsWeakTo(weaknessType);
    }

    public bool IsWeaknessRevealed(AttackType attackType)
    {
        return revealedWeaknessTypes.Contains(attackType);
    }

    public void ResetRevealedWeaknesses()
    {
        if (revealedWeaknessTypes.Count == 0)
            return;

        revealedWeaknessTypes.Clear();
        OnWeaknessStateChanged?.Invoke();
    }

    bool TryRevealWeakness(AttackType attackType)
    {
        if (attackType == AttackType.None || !IsWeakTo(attackType))
            return false;

        if (!revealedWeaknessTypes.Add(attackType))
            return false;

        Debug.Log($"[Weakness] {unitName} 的弱点 {attackType} 已被揭示");
        OnWeaknessRevealed?.Invoke(attackType);
        OnWeaknessStateChanged?.Invoke();
        return true;
    }

    public bool ApplyShieldDamage(AttackType attackType, int hitCount)
    {
        return ApplyShieldDamage(
            BattleFormula.ToWeaponType(attackType),
            BattleFormula.ToElementType(attackType),
            hitCount,
            false);
    }

    public bool ApplyShieldDamage(WeaponType weaponType, ElementType elementType, int hitCount, bool countBothWeaknessesSeparately = false)
    {
        if (unitType != UnitType.Enemy || maxShield <= 0)
            return false;

        bool weaponWeakHit = weaponType != WeaponType.None && IsWeakTo(weaponType);
        bool elementWeakHit = elementType != ElementType.None && IsWeakTo(elementType);

        if (weaponWeakHit)
            TryRevealWeakness(BattleFormula.ToAttackType(weaponType));

        if (elementWeakHit)
            TryRevealWeakness(BattleFormula.ToAttackType(elementType));

        int matchedWeaknessCount = (weaponWeakHit ? 1 : 0) + (elementWeakHit ? 1 : 0);
        if (matchedWeaknessCount <= 0 || isBreak)
            return false;

        int weaknessMultiplier = countBothWeaknessesSeparately ? Mathf.Max(1, matchedWeaknessCount) : 1;
        int shieldDamage = Mathf.Max(1, hitCount) * weaknessMultiplier;
        currentShield = Mathf.Max(0, currentShield - shieldDamage);
        OnShieldChanged?.Invoke(currentShield, maxShield);

        string weaknessLabel = weaponWeakHit && elementWeakHit
            ? $"{weaponType} + {elementType}"
            : weaponWeakHit ? weaponType.ToString() : elementType.ToString();
        Debug.Log($"[Shield] {unitName} 弱点命中 {weaknessLabel}，护盾 -{shieldDamage}，当前 {currentShield}/{maxShield}");

        return currentShield <= 0;
    }

    public void EnterBreak(int skipTurns)
    {
        if (unitType != UnitType.Enemy)
            return;

        isBreak = true;
        breakSkipTurnCount = Mathf.Max(breakSkipTurnCount, Mathf.Max(1, skipTurns));
        ApplyStatusEffect(new StatusEffect(StatusEffectType.Break, -1));
        OnShieldChanged?.Invoke(currentShield, maxShield);
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

public static class BattleFormula
{
    public const int DefaultMaxBP = 5;
    public const int MaxBoostLevel = 3;

    private const float MinLevelMultiplier = 0.58f;
    private const float MaxLevelMultiplier = 1.56f;
    private const int DamageCap = 9999;
    private const int HealCap = 9999;
    private const float NormalAttackBaseMultiplier = 1f;
    private const float DefaultDefenseMultiplier = 0.5f;
    private const float GuardDefenseMultiplier = 0.65f;
    private const float RandomMin = 0.98f;
    private const float RandomMax = 1.02f;
    private const float StatusStep = 1.5f;

    public static float GetLevelMultiplier(int level)
    {
        int clampedLevel = Mathf.Clamp(level, 1, BattleUnit.MaxLevel);
        float t = (clampedLevel - 1f) / (BattleUnit.MaxLevel - 1f);
        return Mathf.Lerp(MinLevelMultiplier, MaxLevelMultiplier, t);
    }

    public static float GetBoostMultiplier(int boostLevel)
    {
        int clampedBoost = Mathf.Clamp(boostLevel, 0, MaxBoostLevel);
        return 1f + 0.9f * clampedBoost;
    }

    public static int GetBoostedAttackHitCount(int boostLevel)
    {
        return 1 + Mathf.Clamp(boostLevel, 0, MaxBoostLevel);
    }

    public static int GetBoostedTriggerChance(int baseChance, int boostLevel)
    {
        return Mathf.Clamp(Mathf.RoundToInt(baseChance * GetBoostMultiplier(boostLevel)), 0, 100);
    }

    public static int GetBoostedDuration(int baseRounds, int boostLevel, int roundsPerBoost = 1)
    {
        int extraRounds = Mathf.Max(0, boostLevel) * Mathf.Max(0, roundsPerBoost);
        return Mathf.Max(1, baseRounds + extraRounds);
    }

    public static bool IsWeaponAttackType(AttackType attackType)
    {
        return ToWeaponType(attackType) != WeaponType.None;
    }

    public static AttackType ToAttackType(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Sword: return AttackType.Sword;
            case WeaponType.Lance: return AttackType.Lance;
            case WeaponType.Dagger: return AttackType.Dagger;
            case WeaponType.Axe: return AttackType.Axe;
            case WeaponType.Bow: return AttackType.Bow;
            case WeaponType.Staff: return AttackType.Staff;
            default: return AttackType.None;
        }
    }

    public static AttackType ToAttackType(ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Fire: return AttackType.Fire;
            case ElementType.Ice: return AttackType.Ice;
            case ElementType.Thunder: return AttackType.Thunder;
            case ElementType.Wind: return AttackType.Wind;
            case ElementType.Light: return AttackType.Light;
            case ElementType.Dark: return AttackType.Dark;
            default: return AttackType.None;
        }
    }

    public static WeaponType ToWeaponType(AttackType attackType)
    {
        switch (attackType)
        {
            case AttackType.Sword: return WeaponType.Sword;
            case AttackType.Lance: return WeaponType.Lance;
            case AttackType.Dagger: return WeaponType.Dagger;
            case AttackType.Axe: return WeaponType.Axe;
            case AttackType.Bow:
            case AttackType.Pierce: return WeaponType.Bow;
            case AttackType.Staff:
            case AttackType.Strike: return WeaponType.Staff;
            default: return WeaponType.None;
        }
    }

    public static ElementType ToElementType(AttackType attackType)
    {
        switch (attackType)
        {
            case AttackType.Fire: return ElementType.Fire;
            case AttackType.Ice: return ElementType.Ice;
            case AttackType.Thunder: return ElementType.Thunder;
            case AttackType.Wind: return ElementType.Wind;
            case AttackType.Light: return ElementType.Light;
            case AttackType.Dark: return ElementType.Dark;
            default: return ElementType.None;
        }
    }

    public static int CalculateNormalAttackDamage(BattleUnit attacker, BattleUnit target)
    {
        if (attacker == null || target == null)
            return 0;

        return CalculateDamageInternal(
            attacker,
            target,
            NormalAttackBaseMultiplier,
            attacker.GetResolvedNormalAttackDamageType(),
            0,
            attacker.GetResolvedNormalAttackWeaponType(),
            attacker.artsAbilityMultiplier,
            includeStatusModifier: true);
    }

    public static int CalculateArtDamage(BattleUnit user, BattleUnit target, float baseMultiplier, bool usesMagicFormula, int boostLevel, AttackType requiredWeaponType)
    {
        return CalculateArtDamage(
            user,
            target,
            baseMultiplier,
            usesMagicFormula ? DamageType.Elemental : DamageType.Physical,
            boostLevel,
            ToWeaponType(requiredWeaponType));
    }

    public static int CalculateArtDamage(BattleUnit user, BattleUnit target, float baseMultiplier, DamageType damageType, int boostLevel, WeaponType requiredWeaponType)
    {
        if (user == null || target == null)
            return 0;

        return CalculateDamageInternal(
            user,
            target,
            baseMultiplier,
            damageType,
            boostLevel,
            requiredWeaponType,
            user.artsAbilityMultiplier,
            includeStatusModifier: true);
    }

    public static int CalculateArtHeal(BattleUnit user, float baseMultiplier, int boostLevel)
    {
        if (user == null)
            return 0;

        float baseValue = -baseMultiplier * user.magicDefense * GetBoostMultiplier(boostLevel);
        float calculatedValue = baseValue * user.artsAbilityMultiplier * user.LevelMultiplier;
        float executedValue = calculatedValue * GetRandomVariance();
        int finalValue = Mathf.Max(Mathf.RoundToInt(executedValue), -HealCap);
        return Mathf.Abs(finalValue);
    }

    public static bool RollJudgement(int triggerChance)
    {
        int chance = Mathf.Clamp(triggerChance, 0, 100);
        return UnityEngine.Random.Range(0, 100) < chance;
    }

    public static float GetStatusModifier(BattleUnit attacker, BattleUnit target)
    {
        float modifier = 1f;

        if (attacker != null)
        {
            if (attacker.HasStatus(StatusEffectType.AttackUp))
                modifier *= StatusStep;

            if (attacker.HasStatus(StatusEffectType.AttackDown))
                modifier /= StatusStep;
        }

        if (target != null)
        {
            if (target.HasStatus(StatusEffectType.DefenseUp))
                modifier /= StatusStep;

            if (target.HasStatus(StatusEffectType.DefenseDown))
                modifier *= StatusStep;
        }

        return modifier;
    }

    private static int CalculateDamageInternal(
        BattleUnit attacker,
        BattleUnit target,
        float baseMultiplier,
        DamageType damageType,
        int boostLevel,
        WeaponType requiredWeaponType,
        float abilityMultiplier,
        bool includeStatusModifier)
    {
        float attackValue = attacker.GetCombatAttackValue(damageType, requiredWeaponType);
        float defenseValue = target.GetCombatDefenseValue(damageType);
        float defenseMultiplier = target.IsDefending ? GuardDefenseMultiplier : DefaultDefenseMultiplier;
        float baseDamage = ((baseMultiplier * attackValue) - (defenseMultiplier * defenseValue)) * GetBoostMultiplier(boostLevel);
        float statusModifier = includeStatusModifier ? GetStatusModifier(attacker, target) : 1f;
        float calculatedDamage = baseDamage * abilityMultiplier * statusModifier * attacker.LevelMultiplier;
        float executedDamage = calculatedDamage * GetRandomVariance() * Mathf.Max(0f, target.forcedDamageReductionMultiplier);
        float totalDamage = executedDamage * target.IncomingDamageMultiplier;
        int finalDamage = Mathf.Clamp(Mathf.RoundToInt(totalDamage), 0, DamageCap);
        return Mathf.Min(finalDamage, Mathf.Max(0, target.currentHP));
    }

    private static float GetRandomVariance()
    {
        return UnityEngine.Random.Range(RandomMin, RandomMax + 0.0001f);
    }
}