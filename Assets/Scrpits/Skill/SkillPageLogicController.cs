using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class SkillPageLogicController : MonoBehaviour
{
    public enum SkillPageMode
    {
        CharacterSelect,
        SkillSelect
    }

    [Serializable]
    public class SkillLearnedEvent : UnityEvent<PartyMemberState, Skill, int>
    {
    }

    [Header("可选：主菜单状态联动")]
    [SerializeField] private ExplorationMainMenuController mainMenuController;
    [SerializeField] private int skillPageIndex = 2;
    [SerializeField] private bool syncWithMainMenu = true;

    [Header("输入")]
    [SerializeField] private KeyCode upKey = KeyCode.W;
    [SerializeField] private KeyCode downKey = KeyCode.S;
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private KeyCode backKey = KeyCode.Escape;

    [Header("事件")]
    public UnityEvent onSelectionChanged;
    public UnityEvent onPageStateChanged;
    public SkillLearnedEvent onSkillLearned;

    private readonly List<PartyMemberState> activeMembers = new List<PartyMemberState>();
    private readonly List<Skill> currentSkillPool = new List<Skill>();

    private int selectedMemberIndex = 0;
    private int selectedSkillIndex = 0;
    private SkillPageMode mode = SkillPageMode.CharacterSelect;

    public SkillPageMode CurrentMode => mode;
    public int SelectedMemberIndex => selectedMemberIndex;
    public int SelectedSkillIndex => selectedSkillIndex;
    public IReadOnlyList<PartyMemberState> ActiveMembers => activeMembers;
    public IReadOnlyList<Skill> CurrentSkillPool => currentSkillPool;

    public PartyMemberState SelectedMember =>
        selectedMemberIndex >= 0 && selectedMemberIndex < activeMembers.Count ? activeMembers[selectedMemberIndex] : null;

    public Skill SelectedSkill =>
        selectedSkillIndex >= 0 && selectedSkillIndex < currentSkillPool.Count ? currentSkillPool[selectedSkillIndex] : null;

    public int SelectedMemberCurrentJP => SelectedMember != null ? Mathf.Max(0, SelectedMember.currentJP) : 0;
    public int NextLearningCostJP => SkillLearningService.GetNextArtLearningCost(SelectedMember);

    private void Awake()
    {
        if (mainMenuController == null)
            mainMenuController = FindObjectOfType<ExplorationMainMenuController>();
    }

    private void OnEnable()
    {
        RefreshPartyMembers(true);
        EnterCharacterSelectMode();
    }

    private void Update()
    {
        if (!IsSkillPageActive())
            return;

        if (Input.GetKeyDown(backKey))
        {
            if (mode == SkillPageMode.SkillSelect)
                EnterCharacterSelectMode();
            return;
        }

        if (mode == SkillPageMode.CharacterSelect)
            HandleCharacterSelectInput();
        else
            HandleSkillSelectInput();
    }

    public void RefreshPartyMembers(bool resetSelection)
    {
        activeMembers.Clear();

        PartyManager partyManager = PartyManager.Instance;
        if (partyManager != null)
            activeMembers.AddRange(partyManager.GetActivePartyMembers(4));

        if (resetSelection)
            selectedMemberIndex = 0;

        selectedMemberIndex = Mathf.Clamp(selectedMemberIndex, 0, Mathf.Max(0, activeMembers.Count - 1));
        RebuildSkillPoolForSelectedMember();
        onSelectionChanged?.Invoke();
    }

    public void EnterCharacterSelectMode()
    {
        mode = SkillPageMode.CharacterSelect;
        selectedSkillIndex = 0;
        onPageStateChanged?.Invoke();
        onSelectionChanged?.Invoke();
    }

    public void EnterSkillSelectMode()
    {
        mode = SkillPageMode.SkillSelect;
        selectedSkillIndex = Mathf.Clamp(selectedSkillIndex, 0, Mathf.Max(0, currentSkillPool.Count - 1));
        onPageStateChanged?.Invoke();
        onSelectionChanged?.Invoke();
    }

    public void MoveMemberSelection(int delta)
    {
        if (activeMembers.Count == 0)
            return;

        selectedMemberIndex = WrapIndex(selectedMemberIndex + delta, activeMembers.Count);
        RebuildSkillPoolForSelectedMember();
        onSelectionChanged?.Invoke();
    }

    public void MoveSkillSelection(int delta)
    {
        if (currentSkillPool.Count == 0)
            return;

        selectedSkillIndex = WrapIndex(selectedSkillIndex + delta, currentSkillPool.Count);
        onSelectionChanged?.Invoke();
    }

    public SkillLearnResult TryLearnSelectedSkill()
    {
        PartyMemberState member = SelectedMember;
        Skill skill = SelectedSkill;
        SkillLearnResult result = SkillLearningService.TryLearnArt(member, skill);

        if (result.success)
        {
            onSkillLearned?.Invoke(member, skill, result.costJP);
            RebuildSkillPoolForSelectedMember();
        }

        onSelectionChanged?.Invoke();
        return result;
    }

    public bool IsSelectedSkillLearned()
    {
        return SkillLearningService.IsArtLearned(SelectedMember, SelectedSkill);
    }

    public bool CanLearnSelectedSkill(out SkillLearnFailureReason reason)
    {
        return SkillLearningService.CanLearnArt(SelectedMember, SelectedSkill, out reason);
    }

    private void HandleCharacterSelectInput()
    {
        if (activeMembers.Count <= 0)
            return;

        if (Input.GetKeyDown(upKey))
        {
            MoveMemberSelection(-1);
        }
        else if (Input.GetKeyDown(downKey))
        {
            MoveMemberSelection(1);
        }
        else if (Input.GetKeyDown(confirmKey))
        {
            EnterSkillSelectMode();
        }
    }

    private void HandleSkillSelectInput()
    {
        if (currentSkillPool.Count <= 0)
            return;

        if (Input.GetKeyDown(upKey))
        {
            MoveSkillSelection(-1);
        }
        else if (Input.GetKeyDown(downKey))
        {
            MoveSkillSelection(1);
        }
        else if (Input.GetKeyDown(confirmKey))
        {
            TryLearnSelectedSkill();
        }
    }

    private void RebuildSkillPoolForSelectedMember()
    {
        currentSkillPool.Clear();

        PartyMemberState member = SelectedMember;
        if (member != null)
            currentSkillPool.AddRange(SkillLearningService.GetLearnableArtsForMember(member));

        selectedSkillIndex = Mathf.Clamp(selectedSkillIndex, 0, Mathf.Max(0, currentSkillPool.Count - 1));
    }

    private bool IsSkillPageActive()
    {
        if (!syncWithMainMenu)
            return true;

        if (mainMenuController == null)
            return true;

        return mainMenuController.IsPageOpened && mainMenuController.OpenedIndex == skillPageIndex;
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
            return 0;

        int wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }
}
