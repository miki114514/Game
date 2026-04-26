using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemySizeType
{
    Small,
    Medium,
    Large,
    Huge
}

public enum BattleEncounterType
{
    Normal,
    Boss
}

public enum BattleLayoutTemplateType
{
    Normal_1,
    Normal_2,
    Normal_3,
    Normal_4,
    Boss_1,
    Boss_2,
    Boss_3,
    Boss_4
}

public enum EncounterEnemyRole
{
    Auto,
    Boss,
    Add
}

public static class BattleUnitDefinitionUtility
{
    public static BattleUnit GetTemplate(GameObject battlePrefab)
    {
        return battlePrefab != null ? battlePrefab.GetComponent<BattleUnit>() : null;
    }

    public static Sprite ResolvePortrait(GameObject battlePrefab, Sprite overridePortrait)
    {
        if (overridePortrait != null)
            return overridePortrait;

        BattleUnit template = GetTemplate(battlePrefab);
        return template != null ? template.portrait : null;
    }

    public static void ApplyFromPrefabTemplate(GameObject battlePrefab, BattleUnit unit, UnitType resolvedUnitType, string resolvedName, Sprite overridePortrait, bool initializeState = true)
    {
        if (unit == null)
            return;

        BattleUnit template = GetTemplate(battlePrefab);
        if (battlePrefab != null && template == null)
        {
            Debug.LogWarning($"[BattleData] `{battlePrefab.name}` 未挂载 BattleUnit，无法读取战斗属性。", battlePrefab);
        }

        if (template != null && template != unit)
            CopyBattleFields(template, unit);

        unit.unitType = resolvedUnitType;

        if (!string.IsNullOrWhiteSpace(resolvedName))
            unit.unitName = resolvedName;

        if (initializeState)
            unit.InitializeBattleState();
    }

    static void CopyBattleFields(BattleUnit source, BattleUnit target)
    {
        target.level = source.level;
        target.maxHP = source.maxHP;
        target.maxSP = source.maxSP;
        target.maxBP = source.maxBP;
        target.startBattleBP = source.startBattleBP;
        target.bpRecoveryPerTurn = source.bpRecoveryPerTurn;

        target.physicalAttack = source.physicalAttack;
        target.magicAttack = source.magicAttack;
        target.physicalDefense = source.physicalDefense;
        target.magicDefense = source.magicDefense;
        target.accuracy = source.accuracy;
        target.speed = source.speed;
        target.critRate = source.critRate;
        target.evasion = source.evasion;

        target.normalAttackDamageType = source.normalAttackDamageType;
        target.normalAttackWeaponType = source.normalAttackWeaponType;
        target.normalAttackElementType = source.normalAttackElementType;
        target.normalAttackType = source.normalAttackType;

        target.weaponAttackEntries = source.weaponAttackEntries != null
            ? new List<WeaponAttackEntry>(source.weaponAttackEntries)
            : new List<WeaponAttackEntry>();

        target.currentExp = source.currentExp;
        target.expToNextLevel = source.expToNextLevel;
        target.currentJP = source.currentJP;

        target.maxShield = source.maxShield;
        target.weaknessTypes = source.weaknessTypes != null
            ? new List<AttackType>(source.weaknessTypes)
            : new List<AttackType>();
        target.statusImmunities = source.statusImmunities != null
            ? new List<StatusEffectType>(source.statusImmunities)
            : new List<StatusEffectType>();

        target.tachie = source.tachie;
        target.idleAnimationSource = source.idleAnimationSource;
        target.battleAnimator = null;
        target.idleAnimationStateName = source.idleAnimationStateName;
        target.randomizeAnimatorIdleStartTime = source.randomizeAnimatorIdleStartTime;
        target.idleAnimationFrames = source.idleAnimationFrames != null
            ? new List<Sprite>(source.idleAnimationFrames)
            : new List<Sprite>();
        target.idleAnimationFrameInterval = source.idleAnimationFrameInterval;
        target.idleAnimationLoop = source.idleAnimationLoop;
        target.randomizeIdleStartFrame = source.randomizeIdleStartFrame;
        target.playBattleEnterAnimation = source.playBattleEnterAnimation;
        target.enterAnimationStateName = source.enterAnimationStateName;
        target.randomizeEnterAnimationStartTime = source.randomizeEnterAnimationStartTime;
        target.waitForEnterAnimationToFinish = source.waitForEnterAnimationToFinish;
        target.enterAnimationFallbackDuration = source.enterAnimationFallbackDuration;
        target.playBattleEnterMove = source.playBattleEnterMove;
        target.enterMoveDistance = source.enterMoveDistance;
        target.enterMoveDuration = source.enterMoveDuration;
        target.playBattleActionAnimation = source.playBattleActionAnimation;
        target.normalAttackAnimationStateName = source.normalAttackAnimationStateName;
        target.randomizeNormalAttackAnimationStartTime = source.randomizeNormalAttackAnimationStartTime;
        target.normalAttackAnimationFallbackDuration = source.normalAttackAnimationFallbackDuration;
        target.autoReturnToIdleAfterAction = source.autoReturnToIdleAfterAction;

        target.artsAbilityMultiplier = source.artsAbilityMultiplier;
        target.forcedDamageReductionMultiplier = source.forcedDamageReductionMultiplier;
        target.classDefinition = source.classDefinition;

        target.artsList = source.artsList != null
            ? new List<Skill>(source.artsList)
            : new List<Skill>();
        target.skillList = source.skillList != null
            ? new List<Skill>(source.skillList)
            : new List<Skill>();
    }
}
