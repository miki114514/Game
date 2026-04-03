using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 行动顺序系统
/// 行动值公式：Speed × Random(0.9 ~ 1.1)，降序排列
/// 始终缓存下一轮的行动顺序，支持因 Break / 异常状态触发动态重算
/// </summary>
public class TurnOrderSystem
{
    private List<BattleUnit> _currentRoundOrder = new List<BattleUnit>();
    private List<BattleUnit> _nextRoundOrder    = new List<BattleUnit>();

    public IReadOnlyList<BattleUnit> CurrentOrder => _currentRoundOrder;
    public IReadOnlyList<BattleUnit> NextOrder    => _nextRoundOrder;

    // -------------------------------------------------------
    // 内部：按公式计算排序列表
    // -------------------------------------------------------
    private List<BattleUnit> ComputeOrder(List<BattleUnit> units)
    {
        var scored = new List<(BattleUnit unit, float score)>(units.Count);
        foreach (var unit in units)
        {
            if (unit == null || unit.currentHP <= 0) continue;
            float score = unit.speed * Random.Range(0.9f, 1.1f);
            scored.Add((unit, score));
        }
        scored.Sort((a, b) => b.score.CompareTo(a.score));

        var result = new List<BattleUnit>(scored.Count);
        for (int i = 0; i < scored.Count; i++)
            result.Add(scored[i].unit);
        return result;
    }

    // -------------------------------------------------------
    // 战斗开始时初始化，同时预算首个下轮顺序
    // -------------------------------------------------------
    public void Initialize(List<BattleUnit> allUnits)
    {
        _currentRoundOrder = ComputeOrder(allUnits);
        _nextRoundOrder    = ComputeOrder(allUnits);
        LogOrder("【初始化】本轮行动顺序", _currentRoundOrder);
        LogOrder("【初始化】下轮预计顺序", _nextRoundOrder);
    }

    // -------------------------------------------------------
    // 当前轮结束，切换至预算的下一轮，同时预算新的下下轮
    // -------------------------------------------------------
    public void AdvanceToNextRound(List<BattleUnit> aliveUnits)
    {
        _currentRoundOrder = _nextRoundOrder;
        _nextRoundOrder    = ComputeOrder(aliveUnits);
        LogOrder("【换轮】本轮行动顺序", _currentRoundOrder);
        LogOrder("【换轮】下轮预计顺序", _nextRoundOrder);
    }

    // -------------------------------------------------------
    // 强制重算下一轮顺序（Break 、状态变化等触发时调用）
    // -------------------------------------------------------
    public void RecalculateNextRound(List<BattleUnit> aliveUnits)
    {
        _nextRoundOrder = ComputeOrder(aliveUnits);
        LogOrder("【重算】下轮行动顺序", _nextRoundOrder);
    }

    // -------------------------------------------------------
    // 单位阵亡，从两轮顺序中移除
    // -------------------------------------------------------
    public void RemoveUnit(BattleUnit unit)
    {
        _currentRoundOrder.Remove(unit);
        _nextRoundOrder.Remove(unit);
    }

    // -------------------------------------------------------
    private void LogOrder(string label, List<BattleUnit> order)
    {
        var sb = new StringBuilder();
        sb.Append("[TurnOrder] ").Append(label).Append(": ");
        for (int i = 0; i < order.Count; i++)
        {
            if (i > 0) sb.Append(" → ");
            sb.Append(order[i].unitName);
        }
        Debug.Log(sb.ToString());
    }
}
