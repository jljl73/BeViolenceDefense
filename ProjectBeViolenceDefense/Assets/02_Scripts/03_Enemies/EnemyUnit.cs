using System;
using System.Collections.Generic;
using UnityEngine;

// 전투 로직이 쓰는 유닛 종류 데이터. 화면 데이터(모델·애니메이터·머티리얼)는 넣지 않는다.
// 화면은 UnitId 로 EnemyCatalog.TryGetVisual 을 찾아 쓴다.
internal sealed class EnemyFaction
{
    public string DisplayName { get; }
    public Color Color { get; }
    public float AttackRange { get; }
    public float AwarenessRange { get; }
    public float BodyRadius { get; }
    public int MaxHealth { get; }
    public int Damage { get; }
    public float AttackInterval { get; }
    public float DamagePerSecond => Damage / AttackInterval;
    public int SpawnCount { get; }
    public float MoveSpeed { get; }
    // 공격 1회마다 받는 피해를 고정으로 줄인다.
    public int Armor { get; }
    public string UnitId { get; }

    public EnemyFaction(string displayName, Color color, float attackRange = 1.5f, int maxHealth = 50,
        int damage = 10, float attackInterval = 0.65f, int spawnCount = 9, float awarenessRange = 3f,
        float moveSpeed = 1.7f, int armor = 0, string unitId = null, float bodyRadius = 0.3f)
    {
        DisplayName = displayName;
        Color = color;
        AttackRange = attackRange;
        AwarenessRange = Mathf.Max(attackRange, awarenessRange);
        MaxHealth = maxHealth;
        Damage = damage;
        AttackInterval = attackInterval;
        SpawnCount = spawnCount;
        MoveSpeed = moveSpeed;
        Armor = armor;
        UnitId = unitId;
        BodyRadius = Mathf.Max(0.05f, bodyRadius);
    }
}

internal sealed class EnemyUnit
{
    public const int AttackBonus = 15;
    public const float MoveSpeedBonusPercent = 0.5f;
    public const int ShieldDamageReductionPercent = 80;
    // 도발로 거대해진 유닛이 얻는 추가 최대 체력(기본 최대 체력 대비). 첫 테스트용 임시 값.
    public const float TauntHealthBonusPercent = 0.5f;
    internal const float RetaliationDuration = 4f;

    private readonly Vector3[] path;
    private readonly List<float> attackBuffTimers = new();
    private readonly List<float> moveSpeedBuffTimers = new();
    private int waypoint = 1;
    private int pendingDamage;
    private EnemyUnit pendingAttacker;
    private bool retaliationRequested;
    private float cooldown;
    private float attackFlash;
    private int tauntBonusHealth;

    public EnemyFaction Faction { get; }
    public int SpawnOrder { get; }
    public int FactionIndex { get; }
    public int Health { get; private set; }
    public int MaxHealth => Faction.MaxHealth + tauntBonusHealth;
    // 도발 중(거대화)인 남은 시간. 0 이면 도발하지 않는다.
    public float TauntRemaining { get; private set; }
    public bool IsTaunting => TauntRemaining > 0f;
    // 이 유닛을 끌어당긴 도발 유닛. 남은 시간이 끝나거나 도발이 풀리면 null 로 본다.
    public EnemyUnit TauntedBy { get; private set; }
    public float TauntedRemaining { get; private set; }
    public EnemyUnit ActiveTaunter =>
        TauntedRemaining > 0f && TauntedBy is { IsAlive: true, IsTaunting: true } taunter ? taunter : null;
    public Vector3 Position { get; private set; }
    // 직전 틱 시작 때의 위치. 화면이 틱 사이를 보간할 때만 쓴다.
    public Vector3 PreviousPosition { get; private set; }
    public Vector3 RoutePosition { get; private set; }
    public Vector3 RouteWaypoint => path[waypoint];
    public int RemainingRouteWaypoints => path.Length - waypoint;
    public Vector3 Heading { get; private set; }
    public int AttackBuffStacks => attackBuffTimers.Count;
    public int MoveSpeedBuffStacks => moveSpeedBuffTimers.Count;
    // 기존 화면 표식에는 방어막의 활성 여부를 0/1로 전달한다. 효과는 중첩하지 않는다.
    public int ArmorBuffStacks => ArmorBuffRemaining > 0f ? 1 : 0;
    public float AttackBuffRemaining => NextBuffExpiry(attackBuffTimers);
    public float MoveSpeedBuffRemaining => NextBuffExpiry(moveSpeedBuffTimers);
    public float ArmorBuffRemaining { get; private set; }
    public float HealFlash { get; private set; }
    public float AttackFlash => attackFlash;
    public int AttacksPerformed { get; private set; }
    public Vector3 AttackHeading { get; private set; }
    public (EnemyUnit Target, bool Boosted, bool TargetArmored) LastAttack { get; private set; }
    public bool IsAlive => Health > 0;
    public bool CanAttack => cooldown <= 0;
    public EnemyUnit Target { get; set; }
    public EnemyUnit PursuitTarget { get; set; }
    public Vector3 LastKnownTargetPosition { get; set; }
    public float LostTargetSearchTime { get; set; }
    public List<Vector3> CombatPath { get; } = new();
    public int CombatPathIndex { get; set; }
    public EnemyUnit CombatPathTarget { get; set; }
    public Vector3 CombatPathAnchor { get; set; }
    public bool CombatPathSearch { get; set; }
    public int ReturnRouteOffset { get; set; } = -1;
    public float PathRetryRemaining { get; set; }
    public EnemyUnit UnreachableTarget { get; set; }
    public Vector3 UnreachableTargetPosition { get; set; }
    public Vector3 UnreachableFromPosition { get; set; }
    public int UnreachableNavigationVersion { get; set; }
    public EnemyUnit RetaliationTarget { get; private set; }
    public float RetaliationRemaining { get; private set; }
    public bool HasRetaliationRequest => retaliationRequested;
    public (EnemyUnit Target, int Order)? CombatQueue { get; set; }
    public int AttackSlot { get; set; } = -1;
    public Vector3? CombatDestination { get; set; }
    public Vector3? CombatSteerWaypoint { get; set; }
    // 직전 옆걸음 방향. 남은 시간 동안은 그 반대쪽으로 되돌아가는 옆걸음을 고르지 않는다.
    public Vector3 SidestepDirection { get; set; }
    public float SidestepMemoryRemaining { get; set; }
    // 스스로 타깃을 바꾼 뒤 다시 바꾸지 않고 유지하는 남은 시간.
    public float RetargetLockRemaining { get; set; }
    public EnemyUnit CombatProgressTarget { get; set; }
    public float CombatBestRemainingDistance { get; set; } = float.PositiveInfinity;
    public float CombatApproachTailLength { get; set; }
    public float CombatStalledTime { get; set; }
    public Vector3? RouteYieldDestination { get; set; }
    public int RouteBypassOffset { get; set; } = -1;
    public float BlockedRouteTime { get; set; }
    public bool ReachedCore => Position.Equals(path[^1]);

