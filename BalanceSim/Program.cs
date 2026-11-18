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
            var grid = new DefenseGrid();
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

    // 실제 게임처럼 프레임마다 델타 시간이 달라지도록 seed 별로 1/144~1/40초 사이에서 흔든다. seed 0 은 고정 1/60.
    private static readonly int Seeds = int.Parse(Environment.GetEnvironmentVariable("SEEDS") ?? "5");

    private static Outcome Run(Stats g, Stats z, Vector2Int[] layout, int seed = 0)
    {
        var grid = new DefenseGrid();
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
            float dt = seed == 0 ? Step : 1f / (40f + (float)rng.NextDouble() * 104f);
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
