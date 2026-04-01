using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;   // 单例

    public List<InventoryItem> items = new List<InventoryItem>();

    void Awake()
    {
        Instance = this;
    }

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
}