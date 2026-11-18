using System.Collections.Generic;
using UnityEngine;

// Stage Editor(에디터 어셈블리)가 게임 보드 표시 코드를 그대로 써서 미리보기를 그린다.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]

/// <summary>
/// 스테이지 하나의 보드: 크기, 코어, 스폰, 처음부터 놓인 장애물. Tools/Stage Editor에서 편집한다.
/// 좌표는 칸 단위이고 (0,0)이 화면 아래쪽 모서리, y가 클수록 코어 쪽이다.
/// </summary>
[CreateAssetMenu(menuName = "Be Violence/Stage Data", fileName = "Stage")]
public sealed class StageData : ScriptableObject
{
    public const int MinSize = 5;
    public const int MaxSize = 40;
    // 진영 = 종족. 순서는 EnemyFactionGroup 과 같다. 스테이지마다 일부 종족만 나올 수 있다.
    public const int FactionCount = 4;
    public static readonly string[] FactionNames = { "고블린", "언데드", "짐승", "드래곤·악마" };
    public static readonly Color[] FactionColors =
        { new(1f, 0.45f, 0.34f), new(0.27f, 0.87f, 0.81f), new(0.95f, 0.78f, 0.3f), new(0.68f, 0.45f, 0.95f) };

    [System.Serializable]
    public struct Spawn
    {
        public Vector2Int cell;
        [Tooltip("0 = 고블린, 1 = 언데드, 2 = 짐승, 3 = 드래곤·악마")]
        public int faction;
    }

    [System.Serializable]
    public struct Obstacle
    {
        [Tooltip("비우면 기본 벽")]
        public GameObject prefab;
        [Tooltip("차지하는 칸 중 x·y가 가장 작은 칸")]
        public Vector2Int cell;
        [Tooltip("회전을 반영한 칸 수")]
        public Vector2Int size;
        [Tooltip("Y축 90° 회전 횟수 (0~3)")]
        public int rotation;

        public RectInt Footprint => new(cell, size);
        public Vector3 Center => new Vector3(cell.x + (size.x - 1) * 0.5f, 0f, cell.y + (size.y - 1) * 0.5f) * DefenseGrid.DefaultCellSize;
    }

    [Range(MinSize, MaxSize)] public int width = DefenseGrid.DefaultSize;
    [Range(MinSize, MaxSize)] public int depth = DefenseGrid.DefaultSize;
    public RectInt core = new(DefenseGrid.DefaultSize / 2, DefenseGrid.DefaultSize - 2, 2, 2);
    public List<Spawn> spawns = new();
    public List<Obstacle> obstacles = new();

    public IEnumerable<Vector2Int> BlockedCells()
    {
        foreach (Obstacle obstacle in obstacles)
            foreach (Vector2Int cell in obstacle.Footprint.allPositionsWithin)
                yield return cell;
    }

    public DefenseGrid CreateGrid()
    {
        var cells = new Vector2Int[spawns.Count];
        var factions = new int[spawns.Count];
        for (int i = 0; i < spawns.Count; i++)
        {
            cells[i] = spawns[i].cell;
            factions[i] = spawns[i].faction;
        }
        return new DefenseGrid(width, depth, core, cells, factions, BlockedCells());
    }

    // 게임을 시작할 수 없는 문제. 없으면 null.
    public string Validate(DefenseGrid grid)
    {
        if (spawns.Count == 0)
            return "스폰이 없습니다.";
        foreach (Spawn spawn in spawns)
            if (spawn.faction < 0 || spawn.faction >= FactionCount)
                return $"스폰 {spawn.cell}의 진영 값({spawn.faction})이 잘못되었습니다.";
        if (core.xMin < 0 || core.yMin < 0 || core.xMax > width || core.yMax > depth)
            return "코어가 보드 밖으로 나갔습니다.";
        if (grid.Routes == null)
            return $"스폰 {spawns[grid.UnreachableSpawn].cell}에서 코어까지 가는 길이 없습니다.";
        return null;
    }
}
