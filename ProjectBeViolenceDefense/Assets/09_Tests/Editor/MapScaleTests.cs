using NUnit.Framework;
using UnityEngine;

// 맵 크기 실험: 칸 한 변(CellSize)을 키워도 유닛 값은 그대로이고, 전투가 벽을 뚫지 않고 끝난다.
public sealed class MapScaleTests
{
    [Test]
    public void DoubledCellsScaleTheMapButNotTheUnits()
    {
        var small = new DefenseGrid();
        var large = new DefenseGrid(2f);
        Assert.That(large.Center, Is.EqualTo(small.Center * 2f));
        Assert.That(large.CorePosition, Is.EqualTo(small.CorePosition * 2f));
        for (int route = 0; route < small.Routes.Length; route++)
        {
            Assert.That(large.Routes[route].Length, Is.EqualTo(small.Routes[route].Length));
            for (int i = 0; i < small.Routes[route].Length; i++)
                Assert.That(large.Routes[route][i], Is.EqualTo(small.Routes[route][i] * 2f));
        }
        // 벽 칸 (0,7) 은 2배 좌표에서 (0,14) 근처를 막는다.
        Assert.That(large.HasLineOfSight(new Vector3(0, 0, 12), new Vector3(0, 0, 16)), Is.False);
        Assert.That(large.WorldToCell(new Vector3(16.4f, 0, 13.6f)), Is.EqualTo(new Vector2Int(8, 7)));
    }

    [TestCase(9)]
    [TestCase(20)]
    public void BattlesOnADoubledMapFinishWithoutCrossingWalls(int count)
    {
        var grid = new DefenseGrid(2f);
        foreach (Vector2Int wall in new[] { new Vector2Int(6, 5), new(10, 5), new(7, 4), new(9, 4) })
            Assert.That(grid.TryPlace(wall), Is.EqualTo(DefenseGrid.BuildResult.Placed));
        var battle = new DefenseBattle(grid, new[]
        {
            new EnemyFaction("A", Color.white, spawnCount: count),
            new EnemyFaction("B", Color.white, spawnCount: count, maxHealth: 90, damage: 15, attackInterval: 1f, moveSpeed: 1.2f, bodyRadius: 0.48f)
        });
        battle.StartRound();
        bool crossedWall = false;
        int attacks = 0;
        while (battle.CurrentPhase == DefenseBattle.Phase.Combat && battle.TickIndex < 60 * 60 * 8)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            foreach (EnemyUnit enemy in battle.Enemies)
            {
                crossedWall |= !grid.CanPursue(enemy.PreviousPosition, enemy.Position);
                attacks = Mathf.Max(attacks, enemy.AttacksPerformed);
            }
        }
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation));
        Assert.That(crossedWall, Is.False);
        Assert.That(attacks, Is.GreaterThan(0), "The two sides must still meet and fight on the larger map.");
    }
}
