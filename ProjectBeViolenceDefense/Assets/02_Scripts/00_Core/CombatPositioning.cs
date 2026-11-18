using System.Collections.Generic;
using UnityEngine;

internal sealed class CombatPositioning
{
    internal const int MaxAttackersPerTarget = 3;
    internal const float StalledRetargetDelay = 0.6f;
    internal const float CombatJoinRange = 4.5f;
    private const float ApproachProgressThreshold = 0.05f;
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
    private static readonly float[] BypassWidths = { 0.125f, 0.25f, 0.5f, 1f, 2f };

    private readonly DefenseGrid grid;
    private readonly IReadOnlyList<EnemyUnit> enemies;
    internal const float BodyGap = 0.06f;

    internal static float RequiredSpacing(EnemyFaction first, EnemyFaction second) =>
        first.BodyRadius + second.BodyRadius + BodyGap;
    private readonly List<EnemyUnit> assignmentOrder = new();
    private readonly Dictionary<EnemyUnit, (int Mask, int Count)> reservedSlots = new();
    private readonly List<(Vector3 Position, float Radius)> reservedPositions = new();
    private readonly HashSet<EnemyUnit> alertedUnits = new();
    private readonly List<EnemyUnit> activeFighters = new();
    private int nextQueueOrder;

    public CombatPositioning(DefenseGrid grid, IReadOnlyList<EnemyUnit> enemies)
    {
        this.grid = grid;
        this.enemies = enemies;
    }

    public void Plan()
    {
        FindNearbyFights();
        assignmentOrder.Clear();
        reservedSlots.Clear();
        reservedPositions.Clear();
        foreach (EnemyUnit enemy in enemies)
        {
            EnemyUnit target = FindTarget(enemy);
            if (target != enemy.CombatProgressTarget)
                ResetApproachProgress(enemy, target);
            if (target != enemy.Target)
            {
                enemy.AttackSlot = -1;
                enemy.CombatSteerWaypoint = null;
            }
            if (enemy.CombatQueue is { } queue && queue.Target != target)
                enemy.CombatQueue = null;
            enemy.Target = target;
            enemy.CombatDestination = null;
            if (target != null)
                assignmentOrder.Add(enemy);
        }
        assignmentOrder.Sort(CompareAttackers);

        // Allocate actual striking positions before approach reservations. A distant incumbent
        // must not keep a ready fighter waiting beside an opponent (dynamic slot reassignment).
        foreach (EnemyUnit enemy in assignmentOrder)
        {
            EnemyUnit immediateTarget = FindImmediateTarget(enemy, enemy.Target);
            if (immediateTarget != null)
                Reserve(enemy, immediateTarget, CurrentPositionSlot, enemy.Position);
        }

        foreach (EnemyUnit enemy in assignmentOrder)
        {
            if (enemy.CombatDestination.HasValue)
                continue;

            EnemyUnit target = enemy.Target;

            // A local sidestep alone is not proof that this fight is reachable. Probe other
            // complete approaches before renewing a stalled target's reservation.
            if (enemy.CombatStalledTime >= StalledRetargetDelay)
            {
                ResetApproachProgress(enemy, target);
                if (FindAlternative(enemy, target, false, out EnemyUnit reachable,
                        out int reachableSlot, out Vector3 reachableWaypoint, out float reachableLength))
                {
                    Reserve(enemy, reachable, reachableSlot, reachableWaypoint, reachableLength);
                    continue;
                }
            }

            int slot = enemy.AttackSlot;
            if (slot >= 0 && TryApproachSlot(enemy, target, slot, true, out Vector3 waypoint, out float length))
            {
                Reserve(enemy, target, slot, waypoint, length);
                continue;
            }

            enemy.AttackSlot = -1;
            if (FindSlot(enemy, target, out slot, out waypoint, out length))
            {
                Reserve(enemy, target, slot, waypoint, length);
                continue;
            }

            // A full target or an unreachable slot sends the unit to another visible opponent.
            if (FindAlternative(enemy, target, false, out EnemyUnit alternative, out slot, out waypoint, out length) ||
                FindAlternative(enemy, target, true, out alternative, out slot, out waypoint, out length))
            {
                Reserve(enemy, alternative, slot, waypoint, length);
                continue;
            }

            EnemyUnit queueTarget = reservedSlots.ContainsKey(target) ? target : null;
            foreach (EnemyUnit other in enemies)
            {
                if (other == target || !CanDetect(enemy, other))
                    continue;
                if (queueTarget == null && reservedSlots.ContainsKey(other))
                {
                    queueTarget = other;
                }
            }
            enemy.Target = null;
            if (queueTarget != null)
                QueueBehindFight(enemy, queueTarget);
            else
                enemy.CombatQueue = null;
        }
    }

