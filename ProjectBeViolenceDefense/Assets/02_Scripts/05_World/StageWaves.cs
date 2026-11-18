using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 하나의 웨이브 편성. StageData.waves 에 연결한다.
/// 웨이브 = 팩션 2~4개, 팩션 = 그룹 1개 이상, 그룹 = 같은 팩션 유닛 목록.
/// 준비 단계마다 팩션별로 그룹 하나를 무작위로 골라 그 웨이브에 내보낸다.
/// </summary>
[CreateAssetMenu(menuName = "Be Violence/Stage Waves", fileName = "Waves")]
public sealed class StageWaves : ScriptableObject
{
    public const int MinFactions = 2;
    public const int MaxFactions = StageData.FactionCount;

    [Serializable]
    public sealed class Entry
    {
        [Tooltip("Enemy Catalog의 유닛 ID")]
        public string unitId;
        [Min(0)] public int count = 1;
    }

    [Serializable]
    public sealed class Group
    {
        public string note;
        [Tooltip("위에서부터 순서대로 나온다.")]
        public List<Entry> enemies = new();
    }

    [Serializable]
    public sealed class FactionEntry
    {
        [Tooltip("0 = 고블린, 1 = 언데드, 2 = 짐승, 3 = 드래곤·악마")]
        public int faction;
        [Tooltip("준비 단계마다 이 중 하나를 무작위로 고른다.")]
        public List<Group> groups = new();
    }

    [Serializable]
    public sealed class Wave
    {
        public string note;
        public List<FactionEntry> factions = new();
    }

    public List<Wave> waves = new();

    // 게임을 시작할 수 없는 문제. 없으면 null.
    public string Validate()
    {
        if (waves == null || waves.Count == 0)
            return "웨이브가 없습니다.";
        for (int w = 0; w < waves.Count; w++)
        {
            List<FactionEntry> factions = waves[w]?.factions;
            int count = factions?.Count ?? 0;
            if (count < MinFactions || count > MaxFactions)
                return $"웨이브 {w + 1}의 팩션 수({count})는 {MinFactions}~{MaxFactions}개여야 합니다.";
            var seen = new bool[StageData.FactionCount];
            foreach (FactionEntry entry in factions)
            {
                if (entry == null || entry.faction < 0 || entry.faction >= StageData.FactionCount)
                    return $"웨이브 {w + 1}에 잘못된 팩션 값({entry?.faction})이 있습니다.";
                if (seen[entry.faction])
                    return $"웨이브 {w + 1}에 팩션 {entry.faction}이 두 번 있습니다.";
                seen[entry.faction] = true;
                if (entry.groups == null || entry.groups.Count == 0)
                    return $"웨이브 {w + 1}의 팩션 {entry.faction}에 그룹이 없습니다.";
            }
        }
        return null;
    }

    // 웨이브 waveIndex 에서 팩션별로 고른 그룹. 웨이브에 없는 팩션은 null. pick(n) 은 0..n-1 을 돌려준다.
    public Group[] PickGroups(int waveIndex, Func<int, int> pick)
    {
        var picked = new Group[StageData.FactionCount];
        foreach (FactionEntry entry in waves[waveIndex].factions)
            picked[entry.faction] = entry.groups[pick(entry.groups.Count)];
        return picked;
    }
}
