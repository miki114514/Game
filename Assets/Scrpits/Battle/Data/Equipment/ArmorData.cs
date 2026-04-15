using UnityEngine;

/// <summary>
/// 防具装备槽位
/// </summary>
public enum ArmorSlot
{
    Head,   // 头部防具（帽子、头盔…）
    Body    // 身体防具（铠甲、长袍…）
}

/// <summary>
/// 防具装备数据（Head / Body 两槽）
///
/// 典型属性分布：
///  Head / Body → 主提供 pDef、eDef，少量 speed
///  特殊防具可提供少量 pAtk / eAtk（交由设计师自由填值）
/// </summary>
[CreateAssetMenu(fileName = "NewArmor", menuName = "Equipment/Armor")]
public class ArmorData : EquipmentData
{
    [Header("防具属性")]
    [Tooltip("防具槽位：Head（头部）或 Body（身体）")]
    public ArmorSlot slot = ArmorSlot.Body;

    // 典型有效字段：pDef, eDef, speed
    // pAtk / eAtk / crit / hp / sp 通常保持 0（少数特殊防具可填）
}
