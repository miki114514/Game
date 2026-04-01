using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DialogueBubbleController : MonoBehaviour
{
    [Header("UI组件")]
    public RectTransform bubbleRect;
    public RectTransform cRect;
    public TextMeshProUGUI text;
    public RectTransform tailRect;

    [Header("完成提示图标")]
    public Image nextIcon;

    [Header("气泡大小")]
    public float horizontalPadding = 20f;
    public float verticalPadding = 20f;

    [Header("尾巴设置")]
    public Image tailImage;
    public TailManager tailManager;

    public BubbleType bubbleType = BubbleType.Default;
    public TailDirection tailDirection = TailDirection.Down;

    [Header("尾巴方向微调")]
    public Vector2 offsetDown = Vector2.zero;
    public Vector2 offsetDownLeft = Vector2.zero;
    public Vector2 offsetUpLeft = Vector2.zero;
    public Vector2 offsetDownRight = Vector2.zero;
    public Vector2 offsetUpRight = Vector2.zero;

    private RectTransform rectTransform;

    private Transform target;
    private Vector3 targetOffset;

    private CanvasGroup canvasGroup;
    private Coroutine typingCoroutine;

    public AudioSource audioSource;

    [Range(0f, 1f)]
    public float voiceVolume = 1f;

    private string fullTextCache;

    private bool isTyping = false;
    private bool isComplete = false;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rectTransform = GetComponent<RectTransform>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = voiceVolume;
        }

        if (nextIcon != null)
            nextIcon.gameObject.SetActive(false);

        Hide();
    }

    void Update()
    {
        if (target != null)
        {
            Vector3 worldPos = target.position + targetOffset;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            bubbleRect.position = screenPos;
        }
    }

    public bool IsTyping()
    {
        return isTyping;
    }

    public bool IsComplete()
    {
        return isComplete;
    }

    public void SetBubbleType(BubbleType type)
    {
        bubbleType = type;
        UpdateTailVisual();
    }

    public void SetTailDirection(TailDirection dir)
    {
        tailDirection = dir;
        UpdateTailVisual();
    }

    private void UpdateBubbleSize()
    {
        Canvas.ForceUpdateCanvases();

        Vector2 size = text.GetPreferredValues(text.text);

        float width = size.x + horizontalPadding;
        float height = size.y + verticalPadding;

        bubbleRect.sizeDelta = new Vector2(width, height);
    }

    public void SetText(DialogueLine line)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        fullTextCache = line.text;

        SetBubbleType(line.bubbleType);

        Vector2 size = text.GetPreferredValues(line.text);

        float width = size.x + horizontalPadding;
        float height = size.y + verticalPadding;

        bubbleRect.sizeDelta = new Vector2(width, height);

        UpdateTailVisual();

        if (nextIcon != null)
            nextIcon.gameObject.SetActive(false);

        if (line.voiceClip != null && audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = line.voiceClip;
            audioSource.volume = voiceVolume;
            audioSource.Play();
        }

        typingCoroutine = StartCoroutine(TypeTextCoroutine(line.text, line.typewriterDuration));
    }

    private IEnumerator TypeTextCoroutine(string fullText, float duration)
    {
        text.text = "";

        isTyping = true;
        isComplete = false;

        if (string.IsNullOrEmpty(fullText))
            yield break;

        int totalChars = fullText.Length;
        float interval = duration / totalChars;

        for (int i = 0; i < totalChars; i++)
        {
            text.text += fullText[i];
            yield return new WaitForSeconds(interval);
        }

        text.text = fullText;

        isTyping = false;
        isComplete = true;

        if (nextIcon != null)
            nextIcon.gameObject.SetActive(true);
    }

    public void SkipTyping()
    {
        if (!isTyping) return;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        text.text = fullTextCache;

        UpdateBubbleSize();
        UpdateTailVisual();

        isTyping = false;
        isComplete = true;

        if (nextIcon != null)
            nextIcon.gameObject.SetActive(true);

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    public void FollowTarget(Transform anchor, Vector3 offset)
    {
        target = anchor;
        targetOffset = offset;
    }

    private void UpdateTailVisual()
    {
        if (tailManager == null || tailImage == null || tailRect == null)
            return;

        TailSpriteInfo tailInfo = tailManager.tailSprites.Find(
            t => t.type == bubbleType && t.direction == tailDirection
        );
        

        tailImage.sprite = tailInfo.sprite;
        tailImage.SetNativeSize();

        Vector2 offset = Vector2.zero;

        float halfWidth = bubbleRect.sizeDelta.x / 2f;
        float halfHeight = bubbleRect.sizeDelta.y / 2f;

        float tailHalfW = tailImage.rectTransform.sizeDelta.x / 2f;
        float tailHalfH = tailImage.rectTransform.sizeDelta.y / 2f;

        switch (tailDirection)
        {
            case TailDirection.Down:
                offset = new Vector2(0, -halfHeight - tailHalfH) + offsetDown;
                break;

            case TailDirection.DownLeft:
                offset = new Vector2(-halfWidth - tailHalfW, -halfHeight - tailHalfH) + offsetDownLeft;
                break;

            case TailDirection.UpLeft:
                offset = new Vector2(-halfWidth - tailHalfW, halfHeight + tailHalfH) + offsetUpLeft;
                break;

            case TailDirection.DownRight:
                offset = new Vector2(halfWidth + tailHalfW, -halfHeight - tailHalfH) + offsetDownRight;
                break;

            case TailDirection.UpRight:
                offset = new Vector2(halfWidth + tailHalfW, halfHeight + tailHalfH) + offsetUpRight;
                break;
        }

        tailRect.anchoredPosition = offset;
    }

    public void Show()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}