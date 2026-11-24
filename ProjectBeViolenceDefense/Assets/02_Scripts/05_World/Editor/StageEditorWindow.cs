using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// StageData 편집 창. 보드 크기, 고정 장애물 프리팹, 진영별 스폰, 코어 위치를 칸 단위로 칠한다.
/// 게임 화면(실제 보드 표시 코드) 또는 격자에서 왼쪽 클릭·드래그로 배치, 오른쪽 클릭으로 삭제, R로 장애물 회전.
/// </summary>
internal sealed class StageEditorWindow : EditorWindow
{
    private const string ObstacleFolder = "Assets/03_Prefabs/04_World";
    private const string StageFolder = "Assets/04_Data/04_Stages";
    private const float SidebarWidth = 300f;
    private const float PaletteTile = 70f;
    private static readonly Regex FootprintPattern = new(@"(\d+)x(\d+)");
    private static readonly Color[] FactionColors = StageData.FactionColors;
    private static readonly string[] FactionNames = StageData.FactionNames;
    private static readonly Color TileA = new(0.28f, 0.36f, 0.40f);
    private static readonly Color TileB = new(0.32f, 0.40f, 0.42f);
    private static readonly Color WallColor = new(0.45f, 0.36f, 0.30f);
    private static readonly Color CoreColor = new(1f, 0.82f, 0.44f);

    private enum Tool { Obstacle, Spawn, Core, Erase }

    private struct PaletteItem
    {
        public GameObject Prefab;   // null = 기본 벽
        public Vector2Int Size;
    }

    [SerializeField] private StageData stage;
    [SerializeField] private Tool tool;
    [SerializeField] private int spawnFaction;
    [SerializeField] private int paletteIndex;
    [SerializeField] private int rotation;
    [SerializeField] private bool gameView = true;
    private StagePreview gamePreview;
    private readonly List<PaletteItem> palette = new();
    private DefenseGrid preview;
    private string problem;
    private Vector2 sidebarScroll;
    private Vector2Int? hoverCell;
    private Vector2Int? lastPaintedCell;

    [MenuItem("Tools/Stage Editor")]
    public static void Open() => Open(null);

    public static void Open(StageData target)
    {
        var window = GetWindow<StageEditorWindow>("Stage Editor");
        window.minSize = new Vector2(640f, 420f);
        if (target != null)
            window.SetStage(target);
    }

    [OnOpenAsset]
    private static bool OnOpenAsset(EntityId entityId, int line)
    {
        if (EditorUtility.EntityIdToObject(entityId) is not StageData target)
            return false;
        Open(target);
        return true;
    }

    // 프리팹 이름의 "NxM"이 차지하는 칸 수다 (모델 크기 아님). 없으면 1x1.
    internal static Vector2Int FootprintFromName(string prefabName)
    {
        Match match = FootprintPattern.Match(prefabName);
        return match.Success
            ? new Vector2Int(Mathf.Max(1, int.Parse(match.Groups[1].Value)), Mathf.Max(1, int.Parse(match.Groups[2].Value)))
            : Vector2Int.one;
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        wantsMouseMove = true;
        gamePreview = new StagePreview();
        LoadPalette();
        Rebuild();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        gamePreview?.Dispose();
        gamePreview = null;
    }

    private void OnProjectChange()
    {
        LoadPalette();
        Repaint();
    }

    private void OnUndoRedo()
    {
        Rebuild();
        Repaint();
    }

