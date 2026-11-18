using System;
using UnityEngine;

[Serializable]
internal sealed class EnemyFaction
{
    [SerializeField] private string displayName;
    [SerializeField] private Color color;
    [SerializeField, Min(0.1f)] private float attackRange;
    [SerializeField, Min(0.1f)] private float awarenessRange = 2.2f;

    [Header("Stats")]
    [SerializeField, Min(1)] private int maxHealth = 50;
    [SerializeField, Min(0)] private int damage = 10;
    [SerializeField, Min(0.05f)] private float attackInterval = 0.65f;
    [Tooltip("Units this faction spawns per round.")]
    [SerializeField, Min(0)] private int spawnCount = 9;

    [Header("Model (empty = default goblin)")]
    [SerializeField] private GameObject model;
    [SerializeField] private RuntimeAnimatorController animations;
    [SerializeField] private Material skin;
    [SerializeField, Min(0.01f)] private float modelScale = 1f;

    public string DisplayName => displayName;
    public Color Color => color;
    public float AttackRange => attackRange;
    public float AwarenessRange => Mathf.Max(attackRange, awarenessRange);
    public int MaxHealth => maxHealth;
    public int Damage => damage;
    public float AttackInterval => attackInterval;
    public float DamagePerSecond => damage / attackInterval;
    public int SpawnCount => spawnCount;
    public GameObject Model => model;
    public RuntimeAnimatorController Animations => animations;
    public Material Skin => skin;
    public float ModelScale => modelScale;

    public EnemyFaction(string displayName, Color color, float attackRange = 1.5f)
    {
        this.displayName = displayName;
        this.color = color;
        this.attackRange = attackRange;
    }

    public EnemyFaction(string displayName, Color color, float attackRange, int maxHealth, int damage,
        float attackInterval, int spawnCount) : this(displayName, color, attackRange)
    {
        this.maxHealth = maxHealth;
        this.damage = damage;
        this.attackInterval = attackInterval;
        this.spawnCount = spawnCount;
    }
}

internal sealed class EnemyUnit
{
    public const int AttackBonus = 10;
    public const int MoveSpeedBonusPercent = 50;
    private const float MoveSpeed = 1.70f;

    private readonly Vector3[] path;
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
    public Vector3 RoutePosition { get; private set; }
    public Vector3 RouteWaypoint => path[waypoint];
    public int RemainingRouteWaypoints => path.Length - waypoint;
    public Vector3 Heading { get; private set; }
    public float AttackBuffRemaining { get; private set; }
    public float MoveSpeedBuffRemaining { get; private set; }
    public float HealFlash { get; private set; }
    public float AttackFlash => attackFlash;
    public int AttacksPerformed { get; private set; }
    public Vector3 AttackHeading { get; private set; }
    public bool IsAlive => Health > 0;
    public bool CanAttack => cooldown <= 0;
    public EnemyUnit Target { get; set; }
    public int AttackSlot { get; set; } = -1;
    public Vector3? CombatDestination { get; set; }
    public Vector3? RouteYieldDestination { get; set; }
    public float BlockedRouteTime { get; set; }
    public bool ReachedCore => Position.Equals(path[^1]);

    public EnemyUnit(EnemyFaction faction, int factionIndex, Vector3[] path, int spawnOrder)
    {
        Faction = faction;
        FactionIndex = factionIndex;
        SpawnOrder = spawnOrder;
        Health = faction.MaxHealth;
        this.path = path;
        Position = RoutePosition = path[0];
        Heading = path[1] - Position;
    }

    public void TickTimers(float deltaTime)
    {
        cooldown = Mathf.Max(0, cooldown - deltaTime);
        attackFlash = Mathf.Max(0, attackFlash - deltaTime);
        HealFlash = Mathf.Max(0, HealFlash - deltaTime);
        AttackBuffRemaining = Mathf.Max(0, AttackBuffRemaining - deltaTime);
        MoveSpeedBuffRemaining = Mathf.Max(0, MoveSpeedBuffRemaining - deltaTime);
    }

    public void Face(Vector3 point)
    {
        Vector3 heading = point - Position;
        if (heading.sqrMagnitude > 0.0001f)
            Heading = heading;
    }

    public int Attack()
    {
        cooldown = Faction.AttackInterval;
        attackFlash = 0.14f;
        AttacksPerformed++;
        AttackHeading = Heading;
        return Faction.Damage + (AttackBuffRemaining > 0 ? AttackBonus : 0);
    }

    public void QueueDamage(int damage) => pendingDamage += damage;

    public void ResolveDamage()
    {
        Health = Mathf.Max(0, Health - pendingDamage);
        pendingDamage = 0;
    }

    public void Die() => Health = 0;

    public void ApplyAttackBuff(float duration) => AttackBuffRemaining = duration;

    public void ApplyMoveSpeedBuff(float duration) => MoveSpeedBuffRemaining = duration;

    public void Heal(int amount)
    {
        Health = Mathf.Min(MaxHealth, Health + amount);
        HealFlash = 0.55f;
    }

    public Vector3 NextRoutePosition(float deltaTime) =>
        StepToward(Position.Equals(RoutePosition) ? path[waypoint] : RoutePosition, deltaTime);

    public Vector3 StepToward(Vector3 destination, float deltaTime)
    {
        float speedMultiplier = MoveSpeedBuffRemaining > 0 ? 1f + MoveSpeedBonusPercent / 100f : 1f;
        return Vector3.MoveTowards(Position, destination, MoveSpeed * speedMultiplier * deltaTime);
    }

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
