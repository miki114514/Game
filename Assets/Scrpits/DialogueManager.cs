using UnityEngine;
using System.Collections.Generic;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI")]
    public Canvas uiCanvas;

    [Header("Bubble Prefabs")]
    public GameObject defaultBubble;
    public GameObject shoutBubble;
    public GameObject thinkBubble;

    [Header("打字机跳过键")]
    public KeyCode skipKey = KeyCode.Space;

    private Dictionary<GameObject, GameObject> activeBubbles =
        new Dictionary<GameObject, GameObject>();

    // ★记录角色原动画
    private Dictionary<GameObject, string> originalAnimations =
        new Dictionary<GameObject, string>();

    // ★记录当前播放的对话动画
    private Dictionary<GameObject, string> playingDialogueAnimations =
        new Dictionary<GameObject, string>();

    private DialogueSequence currentSequence;
    private int dialogueIndex = -1;

    // ★玩家引用
    private PlayerController player;

    void Awake()
    {
        Instance = this;

        // ★ 玩家移动控制：获取玩家
        player = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        if (currentSequence == null) return;

        if (Input.GetKeyDown(skipKey))
        {
            DialogueLine currentLine = currentSequence.lines[dialogueIndex];

            GameObject character = GameObject.Find(currentLine.characterName);

            if (character != null && activeBubbles.ContainsKey(character))
            {
                DialogueBubbleController bubbleCtrl =
                    activeBubbles[character].GetComponent<DialogueBubbleController>();

                if (bubbleCtrl == null) return;

                // 如果正在逐字 → 跳过逐字显示
                if (bubbleCtrl.IsTyping())
                {
                    bubbleCtrl.SkipTyping();

                    // ★动画直接跳到最后一帧
                    Animator animator = character.GetComponent<Animator>();
                    if (animator != null && playingDialogueAnimations.ContainsKey(character))
                    {
                        string animName = playingDialogueAnimations[character];
                        animator.Play(animName, 0, 1f);
                    }

                    return;
                }

                // 如果文本已完整 → 下一句
                if (bubbleCtrl.IsComplete())
                {
                    NextDialogue();
                }
            }
        }
    }

    // -------------------- 开始对话 --------------------
    public void StartDialogue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.lines == null || sequence.lines.Length == 0)
        {
            Debug.LogWarning("DialogueSequence为空或没有行！");
            return;
        }

        Debug.Log($"DialogueManager 开始执行对话，行数：{sequence.lines.Length}");

        // ★ 玩家移动控制：锁定移动
        if (player != null)
        {
            player.canMove = false;
        }

        currentSequence = sequence;
        dialogueIndex = -1;

        NextDialogue();
    }

    // -------------------- 播放下一句 --------------------
    void NextDialogue()
    {
        // ★恢复角色动画
        RestoreCharactersAnimation();

        dialogueIndex++;

        if (currentSequence == null || dialogueIndex >= currentSequence.lines.Length)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = currentSequence.lines[dialogueIndex];
        ShowDialogue(line);
    }

    // -------------------- 显示对话 --------------------
    public void ShowDialogue(DialogueLine line)
    {
        if (line == null)
        {
            Debug.LogError("DialogueLine为空！");
            return;
        }

        // 查找角色
        GameObject character = GameObject.Find(line.characterName);
        if (character == null)
        {
            Debug.LogError("找不到角色: " + line.characterName);
            return;
        }

        // ★播放角色动画
        Animator animator = character.GetComponent<Animator>();
        if (animator != null && !string.IsNullOrEmpty(line.animationName))
        {
            // 记录角色原动画（只记录一次）
            if (!originalAnimations.ContainsKey(character))
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                originalAnimations[character] = state.shortNameHash.ToString();
            }

            playingDialogueAnimations[character] = line.animationName;

            animator.Play(line.animationName, 0, 0f);
        }

        // 查找 Anchor
        Transform anchor = character.transform.Find("Anchor");
        if (anchor == null)
        {
            Debug.LogError("角色没有 Anchor！");
            return;
        }

        // 如果角色已有气泡 → 删除
        if (activeBubbles.ContainsKey(character))
        {
            Destroy(activeBubbles[character]);
        }

        // 创建对应类型的气泡
        GameObject prefab = GetBubblePrefab(line.bubbleType);
        GameObject bubbleObj = Instantiate(prefab, uiCanvas.transform);

        bubbleObj.SetActive(true);

        DialogueBubbleController bubbleCtrl = bubbleObj.GetComponent<DialogueBubbleController>();
        if (bubbleCtrl == null)
        {
            Debug.LogError("气泡预制体缺少 DialogueBubbleController！");
            return;
        }

        // 显示气泡
        bubbleCtrl.Show();

        // 跟随角色
        bubbleCtrl.FollowTarget(anchor, line.offset);

        // 自动判断尾巴方向
        TailDirection dir = GetDirectionFromOffset(line.offset);
        bubbleCtrl.SetTailDirection(dir);

        // 设置文字 + 音频 + 打字机
        bubbleCtrl.SetText(line);

        activeBubbles[character] = bubbleObj;

        Debug.Log($"✅ 气泡已生成：{bubbleObj.name}，父对象：{bubbleObj.transform.parent.name}");
    }

    // -------------------- 获取气泡Prefab --------------------
    GameObject GetBubblePrefab(BubbleType type)
    {
        switch (type)
        {
            case BubbleType.Shout:
                return shoutBubble;

            case BubbleType.Think:
                return thinkBubble;

            default:
                return defaultBubble;
        }
    }

    // -------------------- 恢复角色动画 --------------------
    void RestoreCharactersAnimation()
    {
        foreach (var pair in playingDialogueAnimations)
        {
            GameObject character = pair.Key;

            if (character == null) continue;

            Animator animator = character.GetComponent<Animator>();

            if (animator != null && originalAnimations.ContainsKey(character))
            {
                string originalAnimHash = originalAnimations[character];

                if (!string.IsNullOrEmpty(originalAnimHash))
                {
                    int hash = int.Parse(originalAnimHash);
                    animator.Play(hash);
                }
            }
        }

        playingDialogueAnimations.Clear();
    }

    // -------------------- 结束对话 --------------------
    void EndDialogue()
    {
        // ★恢复动画
        RestoreCharactersAnimation();

        // ★ 玩家移动控制：恢复移动
        if (player != null)
        {
            player.canMove = true;
        }

        foreach (var bubble in activeBubbles.Values)
        {
            Destroy(bubble);
        }

        activeBubbles.Clear();
        currentSequence = null;
        dialogueIndex = -1;
    }

    // -------------------- 根据offset自动判断尾巴方向 --------------------
    TailDirection GetDirectionFromOffset(Vector3 offset)
    {
        float x = offset.x;
        float y = offset.y;

        if (x > 0 && y > 0) return TailDirection.DownLeft;
        if (x < 0 && y > 0) return TailDirection.DownRight;
        if (x > 0 && y < 0) return TailDirection.UpLeft;
        if (x < 0 && y < 0) return TailDirection.UpRight;

        return TailDirection.Down;
    }
}