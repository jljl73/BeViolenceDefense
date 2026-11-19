using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 교전이 끝나 경로를 벗어난 유닛은 원래 경로로 되돌아가지 않고 지금 위치에서 코어로 곧장 간다.
public sealed class RerouteAfterCombatTests
{
    [TestCase(1f)]
    [TestCase(2f)]
    public void ReturningUnitKeepsMovingWhenAnAllyEntersItsDistantRejoinSegment(float cellSize)
    {
        var walls = new List<Vector2Int>();
        for (int z = 0; z < 14; z++)
        {
            walls.Add(new Vector2Int(3, z));
            walls.Add(new Vector2Int(5, z));
        }
        var grid = new DefenseGrid(10, 17, new RectInt(3, 14, 3, 3),
            new[] { new Vector2Int(4, 0) }, new[] { 0 }, walls, cellSize);
        var mover = new EnemyFaction("Rear", Color.white, spawnCount: 0, moveSpeed: 1.7f, bodyRadius: 0.3f);
        var leader = new EnemyFaction("Front", Color.white, spawnCount: 0, moveSpeed: 1.2f, bodyRadius: 0.3f);
        var battle = new DefenseBattle(grid, new[] { mover });
        battle.StartRound();
        Vector3[] Route(int start) => Enumerable.Range(start, 14 - start)
            .Select(z => new Vector3(4, 0, z) * cellSize)
            .Append(new Vector3(4, 0, 13.5f) * cellSize).ToArray();
        var rear = new EnemyUnit(mover, 0, Route(2), 0);
        rear.MoveOffRoute(rear.Position + Vector3.right * 0.1f);
        var units = (List<EnemyUnit>)battle.Enemies;
        units.Add(rear);
        battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(rear.RouteBypassOffset, Is.GreaterThan(0), "The return must already have selected a distant waypoint.");

        // Another survivor merges far ahead after the return waypoint has been chosen.
        var front = new EnemyUnit(leader, 0, Route(6), 1);
        units.Add(front);
        var combat = new CombatPositioning(grid, units);
        Assert.That(combat.CanFollowRoute(rear, rear.GetRouteWaypoint(rear.RouteBypassOffset)), Is.False);
        Vector3 start = rear.Position;
        for (int tick = 0; tick < 120; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            Assert.That(rear.Position.z, Is.GreaterThan(rear.PreviousPosition.z),
                "Distant traffic must not prevent a safe forward step.");
            Assert.That(grid.CanPursue(rear.PreviousPosition, rear.Position), Is.True);
            Assert.That(rear.Target, Is.Null);
            Assert.That(rear.CombatQueue, Is.Null);
        }
        Assert.That(rear.Position.z - start.z, Is.GreaterThan(3f));

        // Keep following until both reach the core, checking spacing even after catching up.
        for (int tick = 0; tick < 2000 && rear.IsAlive; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            if (front.IsAlive)
                Assert.That(Vector3.Distance(rear.Position, front.Position),
                    Is.GreaterThanOrEqualTo(CombatPositioning.RequiredSpacing(mover, leader) - 0.0001f));
            Assert.That(rear.Position.z, Is.GreaterThanOrEqualTo(rear.PreviousPosition.z - 0.0001f),
                "Resuming the return must not send the survivor backwards.");
            if (rear.IsAlive) // The terminal step deliberately touches the core boundary.
                Assert.That(grid.CanPursue(rear.PreviousPosition, rear.Position), Is.True);
        }
        Assert.That(rear.IsAlive, Is.False);
        Assert.That(battle.CoreHealth, Is.EqualTo(DefenseBattle.CoreMaxHealth - 2));
    }

    // 가운데 세로 벽(x=8)이 맵을 좌우로 나누고, z=3 한 칸만 뚫려 있다. 코어는 위쪽 가운데.
    private static DefenseGrid SplitGrid() => new(17, 17, new RectInt(8, 15, 2, 2),
        new[] { new Vector2Int(2, 2), new Vector2Int(14, 2) }, new[] { 0, 1 },
        Enumerable.Range(0, 13).Where(z => z != 3).Select(z => new Vector2Int(8, z)));

    [Test]
    public void ReturningToFinalApproachDoesNotWaitForADistantAlly()
    {
        var grid = new DefenseGrid(10, 17, new RectInt(3, 14, 3, 3),
            new[] { new Vector2Int(4, 0) }, new[] { 0 }, null);
        var faction = new EnemyFaction("Survivor", Color.white, spawnCount: 0);
        var battle = new DefenseBattle(grid, new[] { faction });
        battle.StartRound();
        var rear = new EnemyUnit(faction, 0, new[] { new Vector3(4, 0, 13), new Vector3(4, 0, 13.5f) }, 0);
        rear.MoveOffRoute(new Vector3(4, 0, 8));
        var front = new EnemyUnit(faction, 0, new[] { new Vector3(4, 0, 11), new Vector3(4, 0, 13.5f) }, 1);
        ((List<EnemyUnit>)battle.Enemies).AddRange(new[] { rear, front });
        for (int tick = 0; tick < 60; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            Assert.That(rear.Position.z, Is.GreaterThan(rear.PreviousPosition.z));
            Assert.That(Vector3.Distance(rear.Position, front.Position),
                Is.GreaterThanOrEqualTo(CombatPositioning.RequiredSpacing(faction, faction) - 0.0001f));
        }
    }

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

        target.Die();
        bool reached = false;
        for (int tick = 0; tick < 3000 && !reached; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            reached = survivor.ReachedCore;
            if (reached)
                break;
            Assert.That(survivor.Position.x, Is.GreaterThan(8.5f),
                "The survivor must not walk back through the gap to its old route.");
            Assert.That(grid.CanPursue(survivor.PreviousPosition, survivor.Position), Is.True);
        }
        Assert.That(reached, Is.True);
    }
}
