using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

internal static class Harness
{
    // 저장소의 Assets 폴더. 다른 위치에서 실행하면 BEVD_ASSETS 환경 변수로 지정한다.
    static readonly string Root = (Environment.GetEnvironmentVariable("BEVD_ASSETS") ?? "../../../ProjectBeViolenceDefense/Assets").TrimEnd('/') + "/";
    static Dictionary<string, EnemyFaction> catalog = new();

    static HashSet<int> Snap = new((Environment.GetEnvironmentVariable("SNAP") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
    static int Watch = int.Parse(Environment.GetEnvironmentVariable("WATCH") ?? "-1"), WatchFrom = int.Parse(Environment.GetEnvironmentVariable("FROM") ?? "0"), WatchTo = int.Parse(Environment.GetEnvironmentVariable("TO") ?? "0");
    static Dictionary<string, int> Kinds = new();
    static bool B(EnemyUnit e, string n) => typeof(EnemyUnit).GetProperty(n)?.GetValue(e) is bool b && b;
    static float F(EnemyUnit e, string n) => typeof(EnemyUnit).GetProperty(n)?.GetValue(e) is float f ? f : 0f;
    static int Limit = int.Parse(Environment.GetEnvironmentVariable("LIMIT") ?? "14400");
    static Dictionary<string, int> RevKinds = new();
    static bool Quiet = Environment.GetEnvironmentVariable("QUIET") == "1";
    static System.Reflection.MethodInfo ClearOfUnits = typeof(CombatPositioning).GetMethod("ClearOfUnits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    static EnemyFaction U(string id) => catalog[id];

    static void LoadCatalog()
    {
        string text = System.IO.File.ReadAllText(Root + "04_Data/03_Balance/EnemyCatalog.asset");
        foreach (string block in Regex.Split(text, @"\n  - ").Skip(1))
        {
            string F(string key) { var m = Regex.Match(block, @"(?:^|\n    )" + key + @": ([^\n]*)"); return m.Success ? m.Groups[1].Value.Trim() : null; }
            string id = F("id");
            catalog[id] = new EnemyFaction(id, new Color(1, 1, 1), float.Parse(F("attackRange") ?? "1.5"), int.Parse(F("maxHealth") ?? "50"),
                int.Parse(F("damage") ?? "10"), float.Parse(F("attackInterval") ?? "0.65"), 0, float.Parse(F("awarenessRange") ?? "3"),
                float.Parse(F("moveSpeed") ?? "1.7"), int.Parse(F("armor") ?? "0"), id,
                float.Parse(F("bodyRadius") ?? "0.3") * float.Parse(F("modelScale") ?? "1"));
        }
    }

    static DefenseGrid Stage(string name, params (int x, int y)[] extra)
    {
        string text = System.IO.File.ReadAllText(Root + "04_Data/04_Stages/" + name + ".asset").Replace("\r", "");
        int Int(string p) => int.Parse(Regex.Match(text, p).Groups[1].Value);
        var c = Regex.Match(text, @"\n  core:\n(?:    serializedVersion: \d+\n)?    x: (-?\d+)\n    y: (-?\d+)\n    width: (\d+)\n    height: (\d+)");
        int oi = text.IndexOf("\n  obstacles:", StringComparison.Ordinal);
        var cells = new List<Vector2Int>(); var factions = new List<int>(); var walls = new List<Vector2Int>();
        foreach (Match m in Regex.Matches(text.Substring(0, oi), @"cell: \{x: (-?\d+), y: (-?\d+)\}\n    faction: (-?\d+)"))
        { cells.Add(new Vector2Int(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))); factions.Add(int.Parse(m.Groups[3].Value)); }
        foreach (Match m in Regex.Matches(text.Substring(oi), @"cell: \{x: (-?\d+), y: (-?\d+)\}\n    size: \{x: (\d+), y: (\d+)\}"))
            for (int dx = 0; dx < int.Parse(m.Groups[3].Value); dx++)
                for (int dy = 0; dy < int.Parse(m.Groups[4].Value); dy++)
                    walls.Add(new Vector2Int(int.Parse(m.Groups[1].Value) + dx, int.Parse(m.Groups[2].Value) + dy));
        var grid = new DefenseGrid(Int(@"\n  width: (\d+)"), Int(@"\n  depth: (\d+)"),
            new RectInt(int.Parse(c.Groups[1].Value), int.Parse(c.Groups[2].Value), int.Parse(c.Groups[3].Value), int.Parse(c.Groups[4].Value)),
            cells, factions, walls);
        foreach (var (x, y) in extra)
            if (grid.TryPlace(new Vector2Int(x, y)) != DefenseGrid.BuildResult.Placed) throw new Exception($"wall {x},{y}");
        return grid;
    }

    // 폭 w 인 가로 복도. 양 끝에서 들어와 가운데에서 만나고, 가운데 위쪽 문으로 코어에 간다.
    static DefenseGrid Corridor(int w)
    {
        var walls = new List<Vector2Int>();
        for (int x = 0; x < 17; x++)
            for (int z = 0; z < 17; z++)
            {
                bool lane = z >= 4 && z < 4 + w;
                bool gate = x == 8 && z >= 4 + w && z < 13;
                bool top = z >= 13;
                if (!lane && !gate && !top) walls.Add(new Vector2Int(x, z));
            }
        return new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2), new[] { new Vector2Int(0, 4), new Vector2Int(16, 4) }, new[] { 0, 1 }, walls);
    }

