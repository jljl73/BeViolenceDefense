using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

// 사용: Diag <StageData.asset> <lineup>   환경: WALLS="x,y;x,y" CATALOG=<EnemyCatalog.asset> SCALE=<칸 크기> LABEL=<맵 이름>
// 전투 한 판을 돌리며 유닛·틱마다 상태를 분류하고 어색한 사건을 센다. 한 줄(TSV)로 출력한다.
internal static class Diag
{
    static readonly Dictionary<string, EnemyFaction> catalog = new();
    static void LoadCatalog(string path)
    {
        string text = System.IO.File.ReadAllText(path);
        foreach (string block in Regex.Split(text, @"\n  - ").Skip(1))
        {
            string F(string key) { var m = Regex.Match(block, @"(?:^|\n    )" + key + @": ([^\n]*)"); return m.Success ? m.Groups[1].Value.Trim() : null; }
            string id = F("id"); if (id == null) continue;
            catalog[id] = new EnemyFaction(id, new Color(1, 1, 1), float.Parse(F("attackRange") ?? "1.5"), int.Parse(F("maxHealth") ?? "50"),
                int.Parse(F("damage") ?? "10"), float.Parse(F("attackInterval") ?? "0.65"), 0, float.Parse(F("awarenessRange") ?? "3"),
                float.Parse(F("moveSpeed") ?? "1.7"), int.Parse(F("armor") ?? "0"), id,
                float.Parse(F("bodyRadius") ?? "0.3") * float.Parse(F("modelScale") ?? "1"));
        }
    }

