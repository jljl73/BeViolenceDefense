using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DefenseGrid
{
    public const int DefaultSize = 17;
    public const int WallLimit = 8;
    public const float WallWidth = 0.92f;
    public const float WallHeight = 0.8f;

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

    public int Width { get; }
    public int Depth { get; }
    public IReadOnlyList<Vector2Int> SpawnCells => spawns;
    // 스폰별 진영 인덱스. Routes[i]는 SpawnCells[i]에서 출발한다.
    public IReadOnlyList<int> SpawnFactions => spawnFactions;
    public IReadOnlyCollection<Vector2Int> FixedWalls => fixedWalls;
    // 코어까지 갈 수 없는 스폰이 있으면 null.
    public IReadOnlyList<Vector3[]> Routes => routes;
    // 경로 계산이 실패한 첫 스폰 인덱스. 성공하면 -1.
    public int UnreachableSpawn { get; private set; } = -1;
    public int WallCount => placed.Count;
    public Vector3 CorePosition => new(coreFootprint.center.x - 0.5f, 0f, coreFootprint.center.y - 0.5f);
    public Vector2Int CoreSize => coreFootprint.size;
    public Vector3 Center => new((Width - 1) * 0.5f, 0, (Depth - 1) * 0.5f);

    // 기본 지형: 17x17, 가운데 한 칸만 열린 가로 벽.
    public DefenseGrid() : this(DefaultSize, DefaultSize, new RectInt(DefaultSize / 2 - 1, DefaultSize - 3, 3, 3),
        new[] { new Vector2Int(2, 2), new Vector2Int(DefaultSize - 3, 2) }, new[] { 0, 1 }, DefaultWalls())
    {
    }

    // 범위 밖·코어·스폰 위에 놓인 벽 칸은 무시한다. 경로가 없으면 Routes가 null이다.
    public DefenseGrid(int width, int depth, RectInt core, IReadOnlyList<Vector2Int> spawnCells,
        IReadOnlyList<int> spawnFactionIndices, IEnumerable<Vector2Int> fixedWallCells)
    {
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

    private Vector3 ClosestCorePoint(Vector3 position) => new(
        Mathf.Clamp(position.x, coreFootprint.xMin - 0.5f, coreFootprint.xMax - 0.5f),
        0f,
        Mathf.Clamp(position.z, coreFootprint.yMin - 0.5f, coreFootprint.yMax - 0.5f));

    public BuildResult TryPlace(Vector2Int cell)
    {
        BuildResult result = CheckPlace(cell, out Vector3[][] updated);
        if (result != BuildResult.Placed)
            return result;

        placed.Add(cell);
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
        return true;
    }

    public bool HasLineOfSight(Vector3 from, Vector3 to)
        => IsWallFree(from, to, 0f);

    public bool CanPursue(Vector3 from, Vector3 to)
    {
        Vector3 step = to - from;
        float length = step.magnitude;
        if (length == 0f)
            return true;

        var coreBounds = new Bounds(CorePosition, new Vector3(CoreSize.x, 1f, CoreSize.y));
        return (!coreBounds.IntersectRay(new Ray(from, step / length), out float distance) || distance > length) &&
            IsWallFree(from, to, 0.24f);
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

    public static Bounds WallBounds(Vector2Int cell) =>
        new(CellPosition(cell) + Vector3.up * (WallHeight * 0.5f), new Vector3(WallWidth, WallHeight, WallWidth));

    public static Vector3 CellPosition(Vector2Int cell) => new(cell.x, 0, cell.y);

    public static Vector2Int PositionToCell(Vector3 position) =>
        new(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));

    private static bool IntersectsWall(Vector2Int cell, Ray ray, float length, float expansion)
    {
        Bounds bounds = WallBounds(cell);
        bounds.Expand(expansion);
        return bounds.IntersectRay(ray, out float distance) && distance <= length;
    }

    private bool IsWall(Vector2Int cell) => fixedWalls.Contains(cell) || placed.Contains(cell);

    private Vector3[][] FindRoutes(Vector2Int? candidate = null)
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

        var result = new Vector3[spawns.Length][];
        for (int i = 0; i < result.Length; i++)
        {
            Vector2Int cell = spawns[i];
            int distance = InBounds(cell) ? distances[cell.x, cell.y] : 0;
            if (distance == 0)
            {
                UnreachableSpawn = i;
                return null;
            }

            // Mirror the tie-break for spawns right of center so equally short paths stay symmetric.
            bool mirrored = cell.x * 2 > Width - 1;

            var path = new List<Vector3> { CellPosition(cell) };
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
                    path.Add(CellPosition(cell));
                    break;
                }
            }
            path.Add(ClosestCorePoint(CellPosition(cell)));
            result[i] = path.ToArray();
        }
        UnreachableSpawn = -1;
        return result;
    }

    public bool InBounds(Vector2Int cell) =>
        cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Depth;
}
