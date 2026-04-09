public enum SubCommandEntryType
{
    Arts,
    CharacterSkill,
    Item,
    Confirm
}

public class SubCommand
{
    public string name;       // UI显示
    public Skill skill;       // 技能
    public Item item;         // 道具
    public bool isConfirm;    // 确认类选项
    public SubCommandEntryType entryType;

    public bool ShouldShowBoostTag => entryType == SubCommandEntryType.Arts;

    public SubCommand(
        string name,
        Skill skill = null,
        Item item = null,
        bool isConfirm = false,
        SubCommandEntryType entryType = SubCommandEntryType.Arts)
    {
        this.name = name;
        this.skill = skill;
        this.item = item;
        this.isConfirm = isConfirm;
        this.entryType = entryType;
    }
}