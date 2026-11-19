using System;
using NUnit.Framework;
using UnityEngine;

public sealed class StageProgressTests
{
    private string prefix;

    [SetUp] public void SetUp() => prefix = "BeViolenceDefense.Tests.Progress." + Guid.NewGuid() + ".";
    [TearDown] public void TearDown()
    {
        foreach (string key in new[] { "ClearedStages", "AllStagesUnlocked", "SpellStagesCleared", "Audio" })
            PlayerPrefs.DeleteKey(prefix + key);
        PlayerPrefs.Save();
    }

    [Test]
    public void FreshSaveOnlyOpensFirstStageAndDefaultSpells()
    {
        var progress = new StageProgress(prefix);
        Assert.That(progress.UnlockedStageCount(5), Is.EqualTo(1));
        Assert.That(progress.CurrentStage(5), Is.EqualTo(1));
        Assert.That(progress.IsUnlocked(2, 5), Is.False);
        Assert.That(progress.IsCleared(1), Is.False);
        var spells = new StageSpellUnlocks(progress);
        Assert.That(spells.MissingStage(DefenseBattle.Spell.AttackBuff), Is.Zero);
        Assert.That(spells.MissingStage(DefenseBattle.Spell.Heal), Is.Zero);
        Assert.That(spells.MissingStage(DefenseBattle.Spell.MoveSpeedBuff), Is.EqualTo(2));
    }

    [Test]
    public void VictoriesPersistAndUnlockTheNextStageIncludingTheLastClear()
    {
        for (int stage = 1; stage <= 5; stage++)
        {
            var progress = new StageProgress(prefix);
            Assert.That(progress.IsUnlocked(stage, 5), Is.True);
            new StageSpellUnlocks(progress).RecordClear(stage);
            var loaded = new StageProgress(prefix);
            Assert.That(loaded.IsCleared(stage), Is.True);
            Assert.That(loaded.UnlockedStageCount(5), Is.EqualTo(Math.Min(stage + 1, 5)));
            Assert.That(loaded.CurrentStage(5), Is.EqualTo(Math.Min(stage + 1, 5)));
        }
        Assert.That(new StageProgress(prefix).IsUnlocked(6, 5), Is.False);
    }

    [Test]
    public void UnlockAllPersistsWithoutInventingVictoriesOrUnlockingSpells()
    {
        var progress = new StageProgress(prefix);
        progress.UnlockAllStages();
        progress = new StageProgress(prefix);
        for (int stage = 1; stage <= 5; stage++)
        {
            Assert.That(progress.IsUnlocked(stage, 5), Is.True);
            Assert.That(progress.IsCleared(stage), Is.False);
        }
        Assert.That(progress.CurrentStage(5), Is.EqualTo(1));
        Assert.That(new StageSpellUnlocks(progress).MissingStage(DefenseBattle.Spell.ArmorBuff), Is.EqualTo(3));
        progress.RecordClear(4);
        Assert.That(progress.IsCleared(3), Is.False);
        Assert.That(new StageSpellUnlocks(progress).MissingStage(DefenseBattle.Spell.Taunt), Is.Zero);
    }

    [Test]
    public void ResetClearsProgressOverrideAndLegacyButPreservesOtherPreferences()
    {
        PlayerPrefs.SetInt(prefix + "SpellStagesCleared", (1 << 2) | (1 << 4));
        PlayerPrefs.SetFloat(prefix + "Audio", .42f);
        var progress = new StageProgress(prefix);
        progress.UnlockAllStages();
        progress.Reset();
        progress = new StageProgress(prefix);
        Assert.That(progress.AllStagesUnlocked, Is.False);
        Assert.That(progress.UnlockedStageCount(5), Is.EqualTo(1));
        Assert.That(progress.IsCleared(2), Is.False);
        Assert.That(progress.IsCleared(4), Is.False);
        Assert.That(new StageSpellUnlocks(progress).MissingStage(DefenseBattle.Spell.Taunt), Is.EqualTo(4));
        Assert.That(PlayerPrefs.HasKey(prefix + "SpellStagesCleared"), Is.False);
        Assert.That(PlayerPrefs.GetFloat(prefix + "Audio"), Is.EqualTo(.42f));
    }

    [Test]
    public void LegacySaveMigratesOnlyKnownClearsAndDoesNotOverwriteNewProgress()
    {
        PlayerPrefs.SetInt(prefix + "SpellStagesCleared", 1 << 3);
        var progress = new StageProgress(prefix);
        Assert.That(progress.IsCleared(3), Is.True);
        Assert.That(progress.IsCleared(1), Is.False);
        Assert.That(progress.IsCleared(2), Is.False);
        Assert.That(progress.UnlockedStageCount(5), Is.EqualTo(4));
        Assert.That(new StageSpellUnlocks(progress).MissingStage(DefenseBattle.Spell.ArmorBuff), Is.Zero);
        progress.RecordClear(1);
        PlayerPrefs.SetInt(prefix + "SpellStagesCleared", 0);
        Assert.That(new StageProgress(prefix).IsCleared(1), Is.True);
        Assert.That(new StageProgress(prefix).IsCleared(3), Is.True);
    }

    [TestCase(-1)] [TestCase(0)] [TestCase(31)] [TestCase(32)]
    public void InvalidStageNumbersNeverUnlockOrClear(int stage)
    {
        var progress = new StageProgress(prefix);
        progress.RecordClear(stage);
        Assert.That(progress.IsCleared(stage), Is.False);
        Assert.That(progress.IsUnlocked(stage, 5), Is.False);
        Assert.That(progress.UnlockedStageCount(5), Is.EqualTo(1));
    }

    [Test]
    public void EverySelectableStageHasAFiniteVictoryCondition()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/01_Scenes/02_Gameplay/GameScene.unity");
        try
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var selector in root.GetComponentsInChildren<StageSelectionController>(true))
            {
                var stages = new UnityEditor.SerializedObject(selector).FindProperty("stages");
                Assert.That(stages.arraySize, Is.EqualTo(5));
                for (int i = 0; i < stages.arraySize; i++)
                {
                    var content = (StageContent)stages.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(content, Is.Not.Null);
                    if (content.Data.waves != null)
                    {
                        Assert.That(content.Data.waves.Validate(), Is.Null, content.name);
                        Assert.That(content.Data.waves.waves.Count, Is.GreaterThan(0), content.name);
                    }
                    else Assert.That(content.Data.reinforcementWaveLimit, Is.GreaterThan(0), content.name);
                }
            }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
}
