using UnityEngine;

/// <summary>
/// 武器装备数据
///
/// 设计规则：
///  - weaponType 决定普通攻击击打类型（剑→剑弱点，弓→弓弱点），也是破盾手段的来源。
///  - 技能类型与武器类型无关；武器不影响 SkillPower，仅影响 P.Atk、命中、速度、暴击。
///  - 装备武器后，GetResolvedNormalAttackWeaponType() 将优先返回本武器的 weaponType。
///
/// 典型属性分布：
///  Sword / Spear / Axe  → 高 pAtk
///  Dagger / Bow          → 中 pAtk + speed / crit
///  Staff                 → 低 pAtk + accuracy（法杖角色倾向 E.Atk 而非 P.Atk）
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Equipment/Weapon")]
public class WeaponData : EquipmentData
{
    [Header("武器属性")]
    [Tooltip("武器类型：决定普通攻击的击打类型与可命中的弱点种类")]
    public WeaponType weaponType = WeaponType.Sword;

    // 典型有效字段：pAtk, accuracy, speed, crit
    // pDef / eDef / hp / sp 通常保持 0（少数特殊武器可填）
}
