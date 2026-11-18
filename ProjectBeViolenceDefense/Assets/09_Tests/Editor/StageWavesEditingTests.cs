using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class StageWavesEditingTests
{
    private StageWaves asset;
    private SerializedObject data;

    [SetUp]
    public void SetUp()
    {
        asset = ScriptableObject.CreateInstance<StageWaves>();
        data = new SerializedObject(asset);
        StageWavesEditing.AddWave(data.FindProperty("waves"));
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Undo.ClearUndo(asset);
        data.Dispose();
        Object.DestroyImmediate(asset);
    }

    private void FillFirstGroup()
    {
        asset.waves[0].note = "original";
        asset.waves[0].factions[0].groups[0].note = "candidate";
        asset.waves[0].factions[0].groups[0].enemies.Add(new StageWaves.Entry { unitId = "goblin", count = 7 });
        data.Update();
    }

    [Test]
    public void DuplicateWavePreservesValuesWithoutSharingNestedObjects()
    {
        FillFirstGroup();
        int copy = StageWavesEditing.Duplicate(data.FindProperty("waves"), 0);
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(copy, Is.EqualTo(1));
        Assert.That(asset.waves[copy].note, Is.EqualTo("original"));
        var original = asset.waves[0].factions[0].groups[0];
        var duplicate = asset.waves[copy].factions[0].groups[0];
        Assert.That(duplicate.note, Is.EqualTo("candidate"));
        Assert.That(duplicate.enemies[0].unitId, Is.EqualTo("goblin"));
        duplicate.enemies[0].count = 20;
        duplicate.enemies.Add(new StageWaves.Entry());
        Assert.That(original.enemies[0].count, Is.EqualTo(7));
        Assert.That(original.enemies.Count, Is.EqualTo(1));
    }

    [Test]
    public void NewRowsDoNotInheritLastRowsData()
    {
        FillFirstGroup();
        StageWavesEditing.AddWave(data.FindProperty("waves"));
        var groups = data.FindProperty("waves").GetArrayElementAtIndex(0)
            .FindPropertyRelative("factions").GetArrayElementAtIndex(0).FindPropertyRelative("groups");
        StageWavesEditing.AddGroup(groups);
        StageWavesEditing.AddUnit(groups.GetArrayElementAtIndex(0).FindPropertyRelative("enemies"));
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(asset.waves[1].factions.Select(f => f.faction), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(asset.waves[1].factions[0].groups[0].enemies, Is.Empty);
        Assert.That(asset.waves[0].factions[0].groups[1].enemies, Is.Empty);
        var row = asset.waves[0].factions[0].groups[0].enemies[1];
        Assert.That(row.unitId, Is.Empty);
        Assert.That(row.count, Is.EqualTo(1));
    }

    [Test]
    public void SerializedReorderAndDeleteSupportUndoRedo()
    {
        FillFirstGroup();
        StageWavesEditing.AddWave(data.FindProperty("waves"));
        data.ApplyModifiedPropertiesWithoutUndo();
        Undo.IncrementCurrentGroup();
        data.FindProperty("waves").MoveArrayElement(0, 1);
        data.ApplyModifiedProperties();
        Undo.FlushUndoRecordObjects();
        Assert.That(asset.waves[1].note, Is.EqualTo("original"));
        Undo.PerformUndo();
        Assert.That(asset.waves[0].note, Is.EqualTo("original"));
        Undo.PerformRedo();
        Assert.That(asset.waves[1].factions[0].groups[0].enemies[0].count, Is.EqualTo(7));
        Undo.IncrementCurrentGroup();
        data.Update();
        data.FindProperty("waves").DeleteArrayElementAtIndex(1);
        data.ApplyModifiedProperties();
        Undo.FlushUndoRecordObjects();
        Assert.That(asset.waves.Count, Is.EqualTo(1));
        Undo.PerformUndo();
        Assert.That(asset.waves[1].factions[0].groups[0].enemies[0].unitId, Is.EqualTo("goblin"));
    }

    [Test]
    public void RangeCountsOneCandidatePerFactionRatherThanAllCandidates()
    {
        FillFirstGroup();
        asset.waves[0].factions[0].groups.Add(new StageWaves.Group
            { enemies = new List<StageWaves.Entry> { new() { count = 20 } } });
        asset.waves[0].factions[1].groups[0].enemies.Add(new StageWaves.Entry { count = 3 });
        var range = StageWavesEditing.Range(asset.waves[0]);
        Assert.That(range.Min, Is.EqualTo(10));
        Assert.That(range.Max, Is.EqualTo(23));
    }

    [Test]
    public void ValidationFindsAllProblemsWithoutMutatingData()
    {
        FillFirstGroup();
        asset.waves[0].factions[1].faction = 0;
        asset.waves[0].factions[1].groups[0].enemies.Add(new StageWaves.Entry { unitId = "missing", count = 2 });
        asset.waves[0].factions[0].groups[0].enemies.Add(new StageWaves.Entry { unitId = "", count = -1 });
        string before = EditorJsonUtility.ToJson(asset);
        var units = new List<StageWavesEditing.Unit> { new() { Id = "goblin", Name = "Goblin", Faction = 2 } };
        var found = StageWavesEditing.Validate(asset, units, true);
        Assert.That(found.Any(i => i.Message.Contains("중복")), Is.True);
        Assert.That(found.Any(i => i.Message.Contains("알 수 없는 ID")), Is.True);
        Assert.That(found.Any(i => i.Message.Contains("소속")), Is.True);
        Assert.That(found.Any(i => i.Message.Contains("0 이하")), Is.True);
        Assert.That(EditorJsonUtility.ToJson(asset), Is.EqualTo(before));
    }

    [Test]
    public void EmptyAndMalformedDataCanBeDiagnosed()
    {
        asset.waves = new List<StageWaves.Wave> { null, new() { factions = new List<StageWaves.FactionEntry> { null } } };
        Assert.DoesNotThrow(() => StageWavesEditing.Validate(asset, new(), false));
        Assert.That(StageWavesEditing.Range(null), Is.EqualTo((0L, 0L)));
        asset.waves = null;
        Assert.That(StageWavesEditing.Validate(asset, new(), false).Any(i => i.Message.Contains("웨이브가 없습니다")), Is.True);
    }

    [Test]
    public void CatalogDuplicatesAndMissingSpawnsAreReported()
    {
        var stage = ScriptableObject.CreateInstance<StageData>();
        try
        {
            var units = new List<StageWavesEditing.Unit> { new() { Id = "same" }, new() { Id = "same" } };
            var found = StageWavesEditing.Validate(asset, units, true, stage);
            Assert.That(found.Any(i => i.Message.Contains("카탈로그 ID 중복")), Is.True);
            Assert.That(found.Count(i => i.Message.Contains("스폰 지점")), Is.EqualTo(2));
        }
        finally { Object.DestroyImmediate(stage); }
    }
}
