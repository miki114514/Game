using System.Collections.Generic;
using UnityEngine;

public enum SkillType { Physical, Magical, Heal, Buff, Debuff, Judgement }
public enum SkillTargetType { EnemySingle, AllySingle, Self }

[CreateAssetMenu(fileName = "NewSkill", menuName = "Battle/Skill")]
public class Skill : ScriptableObject
{
    public string skillName;
    public SkillType type;
    public DamageType damageType = DamageType.Physical;
    public WeaponType weaponType = WeaponType.None;
    public ElementType elementType = ElementType.None;
    public AttackType attackType = AttackType.None; // 旧字段兼容
    public AttackType requiredWeaponType = AttackType.None; // 旧字段兼容
    public bool countBothWeaknessesSeparately = false;
    public SkillTargetType targetType = SkillTargetType.EnemySingle;
    public int hitCount = 1;
    public bool allowBoost = true;
    public bool boostAddsHitCount = false;
    public bool boostExtendsDuration = true;
    public int roundsPerBoost = 1;
    public int costSP;
    public float baseMultiplier = 1f;
    [Range(0, 100)] public int triggerChance = 100;
    public StatusEffectType judgementEffect = StatusEffectType.None;
    public int judgementRounds = 1;

    [Header("学习限制")]
    [Tooltip("关闭时表示所有职业都能学习；开启后仅 learnableClasses 列表中的职业可学习。")]
    public bool restrictLearnableClasses = false;
    public List<CharacterClassDefinition> learnableClasses = new List<CharacterClassDefinition>();

    public string description;

    public bool IsDamageSkill => type == SkillType.Physical || type == SkillType.Magical;
    public bool IsHealSkill => type == SkillType.Heal;
    public bool IsJudgementSkill => type == SkillType.Buff || type == SkillType.Debuff || type == SkillType.Judgement;
    public bool UsesMagicFormula => ResolveDamageType() == DamageType.Elemental;
    public bool CanBoost => allowBoost && (IsDamageSkill || IsHealSkill || IsJudgementSkill);
    public bool IsCharacterSkillCategory => type == SkillType.Buff || type == SkillType.Debuff;

    public bool CanBeLearnedBy(BattleUnit unit)
    {
        if (unit == null)
            return !restrictLearnableClasses;

        return CanBeLearnedByClass(unit.classDefinition);
    }

    public bool CanBeLearnedByClass(CharacterClassDefinition classDefinition)
    {
        if (!restrictLearnableClasses)
            return true;

        if (classDefinition == null || learnableClasses == null || learnableClasses.Count == 0)
            return false;

        return learnableClasses.Contains(classDefinition);
    }

    public BattleUnit ResolveTarget(BattleUnit user, BattleUnit selectedTarget)
    {
        if (targetType == SkillTargetType.Self || selectedTarget == null)
            return user;

        return selectedTarget;
    }

    public DamageType ResolveDamageType()
    {
        if (type == SkillType.Magical || damageType == DamageType.Elemental)
            return DamageType.Elemental;

        ElementType resolvedElement = ResolveElementType();
        WeaponType resolvedWeapon = ResolveWeaponType();
        return (resolvedElement != ElementType.None && resolvedWeapon == WeaponType.None)
            ? DamageType.Elemental
            : DamageType.Physical;
    }

    public WeaponType ResolveWeaponType()
    {
        return weaponType != WeaponType.None
            ? weaponType
            : BattleFormula.ToWeaponType(attackType);
    }

    public ElementType ResolveElementType()
    {
        return elementType != ElementType.None
            ? elementType
            : BattleFormula.ToElementType(attackType);
    }

    public WeaponType ResolveRequiredWeaponType()
    {
        if (requiredWeaponType != AttackType.None)
            return BattleFormula.ToWeaponType(requiredWeaponType);

        return ResolveWeaponType();
    }

    public string GetBoostPreviewText(int runtimeBoostLevel)
    {
        int boost = Mathf.Clamp(runtimeBoostLevel, 0, BattleFormula.MaxBoostLevel);
        if (!CanBoost)
            return string.Empty;

        if (IsDamageSkill || IsHealSkill)
            return $"Boost {boost}  ×{BattleFormula.GetBoostMultiplier(boost):0.0}";

        if (IsJudgementSkill)
        {
            if (judgementEffect == StatusEffectType.None)
                return $"Boost {boost}  No Status";

            int chance = BattleFormula.GetBoostedTriggerChance(triggerChance, boost);
            int rounds = boostExtendsDuration
                ? BattleFormula.GetBoostedDuration(judgementRounds, boost, roundsPerBoost)
                : judgementRounds;
            return $"Boost {boost}  {chance}% / {rounds}T";
        }

        return $"Boost {boost}";
    }

