using System.Collections.Generic;
using UnityEngine;

internal sealed class CombatPositioning
{
    internal const int MaxAttackersPerTarget = 3;
    internal const float StalledRetargetDelay = 0.6f;
    internal const float CombatJoinRange = 4.5f;
    internal const float LostTargetSearchDuration = 4f;
    // 보이지 않는 적을 쫓다가 이 시간 동안 조금도 가까워지지 못하면 추격을 그만두고 원래 경로로 돌아간다.
    internal const float HiddenPursuitStallLimit = 2f;
    // 적에게 가는 길이 직선 거리보다 이 거리(유닛 사거리·인지와 같은 단위) 넘게 길면 돌아가지 않는다.
    // 길은 있는데 유닛에 막힌 경우에는 뒤에서 기다리고, 지형 때문에 이만큼 돌아야 하는 적은 그 자리에서는
    // 상대하지 않는다. 칸 크기에 곱하지 않는다: 칸이 커져도 유닛이 감수할 우회는 그대로다. 첫 테스트용 임시 값.
    internal const float MaxPursuitDetour = 2f;
    // 옆걸음 직후 이 시간 동안은 방금 온 쪽으로 되돌아가는 옆걸음을 고르지 않는다.
    internal const float SidestepMemoryDuration = 2.5f;
    // 스스로 다른 적으로 바꾼 뒤 이 시간 동안은 다시 바꾸지 않는다.
    internal const float RetargetLockDuration = 1f;
    // 경로 밖에서 막힌 유닛이 이 여유 안으로 붙어 있는 유닛에게서 한 걸음 비켜선다.
    private const float StepAsideMargin = 0.15f;
    private const float StepAsideDistance = 0.4f;
    // 대기선보다 이만큼 넘게 멀 때만 다가간다. 대기선 근처에서 조금씩 움직이지 않게 한다.
    private const float WaitingRingSlack = 0.3f;
    // 뒤에서 기다리는 것은 싸움이 벌어지는 동안만이다. 기다리는 적이 이 시간 동안 누구와도 싸우지
    // 않으면 대기를 그만두고, 그 뒤 DisengageDuration 동안은 지금 자리에서 때릴 수 있는 적만 상대한다.
    // 서로 다른 적을 기다리는 유닛들이 원을 이뤄 영영 멈추는 것을 막는다. 첫 테스트용 임시 값.
    internal const float IdleQueueLimit = 2f;
    internal const float DisengageDuration = 2f;
    private const float ApproachProgressThreshold = 0.05f;
    private const int CurrentPositionSlot = -2;
    // 여덟 공격 자리가 아닌, 사거리 안에서 때릴 수 있는 임의 지점으로 가는 중.
    private const int OpenPositionSlot = -3;
    private const float StrikeRangeMargin = 0.05f;
    private const int SlotCount = 8;
    private const int DetourPointsPerSide = SlotCount / 2;
    private const float Diagonal = 0.70710678f;
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
    // 지난 계획 때 실제로 때리고 있던 유닛과 맞고 있던 유닛.
    private readonly HashSet<EnemyUnit> foughtUnits = new();
    private int nextQueueOrder;
    private int plannedEnemyCount;
    private readonly PursuitPathfinder pathfinder;
    private readonly List<Vector3> pathGoals = new();
    private readonly List<int> pathSlots = new();
    private readonly List<Vector3> staticPath = new();

    public CombatPositioning(DefenseGrid grid, IReadOnlyList<EnemyUnit> enemies)
    {
        this.grid = grid;
        this.enemies = enemies;
        pathfinder = new PursuitPathfinder(this);
    }

