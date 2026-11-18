using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

// Editor-only authoring helpers. Reading/validation never normalizes or writes existing data.
internal static class StageWavesEditing
{
    internal sealed class Unit
    {
        public string Id;
        public string Name;
        public int Faction;
        public string Label => $"{Name}  [{Id}]";
    }

    internal readonly struct Issue
    {
        public readonly int Wave;
        public readonly string Message;
        public readonly MessageType Severity;
        public Issue(int wave, string message, MessageType severity = MessageType.Error)
        {
            Wave = wave;
            Message = message;
            Severity = severity;
        }
    }

    internal static List<Unit> ReadCatalog(EnemyCatalog catalog)
    {
        var result = new List<Unit>();
        if (catalog == null) return result;
        using var serialized = new SerializedObject(catalog);
        SerializedProperty entries = serialized.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("id").stringValue;
            string name = GameText.Get(entry.FindPropertyRelative("displayNameKey").stringValue);
            result.Add(new Unit
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(name) ? id : name,
                Faction = entry.FindPropertyRelative("factionGroup").intValue
            });
        }
        return result;
    }

    internal static string FactionName(int faction) => faction >= 0 && faction < StageData.FactionCount
        ? StageData.FactionNames[faction] : $"잘못된 팩션 ({faction})";

    internal static long Count(StageWaves.Group group) =>
        group?.enemies?.Sum(entry => (long)Math.Max(0, entry?.count ?? 0)) ?? 0;

    internal static (long Min, long Max) Range(StageWaves.Wave wave)
    {
        long min = 0, max = 0;
        if (wave?.factions == null) return (0, 0);
        foreach (StageWaves.FactionEntry faction in wave.factions)
        {
            if (faction?.groups == null || faction.groups.Count == 0) continue;
            min += faction.groups.Min(Count);
            max += faction.groups.Max(Count);
        }
        return (min, max);
    }

    internal static string RangeLabel(StageWaves.Wave wave)
    {
        var range = Range(wave);
        return range.Min == range.Max ? $"{range.Min:N0}명" : $"{range.Min:N0}~{range.Max:N0}명";
    }

    internal static List<Issue> Validate(StageWaves asset, List<Unit> units, bool hasCatalog,
        StageData stage = null)
    {
        var issues = new List<Issue>();
        if (!hasCatalog)
            issues.Add(new Issue(-1, "카탈로그를 지정하세요. 유닛 ID와 소속 팩션은 아직 검사하지 않았습니다.", MessageType.Warning));
        var byId = units.Where(u => !string.IsNullOrWhiteSpace(u.Id)).GroupBy(u => u.Id)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var duplicate in byId.Where(pair => pair.Value.Count > 1))
            issues.Add(new Issue(-1, $"카탈로그 ID 중복: {duplicate.Key}. 게임에서 유닛을 찾을 수 없습니다."));
        if (asset?.waves == null || asset.waves.Count == 0)
        {
            issues.Add(new Issue(-1, "웨이브가 없습니다. + 웨이브로 시작하세요."));
            return issues;
        }
        for (int w = 0; w < asset.waves.Count; w++)
        {
            var wave = asset.waves[w];
            string prefix = $"W{w + 1:00}";
            int count = wave?.factions?.Count ?? 0;
            if (count < StageWaves.MinFactions || count > StageWaves.MaxFactions)
                issues.Add(new Issue(w, $"{prefix}: 팩션은 2~4개여야 합니다. (현재 {count}개)"));
            if (wave?.factions == null) continue;
            var seen = new HashSet<int>();
            foreach (var faction in wave.factions)
            {
                if (faction == null)
                {
                    issues.Add(new Issue(w, $"{prefix}: 비어 있는 팩션 항목입니다."));
                    continue;
                }
                string location = $"{prefix} / {FactionName(faction.faction)}";
                if (faction.faction < 0 || faction.faction >= StageData.FactionCount)
                    issues.Add(new Issue(w, $"{location}: 팩션 번호를 수정하세요."));
                if (!seen.Add(faction.faction))
                    issues.Add(new Issue(w, $"{location}: 같은 팩션이 중복되었습니다."));
                if (stage != null && !(stage.spawns?.Any(s => s.faction == faction.faction) ?? false))
                    issues.Add(new Issue(w, $"{location}: {stage.name}에 스폰 지점이 없어 출전하지 못합니다.", MessageType.Warning));
                if (faction.groups == null || faction.groups.Count == 0)
                {
                    issues.Add(new Issue(w, $"{location}: 후보 그룹이 필요합니다."));
                    continue;
                }
                for (int g = 0; g < faction.groups.Count; g++)
                {
                    var group = faction.groups[g];
                    string groupPath = $"{location} / 그룹 {g + 1}";
                    if (group?.enemies == null)
                    {
                        issues.Add(new Issue(w, $"{groupPath}: 그룹 또는 유닛 목록이 비어 있습니다."));
                        continue;
                    }
                    if (Count(group) == 0)
                        issues.Add(new Issue(w, $"{groupPath}: 출전 병력이 0명입니다. 이 후보도 선택될 수 있습니다.", MessageType.Warning));
                    for (int e = 0; e < group.enemies.Count; e++)
                    {
                        var entry = group.enemies[e];
                        string rowPath = $"{groupPath} / 행 {e + 1}";
                        if (entry == null || entry.count <= 0)
                        {
                            issues.Add(new Issue(w, $"{rowPath}: 수량이 0 이하이거나 비어 있어 스폰에서 제외됩니다.", MessageType.Warning));
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(entry.unitId))
                            issues.Add(new Issue(w, $"{rowPath}: 유닛을 선택하세요."));
                        else if (hasCatalog)
                        {
                            if (!byId.TryGetValue(entry.unitId, out var found))
                                issues.Add(new Issue(w, $"{rowPath}: 알 수 없는 ID '{entry.unitId}'."));
                            else if (found.Count == 1 && found[0].Faction != faction.faction)
                                issues.Add(new Issue(w, $"{rowPath}: {found[0].Name}의 소속은 {FactionName(found[0].Faction)}입니다.", MessageType.Warning));
                        }
                    }
                }
            }
        }
        return issues;
    }

    internal static int AddWave(SerializedProperty waves)
    {
        int index = waves.arraySize++;
        var wave = waves.GetArrayElementAtIndex(index);
        wave.FindPropertyRelative("note").stringValue = "새 웨이브";
        var factions = wave.FindPropertyRelative("factions");
        factions.ClearArray();
        AddFaction(factions, 0);
        AddFaction(factions, 1);
        return index;
    }

    internal static void AddFaction(SerializedProperty factions, int faction)
    {
        var entry = factions.GetArrayElementAtIndex(factions.arraySize++);
        entry.FindPropertyRelative("faction").intValue = faction;
        var groups = entry.FindPropertyRelative("groups");
        groups.ClearArray();
        AddGroup(groups);
    }

    internal static void AddGroup(SerializedProperty groups)
    {
        var group = groups.GetArrayElementAtIndex(groups.arraySize++);
        group.FindPropertyRelative("note").stringValue = "새 후보";
        group.FindPropertyRelative("enemies").ClearArray();
    }

    internal static void AddUnit(SerializedProperty enemies)
    {
        var entry = enemies.GetArrayElementAtIndex(enemies.arraySize++);
        entry.FindPropertyRelative("unitId").stringValue = "";
        entry.FindPropertyRelative("count").intValue = 1;
    }

    internal static int Duplicate(SerializedProperty array, int index)
    {
        // Unity copies the inline serialized value, including nested lists (no shared Group/Entry references).
        array.InsertArrayElementAtIndex(index);
        return index + 1;
    }
}
