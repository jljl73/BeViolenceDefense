using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseBattle
{
    public const int CoreMaxHealth = 120;
    public const float HealPercent = 0.25f;
    public const float BuffDuration = 6f;
    public const float DefaultAreaSpellRadius = DefaultSpellRadiusTenths / 10f;
    public const int MinSpellRangeStep = -4;
    public const int MaxSpellRangeStep = 5;
    public const int MaxMana = 70;

    private const int CastsPerSpell = 3;
    private const int DefaultSpellRadiusTenths = 14;
    private const int MaxSpellManaAdjustment = 2;
    private const float StartingMana = MaxMana * 0.5f;
    private const float ManaPerSecond = 7f;
    // 스폰 지점마다 이 간격으로 한 마리씩 줄지어 나온다.
    private const float SpawnInterval = 0.6f;
    internal const float UnitSpacing = 0.44f;
    private const float CombatPlanInterval = 0.12f;
    private const float RouteYieldDelay = 0.35f;
    private const int RouteRejoinWaypoints = 3;
    private const int ReinforcementsPerFaction = 1;
    private const int ExtraReinforcements = 3;

    public enum Phase { Preparation, Combat, Defeat }
    public enum Spell { AttackBuff, Heal, MoveSpeedBuff, ArmorBuff }

    private readonly DefenseGrid grid;
    private readonly EnemyFaction[] factions;
    private readonly List<EnemyUnit> enemies = new();
    private readonly CombatPositioning combat;
    private readonly int[] spawnedPerFaction;
    private readonly int[] roundSpawnCounts;
    // 진영 -> 그 진영 스폰의 경로 인덱스. 진영 안에서는 스폰을 번갈아 쓴다.
    private readonly int[][] factionSpawns;
    // 라운드 번호 -> 진영(편)별 스폰 순서. null 이면 진영 하나를 기존 증원 규칙대로 반복한다.
    private readonly Func<int, IReadOnlyList<EnemyFaction>[]> roundLineup;
    private IReadOnlyList<EnemyFaction>[] roundQueues;
    private float untilSpawn;
    private float untilCombatPlan;

    public event Action<EnemyUnit> EnemySpawned;
    public event Action<EnemyUnit> EnemyRemoved;

    public IReadOnlyList<EnemyUnit> Enemies => enemies;
    public Phase CurrentPhase { get; private set; }
    public int Round { get; private set; } = 1;
    public int CoreHealth { get; private set; } = CoreMaxHealth;
    public int SpawnCount { get; private set; }
    public int EnemiesPerRound { get; private set; }
    public int AttackBuffsLeft { get; private set; }
    public int MoveSpeedBuffsLeft { get; private set; }
    public int ArmorBuffsLeft { get; private set; }
    public int HealsLeft { get; private set; }
    public float Mana { get; private set; }

    public DefenseBattle(DefenseGrid grid, EnemyFaction[] factions,
        Func<int, IReadOnlyList<EnemyFaction>[]> roundLineup = null)
    {
        this.grid = grid;
        this.factions = factions;
        this.roundLineup = roundLineup;
        combat = new CombatPositioning(grid, enemies, UnitSpacing);
        spawnedPerFaction = new int[factions.Length];
        roundSpawnCounts = new int[factions.Length];
        factionSpawns = new int[factions.Length][];
        for (int f = 0; f < factions.Length; f++)
        {
            var routes = new List<int>();
            for (int i = 0; i < grid.SpawnFactions.Count; i++)
                if (grid.SpawnFactions[i] == f)
                    routes.Add(i);
            factionSpawns[f] = routes.ToArray();
        }
        PrepareRoundSpawns();
    }

    private void PrepareRoundSpawns()
    {
        int roundsCleared = Round - 1;
        EnemiesPerRound = 0;
        if (roundLineup != null)
        {
            roundQueues = roundLineup(Round);
            for (int i = 0; i < factions.Length; i++)
            {
                roundSpawnCounts[i] = roundQueues != null && i < roundQueues.Length && roundQueues[i] != null
                    ? roundQueues[i].Count
                    : 0;
                EnemiesPerRound += roundSpawnCounts[i];
            }
            return;
        }

        for (int i = 0; i < factions.Length; i++)
        {
            int baseCount = factions[i].SpawnCount;
            // Teal receives the first larger reinforcement, then Coral and Teal alternate.
            int largerReinforcements = roundsCleared / 2 + (i == 1 ? roundsCleared % 2 : 0);
            int reinforcements = roundsCleared * ReinforcementsPerFaction + largerReinforcements * ExtraReinforcements;
            roundSpawnCounts[i] = baseCount > 0 ? baseCount + reinforcements : 0;
            EnemiesPerRound += roundSpawnCounts[i];
        }
    }

    public void StartRound()
    {
        if (CurrentPhase != Phase.Preparation)
            return;

        CurrentPhase = Phase.Combat;
        SpawnCount = 0;
        Array.Clear(spawnedPerFaction, 0, spawnedPerFaction.Length);
        untilSpawn = 0.35f;
        untilCombatPlan = 0f;
        AttackBuffsLeft = CastsPerSpell;
        MoveSpeedBuffsLeft = CastsPerSpell;
        ArmorBuffsLeft = CastsPerSpell;
        HealsLeft = CastsPerSpell;
        Mana = StartingMana;
    }

    public void RecoverMana(float deltaTime)
    {
        if (CurrentPhase == Phase.Combat)
            Mana = Mathf.Min(MaxMana, Mana + ManaPerSecond * deltaTime);
    }

    public static float AreaSpellRadius(int rangeStep) =>
        (DefaultSpellRadiusTenths + Mathf.Clamp(rangeStep, MinSpellRangeStep, MaxSpellRangeStep) * 2) / 10f;

    public static int ManaCost(Spell spell, int rangeStep = 0) =>
        (spell == Spell.Heal ? 32 : 24) + Mathf.Clamp(rangeStep, -MaxSpellManaAdjustment, MaxSpellManaAdjustment);

    public int InitialSpellRangeStep(Spell spell) =>
        Mathf.Clamp(Mathf.FloorToInt(Mana) - ManaCost(spell), -MaxSpellManaAdjustment, 0);

    public int CastsLeft(Spell spell) => spell switch
    {
        Spell.AttackBuff => AttackBuffsLeft,
        Spell.MoveSpeedBuff => MoveSpeedBuffsLeft,
        Spell.ArmorBuff => ArmorBuffsLeft,
        _ => HealsLeft
    };

    public string SpellBlockReason(Spell spell, int rangeStep)
    {
        if (CurrentPhase != Phase.Combat)
            return "Battle only";
        if (CastsLeft(spell) == 0)
            return "No casts left";
        if (Mana < ManaCost(spell, rangeStep))
            return "Not enough mana";
        return null;
    }

    public static bool IsInAreaSpellRange(EnemyUnit enemy, Vector3 center, float radius)
    {
        Vector3 offset = enemy.Position - center;
        return enemy.IsAlive && offset.x * offset.x + offset.z * offset.z <= radius * radius;
    }

    public static bool CanReceiveAreaSpell(EnemyUnit enemy, Spell spell) => spell switch
    {
        Spell.AttackBuff or Spell.MoveSpeedBuff or Spell.ArmorBuff => enemy.IsAlive,
        Spell.Heal => enemy.Health < enemy.MaxHealth,
        _ => false
    };

    public string CastAreaSpell(Spell spell, Vector3 center, int rangeStep, List<EnemyUnit> recipients = null)
    {
        recipients?.Clear();
        string blocked = SpellBlockReason(spell, rangeStep);
        if (blocked != null)
            return blocked;

        float radius = AreaSpellRadius(rangeStep);
        int affected = 0;
        foreach (EnemyUnit enemy in enemies)
        {
            if (!IsInAreaSpellRange(enemy, center, radius) || !CanReceiveAreaSpell(enemy, spell))
                continue;

            switch (spell)
            {
                case Spell.AttackBuff:
                    enemy.ApplyAttackBuff(BuffDuration);
                    break;
                case Spell.MoveSpeedBuff:
                    enemy.ApplyMoveSpeedBuff(BuffDuration);
                    break;
                case Spell.ArmorBuff:
                    enemy.ApplyArmorBuff(BuffDuration);
                    break;
                case Spell.Heal:
                    enemy.Heal(Mathf.CeilToInt(enemy.MaxHealth * HealPercent));
                    break;
            }
            recipients?.Add(enemy);
            affected++;
        }

        if (affected == 0)
            return spell == Spell.Heal ? "No injured enemies in range." : "No eligible enemies in range.";

        Mana -= ManaCost(spell, rangeStep);
        switch (spell)
        {
            case Spell.AttackBuff:
                AttackBuffsLeft--;
                break;
            case Spell.MoveSpeedBuff:
                MoveSpeedBuffsLeft--;
                break;
            case Spell.ArmorBuff:
                ArmorBuffsLeft--;
                break;
            case Spell.Heal:
                HealsLeft--;
                break;
        }
        string action = spell == Spell.Heal ? "Healed" : "Buffed";
        return affected == 1 ? $"{action} 1 enemy." : $"{action} {affected} enemies.";
    }

    public IReadOnlyCollection<Vector2Int> ForecastEncounterCells()
    {
        if (roundSpawnCounts[0] == 0 || roundSpawnCounts[1] == 0)
            return Array.Empty<Vector2Int>();

        float range = Mathf.Max(RoundUnit(0, 0).AttackRange, RoundUnit(1, 0).AttackRange);
        // 임시 가정: 모든 스폰의 선두가 동시에 출발한다. Coral·Teal 스폰 쌍마다 교전 칸 하나.
        var cells = new HashSet<Vector2Int>();
        foreach (int first in factionSpawns[0])
            foreach (int second in factionSpawns[1])
                if (ForecastEncounter(grid.Routes[first], grid.Routes[second], range) is { } cell)
                    cells.Add(cell);
        return cells;
    }

    private Vector2Int? ForecastEncounter(Vector3[] firstRoute, Vector3[] secondRoute, float range)
    {
        // Compare the leaders after each tile step. The last point is a core contact, not a tile.
        int tileCount = Mathf.Min(firstRoute.Length, secondRoute.Length) - 1;
        for (int step = 0; step < tileCount; step++)
        {
            Vector3 first = firstRoute[step];
            Vector3 second = secondRoute[step];
            if ((first - second).sqrMagnitude > range * range || !grid.HasLineOfSight(first, second))
                continue;

            Vector2Int center = DefenseGrid.PositionToCell((first + second) * 0.5f);
            return grid.IsWalkable(center) ? center : DefenseGrid.PositionToCell(first);
        }

        return null;
    }

    public void Tick(float deltaTime, bool pauseMovement)
    {
        if (CurrentPhase != Phase.Combat)
            return;

        untilSpawn -= deltaTime;
        while (SpawnCount < EnemiesPerRound && untilSpawn <= 0)
        {
            if (!SpawnPointsClear())
            {
                untilSpawn = 0f;
                break;
            }

            untilSpawn += SpawnInterval;
            SpawnNext();
            untilCombatPlan = 0f;
        }

        untilCombatPlan -= deltaTime;
        if (untilCombatPlan <= 0f)
        {
            combat.Plan();
            untilCombatPlan = CombatPlanInterval;
        }

        // Evaluate attacks from a common snapshot. Both sides of a duel can land a lethal hit.
        foreach (EnemyUnit enemy in enemies)
        {
            enemy.TickTimers(deltaTime);
            if (enemy.Target is { IsAlive: true } target)
            {
                enemy.Face(target.Position);
                if (enemy.CanAttack && combat.CanAttack(enemy, target))
                    target.QueueDamage(enemy.Attack(target));
            }
        }

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyUnit enemy = enemies[i];
            enemy.ResolveDamage();
            if (!enemy.IsAlive)
                RemoveEnemyAt(i);
        }

        if (!pauseMovement)
        {
            foreach (EnemyUnit enemy in enemies)
            {
                if ((enemy.Target ?? enemy.CombatQueue?.Target) is { IsAlive: true } target)
                {
                    if (enemy.Target == null)
                        enemy.Face(target.Position);
                    combat.Move(enemy, target, deltaTime);
                    continue;
                }

                enemy.CombatQueue = null;
                AdvanceUnit(enemy, deltaTime);
            }

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyUnit enemy = enemies[i];
                if (!enemy.ReachedCore)
                    continue;

                CoreHealth = Mathf.Max(0, CoreHealth - 1);
                enemy.Die();
                RemoveEnemyAt(i);
            }
        }

        if (CoreHealth == 0)
        {
            CurrentPhase = Phase.Defeat;
            return;
        }

        if (SpawnCount == EnemiesPerRound && enemies.Count == 0)
        {
            CurrentPhase = Phase.Preparation;
            Mana = 0f;
            Round++;
            PrepareRoundSpawns();
        }
    }

    // 이번 라운드에 진영(편) side 에서 index 번째로 나오는 유닛 종류.
    public EnemyFaction RoundUnit(int side, int index) =>
        roundQueues != null ? roundQueues[side][index] : factions[side];

    public int RoundSpawnCount(int side) => roundSpawnCounts[side];

    private void SpawnNext()
    {
        for (int factionIndex = 0; factionIndex < factions.Length; factionIndex++)
        {
            if (spawnedPerFaction[factionIndex] >= roundSpawnCounts[factionIndex])
                continue;

            var path = (Vector3[])NextRoute(factionIndex).Clone();
            spawnedPerFaction[factionIndex]++;
            var enemy = new EnemyUnit(RoundUnit(factionIndex, spawnedPerFaction[factionIndex] - 1), factionIndex, path,
                SpawnCount);
            SpawnCount++;
            enemies.Add(enemy);
            EnemySpawned?.Invoke(enemy);
        }
    }

    // 이 진영의 다음 유닛이 출발할 경로.
    private Vector3[] NextRoute(int factionIndex)
    {
        int[] spawns = factionSpawns[factionIndex];
        return grid.Routes[spawns[spawnedPerFaction[factionIndex] % spawns.Length]];
    }

    private bool SpawnPointsClear()
    {
        for (int factionIndex = 0; factionIndex < factions.Length; factionIndex++)
        {
            if (spawnedPerFaction[factionIndex] >= roundSpawnCounts[factionIndex])
                continue;

            Vector3 spawn = NextRoute(factionIndex)[0];
            foreach (EnemyUnit enemy in enemies)
                if ((enemy.Position - spawn).sqrMagnitude < UnitSpacing * UnitSpacing)
                    return false;
        }
        return true;
    }

    private void RemoveEnemyAt(int index)
    {
        EnemyUnit enemy = enemies[index];
        enemies.RemoveAt(index);
        untilCombatPlan = 0f;
        EnemyRemoved?.Invoke(enemy);
    }

    private void AdvanceUnit(EnemyUnit enemy, float deltaTime)
    {
        if (enemy.RouteYieldDestination is { } yieldPoint)
        {
            Vector3 step = enemy.StepToward(yieldPoint, deltaTime);
            if (combat.CanFollowRoute(enemy, step))
            {
                enemy.Face(step);
                enemy.MoveOffRoute(step);
                if (step.Equals(yieldPoint))
                    enemy.RouteYieldDestination = null;
                return;
            }
            enemy.RouteYieldDestination = null;
        }

        if (!enemy.Position.Equals(enemy.RoutePosition) && TryRejoinRoute(enemy, deltaTime))
            return;

        Vector3 next = enemy.NextRoutePosition(deltaTime);
        enemy.Face(next);
        if (combat.CanFollowRoute(enemy, next))
        {
            enemy.FollowRoute(next);
            enemy.BlockedRouteTime = 0f;
            return;
        }

        enemy.BlockedRouteTime += deltaTime;
        if (enemy.BlockedRouteTime < RouteYieldDelay)
            return;
        enemy.BlockedRouteTime = 0f;
        if (combat.TryFindRouteYield(enemy, out Vector3 destination))
            enemy.RouteYieldDestination = destination;
    }

    private bool TryRejoinRoute(EnemyUnit enemy, float deltaTime)
    {
        // Skip a blocked return point only when the path to a forward waypoint is clear.
        int count = Mathf.Min(RouteRejoinWaypoints, enemy.RemainingRouteWaypoints);
        for (int offset = 0; offset < count; offset++)
        {
            Vector3 waypoint = enemy.GetRouteWaypoint(offset);
            if (!grid.CanPursue(enemy.Position, waypoint) || !combat.CanFollowRoute(enemy, waypoint))
                continue;

            Vector3 next = enemy.StepToward(waypoint, deltaTime);
            enemy.Face(next);
            enemy.FollowRoute(next, offset);
            enemy.BlockedRouteTime = 0f;
            return true;
        }
        return false;
    }
}
