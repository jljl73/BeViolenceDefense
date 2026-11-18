using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseGrid
{
    public const int Width = 12;
    public const int Depth = 10;
    public const int WallLimit = 8;
    public const float WallWidth = 0.92f;
    public const float WallHeight = 0.8f;

    private static readonly Vector2Int CoreCell = new(3, 9);
    // 4, 6 and 8 o'clock in the default camera view, in spawn order.
    private static readonly Vector2Int[] Spawns = { new(11, 4), new(8, 0), new(4, 0) };
    // 18 of 120 cells. The dividers funnel the three approaches into the lane at z = 5.
    private static readonly Vector2Int[] InitialWalls =
    {
        // Core ramparts.
        new(2, 8), new(2, 9), new(5, 8), new(5, 9), new(4, 7),
        // Upper edge of the shared lane.
        new(5, 6), new(6, 6), new(7, 6), new(8, 6), new(9, 6), new(10, 6),
        // Dividers on the lower approaches.
        new(2, 3), new(3, 3), new(4, 3), new(5, 3),
        new(7, 1), new(7, 2), new(7, 3)
    };
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    public enum BuildResult { Placed, Outside, Occupied, LimitReached, BlocksRoute }

    private readonly HashSet<Vector2Int> fixedWalls = new(InitialWalls);
    private readonly HashSet<Vector2Int> placed = new();
    private Vector3[][] routes;

    public IReadOnlyList<Vector2Int> SpawnCells => Spawns;
    public IReadOnlyCollection<Vector2Int> FixedWalls => fixedWalls;
    public IReadOnlyList<Vector3[]> Routes => routes;
    public int WallCount => placed.Count;
    public Vector3 CorePosition => CellPosition(CoreCell);
    public Vector3 Center => new((Width - 1) * 0.5f, 0, (Depth - 1) * 0.5f);

    public DefenseGrid()
    {
        routes = FindRoutes();
    }

    public bool IsWalkable(Vector2Int cell) => InBounds(cell) && !IsWall(cell);

    public BuildResult TryPlace(Vector2Int cell)
    {
        if (!InBounds(cell))
            return BuildResult.Outside;
        if (cell == CoreCell || Array.IndexOf(Spawns, cell) >= 0 || IsWall(cell))
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
    {
        Vector3 step = to - from;
        float length = step.magnitude;
        if (length == 0)
            return true;

        var ray = new Ray(from + Vector3.up * (WallHeight * 0.5f), step / length);
        foreach (Vector2Int cell in fixedWalls)
        {
            if (IntersectsWall(cell, ray, length))
                return false;
        }
        foreach (Vector2Int cell in placed)
        {
            if (IntersectsWall(cell, ray, length))
                return false;
        }
        return true;
    }

    public static Bounds WallBounds(Vector2Int cell) =>
        new(CellPosition(cell) + Vector3.up * (WallHeight * 0.5f), new Vector3(WallWidth, WallHeight, WallWidth));

    public static Vector3 CellPosition(Vector2Int cell) => new(cell.x, 0, cell.y);

    public static Vector2Int PositionToCell(Vector3 position) =>
        new(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));

    private static bool IntersectsWall(Vector2Int cell, Ray ray, float length) =>
        WallBounds(cell).IntersectRay(ray, out float distance) && distance <= length;

    private bool IsWall(Vector2Int cell) => fixedWalls.Contains(cell) || placed.Contains(cell);

    private Vector3[][] FindRoutes(Vector2Int? candidate = null)
    {
        if (candidate == CoreCell || IsWall(CoreCell))
            return null;

        // Flood from the core once to validate all entrances and give them a common route after a merge.
        var parents = new Vector2Int?[Width, Depth];
        var queue = new Queue<Vector2Int>();
        parents[CoreCell.x, CoreCell.y] = CoreCell;
        queue.Enqueue(CoreCell);
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                if (!InBounds(next) || parents[next.x, next.y].HasValue || next == candidate || IsWall(next))
                    continue;

                parents[next.x, next.y] = cell;
                queue.Enqueue(next);
            }
        }

        var result = new Vector3[Spawns.Length][];
        for (int i = 0; i < Spawns.Length; i++)
        {
            Vector2Int cell = Spawns[i];
            if (!parents[cell.x, cell.y].HasValue)
                return null;

            var path = new List<Vector3> { CellPosition(cell) };
            while (cell != CoreCell)
            {
                cell = parents[cell.x, cell.y].Value;
                path.Add(CellPosition(cell));
            }
            result[i] = path.ToArray();
        }
        return result;
    }

    public static bool InBounds(Vector2Int cell) =>
        cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Depth;
}