    static int width, depth; static RectInt core; static readonly List<Vector2Int> cells = new(); static readonly List<int> factions = new(); static readonly List<Vector2Int> walls = new();
    static void LoadStage(string path)
    {
        string text = System.IO.File.ReadAllText(path).Replace("\r", "");
        int Int(string p) => int.Parse(Regex.Match(text, p).Groups[1].Value);
        var c = Regex.Match(text, @"\n  core:\n(?:    serializedVersion: \d+\n)?    x: (-?\d+)\n    y: (-?\d+)\n    width: (\d+)\n    height: (\d+)");
        int oi = text.IndexOf("\n  obstacles:", StringComparison.Ordinal);
        foreach (Match m in Regex.Matches(text.Substring(0, oi), @"cell: \{x: (-?\d+), y: (-?\d+)\}\n    faction: (-?\d+)"))
        { cells.Add(new Vector2Int(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))); factions.Add(int.Parse(m.Groups[3].Value)); }
        foreach (Match m in Regex.Matches(text.Substring(oi), @"cell: \{x: (-?\d+), y: (-?\d+)\}\n    size: \{x: (\d+), y: (\d+)\}"))
            for (int dx = 0; dx < int.Parse(m.Groups[3].Value); dx++)
                for (int dy = 0; dy < int.Parse(m.Groups[4].Value); dy++)
                    walls.Add(new Vector2Int(int.Parse(m.Groups[1].Value) + dx, int.Parse(m.Groups[2].Value) + dy));
        width = Int(@"\n  width: (\d+)"); depth = Int(@"\n  depth: (\d+)");
        core = new RectInt(int.Parse(c.Groups[1].Value), int.Parse(c.Groups[2].Value), int.Parse(c.Groups[3].Value), int.Parse(c.Groups[4].Value));
    }

    sealed class Track
    {
        public EnemyUnit LastTarget; public Vector3 LastMove; public bool HasMove; public int LastReversalTick = -100000;
        public int IdleRun; public int FirstSeen = -1, FirstAttack = -1; public int Faction;
        public bool EverSeen; public int LastAttacks; public int Health = -1; public int Demotions; public bool WasQueued; public Vector3 LastHeading; public bool HasHeading; public int LastFlipTick = -100000;
    }

    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        LoadCatalog(Environment.GetEnvironmentVariable("CATALOG"));
        float scale = float.Parse(Environment.GetEnvironmentVariable("SCALE") ?? "1");
        bool crowd = args[0] == "DEFAULT";
        if (!crowd) LoadStage(args[0]); else { factions.Add(0); factions.Add(1); }
        var grid = crowd ? new DefenseGrid(scale) : new DefenseGrid(width, depth, core, cells, factions, walls, scale);
        if (grid.Routes == null) { Console.WriteLine("ROUTE_BLOCKED"); return 1; }
        string wallSpec = Environment.GetEnvironmentVariable("WALLS") ?? "";
        foreach (string w in wallSpec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        { var p = w.Split(','); if (grid.TryPlace(new Vector2Int(int.Parse(p[0]), int.Parse(p[1]))) != DefenseGrid.BuildResult.Placed) { Console.WriteLine("WALL_REJECTED"); return 1; } }

        string spec = args[1]; int sides = Math.Max(2, factions.Max() + 1);
        var queues = new IReadOnlyList<EnemyFaction>[sides]; var parts = spec.Split('|');
        for (int s = 0; s < sides; s++)
        {
            var q = new List<EnemyFaction>();
            if (!crowd && s < parts.Length) foreach (string item in parts[s].Split(',', StringSplitOptions.RemoveEmptyEntries))
            { var kv = item.Trim().Split('*'); for (int i = 0; i < (kv.Length > 1 ? int.Parse(kv[1]) : 1); i++) q.Add(catalog[kv[0]]); }
            queues[s] = q;
        }
        var reps = Enumerable.Range(0, sides).Select(i => new EnemyFaction("F" + i, new Color(1, 1, 1))).ToArray();
        DefenseBattle battle;
        if (crowd)
        {
            int count = int.Parse(spec.Split(':')[1]);
            battle = new DefenseBattle(grid, new[] { new EnemyFaction("A", new Color(1, 1, 1), spawnCount: count),
                new EnemyFaction("B", new Color(1, 1, 1), spawnCount: count, maxHealth: 90, damage: 15, attackInterval: 1f, moveSpeed: 1.2f, bodyRadius: 0.48f) });
        }
        else battle = new DefenseBattle(grid, reps, _ => queues);
        var combat = (CombatPositioning)typeof(DefenseBattle).GetField("combat", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(battle);
        var tracks = new Dictionary<EnemyUnit, Track>(); var all = new List<EnemyUnit>();
        battle.EnemySpawned += e => { all.Add(e); tracks[e] = new Track { Faction = e.FactionIndex }; };
        battle.StartRound();

        // 유닛·틱 분류
        long fight = 0, approachMove = 0, approachStand = 0, queueWait = 0, routeMove = 0, routeBlocked = 0, hidden = 0, seeNoWalk = 0;
        int switchAlive = 0, dropAlive = 0, toQueue = 0, reversals = 0, fastReversals = 0, idle1 = 0, idle3 = 0, acquisitions = 0, detour15 = 0, detour2 = 0;
        long idleTicksInLong = 0; long overlapRest = 0, overlapRestDeep = 0, overlapTransit = 0, enemyOverlap = 0; bool crossedWall = false; int headingFlips = 0, headingFlipsFast = 0; long queueFull = 0, queueNoSlot = 0, nearerIgnored = 0, ignoreVisible = 0; int hits = 0, hitsUnable = 0, hitsQueue = 0, hitsRoute = 0, demotions = 0, promotions = 0;
        int ticks = 0, limit = 400 * 60;
        for (; ticks < limit && battle.CurrentPhase == DefenseBattle.Phase.Combat; ticks++)
        {
            battle.Tick(DefenseBattle.TickDuration, pauseMovement: false);
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                EnemyUnit u = battle.Enemies[i]; bool uRest = u.Position.Equals(u.PreviousPosition);
                crossedWall |= !grid.CanPursue(u.PreviousPosition, u.Position) && !u.Position.Equals(u.PreviousPosition);
                for (int j = i + 1; j < battle.Enemies.Count; j++)
                {
                    EnemyUnit o = battle.Enemies[j];
                    float spacing = CombatPositioning.RequiredSpacing(u.Faction, o.Faction), d = Vector3.Distance(u.Position, o.Position);
                    if (d >= spacing - 0.001f) continue;
                    if (o.FactionIndex != u.FactionIndex) { enemyOverlap++; continue; }
                    bool rest = uRest && o.Position.Equals(o.PreviousPosition);
                    if (rest) { overlapRest++; if (d < spacing * 0.5f) overlapRestDeep++; } else overlapTransit++;
                }
            }
            foreach (EnemyUnit e in battle.Enemies)
            {
                Track t = tracks[e];
                EnemyUnit target = e.Target, queued = e.CombatQueue?.Target;
                bool moved = !e.Position.Equals(e.PreviousPosition);
                bool canAttack = target != null && combat.CanAttack(e, target);
                EnemyUnit seen = target ?? queued;
                bool idle = false;
                if (canAttack) fight++;
                else if (target != null) { if (moved) approachMove++; else { approachStand++; idle = true; } }
                else if (queued != null) { queueWait++; idle = !moved; }
                else if (moved) routeMove++; else routeBlocked++;
                if (seen != null && !canAttack)
                {
                    bool los = grid.HasLineOfSight(e.Position, seen.Position);
                    if (!los) hidden++;
                    else if (!grid.CanPursue(e.Position, seen.Position)) seeNoWalk++;
                }
                if (queued != null && target == null)
                {
                    int attackers = 0; foreach (EnemyUnit o in battle.Enemies) if (o.Target == queued) attackers++;
                    if (attackers >= CombatPositioning.MaxAttackersPerTarget) queueFull++; else queueNoSlot++;
                }
                // 더 가까운 보이는 적을 두고 먼 타깃에게 접근 중 / 보이는 적을 두고 경로 이동 중
                if (!canAttack)
                {
                    float current = seen != null ? Vector3.Distance(e.Position, seen.Position) : float.PositiveInfinity;
                    bool nearer = false, visible = false;
                    foreach (EnemyUnit o in battle.Enemies)
                    {
                        if (o.FactionIndex == e.FactionIndex || o == seen) continue;
                        float d = Vector3.Distance(e.Position, o.Position);
                        if (d > e.Faction.AwarenessRange || !grid.HasLineOfSight(e.Position, o.Position)) continue;
                        visible = true; if (d < current - 1f) nearer = true;
                    }
                    if (target != null && nearer) nearerIgnored++;
                    if (seen == null && visible) ignoreVisible++;
                }
                if (t.Health >= 0 && e.Health < t.Health)
                {
                    hits++;
                    if (!canAttack) hitsUnable++;
                    if (target == null && queued != null) hitsQueue++;
                    if (seen == null) hitsRoute++;
                }
                t.Health = e.Health;
                bool isQueued = target == null && queued != null;
                if (isQueued && t.LastTarget != null) { demotions++; t.Demotions++; }
                if (t.WasQueued && target != null) promotions++;
                t.WasQueued = isQueued;
                if (seen != null && t.FirstSeen < 0) t.FirstSeen = ticks;
                if (e.AttacksPerformed > 0 && t.FirstAttack < 0) t.FirstAttack = ticks;

                // 타깃 변화
                if (t.LastTarget is { IsAlive: true } old && old != target)
                {
                    if (target != null) switchAlive++;
                    else if (queued == old) toQueue++;
                    else dropAlive++;
                }
                if (target != null && target != t.LastTarget)
                {
                    acquisitions++;
                    float straight = Vector3.Distance(e.Position, target.Position);
                    float path = CellPath(grid, grid.WorldToCell(e.Position), grid.WorldToCell(target.Position)) * grid.CellSize;
                    if (straight >= grid.CellSize && path >= 0)
                    { if (path >= straight * 1.5f) detour15++; if (path >= straight * 2f) detour2++; }
                }
                t.LastTarget = target;

                // 이동 방향 뒤집힘
                if (moved)
                {
                    Vector3 move = (e.Position - e.PreviousPosition).normalized;
                    if (t.HasMove && Vector3.Dot(move, t.LastMove) < -0.1736f) // 100도 초과
                    {
                        reversals++;
                        if (ticks - t.LastReversalTick <= 30) fastReversals++;
                        t.LastReversalTick = ticks;
                    }
                    t.LastMove = move; t.HasMove = true;
                }

                // 바라보는 방향이 100도 넘게 뒤집힘 (밀려서 생긴 위치 떨림은 세지 않는다)
                Vector3 heading = e.Heading.normalized;
                if (t.HasHeading && Vector3.Dot(heading, t.LastHeading) < -0.1736f)
                {
                    headingFlips++;
                    if (ticks - t.LastFlipTick <= 30) headingFlipsFast++;
                    t.LastFlipTick = ticks;
                }
                t.LastHeading = heading; t.HasHeading = true;

                // 적을 두고 서 있는 구간
                if (idle) t.IdleRun++;
                else { Close(t); }
            }
        }
        foreach (Track t in tracks.Values) Close(t);
        void Close(Track t)
        {
            if (t.IdleRun >= 60) { idle1++; idleTicksInLong += t.IdleRun; }
            if (t.IdleRun >= 180) idle3++;
            t.IdleRun = 0;
        }

        bool finished = battle.CurrentPhase != DefenseBattle.Phase.Combat;
        int sawNeverAttackedCore = 0, sawNeverAttackedDead = 0, sawAttacked = 0, neverSaw = 0; var delays = new List<int>();
        foreach (EnemyUnit e in all)
        {
            Track t = tracks[e];
            if (t.FirstSeen < 0) { neverSaw++; continue; }
            if (t.FirstAttack >= 0) { sawAttacked++; delays.Add(t.FirstAttack - t.FirstSeen); }
            else if (e.ReachedCore) sawNeverAttackedCore++;
            else if (!e.IsAlive) sawNeverAttackedDead++;
        }
        delays.Sort();
        float Pct(double p) => delays.Count == 0 ? -1 : delays[(int)Math.Min(delays.Count - 1, Math.Floor(delays.Count * p))] / 60f;
        Console.WriteLine(string.Join("\t", new object[] {
            Environment.GetEnvironmentVariable("LABEL") ?? "", scale, wallSpec.Length == 0 ? "-" : wallSpec, spec, finished ? 1 : 0, ticks, all.Count,
            fight, approachMove, approachStand, queueWait, routeMove, routeBlocked, hidden, seeNoWalk,
            switchAlive, dropAlive, toQueue, reversals, fastReversals, idle1, idle3, idleTicksInLong,
            acquisitions, detour15, detour2, sawAttacked, sawNeverAttackedCore, sawNeverAttackedDead, neverSaw,
            Pct(0.5).ToString("0.00"), Pct(0.9).ToString("0.00"), all.Sum(e => e.AttacksPerformed), all.Count(e => e.ReachedCore),
            queueFull, queueNoSlot, nearerIgnored, ignoreVisible, hits, hitsUnable, hitsQueue, hitsRoute, demotions, promotions, tracks.Values.Count(x => x.Demotions >= 3), tracks.Values.Max(x => x.Demotions),
            overlapRest, overlapRestDeep, overlapTransit, enemyOverlap, crossedWall ? 1 : 0, battle.CoreHealth, headingFlips, headingFlipsFast }));
        return 0;
    }

    // 유닛을 무시한 4방향 칸 거리. 닿을 수 없으면 -1.
    static int CellPath(DefenseGrid grid, Vector2Int from, Vector2Int to)
    {
        if (from == to) return 0;
        var dist = new Dictionary<Vector2Int, int> { [from] = 0 }; var queue = new Queue<Vector2Int>(); queue.Enqueue(from);
        var dirs = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var d in dirs)
            {
                var n = c + d;
                if (dist.ContainsKey(n) || !grid.IsWalkable(n)) continue;
                dist[n] = dist[c] + 1;
                if (n == to) return dist[n];
                queue.Enqueue(n);
            }
        }
        return -1;
    }
}

internal static class GameText
{
    public static string Get(string key) => key;
    public static string Format(string key, params object[] args) => key;
}
