// 실제 DefenseBattle/CombatPositioning/DefenseGrid 코드로 고블린 vs 좀비 교전을 반복 실행해 결과를 집계한다.
// 마법·힐 없이 두 진영만 싸우게 해서, 플레이어 개입 전 기본 전력이 비슷한지 확인하는 용도다.
// 사용법은 README.md 참고.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

internal static class Program
{
    private const float Step = 1f / 60f;

    // 지형: MAP 환경 변수(17줄 글자 맵 파일)가 있으면 그것을, 없으면 GameScene 의 boardLayout 을 쓴다. 둘 다 없으면 기본 지형.
    private static readonly List<Vector2Int> Board = LoadBoard();

    private static List<Vector2Int> LoadBoard()
    {
        string text = null;
        string mapPath = Environment.GetEnvironmentVariable("MAP");
        if (mapPath != null)
            text = System.IO.File.ReadAllText(mapPath);
        else if (System.IO.File.Exists("../ProjectBeViolenceDefense/Assets/01_Scenes/02_Gameplay/GameScene.unity"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                System.IO.File.ReadAllText("../ProjectBeViolenceDefense/Assets/01_Scenes/02_Gameplay/GameScene.unity"),
                "\n  boardLayout: \"([^\"]*)\"");
            if (m.Success) text = System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value);
        }
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DefenseGrid.TryParseLayout(text, out List<Vector2Int> walls, out string error)) return walls;
        Console.WriteLine($"지형 오류: {error}. 기본 지형을 씁니다.");
        return null;
    }
    private const int TickLimit = 240 * 60;
    private static readonly float Range = float.Parse(Environment.GetEnvironmentVariable("RANGE") ?? "1.5");

    private record Stats(int Hp, int Dmg, float Interval, int Count)
    {
        public float Dps => Dmg / Interval;
        public override string ToString() => $"HP {Hp,3} / 공격 {Dmg,2} / {Interval:0.00}s (DPS {Dps,5:0.0}) x{Count}";
    }

    private record Outcome(bool Engaged, int GoblinLeft, int ZombieLeft, int GoblinHpLeft, int ZombieHpLeft, float FightTime);

    private static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var layouts = BuildLayouts(int.Parse(Environment.GetEnvironmentVariable("LAYOUTS") ?? "200"));
        // WALLS="x,y;x,y" 이면 무작위 배치 대신 그 플레이어 벽 배치 하나만 쓴다.
        string fixedWalls = Environment.GetEnvironmentVariable("WALLS");
        if (fixedWalls != null)
            layouts = new List<Vector2Int[]> { fixedWalls.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Split(',')).Select(c => new Vector2Int(int.Parse(c[0]), int.Parse(c[1]))).ToArray() };

        if (args.Length > 0 && args[0] == "determinism")
            return Determinism(args.Skip(1).ToArray());

        if (args.Length > 0 && args[0] == "rounds")
        {
            Rounds(layouts, args.Skip(1).ToArray());
            return 0;
        }

        if (args.Length > 0 && args[0] == "mix")
        {
            Mix(layouts, args.Skip(1).ToArray());
            return 0;
        }

        if (args.Length > 0 && args[0] == "sweep")
        {
            Sweep(layouts, args.Skip(1).ToArray());
            return 0;
        }

        var goblin = args.Length >= 8 ? Parse(args, 0) : new Stats(50, 10, 0.65f, 6);
        var zombie = args.Length >= 8 ? Parse(args, 4) : new Stats(50, 10, 0.65f, 4);
        Report(goblin, zombie, layouts, verbose: true);
        return 0;
    }

    private static Stats Parse(string[] a, int i) =>
        new(int.Parse(a[i]), int.Parse(a[i + 1]), float.Parse(a[i + 2]), int.Parse(a[i + 3]));

    // 벽 없는 기본 맵 + 무작위 벽 배치들. 플레이어가 동선을 바꿔도 결과가 한쪽으로 쏠리지 않는지 보기 위함.
    private static List<Vector2Int[]> BuildLayouts(int count)
    {
        var result = new List<Vector2Int[]> { Array.Empty<Vector2Int>() };
        var rng = new System.Random(20261118);
        while (result.Count < count)
        {
            var grid = new DefenseGrid(Board);
            var placed = new List<Vector2Int>();
            int walls = rng.Next(1, DefenseGrid.WallLimit + 1);
            for (int attempt = 0; attempt < 200 && placed.Count < walls; attempt++)
            {
                var cell = new Vector2Int(rng.Next(DefenseGrid.Width), rng.Next(DefenseGrid.Depth));
                if (grid.TryPlace(cell) == DefenseGrid.BuildResult.Placed)
                    placed.Add(cell);
            }
            result.Add(placed.ToArray());
        }
        return result;
    }

    // 게임은 고정 틱(DefenseBattle.TickDuration)으로만 진행하므로 같은 입력이면 seed 와 관계없이 결과가 같다.
    private static readonly int Seeds = int.Parse(Environment.GetEnvironmentVariable("SEEDS") ?? "1");

    private static Outcome Run(Stats g, Stats z, Vector2Int[] layout, int seed = 0)
    {
        var grid = new DefenseGrid(Board);
        foreach (Vector2Int cell in layout)
            grid.TryPlace(cell);

        var goblin = new EnemyFaction("Goblin", new Color(1, 0, 0), Range, g.Hp, g.Dmg, g.Interval, g.Count);
        var zombie = new EnemyFaction("Zombie", new Color(0, 1, 1), Range, z.Hp, z.Dmg, z.Interval, z.Count);
        var battle = new DefenseBattle(grid, new[] { goblin, zombie });

        int goblinLeft = 0, zombieLeft = 0, goblinHp = 0, zombieHp = 0;
        bool engaged = false;
        float firstContact = -1f, lastKill = -1f, time = 0f;
        battle.EnemyRemoved += e =>
        {
            if (!e.ReachedCore)
            {
                lastKill = time;
                return;
            }
            if (e.Faction == goblin) goblinLeft++; else zombieLeft++;
        };

        battle.StartRound();
        var hpBeforeCore = new Dictionary<EnemyUnit, int>();
        var rng = new System.Random(seed * 7919 + 17);
        for (int i = 0; i < TickLimit && battle.CurrentPhase == DefenseBattle.Phase.Combat; i++)
        {
            foreach (EnemyUnit e in battle.Enemies)
                hpBeforeCore[e] = e.Health;
            float dt = DefenseBattle.TickDuration;
            battle.Tick(dt, pauseMovement: false);
            time += dt;
            if (!engaged && battle.Enemies.Any(e => e.AttacksPerformed > 0))
            {
                engaged = true;
                firstContact = time;
            }
        }

        // 코어에 닿은 유닛의 남은 체력 합 (Die() 전 체력)
        foreach (var (e, hp) in hpBeforeCore)
        {
            if (!e.ReachedCore) continue;
            if (e.Faction == goblin) goblinHp += hp; else zombieHp += hp;
        }
        return new Outcome(engaged, goblinLeft, zombieLeft, goblinHp, zombieHp, lastKill > 0 ? lastKill - firstContact : 0f);
    }

    private sealed class Summary
    {
        public int Runs, Engaged, GoblinWins, ZombieWins, Draws, Wipes;
        public double GoblinLeft, ZombieLeft, GoblinHpShare, ZombieHpShare, FightTime;
        // +면 고블린 우세, -면 좀비 우세. 남은 체력 비율 차이의 평균.
        public double Balance => Engaged == 0 ? 0 : (GoblinHpShare - ZombieHpShare) / Engaged;
    }

    private static Summary Evaluate(Stats g, Stats z, List<Vector2Int[]> layouts, List<(Vector2Int[] layout, Outcome o)> detail = null)
    {
        var s = new Summary();
        foreach (var layout in layouts)
        for (int seed = 0; seed < Seeds; seed++)
        {
            Outcome o = Run(g, z, layout, seed);
            s.Runs++;
            detail?.Add((layout, o));
            if (!o.Engaged) continue;
            s.Engaged++;
            s.GoblinLeft += o.GoblinLeft;
            s.ZombieLeft += o.ZombieLeft;
            s.GoblinHpShare += (double)o.GoblinHpLeft / (g.Hp * g.Count);
            s.ZombieHpShare += (double)o.ZombieHpLeft / (z.Hp * z.Count);
            s.FightTime += o.FightTime;
            // 남은 체력 비율이 20%p 이상 벌어지면 승패, 아니면 무승부.
            double diff = (double)o.GoblinHpLeft / (g.Hp * g.Count) - (double)o.ZombieHpLeft / (z.Hp * z.Count);
            if (diff > 0.2) s.GoblinWins++;
            else if (diff < -0.2) s.ZombieWins++;
            else s.Draws++;
            if (o.GoblinLeft == 0 && o.ZombieLeft == 0) s.Wipes++;
        }
        return s;
    }

    private static void Report(Stats g, Stats z, List<Vector2Int[]> layouts, bool verbose)
    {
        var detail = new List<(Vector2Int[] layout, Outcome o)>();
        Summary s = Evaluate(g, z, layouts, detail);
        Console.WriteLine($"고블린 {g}  | 팀 HP {g.Hp * g.Count}, 팀 DPS {g.Dps * g.Count:0.0}");
        Console.WriteLine($"좀비   {z}  | 팀 HP {z.Hp * z.Count}, 팀 DPS {z.Dps * z.Count:0.0}");
        Print(s);
        if (!verbose) return;
        Outcome baseRun = detail[0].o;
        Console.WriteLine($"  기본 맵(벽 없음): 교전 {(baseRun.Engaged ? "O" : "X")}, 생존 고블린 {baseRun.GoblinLeft}/{g.Count} (HP {baseRun.GoblinHpLeft}), 좀비 {baseRun.ZombieLeft}/{z.Count} (HP {baseRun.ZombieHpLeft}), 교전 {baseRun.FightTime:0.0}s");
    }

    private static void Print(Summary s)
    {
        double n = Math.Max(1, s.Engaged);
        Console.WriteLine($"  실행 {s.Runs}회 중 교전 {s.Engaged}회 | 고블린 승 {100.0 * s.GoblinWins / n:0}% · 좀비 승 {100.0 * s.ZombieWins / n:0}% · 무승부 {100.0 * s.Draws / n:0}% (양쪽 전멸 {100.0 * s.Wipes / n:0}%)");
        Console.WriteLine($"  평균 생존: 고블린 {s.GoblinLeft / n:0.00}, 좀비 {s.ZombieLeft / n:0.00} | 남은 체력 비율 차 {s.Balance:+0.000;-0.000} | 평균 교전 시간 {s.FightTime / n:0.0}s");
    }


    // 교전 구도와 "한쪽에만 마법을 걸 수 있는가"를 측정한다.
    // 교전 중 0.2초마다: 싸우는 유닛이 몇 대 몇 묶음에 속하는지, 아군만 맞히는 시전 위치가 있는지 기록한다.
    private static void Mix(List<Vector2Int[]> layouts, string[] a)
    {
        var g = a.Length >= 4 ? Parse(a, 0) : new Stats(50, 10, 0.65f, 6);
        var z = a.Length >= 8 ? Parse(a, 4) : new Stats(90, 15, 1.0f, 4);
        float[] radii = { DefenseBattle.DefaultAreaSpellRadius, 1.25f, 0.75f };
        long fighters = 0, duel = 0, twoOnOne = 0, brawl = 0;
        long sideSamples = 0;
        var clean = new long[radii.Length];
        var cleanTwo = new long[radii.Length];
        var selfCastLeaks = new long[radii.Length];
        long selfCasts = 0;
        var tolSum = new double[radii.Length];
        var tolCount = new long[radii.Length];
        var perRun = new List<(double duelShare, double balance)>();

        foreach (var layout in layouts)
        for (int seed = 0; seed < Seeds; seed++)
        {
            var grid = new DefenseGrid(Board);
            foreach (Vector2Int cell in layout) grid.TryPlace(cell);
            var gf = new EnemyFaction("Goblin", new Color(1, 0, 0), Range, g.Hp, g.Dmg, g.Interval, g.Count);
            var zf = new EnemyFaction("Zombie", new Color(0, 1, 1), Range, z.Hp, z.Dmg, z.Interval, z.Count);
            var battle = new DefenseBattle(grid, new[] { gf, zf });
            battle.StartRound();
            var rng = new System.Random(seed * 7919 + 17);
            float untilSample = 0f;
            long runFighters = 0, runDuel = 0;
            var lastHp = new Dictionary<EnemyUnit, int>();
            for (int i = 0; i < TickLimit && battle.CurrentPhase == DefenseBattle.Phase.Combat; i++)
            {
                foreach (EnemyUnit e in battle.Enemies) lastHp[e] = e.Health;
                float dt = DefenseBattle.TickDuration;
                battle.Tick(dt, pauseMovement: false);
                untilSample -= dt;
                if (untilSample > 0f) continue;
                untilSample = 0.2f;

                var units = battle.Enemies.Where(e => e.IsAlive).ToList();
                var engaged = units.Where(e => e.Target is { IsAlive: true } t &&
                    Vector3.Distance(e.Position, t.Position) <= e.Faction.AttackRange + 0.05f).ToList();
                if (engaged.Count == 0) continue;

                // 서로 때리는 관계로 묶음을 만든다.
                var parent = units.ToDictionary(e => e, e => e);
                EnemyUnit Find(EnemyUnit e) { while (parent[e] != e) e = parent[e] = parent[parent[e]]; return e; }
                foreach (EnemyUnit e in engaged)
                    if (parent.ContainsKey(e.Target)) parent[Find(e)] = Find(e.Target);
                var clusters = engaged.Concat(engaged.Select(e => e.Target)).Where(parent.ContainsKey).Distinct()
                    .GroupBy(Find).ToList();
                foreach (var c in clusters)
                {
                    int n = c.Count();
                    fighters += n;
                    runFighters += n;
                    if (n <= 2) runDuel += n;
                    if (n <= 2) duel += n; else if (n == 3) twoOnOne += n; else brawl += n;
                }

                foreach (EnemyFaction side in new[] { gf, zf })
                {
                    var ownEngaged = engaged.Where(e => e.Faction == side).ToList();
                    if (ownEngaged.Count == 0) continue;
                    sideSamples++;
                    for (int r = 0; r < radii.Length; r++)
                    {
                        float rr = radii[r] * radii[r];
                        bool anyClean = false, anyTwo = false;
                        int aimed = 0, aimedClean = 0;
                        foreach (EnemyUnit anchor in ownEngaged)
                        for (float dx = -radii[r]; dx <= radii[r] + 0.001f; dx += 0.125f)
                        for (float dz = -radii[r]; dz <= radii[r] + 0.001f; dz += 0.125f)
                        {
                            if (dx * dx + dz * dz > rr) continue;
                            var c = anchor.Position + new Vector3(dx, 0, dz);
                            int own = 0, foe = 0;
                            bool hitsFighter = false;
                            foreach (EnemyUnit u in units)
                            {
                                Vector3 o = u.Position - c;
                                if (o.x * o.x + o.z * o.z > rr) continue;
                                if (u.Faction == side) { own++; hitsFighter |= ownEngaged.Contains(u); }
                                else foe++;
                            }
                            if (!hitsFighter) continue;
                            aimed++;
                            if (foe > 0) continue;
                            aimedClean++;
                            anyClean = true;
                            if (own >= 2) anyTwo = true;
                        }
                        if (aimed > 0) { tolSum[r] += (double)aimedClean / aimed; tolCount[r]++; }
                        if (anyClean) clean[r]++;
                        if (anyTwo) cleanTwo[r]++;
                    }
                    // 싸우는 아군 머리 위에 그대로 찍었을 때 적이 같이 걸리는가
                    foreach (EnemyUnit anchor in ownEngaged)
                    {
                        selfCasts++;
                        for (int r = 0; r < radii.Length; r++)
                            if (units.Any(u => u.Faction != side && Sq(u.Position - anchor.Position) <= radii[r] * radii[r]))
                                selfCastLeaks[r]++;
                    }
                }
            }
            double gLeft = lastHp.Where(kv => kv.Key.ReachedCore && kv.Key.Faction == gf).Sum(kv => kv.Value) / (double)(g.Hp * g.Count);
            double zLeft = lastHp.Where(kv => kv.Key.ReachedCore && kv.Key.Faction == zf).Sum(kv => kv.Value) / (double)(z.Hp * z.Count);
            if (runFighters > 0) perRun.Add(((double)runDuel / runFighters, gLeft - zLeft));
        }

        Console.WriteLine($"고블린 {g} / 좀비 {z} / 사거리 {Range} / 배치 {layouts.Count} x 시드 {Seeds}");
        Console.WriteLine("[구도별 승패] 판마다 싸운 유닛 중 1대1 비율로 나눔");
        foreach (var (lo, hi, label) in new[] { (0.0, 0.25, "1대1 25% 미만 (난전 위주)"), (0.25, 0.5, "1대1 25~50%"), (0.5, 1.01, "1대1 50% 이상 (한 명씩)") })
        {
            var b = perRun.Where(x => x.duelShare >= lo && x.duelShare < hi).ToList();
            if (b.Count == 0) continue;
            Console.WriteLine($"  {label,-22} {b.Count,4}판 | 고블린 승 {100.0 * b.Count(x => x.balance > 0.2) / b.Count,3:0}% · 좀비 승 {100.0 * b.Count(x => x.balance < -0.2) / b.Count,3:0}% · 평균 차 {b.Average(x => x.balance):+0.00;-0.00}");
        }
        Console.WriteLine($"[교전 구도] 싸우는 유닛 기준: 1대1 {100.0 * duel / fighters:0}% · 2대1 {100.0 * twoOnOne / fighters:0}% · 4명 이상 난전 {100.0 * brawl / fighters:0}%");
        Console.WriteLine("[한쪽만 마법 걸기] 반경 | 아군 머리 위 시전 시 적도 걸림 | 아군을 맞히는 조준점 중 적이 안 걸리는 비율 | 적 없이 아군 2명 이상 걸 수 있는 순간");
        for (int r = 0; r < radii.Length; r++)
            Console.WriteLine($"                  {radii[r],4:0.00}칸 | {100.0 * selfCastLeaks[r] / selfCasts,5:0}% | {100.0 * tolSum[r] / Math.Max(1, tolCount[r]),5:0}% | {100.0 * cleanTwo[r] / sideSamples,5:0}%");
    }

    private static float Sq(Vector3 v) => v.x * v.x + v.z * v.z;

    // ───── 라운드 편성 검증 ─────
    // 유닛 수치는 EnemyCatalog.asset 에서, 편성은 GameScene 의 rounds 또는 텍스트 파일에서 읽는다.
    private const string CatalogPath = "../ProjectBeViolenceDefense/Assets/04_Data/03_Balance/EnemyCatalog.asset";
    private const string ScenePath = "../ProjectBeViolenceDefense/Assets/01_Scenes/02_Gameplay/GameScene.unity";

    private sealed class UnitStats
    {
        public string Id;
        public int Hp, Dmg, Armor;
        public float Interval, Range, Aware, Speed;
        public double Power => Hp * (Dmg / Interval) / 769.0;
    }

    private sealed class RoundSpec
    {
        public string Note;
        public readonly List<(string Id, int Count)>[] Sides = { new(), new() };
    }

    private static Dictionary<string, UnitStats> LoadCatalog(string path)
    {
        var result = new Dictionary<string, UnitStats>();
        string text = System.IO.File.ReadAllText(path);
        foreach (string block in text.Split("\n  - id: ").Skip(1))
        {
            string F(string key)
            {
                var m = System.Text.RegularExpressions.Regex.Match(block, @"\n    " + key + @": ([^\n]*)");
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }
            var u = new UnitStats
            {
                Id = block.Split('\n')[0].Trim(),
                Hp = int.Parse(F("maxHealth") ?? "50"),
                Dmg = int.Parse(F("damage") ?? "10"),
                Interval = float.Parse(F("attackInterval") ?? "0.65"),
                Range = float.Parse(F("attackRange") ?? "1.5"),
                Aware = float.Parse(F("awarenessRange") ?? "2.2"),
                Speed = float.Parse(F("moveSpeed") ?? "1.7"),
                Armor = int.Parse(F("armor") ?? "0"),
            };
            result[u.Id] = u;
        }
        return result;
    }

    // 텍스트: "메모 | coral-id*수, id*수 | teal-id*수" 한 줄에 한 라운드. # 은 주석.
    // 씬: VoxelDefensePrototype 의 rounds 직렬화 형식.
    private static List<RoundSpec> LoadRounds(string path)
    {
        var rounds = new List<RoundSpec>();
        var lines = System.IO.File.ReadAllLines(path);
        if (path.EndsWith(".txt"))
        {
            foreach (string raw in lines)
            {
                string line = raw.Split('#')[0].Trim();
                if (line.Length == 0) continue;
                var cols = line.Split('|');
                var spec = new RoundSpec { Note = cols[0].Trim() };
                for (int side = 0; side < 2; side++)
                    foreach (string item in cols[side + 1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var kv = item.Trim().Split('*');
                        spec.Sides[side].Add((kv[0].Trim(), kv.Length > 1 ? int.Parse(kv[1]) : 1));
                    }
                rounds.Add(spec);
            }
            return rounds;
        }

        bool inRounds = false;
        int sideIndex = -1;
        string pendingId = null;
        foreach (string raw in lines)
        {
            string t = raw.Trim();
            if (raw.StartsWith("  rounds:")) { inRounds = true; continue; }
            if (!inRounds) continue;
            if (raw.StartsWith("  ") && !raw.StartsWith("   ") && !raw.StartsWith("  - ") && !raw.StartsWith("  rounds")) break;
            if (raw.StartsWith("---")) break;
            if (raw.StartsWith("  - note:")) { rounds.Add(new RoundSpec { Note = System.Text.RegularExpressions.Regex.Unescape(t.Substring("- note:".Length).Trim().Trim('"')) }); sideIndex = -1; continue; }
            if (t == "coral:" || t.StartsWith("coral: [")) { sideIndex = 0; continue; }
            if (t == "teal:" || t.StartsWith("teal: [")) { sideIndex = 1; continue; }
            if (t.StartsWith("- unitId:")) { pendingId = t.Substring("- unitId:".Length).Trim(); continue; }
            if (t.StartsWith("count:") && pendingId != null && sideIndex >= 0)
            {
                rounds[^1].Sides[sideIndex].Add((pendingId, int.Parse(t.Substring("count:".Length).Trim())));
                pendingId = null;
            }
        }
        return rounds;
    }

    private static void Rounds(List<Vector2Int[]> layouts, string[] a)
    {
        var catalog = LoadCatalog(CatalogPath);
        var rounds = LoadRounds(a.Length > 0 ? a[0] : ScenePath);
        if (rounds.Count == 0) { Console.WriteLine("편성을 찾지 못했습니다."); return; }
        var sideColors = new[] { new Color(1, 0.45f, 0.34f), new Color(0.27f, 0.87f, 0.81f) };
        Console.WriteLine($"배치 {layouts.Count} x 시드 {Seeds} | 결과: 남은 체력 비율 차(+는 Coral 우세), ±0.2 안이면 무승부");
        for (int r = 0; r < rounds.Count; r++)
        {
            RoundSpec spec = rounds[r];
            var units = new Dictionary<(string, int), EnemyFaction>();
            var queues = new IReadOnlyList<EnemyFaction>[2];
            var totalHp = new double[2];
            var power = new double[2];
            bool missing = false;
            for (int side = 0; side < 2; side++)
            {
                var q = new List<EnemyFaction>();
                foreach (var (id, n) in spec.Sides[side])
                {
                    if (!catalog.TryGetValue(id, out UnitStats st)) { Console.WriteLine($"  알 수 없는 유닛: {id}"); missing = true; continue; }
                    if (!units.TryGetValue((id, side), out EnemyFaction f))
                        units[(id, side)] = f = new EnemyFaction(id, sideColors[side], st.Range, st.Hp, st.Dmg, st.Interval, 0,
                            st.Aware, st.Speed, null, null, null, 1f, st.Armor, id);
                    for (int i = 0; i < n; i++) q.Add(f);
                    totalHp[side] += st.Hp * n;
                    power[side] += st.Power * n;
                }
                queues[side] = q;
            }
            if (missing) continue;

            var sideReps = new[] { new EnemyFaction("Coral", sideColors[0]), new EnemyFaction("Teal", sideColors[1]) };
            int runs = 0, cWins = 0, tWins = 0, draws = 0;
            double balanceSum = 0, fight = 0;
            var lost = new double[2];
            var leaks = new double[2];
            var attacks = new Dictionary<string, long>();
            var spawned = new Dictionary<string, long>();
            var survived = new Dictionary<string, long>();
            foreach (var layout in layouts)
            for (int seed = 0; seed < Seeds; seed++)
            {
                var grid = new DefenseGrid(Board);
                foreach (Vector2Int cell in layout) grid.TryPlace(cell);
                var battle = new DefenseBattle(grid, sideReps, _ => queues);
                var all = new List<EnemyUnit>();
                var lastHp = new Dictionary<EnemyUnit, int>();
                battle.EnemySpawned += e => all.Add(e);
                battle.StartRound();
                var rng = new System.Random(seed * 7919 + 17);
                float time = 0f;
                for (int i = 0; i < TickLimit && battle.CurrentPhase == DefenseBattle.Phase.Combat; i++)
                {
                    foreach (EnemyUnit e in battle.Enemies) lastHp[e] = e.Health;
                    float dt = DefenseBattle.TickDuration;
                    battle.Tick(dt, pauseMovement: false);
                    time += dt;
                }
                var left = new double[2];
                foreach (EnemyUnit e in all)
                {
                    string key = $"{(e.FactionIndex == 0 ? "C" : "T")}:{e.Faction.UnitId}";
                    spawned[key] = spawned.GetValueOrDefault(key) + 1;
                    attacks[key] = attacks.GetValueOrDefault(key) + e.AttacksPerformed;
                    if (e.ReachedCore)
                    {
                        survived[key] = survived.GetValueOrDefault(key) + 1;
                        left[e.FactionIndex] += lastHp.GetValueOrDefault(e);
                    }
                }
                double diff = left[0] / totalHp[0] - left[1] / totalHp[1];
                for (int side = 0; side < 2; side++)
                {
                    lost[side] += 1 - left[side] / totalHp[side];
                    leaks[side] += all.Count(e => e.FactionIndex == side && e.ReachedCore);
                }
                runs++;
                balanceSum += diff;
                fight += time;
                if (diff > 0.2) cWins++; else if (diff < -0.2) tWins++; else draws++;
            }

            string Lineup(int side) => string.Join(", ", spec.Sides[side].Select(x => $"{x.Id}×{x.Count}"));
            Console.WriteLine($"\nR{r + 1} {spec.Note}");
            Console.WriteLine($"  Coral {Lineup(0)} (전력 {power[0]:0.0}, 체력 {totalHp[0]:0})  vs  Teal {Lineup(1)} (전력 {power[1]:0.0}, 체력 {totalHp[1]:0})");
            Console.WriteLine($"  Coral 승 {100.0 * cWins / runs:0}% · Teal 승 {100.0 * tWins / runs:0}% · 무승부 {100.0 * draws / runs:0}% | 평균 차 {balanceSum / runs:+0.00;-0.00} | 잃은 체력 C {100 * lost[0] / runs:0}% / T {100 * lost[1] / runs:0}% | 코어 도달 C {leaks[0] / runs:0.0} / T {leaks[1] / runs:0.0}마리 | {fight / runs:0}s");
            Console.WriteLine("  유닛별 평균 공격 횟수 / 코어 도달률: " + string.Join(" · ", spawned.Keys.OrderBy(k => k).Select(k =>
                $"{k} {(double)attacks[k] / spawned[k]:0.0}회/{100.0 * survived.GetValueOrDefault(k) / spawned[k]:0}%")));
        }
    }

    // ───── 결정론 검증 ─────
    // 게임 루프와 같은 BattleClock 에 서로 다른 프레임 패턴을 넣고, 틱마다 상태 해시가 같은지 비교한다.
    private static int Determinism(string[] a)
    {
        int roundIndex = a.Length > 0 ? int.Parse(a[0]) - 1 : 0;
        var catalog = LoadCatalog(CatalogPath);
        var spec = LoadRounds(ScenePath)[roundIndex];
        var sideColors = new[] { new Color(1, 0.45f, 0.34f), new Color(0.27f, 0.87f, 0.81f) };
        var queues = new IReadOnlyList<EnemyFaction>[2];
        for (int side = 0; side < 2; side++)
        {
            var q = new List<EnemyFaction>();
            foreach (var (id, n) in spec.Sides[side])
            {
                UnitStats st = catalog[id];
                var f = new EnemyFaction(id, sideColors[side], st.Range, st.Hp, st.Dmg, st.Interval, 0,
                    st.Aware, st.Speed, null, null, null, 1f, st.Armor, id);
                for (int i = 0; i < n; i++) q.Add(f);
            }
            queues[side] = q;
        }
        var walls = (Environment.GetEnvironmentVariable("WALLS") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Split(',')).Select(c => new Vector2Int(int.Parse(c[0]), int.Parse(c[1]))).ToArray();
        // 정해진 틱에 거는 마법(리플레이와 같은 방식)
        var spells = new[]
        {
            new DefenseBattle.SpellCommand(420, DefenseBattle.Spell.AttackBuff, new Vector3(8, 0, 10), 0),
            new DefenseBattle.SpellCommand(780, DefenseBattle.Spell.Heal, new Vector3(8, 0, 11), 0),
        };

        // 프레임 패턴: (이름, 프레임 번호 -> (실제 프레임 시간, 배속, 조준 중))
        var rng = new System.Random(7);
        var patterns = new (string Name, Func<int, (float Dt, int Speed, bool Aim)> Frame)[]
        {
            ("30fps", i => (1f / 30f, 1, false)),
            ("60fps", i => (1f / 60f, 1, false)),
            ("144fps", i => (1f / 144f, 1, false)),
            ("들쭉날쭉 20~144fps", i => (1f / (20f + (float)rng.NextDouble() * 124f), 1, false)),
            ("2배속·조준 슬로우·0.5초 멈칫", i => (i % 97 == 0 ? 0.5f : 1f / 75f, i % 400 < 150 ? 2 : 1, i % 400 >= 300 && i % 400 < 340)),
        };

        List<ulong> reference = null;
        string referenceName = null;
        bool allEqual = true;
        Console.WriteLine($"라운드 {roundIndex + 1} ({spec.Note}) · 플레이어 벽 {walls.Length}개 · 마법 {spells.Length}회(틱 420, 780)");
        foreach (var (name, frame) in patterns)
        {
            var (hashes, outcome, frames) = RunWithClock(queues, walls, spells, frame);
            if (reference == null)
            {
                reference = hashes;
                referenceName = name;
            }
            int mismatch = -1;
            for (int t = 0; t < Math.Max(hashes.Count, reference.Count); t++)
                if (t >= hashes.Count || t >= reference.Count || hashes[t] != reference[t]) { mismatch = t; break; }
            allEqual &= mismatch < 0;
            Console.WriteLine($"  {name,-28} 프레임 {frames,6} · 틱 {hashes.Count,5} · 최종 해시 {hashes[^1]:X16} · {outcome} · " +
                (mismatch < 0 ? $"{referenceName}와 모든 틱 일치" : $"틱 {mismatch}부터 다름"));
        }

        // 비교용: 예전 방식처럼 프레임 시간을 그대로 한 틱으로 쓰면
        Console.WriteLine("  [비교] 예전 방식(프레임 시간 = 틱 길이):");
        foreach (var (name, frame) in patterns.Take(3))
        {
            var grid = new DefenseGrid(Board);
            foreach (Vector2Int cell in walls) grid.TryPlace(cell);
            var battle = new DefenseBattle(grid, new[] { new EnemyFaction("Coral", sideColors[0]), new EnemyFaction("Teal", sideColors[1]) }, _ => queues);
            battle.StartRound();
            for (int i = 0; i < 200000 && battle.CurrentPhase == DefenseBattle.Phase.Combat; i++)
                battle.Tick(frame(i).Dt, false);
            Console.WriteLine($"    {name,-8} 최종 해시 {StateHash(battle):X16} · {BattleSummary(battle)}");
        }
        Console.WriteLine(allEqual ? "결과: 모든 프레임 패턴에서 틱별 상태가 같다." : "결과: 프레임 패턴에 따라 상태가 달라진다.");
        return allEqual ? 0 : 1;
    }

    private static (List<ulong> Hashes, string Outcome, int Frames) RunWithClock(IReadOnlyList<EnemyFaction>[] queues,
        Vector2Int[] walls, DefenseBattle.SpellCommand[] spells, Func<int, (float Dt, int Speed, bool Aim)> frame)
    {
        var grid = new DefenseGrid(Board);
        foreach (Vector2Int cell in walls) grid.TryPlace(cell);
        var sides = new[] { new EnemyFaction("Coral", new Color(1, 0, 0)), new EnemyFaction("Teal", new Color(0, 1, 1)) };
        var battle = new DefenseBattle(grid, sides, _ => queues);
        var clock = new BattleClock();
        battle.StartRound();
        clock.Reset();
        foreach (var spell in spells) battle.ScheduleSpell(spell);
        var hashes = new List<ulong>();
        int frames = 0;
        for (; frames < 400000 && battle.CurrentPhase == DefenseBattle.Phase.Combat; frames++)
        {
            var (dt, speed, aim) = frame(frames);
            clock.BeginFrame(dt, speed, aim);
            for (int t = 0; t < clock.StepCount && battle.CurrentPhase == DefenseBattle.Phase.Combat; t++)
            {
                battle.Tick(clock.StepDeltaTime, false);
                hashes.Add(StateHash(battle));
            }
        }
        return (hashes, BattleSummary(battle), frames);
    }

    private static string BattleSummary(DefenseBattle battle) =>
        $"코어 {battle.CoreHealth}/{DefenseBattle.CoreMaxHealth}, 마나 {battle.Mana:0.0}";

    private static ulong StateHash(DefenseBattle battle)
    {
        ulong h = 14695981039346656037UL;
        void Mix(long v) { unchecked { h ^= (ulong)v; h *= 1099511628211UL; } }
        Mix(battle.TickIndex);
        Mix(BitConverter.SingleToInt32Bits(battle.Mana));
        Mix(battle.SpawnCount);
        Mix(battle.CoreHealth);
        foreach (EnemyUnit e in battle.Enemies)
        {
            Mix(e.SpawnOrder);
            Mix(BitConverter.SingleToInt32Bits(e.Position.x));
            Mix(BitConverter.SingleToInt32Bits(e.Position.z));
            Mix(e.Health);
            Mix(e.AttacksPerformed);
            Mix(e.AttackBuffStacks);
        }
        return h;
    }

    // 고블린 수치를 고정하고 좀비 후보를 훑어 균형점(남은 체력 비율 차 ≈ 0)을 찾는다.
    private static void Sweep(List<Vector2Int[]> layouts, string[] args)
    {
        var goblin = args.Length >= 4 ? Parse(args, 0) : new Stats(40, 8, 0.6f, 6);
        float zInterval = args.Length >= 5 ? float.Parse(args[4]) : 1.0f;
        int[] hps = args.Length >= 7 ? Enumerable.Range(0, (int.Parse(args[6]) - int.Parse(args[5])) / 5 + 1).Select(i => int.Parse(args[5]) + i * 5).ToArray() : new[] { 60, 70, 80, 90, 100 };
        int[] dmgs = args.Length >= 9 ? Enumerable.Range(int.Parse(args[7]), int.Parse(args[8]) - int.Parse(args[7]) + 1).ToArray() : new[] { 14, 16, 18, 20, 22 };

        Console.WriteLine($"고블린 고정: {goblin} | 좀비 공격 간격 {zInterval:0.00}s, 배치 {layouts.Count}개");
        Console.WriteLine("좀비 HP\\공격 " + string.Join("", dmgs.Select(d => $"{d,12}")));
        foreach (int hp in hps)
        {
            var row = new List<string>();
            foreach (int dmg in dmgs)
            {
                Summary s = Evaluate(goblin, new Stats(hp, dmg, zInterval, 4), layouts);
                row.Add($"{s.Balance,7:+0.00;-0.00}/{100.0 * s.Draws / Math.Max(1, s.Engaged),3:0}%");
            }
            Console.WriteLine($"{hp,10}  " + string.Join("", row));
        }
        Console.WriteLine("(값 = 고블린 남은 체력 비율 - 좀비 남은 체력 비율. 0에 가까울수록 무승부, +는 고블린 우세)");
    }
}