    sealed class Track
    {
        public int LastRevTick = -1000; public Vector3 LastDir; public bool HasDir; public EnemyUnit LastTarget; public Vector3 LastHeading;
        public Queue<Vector3> Window = new(); public float WindowPath; public Queue<float> Steps = new();
    }

    sealed class Result
    {
        public string Name; public int Ticks; public bool Finished; public int Core; public int Units;
        public float SeesNoAttack, NoTarget, Queued, Approach, Engaged, InRangeIdle; // 유닛·초
        public int Reversals, TargetSwitches, HeadingFlips, Rapid; public float Wiggle; public float FirstAllAttack;
        public int Attacks; public float MinClear = float.PositiveInfinity; public bool Crossed;
        public Dictionary<int, int> RevByUnit = new();
    }

    static Result Run(string name, DefenseGrid grid, IReadOnlyList<EnemyFaction>[] queues, bool trace = false)
    {
        int sides = queues.Length;
        var reps = Enumerable.Range(0, sides).Select(i => new EnemyFaction("S" + i, new Color(1, 1, 1))).ToArray();
        var battle = new DefenseBattle(grid, reps, _ => queues);
        var combat = (CombatPositioning)typeof(DefenseBattle).GetField("combat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(battle);
        var tracks = new Dictionary<EnemyUnit, Track>();
        var r = new Result { Name = name };
        const float dt = DefenseBattle.TickDuration;
        battle.StartRound();
        while (battle.CurrentPhase == DefenseBattle.Phase.Combat && battle.TickIndex < Limit)
        {
            battle.Tick(dt, false);
            var es = battle.Enemies;
            if (trace && Watch >= 0 && battle.TickIndex >= WatchFrom && battle.TickIndex <= WatchTo)
                foreach (EnemyUnit e in es)
                    if (e.SpawnOrder == Watch)
                        Console.WriteLine($"  w t{battle.TickIndex} pos {e.Position.x:0.000},{e.Position.z:0.000} prev {e.PreviousPosition.x:0.000},{e.PreviousPosition.z:0.000} route {e.RoutePosition.x:0.0},{e.RoutePosition.z:0.0} tgt {(e.Target?.SpawnOrder ?? -1)} q {(e.CombatQueue?.Target.SpawnOrder ?? -1)} pathTgt {(e.CombatPathTarget?.SpawnOrder ?? -1)} idx {e.CombatPathIndex} retry {e.PathRetryRemaining:0.00} path [{string.Join(" ", e.CombatPath.Select(v => $"{v.x:0.0#},{v.z:0.0#}"))}]");
            if (trace && Snap.Contains(battle.TickIndex))
            {
                Console.WriteLine($"--- snapshot t{battle.TickIndex}");
                foreach (EnemyUnit e in es)
                {
                    string near = string.Join(",", es.Where(o => o.FactionIndex != e.FactionIndex && grid.HasLineOfSight(e.Position, o.Position)).OrderBy(o => Vector3.Distance(o.Position, e.Position)).Take(3).Select(o => $"u{o.SpawnOrder}@{Vector3.Distance(o.Position, e.Position):0.00}"));
                    Console.WriteLine($"   u{e.SpawnOrder,-2} f{e.FactionIndex} {e.Faction.DisplayName,-18} ({e.Position.x,5:0.00},{e.Position.z,5:0.00}) hp{e.Health,3} tgt {(e.Target?.SpawnOrder ?? -1),2} atk {(e.Target != null && combat.CanAttack(e, e.Target) ? "Y" : "-")} q {(e.CombatQueue?.Target.SpawnOrder ?? -1),2} slot {e.AttackSlot,2} dest {(e.CombatDestination.HasValue ? $"{e.CombatDestination.Value.x:0.00},{e.CombatDestination.Value.z:0.00}" : "-"),-11} path {e.CombatPath.Count} route ({e.RoutePosition.x:0.00},{e.RoutePosition.z:0.00}) wp ({e.RouteWaypoint.x:0.0},{e.RouteWaypoint.z:0.0}) rem {e.RemainingRouteWaypoints} yield {(e.RouteYieldDestination.HasValue ? $"{e.RouteYieldDestination.Value.x:0.00},{e.RouteYieldDestination.Value.z:0.00}" : "-")} byp {e.RouteBypassOffset} near {near}");
                }
            }
            for (int i = 0; i < es.Count; i++)
            {
                EnemyUnit e = es[i];
                if (!tracks.TryGetValue(e, out Track t)) { tracks[e] = t = new Track { LastHeading = e.Heading }; r.Units++; }
                r.Crossed |= !e.ReachedCore && !grid.CanPursue(e.PreviousPosition, e.Position);
                for (int j = i + 1; j < es.Count; j++)
                    r.MinClear = Math.Min(r.MinClear, Vector3.Distance(e.Position, es[j].Position) - CombatPositioning.RequiredSpacing(e.Faction, es[j].Faction));
                bool sees = false;
                foreach (EnemyUnit o in es)
                    if (o.FactionIndex != e.FactionIndex && (o.Position - e.Position).sqrMagnitude <= e.Faction.AwarenessRange * e.Faction.AwarenessRange &&
                        grid.HasLineOfSight(e.Position, o.Position)) { sees = true; break; }
                bool attacking = e.Target != null && combat.CanAttack(e, e.Target);
                if (attacking) r.Engaged += dt;
                else
                    foreach (EnemyUnit o in es)
                        if (o.FactionIndex != e.FactionIndex && (o.Position - e.Position).sqrMagnitude <= e.Faction.AttackRange * e.Faction.AttackRange &&
                            grid.HasLineOfSight(e.Position, o.Position) && (bool)ClearOfUnits.Invoke(combat, new object[] { e, o, e.Position, o.Position, true }))
                        { r.InRangeIdle += dt; break; }
                if (sees && !attacking)
                {
                    r.SeesNoAttack += dt;
                    if (e.Target != null) r.Approach += dt; else if (e.CombatQueue != null) r.Queued += dt; else r.NoTarget += dt;
                }
                Vector3 step = e.Position - e.PreviousPosition;
                if (step.sqrMagnitude > 1e-10f)
                {
                    Vector3 dir = step.normalized;
                    if (t.HasDir && Vector3.Dot(dir, t.LastDir) < -0.173648f)
                    {
                        r.Reversals++; r.RevByUnit[e.SpawnOrder] = r.RevByUnit.GetValueOrDefault(e.SpawnOrder) + 1;
                        if (battle.TickIndex - t.LastRevTick <= 30) { r.Rapid++; if (trace && !Quiet) Console.Write("  RAPID"); }
                        t.LastRevTick = battle.TickIndex;
                        string rk = e.Target == null ? (e.CombatQueue != null ? "queue" : e.CombatPath.Count > 0 ? "return-path" : e.RouteYieldDestination.HasValue ? "route-yield" : "route") :
                            e.Target != t.LastTarget ? "new-target" : e.AttackSlot == -2 ? "attacking" : B(e, "CombatLocalSteer") || e.CombatSteerWaypoint.HasValue ? "sidestep" : e.CombatPath.Count > 0 ? "path" : "approach";
                        RevKinds[rk] = RevKinds.GetValueOrDefault(rk) + 1;
                        if (trace && !Quiet) Console.WriteLine($"  t{battle.TickIndex} rev u{e.SpawnOrder}({e.Faction.DisplayName}) pos {e.Position.x:0.00},{e.Position.z:0.00} tgt {(e.Target?.SpawnOrder ?? -1)} q {(e.CombatQueue?.Target.SpawnOrder ?? -1)} slot {e.AttackSlot} steer {(e.CombatSteerWaypoint.HasValue ? 1 : 0)} local {(B(e, "CombatLocalSteer") ? 1 : 0)} ex {(B(e, "LocalSteerExhausted") ? 1 : 0)} path {e.CombatPath.Count} stall {e.CombatStalledTime:0.00} dest {(e.CombatDestination.HasValue ? $"{e.CombatDestination.Value.x:0.00},{e.CombatDestination.Value.z:0.00}" : "-")} yield {(e.RouteYieldDestination.HasValue ? 1 : 0)}");
                    }
                    t.LastDir = dir; t.HasDir = true;
                }
                if (e.Target != null && t.LastTarget != null && e.Target != t.LastTarget && t.LastTarget.IsAlive)
                {
                    r.TargetSwitches++;
                    string kind = e.RetaliationTarget == e.Target && e.RetaliationRemaining > EnemyUnit.RetaliationDuration - 0.2f ? "retaliate" :
                        e.AttackSlot == -2 ? "immediate" : F(e, "RetargetLockRemaining") > 0.95f ? (B(e, "CombatLocalSteer") ? "alt-local" : "alt-complete") : "other";
                    Kinds[kind] = Kinds.GetValueOrDefault(kind) + 1;
                    if (trace && !Quiet) Console.WriteLine($"  t{battle.TickIndex} switch u{e.SpawnOrder} {t.LastTarget.SpawnOrder}->{e.Target.SpawnOrder}");
                }
                if (e.Target != null) t.LastTarget = e.Target;
                if (Vector3.Dot(e.Heading.normalized, t.LastHeading.normalized) < -0.173648f) r.HeadingFlips++;
                t.LastHeading = e.Heading;
                // 1초 창에서 "이동 거리 - 순 변위": 제자리에서 맴돈 거리.
                t.Window.Enqueue(e.Position); float len = step.magnitude; t.Steps.Enqueue(len); t.WindowPath += len;
                if (t.Window.Count > 60) { t.Window.Dequeue(); t.WindowPath -= t.Steps.Dequeue(); }
                if (t.Window.Count == 60 && battle.TickIndex % 60 == 0)
                    r.Wiggle += Math.Max(0f, t.WindowPath - Vector3.Distance(t.Window.Peek(), e.Position));
            }
        }
        r.Ticks = battle.TickIndex; r.Finished = battle.CurrentPhase == DefenseBattle.Phase.Preparation; r.Core = battle.CoreHealth;
        r.Attacks = tracks.Keys.Sum(u => u.AttacksPerformed);
        return r;
    }

    static IReadOnlyList<EnemyFaction>[] Q(params string[] sides) => sides.Select(s =>
    {
        var q = new List<EnemyFaction>();
        foreach (string item in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
        { var kv = item.Trim().Split('*'); for (int i = 0; i < (kv.Length > 1 ? int.Parse(kv[1]) : 1); i++) q.Add(U(kv[0])); }
        return (IReadOnlyList<EnemyFaction>)q;
    }).ToArray();

    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        LoadCatalog();
        catalog["tA"] = new EnemyFaction("A", new Color(1, 1, 1), spawnCount: 0);
        catalog["tB"] = new EnemyFaction("B", new Color(1, 1, 1), spawnCount: 0, maxHealth: 90, damage: 15, attackInterval: 1f, moveSpeed: 1.2f, bodyRadius: 0.48f);
        if (args.Length > 0 && args[0].StartsWith("crowd"))
        {
            int n = int.Parse(args[0].Substring(5));
            foreach (bool walls in new[] { false, true })
            {
                var g = new DefenseGrid();
                if (walls) foreach (var w in new[] { (6, 5), (10, 5), (7, 4), (9, 4) }) g.TryPlace(new Vector2Int(w.Item1, w.Item2));
                Result r = Run("crowd", g, Q($"tA*{n}", $"tB*{n}"), args.Length > 1);
                Console.WriteLine($"crowd{n} walls={walls} ticks {r.Ticks} fin {r.Finished} core {r.Core} engaged {r.Engaged:0.0} rev {r.Reversals} switch {r.TargetSwitches} atk {r.Attacks} minClr {r.MinClear:0.000}");
            }
            return 0;
        }
        string only = args.Length > 0 ? args[0] : null;
        var lineups = new (string, string, string)[]
        {
            ("W1", "goblin-grunt-1500*6", "zombie*2,skeleton*2"),
            ("melee8", "goblin-grunt-1500*8", "skeleton*5,zombie*3"),
            ("mixed", "orc*1,goblin-grunt-1500*3,goblin-slinger*2", "living-armor*1,skeleton*2,skeleton-archer*2"),
            ("big", "orc*3,goblin-grunt-1500*5", "living-armor*2,zombie*4,skeleton*4"),
        };
        var maps = new (string, Func<DefenseGrid>)[]
        {
            ("stage1", () => Stage("Stage1")),
            ("stage1+walls", () => Stage("Stage1", (6, 5), (10, 5))),
            ("default+4walls", () => { var g = new DefenseGrid(); foreach (var w in new[] { (6, 5), (10, 5), (7, 4), (9, 4) }) g.TryPlace(new Vector2Int(w.Item1, w.Item2)); return g; }),
            ("corr1", () => Corridor(1)), ("corr2", () => Corridor(2)), ("corr3", () => Corridor(3)),
        };
        Console.WriteLine("scenario               ticks fin core | engaged sees-noatk  noTgt  queue  appr inRngIdle | rev rapid hflip switch wiggle | atk  minClr cross");
        var total = new Result();
        foreach (var (mapName, map) in maps)
            foreach (var (ln, a, b) in lineups)
            {
                string name = mapName + "/" + ln;
                if (only != null && only != name && only != "all") continue;
                Result r = Run(name, map(), Q(a, b), only != null && only != "all");
                Console.WriteLine($"{name,-22} {r.Ticks,5} {(r.Finished ? "Y" : "N"),3} {r.Core,4} | {r.Engaged,7:0.0} {r.SeesNoAttack,9:0.0} {r.NoTarget,6:0.0} {r.Queued,6:0.0} {r.Approach,5:0.0} {r.InRangeIdle,8:0.0} | {r.Reversals,3} {r.Rapid,5} {r.HeadingFlips,5} {r.TargetSwitches,6} {r.Wiggle,6:0.0} | {r.Attacks,4} {r.MinClear,6:0.000} {(r.Crossed ? "X" : "-")}");
                if (only != null && only != "all") Console.WriteLine("  rev by unit: " + string.Join(" ", r.RevByUnit.OrderBy(k => k.Key).Select(k => $"u{k.Key}:{k.Value}")));
                total.SeesNoAttack += r.SeesNoAttack; total.Engaged += r.Engaged; total.InRangeIdle += r.InRangeIdle; total.Ticks += r.Ticks; total.Core += r.Core; total.NoTarget += r.NoTarget; total.Queued += r.Queued; total.Approach += r.Approach;
                total.Reversals += r.Reversals; total.Rapid += r.Rapid; total.HeadingFlips += r.HeadingFlips; total.TargetSwitches += r.TargetSwitches; total.Wiggle += r.Wiggle; total.Attacks += r.Attacks;
            }
        Console.WriteLine($"{"TOTAL",-22} {total.Ticks,5} {"",3} {total.Core,4} | {total.Engaged,7:0.0} {total.SeesNoAttack,9:0.0} {total.NoTarget,6:0.0} {total.Queued,6:0.0} {total.Approach,5:0.0} {total.InRangeIdle,8:0.0} | {total.Reversals,3} {total.Rapid,5} {total.HeadingFlips,5} {total.TargetSwitches,6} {total.Wiggle,6:0.0} | {total.Attacks,4}");
        Console.WriteLine("reversal kinds: " + string.Join(" ", RevKinds.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}")));
        Console.WriteLine("switch kinds: " + string.Join(" ", Kinds.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}")));
        return 0;
    }
}