    public void Plan()
    {
        // A death changes the lanes: let waiting units probe either side again.
        if (enemies.Count < plannedEnemyCount)
            foreach (EnemyUnit enemy in enemies)
                enemy.SidestepMemoryRemaining = 0f;
        plannedEnemyCount = enemies.Count;
        FindNearbyFights();
        assignmentOrder.Clear();
        reservedSlots.Clear();
        reservedPositions.Clear();
        foreach (EnemyUnit enemy in enemies)
        {
            EnemyUnit target = FindTarget(enemy);
            RememberTarget(enemy, target);
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

            if (!grid.HasLineOfSight(enemy.Position, target.Position))
            {
                PursueLastKnownPosition(enemy, target);
                if (IsKnownUnreachable(enemy, target))
                {
                    if (FindAlternative(enemy, target, out EnemyUnit alternativeTarget,
                        out int alternativeSlot, out Vector3 alternativeWaypoint, out float alternativeLength))
                        Reserve(enemy, alternativeTarget, alternativeSlot, alternativeWaypoint, alternativeLength);
                    else
                    {
                        enemy.Target = null; enemy.CombatQueue = null; RememberTarget(enemy, null);
                    }
                }
                // A crowd can close every way to the place the opponent was last seen. Waiting
                // there forever can plug a gate that the same crowd needs, so let go of it.
                else if (enemy.CombatStalledTime >= HiddenPursuitStallLimit)
                {
                    enemy.Target = null; enemy.CombatQueue = null; enemy.CombatDestination = null;
                    RememberTarget(enemy, null);
                }
                continue;
            }

            if (TryUsePath(enemy, target, out Vector3 pathWaypoint, out float pathLength) &&
                (enemy.AttackSlot == OpenPositionSlot
                    ? !IsTargetFull(target) && CanStrikeFrom(enemy, target, enemy.CombatPath[^1])
                    : SlotAvailable(enemy, target, enemy.AttackSlot)))
            {
                Reserve(enemy, target, enemy.AttackSlot, pathWaypoint, pathLength);
                continue;
            }

            // A local sidestep alone is not proof that this fight is reachable. Probe other
            // complete approaches before renewing a stalled target's reservation.
            if (enemy.CombatStalledTime >= StalledRetargetDelay)
            {
                ResetApproachProgress(enemy, target);
                if (FindPathSlot(enemy, target, out int detourSlot, out Vector3 detourWaypoint, out float detourLength,
                        allowOpenPosition: true))
                {
                    Reserve(enemy, target, detourSlot, detourWaypoint, detourLength);
                    continue;
                }
                if (enemy.RetargetLockRemaining <= 0f && FindAlternative(enemy, target, out EnemyUnit reachable,
                        out int reachableSlot, out Vector3 reachableWaypoint, out float reachableLength))
                {
                    Reserve(enemy, reachable, reachableSlot, reachableWaypoint, reachableLength);
                    enemy.RetargetLockRemaining = RetargetLockDuration;
                    continue;
                }
            }

            int slot = enemy.AttackSlot;
            if (slot >= 0 && TryApproachSlot(enemy, target, slot, true, out Vector3 waypoint, out float length,
                    out bool local, true))
            {
                Reserve(enemy, target, slot, waypoint, length, local);
                continue;
            }

            enemy.AttackSlot = -1;
            if (FindSlot(enemy, target, out slot, out waypoint, out length, out local, true))
            {
                Reserve(enemy, target, slot, waypoint, length, local);
                continue;
            }

            // A full target or an unreachable slot sends the unit to another visible opponent
            // with a complete approach. A mere sidestep toward someone else is no reason to
            // switch, and a unit that just switched does not bounce straight back.
            if (enemy.RetargetLockRemaining <= 0f &&
                FindAlternative(enemy, target, out EnemyUnit alternative, out slot, out waypoint, out length))
            {
                Reserve(enemy, alternative, slot, waypoint, length);
                enemy.RetargetLockRemaining = RetargetLockDuration;
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
            if (IsKnownUnreachable(enemy, target))
            {
                enemy.CombatQueue = null; RememberTarget(enemy, null);
                continue;
            }
            if (enemy.IdleQueueTime >= IdleQueueLimit)
            {
                enemy.CombatQueue = null; RememberTarget(enemy, null);
                enemy.IdleQueueTime = 0f;
                enemy.DisengageRemaining = DisengageDuration;
                continue;
            }
            if (queueTarget != null)
                QueueBehindFight(enemy, queueTarget);
            else
                QueueBehindFight(enemy, target);
        }
    }

    // Called with simulation delta before planning, so replanning/slot changes cannot
    // erase the time already spent stuck. Cooldown while in striking position is not a stall.
    public void TrackApproachProgress(EnemyUnit enemy, float deltaTime)
    {
        if (enemy.Target == null && enemy.CombatQueue is { } waitingFor && !foughtUnits.Contains(waitingFor.Target))
            enemy.IdleQueueTime += deltaTime;
        else
            enemy.IdleQueueTime = 0f;
        if (enemy.PursuitTarget is { IsAlive: true } remembered &&
            !grid.HasLineOfSight(enemy.Position, remembered.Position) &&
            Vector3.Distance(enemy.Position, enemy.LastKnownTargetPosition) <= enemy.Faction.AttackRange &&
            grid.HasLineOfSight(enemy.Position, enemy.LastKnownTargetPosition))
            enemy.LostTargetSearchTime += deltaTime;
        EnemyUnit target = enemy.Target ?? enemy.CombatQueue?.Target;
        if (target == null || !target.IsAlive || target != enemy.CombatProgressTarget || CanAttack(enemy, target))
            ResetApproachProgress(enemy, target);
        if (target == null || !target.IsAlive || CanAttack(enemy, target))
            return;

        float remaining = enemy.CombatDestination is { } destination && enemy.Target != null
            ? Vector3.Distance(enemy.Position, destination) + enemy.CombatApproachTailLength
            : Vector3.Distance(enemy.Position, enemy.LastKnownTargetPosition);
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

    // Another opponent is worth switching to only if a complete approach to it exists.
    private bool FindAlternative(EnemyUnit enemy, EnemyUnit current,
        out EnemyUnit alternative, out int slot, out Vector3 waypoint, out float length)
    {
        alternative = null;
        slot = -1;
        waypoint = default;
        length = float.PositiveInfinity;
        // A taunted unit does not walk off to another opponent. It waits for room at the
        // taunter and meanwhile strikes only what is already in reach (FindImmediateTarget).
        if (current != null && enemy.ActiveTaunter == current)
            return false;
        foreach (EnemyUnit other in enemies)
        {
            if (other == current || !CanDetect(enemy, other) ||
                !FindSlot(enemy, other, out int candidateSlot, out Vector3 candidateWaypoint, out float candidateLength,
                    out _, false) ||
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
        // A fighter that left its route may stand far ahead of its old route index, so every
        // forward waypoint is a candidate for it. A unit queueing on the route only looks a few ahead.
        if (!enemy.Position.Equals(enemy.RoutePosition))
            waypointCount = enemy.RemainingRouteWaypoints;
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

    // A fighter left standing off its route can sit in the mouth of a lane while the column
    // behind it stops at exactly the body spacing, each waiting for the other. It gives way.
    public bool TryStepAside(EnemyUnit enemy, out Vector3 destination)
    {
        destination = default;
        EnemyUnit nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (EnemyUnit other in enemies)
        {
            float distance = (other.Position - enemy.Position).sqrMagnitude;
            if (other == enemy || !IsNearer(other, distance, nearest, nearestDistance))
                continue;
            nearest = other;
            nearestDistance = distance;
        }
        if (nearest == null)
            return false;
        float touching = RequiredSpacing(enemy.Faction, nearest.Faction) + StepAsideMargin;
        if (nearestDistance >= touching * touching)
            return false;

        Vector3 away = (enemy.Position - nearest.Position).normalized;
        float best = 0f;
        foreach (Vector3 direction in Directions)
        {
            float alignment = Vector3.Dot(direction, away);
            Vector3 point = enemy.Position + direction * StepAsideDistance;
            if (alignment <= best || !CanMove(enemy, null, enemy.Position, point))
                continue;
            best = alignment;
            destination = point;
        }
        return best > 0f;
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

        // 자리 없이 기다리는 유닛은 대기선까지 곧장 걷기만 한다. 막히면 그 자리에 서고, 옆걸음이나
        // 우회로 비집고 들어가지 않는다.
        if (enemy.Target == null)
        {
            Vector3 closer = enemy.StepToward(destination, deltaTime);
            if (CanMove(enemy, target, enemy.Position, closer))
                enemy.MoveOffRoute(closer);
            return;
        }

        if (TryUsePath(enemy, target, out Vector3 pathWaypoint, out float pathLength))
        {
            destination = pathWaypoint;
            enemy.CombatDestination = pathWaypoint;
            enemy.CombatApproachTailLength = Mathf.Max(0f, pathLength - Vector3.Distance(enemy.Position, pathWaypoint));
        }

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
            BeginSidestep(enemy, detour);
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
        // Taunt overrides awareness, walls, retaliation and a fight already under way.
        // Only a taunter proven unreachable from here falls back to the usual choice. 도발도 우회 한도를
        // 따른다: 벽 너머의 도발 유닛에게 가는 길이 MaxPursuitDetour 넘게 돌아야 하면 끌려가지 않는다.
        if (enemy.ActiveTaunter is { } taunter && !IsKnownUnreachable(enemy, taunter))
        {
            enemy.ConsumeRetaliationRequest();
            return taunter;
        }

        EnemyUnit target = enemy.Target ?? enemy.CombatQueue?.Target ?? enemy.PursuitTarget;
        EnemyUnit aggressor = enemy.ConsumeRetaliationRequest();
        if (aggressor != null && CanDetect(enemy, aggressor) &&
            (target == null || !CanAttackFromHere(enemy, target)))
            return aggressor;
        if (target is { IsAlive: true } && target.FactionIndex != enemy.FactionIndex && !IsKnownUnreachable(enemy, target) &&
            (grid.HasLineOfSight(enemy.Position, target.Position) || enemy.LostTargetSearchTime < LostTargetSearchDuration))
            return target;

        // 싸움 없는 대기를 막 그만둔 유닛은 잠시 지금 자리에서 때릴 수 있는 적만 상대한다.
        bool reachOnly = enemy.DisengageRemaining > 0f;
        EnemyUnit nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (EnemyUnit other in enemies)
        {
            float distance = (other.Position - enemy.Position).sqrMagnitude;
            if (!IsNearer(other, distance, nearest, nearestDistance) || !CanDetect(enemy, other) ||
                (reachOnly && !CanAttackFromHere(enemy, other)))
                continue;

            nearest = other;
            nearestDistance = distance;
        }
        return nearest;
    }

    private void RememberTarget(EnemyUnit enemy, EnemyUnit target)
    {
        if (enemy.PursuitTarget != target)
        {
            enemy.LostTargetSearchTime = 0f;
            if (enemy.CombatPathTarget != target) enemy.CombatPath.Clear();
            enemy.PathRetryRemaining = 0f;
            if (target != null) enemy.LastKnownTargetPosition = target.Position;
        }
        enemy.PursuitTarget = target;
        // A taunted unit always knows where its taunter is, even behind walls.
        if (target != null && (grid.HasLineOfSight(enemy.Position, target.Position) || enemy.ActiveTaunter == target))
        {
            enemy.LastKnownTargetPosition = target.Position;
            enemy.LostTargetSearchTime = 0f;
        }
    }

    private EnemyUnit FindImmediateTarget(EnemyUnit enemy, EnemyUnit preferred)
    {
        // While the taunter has room, a taunted unit leaves whatever it was fighting.
        if (enemy.ActiveTaunter is { } taunter && preferred == taunter && !IsTargetFull(taunter))
            return CanAttackFromHere(enemy, taunter) ? taunter : null;
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
        return other.IsAlive && other.FactionIndex != enemy.FactionIndex && !IsKnownUnreachable(enemy, other) &&
            ((enemy.RetaliationTarget == other && enemy.RetaliationRemaining > 0f) ||
                (other.Position - enemy.Position).sqrMagnitude <= range * range) &&
            grid.HasLineOfSight(enemy.Position, other.Position);
    }

    private void FindNearbyFights()
    {
        activeFighters.Clear();
        alertedUnits.Clear();
        // Snapshot established fights before assigning this plan. Merely acquiring a
        // distant target must not relay awareness indefinitely through the whole army.
        foughtUnits.Clear();
        foreach (EnemyUnit enemy in enemies)
            if (enemy.IsAlive && enemy.Target is { IsAlive: true } target && CanAttackFromHere(enemy, target))
            {
                activeFighters.Add(enemy);
                foughtUnits.Add(enemy);
                foughtUnits.Add(target);
            }
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

        float firstDistance = (first.LastKnownTargetPosition - first.Position).sqrMagnitude;
        float secondDistance = (second.LastKnownTargetPosition - second.Position).sqrMagnitude;
        int distance = firstDistance.CompareTo(secondDistance);
        return distance != 0 ? distance : first.SpawnOrder.CompareTo(second.SpawnOrder);
    }

    private void Reserve(EnemyUnit enemy, EnemyUnit target, int slot, Vector3 waypoint, float length = 0f,
        bool local = false)
    {
        RememberTarget(enemy, target);
        if (slot == CurrentPositionSlot || (enemy.CombatPath.Count > 0 &&
            !enemy.CombatPath[enemy.CombatPathIndex].Equals(waypoint))) enemy.CombatPath.Clear();
        if (enemy.CombatProgressTarget != target || slot == CurrentPositionSlot)
            ResetApproachProgress(enemy, target);
        if (enemy.Target != target || slot == CurrentPositionSlot)
            enemy.CombatSteerWaypoint = null;
        if (local)
            BeginSidestep(enemy, waypoint);
        enemy.Target = target;
        enemy.CombatQueue = null;
        enemy.AttackSlot = slot;
        enemy.CombatDestination = waypoint;
        enemy.CombatApproachTailLength = Mathf.Max(0f, length - Vector3.Distance(enemy.Position, waypoint));
        enemy.RouteYieldDestination = null;
        enemy.RouteBypassOffset = -1;
        enemy.BlockedRouteTime = 0f;
        reservedSlots.TryGetValue(target, out var reservation);
        int mask = slot < 0 ? reservation.Mask : reservation.Mask | (1 << slot);
        reservedSlots[target] = (mask, reservation.Count + 1);
        Vector3 standing = slot >= 0 ? SlotPosition(enemy, target, slot) :
            slot == OpenPositionSlot && enemy.CombatPath.Count > 0 ? enemy.CombatPath[^1] : waypoint;
        reservedPositions.Add((standing, enemy.Faction.BodyRadius));
    }

    private void QueueBehindFight(EnemyUnit enemy, EnemyUnit target)
    {
        RememberTarget(enemy, target);
        enemy.CombatSteerWaypoint = null;
        if (enemy.CombatQueue?.Target != target)
            enemy.CombatQueue = (target, nextQueueOrder++);
        enemy.RouteYieldDestination = null;
        enemy.RouteBypassOffset = -1;
        enemy.BlockedRouteTime = 0f;

        // 싸우는 줄 바로 뒤(공격 자리 반경 + 자기 몸통 간격)까지는 걸어가서 기다린다. 멀리서 적을
        // 정한 채 그 자리에 서 있지 않는다. 다가가기만 하고 물러나지는 않는다: 자리를 내주려고
        // 물러났다가 자리가 나면 다시 들어오면 싸움 뒤에서 왔다 갔다 하게 된다.
        Vector3 offset = enemy.Position - target.Position;
        float distance = offset.magnitude;
        float ring = SlotRadius(enemy, target) + RequiredSpacing(enemy.Faction, enemy.Faction);
        if (distance > ring + WaitingRingSlack && grid.HasLineOfSight(enemy.Position, target.Position))
            enemy.CombatDestination = target.Position + offset / distance * ring;
    }

    private bool FindSlot(EnemyUnit enemy, EnemyUnit target, out int slot, out Vector3 waypoint,
        out float shortest, out bool local, bool allowLocalSteer)
    {
        slot = -1;
        waypoint = default;
        shortest = float.PositiveInfinity;
        local = false;
        if (IsTargetFull(target))
            return false;

        for (int i = 0; i < SlotCount; i++)
        {
            if (!TryApproachSlot(enemy, target, i, false, out Vector3 next, out float length, out _, false) || length >= shortest)
                continue;
            slot = i;
            waypoint = next;
            shortest = length;
        }
        if (slot >= 0)
            return true;

        for (int i = 0; i < SlotCount; i++)
        {
            if (!TryApproachSlot(enemy, target, i, true, out Vector3 next, out float length, out _, false) || length >= shortest)
                continue;
            slot = i;
            waypoint = next;
            shortest = length;
        }
        if (slot >= 0) return true;
        if (FindPathSlot(enemy, target, out slot, out waypoint, out shortest)) return true;
        if (!allowLocalSteer) return false;
        for (int i = 0; i < SlotCount; i++)
        {
            if (!TryApproachSlot(enemy, target, i, true, out Vector3 next, out float length, out _, true) || length >= shortest)
                continue;
            slot = i; waypoint = next; shortest = length;
        }
        local = slot >= 0;
        return slot >= 0;
    }

    private bool IsTargetFull(EnemyUnit target) =>
        reservedSlots.TryGetValue(target, out var reservation) && reservation.Count >= MaxAttackersPerTarget;

    private bool SlotAvailable(EnemyUnit enemy, EnemyUnit target, int slot)
    {
        if (slot < 0 || RequiredSpacing(enemy.Faction, target.Faction) > enemy.Faction.AttackRange) return false;
        Vector3 point = SlotPosition(enemy, target, slot);
        if (!grid.IsWalkable(grid.WorldToCell(point)) || !grid.HasLineOfSight(point, target.Position) ||
            !ClearOfUnits(enemy, target, point, target.Position, attacking: true)) return false;
        if (reservedSlots.TryGetValue(target, out var reservation) &&
            (reservation.Count >= MaxAttackersPerTarget || (reservation.Mask & (1 << slot)) != 0)) return false;
        foreach (var reserved in reservedPositions)
        {
            float spacing = enemy.Faction.BodyRadius + reserved.Radius + BodyGap;
            if ((reserved.Position - point).sqrMagnitude < spacing * spacing) return false;
        }
        return true;
    }

    // A spot to stand and strike from: in reach with a clear line, and clear of every
    // body and of the spots other attackers are already heading for.
    private bool CanStrikeFrom(EnemyUnit enemy, EnemyUnit target, Vector3 point)
    {
        float range = enemy.Faction.AttackRange - StrikeRangeMargin;
        if ((point - target.Position).sqrMagnitude > range * range || !grid.HasLineOfSight(point, target.Position) ||
            !ClearOfUnits(enemy, null, point, point, attacking: false) ||
            !ClearOfUnits(enemy, target, point, target.Position, attacking: true)) return false;
        foreach (var reserved in reservedPositions)
        {
            float spacing = enemy.Faction.BodyRadius + reserved.Radius + BodyGap;
            if ((reserved.Position - point).sqrMagnitude < spacing * spacing) return false;
        }
        return true;
    }

    private bool FindPathSlot(EnemyUnit enemy, EnemyUnit target, out int slot, out Vector3 waypoint, out float length,
        bool allowOpenPosition = false)
    {
        slot = -1; waypoint = default; length = float.PositiveInfinity;
        if (enemy.PathRetryRemaining > 0f || IsTargetFull(target)) return false;
        pathGoals.Clear(); pathSlots.Clear();
        for (int i = 0; i < SlotCount; i++)
            if (SlotAvailable(enemy, target, i)) { pathGoals.Add(SlotPosition(enemy, target, i)); pathSlots.Add(i); }
        if (pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out int goal, maxDetour: MaxPursuitDetour))
            slot = pathSlots[goal];
        else
        {
            // Walls can shut all eight slots around an opponent in plain view, leaving both
            // sides waiting for good. A unit that has stopped getting closer takes any
            // reachable spot with a clear strike.
            pathGoals.Clear(); pathGoals.Add(target.Position);
            if (!allowOpenPosition ||
                !pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out _, strikeTarget: target, maxDetour: MaxPursuitDetour))
            {
                enemy.PathRetryRemaining = StalledRetargetDelay;
                CheckStaticReachability(enemy, target, hidden: false);
                return false;
            }
            slot = OpenPositionSlot;
        }
        enemy.CombatPathIndex = 0;
        enemy.CombatPathTarget = target;
        enemy.CombatPathAnchor = target.Position;
        enemy.CombatPathSearch = false;
        enemy.CombatSteerWaypoint = null;
        ResetApproachProgress(enemy, target);
        return TryUsePath(enemy, target, out waypoint, out length);
    }

    private bool TryUsePath(EnemyUnit enemy, EnemyUnit target, out Vector3 waypoint, out float length)
    {
        waypoint = default; length = 0f;
        if (enemy.CombatPathTarget != target || enemy.CombatPath.Count == 0) return false;
        Vector3 anchor = target != null ? enemy.LastKnownTargetPosition : enemy.CombatPathAnchor;
        if (target != null && enemy.PursuitTarget != target) anchor = target.Position;
        if ((anchor - enemy.CombatPathAnchor).sqrMagnitude > 0.1225f) return false;
        while (enemy.CombatPathIndex < enemy.CombatPath.Count - 1 &&
            enemy.Position.Equals(enemy.CombatPath[enemy.CombatPathIndex])) enemy.CombatPathIndex++;
        // Shorten only through currently clear segments; keep the chosen side of obstacles.
        for (int i = enemy.CombatPath.Count - 1; i > enemy.CombatPathIndex; i--)
            if (CanTraversePath(enemy, enemy.Position, enemy.CombatPath[i])) { enemy.CombatPathIndex = i; break; }
        waypoint = enemy.CombatPath[enemy.CombatPathIndex];
        if (!CanTraversePath(enemy, enemy.Position, waypoint)) return false;
        length = Vector3.Distance(enemy.Position, waypoint);
        for (int i = enemy.CombatPathIndex + 1; i < enemy.CombatPath.Count; i++)
            length += Vector3.Distance(enemy.CombatPath[i - 1], enemy.CombatPath[i]);
        return true;
    }

    private bool CanTraversePath(EnemyUnit enemy, Vector3 from, Vector3 to) =>
        grid.IsWalkable(grid.WorldToCell(to)) && grid.CanPursue(from, to) &&
        ClearOfUnits(enemy, null, from, to, attacking: false);

    private void PursueLastKnownPosition(EnemyUnit enemy, EnemyUnit target)
    {
        enemy.CombatQueue = null;
        enemy.AttackSlot = -1;
        if (!enemy.CombatPathSearch)
            enemy.CombatPath.Clear();
        if (TryUsePath(enemy, target, out Vector3 waypoint, out float length))
        {
            enemy.CombatDestination = waypoint;
            enemy.CombatApproachTailLength = Mathf.Max(0f, length - Vector3.Distance(enemy.Position, waypoint));
            return;
        }
        Vector3 goal = enemy.LastKnownTargetPosition;
        if (CanTraversePath(enemy, enemy.Position, goal))
        {
            enemy.CombatPath.Clear(); enemy.CombatDestination = goal; enemy.CombatApproachTailLength = 0f;
            return;
        }
        if (enemy.PathRetryRemaining > 0f) return;
        pathGoals.Clear(); pathGoals.Add(goal);
        // The remembered location may still be occupied by the opponent. Reach a
        // nearby viewing position instead of requiring movement into its body.
        foreach (Vector3 direction in Directions)
        {
            Vector3 point = goal + direction * (enemy.Faction.AttackRange * 0.85f);
            if (grid.IsWalkable(grid.WorldToCell(point)) && grid.HasLineOfSight(point, goal))
                pathGoals.Add(point);
        }
        if (pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out _, maxDetour: MaxPursuitDetour))
        {
            enemy.CombatPathIndex = 0; enemy.CombatPathTarget = target; enemy.CombatPathAnchor = goal;
            enemy.CombatPathSearch = true;
            enemy.CombatSteerWaypoint = null;
            ResetApproachProgress(enemy, target);
            if (TryUsePath(enemy, target, out waypoint, out length))
            {
                enemy.CombatDestination = waypoint;
                enemy.CombatApproachTailLength = Mathf.Max(0f, length - Vector3.Distance(enemy.Position, waypoint));
            }
        }
        else
        {
            enemy.PathRetryRemaining = StalledRetargetDelay;
            CheckStaticReachability(enemy, target, hidden: true);
        }
    }

    private bool IsKnownUnreachable(EnemyUnit enemy, EnemyUnit target) =>
        enemy.UnreachableTarget == target && enemy.UnreachableNavigationVersion == grid.NavigationVersion &&
        (enemy.Position - enemy.UnreachableFromPosition).sqrMagnitude < 0.25f &&
        (!grid.HasLineOfSight(enemy.Position, target.Position) ||
            (target.Position - enemy.UnreachableTargetPosition).sqrMagnitude < 0.25f);

    private void CheckStaticReachability(EnemyUnit enemy, EnemyUnit target, bool hidden)
    {
        if (enemy.PursuitTarget != target) return;
        pathGoals.Clear();
        if (hidden) pathGoals.Add(enemy.LastKnownTargetPosition);
        else if (RequiredSpacing(enemy.Faction, target.Faction) <= enemy.Faction.AttackRange)
            for (int slot = 0; slot < SlotCount; slot++)
            {
                Vector3 point = SlotPosition(enemy, target, slot);
                if (grid.IsWalkable(grid.WorldToCell(point)) && grid.HasLineOfSight(point, target.Position))
                    pathGoals.Add(point);
            }
        // A temporary crowd is not an unreachable target. Only reject if even a route
        // ignoring every unit fails or is a long way round, or no valid striking position exists.
        if (pathfinder.Find(enemy, pathGoals, staticPath, out _, ignoreUnits: true, maxDetour: MaxPursuitDetour)) return;
        enemy.UnreachableTarget = target;
        enemy.UnreachableTargetPosition = enemy.LastKnownTargetPosition;
        enemy.UnreachableFromPosition = enemy.Position;
        enemy.UnreachableNavigationVersion = grid.NavigationVersion;
    }

    public bool IsReturningAlongPath(EnemyUnit enemy) =>
        enemy.CombatPathTarget == null && enemy.CombatPath.Count > 0;

    // firstOffset: 이 번호부터의 경로 점만 목표로 삼는다(지나온 쪽으로 되돌아가지 않게).
    // shortOnly: 유닛을 돌아가는 짧은 길(MaxPursuitDetour 이내)만 받아들인다.
    // continueOnly: 이미 따라가던 복귀 경로만 이어 가고 새로 찾지 않는다.
    public bool TryReturnAlongPath(EnemyUnit enemy, float deltaTime, int firstOffset = 0, bool shortOnly = false,
        bool continueOnly = false)
    {
        if (enemy.CombatPathTarget != null) enemy.CombatPath.Clear();
        if (!TryUsePath(enemy, null, out Vector3 waypoint, out _))
        {
            if (continueOnly)
            {
                enemy.CombatPath.Clear();
                return false;
            }
            if (enemy.PathRetryRemaining > 0f) return false;
            pathGoals.Clear();
            if (enemy.RemainingRouteWaypoints == 1) pathGoals.Add(enemy.RoutePosition);
            else
                for (int i = firstOffset; i < enemy.RemainingRouteWaypoints - 1; i++) pathGoals.Add(enemy.GetRouteWaypoint(i));
            if (!pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out int offset,
                    maxDetour: shortOnly ? MaxPursuitDetour : float.PositiveInfinity))
            {
                enemy.PathRetryRemaining = StalledRetargetDelay;
                return false;
            }
            enemy.CombatPathTarget = null; enemy.CombatPathIndex = 0;
            enemy.ReturnRouteOffset = enemy.RemainingRouteWaypoints == 1 ? -1 : offset + firstOffset;
            if (!TryUsePath(enemy, null, out waypoint, out _)) return false;
        }
        Vector3 next = enemy.StepToward(waypoint, deltaTime);
        if (!CanTraversePath(enemy, enemy.Position, next)) return false;
        enemy.Face(next);
        if (next.Equals(enemy.CombatPath[^1]))
        {
            enemy.FollowRoute(next, Mathf.Max(0, enemy.ReturnRouteOffset));
            enemy.CombatPath.Clear(); enemy.RouteBypassOffset = -1;
        }
        else enemy.MoveOffRoute(next);
        enemy.BlockedRouteTime = 0f;
        return true;
    }

    private bool TryApproachSlot(EnemyUnit enemy, EnemyUnit target, int slot, bool allowDetour,
        out Vector3 waypoint, out float length, out bool local, bool allowLocalSteer)
    {
        waypoint = SlotPosition(enemy, target, slot);
        length = float.PositiveInfinity;
        local = false;
        if (!SlotAvailable(enemy, target, slot)) return false;

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
        if (allowLocalSteer && grid.CanPursue(enemy.Position, goal) &&
            (TryKeepSidestep(enemy, target, out Vector3 sidestep) || TryLocalSteer(enemy, target, goal, out sidestep)))
        {
            waypoint = sidestep;
            length = Vector3.Distance(enemy.Position, sidestep) + Vector3.Distance(sidestep, goal);
            local = true;
            return true;
        }
        return false;
    }

    // Finish a sidestep already under way instead of choosing a new side on every plan.
    private bool TryKeepSidestep(EnemyUnit enemy, EnemyUnit target, out Vector3 sidestep)
    {
        sidestep = enemy.CombatSteerWaypoint ?? default;
        return enemy.CombatSteerWaypoint.HasValue && !enemy.Position.Equals(sidestep) &&
            CanMove(enemy, target, enemy.Position, sidestep);
    }

    private static void BeginSidestep(EnemyUnit enemy, Vector3 sidestep)
    {
        if (enemy.CombatSteerWaypoint is { } current && current.Equals(sidestep))
            return;
        enemy.CombatSteerWaypoint = sidestep;
        enemy.SidestepDirection = (sidestep - enemy.Position).normalized;
        enemy.SidestepMemoryRemaining = SidestepMemoryDuration;
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
            // Stepping straight back to where the last sidestep started is the shuffle
            // seen in corridors; hold position instead until the memory fades or a unit dies.
            if (enemy.SidestepMemoryRemaining > 0f && Vector3.Dot(direction, enemy.SidestepDirection) < -0.5f)
                continue;
            Vector3 point = enemy.Position + direction * Mathf.Clamp(enemy.Faction.BodyRadius * 3f, 0.4f, 0.9f);
            float cost = Vector3.Distance(point, goal);
            // 비켜선 자리에서 목표까지 길이 열려 있을 때만 옆걸음을 뗀다. 열리지 않는 옆걸음을 이어 가면
            // 싸움터 둘레를 옆으로, 뒤로 맴돈다. 그럴 때는 제자리에서 기다린다.
            if (cost >= shortest || !CanMove(enemy, target, enemy.Position, point) ||
                !CanMove(enemy, target, point, goal))
                continue;
            shortest = cost;
            waypoint = point;
        }
        return !float.IsPositiveInfinity(shortest);
    }

