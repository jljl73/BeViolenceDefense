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

    // 설원·묘지 길 구조 시안: 테마 장식이 맞춰진 틀(크기·코어·스폰)은 그대로 두고, 벽 없이는
    // 두 진영이 각자 길로만 가며, 길목을 막으면 가운데로 돌아가 경로가 길어진다.
    [TestCase("Snowfield", 45, 18, "2,11 14,11", 19, "0,7 1,7 15,7 16,7", 25)]
    [TestCase("Graveyard", 41, 16, "4,12 5,12 11,12 12,12", 17, "4,6 5,6 11,6 12,6", 21)]
    public void SnowfieldAndGraveyardLayoutsKeepTheSceneryFrameAndLengthenRoutesWhenTheirGatesAreWalled(
        string name, int fixedWalls, int openRoute, string upperGates, int upperRoute, string lowerGates, int lowerRoute)
    {
        var original = new DefenseGrid();
        var stage = Load(name);
        var grid = stage.Data.CreateGrid();
        Assert.That(grid.Width, Is.EqualTo(original.Width));
        Assert.That(grid.Depth, Is.EqualTo(original.Depth));
        Assert.That(grid.CorePosition, Is.EqualTo(original.CorePosition));
        Assert.That(grid.CoreSize, Is.EqualTo(original.CoreSize));
        CollectionAssert.AreEqual(original.SpawnCells, grid.SpawnCells);
        CollectionAssert.AreEqual(original.SpawnFactions, grid.SpawnFactions);
        Assert.That(grid.FixedWalls.Count, Is.EqualTo(fixedWalls));
        Assert.That(stage.Data.waves, Is.Null, "The layout draft comes without a wave lineup.");
        Assert.That(RouteLengths(grid), Is.EqualTo(new[] { openRoute, openRoute }));

        Assert.That(RouteLengths(WithWalls(stage, upperGates)), Is.EqualTo(new[] { upperRoute, upperRoute }));
        Assert.That(RouteLengths(WithWalls(stage, lowerGates)), Is.EqualTo(new[] { lowerRoute, lowerRoute }));
    }

    private static int[] RouteLengths(DefenseGrid grid) => grid.Routes.Select(route => route.Length - 2).ToArray();

    private static DefenseGrid WithWalls(StageContent stage, string cells)
    {
        var grid = stage.Data.CreateGrid();
        foreach (string cell in cells.Split(' '))
        {
            string[] xy = cell.Split(',');
            Assert.That(grid.TryPlace(new Vector2Int(int.Parse(xy[0]), int.Parse(xy[1]))), Is.EqualTo(DefenseGrid.BuildResult.Placed), cell);
        }
        return grid;
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
    public void SharedSceneHasOneSelectorWithFiveDistinctStageChoices()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01_Scenes/02_Gameplay/GameScene.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var selectors = roots.SelectMany(r => r.GetComponentsInChildren<StageSelectionController>(true)).ToArray();
            Assert.That(selectors.Length, Is.EqualTo(1));
            var selection = new SerializedObject(selectors[0]);
            var choices = selection.FindProperty("stages");
            // 해안가·단풍·설원·묘지·왕도.
            Assert.That(choices.arraySize, Is.EqualTo(5));
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
