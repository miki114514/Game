using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Sequence")]
public class DialogueSequence : ScriptableObject
{
    public DialogueLine[] lines;
}