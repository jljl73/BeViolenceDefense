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
        int damage = 10, float attackInterval = 0.65f, int spawnCount = 9, float awarenessRange = 2.2f,
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
    public const int ArmorBonus = 5;

    private readonly Vector3[] path;
    private readonly List<float> attackBuffTimers = new();
    private readonly List<float> moveSpeedBuffTimers = new();
    private readonly List<float> armorBuffTimers = new();
    private int waypoint = 1;
    private int pendingDamage;
    private float cooldown;
    private float attackFlash;

    public EnemyFaction Faction { get; }
    public int SpawnOrder { get; }
    public int FactionIndex { get; }
    public int Health { get; private set; }
    public int MaxHealth => Faction.MaxHealth;
    public Vector3 Position { get; private set; }
    // 직전 틱 시작 때의 위치. 화면이 틱 사이를 보간할 때만 쓴다.
    public Vector3 PreviousPosition { get; private set; }
    public Vector3 RoutePosition { get; private set; }
    public Vector3 RouteWaypoint => path[waypoint];
    public int RemainingRouteWaypoints => path.Length - waypoint;
    public Vector3 Heading { get; private set; }
    public int AttackBuffStacks => attackBuffTimers.Count;
    public int MoveSpeedBuffStacks => moveSpeedBuffTimers.Count;
    public int ArmorBuffStacks => armorBuffTimers.Count;
    public float AttackBuffRemaining => NextBuffExpiry(attackBuffTimers);
    public float MoveSpeedBuffRemaining => NextBuffExpiry(moveSpeedBuffTimers);
    public float ArmorBuffRemaining => NextBuffExpiry(armorBuffTimers);
    public float HealFlash { get; private set; }
    public float AttackFlash => attackFlash;
    public int AttacksPerformed { get; private set; }
    public Vector3 AttackHeading { get; private set; }
    public (EnemyUnit Target, bool Boosted, bool TargetArmored) LastAttack { get; private set; }
    public bool IsAlive => Health > 0;
    public bool CanAttack => cooldown <= 0;
    public EnemyUnit Target { get; set; }
    public (EnemyUnit Target, int Order)? CombatQueue { get; set; }
    public int AttackSlot { get; set; } = -1;
    public Vector3? CombatDestination { get; set; }
    public Vector3? CombatSteerWaypoint { get; set; }
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
        TickBuffs(attackBuffTimers, deltaTime);
        TickBuffs(moveSpeedBuffTimers, deltaTime);
        TickBuffs(armorBuffTimers, deltaTime);
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

    public void QueueDamage(int damage)
    {
        long armor = Faction.Armor + (long)ArmorBonus * ArmorBuffStacks;
        long applied = Math.Max(0, (long)damage - armor);
        pendingDamage = (int)Math.Min(int.MaxValue, pendingDamage + applied);
    }

    public void ResolveDamage()
    {
        Health = Mathf.Max(0, Health - pendingDamage);
        pendingDamage = 0;
    }

    public void Die() => Health = 0;

    public void ApplyAttackBuff(float duration) => attackBuffTimers.Add(duration);

    public void ApplyMoveSpeedBuff(float duration) => moveSpeedBuffTimers.Add(duration);

    public void ApplyArmorBuff(float duration) => armorBuffTimers.Add(duration);

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
