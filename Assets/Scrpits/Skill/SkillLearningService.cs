using System.Collections.Generic;
using UnityEngine;

public enum SkillLearnFailureReason
{
    None,
    InvalidMember,
    InvalidSkill,
    NoClass,
    SkillNotInClassPool,
    AlreadyLearned,
    NotLearnableByClass,
    NotEnoughJP
}

public struct SkillLearnResult
{
    public bool success;
    public SkillLearnFailureReason failureReason;
    public Skill learnedSkill;
    public int costJP;
    public int remainingJP;

    public static SkillLearnResult Failed(SkillLearnFailureReason reason, Skill skill = null)
    {
        return new SkillLearnResult
        {
            success = false,
            failureReason = reason,
            learnedSkill = skill,
            costJP = 0,
            remainingJP = 0
        };
    }

    public static SkillLearnResult Succeeded(Skill skill, int costJP, int remainingJP)
    {
        return new SkillLearnResult
        {
            success = true,
            failureReason = SkillLearnFailureReason.None,
            learnedSkill = skill,
            costJP = Mathf.Max(0, costJP),
            remainingJP = Mathf.Max(0, remainingJP)
        };
    }
}

public static class SkillLearningService
{
    // 固定梯度：第 N 次学习战技（从 1 开始）消耗对应 JP。
    private static readonly int[] ArtLearningCosts = { 30, 100, 500, 1000, 2000, 3000, 5000, 8000 };

    public static IReadOnlyList<int> LearningCostTable => ArtLearningCosts;

    public static CharacterClassDefinition GetClassDefinition(PartyMemberState member)
    {
        return member != null ? member.GetClassDefinition() : null;
    }

    public static List<Skill> GetLearnableArtsForMember(PartyMemberState member)
    {
        CharacterClassDefinition classDefinition = GetClassDefinition(member);
        if (classDefinition == null || classDefinition.learnableArts == null)
            return new List<Skill>();

        List<Skill> result = new List<Skill>();
        HashSet<Skill> unique = new HashSet<Skill>();
        for (int i = 0; i < classDefinition.learnableArts.Count; i++)
        {
            Skill skill = classDefinition.learnableArts[i];
            if (skill == null)
                continue;

            if (!skill.CanBeLearnedByClass(classDefinition))
                continue;

            if (unique.Add(skill))
                result.Add(skill);
        }

        return result;
    }

    public static bool IsArtLearned(PartyMemberState member, Skill skill)
    {
        if (member == null || skill == null || member.learnedArts == null)
            return false;

        return member.learnedArts.Contains(skill);
    }

    public static int GetLearnedArtCountInClassPool(PartyMemberState member)
    {
        if (member == null)
            return 0;

        List<Skill> classPool = GetLearnableArtsForMember(member);
        if (classPool.Count == 0 || member.learnedArts == null || member.learnedArts.Count == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < classPool.Count; i++)
        {
            if (member.learnedArts.Contains(classPool[i]))
                count++;
        }

        return count;
    }

    public static int GetNextArtLearningCost(PartyMemberState member)
    {
        int learnedCount = Mathf.Max(0, GetLearnedArtCountInClassPool(member));
        int index = Mathf.Clamp(learnedCount, 0, ArtLearningCosts.Length - 1);
        return ArtLearningCosts[index];
    }

    public static bool CanLearnArt(PartyMemberState member, Skill skill, out SkillLearnFailureReason reason)
    {
        reason = SkillLearnFailureReason.None;

        if (member == null)
        {
            reason = SkillLearnFailureReason.InvalidMember;
            return false;
        }

        if (skill == null)
        {
            reason = SkillLearnFailureReason.InvalidSkill;
            return false;
        }

        CharacterClassDefinition classDefinition = GetClassDefinition(member);
        if (classDefinition == null)
        {
            reason = SkillLearnFailureReason.NoClass;
            return false;
        }

        if (!skill.CanBeLearnedByClass(classDefinition))
        {
            reason = SkillLearnFailureReason.NotLearnableByClass;
            return false;
        }

        List<Skill> classPool = GetLearnableArtsForMember(member);
        if (!classPool.Contains(skill))
        {
            reason = SkillLearnFailureReason.SkillNotInClassPool;
            return false;
        }

        if (IsArtLearned(member, skill))
        {
            reason = SkillLearnFailureReason.AlreadyLearned;
            return false;
        }

        int cost = GetNextArtLearningCost(member);
        if (member.currentJP < cost)
        {
            reason = SkillLearnFailureReason.NotEnoughJP;
            return false;
        }

        return true;
    }

    public static SkillLearnResult TryLearnArt(PartyMemberState member, Skill skill)
    {
        if (!CanLearnArt(member, skill, out SkillLearnFailureReason reason))
            return SkillLearnResult.Failed(reason, skill);

        int cost = GetNextArtLearningCost(member);
        member.currentJP = Mathf.Max(0, member.currentJP - cost);

        if (member.learnedArts == null)
            member.learnedArts = new List<Skill>();

        member.learnedArts.Add(skill);
        return SkillLearnResult.Succeeded(skill, cost, member.currentJP);
    }
}
