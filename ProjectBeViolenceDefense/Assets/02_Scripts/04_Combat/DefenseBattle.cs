using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseBattle
{
    public const int CoreMaxHealth = 120;
    // 전투 로직의 고정 틱. 공격 간격·지속시간이 이 단위로 딱 떨어지도록 60을 쓴다.
    public const int TicksPerSecond = 60;
    public const float TickDuration = 1f / TicksPerSecond;
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
    private const float CombatPlanInterval = 0.12f;
    private const float RouteYieldDelay = 0.35f;
    private const int RouteRejoinWaypoints = 3;
    private const int ReinforcementsPerFaction = 1;
    private const int ExtraReinforcements = 3;

    public enum Phase { Preparation, Combat, Defeat, Victory }
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
        public SpellCommand(int tick, Spell spell, Vector3 center, int rangeStep)
        {
            Tick = tick;
            Spell = spell;
            Center = center;
            RangeStep = rangeStep;
        }

        public int Tick { get; }
        public Spell Spell { get; }
        public Vector3 Center { get; }
        public int RangeStep { get; }
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
    public int CoreHealth { get; private set; } = CoreMaxHealth;
    public int SpawnCount { get; private set; }
    public int EnemiesPerRound { get; private set; }
    public int AttackBuffsLeft { get; private set; }
    public int MoveSpeedBuffsLeft { get; private set; }
    public int ArmorBuffsLeft { get; private set; }
    public int HealsLeft { get; private set; }
    public float Mana { get; private set; }

    public DefenseBattle(DefenseGrid grid, EnemyFaction[] factions,
        Func<int, IReadOnlyList<EnemyFaction>[]> roundLineup = null, int roundLimit = 0)
    {
        this.grid = grid;
        this.factions = factions;
        this.roundLineup = roundLineup;
        this.roundLimit = roundLimit;
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
            int baseCount = factionSpawns[i].Length > 0 ? factions[i].SpawnCount : 0;
            // Faction 1 (Undead) receives the first larger reinforcement, then it alternates with the others.
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
        TickIndex = 0;
        pendingSpells.Clear();
        commandHistory.Clear();
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

    // 마법은 바로 적용하지 않고 다음 틱이 시작될 때 적용한다.
    public void QueueSpell(Spell spell, Vector3 center, int rangeStep) =>
        ScheduleSpell(new SpellCommand(TickIndex, spell, center, rangeStep));

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
            string message = CastAreaSpell(command.Spell, command.Center, command.RangeStep, resolvedRecipients);
            commandHistory.Add(new SpellCommand(TickIndex, command.Spell, command.Center, command.RangeStep));
            SpellResolved?.Invoke(command, message, resolvedRecipients);
        }
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
        // 임시 가정: 모든 스폰의 선두가 동시에 출발한다. 서로 다른 진영의 스폰 쌍마다 교전 칸 하나.
        var cells = new HashSet<Vector2Int>();
        for (int a = 0; a < factions.Length; a++)
        for (int b = a + 1; b < factions.Length; b++)
        {
            if (roundSpawnCounts[a] == 0 || roundSpawnCounts[b] == 0)
                continue;
            float range = Mathf.Max(RoundUnit(a, 0).AttackRange, RoundUnit(b, 0).AttackRange);
            foreach (int first in factionSpawns[a])
                foreach (int second in factionSpawns[b])
                    if (ForecastEncounter(grid.Routes[first], grid.Routes[second], range) is { } cell)
                        cells.Add(cell);
        }
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
                CoreHit?.Invoke();
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
        int[] spawns = factionSpawns[factionIndex];
        return grid.Routes[spawns[spawnedPerFaction[factionIndex] % spawns.Length]];
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
        if (offRoute && combat.IsReturningAlongPath(enemy) && combat.TryReturnAlongPath(enemy, deltaTime))
            return;
        if (offRoute && TryRejoinRoute(enemy, deltaTime))
            return;
        if (offRoute && combat.TryReturnAlongPath(enemy, deltaTime))
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
        if (TryRejoinRoute(enemy, deltaTime))
            return;
        if (combat.TryFindRouteYield(enemy, RouteRejoinWaypoints, out Vector3 destination, out int offset))
        {
            enemy.RouteYieldDestination = destination;
            enemy.RouteBypassOffset = offset;
        }
        else if (offRoute && combat.TryStepAside(enemy, out destination))
            enemy.RouteYieldDestination = destination;
    }

    private bool TryRejoinRoute(EnemyUnit enemy, float deltaTime)
    {
        // The final point is on the core boundary, not a navigation waypoint. Return
        // to its approach tile; the normal route step then handles core contact.
        if (enemy.RemainingRouteWaypoints == 1)
        {
            Vector3 approach = enemy.RoutePosition;
            if (!grid.CanPursue(enemy.Position, approach) || !combat.CanFollowRoute(enemy, approach))
                return false;
            Vector3 step = enemy.StepToward(approach, deltaTime);
            enemy.Face(step);
            enemy.FollowRoute(step);
            enemy.RouteBypassOffset = -1;
            enemy.BlockedRouteTime = 0f;
            return true;
        }

        // A fighter may have moved well beyond its old route index. Rejoin the furthest
        // visible, unobstructed point ahead instead of walking backwards through the army.
        for (int offset = enemy.RemainingRouteWaypoints - 2; offset >= Mathf.Max(0, enemy.RouteBypassOffset); offset--)
        {
            Vector3 waypoint = enemy.GetRouteWaypoint(offset);
            if (!grid.CanPursue(enemy.Position, waypoint) || !combat.CanFollowRoute(enemy, waypoint))
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
