using System.Collections.Generic;
using UnityEngine;

internal sealed class CombatPositioning
{
    internal const int MaxAttackersPerTarget = 3;
    private const int CurrentPositionSlot = -2;
    private const int SlotCount = 8;
    private const int DetourPointsPerSide = SlotCount / 2;
    private const float Diagonal = 0.70710678f;
    private const float QueueAwarenessMargin = 0.22f;
    private static readonly Vector3[] Directions =
    {
        Vector3.right, new(Diagonal, 0f, Diagonal),
        Vector3.forward, new(-Diagonal, 0f, Diagonal),
        Vector3.left, new(-Diagonal, 0f, -Diagonal),
        Vector3.back, new(Diagonal, 0f, -Diagonal)
    };

    private readonly DefenseGrid grid;
    private readonly IReadOnlyList<EnemyUnit> enemies;
    private readonly float unitSpacing;
    private readonly List<EnemyUnit> assignmentOrder = new();
    private readonly Dictionary<EnemyUnit, (int Mask, int Count)> reservedSlots = new();
    private readonly List<Vector3> reservedPositions = new();
    private int nextQueueOrder;

    public CombatPositioning(DefenseGrid grid, IReadOnlyList<EnemyUnit> enemies, float unitSpacing)
    {
        this.grid = grid;
        this.enemies = enemies;
        this.unitSpacing = unitSpacing;
    }

    public void Plan()
    {
        assignmentOrder.Clear();
        reservedSlots.Clear();
        reservedPositions.Clear();
        foreach (EnemyUnit enemy in enemies)
        {
            EnemyUnit target = FindTarget(enemy);
            if (target != enemy.Target)
                enemy.AttackSlot = -1;
            if (enemy.CombatQueue is { } queue && queue.Target != target)
                enemy.CombatQueue = null;
            enemy.Target = target;
            enemy.CombatDestination = null;
            if (target != null)
                assignmentOrder.Add(enemy);
        }
        assignmentOrder.Sort(CompareAttackers);

        foreach (EnemyUnit enemy in assignmentOrder)
        {
            EnemyUnit target = enemy.Target;
            EnemyUnit immediateTarget = FindImmediateTarget(enemy, target);
            if (immediateTarget != null)
            {
                Reserve(enemy, immediateTarget, CurrentPositionSlot, enemy.Position);
                continue;
            }

            int slot = enemy.AttackSlot;
            if (slot >= 0 && TryApproachSlot(enemy, target, slot, true, out Vector3 waypoint, out _))
            {
                Reserve(enemy, target, slot, waypoint);
                continue;
            }

            enemy.AttackSlot = -1;
            if (FindSlot(enemy, target, out slot, out waypoint))
            {
                Reserve(enemy, target, slot, waypoint);
                continue;
            }

            // A full target or an unreachable slot sends the unit to another visible opponent.
            EnemyUnit alternative = null;
            float nearest = float.PositiveInfinity;
            EnemyUnit queueTarget = reservedSlots.ContainsKey(target) ? target : null;
            foreach (EnemyUnit other in enemies)
            {
                float distance = (other.Position - enemy.Position).sqrMagnitude;
                if (other == target || !CanDetect(enemy, other))
                    continue;

                if (distance < nearest && FindSlot(enemy, other, out int otherSlot, out Vector3 otherWaypoint))
                {
                    alternative = other;
                    nearest = distance;
                    slot = otherSlot;
                    waypoint = otherWaypoint;
                }
                else if (queueTarget == null && reservedSlots.ContainsKey(other))
                {
                    queueTarget = other;
                }
            }
            if (alternative != null)
            {
                Reserve(enemy, alternative, slot, waypoint);
            }
            else
            {
                enemy.Target = null;
                if (queueTarget != null)
                    QueueBehindFight(enemy, queueTarget);
                else
                    enemy.CombatQueue = null;
            }
        }
    }

    public bool CanAttack(EnemyUnit enemy, EnemyUnit target) =>
        enemy.Target == target && CanAttackFromHere(enemy, target);

    private bool CanAttackFromHere(EnemyUnit enemy, EnemyUnit target)
    {
        float range = enemy.Faction.AttackRange;
        return target.IsAlive && target.Faction != enemy.Faction &&
            (enemy.Position - target.Position).sqrMagnitude <= range * range &&
            grid.HasLineOfSight(enemy.Position, target.Position) &&
            ClearOfUnits(enemy, target, enemy.Position, target.Position, unitSpacing * 0.5f, attacking: true);
    }

    public bool CanFollowRoute(EnemyUnit enemy, Vector3 next) =>
        ClearOfUnits(enemy, null, enemy.Position, next, unitSpacing, attacking: false);

