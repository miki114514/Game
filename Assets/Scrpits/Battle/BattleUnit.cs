using UnityEngine;
using System;
using System.Collections.Generic;

public enum UnitType { Player, Enemy }

public class BattleUnit : MonoBehaviour
{
    [Header("基础属性")]
    public string unitName;
    public UnitType unitType;

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

    [Header("技能管理")]
    public List<Skill> artsList = new List<Skill>();   // 战技列表（Arts）
    public List<Skill> skillList = new List<Skill>();  // 角色技能列表（Skill）

    public event Action<int, int> OnHPChanged;
    public event Action<int, int> OnSPChanged;

    void Awake()
    {
        currentHP = maxHP;
        currentSP = maxSP;
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

        Debug.Log($"{unitName} 升级到 {level} 级！");
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
        int hitChance = accuracy - target.evasion;
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
}