using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseGrid
{
    public const int Width = 17;
    public const int Depth = Width;
    public const int SpawnPointCount = 2;
    public const int WallLimit = 8;
    public const float WallWidth = 0.92f;
    public const float WallHeight = 0.8f;

    private const int MainLaneColumn = Width / 2;
    private const int GateRow = 7;
    private static readonly RectInt CoreFootprint = new(MainLaneColumn, Depth - 2, 2, 2);
    private static readonly Vector2Int[] Spawns = { new(2, 2), new(Width - 3, 2) };
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    public enum BuildResult { Placed, Outside, Occupied, LimitReached, BlocksRoute }

    private readonly HashSet<Vector2Int> fixedWalls = new();
    private readonly HashSet<Vector2Int> placed = new();
    private Vector3[][] routes;

    public IReadOnlyList<Vector2Int> SpawnCells => Spawns;
    public IReadOnlyCollection<Vector2Int> FixedWalls => fixedWalls;
    public IReadOnlyList<Vector3[]> Routes => routes;
    public int WallCount => placed.Count;
    public Vector3 CorePosition => new(CoreFootprint.center.x - 0.5f, 0f, CoreFootprint.center.y - 0.5f);
    public Vector2Int CoreSize => CoreFootprint.size;
    public Vector3 Center => new((Width - 1) * 0.5f, 0, (Depth - 1) * 0.5f);

    public DefenseGrid()
    {
        // The two entrances share one straight approach from the gate to the core.
        for (int x = 0; x < Width; x++)
            if (x != MainLaneColumn)
                fixedWalls.Add(new Vector2Int(x, GateRow));
        routes = FindRoutes();
    }

    public bool IsWalkable(Vector2Int cell) => InBounds(cell) && !CoreFootprint.Contains(cell) && !IsWall(cell);

    private static Vector3 ClosestCorePoint(Vector3 position) => new(
        Mathf.Clamp(position.x, CoreFootprint.xMin - 0.5f, CoreFootprint.xMax - 0.5f),
        0f,
        Mathf.Clamp(position.z, CoreFootprint.yMin - 0.5f, CoreFootprint.yMax - 0.5f));

    public BuildResult TryPlace(Vector2Int cell)
    {
        if (!InBounds(cell))
            return BuildResult.Outside;
        if (!IsWalkable(cell) || Array.IndexOf(Spawns, cell) >= 0)
            return BuildResult.Occupied;
        if (placed.Count >= WallLimit)
            return BuildResult.LimitReached;

        Vector3[][] updated = FindRoutes(cell);
        if (updated == null)
            return BuildResult.BlocksRoute;

        placed.Add(cell);
        routes = updated;
        return BuildResult.Placed;
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
        foreach (Vector2Int coreCell in CoreFootprint.allPositionsWithin)
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

        var result = new Vector3[SpawnPointCount][];
        for (int i = 0; i < result.Length; i++)
        {
            Vector2Int cell = Spawns[i];
            int distance = distances[cell.x, cell.y];
            if (distance == 0)
                return null;

            var path = new List<Vector3> { CellPosition(cell) };
            while (distance > 1)
            {
                // Mirror the tie-break so equally short paths respect the default terrain's symmetry.
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int step = i == 0 ? direction : new Vector2Int(-direction.x, direction.y);
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
        return result;
    }

    public static bool InBounds(Vector2Int cell) =>
        cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Depth;
}
