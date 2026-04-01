using UnityEngine;

public class DialogueBubbleTester : MonoBehaviour
{
    public TailManager tailManager;   // 你在场景里的 TailManager
    public RectTransform tailRect;    // 气泡上的 Tail Image RectTransform
    public UnityEngine.UI.Image tailImage;

    // 测试用，选择尾巴方向
    public TailDirection testDirection = TailDirection.Down;
    public BubbleType bubbleType = BubbleType.Default;

    void Start()
    {
        ApplyTail();
    }

    void ApplyTail()
    {
        // 找到对应 TailSpriteInfo
        TailSpriteInfo info = tailManager.tailSprites.Find(t => 
            t.direction == testDirection && t.type == bubbleType);

        if (info.sprite != null)
        {
            tailImage.sprite = info.sprite;
            tailImage.SetNativeSize(); // 可选，让尾巴恢复贴图原始大小
        }

        // 这里你可以加一个简单偏移，如果 Inspector 没调好
        tailRect.anchoredPosition = Vector2.zero; 
    }
}