    public bool TryFindRouteYield(EnemyUnit enemy, out Vector3 destination)
    {
        destination = default;
        float bestDistance = float.PositiveInfinity;
        foreach (Vector3 direction in Directions)
        {
            Vector3 point = enemy.Position + direction * unitSpacing;
            float distance = (point - enemy.RouteWaypoint).sqrMagnitude;
            if (distance >= bestDistance || !CanMove(enemy, null, enemy.Position, point))
                continue;
            bestDistance = distance;
            destination = point;
        }

        return !float.IsPositiveInfinity(bestDistance);
    }

    public void Move(EnemyUnit enemy, EnemyUnit target, float deltaTime)
    {
        if (enemy.CombatDestination is not { } destination || CanAttack(enemy, target))
            return;

        Vector3 next = enemy.StepToward(destination, deltaTime);
        if (CanMove(enemy, target, enemy.Position, next))
        {
            enemy.Face(next);
            enemy.MoveOffRoute(next);
        }
    }

    private EnemyUnit FindTarget(EnemyUnit enemy)
    {
        EnemyUnit target = enemy.Target ?? enemy.CombatQueue?.Target;
        if (target != null && CanDetect(enemy, target))
            return target;

        EnemyUnit nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (EnemyUnit other in enemies)
        {
            float distance = (other.Position - enemy.Position).sqrMagnitude;
            if (distance >= nearestDistance || !CanDetect(enemy, other))
                continue;

            nearest = other;
            nearestDistance = distance;
        }
        return nearest;
    }

    private EnemyUnit FindImmediateTarget(EnemyUnit enemy, EnemyUnit preferred)
    {
        if (!IsTargetFull(preferred) && CanAttackFromHere(enemy, preferred))
            return preferred;

        EnemyUnit nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (EnemyUnit other in enemies)
        {
            float distance = (other.Position - enemy.Position).sqrMagnitude;
            if (other == preferred || distance >= nearestDistance ||
                IsTargetFull(other) || !CanAttackFromHere(enemy, other))
                continue;

            nearest = other;
            nearestDistance = distance;
        }
        return nearest;
    }

    private bool CanDetect(EnemyUnit enemy, EnemyUnit other)
    {
        float range = enemy.Faction.AwarenessRange;
        return other.IsAlive && other.Faction != enemy.Faction &&
            (other.Position - enemy.Position).sqrMagnitude <= range * range &&
            grid.HasLineOfSight(enemy.Position, other.Position);
    }

    private static int CompareAttackers(EnemyUnit first, EnemyUnit second)
    {
        int incumbent = (second.AttackSlot != -1).CompareTo(first.AttackSlot != -1);
        if (incumbent != 0)
            return incumbent;

        int queued = (first.CombatQueue?.Order ?? int.MaxValue).CompareTo(second.CombatQueue?.Order ?? int.MaxValue);
        if (queued != 0)
            return queued;

        float firstDistance = (first.Target.Position - first.Position).sqrMagnitude;
        float secondDistance = (second.Target.Position - second.Position).sqrMagnitude;
        int distance = firstDistance.CompareTo(secondDistance);
        return distance != 0 ? distance : first.SpawnOrder.CompareTo(second.SpawnOrder);
    }

    private void Reserve(EnemyUnit enemy, EnemyUnit target, int slot, Vector3 waypoint)
    {
        enemy.Target = target;
        enemy.CombatQueue = null;
        enemy.AttackSlot = slot;
        enemy.CombatDestination = waypoint;
        enemy.RouteYieldDestination = null;
        enemy.BlockedRouteTime = 0f;
        reservedSlots.TryGetValue(target, out var reservation);
        int mask = slot == CurrentPositionSlot ? reservation.Mask : reservation.Mask | (1 << slot);
        reservedSlots[target] = (mask, reservation.Count + 1);
        reservedPositions.Add(slot == CurrentPositionSlot ? waypoint : SlotPosition(enemy, target, slot));
    }

    private void QueueBehindFight(EnemyUnit enemy, EnemyUnit target)
    {
        if (enemy.CombatQueue?.Target != target)
            enemy.CombatQueue = (target, nextQueueOrder++);
        enemy.RouteYieldDestination = null;
        enemy.BlockedRouteTime = 0f;

        // Leave room for the attackers without stepping outside detection range.
        float range = enemy.Faction.AttackRange;
        float distance = Mathf.Min(range + unitSpacing, enemy.Faction.AwarenessRange - QueueAwarenessMargin);
        Vector3 offset = enemy.Position - target.Position;
        if (distance <= range || offset.sqrMagnitude >= distance * distance)
            return;

        Vector3 rear = target.Position + offset.normalized * distance;
        if (CanMove(enemy, target, enemy.Position, rear))
            enemy.CombatDestination = rear;
    }

