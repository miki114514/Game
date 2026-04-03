using UnityEngine;

public enum SkillType { Physical, Magical, Heal, Buff, Debuff }

[CreateAssetMenu(fileName = "NewSkill", menuName = "Battle/Skill")]
public class Skill : ScriptableObject
{
    public string skillName;
    public SkillType type;
    public AttackType attackType = AttackType.None;
    public int hitCount = 1;
    public int costSP;
    public int power;         // 基础伤害或恢复量
    public string description;

    // 技能执行逻辑
    public virtual void Execute(BattleManager battleManager, BattleUnit user, BattleUnit target)
    {
        int actualHitCount = Mathf.Max(1, hitCount);

        if (type == SkillType.Physical || type == SkillType.Magical)
        {
            if (!user.CheckHit(target))
            {
                Debug.Log($"{user.unitName} 使用 {skillName} 但是未命中!");
                user.UseSP(costSP);
                return;
            }

            int damage = power;
            if (type == SkillType.Physical)
            {
                int baseAtk = Mathf.RoundToInt(user.physicalAttack * user.AttackMultiplier);
                damage = Mathf.Max(power + baseAtk - target.physicalDefense, 0);
            }
            else
                damage = Mathf.Max(power + user.magicAttack - target.magicDefense, 0);

            damage = Mathf.RoundToInt(damage * target.IncomingDamageMultiplier);

            if (user.CheckCrit())
            {
                damage = (int)(damage * 1.5f);
                Debug.Log("暴击!");
            }

            target.TakeDamage(damage);
            battleManager?.TryApplyShieldDamage(target, attackType, actualHitCount);
            user.UseSP(costSP);
            Debug.Log($"{user.unitName} 使用 {skillName} 对 {target.unitName} 造成 {damage} 点伤害（{actualHitCount}段）");
        }
        else if (type == SkillType.Heal)
        {
            int healAmount = power + user.magicAttack;
            target.Heal(healAmount); 
            user.UseSP(costSP);
            Debug.Log($"{user.unitName} 使用 {skillName} 治疗 {healAmount} HP");
        }
    }
}