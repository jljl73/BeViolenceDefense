using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
internal static class Rear
{
    static int Main()
    {
        var battle = new DefenseBattle(new DefenseGrid(), new[]
        {
            new EnemyFaction("Goblin", new Color(1,1,1), maxHealth: 75, damage: 10, attackInterval: 0.65f, spawnCount: 4, awarenessRange: 2.2f, moveSpeed: 1.7f, bodyRadius: 0.34f),
            new EnemyFaction("Skeleton", new Color(1,1,1), maxHealth: 70, damage: 12, attackInterval: 0.8f, spawnCount: 6, awarenessRange: 2.2f, moveSpeed: 1.6f, bodyRadius: 0.286f)
        });
        var headings = new Dictionary<int, Vector3>(); var state = new Dictionary<int, string>(); var reversals = new int[10];
        battle.StartRound();
        while (battle.CurrentPhase == DefenseBattle.Phase.Combat && battle.TickIndex < 2400)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            foreach (var u in battle.Enemies)
            {
                string s = u.Target != null ? "T#" + u.Target.SpawnOrder : u.CombatQueue != null ? "Q#" + u.CombatQueue.Value.Target.SpawnOrder : "-";
                if (headings.TryGetValue(u.SpawnOrder, out var prev) && u.Target == null && u.CombatQueue == null && Vector3.Dot(prev.normalized, u.Heading.normalized) < -0.173648f)
                {
                    reversals[u.SpawnOrder]++;
                    Console.WriteLine($"tick {battle.TickIndex} #{u.SpawnOrder} F{u.FactionIndex} pos ({u.Position.x:0.00},{u.Position.z:0.00}) prevState {state.GetValueOrDefault(u.SpawnOrder)} heading ({prev.normalized.x:0.0},{prev.normalized.z:0.0})->({u.Heading.normalized.x:0.0},{u.Heading.normalized.z:0.0}) onRoute {u.Position.Equals(u.RoutePosition)} wp ({u.RouteWaypoint.x:0.0},{u.RouteWaypoint.z:0.0})");
                }
                if (u.SpawnOrder == 8 && battle.TickIndex >= 815 && battle.TickIndex <= 895 && battle.TickIndex % 4 == 0)
                    Console.WriteLine($"   t{battle.TickIndex} #8 {s} pos ({u.Position.x:0.00},{u.Position.z:0.00}) head ({u.Heading.normalized.x:0.0},{u.Heading.normalized.z:0.0}) onRoute {u.Position.Equals(u.RoutePosition)} routePos ({u.RoutePosition.x:0.0},{u.RoutePosition.z:0.0}) wp ({u.RouteWaypoint.x:0.0},{u.RouteWaypoint.z:0.0}) bypass {u.RouteBypassOffset} yield {(u.RouteYieldDestination is { } y ? $"({y.x:0.0},{y.z:0.0})" : "-")} path {u.CombatPath.Count} blocked {u.BlockedRouteTime:0.00} others " + string.Join(" ", battle.Enemies.Where(o => o != u).Select(o => $"#{o.SpawnOrder}({o.Position.x:0.0},{o.Position.z:0.0})")));
                headings[u.SpawnOrder] = u.Heading; state[u.SpawnOrder] = s;
            }
        }
        Console.WriteLine($"phase {battle.CurrentPhase} max {reversals.Max()} [{string.Join(",", reversals)}]");
        return 0;
    }
}
internal static class GameText { public static string Get(string k) => k; public static string Format(string k, params object[] a) => k; }
