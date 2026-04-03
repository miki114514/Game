using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleStatusUIManager : MonoBehaviour
{
    [Header("UI容器")]
    public RectTransform container; // Canvas 下的空物体，用于垂直排列
    public GameObject playerStatusPrefab;

    private List<PlayerStatus> statusUIs = new List<PlayerStatus>();

    void Awake()
    {
        if (container == null)
            container = transform as RectTransform;
    }

    // 初始化战斗UI
    public void InitStatusUI(List<BattleUnit> players)
    {
        if (container == null)
        {
            Debug.LogError("[BattleStatusUIManager] 未配置 container，无法生成人物状态UI");
            return;
        }

        if (playerStatusPrefab == null)
        {
            Debug.LogError("[BattleStatusUIManager] 未配置 playerStatusPrefab，无法生成人物状态UI");
            return;
        }

        // 清理旧UI
        foreach (var ui in statusUIs)
        {
            if (ui != null) Destroy(ui.gameObject);
        }
        statusUIs.Clear();

        // 动态生成
        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("[BattleStatusUIManager] 玩家列表为空，未生成人物状态UI");
            return;
        }

        int count = Mathf.Min(players.Count, 4);
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(playerStatusPrefab, container);
            PlayerStatus ps = go.GetComponent<PlayerStatus>();
            if (ps == null)
            {
                Debug.LogError("[BattleStatusUIManager] playerStatusPrefab 缺少 PlayerStatus 组件");
                Destroy(go);
                continue;
            }

            ps.Init(players[i]);  // 公共初始化方法
            statusUIs.Add(ps);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(container);
    }
}