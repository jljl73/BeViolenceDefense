# 스냅숏(src)에서 실험판 소스를 만든다. 사용: variants.py <이름> <옵션...>   옵션: pass sep sep2 overally reroute over sight
import sys, shutil, os
D=os.path.dirname(os.path.abspath(__file__))
name, opts = sys.argv[1], set(sys.argv[2:])
dst=f'{D}/src-{name}'
shutil.rmtree(dst, ignore_errors=True); shutil.copytree(f'{D}/src', dst)
def patch(path, old, new, count=1):
    p=f'{dst}/{path}'; t=open(p).read(); assert t.count(old)>=1, (path, old[:60]); open(p,'w').write(t.replace(old,new,count))
CP='00_Core/CombatPositioning.cs'; DB='04_Combat/DefenseBattle.cs'; DG='05_World/DefenseGrid.cs'
if 'pass' in opts:
    # 아군은 이동 중에는 서로를 막지 않는다. 서 있을 자리 검사(from == to)와 공격선은 그대로다.
    patch(CP, '''            if (other == enemy || (attacking && other == target))
                continue;
''', '''            if (other == enemy || (attacking && other == target))
                continue;
            if (!attacking && lengthSquared > 0f && other.FactionIndex == enemy.FactionIndex)
                continue; // 실험: 아군 통과
''')
if 'sep' in opts:
    # 서 있는 아군끼리 겹치면 천천히 밀어낸다. 때릴 수 있는 자리에 선 유닛은 버티고 상대가 비킨다.
    patch(CP, '''    public void Move(EnemyUnit enemy, EnemyUnit target, float deltaTime)
    {''', '''    internal const float SeparationSpeed = 1f;
    private readonly List<(EnemyUnit Unit, Vector3 Push)> separation = new();

    public void SeparateRestingAllies(float deltaTime)
    {
        separation.Clear();
        foreach (EnemyUnit unit in enemies)
        {
            if (!unit.Position.Equals(unit.PreviousPosition))
                continue;
            bool anchored = unit.Target != null && CanAttack(unit, unit.Target);
            Vector3 push = Vector3.zero;
            foreach (EnemyUnit other in enemies)
            {
                if (other == unit || other.FactionIndex != unit.FactionIndex || !other.Position.Equals(other.PreviousPosition))
                    continue;
                float spacing = RequiredSpacing(unit.Faction, other.Faction);
                Vector3 offset = unit.Position - other.Position;
                float distance = offset.magnitude;
                if (distance >= spacing)
                    continue;
                if (anchored && !(other.Target != null && CanAttack(other, other.Target)))
                    continue;
                Vector3 direction = distance > 0.0001f ? offset / distance
                    : unit.SpawnOrder < other.SpawnOrder ? Vector3.right : Vector3.left;
                push += direction * (spacing - distance);
            }
            if (push.sqrMagnitude > 0f)
                separation.Add((unit, push));
        }
        foreach ((EnemyUnit unit, Vector3 push) in separation)
        {
            float step = Mathf.Min(push.magnitude, SeparationSpeed * deltaTime);
            Vector3 next = unit.Position + push.normalized * step;
            if (grid.IsWalkable(grid.WorldToCell(next)) && grid.CanPursue(unit.Position, next) &&
                ClearOfUnits(unit, null, unit.Position, next, attacking: false))
                unit.MoveOffRoute(next);
        }
    }

    public void Move(EnemyUnit enemy, EnemyUnit target, float deltaTime)
    {''')
    patch(DB, '''            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyUnit enemy = enemies[i];
                if (!enemy.ReachedCore)
                    continue;
''', '''            combat.SeparateRestingAllies(deltaTime);

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyUnit enemy = enemies[i];
                if (!enemy.ReachedCore)
                    continue;
''')
if 'sep2' in opts:
    # 자리를 배정받지 못하고 서 있는 유닛만 비킨다. 자리를 배정받은 유닛(타깃 있음)은 밀리지 않는다.
    patch(CP, '''    public void Move(EnemyUnit enemy, EnemyUnit target, float deltaTime)
    {''', '''    internal const float SeparationSpeed = 1f;
    private readonly List<(EnemyUnit Unit, Vector3 Push)> separation = new();

    public void SeparateRestingAllies(float deltaTime)
    {
        separation.Clear();
        foreach (EnemyUnit unit in enemies)
        {
            if (unit.Target != null || !unit.Position.Equals(unit.PreviousPosition))
                continue;
            Vector3 push = Vector3.zero;
            foreach (EnemyUnit other in enemies)
            {
                if (other == unit || other.FactionIndex != unit.FactionIndex || !other.Position.Equals(other.PreviousPosition))
                    continue;
                float spacing = RequiredSpacing(unit.Faction, other.Faction);
                Vector3 offset = unit.Position - other.Position;
                float distance = offset.magnitude;
                if (distance >= spacing)
                    continue;
                Vector3 direction = distance > 0.0001f ? offset / distance
                    : unit.SpawnOrder < other.SpawnOrder ? Vector3.right : Vector3.left;
                push += direction * (spacing - distance);
            }
            if (push.sqrMagnitude > 0f)
                separation.Add((unit, push));
        }
        foreach ((EnemyUnit unit, Vector3 push) in separation)
        {
            float step = Mathf.Min(push.magnitude, SeparationSpeed * deltaTime);
            Vector3 next = unit.Position + push.normalized * step;
            if (grid.IsWalkable(grid.WorldToCell(next)) && grid.CanPursue(unit.Position, next) &&
                ClearOfUnits(unit, null, unit.Position, next, attacking: false))
                unit.MoveOffRoute(next);
        }
    }

    public void Move(EnemyUnit enemy, EnemyUnit target, float deltaTime)
    {''')
    patch(DB, '''            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyUnit enemy = enemies[i];
                if (!enemy.ReachedCore)
                    continue;
''', '''            combat.SeparateRestingAllies(deltaTime);

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyUnit enemy = enemies[i];
                if (!enemy.ReachedCore)
                    continue;
''')
if 'overally' in opts:
    # 공격선은 아군 몸통에 막히지 않는다(적 몸통은 그대로 막는다).
    patch(CP, '''            if (other == enemy || (attacking && other == target))
                continue;
''', '''            if (other == enemy || (attacking && other == target))
                continue;
            if (attacking && other.FactionIndex == enemy.FactionIndex)
                continue; // 실험: 아군은 공격선을 막지 않는다
''')
for cap in (4, 6, 8):
    if f'cap{cap}' in opts:
        patch(CP, 'internal const int MaxAttackersPerTarget = 3;', f'internal const int MaxAttackersPerTarget = {cap};')
