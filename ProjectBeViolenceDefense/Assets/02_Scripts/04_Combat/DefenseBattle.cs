using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseBattle
{
    public const int CoreMaxHealth = 120;
    // 전투 로직의 고정 틱. 공격 간격·지속시간이 이 단위로 딱 떨어지도록 60을 쓴다.
    public const int TicksPerSecond = 60;
    public const float TickDuration = 1f / TicksPerSecond;
    public const float HealPercent = 0.50f;
    public const float BuffDuration = 6f;
    // 도발: 고른 유닛이 이 시간 동안 거대해지고, 반경 안의 다른 진영 유닛을 벽 너머까지 끌어당긴다. 첫 테스트용 임시 값.
    public const float TauntDuration = 5f;
    public const float TauntRadius = 3.5f;
    public const float DefaultAreaSpellRadius = DefaultSpellRadiusTenths / 10f;
    public const int MinSpellRangeStep = -4;
    public const int MaxSpellRangeStep = 5;
    public const int MaxMana = 70;

    private const int DefaultSpellRadiusTenths = 14;
    private const int MaxSpellManaAdjustment = 2;
    private const float StartingMana = MaxMana * 0.5f;
    private const float ManaPerSecond = 7f;
    // 스폰 지점마다 이 간격으로 한 마리씩 줄지어 나온다.
    private const float SpawnInterval = 0.6f;
    private const float CombatPlanInterval = 0.12f;
    private const float RouteYieldDelay = 0.35f;
    private const int RouteRejoinWaypoints = 3;
    // 교전 뒤 원래 경로 대신 지금 위치에서 새 경로를 잡으려면 줄어야 하는 최소 거리(칸). 첫 테스트용 임시 값.
    private const float RerouteMinimumSaving = 2f;
    // 경로로 돌아갈 때, 코어까지 가장 짧게 이어지는 점보다 이 칸 수 넘게 손해인 점(지나온 쪽)은 고르지 않는다.
    private const float RejoinSlack = 0.5f;
    private const int ReinforcementsPerFaction = 1;
    private const int ExtraReinforcements = 3;

    public enum Phase { Preparation, Combat, Defeat, Victory }
    public enum Spell { AttackBuff, Heal, MoveSpeedBuff, ArmorBuff, Taunt }

    private readonly DefenseGrid grid;
    private readonly EnemyFaction[] factions;
    private readonly List<EnemyUnit> enemies = new();
    private readonly CombatPositioning combat;
    private readonly int[] spawnedPerFaction;
    private readonly int[] roundSpawnCounts;
    // 진영 -> 그 진영 스폰의 경로 인덱스. 진영 안에서는 스폰을 번갈아 쓴다.
    private readonly int[][] factionSpawns;
    // 진영 -> 이번 라운드에 쓰는 경로 인덱스. -1 이면 그 진영의 스폰을 번갈아 쓴다.
    private readonly int[] roundRoute;
    // 스폰이 여럿인 진영에서 라운드마다 하나를 고른다. pick(n) 은 0..n-1. null 이면 번갈아 쓴다.
    private readonly Func<int, int> pickSpawn;
    // 라운드 번호 -> 진영(편)별 스폰 순서. null 이면 진영 하나를 기존 증원 규칙대로 반복한다.
    private readonly Func<int, IReadOnlyList<EnemyFaction>[]> roundLineup;
    // 이 라운드를 클리어하면 승리. 0 이면 끝없이 이어진다.
    private readonly int roundLimit;
    private IReadOnlyList<EnemyFaction>[] roundQueues;
    private float untilSpawn;
    private float untilCombatPlan;

    public event Action<EnemyUnit> EnemySpawned;
    // 큐에 넣은 마법이 틱 시작에 적용되었을 때. 메시지와 실제로 효과를 받은 유닛을 함께 알린다.
    public event Action<SpellCommand, string, IReadOnlyList<EnemyUnit>> SpellResolved;

    // 결과를 바꾸는 플레이어 입력. 틱 번호가 같으면 프레임 속도와 관계없이 같은 결과가 난다.
    public readonly struct SpellCommand
    {
        public SpellCommand(int tick, Spell spell, Vector3 center, int rangeStep, int targetSpawnOrder = -1)
        {
            Tick = tick;
            Spell = spell;
            Center = center;
            RangeStep = rangeStep;
            TargetSpawnOrder = targetSpawnOrder;
        }

        public int Tick { get; }
        public Spell Spell { get; }
        public Vector3 Center { get; }
        public int RangeStep { get; }
        // 유닛 하나를 고르는 마법(도발)의 대상. 범위 마법은 -1.
        public int TargetSpawnOrder { get; }
    }

    private readonly List<SpellCommand> pendingSpells = new();
    private readonly List<SpellCommand> commandHistory = new();
    private readonly List<EnemyUnit> resolvedRecipients = new();

    // 이번 라운드에 진행한 틱 수. 다음에 돌 틱의 번호이기도 하다.
    public int TickIndex { get; private set; }
    // 이번 라운드에 적용된 마법 기록. 같은 지형·편성에서 이 기록을 다시 넣으면 같은 판이 재현된다.
    public IReadOnlyList<SpellCommand> CommandHistory => commandHistory;
    public event Action<EnemyUnit> EnemyRemoved;
    public event Action CoreHit;

    public IReadOnlyList<EnemyUnit> Enemies => enemies;
    public Phase CurrentPhase { get; private set; }
    public int Round { get; private set; } = 1;
    public int MaxCoreHealth { get; }
    public int CoreHealth { get; private set; }
    public bool HasCoreShield { get; private set; }
    public int SpawnCount { get; private set; }
    public int EnemiesPerRound { get; private set; }
    public float Mana { get; private set; }

    public DefenseBattle(DefenseGrid grid, EnemyFaction[] factions,
        Func<int, IReadOnlyList<EnemyFaction>[]> roundLineup = null, int roundLimit = 0,
        Func<int, int> pickSpawn = null, int coreMaxHealth = CoreMaxHealth)
    {
        this.grid = grid;
        this.factions = factions;
        this.roundLineup = roundLineup;
        this.roundLimit = roundLimit;
        this.pickSpawn = pickSpawn;
        MaxCoreHealth = CoreHealth = coreMaxHealth;
        roundRoute = new int[factions.Length];
        combat = new CombatPositioning(grid, enemies);
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
        for (int i = 0; i < factions.Length; i++)
        {
            int[] spawns = factionSpawns[i];
            roundRoute[i] = pickSpawn != null && spawns.Length > 1 ? spawns[pickSpawn(spawns.Length)] : -1;
        }
        if (roundLineup != null)
        {
            roundQueues = roundLineup(Round);
            for (int i = 0; i < factions.Length; i++)
            {
                // 스폰 지점이 없는 진영(종족)은 이 스테이지에 나오지 않는다.
                roundSpawnCounts[i] = factionSpawns[i].Length > 0 && roundQueues != null && i < roundQueues.Length &&
                    roundQueues[i] != null
                    ? roundQueues[i].Count
                    : 0;
                EnemiesPerRound += roundSpawnCounts[i];
            }
            return;
        }

        for (int i = 0; i < factions.Length; i++)
        {
            roundSpawnCounts[i] = ReinforcedSpawnCount(i, roundsCleared);
            EnemiesPerRound += roundSpawnCounts[i];
        }
    }

    private int ReinforcedSpawnCount(int side, int roundsCleared)
    {
        int baseCount = factionSpawns[side].Length > 0 ? factions[side].SpawnCount : 0;
        // Faction 1 (Undead) receives the first larger reinforcement, then it alternates with the others.
        int largerReinforcements = roundsCleared / 2 + (side == 1 ? roundsCleared % 2 : 0);
        int reinforcements = roundsCleared * ReinforcementsPerFaction + largerReinforcements * ExtraReinforcements;
        return baseCount > 0 ? baseCount + reinforcements : 0;
    }

    /// <summary>다음 라운드에 진영(편) side 가 내보낼 유닛 수. 지금 라운드의 상태는 바꾸지 않는다.</summary>
    public int NextRoundSpawnCount(int side)
    {
        if (factionSpawns[side].Length == 0)
            return 0;
        if (roundLineup == null)
            return ReinforcedSpawnCount(side, Round);

        IReadOnlyList<EnemyFaction>[] next = roundLineup(Round + 1);
        return next != null && side < next.Length && next[side] != null ? next[side].Count : 0;
    }

    public void StartRound()
    {
        if (CurrentPhase != Phase.Preparation)
            return;

        CurrentPhase = Phase.Combat;
        HasCoreShield = true;
        TickIndex = 0;
        pendingSpells.Clear();
        commandHistory.Clear();
        SpawnCount = 0;
        Array.Clear(spawnedPerFaction, 0, spawnedPerFaction.Length);
        untilSpawn = 0.35f;
        untilCombatPlan = 0f;
        Mana = StartingMana;
    }

    public void RecoverMana(float deltaTime)
    {
        if (CurrentPhase == Phase.Combat)
            Mana = Mathf.Min(MaxMana, Mana + ManaPerSecond * deltaTime);
    }

    // 마법은 바로 적용하지 않고 다음 틱이 시작될 때 적용한다.
    public void QueueSpell(Spell spell, Vector3 center, int rangeStep) =>
        ScheduleSpell(new SpellCommand(TickIndex, spell, center, rangeStep));

    public void QueueTaunt(EnemyUnit target) =>
        ScheduleSpell(new SpellCommand(TickIndex, Spell.Taunt, target.Position, 0, target.SpawnOrder));

    // 리플레이용: 지정한 틱에 마법을 건다. 이미 지난 틱이면 다음 틱에 적용한다.
    public void ScheduleSpell(SpellCommand command)
    {
        if (CurrentPhase != Phase.Combat)
            return;
        int index = pendingSpells.Count;
        while (index > 0 && pendingSpells[index - 1].Tick > command.Tick)
            index--;
        pendingSpells.Insert(index, command);
    }

    private void ApplyPendingSpells()
    {
        while (pendingSpells.Count > 0 && pendingSpells[0].Tick <= TickIndex)
        {
            SpellCommand command = pendingSpells[0];
            pendingSpells.RemoveAt(0);
            string message = command.Spell == Spell.Taunt
                ? CastTaunt(command.TargetSpawnOrder, resolvedRecipients)
                : CastAreaSpell(command.Spell, command.Center, command.RangeStep, resolvedRecipients);
            commandHistory.Add(new SpellCommand(TickIndex, command.Spell, command.Center, command.RangeStep,
                command.TargetSpawnOrder));
            SpellResolved?.Invoke(command, message, resolvedRecipients);
        }
    }

    public static float AreaSpellRadius(int rangeStep) =>
        (DefaultSpellRadiusTenths + Mathf.Clamp(rangeStep, MinSpellRangeStep, MaxSpellRangeStep) * 2) / 10f;

    // 도발은 반경을 조절하지 않으므로 rangeStep 을 무시한다.
    public static int ManaCost(Spell spell, int rangeStep = 0) => spell switch
    {
        Spell.Taunt => 32,
        Spell.Heal => 32 + Mathf.Clamp(rangeStep, -MaxSpellManaAdjustment, MaxSpellManaAdjustment),
        _ => 24 + Mathf.Clamp(rangeStep, -MaxSpellManaAdjustment, MaxSpellManaAdjustment)
    };

    public static bool IsAreaSpell(Spell spell) => spell != Spell.Taunt;

    public int InitialSpellRangeStep(Spell spell) => IsAreaSpell(spell)
        ? Mathf.Clamp(Mathf.FloorToInt(Mana) - ManaCost(spell), -MaxSpellManaAdjustment, 0)
        : 0;

    public string SpellBlockReason(Spell spell, int rangeStep)
    {
        if (CurrentPhase != Phase.Combat)
            return GameText.Get("cast.combat_only");
        if (Mana < ManaCost(spell, rangeStep))
            return GameText.Get("cast.not_enough_mana");
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
        if (!IsAreaSpell(spell))
            return GameText.Get("cast.pick_target");
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
            return spell == Spell.Heal ? GameText.Get("cast.no_injured_units") : GameText.Get("cast.no_eligible_units");

        Mana -= ManaCost(spell, rangeStep);
        return spell == Spell.Heal ? GameText.Format("cast.healed", affected) : GameText.Format("cast.buffed", affected);
    }

    // 도발에 끌려올 유닛: 살아 있는 다른 진영 유닛 중 도발 유닛 둘레 반경 안. 벽은 막지 않는다.
    // 이미 교전 중인 유닛도 포함한다.
    public static bool IsTauntable(EnemyUnit taunter, EnemyUnit other) =>
        other != taunter && other.IsAlive && other.FactionIndex != taunter.FactionIndex &&
        IsInAreaSpellRange(other, taunter.Position, TauntRadius);

    // recipients 의 첫 항목은 도발 유닛, 나머지는 끌려온 유닛이다.
    public string CastTaunt(int targetSpawnOrder, List<EnemyUnit> recipients = null)
    {
        recipients?.Clear();
        string blocked = SpellBlockReason(Spell.Taunt, 0);
        if (blocked != null)
            return blocked;

        EnemyUnit taunter = null;
        foreach (EnemyUnit enemy in enemies)
            if (enemy.SpawnOrder == targetSpawnOrder && enemy.IsAlive)
                taunter = enemy;
        if (taunter == null)
            return GameText.Get("cast.target_gone");

        taunter.ApplyTaunt(TauntDuration);
        recipients?.Add(taunter);
        int pulled = 0;
        foreach (EnemyUnit enemy in enemies)
        {
            if (!IsTauntable(taunter, enemy))
                continue;
            enemy.ApplyTauntedBy(taunter, TauntDuration);
            recipients?.Add(enemy);
            pulled++;
        }

        Mana -= ManaCost(Spell.Taunt);
        // 끌려온 유닛이 다음 계획까지 기다리지 않고 바로 방향을 바꾼다.
        untilCombatPlan = 0f;
        return GameText.Format("cast.taunted", pulled);
    }

    /// <summary>두 진영의 선두가 처음 만날 것으로 보이는 지점.</summary>
    public readonly struct EncounterForecast
    {
        public EncounterForecast(Vector2Int cell, int sideA, int sideB, float seconds)
        {
            Cell = cell;
            SideA = sideA;
            SideB = sideB;
            Seconds = seconds;
        }

        public Vector2Int Cell { get; }
        public int SideA { get; }
        public int SideB { get; }
        public float Seconds { get; }
    }

    public IReadOnlyCollection<Vector2Int> ForecastEncounterCells()
    {
        var cells = new HashSet<Vector2Int>();
        foreach (EncounterForecast forecast in ForecastEncounters())
            cells.Add(forecast.Cell);
        return cells;
    }

    public IReadOnlyList<EncounterForecast> ForecastEncounters()
    {
        // 임시 가정: 모든 스폰의 선두가 동시에 출발해 각자의 이동속도로 경로를 따라 걷는다.
        // 서로 다른 진영의 스폰 쌍마다 교전 칸 하나.
        var forecasts = new List<EncounterForecast>();
        for (int a = 0; a < factions.Length; a++)
        for (int b = a + 1; b < factions.Length; b++)
        {
            if (roundSpawnCounts[a] == 0 || roundSpawnCounts[b] == 0)
                continue;
            EnemyFaction leaderA = RoundUnit(a, 0);
            EnemyFaction leaderB = RoundUnit(b, 0);
            float range = Mathf.Max(leaderA.AttackRange, leaderB.AttackRange);
            foreach (int first in RoundSpawns(a))
                foreach (int second in RoundSpawns(b))
                    if (ForecastEncounter(grid.Routes[first], grid.Routes[second], leaderA.MoveSpeed,
                            leaderB.MoveSpeed, range) is { } hit)
                        forecasts.Add(new EncounterForecast(hit.Cell, a, b, hit.Seconds));
        }
        return forecasts;
    }

    private (Vector2Int Cell, float Seconds)? ForecastEncounter(Vector3[] firstRoute, Vector3[] secondRoute,
        float firstSpeed, float secondSpeed, float range)
    {
        // Walk both leaders along their routes in small steps. A leader that reaches the core is gone.
        const float step = 0.1f;
        float firstLength = RouteLength(firstRoute);
        float secondLength = RouteLength(secondRoute);
        for (float seconds = 0f; ; seconds += step)
        {
            float firstDistance = firstSpeed * seconds;
            float secondDistance = secondSpeed * seconds;
            if (firstDistance >= firstLength || secondDistance >= secondLength)
                return null;

            Vector3 first = PointAlong(firstRoute, firstDistance);
            Vector3 second = PointAlong(secondRoute, secondDistance);
            if ((first - second).sqrMagnitude > range * range || !grid.HasLineOfSight(first, second))
                continue;

            Vector2Int center = grid.WorldToCell((first + second) * 0.5f);
            return (grid.IsWalkable(center) ? center : grid.WorldToCell(first), seconds);
        }
    }

    private static float RouteLength(Vector3[] route)
    {
        float length = 0f;
        for (int i = 1; i < route.Length; i++)
            length += Vector3.Distance(route[i - 1], route[i]);
        return length;
    }

    private static Vector3 PointAlong(Vector3[] route, float distance)
    {
        for (int i = 1; i < route.Length; i++)
        {
            float segment = Vector3.Distance(route[i - 1], route[i]);
            if (distance <= segment)
                return Vector3.Lerp(route[i - 1], route[i], segment > 0f ? distance / segment : 0f);
            distance -= segment;
        }
        return route[^1];
    }

    // 게임에서는 항상 TickDuration 으로 부른다. 다른 값은 시험용이다.
    public void Tick(float deltaTime, bool pauseMovement)
    {
        if (CurrentPhase != Phase.Combat)
            return;

        foreach (EnemyUnit enemy in enemies)
            enemy.BeginTick();
        ApplyPendingSpells();
        RecoverMana(deltaTime);
        TickIndex++;

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

        if (!pauseMovement)
            foreach (EnemyUnit enemy in enemies)
                combat.TrackApproachProgress(enemy, deltaTime);

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
                enemy.Face(grid.HasLineOfSight(enemy.Position, target.Position) ? target.Position : enemy.LastKnownTargetPosition);
                if (enemy.CanAttack && combat.CanAttack(enemy, target))
                {
                    target.QueueDamage(enemy.Attack(target), enemy);
                }
            }
        }

        bool replanBeforeMoving = false;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyUnit enemy = enemies[i];
            enemy.ResolveDamage();
            if (!enemy.IsAlive)
            {
                RemoveEnemyAt(i);
                replanBeforeMoving = true;
            }
            else
                replanBeforeMoving |= enemy.HasRetaliationRequest;
        }

        // All hits resolve before replanning, preserving simultaneous lethal attacks.
        // React before route movement so a hit unit cannot walk into the core first, and
        // a unit whose opponent just died does not take one step toward its route and back.
        if (replanBeforeMoving)
        {
            combat.Plan();
            untilCombatPlan = CombatPlanInterval;
        }

        if (!pauseMovement)
        {
            foreach (EnemyUnit enemy in enemies)
            {
                if ((enemy.Target ?? enemy.CombatQueue?.Target) is { IsAlive: true } target)
                {
                    if (enemy.Target == null)
                        enemy.Face(grid.HasLineOfSight(enemy.Position, target.Position) ? target.Position : enemy.LastKnownTargetPosition);
                    combat.Move(enemy, target, deltaTime);
                    enemy.WasEngaged = true;
                    continue;
                }

                enemy.CombatQueue = null;
                if (enemy.WasEngaged)
                {
                    enemy.WasEngaged = false;
                    RerouteFromHere(enemy);
                }
                AdvanceUnit(enemy, deltaTime);
            }

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyUnit enemy = enemies[i];
                if (!enemy.ReachedCore)
                    continue;

                if (HasCoreShield)
                    HasCoreShield = false;
                else
                {
                    CoreHealth = Mathf.Max(0, CoreHealth - 1);
                    CoreHit?.Invoke();
                }
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
            Mana = 0f;
            if (Round == roundLimit)
            {
                CurrentPhase = Phase.Victory;
                return;
            }
            CurrentPhase = Phase.Preparation;
            Round++;
            PrepareRoundSpawns();
        }
    }

    // 이번 라운드에 진영(편) side 에서 index 번째로 나오는 유닛 종류.
    public EnemyFaction RoundUnit(int side, int index) =>
        roundQueues != null ? roundQueues[side][index] : factions[side];

    public int RoundSpawnCount(int side) => roundSpawnCounts[side];

    public int FactionCount => factions.Length;

    // 살아 있는 유닛이 한 진영뿐이고 다른 진영은 이번 라운드에 더 나올 유닛도 없다. 더는 교전이 없으므로
    // 화면에서 빨리 감는 데 쓴다. 전투 진행에는 쓰지 않는 조회다.
    public bool OnlyOneSideRemains
    {
        get
        {
            if (CurrentPhase != Phase.Combat || enemies.Count == 0)
                return false;
            int side = enemies[0].FactionIndex;
            foreach (EnemyUnit enemy in enemies)
                if (enemy.FactionIndex != side)
                    return false;
            for (int other = 0; other < factions.Length; other++)
                if (other != side && spawnedPerFaction[other] < roundSpawnCounts[other])
                    return false;
            return true;
        }
    }

    private void SpawnNext()
    {
        for (int factionIndex = 0; factionIndex < factions.Length; factionIndex++)
        {
            if (spawnedPerFaction[factionIndex] >= roundSpawnCounts[factionIndex])
                continue;

            // Earlier spawns in this batch already occupy space, including across factions.
            if (!SpawnPointClear(factionIndex))
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
        if (roundRoute[factionIndex] >= 0)
            return grid.Routes[roundRoute[factionIndex]];
        int[] spawns = factionSpawns[factionIndex];
        return grid.Routes[spawns[spawnedPerFaction[factionIndex] % spawns.Length]];
    }

    // 이번 라운드에 이 진영이 쓰는 스폰(경로 인덱스).
    private int[] RoundSpawns(int factionIndex) =>
        roundRoute[factionIndex] >= 0 ? new[] { roundRoute[factionIndex] } : factionSpawns[factionIndex];

    // 이번 라운드에 쓰는 스폰의 경로. 인덱스는 grid.Routes 와 같고, 쓰지 않는 스폰은 null 이다.
    public Vector3[][] RoundRoutes()
    {
        var routes = new Vector3[grid.Routes.Length][];
        for (int f = 0; f < factions.Length; f++)
            foreach (int route in RoundSpawns(f))
                routes[route] = grid.Routes[route];
        return routes;
    }

    private bool SpawnPointsClear()
    {
        for (int side = 0; side < factions.Length; side++)
            if (spawnedPerFaction[side] < roundSpawnCounts[side] && !SpawnPointClear(side))
                return false;
        return true;
    }

    private bool SpawnPointClear(int side)
    {
        Vector3 spawn = NextRoute(side)[0];
        EnemyFaction incoming = RoundUnit(side, spawnedPerFaction[side]);
        foreach (EnemyUnit enemy in enemies)
        {
            float spacing = CombatPositioning.RequiredSpacing(incoming, enemy.Faction);
            if ((enemy.Position - spawn).sqrMagnitude < spacing * spacing)
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

        // Finish a return path already under way. Choosing between it and a direct line to
        // the route on every tick zigzags along the edge of where that line is clear.
        bool offRoute = !enemy.Position.Equals(enemy.RoutePosition);
        if (offRoute && combat.IsReturningAlongPath(enemy) && combat.TryReturnAlongPath(enemy, deltaTime, continueOnly: true))
            return;
        int firstForward = -1;
        if (offRoute && TryRejoinRoute(enemy, deltaTime, out firstForward))
            return;
        // 보이는 경로 점이 하나도 없으면(벽 뒤) 경로 탐색으로 돌아간다. 보이는데 다른 유닛에 가려 못 가는
        // 것이면 일단 기다린다: 여기서 바로 다른 점이나 우회로를 고르면 틱마다 방향이 바뀐다.
        if (offRoute && firstForward < 0 && combat.TryReturnAlongPath(enemy, deltaTime))
            return;

        // Off-route units wait for their forward connection, rather than walking back
        // to the spot they just yielded from and immediately starting the same detour.
        if (enemy.Position.Equals(enemy.RoutePosition))
        {
            Vector3 next = enemy.NextRoutePosition(deltaTime);
            if (combat.CanFollowRoute(enemy, next))
            {
                enemy.Face(next);
                enemy.FollowRoute(next);
                enemy.BlockedRouteTime = 0f;
                return;
            }
        }

        enemy.BlockedRouteTime += deltaTime;
        if (enemy.BlockedRouteTime < RouteYieldDelay)
            return;
        enemy.BlockedRouteTime = 0f;
        // RouteYieldDelay 동안 막혀 있었다. 향하던 점을 고집하지 않고 앞쪽 경로 점 아무 데나 곧장 갈 수
        // 있으면 그리로 가고, 없으면 그 점들로 막은 유닛을 짧게 돌아가는 길을 찾는다. 지나온 쪽 점은
        // 여전히 고르지 않는다. 기다리기만 하면 앞의 아군이 비킬 때까지 몇 초씩 서 있게 된다.
        if (TryRejoinRoute(enemy, deltaTime, out firstForward, anyForwardPoint: true))
            return;
        if (offRoute && firstForward >= 0 && combat.TryReturnAlongPath(enemy, deltaTime, firstForward, shortOnly: true))
            return;
        if (combat.TryFindRouteYield(enemy, RouteRejoinWaypoints, out Vector3 destination, out int offset))
        {
            enemy.RouteYieldDestination = destination;
            enemy.RouteBypassOffset = offset;
        }
        else if (offRoute && combat.TryStepAside(enemy, out destination))
            enemy.RouteYieldDestination = destination;
    }

    // 교전이 끝나 경로를 벗어난 유닛은 원래 경로로 돌아가는 길이 지금 위치에서 코어로 곧장 가는
    // 최단 경로보다 RerouteMinimumSaving 이상 길면 새 경로를 잡는다. 모두가 새 경로를 잡으면
    // 같은 진영이 여러 갈래로 갈라져 합류 지점에서 서로 막으므로, 크게 돌아갈 때만 바꾼다.
    private void RerouteFromHere(EnemyUnit enemy)
    {
        if (enemy.Position.Equals(enemy.RoutePosition) || grid.RouteFrom(enemy.Position) is not { } route)
            return;

        float direct = 0f;
        for (int i = 1; i < route.Length; i++)
            direct += Vector3.Distance(route[i - 1], route[i]);

        // 보이는 원래 경로 점 중 가장 짧게 이어지는 곳. 하나도 안 보이면 우회 경로로 돌아가야 한다.
        float rejoin = float.PositiveInfinity;
        if (grid.CanPursue(enemy.Position, enemy.RoutePosition))
            rejoin = Vector3.Distance(enemy.Position, enemy.RoutePosition) +
                Vector3.Distance(enemy.RoutePosition, enemy.GetRouteWaypoint(0)) + enemy.RouteLengthFrom(0);
        for (int offset = 0; offset < enemy.RemainingRouteWaypoints - 1; offset++)
        {
            Vector3 waypoint = enemy.GetRouteWaypoint(offset);
            if (grid.CanPursue(enemy.Position, waypoint))
                rejoin = Mathf.Min(rejoin, Vector3.Distance(enemy.Position, waypoint) + enemy.RouteLengthFrom(offset));
        }

        if (rejoin - direct >= RerouteMinimumSaving * grid.CellSize)
            enemy.Reroute(route);
    }

    // firstForward: 벽에 가리지 않고, 코어까지의 길이 가장 짧은 점보다 RejoinSlack 칸 넘게 손해가 아닌 경로 점 중
    // 가장 앞 번호(유닛에 막혔는지와 무관). 보이는 경로 점이 없으면 -1.
    // anyForwardPoint: 향하던 점(RouteBypassOffset)보다 앞 번호만 보는 제한을 풀고, 길이 조건만으로 고른다.
    private bool TryRejoinRoute(EnemyUnit enemy, float deltaTime, out int firstForward, bool anyForwardPoint = false)
    {
        firstForward = -1;
        // The final point is on the core boundary, not a navigation waypoint. Return
        // to its approach tile; the normal route step then handles core contact.
        if (enemy.RemainingRouteWaypoints == 1)
        {
            Vector3 approach = enemy.RoutePosition;
            if (!grid.CanPursue(enemy.Position, approach))
                return false;
            firstForward = 0;
            if (!combat.CanFollowRoute(enemy, approach))
                return false;
            Vector3 step = enemy.StepToward(approach, deltaTime);
            enemy.Face(step);
            enemy.FollowRoute(step);
            enemy.RouteBypassOffset = -1;
            enemy.BlockedRouteTime = 0f;
            return true;
        }

        // 보이는 경로 점 가운데 코어까지 가장 짧게 이어지는 길. 그 점이 유닛에 막혀 있어도, 그보다
        // RejoinSlack 칸 넘게 손해인 점(지나온 쪽)으로 되돌아가지는 않는다.
        int first = anyForwardPoint ? 0 : Mathf.Max(0, enemy.RouteBypassOffset);
        float shortest = float.PositiveInfinity;
        for (int offset = enemy.RemainingRouteWaypoints - 2; offset >= first; offset--)
        {
            Vector3 waypoint = enemy.GetRouteWaypoint(offset);
            if (grid.CanPursue(enemy.Position, waypoint))
                shortest = Mathf.Min(shortest, Vector3.Distance(enemy.Position, waypoint) + enemy.RouteLengthFrom(offset));
        }
        for (int offset = first; offset <= enemy.RemainingRouteWaypoints - 2 && firstForward < 0; offset++)
        {
            Vector3 waypoint = enemy.GetRouteWaypoint(offset);
            if (grid.CanPursue(enemy.Position, waypoint) &&
                Vector3.Distance(enemy.Position, waypoint) + enemy.RouteLengthFrom(offset) <= shortest + RejoinSlack * grid.CellSize)
                firstForward = offset;
        }

        // A fighter may have moved well beyond its old route index. Rejoin the furthest
        // visible, unobstructed point ahead instead of walking backwards through the army.
        for (int offset = enemy.RemainingRouteWaypoints - 2; offset >= first; offset--)
        {
            Vector3 waypoint = enemy.GetRouteWaypoint(offset);
            if (!grid.CanPursue(enemy.Position, waypoint) ||
                Vector3.Distance(enemy.Position, waypoint) + enemy.RouteLengthFrom(offset) > shortest + RejoinSlack * grid.CellSize ||
                !combat.CanFollowRoute(enemy, waypoint))
                continue;

            Vector3 next = enemy.StepToward(waypoint, deltaTime);
            enemy.Face(next);
            if (next.Equals(waypoint))
            {
                enemy.FollowRoute(next, offset);
                enemy.RouteBypassOffset = -1;
            }
            else
            {
                enemy.MoveOffRoute(next);
                enemy.RouteBypassOffset = offset;
            }
            enemy.BlockedRouteTime = 0f;
            return true;
        }
        return false;
    }
}