    private bool FindSlot(EnemyUnit enemy, EnemyUnit target, out int slot, out Vector3 waypoint)
    {
        slot = -1;
        waypoint = default;
        if (IsTargetFull(target))
            return false;

        float shortest = float.PositiveInfinity;
        for (int i = 0; i < SlotCount; i++)
        {
            if (!TryApproachSlot(enemy, target, i, false, out Vector3 next, out float length) || length >= shortest)
                continue;
            slot = i;
            waypoint = next;
            shortest = length;
        }
        if (slot >= 0)
            return true;

        for (int i = 0; i < SlotCount; i++)
        {
            if (!TryApproachSlot(enemy, target, i, true, out Vector3 next, out float length) || length >= shortest)
                continue;
            slot = i;
            waypoint = next;
            shortest = length;
        }
        return slot >= 0;
    }

    private bool IsTargetFull(EnemyUnit target) =>
        reservedSlots.TryGetValue(target, out var reservation) && reservation.Count >= MaxAttackersPerTarget;

    private bool TryApproachSlot(EnemyUnit enemy, EnemyUnit target, int slot, bool allowDetour,
        out Vector3 waypoint, out float length)
    {
        waypoint = SlotPosition(enemy, target, slot);
        length = float.PositiveInfinity;
        if (!grid.IsWalkable(DefenseGrid.PositionToCell(waypoint)) || !grid.HasLineOfSight(waypoint, target.Position))
            return false;

        if ((reservedSlots.TryGetValue(target, out var reservation) &&
                (reservation.Count >= MaxAttackersPerTarget || (reservation.Mask & (1 << slot)) != 0)) ||
            !ClearOfUnits(enemy, target, waypoint, target.Position, unitSpacing * 0.5f, attacking: true))
            return false;
        foreach (Vector3 reserved in reservedPositions)
        {
            if ((reserved - waypoint).sqrMagnitude < unitSpacing * unitSpacing)
                return false;
        }

        Vector3 goal = waypoint;
        if (CanMove(enemy, target, enemy.Position, goal))
        {
            length = Vector3.Distance(enemy.Position, goal);
            return true;
        }
        if (!allowDetour)
            return false;

        // Work backwards from the attack slot along either edge of the fight.
        float outerRadius = enemy.Faction.AttackRange + unitSpacing;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 next = goal;
            float remaining = 0f;
            for (int leg = 0; leg < DetourPointsPerSide; leg++)
            {
                int direction = (slot + side * (leg + 1) + SlotCount) % SlotCount;
                Vector3 corner = target.Position + Directions[direction] * outerRadius;
                if (!CanMove(enemy, target, corner, next))
                    break;

                remaining += Vector3.Distance(corner, next);
                float cost = Vector3.Distance(enemy.Position, corner) + remaining;
                if (cost < length && CanMove(enemy, target, enemy.Position, corner))
                {
                    length = cost;
                    waypoint = corner;
                }
                next = corner;
            }
        }
        return !float.IsPositiveInfinity(length);
    }

    private static Vector3 SlotPosition(EnemyUnit enemy, EnemyUnit target, int slot) =>
        target.Position + Directions[slot] * (enemy.Faction.AttackRange * 0.85f);

    private bool CanMove(EnemyUnit enemy, EnemyUnit target, Vector3 from, Vector3 to) =>
        grid.IsWalkable(DefenseGrid.PositionToCell(from)) &&
        grid.IsWalkable(DefenseGrid.PositionToCell(to)) &&
        ClearOfUnits(enemy, target, from, to, unitSpacing, attacking: false) &&
        grid.CanPursue(from, to) &&
        // A unit that leaves the fight can return directly to its original route.
        grid.CanPursue(enemy.RoutePosition, to);

    private bool ClearOfUnits(EnemyUnit enemy, EnemyUnit target, Vector3 from, Vector3 to, float spacing, bool attacking)
    {
        Vector3 step = to - from;
        float lengthSquared = step.sqrMagnitude;
        foreach (EnemyUnit other in enemies)
        {
            if (other == enemy || (attacking && other == target))
                continue;

            Vector3 offset = other.Position - from;
            float along = Vector3.Dot(offset, step);
            if (!attacking && lengthSquared > 0f && along <= 0f)
                continue; // Allow a step away from a unit even when already very close.

            float fraction = lengthSquared > 0f ? Mathf.Clamp01(along / lengthSquared) : 0f;
            float clearance = other == target ? Mathf.Min(spacing, enemy.Faction.AttackRange * 0.8f) : spacing;
            if ((offset - step * fraction).sqrMagnitude < clearance * clearance)
                return false;
        }
        return true;
    }
}
