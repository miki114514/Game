using UnityEngine;
using System.Collections.Generic;
using System;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    public static bool IsDialogueActive => Instance != null && Instance.currentSequence != null;

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

    // 记录角色位移协程，确保同一角色的新位移会覆盖旧位移
    private Dictionary<GameObject, Coroutine> playingMovementCoroutines =
        new Dictionary<GameObject, Coroutine>();

    // 记录每个角色最后朝向，和玩家移动动画逻辑保持一致
    private Dictionary<GameObject, Vector2> lastFacingDirections =
        new Dictionary<GameObject, Vector2>();

    private struct MovementAnimatorParamSupport
    {
        public bool hasMoveX;
        public bool hasMoveY;
        public bool hasSpeed;
        public bool hasIsRunning;
    }

    private Dictionary<Animator, MovementAnimatorParamSupport> movementAnimatorParamCache =
        new Dictionary<Animator, MovementAnimatorParamSupport>();

    private DialogueSequence currentSequence;
    private int dialogueIndex = -1;
    private int dialogueLineToken = 0;

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
            bool currentLineShouldShowBubble = ShouldShowBubble(currentLine);

            if (!currentLineShouldShowBubble)
            {
                NextDialogue();
                return;
            }

            GameObject character = FindCharacterByName(currentLine.characterName);

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
        // 执行上一句设置为“句后”的角色显隐。
        if (currentSequence != null && dialogueIndex >= 0 && dialogueIndex < currentSequence.lines.Length)
        {
            ApplyVisibilityChanges(currentSequence.lines[dialogueIndex], DialogueVisibilityTiming.AfterLine);
        }

        // 进入下一句前，先稳定清理上一句遗留气泡。
        ClearAllActiveBubbles();

        dialogueIndex++;
        dialogueLineToken++;

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

        ApplyVisibilityChanges(line, DialogueVisibilityTiming.BeforeLine);

        // 查找角色
        GameObject character = FindCharacterByName(line.characterName);
        if (character == null)
        {
            Debug.LogError("找不到角色: " + line.characterName);
            NextDialogue();
            return;
        }

        // 进入新句时，先取消该角色上一句可能仍在运行的位移协程，避免旧回调污染当前句。
        StopMovementCoroutine(character);

        Animator animator = character.GetComponent<Animator>();
        int expectedDialogueIndex = dialogueIndex;
        int expectedLineToken = dialogueLineToken;
        bool shouldShowBubble = ShouldShowBubble(line);

        bool shouldDelayBubbleAndVoice =
            shouldShowBubble && line.enableMovement && line.showBubbleAndVoiceAfterMovement;

        Action onMovementCompleted = null;
        if (shouldDelayBubbleAndVoice)
        {
            onMovementCompleted = () =>
            {
                bool stillSameLine =
                    currentSequence != null &&
                    dialogueIndex == expectedDialogueIndex &&
                    dialogueLineToken == expectedLineToken;

                if (stillSameLine)
                    ShowBubbleForLine(line, character);
            };
        }

        // 有移动时：先移动（播移动动画），移动结束后再播放指定动画。
        // 无移动时：直接播放指定动画。
        TryMoveCharacter(line, character, animator, onMovementCompleted);

        if (!line.enableMovement)
        {
            PlayDialogueAnimation(character, animator, line.animationName);
        }

        if (shouldShowBubble && !shouldDelayBubbleAndVoice)
        {
            ShowBubbleForLine(line, character);
        }
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

        StopAllMovementCoroutines();

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

    void TryMoveCharacter(DialogueLine line, GameObject character, Animator animator, Action onMovementCompleted)
    {
        if (line == null || character == null || !line.enableMovement)
            return;

        Transform characterTransform = character.transform;
        Vector3 startPos = characterTransform.position;
        Vector3 targetPos = line.useRelativeMovement
            ? startPos + line.moveOffset
            : line.moveTargetWorldPosition;

        if (!line.useRelativeMovement &&
            line.moveTargetWorldPosition == Vector3.zero &&
            line.moveOffset != Vector3.zero)
        {
            targetPos = startPos + line.moveOffset;
            Debug.LogWarning($"{line.characterName} 当前句使用绝对位移，但 Move Target World Position 为 (0,0,0) 且 Move Offset 不为 0。已自动回退为相对位移，避免角色意外回到世界原点。\n如果你需要绝对位移，请填写有效世界坐标。\n");
        }

        float duration = Mathf.Max(0f, line.moveDuration);

        if (playingMovementCoroutines.TryGetValue(character, out Coroutine running))
        {
            if (running != null)
                StopCoroutine(running);

            playingMovementCoroutines.Remove(character);
        }

        Vector3 delta = targetPos - startPos;
        bool useDialogueAnimationDuringMovement =
            line.useDialogueAnimationDuringMovement && !string.IsNullOrEmpty(line.animationName);

        if (line.useDialogueAnimationDuringMovement && string.IsNullOrEmpty(line.animationName))
        {
            Debug.LogWarning($"{line.characterName} 启用了 useDialogueAnimationDuringMovement，但 Animation Name 为空，将回退为移动动画参数驱动。\n请在该句填写 Animation Name。\n");
        }

        // 参数驱动移动前，先释放该角色上一句的对话动画覆盖，避免旧固定状态吞掉跑步/行走动画。
        if (!useDialogueAnimationDuringMovement)
        {
            ReleaseDialogueAnimationOverride(character, animator);
        }

        if (duration <= 0f)
        {
            characterTransform.position = targetPos;
            ApplyMovementAnimation(animator, delta, false, character, line.moveStyle);
            PlayDialogueAnimation(character, animator, line.animationName);
            onMovementCompleted?.Invoke();
            return;
        }

        Coroutine moveCoroutine = StartCoroutine(
            MoveCharacterRoutine(
                character,
                animator,
                targetPos,
                duration,
                line.animationName,
                line.moveStyle,
                useDialogueAnimationDuringMovement,
                onMovementCompleted
            )
        );
        playingMovementCoroutines[character] = moveCoroutine;
    }

    System.Collections.IEnumerator MoveCharacterRoutine(
        GameObject character,
        Animator animator,
        Vector3 targetPos,
        float duration,
        string pendingDialogueAnimation,
        DialogueMoveStyle moveStyle,
        bool useDialogueAnimationDuringMovement,
        Action onMovementCompleted
    )
    {
        if (character == null)
            yield break;

        Transform characterTransform = character.transform;
        Vector3 startPos = characterTransform.position;
        Vector3 moveDelta = targetPos - startPos;

        if (useDialogueAnimationDuringMovement)
        {
            PlayDialogueAnimation(character, animator, pendingDialogueAnimation);
        }
        else
        {
            ApplyMovementAnimation(animator, moveDelta, true, character, moveStyle);
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (character == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            characterTransform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        if (character != null)
            characterTransform.position = targetPos;

        if (!useDialogueAnimationDuringMovement)
        {
            ApplyMovementAnimation(animator, moveDelta, false, character, moveStyle);
        }

        playingMovementCoroutines.Remove(character);

        if (character != null && !useDialogueAnimationDuringMovement)
            PlayDialogueAnimation(character, animator, pendingDialogueAnimation);

        onMovementCompleted?.Invoke();
    }

    void ShowBubbleForLine(DialogueLine line, GameObject character)
    {
        if (line == null || character == null)
            return;

        Transform anchor = character.transform.Find("Anchor");
        if (anchor == null)
        {
            Debug.LogError("角色没有 Anchor！");
            return;
        }

        if (activeBubbles.ContainsKey(character))
        {
            Destroy(activeBubbles[character]);
        }

        GameObject prefab = GetBubblePrefab(line.bubbleType);
        GameObject bubbleObj = Instantiate(prefab, uiCanvas.transform);
        bubbleObj.SetActive(true);

        DialogueBubbleController bubbleCtrl = bubbleObj.GetComponent<DialogueBubbleController>();
        if (bubbleCtrl == null)
        {
            Debug.LogError("气泡预制体缺少 DialogueBubbleController！");
            return;
        }

        bubbleCtrl.Show();
        bubbleCtrl.FollowTarget(anchor, line.offset);

        TailDirection dir = GetDirectionFromOffset(line.offset);
        bubbleCtrl.SetTailDirection(dir);

        bubbleCtrl.SetText(line);

        activeBubbles[character] = bubbleObj;

        Debug.Log($"✅ 气泡已生成：{bubbleObj.name}，父对象：{bubbleObj.transform.parent.name}");
    }

    void StopAllMovementCoroutines()
    {
        foreach (var pair in playingMovementCoroutines)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);
        }

        playingMovementCoroutines.Clear();
    }

    void ClearAllActiveBubbles()
    {
        foreach (var pair in activeBubbles)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }

        activeBubbles.Clear();
    }

    void StopMovementCoroutine(GameObject character)
    {
        if (character == null)
            return;

        if (!playingMovementCoroutines.TryGetValue(character, out Coroutine running))
            return;

        if (running != null)
            StopCoroutine(running);

        playingMovementCoroutines.Remove(character);
    }

    void ReleaseDialogueAnimationOverride(GameObject character, Animator animator)
    {
        if (character == null)
            return;

        bool hadDialogueOverride = playingDialogueAnimations.Remove(character);
        if (!hadDialogueOverride)
            return;

        ClearCharacterExternalAnimationControl(character);

        if (animator == null)
            return;

        if (!originalAnimations.TryGetValue(character, out string originalAnimHash))
            return;

        if (string.IsNullOrEmpty(originalAnimHash))
            return;

        if (int.TryParse(originalAnimHash, out int hash))
        {
            animator.Play(hash, 0, 0f);
        }
    }

    bool ShouldShowBubble(DialogueLine line)
    {
        if (line == null || !line.enableBubble)
            return false;

        bool hasText = !string.IsNullOrWhiteSpace(line.text);
        return hasText || line.showBubbleWhenTextEmpty;
    }

    void ApplyVisibilityChanges(DialogueLine line, DialogueVisibilityTiming timing)
    {
        if (line == null || line.visibilityChanges == null || line.visibilityChanges.Length == 0)
            return;

        for (int i = 0; i < line.visibilityChanges.Length; i++)
        {
            DialogueVisibilityChange change = line.visibilityChanges[i];
            if (change == null || change.timing != timing)
                continue;

            if (string.IsNullOrWhiteSpace(change.characterName))
                continue;

            GameObject targetCharacter = FindCharacterByName(change.characterName);
            if (targetCharacter == null)
            {
                Debug.LogWarning($"显隐目标不存在: {change.characterName}");
                continue;
            }

            if (change.setVisible)
            {
                if (!targetCharacter.activeSelf)
                    targetCharacter.SetActive(true);
            }
            else
            {
                StopMovementCoroutine(targetCharacter);

                if (activeBubbles.TryGetValue(targetCharacter, out GameObject bubble))
                {
                    if (bubble != null)
                        Destroy(bubble);

                    activeBubbles.Remove(targetCharacter);
                }

                ClearCharacterExternalAnimationControl(targetCharacter);
                playingDialogueAnimations.Remove(targetCharacter);
                originalAnimations.Remove(targetCharacter);

                if (targetCharacter.activeSelf)
                    targetCharacter.SetActive(false);
            }
        }
    }

    GameObject FindCharacterByName(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return null;

        GameObject foundActive = GameObject.Find(characterName);
        if (foundActive != null)
            return foundActive;

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform transform = allTransforms[i];
            if (transform == null)
                continue;

            GameObject go = transform.gameObject;
            if (go == null || !go.scene.IsValid())
                continue;

            if (go.name == characterName)
                return go;
        }

        return null;
    }

    void PlayDialogueAnimation(GameObject character, Animator animator, string animationName)
    {
        if (character == null || animator == null || string.IsNullOrEmpty(animationName))
            return;

        ClearCharacterExternalAnimationControl(character);

        if (!originalAnimations.ContainsKey(character))
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            originalAnimations[character] = state.shortNameHash.ToString();
        }

        playingDialogueAnimations[character] = animationName;
        animator.Play(animationName, 0, 0f);
    }

    void ApplyMovementAnimation(Animator animator, Vector3 worldDelta, bool moving, GameObject character, DialogueMoveStyle moveStyle)
    {
        if (animator == null || character == null)
            return;

        MovementAnimatorParamSupport support = GetMovementAnimatorParamSupport(animator);

        Vector2 currentFacing = GetLastFacing(character);
        Vector2 moveDir = ConvertWorldDeltaToMoveVector(worldDelta);

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            moveDir.Normalize();
            currentFacing = moveDir;
            lastFacingDirections[character] = currentFacing;
        }

        bool isRunning = moving && moveStyle == DialogueMoveStyle.Run;

        PlayerController characterPlayerController = character.GetComponent<PlayerController>();
        if (characterPlayerController != null)
        {
            characterPlayerController.SetExternalAnimationControl(currentFacing, moving ? 1f : 0f, isRunning);
        }

        if (support.hasIsRunning)
            animator.SetBool("isRunning", isRunning);

        if (support.hasMoveX)
            animator.SetFloat("MoveX", currentFacing.x);

        if (support.hasMoveY)
            animator.SetFloat("MoveY", currentFacing.y);

        if (support.hasSpeed)
            animator.SetFloat("Speed", moving ? 1f : 0f);
    }

    Vector2 ConvertWorldDeltaToMoveVector(Vector3 worldDelta)
    {
        float vertical = Mathf.Abs(worldDelta.z) >= Mathf.Abs(worldDelta.y)
            ? worldDelta.z
            : worldDelta.y;

        return new Vector2(worldDelta.x, vertical);
    }

    void ClearCharacterExternalAnimationControl(GameObject character)
    {
        if (character == null)
            return;

        PlayerController characterPlayerController = character.GetComponent<PlayerController>();
        if (characterPlayerController != null)
        {
            characterPlayerController.ClearExternalAnimationControl();
        }
    }

    Vector2 GetLastFacing(GameObject character)
    {
        if (character == null)
            return Vector2.down;

        if (lastFacingDirections.TryGetValue(character, out Vector2 facing))
            return facing;

        lastFacingDirections[character] = Vector2.down;
        return Vector2.down;
    }

    MovementAnimatorParamSupport GetMovementAnimatorParamSupport(Animator animator)
    {
        if (animator == null)
            return default;

        if (movementAnimatorParamCache.TryGetValue(animator, out MovementAnimatorParamSupport support))
            return support;

        support = new MovementAnimatorParamSupport();
        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Float)
            {
                if (parameter.name == "MoveX") support.hasMoveX = true;
                else if (parameter.name == "MoveY") support.hasMoveY = true;
                else if (parameter.name == "Speed") support.hasSpeed = true;
            }
            else if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                if (parameter.name == "isRunning") support.hasIsRunning = true;
            }
        }

        movementAnimatorParamCache[animator] = support;
        return support;
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