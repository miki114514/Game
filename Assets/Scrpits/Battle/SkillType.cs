using UnityEngine;

public enum SkillType { Physical, Magical, Heal, Buff, Debuff }

[CreateAssetMenu(fileName = "NewSkill", menuName = "Battle/Skill")]
public class Skill : ScriptableObject
{
    public string skillName;
    public SkillType type;
    public int costSP;
    public int power;         // 基础伤害或恢复量
    public string description;

    // 技能执行逻辑
    public virtual void Execute(BattleUnit user, BattleUnit target)
    {
        if (type == SkillType.Physical || type == SkillType.Magical)
        {
            if (!user.CheckHit(target))
            {
                Debug.Log($"{user.unitName} 使用 {skillName} 但是未命中!");
                return;
            }

            int damage = power;
            if (type == SkillType.Physical)
                damage = Mathf.Max(power + user.physicalAttack - target.physicalDefense, 0);
            else
                damage = Mathf.Max(power + user.magicAttack - target.magicDefense, 0);

            if (user.CheckCrit())
            {
                damage = (int)(damage * 1.5f);
                Debug.Log("暴击!");
            }

            target.TakeDamage(damage);
            user.UseSP(costSP);
            Debug.Log($"{user.unitName} 使用 {skillName} 对 {target.unitName} 造成 {damage} 点伤害");
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