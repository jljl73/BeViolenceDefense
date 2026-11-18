using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatPositioningTests
{
    // These compact regression layouts reproduce the original steering/slot bugs.
    // Full-battle tests below use the actual gameplay spacing.
    private const float FixtureRadius = 0.19f;
    private const float FixtureSpacing = FixtureRadius * 2f + CombatPositioning.BodyGap;
    private static DefenseGrid OpenGrid(params Vector2Int[] walls) => new(17, 17,
        new RectInt(8, 15, 2, 2), new[] { new Vector2Int(2, 2), new Vector2Int(14, 2) }, new[] { 0, 1 }, walls);

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
    public void CrowdBarrierDetourKeepsTargetAndStartsAttacking()
    {
        var units = BlockedFrontLine(out var attacker, out var preferred);
        units.Insert(2, Unit(1, 2, 4, 7, radius: 0.3f));
        var combat = new CombatPositioning(OpenGrid(), units);
        bool usedPath = false;
        for (int tick = 0; tick < 600; tick++)
        {
            StepApproach(combat, attacker, tick);
            Assert.That(attacker.Target ?? attacker.CombatQueue?.Target, Is.SameAs(preferred));
            usedPath |= attacker.CombatPath.Count > 0;
            if (combat.CanAttack(attacker, preferred) && attacker.CanAttack) attacker.Attack(preferred);
            foreach (var other in units.Where(u => u != attacker))
                Assert.That(Vector3.Distance(attacker.Position, other.Position),
                    Is.GreaterThanOrEqualTo(CombatPositioning.RequiredSpacing(attacker.Faction, other.Faction) - 0.0001f));
            attacker.TickTimers(DefenseBattle.TickDuration);
        }
        Assert.That(usedPath, Is.True, "A long clear approach should replace oscillating local sidesteps.");
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
        var battle = new DefenseBattle(new DefenseGrid(), new[]
        {
            new EnemyFaction("Goblin", Color.white, maxHealth: 75, damage: 10, attackInterval: 0.65f,
                spawnCount: 4, awarenessRange: 2.2f, moveSpeed: 1.7f, bodyRadius: 0.34f),
            new EnemyFaction("Skeleton", Color.white, maxHealth: 70, damage: 12, attackInterval: 0.8f,
                spawnCount: 6, awarenessRange: 2.2f, moveSpeed: 1.6f, bodyRadius: 0.286f)
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
        var battle = new DefenseBattle(new DefenseGrid(), new[]
        {
            new EnemyFaction("Goblin", Color.white, maxHealth: 75, damage: 10, attackInterval: 0.65f,
                spawnCount: 4, awarenessRange: 2.2f, moveSpeed: 1.7f, bodyRadius: 0.34f),
            new EnemyFaction("Skeleton", Color.white, maxHealth: 70, damage: 12, attackInterval: 0.8f,
                spawnCount: 6, awarenessRange: 2.2f, moveSpeed: 1.6f, bodyRadius: 0.286f)
        });
        var combat = new CombatPositioning(new DefenseGrid(), battle.Enemies);
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
        var grid = new DefenseGrid();
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
        var grid = new DefenseGrid();
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
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation));
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

    [TestCase(3f, 2.2f)]
    [TestCase(6f, 3f)]
    public void RangedHitRevealsAttackerAndStartsCounterattackInTheSameTick(float range, float awareness)
    {
        var grid = new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2),
            new[] { new Vector2Int(4, 6), new Vector2Int(4 + (int)range, 6) }, new[] { 0, 1 }, null);
        var battle = new DefenseBattle(grid, new[]
        {
            new EnemyFaction("Victim", Color.white, maxHealth: 150, spawnCount: 1, awarenessRange: awareness),
            new EnemyFaction("Shooter", Color.white, attackRange: range, damage: 4, spawnCount: 1, moveSpeed: 0f)
        });
        EnemyUnit victim = null, shooter = null;
        battle.EnemySpawned += unit => { if (unit.FactionIndex == 0) victim = unit; else shooter = unit; };
        battle.StartRound();
        for (int tick = 0; tick < 60 && (victim == null || victim.Health == victim.MaxHealth); tick++)
            battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(victim, Is.Not.Null);
        Assert.That(victim.Health, Is.LessThan(victim.MaxHealth));
        Assert.That(victim.RetaliationTarget, Is.SameAs(shooter));
        Assert.That(victim.Target, Is.SameAs(shooter), "React before the hit tick's route movement, including outside combat-join range.");
        Assert.That(Vector3.Distance(victim.Position, shooter.Position), Is.LessThan(range));
        for (int tick = 0; tick < 300 && victim.AttacksPerformed == 0; tick++)
            battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(victim.AttacksPerformed, Is.GreaterThan(0), "Recognition must lead to an actual counterattack.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RangedAggroInterruptsPursuitButKeepsAnImmediateFight(bool alreadyFighting)
    {
        var victim = Unit(0, 0, 4, 4);
        var previous = Unit(1, 1, alreadyFighting ? 5.1f : 6.4f, 4);
        var shooter = Unit(1, 2, 4, 7, 3f);
        victim.Target = previous;
        victim.QueueDamage(8, shooter);
        victim.ResolveDamage();
        var combat = new CombatPositioning(OpenGrid(), new[] { victim, previous, shooter });
        combat.Plan();
        Assert.That(victim.Target, Is.SameAs(alreadyFighting ? previous : shooter));
    }

    [TestCase("wall")]
    [TestCase("expired")]
    [TestCase("dead")]
    public void RetaliationRespectsWallsDeathAndMemoryExpiry(string reason)
    {
        var victim = Unit(0, 0, 4, 4);
        var shooter = Unit(1, 1, 10, 4, 6f);
        victim.QueueDamage(8, shooter);
        victim.ResolveDamage();
        if (reason == "dead") shooter.Die();
        if (reason == "expired")
            for (int tick = 0; tick <= 240; tick++) victim.TickTimers(DefenseBattle.TickDuration);
        var combat = new CombatPositioning(reason == "wall" ? OpenGrid(new Vector2Int(7, 4)) : OpenGrid(),
            new[] { victim, shooter });
        combat.Plan();
        Assert.That(victim.Target, Is.Null);
        if (reason == "expired") Assert.That(victim.RetaliationTarget, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SimultaneousArmorBlockedHitsChooseStableAttacker(bool reverse)
    {
        var victim = new EnemyUnit(new EnemyFaction("Armor", Color.white, armor: 30), 0,
            new[] { new Vector3(4, 0, 4), new Vector3(4, 0, 5) }, 0);
        var first = Unit(1, 1, 7, 4, 3f);
        var second = Unit(1, 2, 4, 7, 3f);
        victim.QueueDamage(8, reverse ? second : first);
        victim.QueueDamage(8, reverse ? first : second);
        Assert.That(victim.RetaliationTarget, Is.Null, "Do not mutate targeting while simultaneous attacks are being evaluated.");
        victim.ResolveDamage();
        new CombatPositioning(OpenGrid(), new[] { second, victim, first }).Plan();
        Assert.That(victim.Health, Is.EqualTo(victim.MaxHealth));
        Assert.That(victim.RetaliationTarget, Is.SameAs(first));
        Assert.That(victim.Target, Is.SameAs(first));
    }

    [Test]
    public void RepeatedHitsDoNotUndoAReachableAlternativeOrCauseTargetOscillation()
    {
        var victim = Unit(0, 0, 4, 4);
        var shooter = Unit(1, 1, 7, 4, 3f);
        var reachable = Unit(1, 2, 4, 7);
        victim.QueueDamage(1, shooter);
        victim.ResolveDamage();
        var combat = new CombatPositioning(OpenGrid(), new[] { victim, shooter, reachable });
        combat.Plan();
        Assert.That(victim.Target, Is.SameAs(shooter));
        // The planner has found a reachable neighbor after a stalled approach.
        victim.Target = reachable;
        victim.TickTimers(1f);
        victim.QueueDamage(1, reachable);
        victim.QueueDamage(1, shooter);
        victim.ResolveDamage();
        combat.Plan();
        Assert.That(victim.Target, Is.SameAs(reachable));
        Assert.That(victim.RetaliationTarget, Is.SameAs(shooter));
        Assert.That(victim.RetaliationRemaining, Is.EqualTo(EnemyUnit.RetaliationDuration));
    }

    [Test]
    public void SimultaneousLethalAttacksStillBothLandWithRetaliation()
    {
        var grid = new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2),
            new[] { new Vector2Int(4, 6), new Vector2Int(5, 6) }, new[] { 0, 1 }, null);
        var faction = new EnemyFaction("Duel", Color.white, maxHealth: 10, damage: 10, spawnCount: 1);
        var battle = new DefenseBattle(grid, new[] { faction, faction });
        var spawned = new List<EnemyUnit>();
        battle.EnemySpawned += spawned.Add;
        battle.StartRound();
        for (int tick = 0; tick < 60 && battle.CurrentPhase == DefenseBattle.Phase.Combat; tick++)
            battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(spawned.Count, Is.EqualTo(2));
        Assert.That(spawned.All(unit => !unit.IsAlive && unit.AttacksPerformed == 1), Is.True);
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation));
    }

    [Test]
    public void AdjacentSpawnsDoNotOverlapLargeUnitsInTheSameTick()
    {
        var grid = new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2),
            new[] { new Vector2Int(2, 2), new Vector2Int(3, 2) }, new[] { 0, 1 }, null);
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

    private static DefenseGrid UShapedWall()
    {
        var walls = new List<Vector2Int>();
        for (int z = 3; z <= 9; z++) walls.Add(new Vector2Int(6, z));
        for (int x = 3; x < 6; x++) { walls.Add(new Vector2Int(x, 3)); walls.Add(new Vector2Int(x, 9)); }
        return OpenGrid(walls.ToArray());
    }

    [Test]
    public void KnownTargetBeyondAwarenessIsKeptUntilReached()
    {
        var attacker = Unit(0, 0, 4, 4);
        var target = Unit(1, 1, 6, 4);
        var combat = new CombatPositioning(OpenGrid(), new[] { attacker, target });
        combat.Plan();
        target.MoveOffRoute(new Vector3(13, 0, 4));
        for (int tick = 0; tick < 600 && !combat.CanAttack(attacker, target); tick++)
        {
            StepApproach(combat, attacker, tick);
            attacker.TickTimers(DefenseBattle.TickDuration);
            Assert.That(attacker.Target ?? attacker.CombatQueue?.Target, Is.SameAs(target));
        }
        Assert.That(combat.CanAttack(attacker, target), Is.True);
    }

    [Test]
    public void KnownTargetIsReachedAroundAUWallWithoutLosingItOrCuttingWalls()
    {
        var grid = UShapedWall();
        var attacker = Unit(0, 0, 4, 6, radius: 0.3f);
        var target = Unit(1, 1, 8, 6, radius: 0.3f);
        // An already seen opponent is now occluded while the pursuer is inside the U.
        attacker.Target = target;
        var combat = new CombatPositioning(grid, new[] { attacker, target });
        float travelled = 0f, leftmost = attacker.Position.x;
        bool usedPath = false;
        for (int tick = 0; tick < 1200 && !combat.CanAttack(attacker, target); tick++)
        {
            Vector3 before = attacker.Position;
            StepApproach(combat, attacker, tick);
            attacker.TickTimers(DefenseBattle.TickDuration);
            Assert.That(attacker.Target ?? attacker.CombatQueue?.Target, Is.SameAs(target));
            Assert.That(grid.CanPursue(before, attacker.Position), Is.True);
            Assert.That(Vector3.Distance(attacker.Position, target.Position), Is.GreaterThanOrEqualTo(0.66f - 0.0001f));
            travelled += Vector3.Distance(before, attacker.Position);
            leftmost = Mathf.Min(leftmost, attacker.Position.x);
            usedPath |= attacker.CombatPath.Count > 0;
        }
        Assert.That(usedPath, Is.True);
        Assert.That(leftmost, Is.LessThan(2.5f), "The route must initially move away from the opponent to leave the U.");
        Assert.That(travelled, Is.GreaterThan(10f));
        Assert.That(combat.CanAttack(attacker, target), Is.True);
    }

    [Test]
    public void OccludedMovingTargetDoesNotUpdateItsLastSeenPosition()
    {
        var grid = OpenGrid(Enumerable.Range(2, 10).Select(z => new Vector2Int(6, z)).ToArray());
        var attacker = Unit(0, 0, 4, 6);
        var target = Unit(1, 1, 5, 6);
        var combat = new CombatPositioning(grid, new[] { attacker, target });
        combat.Plan();
        Vector3 seen = target.Position;
        target.MoveOffRoute(new Vector3(8, 0, 6));
        combat.Plan();
        Assert.That(attacker.Target, Is.SameAs(target));
        Assert.That(attacker.LastKnownTargetPosition, Is.EqualTo(seen));
        Assert.That(attacker.CombatDestination, Is.EqualTo(seen));
    }

    [Test]
    public void TrulyUnreachableKnownTargetIsReplacedByReachableOpponent()
    {
        var walls = new List<Vector2Int>();
        for (int x = 5; x <= 7; x++) for (int z = 5; z <= 7; z++)
            if (x != 6 || z != 6) walls.Add(new Vector2Int(x, z));
        var attacker = Unit(0, 0, 3, 6);
        var trapped = Unit(1, 1, 6, 6);
        var reachable = Unit(1, 2, 2, 8);
        attacker.Target = trapped;
        var combat = new CombatPositioning(OpenGrid(walls.ToArray()), new[] { attacker, trapped, reachable });
        for (int tick = 0; tick < 180 && !combat.CanAttack(attacker, reachable); tick++)
        {
            StepApproach(combat, attacker, tick);
            attacker.TickTimers(DefenseBattle.TickDuration);
        }
        Assert.That(attacker.Target, Is.SameAs(reachable));
        Assert.That(combat.CanAttack(attacker, reachable), Is.True);
    }

    [Test]
    public void TargetDeathDuringLongDetourStillAllowsReturningToCoreRoute()
    {
        var grid = UShapedWall();
        var faction = new EnemyFaction("Pursuer", Color.white, spawnCount: 0, maxHealth: 1000);
        var stationary = new EnemyFaction("Target", Color.white, spawnCount: 0, maxHealth: 1000, moveSpeed: 0f);
        var battle = new DefenseBattle(grid, new[] { faction, stationary });
        var attacker = new EnemyUnit(faction, 0, grid.Routes[0], 0);
        var target = new EnemyUnit(stationary, 1, grid.Routes[1], 1);
        attacker.MoveOffRoute(new Vector3(4, 0, 6));
        target.MoveOffRoute(new Vector3(8, 0, 6));
        attacker.Target = target;
        battle.StartRound();
        ((List<EnemyUnit>)battle.Enemies).AddRange(new[] { attacker, target });
        bool usedPath = false;
        for (int tick = 0; tick < 180; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            usedPath |= attacker.CombatPath.Count > 0;
        }
        Assert.That(usedPath, Is.True);
        target.Die();
        for (int tick = 0; tick < 3000 && battle.CurrentPhase == DefenseBattle.Phase.Combat; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            if (!attacker.ReachedCore)
                Assert.That(grid.CanPursue(attacker.PreviousPosition, attacker.Position), Is.True);
        }
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation));
        Assert.That(battle.CoreHealth, Is.EqualTo(119));
    }

    // 폭이 width 칸인 가로 복도. 양 끝에서 들어와 가운데에서 만나고, 가운데 위쪽 문으로 코어에 간다.
    private static DefenseGrid CorridorGrid(int width)
    {
        var walls = new List<Vector2Int>();
        for (int x = 0; x < 17; x++)
            for (int z = 0; z < 13; z++)
                if ((z < 4 || z >= 4 + width) && !(x == 8 && z >= 4 + width))
                    walls.Add(new Vector2Int(x, z));
        return new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2),
            new[] { new Vector2Int(0, 4), new Vector2Int(16, 4) }, new[] { 0, 1 }, walls);
    }

    [TestCase(2)]
    [TestCase(3)]
    public void CorridorFightDoesNotShuffleSidewaysOrBounceBetweenTargets(int width)
    {
        // Unit values are frozen here so later balancing cannot hide this regression.
        var battle = new DefenseBattle(CorridorGrid(width), new[]
        {
            new EnemyFaction("Goblin", Color.white, maxHealth: 75, damage: 10, attackInterval: 0.65f,
                spawnCount: 8, moveSpeed: 1.7f, bodyRadius: 0.34f),
            new EnemyFaction("Skeleton", Color.white, maxHealth: 70, damage: 12, attackInterval: 0.8f,
                spawnCount: 8, moveSpeed: 1.6f, bodyRadius: 0.286f)
        });
        var seen = new Dictionary<EnemyUnit, (Vector3 Direction, int ReversalTick, EnemyUnit Target)>();
        int quickReversals = 0, targetSwitches = 0;
        battle.StartRound();
        while (battle.CurrentPhase == DefenseBattle.Phase.Combat && battle.TickIndex < 7200)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            foreach (EnemyUnit unit in battle.Enemies)
            {
                if (!seen.TryGetValue(unit, out var last))
                    last.ReversalTick = -1000;
                Vector3 step = unit.Position - unit.PreviousPosition;
                if (step.sqrMagnitude > 1e-10f)
                {
                    // A turn of more than 100 degrees that follows another one within half a second.
                    if (last.Direction.sqrMagnitude > 0f && Vector3.Dot(step.normalized, last.Direction) < -0.173648f)
                    {
                        if (battle.TickIndex - last.ReversalTick <= 30)
                            quickReversals++;
                        last.ReversalTick = battle.TickIndex;
                    }
                    last.Direction = step.normalized;
                }
                if (unit.Target != null && last.Target is { IsAlive: true } && unit.Target != last.Target)
                    targetSwitches++;
                if (unit.Target != null)
                    last.Target = unit.Target;
                seen[unit] = last;
            }
        }
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation));
        Assert.That(quickReversals, Is.LessThanOrEqualTo(12),
            "Units without a complete approach must hold their position instead of stepping side to side.");
        Assert.That(targetSwitches, Is.LessThanOrEqualTo(24),
            "A living opponent is only dropped for an immediate attack, a hit, or a complete approach to another one.");
    }

    [Test]
    public void WaitingUnitHoldsItsPositionBehindAFullFight()
    {
        var target = Unit(1, 0, 6, 6, radius: 0.3f);
        var fighters = new[] { Unit(0, 1, 4.9f, 6, radius: 0.3f), Unit(0, 2, 6, 4.9f, radius: 0.3f), Unit(0, 3, 6, 7.1f, radius: 0.3f) };
        var waiting = Unit(0, 4, 4.2f, 6.6f, radius: 0.3f);
        var units = new List<EnemyUnit>(fighters) { waiting, target };
        var combat = new CombatPositioning(OpenGrid(), units);
        Vector3 start = waiting.Position;
        for (int tick = 0; tick < 120; tick++)
        {
            StepApproach(combat, waiting, tick);
            Assert.That(waiting.Position, Is.EqualTo(start), "Backing away and walking in again made waiting units pace.");
        }
        Assert.That(fighters.All(f => combat.CanAttack(f, target)), Is.True);
        Assert.That(waiting.CombatQueue?.Target, Is.SameAs(target));
    }

    [Test]
    public void HiddenOpponentBehindACrowdIsGivenUpInsteadOfBlockingTheLane()
    {
        // A closed one-tile lane along z = 4 that turns up at x = 8; the opponent was seen around that corner.
        var walls = new List<Vector2Int>();
        for (int x = 0; x <= 9; x++) { walls.Add(new Vector2Int(x, 3)); if (x != 8) walls.Add(new Vector2Int(x, 5)); }
        walls.Add(new Vector2Int(9, 4));
        var grid = OpenGrid(walls.ToArray());
        var pursuer = Unit(0, 0, 3, 4, radius: 0.3f);
        var friend = Unit(0, 1, 4, 4, range: 0.1f, radius: 0.3f);
        var hidden = Unit(1, 2, 8, 7, radius: 0.3f);
        pursuer.Target = hidden;
        var combat = new CombatPositioning(grid, new[] { pursuer, friend, hidden });
        Assert.That(grid.HasLineOfSight(pursuer.Position, hidden.Position), Is.False);
        for (int tick = 0; tick < 60; tick++)
            StepApproach(combat, pursuer, tick);
        Assert.That(pursuer.Target, Is.SameAs(hidden), "A short jam is not a reason to forget a known opponent.");
        for (int tick = 60; tick < 240; tick++)
            StepApproach(combat, pursuer, tick);
        Assert.That(pursuer.Target, Is.Null);
        Assert.That(pursuer.PursuitTarget, Is.Null);
    }

    [TestCase(4.5f, true)]
    [TestCase(5f, false)]
    public void IdleUnitOffItsRouteStepsAwayOnlyFromATouchingNeighbor(float neighborX, bool steps)
    {
        var idle = Unit(0, 0, 4, 4);
        var neighbor = Unit(0, 1, neighborX, 4);
        var combat = new CombatPositioning(OpenGrid(), new[] { idle, neighbor });
        Assert.That(combat.TryStepAside(idle, out Vector3 destination), Is.EqualTo(steps));
        if (steps)
            Assert.That(destination.x, Is.LessThan(idle.Position.x), "It gives way on the side away from the neighbor.");
    }

    // Rows from z = 16 down to z = 0. '#' is a fixed wall.
    private static DefenseGrid MapGrid(Vector2Int[] spawns, params string[] rows)
    {
        var walls = new List<Vector2Int>();
        for (int i = 0; i < rows.Length; i++)
            for (int x = 0; x < rows[i].Length; x++)
                if (rows[i][x] == '#') walls.Add(new Vector2Int(x, rows.Length - 1 - i));
        return new DefenseGrid(17, 17, new RectInt(7, 14, 3, 3), spawns, new[] { 0, 1 }, walls);
    }

    private static EnemyFaction Kind(string name, int health, int damage, float interval, float speed, float radius,
        bool shooter = false) =>
        new(name, Color.white, attackRange: shooter ? 3f : 1.5f, maxHealth: health, damage: damage, attackInterval: interval,
            spawnCount: 0, awarenessRange: shooter ? 4.3f : 3f, moveSpeed: speed, bodyRadius: radius);

    private static IReadOnlyList<EnemyFaction>[] Lineup(params (EnemyFaction Unit, int Count)[][] sides) => sides
        .Select(side => (IReadOnlyList<EnemyFaction>)side.SelectMany(e => Enumerable.Repeat(e.Unit, e.Count)).ToList())
        .ToArray();

    private static (bool Finished, int Attacks, int ReachedCore, int Spawned) RunToEnd(DefenseGrid grid,
        IReadOnlyList<EnemyFaction>[] lineup, int tickLimit)
    {
        var battle = new DefenseBattle(grid, new[] { new EnemyFaction("A", Color.white), new EnemyFaction("B", Color.white) },
            _ => lineup);
        var all = new List<EnemyUnit>();
        battle.EnemySpawned += all.Add;
        battle.StartRound();
        while (battle.CurrentPhase == DefenseBattle.Phase.Combat && battle.TickIndex < tickLimit)
            battle.Tick(DefenseBattle.TickDuration, false);
        return (battle.CurrentPhase != DefenseBattle.Phase.Combat, all.Sum(e => e.AttacksPerformed),
            all.Count(e => e.ReachedCore), all.Count);
    }

    [Test]
    public void CoreBlocksDetectionAndAttacksLikeAWall()
    {
        // OpenGrid's core covers x 7.5..9.5, z 14.5..16.5.
        var left = Unit(0, 0, 6.8f, 15.5f, 4);
        var right = Unit(1, 1, 10.2f, 15.5f, 4);
        var below = Unit(1, 2, 6.8f, 13.2f, 4);
        var grid = OpenGrid();
        var combat = new CombatPositioning(grid, new[] { left, right, below });
        combat.Plan();
        Assert.That(right.Target, Is.Not.SameAs(left), "The core must hide units on its far side.");
        Assert.That(combat.CanAttack(left, right), Is.False, "Shots do not pass through the core.");
        Assert.That(left.Target, Is.SameAs(below), "Units on the same side of the core still see each other.");
        Assert.That(grid.HasLineOfSight(new Vector3(7.2f, 0, 14.2f), new Vector3(9.8f, 0, 14.2f)), Is.True,
            "A line along a face of the core is open.");
    }

    [Test]
    public void ShootersOnOppositeFacesOfTheCoreWalkInInsteadOfWaitingOnEachOther()
    {
        // Two one-tile lanes that end on opposite faces of the core and meet only along the bottom
        // row. The leaders stand within awareness of each other with the core in between; the only
        // way round is through their own column, and nowhere in a lane is close enough to shoot from.
        var grid = MapGrid(new[] { new Vector2Int(4, 2), new Vector2Int(12, 2) },
            "#######...#######",
            "###...........###",
            "####.##...##.####",
            "####.#######.####", "####.#######.####", "####.#######.####", "####.#######.####",
            "####.#######.####", "####.#######.####", "####.#######.####", "####.#######.####",
            "####.#######.####", "####.#######.####", "####.#######.####", "####.#######.####",
            "####.#######.####",
            "####.........####");
        Assert.That(grid.Routes, Is.Not.Null);
        var shooter = Kind("shooter", 55, 8, 0.8f, 1.7f, 0.3f, shooter: true);
        var result = RunToEnd(grid, Lineup(new[] { (shooter, 3) }, new[] { (shooter, 3) }), 60 * 60);
        Assert.That(result.Finished, Is.True, "Seeing each other through the core held both columns for good.");
        Assert.That(result.ReachedCore, Is.EqualTo(result.Spawned));
        Assert.That(result.Attacks, Is.Zero);
    }

    [Test]
    public void CrowdArrivingOnBothSidesOfTheCoreFinishes()
    {
        // Side lanes that reach the core from the left and the right, joined by a one-tile band below it.
        var grid = MapGrid(new[] { new Vector2Int(2, 2), new Vector2Int(14, 2) },
            ".................", ".................", ".................",
            ".....#.....#.....",
            "###.#########.###",
            "....#.......#....",
            "##.....###.....##",
            "....#.......#....",
            "..######.######..",
            "....#.......#....",
            "##...#.###.#...##",
            "....#.......#....",
            "..######.######..",
            "....#.......#....",
            "....#.......#....",
            ".......###.......",
            "....#.......#....");
        var result = RunToEnd(grid, Lineup(
            new[]
            {
                (Kind("orc", 210, 22, 1.1f, 1.5f, 0.4f), 3), (Kind("grunt", 75, 10, 0.65f, 1.7f, 0.34f), 8),
                (Kind("slinger", 55, 8, 0.8f, 1.7f, 0.28f * 1.2f, shooter: true), 4)
            },
            new[]
            {
                (Kind("skeleton", 70, 12, 0.8f, 1.6f, 0.22f * 1.3f), 8), (Kind("necromancer", 60, 8, 1.2f, 1.4f, 0.29f, shooter: true), 1),
                (Kind("archer", 55, 10, 1f, 1.6f, 0.3f, shooter: true), 4), (Kind("zombie", 135, 15, 1f, 1.2f, 0.24f * 1.3f), 3)
            }), 120 * 60);
        Assert.That(result.Finished, Is.True, "Shooters waiting on each other across the core held the whole column.");
    }
}
