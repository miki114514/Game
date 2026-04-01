using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CreateDialogueBubbleUI : MonoBehaviour
{
    [ContextMenu("Create Dialogue Bubble")]
    void CreateBubble()
    {
        // 父对象
        GameObject bubble = new GameObject("DialogueBubble", typeof(RectTransform));
        bubble.transform.SetParent(this.transform, false);

        // 所有九宫格部分
        string[] parts = { "TL", "T", "TR", "L", "C", "R", "BL", "B", "BR", "Tail" };

        foreach (string part in parts)
        {
            GameObject go = new GameObject(part, typeof(RectTransform));
            go.transform.SetParent(bubble.transform, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.5f); // 临时颜色
        }

        // Text
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(bubble.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Hello!";
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;

        // 可选：自动调整 RectTransform
        RectTransform bubbleRT = bubble.GetComponent<RectTransform>();
        bubbleRT.sizeDelta = new Vector2(200, 100);

        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0, 0);
        textRT.anchorMax = new Vector2(1, 1);
        textRT.offsetMin = new Vector2(10, 10);
        textRT.offsetMax = new Vector2(-10, -10);

        Debug.Log("DialogueBubble UI created!");
    }
}