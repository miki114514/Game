using UnityEngine;

public class DialogueTest : MonoBehaviour
{
    [Header("测试对象")]
    public GameObject npc;              
    public Canvas uiCanvas;             

    [Header("气泡 Prefab")]
    public GameObject defaultBubblePrefab;
    public GameObject shoutBubblePrefab;
    public GameObject thinkBubblePrefab;

    [Header("气泡偏移")]
    public Vector3 bubbleOffset = new Vector3(-0.5f, 1.2f, 0);

    void Update()
    {
        // 1 Default
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ShowTestBubble(defaultBubblePrefab, "你好！冒险者", BubbleType.Default);
        }

        // 2 Shout
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ShowTestBubble(shoutBubblePrefab, "喂！！！冒险者！！！", BubbleType.Shout);
        }

        // 3 Think
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ShowTestBubble(thinkBubblePrefab, "嗯……让我想想……", BubbleType.Think);
        }
    }

    void ShowTestBubble(GameObject bubblePrefab, string text, BubbleType type)
    {
        if (npc == null || bubblePrefab == null || uiCanvas == null)
        {
            Debug.LogError("请确保 npc、bubblePrefab、uiCanvas 已赋值！");
            return;
        }

        // 找角色 Anchor
        Transform anchor = npc.transform.Find("Anchor");

        if (anchor == null)
        {
            Debug.LogError("角色没有 Anchor！请在角色下创建 Anchor 空对象");
            return;
        }

        // 创建气泡
        GameObject bubbleObj = Instantiate(bubblePrefab, uiCanvas.transform);

        DialogueBubbleController bubbleCtrl =
            bubbleObj.GetComponent<DialogueBubbleController>();

        if (bubbleCtrl == null)
        {
            Debug.LogError("气泡预制体缺少 DialogueBubbleController！");
            return;
        }

        // ✅ 创建 DialogueLine 对象并传入
        DialogueLine line = new DialogueLine
        {
            characterName = npc.name,
            text = text,
            bubbleType = type,
            offset = bubbleOffset,
            typewriterDuration = 2f // 可根据需要修改时间
        };

        bubbleCtrl.SetText(line);

        // 设置跟随
        bubbleCtrl.FollowTarget(anchor, bubbleOffset);

        // 自动判断尾巴方向
        TailDirection dir = GetDirectionFromOffset(bubbleOffset);
        bubbleCtrl.SetTailDirection(dir);

        // 显示
        bubbleCtrl.Show();
    }

    // 根据 offset 自动判断尾巴方向
    TailDirection GetDirectionFromOffset(Vector3 offset)
    {
        float x = offset.x;
        float y = offset.y;

        if (x > 0 && y > 0)
            return TailDirection.DownLeft;

        if (x < 0 && y > 0)
            return TailDirection.DownRight;

        if (x > 0 && y < 0)
            return TailDirection.UpLeft;

        if (x < 0 && y < 0)
            return TailDirection.UpRight;

        return TailDirection.Down;
    }
}