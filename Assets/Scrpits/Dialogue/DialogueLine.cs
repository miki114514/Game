using UnityEngine;

[System.Serializable]
public enum DialogueMoveStyle
{
    Walk,
    Run
}

[System.Serializable]
public enum DialogueVisibilityTiming
{
    BeforeLine,
    AfterLine
}

[System.Serializable]
public class DialogueVisibilityChange
{
    [Tooltip("需要执行显隐的角色名（GameObject 名称）")]
    public string characterName;
    [Tooltip("勾选=显示角色；取消勾选=隐藏角色")]
    public bool setVisible = true;
    [Tooltip("在本句开始前还是结束后执行")]
    public DialogueVisibilityTiming timing = DialogueVisibilityTiming.BeforeLine;
}

[System.Serializable]
public class DialogueLine
{
    public string characterName;  // 角色名字
    public string text;
    public BubbleType bubbleType;
    public Vector3 offset;
    [Tooltip("打字机效果总时长（秒）")]
    public float typewriterDuration = 2f; // 新增字段
    [Tooltip("播放的语音音频")]
    public AudioClip voiceClip;   // 新增字段
    [Tooltip("播放的动画")]
    public string animationName;

    [Header("气泡显示")]
    [Tooltip("是否显示本句气泡（关闭后可用于纯演出句）")]
    public bool enableBubble = true;
    [Tooltip("文本为空时是否仍强制显示气泡")]
    public bool showBubbleWhenTextEmpty;
    
    [Header("角色移动")]
    [Tooltip("是否在该句对话触发角色移动")]
    public bool enableMovement;
    [Tooltip("移动时长（秒），小于等于0表示瞬移")]
    public float moveDuration = 0.3f;
    [Tooltip("是否使用相对位移（在当前坐标基础上偏移）")]
    public bool useRelativeMovement = true;
    [Tooltip("相对位移偏移量（useRelativeMovement=true 时生效）")]
    public Vector3 moveOffset = Vector3.zero;
    [Tooltip("目标世界坐标（useRelativeMovement=false 时生效）")]
    public Vector3 moveTargetWorldPosition = Vector3.zero;
    [Tooltip("本次移动动画类型：行走或奔跑")]
    public DialogueMoveStyle moveStyle = DialogueMoveStyle.Walk;
    [Tooltip("勾选后：移动期间不使用角色控制同款移动动画，改为播放本句 Animation Name")]
    public bool useDialogueAnimationDuringMovement;
    [Tooltip("勾选后：气泡显示与语音播放会在移动完成后才开始")]
    public bool showBubbleAndVoiceAfterMovement;

    [Header("角色显隐")]
    [Tooltip("本句可执行多个角色显隐指令（出现/消失）")]
    public DialogueVisibilityChange[] visibilityChanges;
}