    // Called with simulation delta before planning, so replanning/slot changes cannot
    // erase the time already spent stuck. Cooldown while in striking position is not a stall.
    public void TrackApproachProgress(EnemyUnit enemy, float deltaTime)
    {
        EnemyUnit target = enemy.Target ?? enemy.CombatQueue?.Target;
        if (target == null || !target.IsAlive || target != enemy.CombatProgressTarget || CanAttack(enemy, target))
            ResetApproachProgress(enemy, target);
        if (target == null || !target.IsAlive || CanAttack(enemy, target))
            return;

        float remaining = enemy.CombatDestination is { } destination && enemy.Target != null
            ? Vector3.Distance(enemy.Position, destination) + enemy.CombatApproachTailLength
            : Vector3.Distance(enemy.Position, target.Position);
        // Compare against the best remaining route, not distance moved: oscillation must
        // not keep a blocked target alive. Include the tail so genuine detours make progress.
        if (remaining <= enemy.CombatBestRemainingDistance - ApproachProgressThreshold)
        {
            enemy.CombatBestRemainingDistance = remaining;
            enemy.CombatStalledTime = 0f;
        }
        else
            enemy.CombatStalledTime += deltaTime;
    }

    private static void ResetApproachProgress(EnemyUnit enemy, EnemyUnit target)
    {
        enemy.CombatProgressTarget = target;
        enemy.CombatBestRemainingDistance = float.PositiveInfinity;
        enemy.CombatStalledTime = 0f;
    }

    private bool FindAlternative(EnemyUnit enemy, EnemyUnit current, bool allowLocalSteer,
        out EnemyUnit alternative, out int slot, out Vector3 waypoint, out float length)
    {
        alternative = null;
        slot = -1;
        waypoint = default;
        length = float.PositiveInfinity;
        foreach (EnemyUnit other in enemies)
        {
            if (other == current || !CanDetect(enemy, other) ||
                !FindSlot(enemy, other, out int candidateSlot, out Vector3 candidateWaypoint, out float candidateLength, allowLocalSteer) ||
                !IsNearer(other, candidateLength, alternative, length))
                continue;
            alternative = other;
            slot = candidateSlot;
            waypoint = candidateWaypoint;
            length = candidateLength;
        }
        return alternative != null;
    }

    public bool CanAttack(EnemyUnit enemy, EnemyUnit target) =>
        enemy.Target == target && CanAttackFromHere(enemy, target);

    private bool CanAttackFromHere(EnemyUnit enemy, EnemyUnit target)
    {
        float range = enemy.Faction.AttackRange;
        return target.IsAlive && target.FactionIndex != enemy.FactionIndex &&
            (enemy.Position - target.Position).sqrMagnitude <= range * range &&
            grid.HasLineOfSight(enemy.Position, target.Position) &&
            ClearOfUnits(enemy, target, enemy.Position, target.Position, attacking: true);
    }

    public bool CanFollowRoute(EnemyUnit enemy, Vector3 next) =>
        ClearOfUnits(enemy, null, enemy.Position, next, attacking: false);

    public bool TryFindRouteYield(EnemyUnit enemy, int waypointCount, out Vector3 destination, out int routeOffset)
    {
        destination = default;
        routeOffset = -1;
        int count = Mathf.Min(Mathf.Max(waypointCount, enemy.RouteBypassOffset + 1), enemy.RemainingRouteWaypoints - 1);
        // A sidestep is useful only if it connects to a forward route waypoint. A free
        // patch beside a stationary queue otherwise causes endless leave/rejoin cycles.
        for (int offset = count - 1; offset >= Mathf.Max(0, enemy.RouteBypassOffset); offset--)
        {
            Vector3 goal = enemy.GetRouteWaypoint(offset);
            float shortest = float.PositiveInfinity;
            foreach (Vector3 direction in Directions)
            {
                foreach (float width in BypassWidths)
                {
                    Vector3 point = enemy.Position + direction * ((enemy.Faction.BodyRadius * 2f + BodyGap) * width);
                    float distance = Vector3.Distance(enemy.Position, point) + Vector3.Distance(point, goal);
                    if (distance >= shortest || !CanMove(enemy, null, enemy.Position, point) ||
                        !CanMove(enemy, null, point, goal))
                        continue;
                    shortest = distance;
                    destination = point;
                    routeOffset = offset;
                }
            }
            if (routeOffset >= 0)
                return true;
        }
        return false;
    }

