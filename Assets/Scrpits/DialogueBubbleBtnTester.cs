using UnityEngine;

public class DialogueBubbleBtnTester : MonoBehaviour
{
    public DialogueBubbleController bubble;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ShowTestBubble("Default, Down", BubbleType.Default, TailDirection.Down);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ShowTestBubble("Default, DownLeft", BubbleType.Default, TailDirection.DownLeft);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ShowTestBubble("Default, UpLeft", BubbleType.Default, TailDirection.UpLeft);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ShowTestBubble("Default, DownRight", BubbleType.Default, TailDirection.DownRight);
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            ShowTestBubble("Default, UpRight", BubbleType.Default, TailDirection.UpRight);
        }
    }

    // -------------------- 新增辅助方法 --------------------
    void ShowTestBubble(string content, BubbleType type, TailDirection dir)
    {
        // 设置气泡类型和尾巴方向
        bubble.bubbleType = type;
        bubble.tailDirection = dir;

        // 创建 DialogueLine 对象
        DialogueLine line = new DialogueLine
        {
            characterName = "Tester",       // 这里随意填名字，主要是演示
            text = content,
            bubbleType = type,
            offset = Vector3.zero,
            typewriterDuration = 2f          // 可以改成你希望的显示时间
        };

        // 使用 SetText(DiaogueLine)
        bubble.SetText(line);
    }
}