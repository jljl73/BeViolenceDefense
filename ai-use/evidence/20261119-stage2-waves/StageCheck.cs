using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// 사용: StageCheck <StageData.asset> [lineup "id*n,id*n|id*n"]   환경: WALLS="x,y;x,y"  CATALOG=<EnemyCatalog.asset>
internal static class StageCheck
{
    static Dictionary<string, EnemyFaction> catalog = new();

    static void LoadCatalog(string path)
    {
        string text = System.IO.File.ReadAllText(path).Replace("\r", "");
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

    static int width, depth; static RectInt core; static List<Vector2Int> cells = new(); static List<int> factions = new(); static List<Vector2Int> walls = new();

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

    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        LoadCatalog(Environment.GetEnvironmentVariable("CATALOG"));
        LoadStage(args[0]);
        var grid = new DefenseGrid(width, depth, core, cells, factions, walls);
        if (grid.Routes == null) { Console.WriteLine("고정 지형에서 막힌 스폰: " + grid.UnreachableSpawn); return 1; }
        var placed = new List<Vector2Int>();
        foreach (string w in (Environment.GetEnvironmentVariable("WALLS") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = w.Split(','); var cell = new Vector2Int(int.Parse(p[0]), int.Parse(p[1]));
            var r = grid.TryPlace(cell);
            if (r != DefenseGrid.BuildResult.Placed) { Console.WriteLine($"벽 {cell.x},{cell.y} 배치 실패: {r}"); return 1; }
            placed.Add(cell);
        }
        bool quiet = Environment.GetEnvironmentVariable("QUIET") == "1";
        string lens = string.Join(" / ", grid.Routes.Select((r, i) => $"스폰{i}(진영{grid.SpawnFactions[i]}) {r.Length - 2}칸"));
        Console.WriteLine($"벽 {placed.Count}개 [{string.Join(";", placed.Select(c => c.x + "," + c.y))}]  경로: {lens}");

        string spec = args.Length > 1 ? args[1] : "goblin-grunt-1500*6|skeleton*6";
        int sides = Math.Max(2, factions.Max() + 1);
        var queues = new IReadOnlyList<EnemyFaction>[sides];
        var parts = spec.Split('|');
        for (int s = 0; s < sides; s++)
        {
            var q = new List<EnemyFaction>();
            if (s < parts.Length)
                foreach (string item in parts[s].Split(',', StringSplitOptions.RemoveEmptyEntries))
                { var kv = item.Trim().Split('*'); for (int i = 0; i < (kv.Length > 1 ? int.Parse(kv[1]) : 1); i++) q.Add(catalog[kv[0]]); }
            queues[s] = q;
        }
        var reps = Enumerable.Range(0, sides).Select(i => new EnemyFaction("F" + i, new Color(1, 1, 1))).ToArray();
        var battle = new DefenseBattle(grid, reps, _ => queues);
        var all = new List<EnemyUnit>();
        battle.EnemySpawned += e => all.Add(e);
        battle.StartRound();
        var forecast = battle.ForecastEncounterCells();

        if (!quiet)
        {
            var map = new char[width, depth];
            for (int x = 0; x < width; x++) for (int y = 0; y < depth; y++) map[x, y] = '.';
            foreach (var w in walls) if (grid.InBounds(w)) map[w.x, w.y] = '#';
            for (int i = 0; i < grid.Routes.Count; i++)
                for (int k = 0; k < grid.Routes[i].Length - 1; k++)
                {
                    var c = DefenseGrid.PositionToCell(grid.Routes[i][k]); char ch = (char)('a' + i);
                    map[c.x, c.y] = map[c.x, c.y] == '.' || map[c.x, c.y] == ch ? ch : '*';
                }
            foreach (var c in core.allPositionsWithin) map[c.x, c.y] = 'C';
            foreach (var c in placed) map[c.x, c.y] = 'W';
            foreach (var c in forecast) map[c.x, c.y] = 'X';
            for (int i = 0; i < cells.Count; i++) map[cells[i].x, cells[i].y] = (char)('A' + i);
            Console.WriteLine("    " + string.Concat(Enumerable.Range(0, width).Select(x => (x % 10).ToString())));
            for (int y = depth - 1; y >= 0; y--) Console.WriteLine($"{y,3} " + string.Concat(Enumerable.Range(0, width).Select(x => map[x, y])));
        }
        Console.WriteLine("  교전 예측 칸: " + (forecast.Count == 0 ? "없음" : string.Join(" ", forecast.Select(c => $"({c.x},{c.y})"))));

        var hp = new Dictionary<EnemyUnit, int>();
        int firstHitTick = -1; Vector3 firstHitPos = default; int ticks = 0; int limit = 300 * 60;
        var hitCells = new Dictionary<Vector2Int, int>();
        // MAGIC=1: 1번(공격력 버프)·2번(힐)만 쓰는 단순 플레이어. 교전 중 열세 진영을 반경 1.4로 돕는다.
        bool magic = Environment.GetEnvironmentVariable("MAGIC") == "1"; int lastCast = -999, casts = 0, heals = 0;
        float radius = DefenseBattle.AreaSpellRadius(0);
        float Ratio = float.Parse(Environment.GetEnvironmentVariable("RATIO") ?? "0.6", CultureInfo.InvariantCulture);
        void TryCast()
        {
            var engaged = battle.Enemies.Where(e => e.IsAlive && e.Target != null && e.Target.IsAlive).ToList();
            if (engaged.Select(e => e.FactionIndex).Distinct().Count() < 2) return;
            var power = engaged.GroupBy(e => e.FactionIndex).Select(g => (g.Key, P: g.Sum(e => e.Health * (e.Faction.DamagePerSecond + e.AttackBuffStacks * EnemyUnit.AttackBonus / e.Faction.AttackInterval)))).OrderBy(g => g.P).ToList();
            if (power[0].P > power[^1].P * Ratio) return; // 비등하면 개입하지 않는다
            int losing = power[0].Key;
            var alive = battle.Enemies.Where(e => e.IsAlive).ToList();
            EnemyUnit best = null; int bestNet = 0;
            foreach (var c in engaged.Where(e => e.FactionIndex == losing))
            {
                int net = alive.Count(e => DefenseBattle.IsInAreaSpellRange(e, c.Position, radius) == true && e.FactionIndex == losing)
                        - alive.Count(e => DefenseBattle.IsInAreaSpellRange(e, c.Position, radius) == true && e.FactionIndex != losing);
                if (net > bestNet) { bestNet = net; best = c; }
            }
            if (best == null) return;
            var mine = alive.Where(e => e.FactionIndex == losing && DefenseBattle.IsInAreaSpellRange(e, best.Position, radius)).ToList();
            bool hurt = mine.Sum(e => e.MaxHealth - e.Health) >= mine.Sum(e => e.MaxHealth) * 0.35f;
            var spell = hurt ? DefenseBattle.Spell.Heal : DefenseBattle.Spell.AttackBuff;
            if (battle.Mana < DefenseBattle.ManaCost(spell)) return;
            battle.QueueSpell(spell, best.Position, 0); lastCast = ticks; casts++; if (hurt) heals++;
        }
        for (; ticks < limit && battle.CurrentPhase == DefenseBattle.Phase.Combat; ticks++)
        {
            foreach (EnemyUnit e in battle.Enemies) hp[e] = e.Health;
            if (magic && ticks % 15 == 0 && ticks - lastCast >= 60) TryCast();
            battle.Tick(DefenseBattle.TickDuration, pauseMovement: false);
            foreach (EnemyUnit e in battle.Enemies)
                if (hp.TryGetValue(e, out int before) && e.Health < before)
                {
                    if (firstHitTick < 0) { firstHitTick = ticks; firstHitPos = e.Position; }
                    var c = DefenseGrid.PositionToCell(e.Position); hitCells[c] = hitCells.GetValueOrDefault(c) + 1;
                }
        }
        bool finished = battle.CurrentPhase != DefenseBattle.Phase.Combat;
        if (!finished)
        {
            string Dump(EnemyUnit e)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var m in typeof(EnemyUnit).GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    object v = m is System.Reflection.PropertyInfo pi && pi.GetIndexParameters().Length == 0 ? pi.GetValue(e) : m is System.Reflection.FieldInfo fi ? fi.GetValue(e) : null;
                    if (v == null || m.Name is "Faction" or "Route" or "CombatPath") continue;
                    string t = v is EnemyUnit u ? "#" + u.SpawnOrder : v is Vector3 p3 ? $"({p3.x:0.00},{p3.z:0.00})" : v is float f ? f.ToString("0.00") : v.ToString();
                    if (t is "False" or "0" or "0.00") continue;
                    sb.Append(m.Name).Append('=').Append(t).Append(' ');
                }
                return sb.ToString();
            }
            foreach (EnemyUnit e in battle.Enemies) Console.WriteLine($"    #{e.SpawnOrder} 진영{e.FactionIndex}: " + Dump(e));
            int watch = int.Parse(Environment.GetEnvironmentVariable("WATCH") ?? "-1");
            if (watch >= 0)
                for (int k = 0; k < 40; k++)
                {
                    battle.Tick(DefenseBattle.TickDuration, pauseMovement: false);
                    EnemyUnit e = battle.Enemies.First(x => x.SpawnOrder == watch);
                    Console.WriteLine($"    +{k}: " + Dump(e));
                }
        }
        string Side(int s) { var u = all.Where(e => e.FactionIndex == s).ToList(); return $"진영{s} 출전 {u.Count} 코어도달 {u.Count(e => e.ReachedCore)} 사망 {u.Count(e => !e.ReachedCore && !e.IsAlive)} 공격 {u.Sum(e => e.AttacksPerformed)}회"; }
        Console.WriteLine($"  전투[{spec}]: {(finished ? "종료" : "미종료!")} {ticks / 60f:0.0}s | 마법 {casts}(힐 {heals}) | " + string.Join(" | ", Enumerable.Range(0, sides).Where(s => queues[s].Count > 0).Select(Side)) +
            (firstHitTick < 0 ? " | 교전 없음" : $" | 첫 타격 {firstHitTick / 60f:0.0}s ({firstHitPos.x:0.0},{firstHitPos.z:0.0}), 타격 많은 칸 " +
            string.Join(" ", hitCells.OrderByDescending(k => k.Value).Take(3).Select(k => $"({k.Key.x},{k.Key.y})x{k.Value}"))));
        return 0;
    }
}

internal static class GameText
{
    public static string Get(string key) => key;
    public static string Format(string key, params object[] args) => key;
}
