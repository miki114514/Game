using UnityEngine;
using UnityEngine.UI;

public class ArrowBlink : MonoBehaviour
{
    public float blinkSpeed = 2f;
    public float minAlpha = 0.3f;
    public float maxAlpha = 1f;

    private Image img;
    private Color baseColor;

    void Awake()
    {
        // 在自己或子物体中查找 Image
        img = GetComponentInChildren<Image>();

        if (img == null)
        {
            Debug.LogError("ArrowBlink 找不到 Image 组件！");
            enabled = false;
            return;
        }

        baseColor = img.color;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        Color c = baseColor;
        c.a = alpha;
        img.color = c;
    }
}