using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [Header("对话")]
    public DialogueSequence sequence;

    [Header("对话结束后进入战斗")]
    public bool startBattleAfterDialogue = false;
    public BattleEncounterData encounterToStart;
    public string battleSceneName = "BattleSecene";

    private bool triggered = false;
    private bool waitingForBattleStart = false;

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

        if (startBattleAfterDialogue && !waitingForBattleStart)
        {
            waitingForBattleStart = true;
            StartCoroutine(WaitDialogueEndThenStartBattle());
        }

        triggered = true;
    }

    System.Collections.IEnumerator WaitDialogueEndThenStartBattle()
    {
        while (DialogueManager.IsDialogueActive)
            yield return null;

        PartyManager partyManager = PartyManager.Instance != null
            ? PartyManager.Instance
            : FindObjectOfType<PartyManager>();

        if (partyManager != null)
            partyManager.SetPendingEncounter(encounterToStart);
        else
            Debug.LogWarning("未找到 PartyManager，无法设置 pendingEncounter。战斗可能使用默认遭遇。", this);

        if (string.IsNullOrWhiteSpace(battleSceneName))
        {
            Debug.LogError("battleSceneName 为空，无法切换到战斗场景。", this);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(battleSceneName))
        {
            Debug.LogError(
                $"场景 `{battleSceneName}` 当前不可加载。请确认它已加入 Build Settings，且名称与 Inspector 中填写一致。",
                this);
            yield break;
        }

        if (partyManager != null)
        {
            bool started = partyManager.StartBattleFromCurrentScene(encounterToStart, battleSceneName);
            if (!started)
                Debug.LogError("进入战斗失败：PartyManager 未能启动战斗场景切换。", this);
        }
        else
        {
            Debug.LogError("未找到 PartyManager，无法执行场景冻结并进入战斗。", this);
        }
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