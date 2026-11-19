using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class GameText
{
    public static string Get(string key) => key;
    public static string Format(string key, params object[] values) => key;
}

internal static class Diagnosis
{
    private static void Main(string[] args)
    {
        bool expectFixed = !args.Contains("--expect-original");
        Run(false, expectFixed);
        Run(true, expectFixed);
    }

    private static void Run(bool resetBypass, bool expectFixed)
    {
        var walls = new List<Vector2Int>();
        for (int z = 0; z < 14; z++)
        {
            walls.Add(new Vector2Int(3, z));
            walls.Add(new Vector2Int(5, z));
        }
        var grid = new DefenseGrid(10, 17, new RectInt(3, 14, 3, 3),
            new[] { new Vector2Int(4, 0) }, new[] { 0 }, walls);
        var mover = new EnemyFaction("rear", default, spawnCount: 0, moveSpeed: 1.7f, bodyRadius: 0.3f);
        var leader = new EnemyFaction("front", default, spawnCount: 0, moveSpeed: 1.2f, bodyRadius: 0.3f);
        var battle = new DefenseBattle(grid, new[] { mover });
        battle.StartRound();
        Vector3[] route = Enumerable.Range(2, 12).Select(z => new Vector3(4, 0, z))
            .Append(new Vector3(4, 0, 13.5f)).ToArray();
        var rear = new EnemyUnit(mover, 0, route, 0);
        rear.MoveOffRoute(new Vector3(4.1f, 0, 2));
        var units = (List<EnemyUnit>)battle.Enemies;
        units.Add(rear);
        battle.Tick(DefenseBattle.TickDuration, false);
        Console.WriteLine($"CASE resetBypass={resetBypass}; naturally selected offset={rear.RouteBypassOffset}; position={Pos(rear.Position)}");
        var front = new EnemyUnit(leader, 0,
            Enumerable.Range(6, 8).Select(z => new Vector3(4, 0, z)).Append(new Vector3(4, 0, 13.5f)).ToArray(), 1);
        units.Add(front);
        var combat = new CombatPositioning(grid, units);
        int stoppedWithSafeStep = 0, stopped = 0, transitions = 0;
        bool wasMoving = true;
        for (int tick = 0; tick < 600 && rear.IsAlive; tick++)
        {
            if (resetBypass) rear.RouteBypassOffset = -1;
            Vector3 before = rear.Position;
            Vector3 goal = rear.GetRouteWaypoint(Math.Max(0, rear.RouteBypassOffset));
            bool safeStep = combat.CanFollowRoute(rear, rear.StepToward(goal, DefenseBattle.TickDuration));
            bool safeSegment = combat.CanFollowRoute(rear, goal);
            battle.Tick(DefenseBattle.TickDuration, false);
            bool moving = Vector3.Distance(before, rear.Position) > 0.00001f;
            if (!moving) { stopped++; if (safeStep) stoppedWithSafeStep++; }
            if (moving != wasMoving) transitions++;
            if (tick < 3 || tick % 60 == 0 || moving != wasMoving)
                Console.WriteLine($"t={tick / 60f:F3} rear={Pos(rear.Position)} front={Pos(front.Position)} gap={Vector3.Distance(rear.Position, front.Position):F3} moving={moving} stepClear={safeStep} segmentClear={safeSegment} bypass={rear.RouteBypassOffset} blocked={rear.BlockedRouteTime:F3} target={rear.Target?.SpawnOrder.ToString() ?? "null"} queue={rear.CombatQueue?.Target.SpawnOrder.ToString() ?? "null"}");
            wasMoving = moving;
        }
        Console.WriteLine($"SUMMARY resetBypass={resetBypass}; stoppedTicks={stopped}; stoppedDespiteClearNextStep={stoppedWithSafeStep}; movementTransitions={transitions}; end={Pos(rear.Position)}");
        if (!expectFixed && !resetBypass && stoppedWithSafeStep < 300)
            throw new InvalidOperationException("Expected to reproduce at least five seconds of unnecessary stopping.");
        if ((expectFixed || resetBypass) && stoppedWithSafeStep != 0)
            throw new InvalidOperationException("The diagnostic comparison should remove the unnecessary stopping.");
    }

    private static string Pos(Vector3 p) => $"({p.x:F3},{p.z:F3})";
}
