using System.Collections.Generic;
using UnityEngine;

public class BattleStatusUIManager : MonoBehaviour
{
    [Header("UI容器")]
    public RectTransform container; // Canvas 下的空物体，用于垂直排列
    public GameObject playerStatusPrefab;

    private List<PlayerStatus> statusUIs = new List<PlayerStatus>();

    // 初始化战斗UI
    public void InitStatusUI(List<BattleUnit> players)
    {
        // 清理旧UI
        foreach (var ui in statusUIs)
        {
            if (ui != null) Destroy(ui.gameObject);
        }
        statusUIs.Clear();

        // 动态生成
        for (int i = 0; i < players.Count; i++)
        {
            GameObject go = Instantiate(playerStatusPrefab, container);
            PlayerStatus ps = go.GetComponent<PlayerStatus>();
            ps.Init(players[i]);  // 公共初始化方法
            statusUIs.Add(ps);
        }
    }
}