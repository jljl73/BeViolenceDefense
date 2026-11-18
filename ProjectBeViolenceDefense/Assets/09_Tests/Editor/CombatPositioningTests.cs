using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatPositioningTests
{
    [Test]
    public void DefaultGridDoublesCellsWallsAndCoreContact()
    {
        var original = new DefenseGrid(cellSize: 1f);
        var enlarged = new DefenseGrid();
        Assert.That(enlarged.CellSize, Is.EqualTo(2f));
        Assert.That(enlarged.Center, Is.EqualTo(original.Center * 2f));
        Assert.That(enlarged.CorePosition, Is.EqualTo(original.CorePosition * 2f));
        for (int route = 0; route < original.Routes.Count; route++)
        {
            Assert.That(enlarged.Routes[route].Length, Is.EqualTo(original.Routes[route].Length));
            for (int i = 0; i < original.Routes[route].Length; i++)
                Assert.That(enlarged.Routes[route][i], Is.EqualTo(original.Routes[route][i] * 2f));
        }
        var cell = new Vector2Int(5, 7);
        Bounds wall = DefenseGrid.WallBounds(cell);
        Assert.That(wall.center, Is.EqualTo(DefenseGrid.WallBounds(cell, 1f).center * 2f));
        Assert.That(wall.size, Is.EqualTo(DefenseGrid.WallBounds(cell, 1f).size * 2f));
        Assert.That(enlarged.HasLineOfSight(new Vector3(10, 0, 12), new Vector3(10, 0, 16)), Is.False);
        Assert.That(DefenseGrid.PositionToCell(new Vector3(10.8f, 0, 14.8f)), Is.EqualTo(cell));
    }

    [Test]
    public void DoubledGridKeepsUnitSizeSpeedAndRange()
    {
        var grid = new DefenseGrid();
        var faction = new EnemyFaction("Unit", Color.white);
        var unit = new EnemyUnit(faction, 0, grid.Routes[0], 0);
        Assert.That(Vector3.Distance(unit.Position, unit.NextRoutePosition(0.1f)), Is.EqualTo(faction.MoveSpeed * 0.1f).Within(0.0001f));
        Assert.That(faction.BodyRadius, Is.EqualTo(0.3f));
        Assert.That(faction.AttackRange, Is.EqualTo(1.5f));
        var battle = new DefenseBattle(grid, new[] { faction, faction });
        battle.StartRound();
        for (int tick = 0; tick < 14400 && battle.CurrentPhase == DefenseBattle.Phase.Combat; tick++)
            battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BlockedQueueReroutesAndReachesOpponent(bool hasAttacker)
    {
        var attacker = Unit(0, 0, 4, 4, radius: 0.3f);
        var target = Unit(1, 1, 6, 4, radius: 0.3f);
        var units = new List<EnemyUnit> { attacker, target,
            Unit(0, 2, 4.7f, 4, range: 0.1f, radius: 0.3f),
            Unit(0, 3, 4, 4.7f, range: 0.1f, radius: 0.3f),
            Unit(0, 4, 4, 3.3f, range: 0.1f, radius: 0.3f) };
        if (hasAttacker) units.Add(Unit(0, 5, 6, 5.2f, radius: 0.3f));
        var grid = OpenGrid(new Vector2Int(4, 2));
        var combat = new CombatPositioning(grid, units);
        combat.Plan();
        Assert.That(attacker.Target, Is.Null, "The regression must begin in a queue, not in an assigned approach.");
        Assert.That(attacker.CombatQueue?.Target, Is.SameAs(target));
        Assert.That(attacker.CombatQueueReason, Is.EqualTo(EnemyUnit.QueueReason.BlockedApproach));
        bool detoured = false;
        for (int tick = 1; tick <= 600 && !combat.CanAttack(attacker, target); tick++)
        {
            Vector3 previous = attacker.Position;
            StepApproach(combat, attacker, tick);
            detoured |= attacker.CombatDetour != null;
            Assert.That(grid.CanPursue(previous, attacker.Position), Is.True);
            foreach (var other in units.Where(u => u != attacker))
                Assert.That(Vector3.Distance(attacker.Position, other.Position),
                    Is.GreaterThanOrEqualTo(CombatPositioning.RequiredSpacing(attacker.Faction, other.Faction) - 0.0001f));
        }
        Assert.That(detoured, Is.True);
        Assert.That(combat.CanAttack(attacker, target), Is.True, $"Stopped at {attacker.Position}");
        Assert.That(attacker.CombatQueueReason, Is.EqualTo(EnemyUnit.QueueReason.None));
    }

    [Test]
    public void FullTargetQueueWaitsThenUsesVacatedAttackPosition()
    {
        var target = Unit(1, 0, 6, 6);
        var rear = Unit(0, 4, 4, 6);
        var units = new List<EnemyUnit> { target, rear, Unit(0, 1, 5, 6), Unit(0, 2, 6, 5), Unit(0, 3, 6, 7) };
        var combat = new CombatPositioning(OpenGrid(), units);
        combat.Plan();
        Assert.That(rear.CombatQueueReason, Is.EqualTo(EnemyUnit.QueueReason.Capacity));
        for (int tick = 1; tick < 120; tick++)
        {
            StepApproach(combat, rear, tick);
            Assert.That(rear.CombatStalledTime, Is.Zero);
            Assert.That(rear.CombatDetour, Is.Null);
            Assert.That(units.Count(u => u.Target == target), Is.LessThanOrEqualTo(3));
        }
        units.RemoveAt(2);
        combat.Plan();
        Assert.That(rear.Target, Is.SameAs(target));
        Assert.That(rear.CombatQueueReason, Is.EqualTo(EnemyUnit.QueueReason.None));
    }

    [Test]
    public void BlockedQueuePreservesPenaltyAcrossPlanningAndClearsOnTargetLoss()
    {
        var attacker = Unit(0, 0, 4, 4, range: 0.1f);
        var target = Unit(1, 1, 6, 4);
        var combat = new CombatPositioning(OpenGrid(), new[] { attacker, target });
        combat.Plan();
        Assert.That(attacker.CombatQueueReason, Is.EqualTo(EnemyUnit.QueueReason.BlockedApproach));
        attacker.CongestedPoint = new Vector3(4.65f, 0, 4);
        attacker.CongestionRemaining = 1f;
        for (int i = 0; i < 10; i++) combat.Plan();
        Assert.That(attacker.CongestedPoint, Is.Not.Null);
        Assert.That(attacker.CongestionRemaining, Is.EqualTo(1f));
        target.Die();
        combat.Plan();
        Assert.That(attacker.CombatQueue, Is.Null);
        Assert.That(attacker.CombatQueueReason, Is.EqualTo(EnemyUnit.QueueReason.None));
        Assert.That(attacker.CongestedPoint, Is.Null);
    }

    [Test]
    public void StalledUnitFlanksRockAndFrontLineWithoutChangingOpponent()
    {
        var units = BlockedFrontLine(out var attacker, out var opponent);
        var grid = OpenGrid(new Vector2Int(4, 2), new Vector2Int(5, 2));
        var combat = new CombatPositioning(grid, units);
        bool selectedDetour = false;
        bool wentAroundLine = false;
        Vector3[] committed = null;
        int keptPlans = 0;
        for (int tick = 0; tick < 600 && !combat.CanAttack(attacker, opponent); tick++)
        {
            Vector3 previous = attacker.Position;
            StepApproach(combat, attacker, tick);
            if (attacker.CombatDetour != null)
            {
                selectedDetour = true;
                if (committed == attacker.CombatDetour && tick % 8 == 0) keptPlans++;
                committed = attacker.CombatDetour;
            }
            wentAroundLine |= attacker.Position.z > 6f;
            Assert.That(attacker.Target, Is.SameAs(opponent));
            Assert.That(grid.CanPursue(previous, attacker.Position), Is.True);
            foreach (var other in units.Where(u => u != attacker))
                Assert.That(Vector3.Distance(attacker.Position, other.Position),
                    Is.GreaterThanOrEqualTo(CombatPositioning.RequiredSpacing(attacker.Faction, other.Faction) - 0.0001f));
        }
        Assert.That(selectedDetour, Is.True);
        Assert.That(keptPlans, Is.GreaterThan(2), "Do not discard the selected path at every plan.");
        Assert.That(wentAroundLine, Is.True);
        Assert.That(combat.CanAttack(attacker, opponent), Is.True, $"Stopped at {attacker.Position}");
    }

    [Test]
    public void CongestionPenaltyIsPersonalTemporaryAndNotABarrier()
    {
        var first = Unit(0, 0, 4, 4);
        var second = Unit(0, 1, 4, 4);
        var combat = new CombatPositioning(OpenGrid(), new[] { first, second });
        first.CongestedPoint = new Vector3(4.65f, 0, 4);
        first.CongestionRemaining = CombatPositioning.CongestionDuration;
        Vector3 end = new(5, 0, 4);
        Assert.That(CombatPositioning.CongestionCost(first, first.Position, end), Is.GreaterThan(1f));
        Assert.That(float.IsInfinity(CombatPositioning.CongestionCost(first, first.Position, end)), Is.False);
        Assert.That(CombatPositioning.CongestionCost(second, second.Position, end), Is.EqualTo(1f));
        for (int tick = 0; tick < 121; tick++) combat.TrackApproachProgress(first, DefenseBattle.TickDuration);
        Assert.That(first.CongestedPoint, Is.Null);
        Assert.That(CombatPositioning.CongestionCost(first, first.Position, end), Is.EqualTo(1f));
    }

    [Test]
    public void IntentionalCombatQueueDoesNotTriggerRerouting()
    {
        var unit = Unit(0, 0, 4, 4);
        var target = Unit(1, 1, 6, 4);
        unit.CombatQueue = (target, 0);
        unit.CombatQueueReason = EnemyUnit.QueueReason.Capacity;
        var combat = new CombatPositioning(OpenGrid(), new[] { unit, target });
        for (int tick = 0; tick < 120; tick++) combat.TrackApproachProgress(unit, DefenseBattle.TickDuration);
        Assert.That(unit.CombatStalledTime, Is.Zero);
        Assert.That(unit.CongestedPoint, Is.Null);
    }

    [Test]
    public void LosingTargetDiscardsCommittedDetour()
    {
        var units = BlockedFrontLine(out var attacker, out var target);
        var combat = new CombatPositioning(OpenGrid(), units);
        attacker.CombatDetour = new[] { new Vector3(4, 0, 6) };
        attacker.CongestedPoint = new Vector3(4.7f, 0, 4);
        attacker.CongestionRemaining = 2f;
        target.Die();
        combat.Plan();
        Assert.That(attacker.CombatDetour, Is.Null);
        Assert.That(attacker.CongestedPoint, Is.Null);
    }
    // These compact regression layouts reproduce the original steering/slot bugs.
    // Full-battle tests below use the actual gameplay spacing.
    private const float FixtureRadius = 0.19f;
    private const float FixtureSpacing = FixtureRadius * 2f + CombatPositioning.BodyGap;
    private static DefenseGrid OpenGrid(params Vector2Int[] walls) => new(17, 17,
        new RectInt(8, 15, 2, 2), new[] { new Vector2Int(2, 2), new Vector2Int(14, 2) }, new[] { 0, 1 }, walls, cellSize: 1f);

    private static EnemyUnit Unit(int side, int order, float x, float z, float range = 1.5f, float radius = FixtureRadius) =>
        new(new EnemyFaction("Test", Color.white, attackRange: range, awarenessRange: 4f, bodyRadius: radius), side,
            new[] { new Vector3(x, 0, z), new Vector3(x, 0, z + 1) }, order);

    [Test]
    public void ReadyAttackerTakesPriorityOverDistantReservations()
    {
        var target = Unit(1, 4, 6, 6);
        var ready = Unit(0, 3, 6, 4.8f);
        var far = new[] { Unit(0, 0, 8, 6), Unit(0, 1, 6, 8), Unit(0, 2, 4, 6) };
        for (int i = 0; i < far.Length; i++) { far[i].Target = target; far[i].AttackSlot = i * 2; }
        var units = new List<EnemyUnit>(far) { ready, target };
        var combat = new CombatPositioning(OpenGrid(), units);
        combat.Plan();
        Assert.That(combat.CanAttack(ready, target), Is.True, "A distant reservation must not exclude a unit already in striking distance.");
        Assert.That(units.Count(u => u.Target == target), Is.LessThanOrEqualTo(CombatPositioning.MaxAttackersPerTarget));
    }

    [Test]
    public void BlockedApproachSidestepsAndReachesAttackRangeWithoutOverlap()
    {
        var attacker = Unit(0, 0, 4, 4);
        var blocker = Unit(0, 1, 4.45f, 4);
        var target = Unit(1, 2, 6, 4);
        var units = new[] { attacker, blocker, target };
        var combat = new CombatPositioning(OpenGrid(), units);
        attacker.Target = target;
        attacker.CombatDestination = new Vector3(4.725f, 0, 4);
        for (int i = 0; i < 120 && !combat.CanAttack(attacker, target); i++)
        {
            combat.Move(attacker, target, DefenseBattle.TickDuration);
            Assert.That(Vector3.Distance(attacker.Position, blocker.Position), Is.GreaterThanOrEqualTo(FixtureSpacing - 0.0001f));
        }
        Assert.That(combat.CanAttack(attacker, target), Is.True, "The unit should step around the temporary blocker instead of waiting indefinitely.");
    }

    [Test]
    public void WallStillBlocksDetectionAndAttacks()
    {
        var left = Unit(0, 0, 4, 4, 3);
        var right = Unit(1, 1, 6, 4, 3);
        var combat = new CombatPositioning(OpenGrid(new Vector2Int(5, 4)), new[] { left, right });
        combat.Plan();
        Assert.That(left.Target, Is.Null);
        left.Target = right;
        Assert.That(combat.CanAttack(left, right), Is.False);
    }

    [Test]
    public void ReadyFighterStaysPutAndAlternativeTargetIsUsedWhenFull()
    {
        var units = new List<EnemyUnit> { Unit(1, 4, 6, 6), Unit(1, 5, 8, 6) };
        units.AddRange(new[] { Unit(0, 0, 5, 6), Unit(0, 1, 6, 5), Unit(0, 2, 6, 7), Unit(0, 3, 7, 6) });
        var combat = new CombatPositioning(OpenGrid(), units);
        combat.Plan();
        foreach (EnemyUnit unit in units.Where(u => u.Target != null && combat.CanAttack(u, u.Target)))
        {
            Vector3 before = unit.Position;
            combat.Move(unit, unit.Target, DefenseBattle.TickDuration);
            Assert.That(unit.Position, Is.EqualTo(before));
        }
        Assert.That(units[5].Target, Is.EqualTo(units[1]));
    }

    [Test]
    public void EquidistantTargetsUseSpawnOrderInsteadOfListOrder()
    {
        var attacker = Unit(0, 0, 6, 6);
        var first = Unit(1, 1, 6, 5);
        var second = Unit(1, 2, 6, 7);
        var combat = new CombatPositioning(OpenGrid(), new[] { second, first, attacker });
        combat.Plan();
        Assert.That(attacker.Target, Is.EqualTo(first));
    }

    [Test]
    public void LocalSteeringGoesAroundUnitsWithoutCuttingThroughAWall()
    {
        var attacker = Unit(0, 0, 4, 4);
        var blocker = Unit(0, 1, 4.45f, 4);
        var target = Unit(1, 2, 6, 4);
        var grid = OpenGrid(new Vector2Int(4, 5));
        var combat = new CombatPositioning(grid, new[] { attacker, blocker, target });
        attacker.Target = target;
        attacker.CombatDestination = new Vector3(4.725f, 0, 4);
        for (int i = 0; i < 120 && !combat.CanAttack(attacker, target); i++)
        {
            Vector3 previous = attacker.Position;
            combat.Move(attacker, target, DefenseBattle.TickDuration);
            Assert.That(grid.CanPursue(previous, attacker.Position), Is.True);
        }
        Assert.That(combat.CanAttack(attacker, target), Is.True);
    }

    // A stationary front line leaves a safe sideways step but no complete approach to
    // the preferred opponent. Previously the attacker oscillated here indefinitely.
    private static List<EnemyUnit> BlockedFrontLine(out EnemyUnit attacker, out EnemyUnit preferred)
    {
        attacker = Unit(0, 0, 4, 4, radius: 0.3f);
        preferred = Unit(1, 1, 6, 4, radius: 0.3f);
        attacker.Target = preferred;
        var units = new List<EnemyUnit> { attacker, preferred };
        for (int i = 0; i < 5; i++)
            units.Add(Unit(0, 3 + i, 4.7f, 2.6f + i * 0.7f, range: 0.1f, radius: 0.3f));
        return units;
    }

    private static void StepApproach(CombatPositioning combat, EnemyUnit attacker, int tick)
    {
        combat.TrackApproachProgress(attacker, DefenseBattle.TickDuration);
        if (tick % 8 == 0)
            combat.Plan();
        if ((attacker.Target ?? attacker.CombatQueue?.Target) is { } target)
            combat.Move(attacker, target, DefenseBattle.TickDuration);
    }

    [Test]
    public void StalledSidestepsSwitchToReachableNeighborAndStartAttacking()
    {
        var units = BlockedFrontLine(out var attacker, out var preferred);
        var neighbor = Unit(1, 2, 4, 7, radius: 0.3f);
        units.Insert(2, neighbor);
        var combat = new CombatPositioning(OpenGrid(), units);
        int switchedAt = -1;
        int firstAttackAt = -1;
        for (int tick = 0; tick < 240; tick++)
        {
            StepApproach(combat, attacker, tick);
            if (attacker.Target == neighbor)
            {
                if (switchedAt < 0) switchedAt = tick;
                if (combat.CanAttack(attacker, neighbor) && attacker.CanAttack)
                {
                    attacker.Attack(neighbor);
                    if (firstAttackAt < 0) firstAttackAt = tick;
                }
            }
            if (switchedAt >= 0)
                Assert.That(attacker.Target, Is.SameAs(neighbor), "Do not bounce back to the stalled opponent while approaching/fighting the new one.");
            foreach (var other in units.Where(u => u != attacker))
                Assert.That(Vector3.Distance(attacker.Position, other.Position),
                    Is.GreaterThanOrEqualTo(CombatPositioning.RequiredSpacing(attacker.Faction, other.Faction) - 0.0001f));
            attacker.TickTimers(DefenseBattle.TickDuration);
        }
        Assert.That(switchedAt, Is.InRange(36, 95), "Try a same-target detour before falling back to another reachable opponent.");
        Assert.That(firstAttackAt, Is.InRange(0, 119), "The reachable neighbor should be engaged within two seconds.");
        Assert.That(attacker.AttacksPerformed, Is.GreaterThan(1));
    }

    [Test]
    public void StalledUnitWithoutAlternativeResumesWhenFrontLineClears()
    {
        var units = BlockedFrontLine(out var attacker, out var preferred);
        var combat = new CombatPositioning(OpenGrid(), units);
        for (int tick = 0; tick < 120; tick++)
            StepApproach(combat, attacker, tick);
        Assert.That(attacker.Target, Is.SameAs(preferred));
        Assert.That(combat.CanAttack(attacker, preferred), Is.False);
        units.RemoveAll(u => u != attacker && u != preferred);
        for (int tick = 120; tick < 240 && !combat.CanAttack(attacker, preferred); tick++)
            StepApproach(combat, attacker, tick);
        Assert.That(combat.CanAttack(attacker, preferred), Is.True, "A failed retarget probe must not blacklist the only opponent.");
    }

    [TestCase("wall")]
    [TestCase("range")]
    [TestCase("dead")]
    [TestCase("body")]
    public void StalledRetargetStillRequiresVisibleReachableLivingEnemy(string obstruction)
    {
        var units = BlockedFrontLine(out var attacker, out var preferred);
        var neighbor = Unit(1, 2, 4, obstruction == "range" ? 10 : 7,
            radius: obstruction == "body" ? 1.3f : 0.3f);
        units.Insert(2, neighbor);
        if (obstruction == "dead") neighbor.Die();
        var combat = new CombatPositioning(obstruction == "wall" ? OpenGrid(new Vector2Int(4, 6)) : OpenGrid(), units);
        for (int tick = 0; tick < 180; tick++)
        {
            StepApproach(combat, attacker, tick);
            Assert.That(attacker.Target, Is.Not.SameAs(neighbor));
        }
    }

    [Test]
    public void SuccessfulDetourKeepsPreferredTarget()
    {
        var attacker = Unit(0, 0, 4, 4, radius: 0.3f);
        var blocker = Unit(0, 3, 4.7f, 4, range: 0.1f, radius: 0.3f);
        var preferred = Unit(1, 1, 7, 4, radius: 0.3f);
        var neighbor = Unit(1, 2, 4, 7, radius: 0.3f);
        attacker.Target = preferred;
        var combat = new CombatPositioning(OpenGrid(), new[] { attacker, preferred, neighbor, blocker });
        for (int tick = 0; tick < 180 && !combat.CanAttack(attacker, preferred); tick++)
        {
            StepApproach(combat, attacker, tick);
            Assert.That(attacker.Target, Is.SameAs(preferred));
        }
        Assert.That(combat.CanAttack(attacker, preferred), Is.True);
    }

    [Test]
    public void AttackCooldownDoesNotCountAsApproachStall()
    {
        var attacker = Unit(0, 0, 4, 4);
        var preferred = Unit(1, 1, 5.2f, 4);
        var neighbor = Unit(1, 2, 4, 5);
        attacker.Target = preferred;
        attacker.Attack(preferred);
        var combat = new CombatPositioning(OpenGrid(), new[] { attacker, preferred, neighbor });
        for (int tick = 0; tick < 120; tick++)
        {
            StepApproach(combat, attacker, tick);
            Assert.That(attacker.Target, Is.SameAs(preferred));
            Assert.That(attacker.CombatStalledTime, Is.Zero);
        }
    }

    [Test]
    public void FirstWaveRearUnitsDoNotRepeatedlyReverseWhileWithoutTarget()
    {
        // GameScene_LeftCoreHud W1: the user's live reproduction, with catalog values
        // frozen here so later balancing cannot silently remove this traffic regression.
        var battle = new DefenseBattle(new DefenseGrid(cellSize: 1f), new[]
        {
            new EnemyFaction("Goblin", Color.white, maxHealth: 75, damage: 10, attackInterval: 0.65f,
                spawnCount: 4, moveSpeed: 1.7f, bodyRadius: 0.34f),
            new EnemyFaction("Skeleton", Color.white, maxHealth: 70, damage: 12, attackInterval: 0.8f,
                spawnCount: 6, moveSpeed: 1.6f, bodyRadius: 0.286f)
        });
        var headings = new Dictionary<int, Vector3>();
        var reversals = new int[10];
        battle.StartRound();
        while (battle.CurrentPhase == DefenseBattle.Phase.Combat && battle.TickIndex < 2400)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            foreach (var unit in battle.Enemies)
            {
                if (headings.TryGetValue(unit.SpawnOrder, out var previous) &&
                    unit.Target == null && unit.CombatQueue == null &&
                    Vector3.Dot(previous.normalized, unit.Heading.normalized) < -0.173648f)
                    reversals[unit.SpawnOrder]++;
                headings[unit.SpawnOrder] = unit.Heading;
            }
        }
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation));
        Assert.That(reversals.Max(), Is.LessThanOrEqualTo(2),
            "Allow a turn when combat ends, but not repeated >100 degree leave/rejoin turns in the rear queue.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RouteQueueBypassesAnOpenLaneOrWaitsUntilNarrowLaneClears(bool narrow)
    {
        var walls = new List<Vector2Int>();
        if (narrow)
            for (int z = 0; z <= 10; z++) { walls.Add(new Vector2Int(3, z)); walls.Add(new Vector2Int(5, z)); }
        var grid = OpenGrid(walls.ToArray());
        var moving = new EnemyFaction("Mover", Color.white, spawnCount: 0, bodyRadius: 0.3f);
        var stopped = new EnemyFaction("Stopped", Color.white, spawnCount: 0, moveSpeed: 0, bodyRadius: 0.3f);
        var battle = new DefenseBattle(grid, new[] { moving, stopped });
        battle.StartRound();
        var path = Enumerable.Range(2, 11).Select(z => new Vector3(4, 0, z)).ToArray();
        var rear = new EnemyUnit(moving, 0, path, 0);
        var front = new EnemyUnit(stopped, 0, new[] { new Vector3(4, 0, 3), new Vector3(4, 0, 4) }, 1);
        // Isolate queue traffic from spawning/attacks while using the real battle tick.
        var units = (List<EnemyUnit>)battle.Enemies;
        units.Add(rear); units.Add(front);
        Vector3 waitingPosition = default;
        float waitingTravel = 0f;
        for (int tick = 0; tick < 240; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            Assert.That(Vector3.Distance(rear.Position, front.Position), Is.GreaterThanOrEqualTo(0.66f - 0.0001f));
            Assert.That(grid.CanPursue(rear.PreviousPosition, rear.Position), Is.True);
            if (tick == 120) waitingPosition = rear.Position;
            if (tick > 120) waitingTravel += Vector3.Distance(rear.PreviousPosition, rear.Position);
        }
        if (narrow)
        {
            Assert.That(waitingTravel, Is.LessThan(0.01f), "No useful bypass exists: wait without repeatedly stepping out and back.");
            units.Remove(front);
            for (int tick = 0; tick < 120; tick++) battle.Tick(DefenseBattle.TickDuration, false);
            Assert.That(rear.Position.z, Is.GreaterThan(waitingPosition.z + 2f), "Resume forward travel when the lane clears.");
        }
        else
            Assert.That(rear.Position.z, Is.GreaterThan(5f), "An open side lane must lead past the blocker, not back to the old queue position.");
    }

    [Test]
    public void ForwardRouteShortcutKeepsItsChosenWaypointUntilReached()
    {
        var mover = new EnemyFaction("Mover", Color.white, spawnCount: 0);
        var stopped = new EnemyFaction("Stopped", Color.white, spawnCount: 0, moveSpeed: 0);
        var battle = new DefenseBattle(OpenGrid(), new[] { mover, stopped });
        battle.StartRound();
        var rear = new EnemyUnit(mover, 0, new[]
        {
            new Vector3(4, 0, 4), new(5, 0, 4), new(5, 0, 5), new(4, 0, 5),
            new(4, 0, 6), new(4, 0, 7), new(4, 0, 10)
        }, 0);
        var front = new EnemyUnit(stopped, 0, new[] { new Vector3(5, 0, 4), new Vector3(5, 0, 5) }, 1);
        var units = (List<EnemyUnit>)battle.Enemies;
        units.Add(rear); units.Add(front);
        for (int tick = 0; tick < 120; tick++)
            battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(rear.Position.z, Is.GreaterThan(5.5f),
            "A clear shortcut to a later waypoint must not turn back toward the blocked earlier waypoint on the next tick.");
    }

    [Test]
    public void FirstWaveRearUnitsSpreadAndJoinSeveralFights()
    {
        var battle = new DefenseBattle(new DefenseGrid(cellSize: 1f), new[]
        {
            new EnemyFaction("Goblin", Color.white, maxHealth: 75, damage: 10, attackInterval: 0.65f,
                spawnCount: 4, moveSpeed: 1.7f, bodyRadius: 0.34f),
            new EnemyFaction("Skeleton", Color.white, maxHealth: 70, damage: 12, attackInterval: 0.8f,
                spawnCount: 6, moveSpeed: 1.6f, bodyRadius: 0.286f)
        });
        var combat = new CombatPositioning(new DefenseGrid(cellSize: 1f), battle.Enemies);
        var attackers = new HashSet<int>();
        var flankers = new HashSet<int>();
        var flankingSides = new HashSet<int>();
        int firstAttackTick = -1;
        int simultaneous = 0;
        battle.StartRound();
        while (battle.CurrentPhase == DefenseBattle.Phase.Combat && battle.TickIndex < 900 &&
            (firstAttackTick < 0 || battle.TickIndex <= firstAttackTick + 300))
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            int ready = 0;
            foreach (var unit in battle.Enemies)
            {
                if (unit.AttacksPerformed > 0)
                {
                    attackers.Add(unit.SpawnOrder);
                    if (firstAttackTick < 0) firstAttackTick = battle.TickIndex;
                }
                if (unit.Target != null && combat.CanAttack(unit, unit.Target)) ready++;
                if (unit.Target != null && unit.Position.x > 5f && unit.Position.x < 11f && unit.Position.z < 5f)
                {
                    flankers.Add(unit.SpawnOrder);
                    flankingSides.Add(unit.FactionIndex);
                }
            }
            simultaneous = Mathf.Max(simultaneous, ready);
        }
        Assert.That(attackers.Count, Is.GreaterThanOrEqualTo(8), "The rear must join within five seconds of first contact, rather than await a series of duels.");
        Assert.That(simultaneous, Is.GreaterThanOrEqualTo(6), "At least three pairs must be able to fight at once.");
        Assert.That(flankers.Count, Is.GreaterThanOrEqualTo(4));
        Assert.That(flankingSides.Count, Is.EqualTo(2), "Both sides should use the open floor beside the route.");
    }

    [TestCase(4f, true, false, true)]
    [TestCase(4f, false, false, false)]
    [TestCase(1f, true, false, false)]
    [TestCase(4f, true, true, false)]
    public void CombatJoinRequiresNearbyVisibleActualFight(float rearX, bool engaged, bool wall, bool joins)
    {
        var faction = new EnemyFaction("Melee", Color.white, awarenessRange: 2.2f);
        var rear = new EnemyUnit(faction, 0, new[] { new Vector3(rearX, 0, 6), new Vector3(rearX, 0, 7) }, 0);
        var ally = new EnemyUnit(faction, 0, new[] { new Vector3(6, 0, 6), new Vector3(6, 0, 7) }, 1);
        float targetX = engaged ? 7.2f : 7.8f;
        var opponent = new EnemyUnit(faction, 1, new[] { new Vector3(targetX, 0, 6), new Vector3(targetX, 0, 7) }, 2);
        ally.Target = opponent;
        opponent.Target = ally;
        var combat = new CombatPositioning(wall ? OpenGrid(new Vector2Int(5, 6)) : OpenGrid(), new[] { rear, ally, opponent });
        combat.Plan();
        Assert.That(rear.Target == opponent, Is.EqualTo(joins));
    }

    [Test]
    public void FlankingSurvivorCanBypassTrafficUsingForwardRouteConnection()
    {
        var grid = new DefenseGrid(cellSize: 1f);
        foreach (var cell in new[] { new Vector2Int(6, 5), new(10, 5), new(7, 4), new(9, 4) })
            Assert.That(grid.TryPlace(cell), Is.EqualTo(DefenseGrid.BuildResult.Placed));
        var faction = new EnemyFaction("Large", Color.white, spawnCount: 0, moveSpeed: 1.2f, bodyRadius: 0.48f);
        var battle = new DefenseBattle(grid, new[] { faction, faction });
        battle.StartRound();
        var lead = new EnemyUnit(faction, 0, new[]
        {
            new Vector3(9.12f, 0, 6), new(9, 0, 6), new(8, 0, 6), new(8, 0, 7),
            new(8, 0, 8), new(8, 0, 9), new(8, 0, 10), new(8, 0, 14), new(8, 0, 14.5f)
        }, 0);
        lead.MoveOffRoute(new Vector3(7.76f, 0, 5.75f));
        lead.RouteBypassOffset = 5;
        var follower = new EnemyUnit(faction, 0, new[]
        {
            new Vector3(8.76f, 0, 6), new(8, 0, 6), new(8, 0, 7),
            new(8, 0, 8), new(8, 0, 14), new(8, 0, 14.5f)
        }, 1);
        var units = (List<EnemyUnit>)battle.Enemies;
        units.Add(lead); units.Add(follower);
        for (int tick = 0; tick < 240; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            Assert.That(Vector3.Distance(lead.Position, follower.Position), Is.GreaterThanOrEqualTo(1.02f - 0.0001f));
            Assert.That(grid.CanPursue(lead.PreviousPosition, lead.Position), Is.True);
        }
        Assert.That(lead.Position.z, Is.GreaterThan(7f),
            "The old pre-fight route point is behind a wall; the visible forward connection must let the survivor leave the gate.");
    }

    [TestCase(9, false)]
    [TestCase(9, true)]
    [TestCase(20, false)]
    [TestCase(20, true)]
    [TestCase(40, false)]
    [TestCase(40, true)]
    public void CrowdedBattlesFinishWithoutOverlapOrWallPenetration(int count, bool walls)
    {
        var grid = new DefenseGrid(cellSize: 1f);
        if (walls)
            foreach (Vector2Int wall in new[] { new Vector2Int(6, 5), new(10, 5), new(7, 4), new(9, 4) })
                Assert.That(grid.TryPlace(wall), Is.EqualTo(DefenseGrid.BuildResult.Placed));
        var battle = new DefenseBattle(grid, new[]
        {
            new EnemyFaction("A", Color.white, spawnCount: count),
            new EnemyFaction("B", Color.white, spawnCount: count, maxHealth: 90, damage: 15, attackInterval: 1f, moveSpeed: 1.2f, bodyRadius: 0.48f)
        });
        battle.StartRound();
        float minimumClearance = float.PositiveInfinity;
        bool crossedWall = false;
        while (battle.CurrentPhase == DefenseBattle.Phase.Combat && battle.TickIndex < 14400)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                EnemyUnit enemy = battle.Enemies[i];
                crossedWall |= !grid.CanPursue(enemy.PreviousPosition, enemy.Position);
                for (int j = i + 1; j < battle.Enemies.Count; j++)
                    minimumClearance = Mathf.Min(minimumClearance, Vector3.Distance(enemy.Position, battle.Enemies[j].Position) -
                        CombatPositioning.RequiredSpacing(enemy.Faction, battle.Enemies[j].Faction));
            }
        }
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation),
            string.Join("; ", battle.Enemies.Select(e => $"#{e.SpawnOrder} {e.Position} route {e.RouteWaypoint} target {e.Target?.SpawnOrder} detour {e.CombatDetourIndex}/{e.CombatDetour?.Length} stall {e.CombatStalledTime}")));
        Assert.That(minimumClearance, Is.GreaterThanOrEqualTo(-0.0001f));
        Assert.That(crossedWall, Is.False);
    }

    [Test]
    public void ShortAttackRangeDoesNotAllowEnteringAnotherUnitsSpace()
    {
        var attacker = Unit(0, 0, 4, 4, 0.5f, radius: 0.3f);
        var target = Unit(1, 1, 5.3f, 4, radius: 0.48f);
        var combat = new CombatPositioning(OpenGrid(), new[] { attacker, target });
        attacker.Target = target;
        attacker.CombatDestination = target.Position;
        for (int i = 0; i < 120; i++)
        {
            combat.Move(attacker, target, DefenseBattle.TickDuration);
            Assert.That(Vector3.Distance(attacker.Position, target.Position),
                Is.GreaterThanOrEqualTo(CombatPositioning.RequiredSpacing(attacker.Faction, target.Faction) - 0.0001f));
        }
    }

    [TestCase(0.2f, 0.2f)]
    [TestCase(0.2f, 0.5f)]
    [TestCase(0.5f, 0.5f)]
    public void MovementUsesBothUnitsRadii(float firstRadius, float secondRadius)
    {
        float required = firstRadius + secondRadius + CombatPositioning.BodyGap;
        var mover = Unit(0, 0, 4, 4, radius: firstRadius);
        var other = Unit(0, 1, 4 + required + 0.1f, 4, radius: secondRadius);
        var combat = new CombatPositioning(OpenGrid(), new[] { mover, other });
        Assert.That(combat.CanFollowRoute(mover, mover.Position + Vector3.right * 0.09f), Is.True);
        Assert.That(combat.CanFollowRoute(mover, mover.Position + Vector3.right * 0.11f), Is.False);
    }

    [TestCase(2.2f, false)]
    [TestCase(3.2f, true)]
    public void AwarenessControlsFindingDistantEnemies(float awareness, bool detected)
    {
        var faction = new EnemyFaction("Scout", Color.white, awarenessRange: awareness);
        var scout = new EnemyUnit(faction, 0, new[] { new Vector3(4, 0, 4), new Vector3(4, 0, 5) }, 0);
        var enemy = Unit(1, 1, 7, 4);
        var combat = new CombatPositioning(OpenGrid(), new[] { scout, enemy });
        combat.Plan();
        Assert.That(scout.Target == enemy, Is.EqualTo(detected));
    }

    [Test]
    public void AdjacentSpawnsDoNotOverlapLargeUnitsInTheSameTick()
    {
        var grid = new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2),
            new[] { new Vector2Int(2, 2), new Vector2Int(3, 2) }, new[] { 0, 1 }, null, cellSize: 1f);
        var factions = new[]
        {
            new EnemyFaction("A", Color.white, spawnCount: 1, bodyRadius: 0.55f),
            new EnemyFaction("B", Color.white, spawnCount: 1, bodyRadius: 0.55f)
        };
        var battle = new DefenseBattle(grid, factions);
        battle.StartRound();
        for (int i = 0; i < 60 && battle.SpawnCount == 0; i++)
            battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(battle.SpawnCount, Is.EqualTo(1));
        for (int i = 0; i < 300 && battle.SpawnCount < 2; i++)
            battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(battle.SpawnCount, Is.EqualTo(2));
        Assert.That(Vector3.Distance(battle.Enemies[0].Position, battle.Enemies[1].Position),
            Is.GreaterThanOrEqualTo(CombatPositioning.RequiredSpacing(factions[0], factions[1]) - 0.0001f));
    }
}