    public void Move(EnemyUnit enemy, EnemyUnit target, float deltaTime)
    {
        if (CanAttack(enemy, target))
        {
            enemy.CombatSteerWaypoint = null;
            return;
        }
        if (enemy.CombatDestination is not { } destination)
            return;

        // Keep the selected side until the short waypoint is reached; don't alternate sides
        // every tick when two agents meet. Revalidate against the current movement snapshot.
        if (enemy.CombatSteerWaypoint is { } steer)
        {
            if (enemy.Position.Equals(steer) || !CanMove(enemy, target, enemy.Position, steer))
                enemy.CombatSteerWaypoint = null;
            else
                destination = steer;
        }
        Vector3 next = enemy.StepToward(destination, deltaTime);
        if (!CanMove(enemy, target, enemy.Position, next) &&
            TryLocalSteer(enemy, target, destination, out Vector3 detour))
        {
            enemy.CombatSteerWaypoint = detour;
            next = enemy.StepToward(detour, deltaTime);
        }
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
            if (!IsNearer(other, distance, nearest, nearestDistance) || !CanDetect(enemy, other))
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
            if (other == preferred || !IsNearer(other, distance, nearest, nearestDistance) ||
                IsTargetFull(other) || !CanAttackFromHere(enemy, other))
                continue;

            nearest = other;
            nearestDistance = distance;
        }
        return nearest;
    }

    private static bool IsNearer(EnemyUnit candidate, float distance, EnemyUnit current, float currentDistance) =>
        distance < currentDistance || (distance == currentDistance &&
            (current == null || candidate.SpawnOrder < current.SpawnOrder));

    private bool CanDetect(EnemyUnit enemy, EnemyUnit other)
    {
        float range = alertedUnits.Contains(enemy) || enemy.Target == other || enemy.CombatQueue?.Target == other
            ? Mathf.Max(enemy.Faction.AwarenessRange, CombatJoinRange) : enemy.Faction.AwarenessRange;
        return other.IsAlive && other.FactionIndex != enemy.FactionIndex &&
            (other.Position - enemy.Position).sqrMagnitude <= range * range &&
            grid.HasLineOfSight(enemy.Position, other.Position);
    }

    private void FindNearbyFights()
    {
        activeFighters.Clear();
        alertedUnits.Clear();
        // Snapshot established fights before assigning this plan. Merely acquiring a
        // distant target must not relay awareness indefinitely through the whole army.
        foreach (EnemyUnit enemy in enemies)
            if (enemy.IsAlive && enemy.Target is { IsAlive: true } target && CanAttackFromHere(enemy, target))
                activeFighters.Add(enemy);
        foreach (EnemyUnit enemy in enemies)
            foreach (EnemyUnit fighter in activeFighters)
                if ((enemy.Position - fighter.Position).sqrMagnitude <= CombatJoinRange * CombatJoinRange &&
                    grid.HasLineOfSight(enemy.Position, fighter.Position))
                {
                    alertedUnits.Add(enemy);
                    break;
                }
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

    private void Reserve(EnemyUnit enemy, EnemyUnit target, int slot, Vector3 waypoint, float length = 0f)
    {
        if (enemy.CombatProgressTarget != target || slot == CurrentPositionSlot)
            ResetApproachProgress(enemy, target);
        if (enemy.Target != target || slot == CurrentPositionSlot)
            enemy.CombatSteerWaypoint = null;
        enemy.Target = target;
        enemy.CombatQueue = null;
        enemy.AttackSlot = slot;
        enemy.CombatDestination = waypoint;
        enemy.CombatApproachTailLength = Mathf.Max(0f, length - Vector3.Distance(enemy.Position, waypoint));
        enemy.RouteYieldDestination = null;
        enemy.RouteBypassOffset = -1;
        enemy.BlockedRouteTime = 0f;
        reservedSlots.TryGetValue(target, out var reservation);
        int mask = slot == CurrentPositionSlot ? reservation.Mask : reservation.Mask | (1 << slot);
        reservedSlots[target] = (mask, reservation.Count + 1);
        reservedPositions.Add((slot == CurrentPositionSlot ? waypoint : SlotPosition(enemy, target, slot), enemy.Faction.BodyRadius));
    }

    private void QueueBehindFight(EnemyUnit enemy, EnemyUnit target)
    {
        enemy.CombatSteerWaypoint = null;
        if (enemy.CombatQueue?.Target != target)
            enemy.CombatQueue = (target, nextQueueOrder++);
        enemy.RouteYieldDestination = null;
        enemy.RouteBypassOffset = -1;
        enemy.BlockedRouteTime = 0f;

        // Leave room for the attackers without stepping outside detection range.
        float range = enemy.Faction.AttackRange;
        float distance = Mathf.Min(range + RequiredSpacing(enemy.Faction, target.Faction), enemy.Faction.AwarenessRange - QueueAwarenessMargin);
        Vector3 offset = enemy.Position - target.Position;
        if (distance <= range || offset.sqrMagnitude >= distance * distance)
            return;

        Vector3 rear = target.Position + offset.normalized * distance;
        if (CanMove(enemy, target, enemy.Position, rear))
            enemy.CombatDestination = rear;
    }

    private bool FindSlot(EnemyUnit enemy, EnemyUnit target, out int slot, out Vector3 waypoint,
        out float shortest, bool allowLocalSteer = true)
    {
        slot = -1;
        waypoint = default;
        shortest = float.PositiveInfinity;
        if (IsTargetFull(target))
            return false;

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
            if (!TryApproachSlot(enemy, target, i, true, out Vector3 next, out float length, allowLocalSteer) || length >= shortest)
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
        out Vector3 waypoint, out float length, bool allowLocalSteer = true)
    {
        waypoint = SlotPosition(enemy, target, slot);
        length = float.PositiveInfinity;
        if (RequiredSpacing(enemy.Faction, target.Faction) > enemy.Faction.AttackRange)
            return false;
        if (!grid.IsWalkable(DefenseGrid.PositionToCell(waypoint)) || !grid.HasLineOfSight(waypoint, target.Position))
            return false;

        if ((reservedSlots.TryGetValue(target, out var reservation) &&
                (reservation.Count >= MaxAttackersPerTarget || (reservation.Mask & (1 << slot)) != 0)) ||
            !ClearOfUnits(enemy, target, waypoint, target.Position, attacking: true))
            return false;
        foreach (var reserved in reservedPositions)
        {
            float spacing = enemy.Faction.BodyRadius + reserved.Radius + BodyGap;
            if ((reserved.Position - waypoint).sqrMagnitude < spacing * spacing)
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
        float outerRadius = enemy.Faction.AttackRange + RequiredSpacing(enemy.Faction, target.Faction);
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
        if (!float.IsPositiveInfinity(length))
            return true;

        // A temporarily occupied corridor need not make a free attack slot unreachable.
        // Only use local steering when the static route to that slot is clear.
        if (allowLocalSteer && grid.CanPursue(enemy.Position, goal) && TryLocalSteer(enemy, target, goal, out Vector3 local))
        {
            waypoint = local;
            length = Vector3.Distance(enemy.Position, local) + Vector3.Distance(local, goal);
            return true;
        }
        return false;
    }

    private bool TryLocalSteer(EnemyUnit enemy, EnemyUnit target, Vector3 goal, out Vector3 waypoint)
    {
        waypoint = default;
        Vector3 desired = (goal - enemy.Position).normalized;
        if (desired.sqrMagnitude == 0f)
            return false;

        float shortest = float.PositiveInfinity;
        foreach (Vector3 direction in Directions)
        {
            // Tangential steps can open a lane; backwards wandering is not a useful fallback.
            if (Vector3.Dot(direction, desired) < -0.001f)
                continue;
            Vector3 point = enemy.Position + direction * Mathf.Clamp(enemy.Faction.BodyRadius * 3f, 0.4f, 0.9f);
            float cost = Vector3.Distance(point, goal);
            if (cost >= shortest || !CanMove(enemy, target, enemy.Position, point))
                continue;
            shortest = cost;
            waypoint = point;
        }
        return !float.IsPositiveInfinity(shortest);
    }

    private static Vector3 SlotPosition(EnemyUnit enemy, EnemyUnit target, int slot) =>
        target.Position + Directions[slot] * Mathf.Min(enemy.Faction.AttackRange,
            Mathf.Max(enemy.Faction.AttackRange * 0.85f, RequiredSpacing(enemy.Faction, target.Faction) + 0.02f));

    private bool CanMove(EnemyUnit enemy, EnemyUnit target, Vector3 from, Vector3 to) =>
        grid.IsWalkable(DefenseGrid.PositionToCell(from)) &&
        grid.IsWalkable(DefenseGrid.PositionToCell(to)) &&
        ClearOfUnits(enemy, target, from, to, attacking: false) &&
        grid.CanPursue(from, to) &&
        HasRouteConnection(enemy, to);

    private bool HasRouteConnection(EnemyUnit enemy, Vector3 position)
    {
        if (grid.CanPursue(enemy.RoutePosition, position))
            return true;
        // Flanking may cross a bend or gate. A visible forward route tile is a valid
        // return connection; requiring visibility to the old tile strands such units.
        for (int offset = 0; offset < enemy.RemainingRouteWaypoints - 1; offset++)
            if (grid.CanPursue(position, enemy.GetRouteWaypoint(offset)))
                return true;
        return false;
    }

    private bool ClearOfUnits(EnemyUnit enemy, EnemyUnit target, Vector3 from, Vector3 to, bool attacking)
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
            float spacing = attacking ? other.Faction.BodyRadius : RequiredSpacing(enemy.Faction, other.Faction);
            if ((offset - step * fraction).sqrMagnitude < spacing * spacing)
                return false;
        }
        return true;
    }
}