if 'open' in opts:
    # 여덟 고정 자리를 못 잡으면 누구나 사거리 안의 빈 지점을 자리로 쓴다(기존에는 멈춘 유닛만).
    patch(CP, 'if (FindPathSlot(enemy, target, out slot, out waypoint, out shortest)) return true;',
        'if (FindPathSlot(enemy, target, out slot, out waypoint, out shortest, allowOpenPosition: true)) return true;')
if 'reroute' in opts:
    # 경로를 벗어난 채 타깃이 없으면 원래 경로로 돌아가지 않고 지금 위치에서 코어 경로를 새로 잡는다.
    patch(DB, '''        // Finish a return path already under way. Choosing between it and a direct line to''', '''        if (!enemy.Position.Equals(enemy.RoutePosition) && grid.RouteFrom(enemy.Position) is { } fresh)
            enemy.Reroute(fresh); // 실험: 항상 지금 위치에서

        // Finish a return path already under way. Choosing between it and a direct line to''')
if 'over' in opts:
    patch(CP, '''        Vector3 step = to - from;
        float lengthSquared = step.sqrMagnitude;
        foreach (EnemyUnit other in enemies)''', '''        if (attacking) return true; // 실험: 공격선은 다른 유닛 몸통에 막히지 않는다
        Vector3 step = to - from;
        float lengthSquared = step.sqrMagnitude;
        foreach (EnemyUnit other in enemies)''')
if 'sight' in opts:
    patch(DG, '=> !CrossesCore(from, to, SightInset * CellSize) && IsWallFree(from, to, 0f);', '=> !CrossesCore(from, to, 0f) && IsWallFree(from, to, 0.24f); // 실험: 시야를 이동 판정과 같게')
print('made', dst, sorted(opts))
