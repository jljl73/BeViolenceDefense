using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseBattle
{
    public const int CoreMaxHealth = 120;
    public const int EnemiesPerRound = 6;
    public const int HealAmount = 15;
    public const float BuffDuration = 5f;
    public const float AreaBuffRadius = 2f;
    public const int MaxMana = 100;

    private const int CastsPerSpell = 3;
    private const float StartingMana = MaxMana * 0.5f;
    private const float ManaPerSecond = 10f;
    private const float SpawnInterval = 1.15f;
    private const float UnitSpacing = 0.42f;
    private const float ForecastStep = 1f / 60f;
    private const int ForecastTickLimit = 90 * 60;

    public enum Phase { Preparation, Combat, Defeat }
    public enum Spell { AttackBuff, Heal, MoveSpeedBuff }

    private readonly DefenseGrid grid;
    private readonly EnemyFaction[] factions;
    private readonly List<EnemyUnit> enemies = new();
    private float untilSpawn;

    public event Action<EnemyUnit> EnemySpawned;
    public event Action<EnemyUnit> EnemyRemoved;

    public IReadOnlyList<EnemyUnit> Enemies => enemies;
    public Phase CurrentPhase { get; private set; }
    public int Round { get; private set; } = 1;
    public int CoreHealth { get; private set; } = CoreMaxHealth;
    public int SpawnCount { get; private set; }
    public int AttackBuffsLeft { get; private set; }
    public int MoveSpeedBuffsLeft { get; private set; }
    public int HealsLeft { get; private set; }
    public float Mana { get; private set; }

    public DefenseBattle(DefenseGrid grid, EnemyFaction[] factions)
    {
        this.grid = grid;
        this.factions = factions;
    }

    public void StartRound()
    {
        if (CurrentPhase != Phase.Preparation)
            return;

        CurrentPhase = Phase.Combat;
        SpawnCount = 0;
        untilSpawn = 0.35f;
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

    public string SpellBlockReason(EnemyUnit unit, Spell spell)
    {
        if (CurrentPhase != Phase.Combat)
            return "Battle only";
        if (CastsLeft(spell) == 0)
            return "No casts left";
        if (Mana < ManaCost(spell))
            return "Not enough mana";
        if (spell != Spell.Heal)
            return null;
        if (unit == null || !unit.IsAlive || !enemies.Contains(unit))
            return "Select an enemy";

        return unit.Health == EnemyUnit.MaxHealth ? "Target at full health" : null;
    }

    public string CastAreaBuff(Spell spell, Vector3 center)
    {
        if (spell == Spell.Heal)
            throw new ArgumentException("Only area buffs can target a board position.", nameof(spell));

        string blocked = SpellBlockReason(null, spell);
        if (blocked != null)
            return blocked;

        bool isAttackBuff = spell == Spell.AttackBuff;
        int affected = 0;
        foreach (EnemyUnit enemy in enemies)
        {
            Vector3 offset = enemy.Position - center;
            float distanceSquared = offset.x * offset.x + offset.z * offset.z;
            float remaining = isAttackBuff ? enemy.AttackBuffRemaining : enemy.MoveSpeedBuffRemaining;
            if (!enemy.IsAlive || remaining > 0 || distanceSquared > AreaBuffRadius * AreaBuffRadius)
                continue;

            if (isAttackBuff)
                enemy.ApplyAttackBuff(BuffDuration);
            else
                enemy.ApplyMoveSpeedBuff(BuffDuration);
            affected++;
        }

        if (affected == 0)
            return "No eligible enemies in range.";

        Mana -= ManaCost(spell);
        if (isAttackBuff)
            AttackBuffsLeft--;
        else
            MoveSpeedBuffsLeft--;
        return affected == 1 ? "Buffed 1 enemy." : $"Buffed {affected} enemies.";
    }

    public string CastHeal(EnemyUnit unit)
    {
        string blocked = SpellBlockReason(unit, Spell.Heal);
        if (blocked != null)
            return blocked;

        Mana -= ManaCost(Spell.Heal);
        unit.Heal(HealAmount);
        HealsLeft--;
        return "Health restored.";
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
        if (SpawnCount < EnemiesPerRound && untilSpawn <= 0)
        {
            var enemy = new EnemyUnit(factions[SpawnCount % factions.Length],
                grid.Routes[SpawnCount % grid.Routes.Count], SpawnCount);
            SpawnCount++;
            untilSpawn += SpawnInterval;
            enemies.Add(enemy);
            EnemySpawned?.Invoke(enemy);
        }

        // Evaluate attacks from a common snapshot. Both sides of a duel can land a lethal hit.
        int coreDamage = 0;
        foreach (EnemyUnit enemy in enemies)
        {
            enemy.TickTimers(deltaTime);
            enemy.Target = FindTarget(enemy);
            enemy.AttackingCore = enemy.Target == null && CanReach(enemy, grid.CorePosition);
            if (enemy.Target != null)
            {
                enemy.Face(enemy.Target.Position);
                if (enemy.CanAttack)
                {
                    if (forecastCells != null)
                    {
                        Vector2Int cell = DefenseGrid.PositionToCell((enemy.Position + enemy.Target.Position) * 0.5f);
                        if (!grid.IsWalkable(cell))
                            cell = DefenseGrid.PositionToCell(enemy.Position);
                        forecastCells.Add(cell);
                    }
                    enemy.Target.QueueDamage(enemy.Attack());
                }
            }
            else if (enemy.AttackingCore)
            {
                enemy.Face(grid.CorePosition);
                if (enemy.CanAttack)
                    coreDamage += enemy.Attack();
            }
        }

        CoreHealth = Mathf.Max(0, CoreHealth - coreDamage);
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyUnit enemy = enemies[i];
            enemy.ResolveDamage();
            if (!enemy.IsAlive)
            {
                enemies.RemoveAt(i);
                EnemyRemoved?.Invoke(enemy);
            }
        }

        if (CoreHealth == 0)
        {
            CurrentPhase = Phase.Defeat;
            return;
        }

        if (!pauseMovement)
        {
            foreach (EnemyUnit enemy in enemies)
            {
                if (enemy.Target == null && !enemy.AttackingCore)
                    AdvanceUnit(enemy, deltaTime);
            }
        }

        if (SpawnCount == EnemiesPerRound && enemies.Count == 0)
        {
            CurrentPhase = Phase.Preparation;
            Mana = 0f;
            Round++;
        }
    }

    private EnemyUnit FindTarget(EnemyUnit enemy)
    {
        if (enemy.Target is { IsAlive: true } && CanReach(enemy, enemy.Target.Position))
            return enemy.Target;

        EnemyUnit nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (EnemyUnit other in enemies)
        {
            if (other.Faction == enemy.Faction || !CanReach(enemy, other.Position))
                continue;

            float distance = (other.Position - enemy.Position).sqrMagnitude;
            if (distance <= nearestDistance)
            {
                nearest = other;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    private bool CanReach(EnemyUnit attacker, Vector3 target)
    {
        float range = attacker.Faction.AttackRange;
        return (target - attacker.Position).sqrMagnitude <= range * range &&
            grid.HasLineOfSight(attacker.Position, target);
    }

    private void AdvanceUnit(EnemyUnit enemy, float deltaTime)
    {
        Vector3 next = enemy.NextPosition(deltaTime);
        Vector3 step = next - enemy.Position;
        enemy.Face(next);

        float remaining = enemy.DistanceToCore;
        foreach (EnemyUnit other in enemies)
        {
            // Only yield to units with right of way, so two branches of a merge cannot wait on each other.
            int order = other.DistanceToCore.CompareTo(remaining);
            bool hasRightOfWay = order < 0 || (order == 0 && other.SpawnOrder < enemy.SpawnOrder);
            if (hasRightOfWay && Vector3.Dot(step, other.Position - enemy.Position) > 0 &&
                (other.Position - next).sqrMagnitude < UnitSpacing * UnitSpacing &&
                grid.HasLineOfSight(enemy.Position, other.Position))
            {
                return;
            }
        }
        enemy.MoveTo(next);
    }
}
