using UnityEngine;

public class Character : MonoBehaviour
{
    [Header("角色基本信息")]
    public string characterName;   // 与DialogueLine.characterName对应
    public Animator animator;      // 角色Animator
    public Transform anchor;       // 气泡或特效挂点

    void Awake()
    {
        // 如果没有手动绑定Animator或Anchor，这里尝试自动获取
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
                Debug.LogWarning($"Character {characterName} 缺少Animator组件！");
        }

        if (anchor == null)
        {
            Transform a = transform.Find("Anchor");
            if (a != null)
                anchor = a;
            else
                Debug.LogWarning($"Character {characterName} 缺少Anchor挂点！");
        }
    }

    /// <summary>
    /// 播放指定动画
    /// </summary>
    public void PlayAnimation(string animName)
    {
        if (animator != null && !string.IsNullOrEmpty(animName))
        {
            animator.Play(animName);
        }
    }
}