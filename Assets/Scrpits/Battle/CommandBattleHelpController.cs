using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class CommandBattleHelpController : MonoBehaviour
{
    [Header("UI组件")]
    public GameObject CommandBattleHelp;  // 指令详情面板节点本身
    public TextMeshProUGUI Txt_Desc;      // 描述文本
    public UnityEngine.UI.Image BgLeft;   // 左侧底图
    public UnityEngine.UI.Image BgRight;  // 右侧底图

    [Header("指令数据")]
    public List<CommandDetail> commandDetails; // 在Inspector里配置数据
    private Dictionary<string, CommandDetail> commandDict;

    [System.Serializable]
    public class CommandDetail
    {
        public string characterName;
        public string commandName;
        [TextArea(2,5)]
        public string description;
    }

    void Awake()
    {
        // 初始化字典
        commandDict = new Dictionary<string, CommandDetail>();
        foreach(var detail in commandDetails)
        {
            string key = detail.characterName + "_" + detail.commandName;
            commandDict[key] = detail;
        }

        // 默认隐藏面板
        if(CommandBattleHelp != null)
            CommandBattleHelp.SetActive(false);
    }

    // 更新显示内容
    public void ShowCommandDetail(string commandName, string characterName)
    {
        string key = characterName + "_" + commandName;
        if(commandDict.TryGetValue(key, out var detail))
        {
            Txt_Desc.text = detail.description;
            CommandBattleHelp.SetActive(true);
        }
        else
        {
            Txt_Desc.text = "暂无信息";
            CommandBattleHelp.SetActive(false);
        }
    }

    // 隐藏面板
    public void HideCommandDetail()
    {
        CommandBattleHelp.SetActive(false);
    }
}