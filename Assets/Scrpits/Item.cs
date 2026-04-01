using UnityEngine;

public enum ItemType
{
    Heal,
    Mana,
    Revive,
    Buff,
    Attack
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Item/ItemCeate")]
public class Item : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public ItemType itemType;
    public int value;
    public string description;

    // 新增：道具使用效果
    public void Use(BattleUnit user, BattleUnit target)
    {
        switch (itemType)
        {
            case ItemType.Heal:
                target.currentHP += value;
                if (target.currentHP > target.maxHP)
                    target.currentHP = target.maxHP;

                Debug.Log(target.unitName + " 恢复HP " + value);
                break;

            case ItemType.Mana:
                target.currentSP += value;
                if (target.currentSP > target.maxSP)
                    target.currentSP = target.maxSP;

                Debug.Log(target.unitName + " 恢复SP " + value);
                break;

            case ItemType.Revive:
                if (target.currentHP <= 0)
                {
                    target.currentHP = value;
                    Debug.Log(target.unitName + " 被复活");
                }
                break;

            case ItemType.Attack:
                target.TakeDamage(value);
                Debug.Log(target.unitName + " 受到道具伤害 " + value);
                break;
        }
    }
}