    public EnemyUnit(EnemyFaction faction, int factionIndex, Vector3[] path, int spawnOrder)
    {
        Faction = faction;
        FactionIndex = factionIndex;
        SpawnOrder = spawnOrder;
        Health = faction.MaxHealth;
        this.path = path;
        Position = RoutePosition = PreviousPosition = path[0];
        Heading = path[1] - Position;
    }

    public void TickTimers(float deltaTime)
    {
        cooldown = Mathf.Max(0, cooldown - deltaTime);
        attackFlash = Mathf.Max(0, attackFlash - deltaTime);
        HealFlash = Mathf.Max(0, HealFlash - deltaTime);
        PathRetryRemaining = Mathf.Max(0, PathRetryRemaining - deltaTime);
        RetargetLockRemaining = Mathf.Max(0, RetargetLockRemaining - deltaTime);
        SidestepMemoryRemaining = Mathf.Max(0, SidestepMemoryRemaining - deltaTime);
        RetaliationRemaining = Mathf.Max(0, RetaliationRemaining - deltaTime);
        if (RetaliationRemaining <= 0f || RetaliationTarget is not { IsAlive: true })
        {
            RetaliationTarget = null;
            retaliationRequested = false;
        }
        TickBuffs(attackBuffTimers, deltaTime);
        TickBuffs(moveSpeedBuffTimers, deltaTime);
        ArmorBuffRemaining = Mathf.Max(0f, ArmorBuffRemaining - deltaTime);
        if (TauntRemaining > 0f)
        {
            TauntRemaining = Mathf.Max(0f, TauntRemaining - deltaTime);
            if (TauntRemaining <= 0f)
                EndTaunt();
        }
        TauntedRemaining = Mathf.Max(0f, TauntedRemaining - deltaTime);
        if (ActiveTaunter == null)
        {
            TauntedBy = null;
            TauntedRemaining = 0f;
        }
    }

    // 다시 걸면 시간만 새로 채운다. 추가 체력은 겹치지 않는다.
    public void ApplyTaunt(float duration)
    {
        TauntRemaining = duration;
        if (tauntBonusHealth > 0)
            return;
        tauntBonusHealth = Mathf.CeilToInt(Faction.MaxHealth * TauntHealthBonusPercent);
        Health += tauntBonusHealth;
    }

    // 끝나면 늘어난 만큼 체력을 되돌린다. 이것만으로는 죽지 않는다.
    private void EndTaunt()
    {
        Health = Mathf.Max(1, Health - tauntBonusHealth);
        tauntBonusHealth = 0;
    }

    public void ApplyTauntedBy(EnemyUnit taunter, float duration)
    {
        TauntedBy = taunter;
        TauntedRemaining = duration;
    }