    private void LoadPalette()
    {
        palette.Clear();
        palette.Add(new PaletteItem { Prefab = null, Size = Vector2Int.one });
        if (!AssetDatabase.IsValidFolder(ObstacleFolder))
            return;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ObstacleFolder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null)
                palette.Add(new PaletteItem { Prefab = prefab, Size = FootprintFromName(prefab.name) });
        }
        paletteIndex = Mathf.Clamp(paletteIndex, 0, palette.Count - 1);
    }

    private void SetStage(StageData target)
    {
        stage = target;
        Rebuild();
        Repaint();
    }

    private void Rebuild()
    {
        if (stage == null)
        {
            preview = null;
            problem = null;
            return;
        }
        preview = stage.CreateGrid();
        problem = stage.Validate(preview);
        if (gameView)
            gamePreview?.Rebuild(stage);
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(sidebarScroll, false, false, GUIStyle.none,
                       GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.Width(SidebarWidth)))
            {
                sidebarScroll = scroll.scrollPosition;
                EditorGUIUtility.labelWidth = 70f;
                DrawSidebar();
                EditorGUIUtility.labelWidth = 0f;
            }

            using (new EditorGUILayout.VerticalScope())
            {
                if (stage != null)
                    DrawCanvasToolbar();
                Rect canvas = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (stage == null)
                    EditorGUI.LabelField(canvas, "StageData를 선택하거나 새로 만드세요.", EditorStyles.centeredGreyMiniLabel);
                else if (gameView)
                    DrawGameView(canvas);
                else
                    DrawBoard(canvas);
            }
        }

        Event e = Event.current;
        if (stage != null && tool == Tool.Obstacle && e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
        {
            rotation = (rotation + 1) % 4;
            e.Use();
            Repaint();
        }
    }

    private void DrawSidebar()
    {
        EditorGUI.BeginChangeCheck();
        var picked = (StageData)EditorGUILayout.ObjectField("스테이지", stage, typeof(StageData), false);
        if (EditorGUI.EndChangeCheck())
            SetStage(picked);
        if (GUILayout.Button("새 스테이지"))
            CreateStage();
        if (stage == null)
            return;

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUI.BeginChangeCheck();
            var waves = (StageWaves)EditorGUILayout.ObjectField("웨이브", stage.waves, typeof(StageWaves), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(stage, "스테이지 웨이브 연결");
                stage.waves = waves;
                EditorUtility.SetDirty(stage);
            }
        }
        if (GUILayout.Button(stage.waves != null ? "웨이브 편집기 열기" : "웨이브 만들기…"))
            StageWavesEditorWindow.Open(stage.waves, stage);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("보드 크기", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int width = EditorGUILayout.IntSlider("가로", stage.width, StageData.MinSize, StageData.MaxSize);
        int depth = EditorGUILayout.IntSlider("세로", stage.depth, StageData.MinSize, StageData.MaxSize);
        if (EditorGUI.EndChangeCheck())
            Resize(width, depth);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("도구", EditorStyles.boldLabel);
        tool = (Tool)GUILayout.Toolbar((int)tool, new[] { "장애물", "스폰", "코어", "지우기" });
        switch (tool)
        {
            case Tool.Obstacle:
                DrawPalette();
                break;
            case Tool.Spawn:
                spawnFaction = GUILayout.Toolbar(spawnFaction, FactionNames);
                EditorGUILayout.HelpBox("클릭한 칸에 스폰을 놓습니다. 기존 스폰을 클릭하면 진영을 바꿉니다. " +
                    "한 진영에 스폰이 여러 개면 유닛이 번갈아 나옵니다.", MessageType.None);
                break;
            case Tool.Core:
                EditorGUILayout.HelpBox($"클릭한 칸이 코어({stage.core.width}x{stage.core.height})의 왼쪽 아래 칸이 됩니다.",
                    MessageType.None);
                break;
            case Tool.Erase:
                EditorGUILayout.HelpBox("클릭한 칸의 장애물이나 스폰을 지웁니다. 오른쪽 클릭은 어느 도구에서든 지우기입니다.",
                    MessageType.None);
                break;
        }

        EditorGUILayout.Space();
        int[] spawnCounts = new int[StageData.FactionCount];
        foreach (StageData.Spawn spawn in stage.spawns)
            if (spawn.faction >= 0 && spawn.faction < spawnCounts.Length)
                spawnCounts[spawn.faction]++;
        EditorGUILayout.LabelField($"장애물 {stage.obstacles.Count}개 · 막힌 칸 {preview.FixedWalls.Count}칸");
        var spawnSummary = new string[spawnCounts.Length];
        for (int i = 0; i < spawnCounts.Length; i++)
            spawnSummary[i] = $"{FactionNames[i]} {spawnCounts[i]}";
        EditorGUILayout.LabelField("스폰 " + string.Join(" · ", spawnSummary));
        if (problem != null)
            EditorGUILayout.HelpBox(problem, MessageType.Error);
        else
            EditorGUILayout.HelpBox("모든 스폰에서 코어까지 갈 수 있습니다.", MessageType.Info);
    }

    private void DrawPalette()
    {
        PaletteItem current = palette[paletteIndex];
        using (new EditorGUILayout.HorizontalScope())
        {
            Vector2Int size = RotatedSize(current.Size);
            EditorGUILayout.LabelField($"회전 {rotation * 90}° · {size.x}x{size.y}칸");
            if (GUILayout.Button("회전 (R)", GUILayout.Width(70f)))
                rotation = (rotation + 1) % 4;
        }

        int columns = Mathf.Max(1, Mathf.FloorToInt((SidebarWidth - 30f) / (PaletteTile + 4f)));
        for (int start = 0; start < palette.Count; start += columns)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = start; i < Mathf.Min(start + columns, palette.Count); i++)
                {
                    PaletteItem item = palette[i];
                    Texture preview = item.Prefab != null ? AssetPreview.GetAssetPreview(item.Prefab) : null;
                    string label = item.Prefab != null ? item.Prefab.name : "기본 벽";
                    var content = new GUIContent(preview != null ? null : label, preview,
                        $"{label} ({item.Size.x}x{item.Size.y}칸)");
                    Rect rect = GUILayoutUtility.GetRect(PaletteTile, PaletteTile, GUILayout.Width(PaletteTile));
                    if (GUI.Toggle(rect, paletteIndex == i, content, "Button") && paletteIndex != i)
                        paletteIndex = i;
                    GUI.Label(new Rect(rect.x + 3f, rect.yMax - 16f, rect.width, 16f),
                        $"{item.Size.x}x{item.Size.y}", EditorStyles.whiteMiniLabel);
                }
            }
        }
        if (AssetPreview.IsLoadingAssetPreviews())
            Repaint();
        EditorGUILayout.HelpBox($"{ObstacleFolder}의 프리팹. 이름의 NxM이 차지하는 칸 수입니다.", MessageType.None);
    }

    private void DrawCanvasToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            gameView = GUILayout.Toolbar(gameView ? 0 : 1, new[] { "게임 화면", "격자" }, EditorStyles.toolbarButton,
                GUILayout.Width(140f)) == 0;
            if (EditorGUI.EndChangeCheck())
            {
                hoverCell = null;
                Rebuild();
            }
            GUILayout.FlexibleSpace();
            if (!gameView)
                return;
            GUILayout.Label(gamePreview.Source != null
                ? $"설정: {gamePreview.Source}"
                : "씬에 Voxel Defense Prototype이 없어 기본 설정으로 그립니다", EditorStyles.miniLabel);
            GUILayout.Label("· 휠 줌 · 가운데 버튼 드래그 이동", EditorStyles.miniLabel);
            if (GUILayout.Button("새로 고침", EditorStyles.toolbarButton))
            {
                // 씬의 조명·후처리·카메라 설정을 다시 읽는다.
                gamePreview.Dispose();
                gamePreview = new StagePreview();
                Rebuild();
            }
        }
    }

    // 게임과 같은 보드 표시 코드·카메라 각도로 그린 화면 위에서 바로 칠한다.
    private void DrawGameView(Rect canvas)
    {
        Event e = Event.current;
        bool inside = canvas.Contains(e.mousePosition);
        if (inside && e.type == EventType.ScrollWheel)
        {
            gamePreview.Zoom(canvas, e.mousePosition, -e.delta.y);
            e.Use();
            Repaint();
        }
        else if (inside && e.type == EventType.MouseDrag && e.button == 2)
        {
            gamePreview.Drag(canvas, e.mousePosition - e.delta, e.mousePosition);
            e.Use();
            Repaint();
        }

        Vector2Int? cellUnderMouse = null;
        if (inside && gamePreview.TryPickCell(canvas, e.mousePosition, out Vector2Int picked) &&
            InBoard(new RectInt(picked, Vector2Int.one)))
            cellUnderMouse = picked;
        HandleInput(cellUnderMouse);
        if (e.type != EventType.Repaint)
            return;

        if (gamePreview.Error != null)
        {
            EditorGUI.HelpBox(canvas, $"미리보기를 만들 수 없습니다: {gamePreview.Error}", MessageType.Error);
            return;
        }
        if (hoverCell is { } hover)
        {
            bool allowed = BrushAllowed(hover);
            gamePreview.ShowBrush(Brush(hover), BrushColor(allowed), allowed, tool == Tool.Obstacle,
                palette[paletteIndex].Prefab, rotation);
        }
        else
        {
            gamePreview.HideBrush();
        }
        gamePreview.Render(canvas);
    }

    private bool BrushAllowed(Vector2Int hover) => tool switch
    {
        Tool.Obstacle => CanPlace(Brush(hover), ignoreCore: false),
        Tool.Core => CanPlace(Brush(hover), ignoreCore: true),
        Tool.Spawn => CanPlaceSpawn(hover),
        _ => true
    };

    private Color BrushColor(bool allowed) => tool == Tool.Erase ? new Color(1f, 1f, 1f, 0.25f)
        : allowed ? new Color(0.3f, 1f, 0.4f, 0.35f) : new Color(1f, 0.25f, 0.2f, 0.4f);

    private void DrawBoard(Rect canvas)
    {
        const float margin = 12f;
        float cell = Mathf.Floor(Mathf.Min((canvas.width - margin * 2f) / stage.width,
            (canvas.height - margin * 2f) / stage.depth));
        if (cell < 4f)
            return;
        var origin = new Vector2(canvas.x + (canvas.width - cell * stage.width) * 0.5f,
            canvas.y + (canvas.height - cell * stage.depth) * 0.5f);
        Rect CellsRect(RectInt cells) => new(origin.x + cells.xMin * cell, origin.y + (stage.depth - cells.yMax) * cell,
            cells.width * cell, cells.height * cell);
        Vector2 CellCenter(Vector3 point) => new(origin.x + (point.x + 0.5f) * cell, origin.y + (stage.depth - point.z - 0.5f) * cell);

        Vector2 mouse = Event.current.mousePosition;
        Vector2Int? cellUnderMouse = null;
        if (canvas.Contains(mouse))
        {
            int x = Mathf.FloorToInt((mouse.x - origin.x) / cell);
            int y = stage.depth - 1 - Mathf.FloorToInt((mouse.y - origin.y) / cell);
            if (x >= 0 && x < stage.width && y >= 0 && y < stage.depth)
                cellUnderMouse = new Vector2Int(x, y);
        }
        HandleInput(cellUnderMouse);
        if (Event.current.type != EventType.Repaint)
            return;

        for (int x = 0; x < stage.width; x++)
            for (int y = 0; y < stage.depth; y++)
                EditorGUI.DrawRect(Inset(CellsRect(new RectInt(x, y, 1, 1)), 1f), (x + y) % 2 == 0 ? TileA : TileB);

        if (preview.Routes != null)
        {
            for (int i = 0; i < preview.Routes.Length; i++)
            {
                Vector3[] route = preview.Routes[i];
                var points = new Vector3[route.Length];
                for (int p = 0; p < route.Length; p++)
                    points[p] = CellCenter(route[p]);
                Handles.color = FactionColors[preview.SpawnFactions[i]] * new Color(1f, 1f, 1f, 0.75f);
                Handles.DrawAAPolyLine(Mathf.Max(2f, cell * 0.12f), points);
            }
        }

        EditorGUI.DrawRect(Inset(CellsRect(stage.core), 2f), CoreColor);
        GUI.Label(CellsRect(stage.core), "Core", CenteredLabel(Color.black));

        foreach (StageData.Obstacle obstacle in stage.obstacles)
        {
            Rect rect = Inset(CellsRect(obstacle.Footprint), 2f);
            EditorGUI.DrawRect(rect, WallColor);
            Texture texture = obstacle.prefab != null ? AssetPreview.GetAssetPreview(obstacle.prefab) : null;
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
        }

        for (int i = 0; i < stage.spawns.Count; i++)
        {
            StageData.Spawn spawn = stage.spawns[i];
            Rect rect = Inset(CellsRect(new RectInt(spawn.cell, Vector2Int.one)), cell * 0.12f);
            int faction = Mathf.Clamp(spawn.faction, 0, FactionColors.Length - 1);
            EditorGUI.DrawRect(rect, FactionColors[faction]);
            GUI.Label(rect, $"{FactionNames[faction][0]}{i + 1}", CenteredLabel(Color.black));
        }

        if (hoverCell is { } hover)
            EditorGUI.DrawRect(CellsRect(Brush(hover)), BrushColor(BrushAllowed(hover)));
    }

    private void HandleInput(Vector2Int? cellUnderMouse)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            if (hoverCell != cellUnderMouse)
            {
                hoverCell = cellUnderMouse;
                Repaint();
            }
        }

        if (e.type == EventType.MouseUp)
            lastPaintedCell = null;
        if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || cellUnderMouse is not { } target)
            return;
        if (e.button != 0 && e.button != 1)
            return;
        if (e.type == EventType.MouseDown)
            GUI.FocusControl(null);

        e.Use();
        if (target == lastPaintedCell)
            return;
        lastPaintedCell = target;
        if (e.button == 1 || tool == Tool.Erase)
            Erase(target);
        else
            Paint(target);
    }

    private Vector2Int RotatedSize(Vector2Int size) => rotation % 2 == 0 ? size : new Vector2Int(size.y, size.x);

    private RectInt Brush(Vector2Int cell) => tool switch
    {
        Tool.Obstacle => new RectInt(cell, RotatedSize(palette[paletteIndex].Size)),
        Tool.Core => new RectInt(cell, stage.core.size),
        _ => new RectInt(cell, Vector2Int.one)
    };

    private bool InBoard(RectInt rect) =>
        rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= stage.width && rect.yMax <= stage.depth;

    // ignoreCore: 코어를 옮길 때 지금 코어 자리와의 겹침은 무시한다.
    private bool CanPlace(RectInt rect, bool ignoreCore)
    {
        if (!InBoard(rect))
            return false;
        if (!ignoreCore && rect.Overlaps(stage.core))
            return false;
        foreach (StageData.Obstacle obstacle in stage.obstacles)
            if (rect.Overlaps(obstacle.Footprint))
                return false;
        foreach (StageData.Spawn spawn in stage.spawns)
            if (rect.Contains(spawn.cell))
                return false;
        return true;
    }

    private bool CanPlaceSpawn(Vector2Int cell)
    {
        if (stage.spawns.Exists(spawn => spawn.cell == cell))
            return true;
        return CanPlace(new RectInt(cell, Vector2Int.one), ignoreCore: false);
    }

    private void Paint(Vector2Int cell)
    {
        switch (tool)
        {
            case Tool.Obstacle:
            {
                PaletteItem item = palette[paletteIndex];
                RectInt footprint = Brush(cell);
                if (!CanPlace(footprint, ignoreCore: false))
                    return;
                Record("장애물 배치");
                stage.obstacles.Add(new StageData.Obstacle
                {
                    prefab = item.Prefab, cell = cell, size = footprint.size, rotation = rotation
                });
                break;
            }
            case Tool.Spawn:
            {
                int index = stage.spawns.FindIndex(spawn => spawn.cell == cell);
                if (index >= 0)
                {
                    if (stage.spawns[index].faction == spawnFaction)
                        return;
                    Record("스폰 진영 변경");
                    stage.spawns[index] = new StageData.Spawn { cell = cell, faction = spawnFaction };
                    break;
                }
                if (!CanPlaceSpawn(cell))
                    return;
                Record("스폰 배치");
                stage.spawns.Add(new StageData.Spawn { cell = cell, faction = spawnFaction });
                break;
            }
            case Tool.Core:
            {
                RectInt core = Brush(cell);
                if (core.position == stage.core.position || !CanPlace(core, ignoreCore: true))
                    return;
                Record("코어 이동");
                stage.core = core;
                break;
            }
        }
        Changed();
    }

    private void Erase(Vector2Int cell)
    {
        int obstacle = stage.obstacles.FindLastIndex(o => o.Footprint.Contains(cell));
        if (obstacle >= 0)
        {
            Record("장애물 삭제");
            stage.obstacles.RemoveAt(obstacle);
            Changed();
            return;
        }

        int spawn = stage.spawns.FindIndex(s => s.cell == cell);
        if (spawn >= 0)
        {
            Record("스폰 삭제");
            stage.spawns.RemoveAt(spawn);
            Changed();
        }
    }

    // 줄어든 보드 밖의 장애물·스폰은 지우고 코어는 안쪽으로 민다. Undo로 되돌릴 수 있다.
    private void Resize(int width, int depth)
    {
        Record("보드 크기 변경");
        stage.width = width;
        stage.depth = depth;
        stage.obstacles.RemoveAll(o => !InBoard(o.Footprint));
        stage.spawns.RemoveAll(s => !InBoard(new RectInt(s.cell, Vector2Int.one)));
        RectInt core = stage.core;
        core.x = Mathf.Clamp(core.x, 0, Mathf.Max(0, width - core.width));
        core.y = Mathf.Clamp(core.y, 0, Mathf.Max(0, depth - core.height));
        stage.core = core;
        Changed();
    }

    private void Record(string action) => Undo.RecordObject(stage, action);

    private void Changed()
    {
        EditorUtility.SetDirty(stage);
        Rebuild();
        Repaint();
    }

    private void CreateStage()
    {
        if (!AssetDatabase.IsValidFolder(StageFolder))
            AssetDatabase.CreateFolder("Assets/04_Data", "04_Stages");
        string path = EditorUtility.SaveFilePanelInProject("새 스테이지", "Stage", "asset", "스테이지 에셋을 저장할 위치", StageFolder);
        if (string.IsNullOrEmpty(path))
            return;

        var created = CreateInstance<StageData>();
        created.spawns.Add(new StageData.Spawn { cell = new Vector2Int(2, 2), faction = 0 });
        created.spawns.Add(new StageData.Spawn { cell = new Vector2Int(created.width - 3, 2), faction = 1 });
        AssetDatabase.CreateAsset(created, path);
        AssetDatabase.SaveAssets();
        SetStage(created);
    }

    private static Rect Inset(Rect rect, float amount) =>
        new(rect.x + amount, rect.y + amount, Mathf.Max(0f, rect.width - amount * 2f), Mathf.Max(0f, rect.height - amount * 2f));

    private static GUIStyle CenteredLabel(Color color) => new(EditorStyles.miniBoldLabel)
    {
        alignment = TextAnchor.MiddleCenter,
        normal = { textColor = color }
    };
}

[CustomEditor(typeof(StageData))]
internal sealed class StageDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Stage Editor 열기", GUILayout.Height(28f)))
            StageEditorWindow.Open((StageData)target);
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
