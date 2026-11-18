using System;
using UnityEngine;

[Serializable]
internal sealed class EnemyFaction
{
    [SerializeField] private string displayName;
    [SerializeField] private Color color;
    [SerializeField, Min(0.1f)] private float attackRange;
    [SerializeField, Min(0.1f)] private float awarenessRange = 2.2f;

    public string DisplayName => displayName;
    public Color Color => color;
    public float AttackRange => attackRange;
    public float AwarenessRange => Mathf.Max(attackRange, awarenessRange);

    public EnemyFaction(string displayName, Color color, float attackRange)
    {
        this.displayName = displayName;
        this.color = color;
        this.attackRange = attackRange;
    }
}

internal sealed class EnemyUnit
{
    public const int MaxHealth = 30;
    public const int AttackBonus = 10;
    public const int MoveSpeedBonusPercent = 50;
    private const int BaseDamage = 10;
    private const float AttackInterval = 0.65f;
    private const float MoveSpeed = 2.25f;

    private readonly Vector3[] path;
    private readonly float[] remainingDistance;
    private int waypoint = 1;
    private int pendingDamage;
    private float cooldown;
    private float attackFlash;

    public EnemyFaction Faction { get; }
    public int SpawnOrder { get; }
    public int FactionIndex { get; }
    public int Health { get; private set; } = MaxHealth;
    public Vector3 Position { get; private set; }
    public Vector3 RoutePosition { get; private set; }
    public Vector3 Heading { get; private set; }
    public float AttackBuffRemaining { get; private set; }
    public float MoveSpeedBuffRemaining { get; private set; }
    public float HealFlash { get; private set; }
    public float AttackFlash => attackFlash;
    public bool IsAlive => Health > 0;
    public bool CanAttack => cooldown <= 0;
    public EnemyUnit Target { get; set; }
    public bool ReachedCore => Position.Equals(path[^1]);

    public float DistanceToCore => Vector3.Distance(Position, RoutePosition) +
        Vector3.Distance(RoutePosition, path[waypoint]) + remainingDistance[waypoint];

    public EnemyUnit(EnemyFaction faction, int factionIndex, Vector3[] path, int spawnOrder)
    {
        Faction = faction;
        FactionIndex = factionIndex;
        SpawnOrder = spawnOrder;
        this.path = path;
        remainingDistance = new float[path.Length];
        for (int i = path.Length - 2; i >= 0; i--)
            remainingDistance[i] = remainingDistance[i + 1] + Vector3.Distance(path[i], path[i + 1]);
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
        cooldown = AttackInterval;
        attackFlash = 0.14f;
        return BaseDamage + (AttackBuffRemaining > 0 ? AttackBonus : 0);
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

    public void FollowRoute(Vector3 next)
    {
        if (Position.Equals(RoutePosition))
            RoutePosition = next;
        Position = next;
        if (next == path[waypoint] && waypoint < path.Length - 1)
            waypoint++;
    }
}
