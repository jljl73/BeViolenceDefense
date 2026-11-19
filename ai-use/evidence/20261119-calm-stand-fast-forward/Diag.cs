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
        public bool EverSeen; public int LastAttacks; public int Health = -1; public int Demotions; public bool WasQueued; public Vector3 LastHeading; public bool HasHeading; public int LastFlipTick = -100000; public EnemyUnit EpTarget; public float EpStart, EpWalk; public bool EpPath, EpSteer, EpHidden; public int BestCore = int.MaxValue; public int BackRun; public int CalmStand; public int RestartFrom = -1; public float RestartWalk; public Vector3[] Hist = new Vector3[181]; public char[] HistState = new char[181]; public float[] HistWalk = new float[181]; public int HistCount; public float Walked;
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

        // 코어까지의 칸 거리(코어 옆 칸 = 1). 벽 배치가 끝난 뒤 한 번 계산한다.
        var coreDist = new Dictionary<Vector2Int, int>(); var frontier = new Queue<Vector2Int>();
        var four = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Depth; y++)
        {
            var c = new Vector2Int(x, y); if (!grid.IsWalkable(c)) continue;
            foreach (var d in four) { var n = c + d; if (grid.InBounds(n) && !grid.IsWalkable(n) && !grid.IsFixedWall(n) && IsCore(grid, n)) { coreDist[c] = 1; frontier.Enqueue(c); break; } }
        }
        while (frontier.Count > 0)
        {
            var c = frontier.Dequeue();
            foreach (var d in four) { var n = c + d; if (coreDist.ContainsKey(n) || !grid.IsWalkable(n)) continue; coreDist[n] = coreDist[c] + 1; frontier.Enqueue(n); }
        }

        string[] trace = (Environment.GetEnvironmentVariable("TRACE") ?? "-1:0:0").Split(':');
        int traceUnit = int.Parse(trace[0]), traceFrom = int.Parse(trace[1]), traceTo = int.Parse(trace[2]);
        // 유닛·틱 분류
        long fight = 0, approachMove = 0, approachStand = 0, queueWait = 0, routeMove = 0, routeBlocked = 0, hidden = 0, seeNoWalk = 0;
        int switchAlive = 0, dropAlive = 0, toQueue = 0, reversals = 0, fastReversals = 0, idle1 = 0, idle3 = 0, acquisitions = 0, detour15 = 0, detour2 = 0;
        long idleTicksInLong = 0; long overlapRest = 0, overlapRestDeep = 0, overlapTransit = 0, enemyOverlap = 0; bool crossedWall = false; int headingFlips = 0, headingFlipsFast = 0; int episodes = 0, long3 = 0, long6 = 0; double extraSum = 0, extraMax = 0, approachWalk = 0; double waitDistSum = 0, waitGapSum = 0; long waitFar = 0, waitFarFree = 0; long awayPath = 0, awaySteer = 0, awayOther = 0, movePath = 0, moveSteer = 0, moveOther = 0, returnPath = 0, offRouteMove = 0; long calmStand = 0; int calm2 = 0, calm4 = 0, calmMax = 0; var calmExamples = new List<string>(); int restarts = 0, restart1 = 0, restart2 = 0, restart4 = 0, restartMax = 0; long restartSum = 0; long marchMove = 0, back1 = 0, back2 = 0; var loops = new Dictionary<char, int> { ['A'] = 0, ['Q'] = 0, ['R'] = 0, ['O'] = 0, ['F'] = 0 }; var loopExamples = new List<string>(); int backEpisodes = 0, backUnits = 0; int ex05 = 0, ex1 = 0, ex2 = 0, ex1Path = 0, ex1Steer = 0, ex1Hidden = 0, ex1Plain = 0; long queueFull = 0, queueNoSlot = 0, nearerIgnored = 0, ignoreVisible = 0; int hits = 0, hitsUnable = 0, hitsQueue = 0, hitsRoute = 0, demotions = 0, promotions = 0;
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
            // 반대 진영이 하나도 없는 틱인지
            bool calm = battle.Enemies.Count > 0;
            for (int i = 1; i < battle.Enemies.Count && calm; i++) calm = battle.Enemies[i].FactionIndex == battle.Enemies[0].FactionIndex;
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
                    // 대기 유닛이 싸움터에서 얼마나 떨어져 서 있는지. ring = 공격 자리 반경 + 자기 몸통 간격.
                    float wd = Vector3.Distance(e.Position, queued.Position);
                    float between = CombatPositioning.RequiredSpacing(e.Faction, queued.Faction);
                    float ring = Mathf.Min(e.Faction.AttackRange, Mathf.Max(e.Faction.AttackRange * 0.85f, between + 0.02f))
                        + CombatPositioning.RequiredSpacing(e.Faction, e.Faction);
                    waitDistSum += wd; waitGapSum += Mathf.Max(0f, wd - ring);
                    if (wd > ring + 0.5f)
                    {
                        waitFar++;
                        Vector3 ahead = e.Position + (queued.Position - e.Position).normalized * 0.4f;
                        bool free = grid.CanPursue(e.Position, ahead);
                        foreach (EnemyUnit o in battle.Enemies)
                        {
                            if (!free) break;
                            if (o == e) continue;
                            free = Vector3.Distance(o.Position, ahead) >= CombatPositioning.RequiredSpacing(e.Faction, o.Faction);
                        }
                        if (free) waitFarFree++;
                    }

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

                // 접근 한 번(같은 타깃을 때릴 수 있게 되기 전까지)에 걸은 거리와 처음 직선 거리의 차이
                if (target != t.EpTarget)
                {
                    CloseEpisode(t);
                    t.EpTarget = target; t.EpWalk = 0f; t.EpPath = t.EpSteer = t.EpHidden = false;
                    if (target != null) t.EpStart = Vector3.Distance(e.PreviousPosition, target.Position);
                }
                if (target != null && !canAttack) { float w = Vector3.Distance(e.Position, e.PreviousPosition); t.EpWalk += w; approachWalk += w; }
                if (target != null && !canAttack && moved)
                {
                    bool viaPath = e.CombatPath.Count > 0 && e.CombatPathTarget == target, viaSteer = e.CombatSteerWaypoint.HasValue;
                    bool unseen = !grid.HasLineOfSight(e.Position, target.Position);
                    t.EpPath |= viaPath; t.EpSteer |= viaSteer; t.EpHidden |= unseen;
                    if (viaPath) movePath++; else if (viaSteer) moveSteer++; else moveOther++;
                    if (Vector3.Dot(e.Position - e.PreviousPosition, target.Position - e.PreviousPosition) < 0f)
                    { if (viaPath) awayPath++; else if (viaSteer) awaySteer++; else awayOther++; }
                }
                // 타깃 없이 코어로 가는 중에 지금까지 가장 가까웠던 칸보다 코어에서 먼 칸에 있는 시간
                if (coreDist.TryGetValue(grid.WorldToCell(e.Position), out int cd))
                {
                    if (target == null && queued == null && moved)
                    {
                        marchMove++;
                        if (t.BestCore != int.MaxValue && cd >= t.BestCore + 1) { back1++; if (t.BackRun++ == 0) backEpisodes++; } else t.BackRun = 0;
                        if (t.BestCore != int.MaxValue && cd >= t.BestCore + 2) back2++;
                    }
                    if (cd < t.BestCore) t.BestCore = cd;
                }
                if (target == null && queued == null && moved && !e.Position.Equals(e.RoutePosition))
                { offRouteMove++; if (e.CombatPath.Count > 0) returnPath++; }


                if (traceUnit == e.SpawnOrder && ticks >= traceFrom && ticks <= traceTo && ticks % 6 == 0)
                    Console.Error.WriteLine($"TR t{ticks} pos ({e.Position.x:0.00},{e.Position.z:0.00}) {(canAttack ? "F" : target != null ? "A" : queued != null ? "Q" : e.Position.Equals(e.RoutePosition) ? "R" : "O")} tgt {(target == null ? "-" : "#" + target.SpawnOrder + $"({target.Position.x:0.0},{target.Position.z:0.0})")} q {(queued == null ? "-" : "#" + queued.SpawnOrder)} slot {e.AttackSlot} dest {(e.CombatDestination is { } dd ? $"({dd.x:0.0},{dd.z:0.0})" : "-")} steer {(e.CombatSteerWaypoint is { } sw ? $"({sw.x:0.0},{sw.z:0.0})" : "-")} path {e.CombatPath.Count} stall {e.CombatStalledTime:0.0} onRoute {e.Position.Equals(e.RoutePosition)} routePos ({e.RoutePosition.x:0.0},{e.RoutePosition.z:0.0}) wp ({e.RouteWaypoint.x:0.0},{e.RouteWaypoint.z:0.0}) left {e.RemainingRouteWaypoints} bypass {e.RouteBypassOffset} yield {(e.RouteYieldDestination is { } yy ? $"({yy.x:0.0},{yy.z:0.0})" : "-")} blk {e.BlockedRouteTime:0.00} near " + string.Join(" ", battle.Enemies.Where(o => o != e).OrderBy(o => Vector3.Distance(o.Position, e.Position)).Take(4).Select(o => $"#{o.SpawnOrder}F{o.FactionIndex}({o.Position.x:0.0},{o.Position.z:0.0}){(o.Position.Equals(o.PreviousPosition) ? "s" : "m")}")));
                // 적이 하나도 없는데 서 있는 시간
                if (calm && !moved)
                {
                    calmStand++; t.CalmStand++;
                    if (t.CalmStand == 120) calm2++;
                    if (t.CalmStand == 240) { calm4++; if (calmExamples.Count < 4) calmExamples.Add($"t{ticks} #{e.SpawnOrder} F{e.FactionIndex} at ({e.Position.x:0.0},{e.Position.z:0.0}) onRoute {e.Position.Equals(e.RoutePosition)} blocked {e.BlockedRouteTime:0.00} wp ({e.RouteWaypoint.x:0.0},{e.RouteWaypoint.z:0.0})"); }
                    if (t.CalmStand > calmMax) calmMax = t.CalmStand;
                }
                else t.CalmStand = 0;
                // 교전(타깃·대기)이 끝난 뒤 0.5를 걸을 때까지 걸린 시간
                bool engaged = target != null || queued != null;
                if (engaged) { t.RestartFrom = -2; }
                else if (t.RestartFrom == -2) { t.RestartFrom = ticks; t.RestartWalk = 0f; }
                if (!engaged && t.RestartFrom >= 0)
                {
                    t.RestartWalk += Vector3.Distance(e.Position, e.PreviousPosition);
                    if (t.RestartWalk >= 0.5f)
                    {
                        int d = ticks - t.RestartFrom; restarts++; restartSum += d;
                        if (d >= 60) restart1++; if (d >= 120) restart2++; if (d >= 240) restart4++; if (d > restartMax) restartMax = d;
                        t.RestartFrom = -1;
                    }
                }

                // 3초 창: 2 넘게 걸었는데 실제로 나아간 거리가 절반 이하면 "맴돌거나 돌아간" 구간으로 센다.
                {
                    char st = canAttack ? 'F' : target != null ? 'A' : queued != null ? 'Q' : e.Position.Equals(e.RoutePosition) ? 'R' : 'O';
                    t.Walked += Vector3.Distance(e.Position, e.PreviousPosition);
                    int slot = t.HistCount % 181;
                    t.Hist[slot] = e.Position; t.HistState[slot] = st; t.HistWalk[slot] = t.Walked; t.HistCount++;
                    if (t.HistCount > 180 && t.HistCount % 30 == 0)
                    {
                        int oldest = (t.HistCount - 181) % 181;
                        float walked = t.Walked - t.HistWalk[oldest], net = Vector3.Distance(e.Position, t.Hist[oldest]);
                        if (walked >= 2f && net <= walked * 0.5f)
                        {
                            var tally = new Dictionary<char, int>();
                            for (int k = 1; k <= 180; k++)
                            {
                                int a1 = (t.HistCount - k) % 181, a0 = (t.HistCount - k - 1 + 181) % 181;
                                if (t.Hist[a1].Equals(t.Hist[a0])) continue;
                                tally[t.HistState[a1]] = tally.GetValueOrDefault(t.HistState[a1]) + 1;
                            }
                            char dom = tally.OrderByDescending(kv => kv.Value).First().Key;
                            loops[dom]++;
                            if (loopExamples.Count < 6)
                                loopExamples.Add($"t{ticks} #{e.SpawnOrder} F{e.FactionIndex} {dom} walked {walked:0.0} net {net:0.0} at ({e.Position.x:0.0},{e.Position.z:0.0}) mix {string.Join(",", tally.Select(kv => kv.Key + ":" + kv.Value))}");
                        }
                    }
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
        foreach (Track t in tracks.Values) CloseEpisode(t);
        void CloseEpisode(Track t)
        {
            if (t.EpTarget == null) return;
            episodes++;
            float extra = (t.EpWalk - t.EpStart) / grid.CellSize;
            if (extra > 0.5f) ex05++;
            if (extra > 1f) { ex1++; if (t.EpPath) ex1Path++; else if (t.EpSteer) ex1Steer++; else if (t.EpHidden) ex1Hidden++; else ex1Plain++; }
            if (extra > 2f) ex2++;
            if (extra > 3f) long3++;
            if (extra > 6f) long6++;
            if (extra > 0f) extraSum += extra;
            if (extra > extraMax) extraMax = extra;
            t.EpTarget = null;
        }

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
            overlapRest, overlapRestDeep, overlapTransit, enemyOverlap, crossedWall ? 1 : 0, battle.CoreHealth, headingFlips, headingFlipsFast,
            episodes, long3, long6, extraSum.ToString("0.0"), extraMax.ToString("0.0"), approachWalk.ToString("0.0"),
            waitDistSum.ToString("0.0"), waitGapSum.ToString("0.0"), waitFar, waitFarFree,
            awayPath, awaySteer, awayOther, movePath, moveSteer, moveOther, returnPath, offRouteMove, ex05, ex1, ex2, ex1Path, ex1Steer, ex1Hidden, ex1Plain, marchMove, back1, back2, backEpisodes,
            loops['A'], loops['Q'], loops['R'], loops['O'], loops['F'],
            calmStand, calm2, calm4, calmMax, restarts, restartSum, restart1, restart2, restart4, restartMax }));
        if (Environment.GetEnvironmentVariable("EXAMPLES") == "1") foreach (string ex in calmExamples) Console.Error.WriteLine("CALM " + (Environment.GetEnvironmentVariable("LABEL") ?? "") + " [" + wallSpec + "] " + spec.Substring(0, Math.Min(30, spec.Length)) + " | " + ex);
        if (Environment.GetEnvironmentVariable("EXAMPLES") == "1") foreach (string ex in loopExamples) Console.Error.WriteLine("EX " + (Environment.GetEnvironmentVariable("LABEL") ?? "") + " [" + wallSpec + "] " + spec.Substring(0, Math.Min(30, spec.Length)) + " | " + ex);
        return 0;
    }

    static bool IsCore(DefenseGrid grid, Vector2Int cell)
    {
        Vector3 c = grid.CorePosition / grid.CellSize; Vector2Int s = grid.CoreSize;
        return Mathf.Abs(cell.x - c.x) <= s.x * 0.5f && Mathf.Abs(cell.y - c.z) <= s.y * 0.5f;
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
