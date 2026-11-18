using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseBattle
{
    public const int CoreMaxHealth = 120;
    public const int HealAmount = 15;
    public const float BuffDuration = 5f;
    public const float AreaSpellRadius = 2f;
    public const int MaxMana = 100;

    private const int CastsPerSpell = 3;
    private const float StartingMana = MaxMana * 0.5f;
    private const float ManaPerSecond = 10f;
    // 스폰 지점마다 이 간격으로 한 마리씩 줄지어 나온다.
    private const float SpawnInterval = 0.6f;
    internal const float UnitSpacing = 0.44f;
    private const float ForecastStep = 1f / 60f;
    private const int ForecastTickLimit = 90 * 60;
    private const float CombatPlanInterval = 0.12f;
    private const float RouteYieldDelay = 0.35f;
    private const int RouteRejoinWaypoints = 3;

    public enum Phase { Preparation, Combat, Defeat }
    public enum Spell { AttackBuff, Heal, MoveSpeedBuff }

    private readonly DefenseGrid grid;
    private readonly EnemyFaction[] factions;
    private readonly List<EnemyUnit> enemies = new();
    private readonly CombatPositioning combat;
    private readonly int[] spawnedPerFaction;
    private float untilSpawn;
    private float untilCombatPlan;

    public event Action<EnemyUnit> EnemySpawned;
    public event Action<EnemyUnit> EnemyRemoved;

    public IReadOnlyList<EnemyUnit> Enemies => enemies;
    public Phase CurrentPhase { get; private set; }
    public int Round { get; private set; } = 1;
    public int CoreHealth { get; private set; } = CoreMaxHealth;
    public int SpawnCount { get; private set; }
    // 진영별 SpawnCount 의 합. 스테이지 구성은 각 EnemyFaction 의 spawnCount 로 정한다.
    public int EnemiesPerRound { get; }
    public int AttackBuffsLeft { get; private set; }
    public int MoveSpeedBuffsLeft { get; private set; }
    public int HealsLeft { get; private set; }
    public float Mana { get; private set; }

    public DefenseBattle(DefenseGrid grid, EnemyFaction[] factions)
    {
        this.grid = grid;
        this.factions = factions;
        combat = new CombatPositioning(grid, enemies, UnitSpacing);
        spawnedPerFaction = new int[factions.Length];
        foreach (EnemyFaction faction in factions)
            EnemiesPerRound += faction.SpawnCount;
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
        HealsLeft = CastsPerSpell;
        Mana = StartingMana;
    }

    public void RecoverMana(float deltaTime)
    {
        if (CurrentPhase == Phase.Combat)
            Mana = Mathf.Min(MaxMana, Mana + ManaPerSecond * deltaTime);
    }

    public static int ManaCost(Spell spell) => spell == Spell.Heal ? 40 : 30;

    public int CastsLeft(Spell spell) => spell switch
    {
        Spell.AttackBuff => AttackBuffsLeft,
        Spell.MoveSpeedBuff => MoveSpeedBuffsLeft,
        _ => HealsLeft
    };

    public string SpellBlockReason(Spell spell)
    {
        if (CurrentPhase != Phase.Combat)
            return "Battle only";
        if (CastsLeft(spell) == 0)
            return "No casts left";
        if (Mana < ManaCost(spell))
            return "Not enough mana";
        return null;
    }

    public static bool IsInAreaSpellRange(EnemyUnit enemy, Vector3 center)
    {
        Vector3 offset = enemy.Position - center;
        return enemy.IsAlive && offset.x * offset.x + offset.z * offset.z <= AreaSpellRadius * AreaSpellRadius;
    }

    public static bool CanReceiveAreaSpell(EnemyUnit enemy, Spell spell) => spell switch
    {
        Spell.AttackBuff => enemy.AttackBuffRemaining <= 0,
        Spell.MoveSpeedBuff => enemy.MoveSpeedBuffRemaining <= 0,
        Spell.Heal => enemy.Health < enemy.MaxHealth,
        _ => false
    };

    public string CastAreaSpell(Spell spell, Vector3 center)
    {
        string blocked = SpellBlockReason(spell);
        if (blocked != null)
            return blocked;

        int affected = 0;
        foreach (EnemyUnit enemy in enemies)
        {
            if (!IsInAreaSpellRange(enemy, center) || !CanReceiveAreaSpell(enemy, spell))
                continue;

            switch (spell)
            {
                case Spell.AttackBuff:
                    enemy.ApplyAttackBuff(BuffDuration);
                    break;
                case Spell.MoveSpeedBuff:
                    enemy.ApplyMoveSpeedBuff(BuffDuration);
                    break;
                case Spell.Heal:
                    enemy.Heal(HealAmount);
                    break;
            }
            affected++;
        }

        if (affected == 0)
            return spell == Spell.Heal ? "No injured enemies in range." : "No eligible enemies in range.";

        Mana -= ManaCost(spell);
        switch (spell)
        {
            case Spell.AttackBuff:
                AttackBuffsLeft--;
                break;
            case Spell.MoveSpeedBuff:
                MoveSpeedBuffsLeft--;
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
        var cells = new HashSet<Vector2Int>();
        var forecast = new DefenseBattle(grid, factions) { CoreHealth = CoreHealth };
        forecast.StartRound();
        for (int i = 0; i < ForecastTickLimit && forecast.CurrentPhase == Phase.Combat; i++)
            forecast.Tick(ForecastStep, pauseMovement: false, cells);
        return cells;
    }

    public void Tick(float deltaTime, bool pauseMovement) => Tick(deltaTime, pauseMovement, null);

    private void Tick(float deltaTime, bool pauseMovement, HashSet<Vector2Int> forecastCells)
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
                {
                    if (forecastCells != null)
                    {
                        Vector2Int cell = DefenseGrid.PositionToCell((enemy.Position + target.Position) * 0.5f);
                        if (!grid.IsWalkable(cell))
                            cell = DefenseGrid.PositionToCell(enemy.Position);
                        forecastCells.Add(cell);
                    }
                    target.QueueDamage(enemy.Attack());
                }
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
                if (enemy.Target is { IsAlive: true } target)
                {
                    combat.Move(enemy, target, deltaTime);
                    continue;
                }

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
        }
    }

    private void SpawnNext()
    {
        for (int factionIndex = 0; factionIndex < grid.Routes.Count; factionIndex++)
        {
            if (spawnedPerFaction[factionIndex] >= factions[factionIndex].SpawnCount)
                continue;

            spawnedPerFaction[factionIndex]++;
            var path = (Vector3[])grid.Routes[factionIndex].Clone();
            var enemy = new EnemyUnit(factions[factionIndex], factionIndex, path, SpawnCount);
            SpawnCount++;
            enemies.Add(enemy);
            EnemySpawned?.Invoke(enemy);
        }
    }

    private bool SpawnPointsClear()
    {
        foreach (Vector3[] route in grid.Routes)
        {
            foreach (EnemyUnit enemy in enemies)
                if ((enemy.Position - route[0]).sqrMagnitude < UnitSpacing * UnitSpacing)
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
