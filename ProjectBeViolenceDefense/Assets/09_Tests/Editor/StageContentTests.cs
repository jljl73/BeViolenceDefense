using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class StageContentTests
{
    private static readonly string[] Names = { "Default", "GoldenMaple", "Snowfield", "Graveyard" };
    private static StageContent Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(
        $"Assets/03_Prefabs/05_Stages/Stage_{name}.prefab").GetComponent<StageContent>();

    [Test]
    public void EveryStageIsSelfContainedWithoutDuplicatingSharedGameplayOrUi()
    {
        foreach (string name in Names)
        {
            StageContent stage = Load(name);
            Assert.That(stage, Is.Not.Null, name);
            Assert.That(stage.Data, Is.Not.Null, name);
            Assert.That(stage.Data.Validate(stage.Data.CreateGrid()), Is.Null, name);
            Assert.That(stage.BoardCamera, Is.Not.Null, name);
            Assert.That(stage.BoardCamera.transform.IsChildOf(stage.transform), Is.True, name);
            Assert.That(stage.GetComponentsInChildren<AudioListener>(true).Length, Is.EqualTo(1), name);
            Assert.That(stage.GetComponentsInChildren<VoxelDefensePrototype>(true), Is.Empty, name);
            Assert.That(stage.GetComponentsInChildren<UIManager>(true), Is.Empty, name);
            Assert.That(stage.GetComponentsInChildren<EventSystem>(true), Is.Empty, name);
            Assert.That(GameText.Get(new SerializedObject(stage).FindProperty("displayNameKey").stringValue), Does.Not.StartWith("stage_select."));
        }
    }

    [Test]
    public void SnowfieldAndGraveyardPreserveTheirPreviousDefaultTerrain()
    {
        var original = new DefenseGrid();
        foreach (string name in Names.Skip(2))
        {
            var stage = Load(name);
            var grid = stage.Data.CreateGrid();
            Assert.That(grid.Width, Is.EqualTo(original.Width));
            Assert.That(grid.Depth, Is.EqualTo(original.Depth));
            Assert.That(grid.CorePosition, Is.EqualTo(original.CorePosition));
            Assert.That(grid.CoreSize, Is.EqualTo(original.CoreSize));
            CollectionAssert.AreEqual(original.SpawnCells, grid.SpawnCells);
            CollectionAssert.AreEqual(original.SpawnFactions, grid.SpawnFactions);
            CollectionAssert.AreEquivalent(original.FixedWalls, grid.FixedWalls);
            Assert.That(stage.Data.waves, Is.Null, "Migration must not invent a wave lineup.");
        }
    }

    [Test]
    public void GoldenMapleLoadsTheStage2LayoutOnTheFrameItsSceneryWasBuiltFor()
    {
        var original = new DefenseGrid();
        var stage = Load("GoldenMaple");
        Assert.That(AssetDatabase.GetAssetPath(stage.Data), Is.EqualTo("Assets/04_Data/04_Stages/Stage2.asset"));
        var grid = stage.Data.CreateGrid();
        Assert.That(grid.Width, Is.EqualTo(original.Width));
        Assert.That(grid.Depth, Is.EqualTo(original.Depth));
        Assert.That(grid.CorePosition, Is.EqualTo(original.CorePosition));
        Assert.That(grid.CoreSize, Is.EqualTo(original.CoreSize));
        CollectionAssert.AreEqual(original.SpawnCells, grid.SpawnCells);
        CollectionAssert.AreEqual(original.SpawnFactions, grid.SpawnFactions);
        Assert.That(grid.FixedWalls.Count, Is.EqualTo(71));
        // Without player walls each side keeps to its own lane, 20 tiles to the core.
        Assert.That(grid.Routes.Select(route => route.Length - 2), Is.EqualTo(new[] { 20, 20 }));
        Assert.That(stage.Data.waves, Is.Null, "The layout draft comes without a wave lineup.");
    }

    [Test]
    public void SharedSceneHasOneSelectorWithFourDistinctStageChoices()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01_Scenes/02_Gameplay/GameScene.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var selectors = roots.SelectMany(r => r.GetComponentsInChildren<StageSelectionController>(true)).ToArray();
            Assert.That(selectors.Length, Is.EqualTo(1));
            var selection = new SerializedObject(selectors[0]);
            var choices = selection.FindProperty("stages");
            Assert.That(choices.arraySize, Is.EqualTo(4));
            var seen = new HashSet<Object>();
            for (int i = 0; i < choices.arraySize; i++)
                Assert.That(seen.Add(choices.GetArrayElementAtIndex(i).objectReferenceValue), Is.True);
            foreach (string reference in new[] { "game", "ui", "popup", "selectionCamera" })
                Assert.That(selection.FindProperty(reference).objectReferenceValue, Is.Not.Null, reference);
            var game = new SerializedObject(selection.FindProperty("game").objectReferenceValue);
            Assert.That(game.FindProperty("waitForStageSelection").boolValue, Is.True);
            Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Count(), Is.EqualTo(1));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
