using UnityEngine;

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
}