    private static Vector3 SlotPosition(EnemyUnit enemy, EnemyUnit target, int slot) =>
        target.Position + Directions[slot] * SlotRadius(enemy, target);

    private static float SlotRadius(EnemyUnit enemy, EnemyUnit target) =>
        Mathf.Min(enemy.Faction.AttackRange,
            Mathf.Max(enemy.Faction.AttackRange * 0.85f, RequiredSpacing(enemy.Faction, target.Faction) + 0.02f));

    private bool CanMove(EnemyUnit enemy, EnemyUnit target, Vector3 from, Vector3 to) =>
        grid.IsWalkable(grid.WorldToCell(from)) &&
        grid.IsWalkable(grid.WorldToCell(to)) &&
        ClearOfUnits(enemy, target, from, to, attacking: false) &&
        grid.CanPursue(from, to) &&
        (target != null || HasRouteConnection(enemy, to));

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
    // A bounded board graph at half-tile spacing. Static wall edges are cached; unit
    // clearance is checked against the current snapshot. No scene navigation state.
    private sealed class PursuitPathfinder
    {
        private static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] Dz = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private readonly CombatPositioning owner;
        private readonly int width, depth;
        private readonly Vector3[] points;
        private readonly byte[] edges;
        private readonly float[] costs;
        private readonly int[] parents;
        private readonly bool[] closed;
        private readonly List<(int Node, float Score)> open = new();
        private int version = -1;
        private bool ignoreUnits;