    private static void TickBuffs(List<float> timers, float deltaTime)
    {
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            float remaining = timers[i] - deltaTime;
            if (remaining <= 0f)
                timers.RemoveAt(i);
            else
                timers[i] = remaining;
        }
    }

    private static float NextBuffExpiry(List<float> timers)
    {
        if (timers.Count == 0)
            return 0f;

        float remaining = timers[0];
        for (int i = 1; i < timers.Count; i++)
            remaining = Mathf.Min(remaining, timers[i]);
        return remaining;
    }

    public void Face(Vector3 point)
    {
        Vector3 heading = point - Position;
        if (heading.sqrMagnitude > 0.0001f)
            Heading = heading;
    }

    public int Attack(EnemyUnit target)
    {
        cooldown = Faction.AttackInterval;
        attackFlash = 0.14f;
        AttacksPerformed++;
        AttackHeading = Heading;
        LastAttack = (target, AttackBuffStacks > 0, target.ArmorBuffStacks > 0);
        return (int)Math.Min(int.MaxValue, Faction.Damage + (long)AttackBonus * AttackBuffStacks);
    }

    public void QueueDamage(int damage, EnemyUnit attacker = null)
    {
        long applied = Math.Max(0, (long)damage - Faction.Armor);
        // 기본 방어력을 먼저 적용하고 남은 피해를 줄인다. 정수 올림으로 작은 공격도
        // 양수 피해를 유지하며, 중간 곱셈은 long으로 계산해 오버플로를 막는다.
        if (ArmorBuffRemaining > 0f)
            applied = (applied * (100 - ShieldDamageReductionPercent) + 99) / 100;
        pendingDamage = (int)Math.Min(int.MaxValue, pendingDamage + applied);
        // Even armor absorbing a hit must reveal its source. Select simultaneous
        // attackers independently of iteration order, retaining an existing aggressor.
        if (damage <= 0 || attacker is not { IsAlive: true } || attacker.FactionIndex == FactionIndex)
            return;
        if (pendingAttacker == RetaliationTarget && pendingAttacker != null)
            return;
        float distance = (attacker.Position - Position).sqrMagnitude;
        float pendingDistance = pendingAttacker == null ? float.PositiveInfinity :
            (pendingAttacker.Position - Position).sqrMagnitude;
        if (attacker == RetaliationTarget || distance < pendingDistance ||
            (distance == pendingDistance && attacker.SpawnOrder < pendingAttacker.SpawnOrder))
            pendingAttacker = attacker;
    }

    public void ResolveDamage()
    {
        Health = Mathf.Max(0, Health - pendingDamage);
        pendingDamage = 0;
        if (IsAlive && pendingAttacker != null &&
            (RetaliationTarget == null || RetaliationRemaining <= 0f || pendingAttacker == RetaliationTarget))
        {
            // Request the switch once; repeated hits renew recognition without
            // undoing a stalled unit's decision to approach a reachable neighbor.
            retaliationRequested |= RetaliationTarget != pendingAttacker;
            RetaliationTarget = pendingAttacker;
            RetaliationRemaining = RetaliationDuration;
        }
        pendingAttacker = null;
    }

    public EnemyUnit ConsumeRetaliationRequest()
    {
        EnemyUnit attacker = retaliationRequested ? RetaliationTarget : null;
        retaliationRequested = false;
        return attacker;
    }

    public void Die() => Health = 0;

    public void ApplyAttackBuff(float duration) => attackBuffTimers.Add(duration);

    public void ApplyMoveSpeedBuff(float duration) => moveSpeedBuffTimers.Add(duration);

    public void ApplyArmorBuff(float duration) => ArmorBuffRemaining = Mathf.Max(ArmorBuffRemaining, duration);

    public void Heal(int amount)
    {
        Health = Mathf.Min(MaxHealth, Health + amount);
        HealFlash = 0.55f;
    }

    public Vector3 NextRoutePosition(float deltaTime) =>
        StepToward(Position.Equals(RoutePosition) ? path[waypoint] : RoutePosition, deltaTime);

    public Vector3 StepToward(Vector3 destination, float deltaTime)
    {
        float speed = Faction.MoveSpeed * (1f + MoveSpeedBonusPercent * MoveSpeedBuffStacks);
        return Vector3.MoveTowards(Position, destination, speed * deltaTime);
    }

    public void BeginTick() => PreviousPosition = Position;

    public void MoveOffRoute(Vector3 next) => Position = next;

    public Vector3 GetRouteWaypoint(int offset) => path[waypoint + offset];

    public void FollowRoute(Vector3 next, int rejoinOffset = 0)
    {
        bool reachedWaypoint = next.Equals(GetRouteWaypoint(rejoinOffset));
        if (Position.Equals(RoutePosition) || reachedWaypoint)
            RoutePosition = next;
        Position = next;
        if (reachedWaypoint)
            waypoint = Mathf.Min(waypoint + rejoinOffset + 1, path.Length - 1);
    }
}
