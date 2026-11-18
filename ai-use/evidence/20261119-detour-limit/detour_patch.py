# 추격·접근 우회 한도 패치. apply(text, cells) -> 고친 CombatPositioning.cs 본문
def apply(t, cells):
    def rep(old, new, count=1):
        nonlocal t
        assert t.count(old) >= 1, old[:70]
        t = t.replace(old, new, count)
    rep('''    internal const float HiddenPursuitStallLimit = 2f;
''', f'''    internal const float HiddenPursuitStallLimit = 2f;
    // 적에게 가는 길이 직선 거리보다 이 칸 수 넘게 길면 돌아가지 않는다. 길은 있는데 유닛에 막힌
    // 경우에는 뒤에서 기다리고, 지형 때문에 이만큼 돌아야 하는 적은 그 자리에서는 상대하지 않는다.
    // 첫 테스트용 임시 값.
    internal const float MaxPursuitDetour = {cells}f;
''')
    rep('''    private bool ClearOfUnits(EnemyUnit enemy, EnemyUnit target, Vector3 from, Vector3 to, bool attacking)
    {''', '''    private float PursuitDetourLimit => MaxPursuitDetour * grid.CellSize;

    private bool ClearOfUnits(EnemyUnit enemy, EnemyUnit target, Vector3 from, Vector3 to, bool attacking)
    {''')
    # 탐색 호출부
    rep('if (pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out int goal))\n            slot = pathSlots[goal];',
        'if (pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out int goal, maxDetour: PursuitDetourLimit))\n            slot = pathSlots[goal];')
    rep('!pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out _, strikeTarget: target))',
        '!pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out _, strikeTarget: target, maxDetour: PursuitDetourLimit))')
    rep('''        if (pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out _))
        {
            enemy.CombatPathIndex = 0; enemy.CombatPathTarget = target; enemy.CombatPathAnchor = goal;''',
        '''        if (pathfinder.Find(enemy, pathGoals, enemy.CombatPath, out _, maxDetour: PursuitDetourLimit))
        {
            enemy.CombatPathIndex = 0; enemy.CombatPathTarget = target; enemy.CombatPathAnchor = goal;''')
    rep('''        // A temporary crowd is not an unreachable target. Only reject if even a
        // route ignoring every unit fails, or no valid striking position exists.
        if (pathfinder.Find(enemy, pathGoals, staticPath, out _, ignoreUnits: true)) return;''',
        '''        // A temporary crowd is not an unreachable target. Only reject if even a route
        // ignoring every unit fails or is a long way round, or no valid striking position exists.
        if (pathfinder.Find(enemy, pathGoals, staticPath, out _, ignoreUnits: true, maxDetour: PursuitDetourLimit)) return;''')
    # 탐색기
    rep('''        // With strikeTarget, the search ends at the first node the unit can strike it from.
        public bool Find(EnemyUnit enemy, IReadOnlyList<Vector3> goals, List<Vector3> result, out int goalIndex,
            bool ignoreUnits = false, EnemyUnit strikeTarget = null)
        {''', '''        // With strikeTarget, the search ends at the first node the unit can strike it from.
        // maxDetour: 경로 길이가 그 목표까지의 직선 거리보다 이만큼 넘게 길면 찾지 못한 것으로 본다.
        public bool Find(EnemyUnit enemy, IReadOnlyList<Vector3> goals, List<Vector3> result, out int goalIndex,
            bool ignoreUnits = false, EnemyUnit strikeTarget = null, float maxDetour = float.PositiveInfinity)
        {''')
    rep('''            int end = -1;
            float best = float.PositiveInfinity;
            while (open.Count > 0)
            {
                var entry = Pop();
                if (entry.Score >= best) break;''', '''            int end = -1;
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
                if (entry.Score >= best || entry.Score > limit) break;''')
    rep('''                    if (distance > Spacing * 2f || costs[i] + distance >= best || !Clear(enemy, points[i], goals[g])) continue;''',
        '''                    if (distance > Spacing * 2f || costs[i] + distance >= best ||
                        costs[i] + distance > Vector3.Distance(enemy.Position, goals[g]) + maxDetour ||
                        !Clear(enemy, points[i], goals[g])) continue;''')
    return t
