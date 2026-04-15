using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备状态抗性条目：抵消指定异常状态的触发概率
/// FinalChance = BaseChance × (1 - resistancePercent)
/// </summary>
[Serializable]
public class StatusResistanceEntry
{
    [Tooltip("目标异常状态类型")]
    public StatusEffectType statusType;

    [Range(0f, 1f), Tooltip("抗性比例：0 = 无效，1.0 = 完全免疫")]
    public float resistancePercent;
}

/// <summary>
/// 装备 ScriptableObject 基类（不可直接在编辑器创建，使用 WeaponData / ArmorData / AccessoryData 子类）
///
/// 属性叠加规则（线性加算，无百分比乘算）：
///   Final P.Atk = Base + Weapon.pAtk + Head.pAtk + Body.pAtk + Σ Accessory.pAtk
///   Final E.Atk = Base + Σ All Equipment.eAtk
///   Final P.Def = Base + Head.pDef + Body.pDef + Σ Accessory.pDef
///   Final E.Def = Base + Head.eDef + Body.eDef + Σ Accessory.eDef
/// </summary>
public abstract class EquipmentData : ScriptableObject
{
    [Header("基础信息")]
    public string equipmentName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("属性加成（线性加算）")]
    [Tooltip("物理攻击加成（武器主要属性）")]    public int pAtk;
    [Tooltip("属性攻击加成（饰品偶有）")]          public int eAtk;
    [Tooltip("物理防御加成（防具主要属性）")]      public int pDef;
    [Tooltip("属性防御加成（防具主要属性）")]      public int eDef;
    [Tooltip("速度加成")]                          public int speed;
    [Tooltip("命中加成（武器为主）")]              public int accuracy;
    [Tooltip("暴击加成（武器部分）")]              public int crit;
    [Tooltip("最大 HP 加成（饰品为主）")]          public int hp;
    [Tooltip("最大 SP 加成（饰品为主）")]          public int sp;

    [Header("状态抗性")]
    public List<StatusResistanceEntry> resistances = new List<StatusResistanceEntry>();

    /// <summary>
    /// 返回对指定状态的抗性比例（0~1）；存在多条同类型时取最大值。
    /// </summary>
    public float GetResistance(StatusEffectType type)
    {
        float max = 0f;
        foreach (var entry in resistances)
        {
            if (entry != null && entry.statusType == type && entry.resistancePercent > max)
                max = entry.resistancePercent;
        }
        return Mathf.Clamp01(max);
    }
}
