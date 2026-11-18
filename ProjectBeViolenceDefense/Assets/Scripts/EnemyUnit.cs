using System;
using UnityEngine;

[Serializable]
internal sealed class EnemyFaction
{
    [SerializeField] private string displayName;
    [SerializeField] private Color color;
    [SerializeField, Min(0.1f)] private float attackRange;

    public string DisplayName => displayName;
    public Color Color => color;
    public float AttackRange => attackRange;

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
    private readonly float lastStepLength;
    private int waypoint = 1;
    private int pendingDamage;
    private float cooldown;
    private float attackFlash;

    public EnemyFaction Faction { get; }
    public int SpawnOrder { get; }
    public int Health { get; private set; } = MaxHealth;
    public Vector3 Position { get; private set; }
    public Vector3 Heading { get; private set; }
    public float AttackBuffRemaining { get; private set; }
    public float MoveSpeedBuffRemaining { get; private set; }
    public float HealFlash { get; private set; }
    public float AttackFlash => attackFlash;
    public bool IsAlive => Health > 0;
    public bool CanAttack => cooldown <= 0;
    public EnemyUnit Target { get; set; }
    public bool AttackingCore { get; set; }

    public float DistanceToCore => Vector3.Distance(Position, path[waypoint]) +
        (waypoint == path.Length - 1 ? 0f : path.Length - 2 - waypoint + lastStepLength);

    public EnemyUnit(EnemyFaction faction, Vector3[] path, int spawnOrder)
    {
        Faction = faction;
        SpawnOrder = spawnOrder;
        this.path = path;
        lastStepLength = Vector3.Distance(path[^2], path[^1]);
        Position = path[0];
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

    public void ApplyAttackBuff(float duration) => AttackBuffRemaining = duration;

    public void ApplyMoveSpeedBuff(float duration) => MoveSpeedBuffRemaining = duration;

    public void Heal(int amount)
    {
        Health = Mathf.Min(MaxHealth, Health + amount);
        HealFlash = 0.55f;
    }

    public Vector3 NextPosition(float deltaTime)
    {
        float speedMultiplier = MoveSpeedBuffRemaining > 0 ? 1f + MoveSpeedBonusPercent / 100f : 1f;
        return Vector3.MoveTowards(Position, path[waypoint], MoveSpeed * speedMultiplier * deltaTime);
    }

    public void MoveTo(Vector3 next)
    {
        Position = next;
        if (next == path[waypoint] && waypoint < path.Length - 1)
            waypoint++;
    }
}