        // 칸 절반 간격. 칸 한 변이 바뀌어도 칸당 노드 수는 같다.
        private float Spacing => owner.grid.CellSize * 0.5f;

        public PursuitPathfinder(CombatPositioning owner)
        {
            this.owner = owner;
            width = owner.grid.Width * 2 - 1;
            depth = owner.grid.Depth * 2 - 1;
            int count = width * depth;
            points = new Vector3[count]; edges = new byte[count]; costs = new float[count];
            parents = new int[count]; closed = new bool[count];
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++) points[z * width + x] = new Vector3(x, 0, z) * Spacing;
        }

        private void RefreshEdges()
        {
            if (version == owner.grid.NavigationVersion) return;
            version = owner.grid.NavigationVersion;
            for (int i = 0; i < points.Length; i++)
            {
                edges[i] = 0;
                if (!owner.grid.IsWalkable(owner.grid.WorldToCell(points[i]))) continue;
                for (int d = 0; d < 8; d++)
                {
                    int x = i % width + Dx[d], z = i / width + Dz[d];
                    if (x < 0 || z < 0 || x >= width || z >= depth) continue;
                    int next = z * width + x;
                    if (owner.grid.IsWalkable(owner.grid.WorldToCell(points[next])) &&
                        owner.grid.CanPursue(points[i], points[next])) edges[i] |= (byte)(1 << d);
                }
            }
        }

        // With strikeTarget, the search ends at the first node the unit can strike it from.
        // maxDetour: 경로 길이가 그 목표까지의 직선 거리보다 이만큼 넘게 길면 찾지 못한 것으로 본다.
        public bool Find(EnemyUnit enemy, IReadOnlyList<Vector3> goals, List<Vector3> result, out int goalIndex,
            bool ignoreUnits = false, EnemyUnit strikeTarget = null, float maxDetour = float.PositiveInfinity)
        {
            this.ignoreUnits = ignoreUnits;
            goalIndex = -1;
            if (goals.Count == 0) return false;
            RefreshEdges();
            open.Clear();
            for (int i = 0; i < costs.Length; i++) { costs[i] = float.PositiveInfinity; parents[i] = -1; closed[i] = false; }
            int sx = Mathf.RoundToInt(enemy.Position.x / Spacing), sz = Mathf.RoundToInt(enemy.Position.z / Spacing);
            for (int z = Mathf.Max(0, sz - 1); z <= Mathf.Min(depth - 1, sz + 1); z++)
                for (int x = Mathf.Max(0, sx - 1); x <= Mathf.Min(width - 1, sx + 1); x++)
                {
                    int i = z * width + x;
                    if (edges[i] == 0 || !Clear(enemy, enemy.Position, points[i])) continue;
                    costs[i] = Vector3.Distance(enemy.Position, points[i]);
                    Push(i, costs[i] + Heuristic(points[i], goals));
                }
            int end = -1;
            float best = float.PositiveInfinity;
            // 남은 직선 거리를 더한 값이 가장 먼 목표의 한도를 넘는 지점부터는 어떤 목표에도 한도 안에 닿지 못한다.
            float limit = float.PositiveInfinity;
            if (!float.IsPositiveInfinity(maxDetour))
            {
                limit = 0f;
                foreach (Vector3 goal in goals)
                    limit = Mathf.Max(limit, Vector3.Distance(enemy.Position, goal) + maxDetour);
            }
            while (open.Count > 0)
            {
                var entry = Pop();
                if (entry.Score >= best || entry.Score > limit) break;
                int i = entry.Node;
                if (closed[i]) continue;
                closed[i] = true;
                if (strikeTarget != null && owner.CanStrikeFrom(enemy, strikeTarget, points[i]))
                {
                    end = i; goalIndex = 0;
                    break;
                }
                for (int g = 0; strikeTarget == null && g < goals.Count; g++)
                {
                    float distance = Vector3.Distance(points[i], goals[g]);
                    if (distance > Spacing * 2f || costs[i] + distance >= best ||
                        costs[i] + distance > Vector3.Distance(enemy.Position, goals[g]) + maxDetour ||
                        !Clear(enemy, points[i], goals[g])) continue;
                    best = costs[i] + distance; end = i; goalIndex = g;
                }
                for (int d = 0; d < 8; d++)
                {
                    if ((edges[i] & (1 << d)) == 0) continue;
                    int next = i + Dz[d] * width + Dx[d];
                    float cost = costs[i] + Spacing * (d % 2 == 0 ? 1f : 1.41421356f);
                    if (closed[next] || cost >= costs[next] ||
                        (!ignoreUnits && !owner.ClearOfUnits(enemy, null, points[i], points[next], attacking: false))) continue;
                    costs[next] = cost; parents[next] = i;
                    Push(next, cost + Heuristic(points[next], goals));
                }
            }
            if (end < 0) return false;
            result.Clear();
            if (strikeTarget == null) result.Add(goals[goalIndex]);
            for (int i = end; i >= 0; i = parents[i]) result.Add(points[i]);
            result.Reverse();
            return true;
        }

        private bool Clear(EnemyUnit enemy, Vector3 from, Vector3 to) =>
            owner.grid.IsWalkable(owner.grid.WorldToCell(to)) && owner.grid.CanPursue(from, to) &&
            (ignoreUnits || owner.ClearOfUnits(enemy, null, from, to, attacking: false));

        private static float Heuristic(Vector3 point, IReadOnlyList<Vector3> goals)
        {
            float distance = float.PositiveInfinity;
            foreach (Vector3 goal in goals) distance = Mathf.Min(distance, Vector3.Distance(point, goal));
            return distance;
        }

        private static bool Before((int Node, float Score) a, (int Node, float Score) b) =>
            a.Score < b.Score || (a.Score == b.Score && a.Node < b.Node);

        private void Push(int node, float score)
        {
            var value = (Node: node, Score: score);
            int i = open.Count; open.Add(value);
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Before(value, open[parent])) break;
                open[i] = open[parent]; i = parent;
            }
            open[i] = value;
        }

        private (int Node, float Score) Pop()
        {
            var result = open[0]; var value = open[^1]; open.RemoveAt(open.Count - 1);
            if (open.Count == 0) return result;
            int i = 0;
            while (i * 2 + 1 < open.Count)
            {
                int child = i * 2 + 1;
                if (child + 1 < open.Count && Before(open[child + 1], open[child])) child++;
                if (!Before(open[child], value)) break;
                open[i] = open[child]; i = child;
            }
            open[i] = value;
            return result;
        }
    }

}
