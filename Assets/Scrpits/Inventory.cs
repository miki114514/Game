using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;   // 单例

    public List<InventoryItem> items = new List<InventoryItem>();

    // ────────────────────────────────────────────────────────────────
    // 装备背包（武器 / 防具 / 饰品共用同一列表，通过类型区分）
    // EquipmentData 是抽象基类，实际存储 WeaponData / ArmorData / AccessoryData
    // ────────────────────────────────────────────────────────────────
    public List<EquipmentData> equipments = new List<EquipmentData>();

    void Awake()
    {
        Instance = this;
    }

    // ────────────────────────────────────────────────────────────────
    // 道具操作
    // ────────────────────────────────────────────────────────────────

    // 添加道具
    public void AddItem(Item item, int amount)
    {
        InventoryItem found = items.Find(i => i.item == item);
        if (found != null)
        {
            found.amount += amount;
        }
        else
        {
            items.Add(new InventoryItem(item, amount));
        }
    }

    // 移除道具
    public void RemoveItem(Item item, int amount)
    {
        InventoryItem found = items.Find(i => i.item == item);
        if (found != null)
        {
            found.amount -= amount;
            if (found.amount <= 0)
                items.Remove(found);
        }
    }

    // 给战斗系统用：获取所有可用道具
    public List<Item> GetAllItems()
    {
        List<Item> list = new List<Item>();
        foreach (var invItem in items)
        {
            list.Add(invItem.item);
        }
        return list;
    }

    // 获取数量（以后UI会用）
    public int GetItemAmount(Item item)
    {
        InventoryItem found = items.Find(i => i.item == item);
        if (found != null)
            return found.amount;
        return 0;
    }

    // ────────────────────────────────────────────────────────────────
    // 装备操作
    // ────────────────────────────────────────────────────────────────

    /// <summary>将一件装备加入背包（同一 ScriptableObject 允许持有多份）</summary>
    public void AddEquipment(EquipmentData equipment)
    {
        if (equipment == null) return;
        equipments.Add(equipment);
    }

    /// <summary>从背包移除一件装备（只移除第一个匹配项）</summary>
    public void RemoveEquipment(EquipmentData equipment)
    {
        if (equipment == null) return;
        equipments.Remove(equipment);
    }

    /// <summary>背包中是否至少持有一件指定装备</summary>
    public bool HasEquipment(EquipmentData equipment)
    {
        return equipment != null && equipments.Contains(equipment);
    }

    /// <summary>获取背包中所有装备（只读副本）</summary>
    public List<EquipmentData> GetAllEquipments()
    {
        return new List<EquipmentData>(equipments);
    }

    /// <summary>获取背包中所有武器</summary>
    public List<WeaponData> GetAllWeapons()
    {
        var result = new List<WeaponData>();
        foreach (var e in equipments)
            if (e is WeaponData w) result.Add(w);
        return result;
    }

    /// <summary>获取背包中所有防具（可按槽位过滤）</summary>
    public List<ArmorData> GetAllArmors(ArmorSlot? filterSlot = null)
    {
        var result = new List<ArmorData>();
        foreach (var e in equipments)
        {
            if (e is ArmorData a && (filterSlot == null || a.slot == filterSlot.Value))
                result.Add(a);
        }
        return result;
    }

    /// <summary>获取背包中所有饰品</summary>
    public List<AccessoryData> GetAllAccessories()
    {
        var result = new List<AccessoryData>();
        foreach (var e in equipments)
            if (e is AccessoryData acc) result.Add(acc);
        return result;
    }

    /// <summary>
    /// 为指定战斗单位装备武器（自动从背包中消耗，并将旧武器归还背包）
    /// </summary>
    public bool TryEquipWeapon(BattleUnit unit, WeaponData weapon)
    {
        if (unit == null || weapon == null) return false;
        if (!HasEquipment(weapon)) return false;

        if (unit.equippedWeapon != null)
            AddEquipment(unit.equippedWeapon);

        RemoveEquipment(weapon);
        unit.EquipWeapon(weapon);
        return true;
    }

    /// <summary>
    /// 为指定战斗单位装备防具（自动从背包中消耗，并将旧防具归还背包）
    /// </summary>
    public bool TryEquipArmor(BattleUnit unit, ArmorData armor)
    {
        if (unit == null || armor == null) return false;
        if (!HasEquipment(armor)) return false;

        ArmorData oldArmor = armor.slot == ArmorSlot.Head ? unit.equippedHead : unit.equippedBody;
        if (oldArmor != null)
            AddEquipment(oldArmor);

        RemoveEquipment(armor);
        unit.EquipArmor(armor);
        return true;
    }

    /// <summary>
    /// 为指定战斗单位装备饰品到指定槽位（自动从背包中消耗，并将旧饰品归还背包）
    /// </summary>
    public bool TryEquipAccessory(BattleUnit unit, AccessoryData accessory, int slot = 0)
    {
        if (unit == null || accessory == null) return false;
        if (!HasEquipment(accessory)) return false;

        int safeSlot = Mathf.Clamp(slot, 0, unit.equippedAccessories.Length - 1);
        AccessoryData oldAcc = unit.equippedAccessories[safeSlot];
        if (oldAcc != null)
            AddEquipment(oldAcc);

        RemoveEquipment(accessory);
        unit.EquipAccessory(accessory, safeSlot);
        return true;
    }
}