using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
// 밀집 전투(기본 지형)를 돌리고 끝나지 않으면 남은 유닛 상태를 출력한다. 사용: Dump <count> <walls 0|1> [scale]
internal static class Dump
{
    static int Main(string[] a)
    {
        int count = int.Parse(a[0]); bool walls = a[1] == "1"; float scale = a.Length > 2 ? float.Parse(a[2]) : 1f;
        var grid = new DefenseGrid(scale);
        if (walls) foreach (var w in new[] { new Vector2Int(6, 5), new Vector2Int(10, 5), new Vector2Int(7, 4), new Vector2Int(9, 4) }) grid.TryPlace(w);
        var battle = new DefenseBattle(grid, new[] { new EnemyFaction("A", new Color(1,1,1), spawnCount: count),
            new EnemyFaction("B", new Color(1,1,1), spawnCount: count, maxHealth: 90, damage: 15, attackInterval: 1f, moveSpeed: 1.2f, bodyRadius: 0.48f) });
        var combat = (CombatPositioning)typeof(DefenseBattle).GetField("combat", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(battle);
        battle.StartRound();
        int lastAttackTick = 0, attacks = 0, ticks = 0;
        for (; ticks < 24000 && battle.CurrentPhase == DefenseBattle.Phase.Combat; ticks++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            int now = battle.Enemies.Sum(e => e.AttacksPerformed);
            if (now != attacks) { attacks = now; lastAttackTick = ticks; }
        }
        Console.WriteLine($"finished={battle.CurrentPhase != DefenseBattle.Phase.Combat} ticks={ticks} units={battle.Enemies.Count} spawned={battle.SpawnCount} lastAttackChangeTick={lastAttackTick} core={battle.CoreHealth}");
        if (battle.CurrentPhase != DefenseBattle.Phase.Combat) return 0;
        foreach (EnemyUnit e in battle.Enemies)
        {
            EnemyUnit t = e.Target, q = e.CombatQueue?.Target;
            Console.WriteLine($"#{e.SpawnOrder} F{e.FactionIndex} pos ({e.Position.x:0.00},{e.Position.z:0.00}) moved {!e.Position.Equals(e.PreviousPosition)} tgt {(t == null ? "-" : "#" + t.SpawnOrder)} canAtk {(t != null && combat.CanAttack(e, t))} queue {(q == null ? "-" : "#" + q.SpawnOrder)} dest {(e.CombatDestination is { } d ? $"({d.x:0.00},{d.z:0.00})" : "-")} slot {e.AttackSlot} stall {e.CombatStalledTime:0.00} idleQ {e.IdleQueueTime:0.00} diseng {e.DisengageRemaining:0.00} routeWp ({e.RouteWaypoint.x:0.0},{e.RouteWaypoint.z:0.0}) onRoute {e.Position.Equals(e.RoutePosition)} blocked {e.BlockedRouteTime:0.00} yield {(e.RouteYieldDestination.HasValue)} hp {e.Health}");
        }
        return 0;
    }
}
internal static class GameText { public static string Get(string k) => k; public static string Format(string k, params object[] a) => k; }