    // 技能执行逻辑
    public virtual void Execute(BattleManager battleManager, BattleUnit user, BattleUnit target, int runtimeBoostLevel = 0)
    {
        BattleUnit resolvedTarget = ResolveTarget(user, target);
        int boostLevel = CanBoost ? Mathf.Clamp(runtimeBoostLevel, 0, BattleFormula.MaxBoostLevel) : 0;
        int actualHitCount = Mathf.Max(1, hitCount + (boostAddsHitCount ? boostLevel : 0));

        if (resolvedTarget == null)
            return;

        if (IsDamageSkill)
        {
            DamageType resolvedDamageType = ResolveDamageType();
            WeaponType resolvedWeaponType = ResolveWeaponType();
            ElementType resolvedElementType = ResolveElementType();
            WeaponType resolvedRequiredWeaponType = ResolveRequiredWeaponType();

            bool requiresHitCheck = resolvedDamageType == DamageType.Physical;
            if (requiresHitCheck && !user.CheckHit(resolvedTarget))
            {
                Debug.Log($"{user.unitName} 使用 {skillName} 但是未命中!");
                user.UseSP(costSP);
                return;
            }

            int damage = BattleFormula.CalculateArtDamage(user, resolvedTarget, baseMultiplier, resolvedDamageType, boostLevel, resolvedRequiredWeaponType);

            if (user.CheckCrit())
            {
                damage = Mathf.Min(9999, Mathf.RoundToInt(damage * 1.5f));
                Debug.Log("暴击!");
            }

            resolvedTarget.TakeDamage(damage);
            battleManager?.TryApplyShieldDamage(resolvedTarget, resolvedWeaponType, resolvedElementType, actualHitCount, countBothWeaknessesSeparately);
            user.UseSP(costSP);
            Debug.Log($"{user.unitName} 使用 {skillName} 对 {resolvedTarget.unitName} 造成 {damage} 点伤害（{actualHitCount}段，Boost {boostLevel}，{resolvedDamageType}/{resolvedWeaponType}/{resolvedElementType}）");
        }
        else if (IsHealSkill)
        {
            int healAmount = BattleFormula.CalculateArtHeal(user, baseMultiplier, boostLevel);
            resolvedTarget.Heal(healAmount);
            user.UseSP(costSP);
            Debug.Log($"{user.unitName} 使用 {skillName} 治疗 {resolvedTarget.unitName} {healAmount} HP（Boost {boostLevel}）");
        }
        else if (IsJudgementSkill)
        {
            if (judgementEffect == StatusEffectType.None)
            {
                user.UseSP(costSP);
                Debug.Log($"{user.unitName} 使用 {skillName}，但未设置附加状态");
                return;
            }

            int effectiveChance = BattleFormula.GetBoostedTriggerChance(triggerChance, boostLevel);
            int effectiveRounds = boostExtendsDuration
                ? BattleFormula.GetBoostedDuration(judgementRounds, boostLevel, roundsPerBoost)
                : judgementRounds;

            bool triggered = BattleFormula.RollJudgement(effectiveChance);
            if (!triggered)
            {
                user.UseSP(costSP);
                Debug.Log($"{user.unitName} 使用 {skillName}，判定未触发（Boost {boostLevel}，触发率 {effectiveChance}%）");
                return;
            }

            if (resolvedTarget.IsImmuneTo(judgementEffect))
            {
                user.UseSP(costSP);
                Debug.Log($"{resolvedTarget.unitName} 免疫 {judgementEffect}，{skillName} 未附加状态");
                return;
            }

            if (battleManager != null)
                battleManager.ApplyStatus(resolvedTarget, judgementEffect, effectiveRounds);
            else
                resolvedTarget.ApplyStatusEffect(new StatusEffect(judgementEffect, effectiveRounds));

            user.UseSP(costSP);
            Debug.Log($"{user.unitName} 使用 {skillName}，为 {resolvedTarget.unitName} 附加 {judgementEffect}（{effectiveRounds}回合，Boost {boostLevel}）");
        }
    }
}