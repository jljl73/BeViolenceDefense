using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 교전이 끝나 경로를 벗어난 유닛은 원래 경로로 되돌아가지 않고 지금 위치에서 코어로 곧장 간다.
public sealed class RerouteAfterCombatTests
{
    // 가운데 세로 벽(x=8)이 맵을 좌우로 나누고, z=3 한 칸만 뚫려 있다. 코어는 위쪽 가운데.
    private static DefenseGrid SplitGrid() => new(17, 17, new RectInt(8, 15, 2, 2),
        new[] { new Vector2Int(2, 2), new Vector2Int(14, 2) }, new[] { 0, 1 },
        Enumerable.Range(0, 13).Where(z => z != 3).Select(z => new Vector2Int(8, z)));

    [Test]
    public void RouteFromStartsHereAndStepsOnlyThroughOpenCells()
    {
        DefenseGrid grid = SplitGrid();
        var start = new Vector3(12.2f, 0, 3.1f);
        Vector3[] route = grid.RouteFrom(start);
        Assert.That(route, Is.Not.Null);
        Assert.That(route[0], Is.EqualTo(start));
        // 마지막 점은 코어 외곽이라 코어에 닿는 것으로 판정된다. 그 앞까지만 본다.
        for (int i = 1; i < route.Length - 1; i++)
            Assert.That(grid.CanPursue(route[i - 1], route[i]), Is.True, $"Step {i} crosses a wall.");
        Assert.That(route.All(point => point.x > 8.5f), Is.True, "The right side has its own way to the core.");
        Assert.That(grid.RouteFrom(new Vector3(8, 0, 6)), Is.Null, "A wall cell has no route.");
    }

    [Test]
    public void SurvivorHeadsToTheCoreFromWhereTheFightEnded()
    {
        DefenseGrid grid = SplitGrid();
        Assert.That(grid.Routes[0].All(point => point.x < 8.5f), Is.True, "The fixture route must run up the left side.");
        var faction = new EnemyFaction("Pursuer", Color.white, spawnCount: 0, maxHealth: 1000);
        var stationary = new EnemyFaction("Target", Color.white, spawnCount: 0, maxHealth: 1000, moveSpeed: 0f);
        var battle = new DefenseBattle(grid, new[] { faction, stationary });
        var survivor = new EnemyUnit(faction, 0, grid.Routes[0], 0);
        var target = new EnemyUnit(stationary, 1, grid.Routes[1], 1);
        // 원래 경로(왼쪽)에서 벗어나 벽 틈 너머 오른쪽에서 싸우는 중이다.
        survivor.MoveOffRoute(new Vector3(12, 0, 3));
        target.MoveOffRoute(new Vector3(13, 0, 3));
        survivor.Target = target;
        battle.StartRound();
        ((List<EnemyUnit>)battle.Enemies).AddRange(new[] { survivor, target });
        for (int tick = 0; tick < 30; tick++)
            battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(survivor.Target, Is.SameAs(target), "The fixture must start with the survivor fighting.");

        int coreHealth = battle.CoreHealth;
        target.Die();
        bool reached = false;
        for (int tick = 0; tick < 3000 && !reached; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            reached = battle.CoreHealth < coreHealth;
            if (reached)
                break;
            Assert.That(survivor.Position.x, Is.GreaterThan(8.5f),
                "The survivor must not walk back through the gap to its old route.");
            Assert.That(grid.CanPursue(survivor.PreviousPosition, survivor.Position), Is.True);
        }
        Assert.That(reached, Is.True);
    }
}
