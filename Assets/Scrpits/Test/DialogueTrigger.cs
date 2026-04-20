using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    public DialogueSequence sequence;
    private bool triggered = false;

    void TryStartDialogue(GameObject otherObject, string source)
    {
        Debug.Log($"{source} triggered by: {otherObject.name}");

        if (!otherObject.CompareTag("Player"))
        {
            Debug.Log("Collider 不是 Player 标签");
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError("DialogueManager.Instance 为 null，请确认场景中存在 DialogueManager");
            return;
        }

        if (triggered)
        {
            Debug.Log("对话已经触发过");
            return;
        }

        if (sequence == null)
        {
            Debug.LogError("DialogueTrigger 的 sequence 为空，请赋值 DialogueSequence");
            return;
        }

        Debug.Log($"触发对话: {sequence.name}");
        DialogueManager.Instance.StartDialogue(sequence);

        triggered = true;
    }

    // 2D 触发回调
    void OnTriggerEnter2D(Collider2D other)
    {
        TryStartDialogue(other.gameObject, "OnTriggerEnter2D");
    }

    // 3D 触发回调
    void OnTriggerEnter(Collider other)
    {
        TryStartDialogue(other.gameObject, "OnTriggerEnter3D");
    }

    void OnTriggerExit2D(Collider2D other)
    {
        Debug.Log($"OnTriggerExit2D triggered by: {other.name}");
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log($"OnTriggerExit3D triggered by: {other.name}");
    }
}