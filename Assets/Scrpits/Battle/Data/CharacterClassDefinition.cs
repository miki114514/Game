using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterClass", menuName = "Battle/Data/Character Class")]
public class CharacterClassDefinition : ScriptableObject
{
    [Header("基础信息")]
    public string classId = "class_swordfighter";
    public string displayName = "新职业";
    [TextArea(2, 5)] public string description;

    [Header("装备限制")]
    [Tooltip("可装备武器类型列表。为空时表示不限制（兼容旧角色）。")]
    public List<WeaponType> allowedWeaponTypes = new List<WeaponType>();

    [Header("可学习技能池")]
    [Tooltip("该职业可学习的战技（Arts）。技能页将按此列表展示。")]
    public List<Skill> learnableArts = new List<Skill>();
    [Tooltip("该职业可学习的角色技能（Skill，可选）。")]
    public List<Skill> learnableCharacterSkills = new List<Skill>();

    public bool AllowsAnyWeapon => allowedWeaponTypes == null || allowedWeaponTypes.Count == 0;

    public bool CanEquipWeaponType(WeaponType weaponType)
    {
        if (weaponType == WeaponType.None)
            return true;

        if (AllowsAnyWeapon)
            return true;

        return allowedWeaponTypes.Contains(weaponType);
    }

    public string GetDisplayNameOrFallback()
    {
        return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }

    public bool CanLearnArt(Skill skill)
    {
        if (skill == null)
            return false;

        if (learnableArts == null || learnableArts.Count == 0)
            return false;

        return learnableArts.Contains(skill) && skill.CanBeLearnedByClass(this);
    }

    public bool CanLearnCharacterSkill(Skill skill)
    {
        if (skill == null)
            return false;

        if (learnableCharacterSkills == null || learnableCharacterSkills.Count == 0)
            return false;

        return learnableCharacterSkills.Contains(skill) && skill.CanBeLearnedByClass(this);
    }
}