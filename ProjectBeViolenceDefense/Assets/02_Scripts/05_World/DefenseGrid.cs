using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DefenseGrid
{
    public const int DefaultSize = 17;
    public const int WallLimit = 8;
    public const float WallWidth = 0.92f;
    public const float WallHeight = 0.8f;
    // 이동·시야 판정에서 벽 상자를 넓히는 양(상자 전체 폭 기준).
    private const float PursuitWallMargin = 0.24f;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    public enum BuildResult { Placed, Outside, Occupied, LimitReached, BlocksRoute }

    private readonly RectInt coreFootprint;
    private readonly Vector2Int[] spawns;
    private readonly int[] spawnFactions;
    private readonly HashSet<Vector2Int> fixedWalls = new();
    private readonly HashSet<Vector2Int> placed = new();
    private Vector3[][] routes;
    private int[,] coreDistances;
    private int coreDistancesVersion = -1;

    public int Width { get; }
    public int Depth { get; }
    // 전투 좌표에서 칸 한 변의 길이. 유닛의 몸통·사거리·속도는 이 값과 관계없이 그대로라서,
    // 2면 유닛 대비 맵(벽·길·코어)이 2배로 커진다. 맵 크기 실험용이며 기본값은 1.
    public float CellSize { get; }
    public IReadOnlyList<Vector2Int> SpawnCells => spawns;
    // 스폰별 진영 인덱스. Routes[i]는 SpawnCells[i]에서 출발한다.
    public IReadOnlyList<int> SpawnFactions => spawnFactions;
    public IReadOnlyCollection<Vector2Int> FixedWalls => fixedWalls;
    // 코어까지 갈 수 없는 스폰이 있으면 null.
    public IReadOnlyList<Vector3[]> Routes => routes;
    // 경로 계산이 실패한 첫 스폰 인덱스. 성공하면 -1.
    public int UnreachableSpawn { get; private set; } = -1;
    public int WallCount => placed.Count;
    public int NavigationVersion { get; private set; }
    public Vector3 CorePosition => new Vector3(coreFootprint.center.x - 0.5f, 0f, coreFootprint.center.y - 0.5f) * CellSize;
    // 코어가 차지하는 칸 수.
    public Vector2Int CoreSize => coreFootprint.size;
    public Vector3 Center => new Vector3((Width - 1) * 0.5f, 0, (Depth - 1) * 0.5f) * CellSize;

    // 기본 지형: 17x17, 가운데 한 칸만 열린 가로 벽.
    public DefenseGrid(float cellSize = 1f) : this(DefaultSize, DefaultSize, new RectInt(DefaultSize / 2 - 1, DefaultSize - 3, 3, 3),
        new[] { new Vector2Int(2, 2), new Vector2Int(DefaultSize - 3, 2) }, new[] { 0, 1 }, DefaultWalls(), cellSize)
    {
    }

    // 범위 밖·코어·스폰 위에 놓인 벽 칸은 무시한다. 경로가 없으면 Routes가 null이다.
    public DefenseGrid(int width, int depth, RectInt core, IReadOnlyList<Vector2Int> spawnCells,
        IReadOnlyList<int> spawnFactionIndices, IEnumerable<Vector2Int> fixedWallCells, float cellSize = 1f)
    {
        CellSize = cellSize;
        Width = width;
        Depth = depth;
        coreFootprint = core;
        spawns = new Vector2Int[spawnCells.Count];
        spawnFactions = new int[spawnCells.Count];
        for (int i = 0; i < spawns.Length; i++)
        {
            spawns[i] = spawnCells[i];
            spawnFactions[i] = spawnFactionIndices[i];
        }

        if (fixedWallCells != null)
            foreach (Vector2Int cell in fixedWallCells)
                if (InBounds(cell) && !coreFootprint.Contains(cell) && Array.IndexOf(spawns, cell) < 0)
                    fixedWalls.Add(cell);
        routes = FindRoutes();
    }

    private static IEnumerable<Vector2Int> DefaultWalls()
    {
        const int gateRow = 7;
        for (int x = 0; x < DefaultSize; x++)
            if (x != DefaultSize / 2)
                yield return new Vector2Int(x, gateRow);
    }

    public bool IsWalkable(Vector2Int cell) => InBounds(cell) && !coreFootprint.Contains(cell) && !IsWall(cell);

    public bool IsFixedWall(Vector2Int cell) => fixedWalls.Contains(cell);

    private Vector3 ClosestCorePoint(Vector3 position) => new Vector3(
        Mathf.Clamp(position.x / CellSize, coreFootprint.xMin - 0.5f, coreFootprint.xMax - 0.5f),
        0f,
        Mathf.Clamp(position.z / CellSize, coreFootprint.yMin - 0.5f, coreFootprint.yMax - 0.5f)) * CellSize;

    public BuildResult TryPlace(Vector2Int cell)
    {
        BuildResult result = CheckPlace(cell, out Vector3[][] updated);
        if (result != BuildResult.Placed)
            return result;

        placed.Add(cell);
        NavigationVersion++;
        routes = updated;
        return BuildResult.Placed;
    }

    /// <summary>벽을 놓지 않고 그 칸에 놓을 수 있는지만 본다. 놓을 수 있으면 <see cref="BuildResult.Placed"/>.</summary>
    public BuildResult CheckPlace(Vector2Int cell) => CheckPlace(cell, out _);

    private BuildResult CheckPlace(Vector2Int cell, out Vector3[][] updated)
    {
        updated = null;
        if (!InBounds(cell))
            return BuildResult.Outside;
        if (!IsWalkable(cell) || Array.IndexOf(spawns, cell) >= 0)
            return BuildResult.Occupied;
        if (placed.Count >= WallLimit)
            return BuildResult.LimitReached;

        updated = FindRoutes(cell);
        return updated == null ? BuildResult.BlocksRoute : BuildResult.Placed;
    }

    public bool TryRemove(Vector2Int cell)
    {
        if (!placed.Remove(cell))
            return false;

        routes = FindRoutes();
        NavigationVersion++;
        return true;
    }

    // 코어도 벽처럼 시야와 공격을 막는다. 시야는 이동과 같은 판정을 쓴다. 벽·코어 가장자리에서
    // 같은 여유를 두므로 "보이지만 곧장 걸어갈 수 없는" 위치가 생기지 않는다.
    public bool HasLineOfSight(Vector3 from, Vector3 to) => CanPursue(from, to);

    public bool CanPursue(Vector3 from, Vector3 to)
        => !CrossesCore(from, to) && IsWallFree(from, to, PursuitWallMargin);

    private bool CrossesCore(Vector3 from, Vector3 to)
    {
        Vector3 step = to - from;
        float length = step.magnitude;
        if (length == 0f)
            return false;

        var coreBounds = new Bounds(CorePosition, new Vector3(CoreSize.x * CellSize, 1f, CoreSize.y * CellSize));
        return coreBounds.IntersectRay(new Ray(from, step / length), out float distance) && distance <= length;
    }

    private bool IsWallFree(Vector3 from, Vector3 to, float wallExpansion)
    {
        Vector3 step = to - from;
        float length = step.magnitude;
        if (length == 0)
            return true;

        var ray = new Ray(from + Vector3.up * (WallHeight * 0.5f), step / length);
        foreach (Vector2Int cell in fixedWalls)
        {
            if (IntersectsWall(cell, ray, length, wallExpansion))
                return false;
        }
        foreach (Vector2Int cell in placed)
        {
            if (IntersectsWall(cell, ray, length, wallExpansion))
                return false;
        }
        return true;
    }

    // 아래 정적 함수 셋은 칸 한 변이 1인 칸 좌표(화면 보드 좌표)다. 전투 좌표는 CellToWorld·WorldToCell을 쓴다.
    public static Bounds WallBounds(Vector2Int cell) =>
        new(CellPosition(cell) + Vector3.up * (WallHeight * 0.5f), new Vector3(WallWidth, WallHeight, WallWidth));

    public static Vector3 CellPosition(Vector2Int cell) => new(cell.x, 0, cell.y);

    public static Vector2Int PositionToCell(Vector3 position) =>
        new(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));

    public Vector3 CellToWorld(Vector2Int cell) => CellPosition(cell) * CellSize;

    public Vector2Int WorldToCell(Vector3 position) => PositionToCell(position / CellSize);

    private bool IntersectsWall(Vector2Int cell, Ray ray, float length, float expansion)
    {
        var bounds = new Bounds(CellToWorld(cell) + Vector3.up * (WallHeight * 0.5f),
            new Vector3(WallWidth * CellSize, WallHeight, WallWidth * CellSize));
        bounds.Expand(expansion);
        return bounds.IntersectRay(ray, out float distance) && distance <= length;
    }

    private bool IsWall(Vector2Int cell) => fixedWalls.Contains(cell) || placed.Contains(cell);

    // 지금 위치에서 코어까지 가는 최단 경로. 첫 점은 position 그대로다. 위치가 걸을 수 없는 칸이거나
    // 코어까지 길이 없으면 null.
    public Vector3[] RouteFrom(Vector3 position)
    {
        Vector2Int cell = WorldToCell(position);
        if (!IsWalkable(cell))
            return null;
        if (coreDistancesVersion != NavigationVersion || coreDistances == null)
        {
            coreDistances = FloodFromCore(null);
            coreDistancesVersion = NavigationVersion;
        }
        if (coreDistances[cell.x, cell.y] == 0)
            return null;

        var path = new List<Vector3> { position };
        TraceRoute(cell, coreDistances, path);
        // 자기 칸 중심으로 되돌아가지 않고, 다음 칸이 바로 보이면 그쪽으로 곧장 간다.
        if (path.Count > 2 && CanPursue(position, path[2]))
            path.RemoveAt(1);
        return path.ToArray();
    }

    private Vector3[][] FindRoutes(Vector2Int? candidate = null)
    {
        int[,] distances = FloodFromCore(candidate);
        var result = new Vector3[spawns.Length][];
        for (int i = 0; i < result.Length; i++)
        {
            Vector2Int cell = spawns[i];
            if (!InBounds(cell) || distances[cell.x, cell.y] == 0)
            {
                UnreachableSpawn = i;
                return null;
            }

            var path = new List<Vector3>();
            TraceRoute(cell, distances, path);
            result[i] = path.ToArray();
        }
        UnreachableSpawn = -1;
        return result;
    }

    // 코어 옆 칸이 1, 그다음이 2...인 걸음 수. 갈 수 없는 칸은 0. candidate 칸은 벽으로 본다.
    private int[,] FloodFromCore(Vector2Int? candidate)
    {
        // Flood from all open tiles touching the core's sides.
        var distances = new int[Width, Depth];
        var queue = new Queue<Vector2Int>();
        foreach (Vector2Int coreCell in coreFootprint.allPositionsWithin)
        {
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int approach = coreCell + direction;
                if (!IsWalkable(approach) || approach == candidate || distances[approach.x, approach.y] != 0)
                    continue;

                distances[approach.x, approach.y] = 1;
                queue.Enqueue(approach);
            }
        }

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                if (!IsWalkable(next) || next == candidate || distances[next.x, next.y] != 0)
                    continue;

                distances[next.x, next.y] = distances[cell.x, cell.y] + 1;
                queue.Enqueue(next);
            }
        }
        return distances;
    }

    // cell 중심부터 코어 외곽 점까지 칸 중심을 path에 덧붙인다. cell은 코어에 닿을 수 있어야 한다.
    private void TraceRoute(Vector2Int cell, int[,] distances, List<Vector3> path)
    {
        int distance = distances[cell.x, cell.y];
        // Mirror the tie-break for spawns right of center so equally short paths stay symmetric.
        bool mirrored = cell.x * 2 > Width - 1;

        path.Add(CellToWorld(cell));
        while (distance > 1)
        {
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int step = mirrored ? new Vector2Int(-direction.x, direction.y) : direction;
                Vector2Int next = cell + step;
                if (!InBounds(next) || distances[next.x, next.y] != distance - 1)
                    continue;

                cell = next;
                distance--;
                path.Add(CellToWorld(cell));
                break;
            }
        }
        path.Add(ClosestCorePoint(CellToWorld(cell)));
    }

    public bool InBounds(Vector2Int cell) =>
        cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Depth;
}
