using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

internal sealed class StageWavesEditorWindow : EditorWindow
{
    [SerializeField] private StageWaves asset;
    [SerializeField] private EnemyCatalog catalog;
    [SerializeField] private StageData previewStage;
    [SerializeField] private int selectedWave;
    [SerializeField] private bool showIssues;
    private SerializedObject data;
    private ReorderableList waveList;
    private readonly Dictionary<string, ReorderableList> unitLists = new();
    private List<StageWavesEditing.Unit> units = new();
    private List<StageWavesEditing.Issue> issues = new();
    private readonly AdvancedDropdownState pickerState = new();
    private UnitPicker picker;
    private Vector2 waveScroll, detailScroll, issueScroll;
    private double nextRefresh;
    private GUIStyle waveStyle, selectedWaveStyle, groupStyle;

    [MenuItem("Tools/Stage Waves Editor")]
    public static void Open()
    {
        if (Selection.activeObject is StageData stage)
            Open(stage.waves, stage);
        else
            Open(Selection.activeObject as StageWaves);
    }

    public static void Open(StageWaves target, StageData stage = null)
    {
        var window = GetWindow<StageWavesEditorWindow>("Stage Waves");
        window.minSize = new Vector2(840f, 520f);
        if (target != null || stage != null) window.SetAsset(target, stage);
        window.Show();
    }

    [OnOpenAsset]
    private static bool OnOpenAsset(EntityId entityId, int line)
    {
        if (EditorUtility.EntityIdToObject(entityId) is not StageWaves target) return false;
        Open(target);
        return true;
    }

    private void OnEnable()
    {
        minSize = new Vector2(840f, 520f);
        Undo.undoRedoPerformed += Rebuild;
        EditorApplication.projectChanged += RefreshCatalog;
        if (catalog == null)
            catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/04_Data/03_Balance/EnemyCatalog.asset");
        RefreshCatalog();
        Rebuild();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Rebuild;
        EditorApplication.projectChanged -= RefreshCatalog;
        data?.Dispose();
        data = null;
    }

    private void OnInspectorUpdate()
    {
        if (EditorApplication.timeSinceStartup < nextRefresh) return;
        nextRefresh = EditorApplication.timeSinceStartup + 0.5;
        RefreshCatalog();
        Repaint();
    }

    private void RefreshCatalog()
    {
        units = StageWavesEditing.ReadCatalog(catalog);
        RefreshIssues();
    }

    private void RefreshIssues() => issues = StageWavesEditing.Validate(asset, units, catalog != null, previewStage);

    private void SetAsset(StageWaves target, StageData stage = null)
    {
        GUI.FocusControl(null);
        asset = target;
        previewStage = stage;
        selectedWave = 0;
        detailScroll = waveScroll = Vector2.zero;
        Rebuild();
    }

    private void Rebuild()
    {
        data?.Dispose();
        data = asset != null ? new SerializedObject(asset) : null;
        waveList = null;
        unitLists.Clear();
        if (data != null)
        {
            var waves = data.FindProperty("waves");
            selectedWave = Mathf.Clamp(selectedWave, 0, Mathf.Max(0, waves.arraySize - 1));
            waveList = new ReorderableList(data, waves, true, false, false, false)
            {
                elementHeight = 56f,
                index = selectedWave,
                drawElementCallback = DrawWaveRow,
                onSelectCallback = list => SelectWave(list.index),
                onReorderCallback = list => { selectedWave = list.index; CommitStructure(); }
            };
        }
        RefreshIssues();
        Repaint();
    }

    private void SelectWave(int index)
    {
        GUI.FocusControl(null);
        selectedWave = index;
        if (waveList != null) waveList.index = index;
        detailScroll = Vector2.zero;
        unitLists.Clear();
    }

    private void CommitStructure()
    {
        data.ApplyModifiedProperties();
        GUI.FocusControl(null);
        Rebuild();
        GUIUtility.ExitGUI();
    }

    private void InitStyles()
    {
        waveStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = false, padding = new RectOffset(8, 4, 4, 2) };
        selectedWaveStyle ??= new GUIStyle(waveStyle) { normal = { textColor = UIPalette.TextOnAccent } };
        groupStyle ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 8, 8) };
    }

    private void OnGUI()
    {
        InitStyles();
        DrawToolbar();
        if (asset == null)
        {
            EditorGUILayout.Space(24);
            EditorGUILayout.HelpBox("StageWaves 에셋을 위에 놓거나 새로 만드세요.\n웨이브 목록 → 팩션별 후보 그룹 → 출전 유닛 순서로 편집합니다.", MessageType.Info);
            if (previewStage != null)
                EditorGUILayout.LabelField($"새로 만들면 {previewStage.name}.waves에 자동으로 연결합니다.");
            return;
        }
        if (data == null || data.targetObject != asset) Rebuild();
        data.Update();
        bool readOnly = EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.IsOpenForEdit(asset);
        if (readOnly)
            EditorGUILayout.HelpBox("현재 읽기 전용입니다. Play Mode를 종료하고 에셋의 쓰기 권한을 확인하세요.", MessageType.Info);
        DrawContext();
        using (new EditorGUI.DisabledScope(readOnly))
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawWaveList();
            using (new EditorGUILayout.VerticalScope())
            using (var scroll = new EditorGUILayout.ScrollViewScope(detailScroll))
            {
                detailScroll = scroll.scrollPosition;
                DrawWaveDetails();
            }
        }
        if (data.ApplyModifiedProperties()) RefreshIssues();
        DrawIssues();
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label($"{asset.waves?.Count ?? 0} 웨이브  ·  Ctrl+Z 되돌리기 / Ctrl+Y 다시 실행", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(EditorUtility.IsDirty(asset) ? "● 저장하지 않은 변경 · Ctrl+S" : "저장됨", EditorStyles.miniLabel);
        }
        var e = Event.current;
        if (e.type == EventType.KeyDown && (e.control || e.command) && e.keyCode == KeyCode.S && !readOnly)
        {
            Save();
            e.Use();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("웨이브 편성", EditorStyles.boldLabel, GUILayout.Width(86));
            EditorGUI.BeginChangeCheck();
            var picked = (StageWaves)EditorGUILayout.ObjectField(asset, typeof(StageWaves), false, GUILayout.MinWidth(150));
            if (EditorGUI.EndChangeCheck()) SetAsset(picked);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("새로 만들기", EditorStyles.toolbarButton, GUILayout.Width(82))) CreateAsset();
            using (new EditorGUI.DisabledScope(asset == null))
            {
                if (GUILayout.Button("찾기", EditorStyles.toolbarButton, GUILayout.Width(42))) EditorGUIUtility.PingObject(asset);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                           (asset != null && !AssetDatabase.IsOpenForEdit(asset))))
                    if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(42))) Save();
            }
        }
    }

    private void DrawContext()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(new GUIContent("유닛 카탈로그", "선택·검사용입니다. 게임은 씬의 VoxelDefensePrototype에 연결된 EnemyCatalog를 사용합니다."), GUILayout.Width(88));
            EditorGUI.BeginChangeCheck();
            catalog = (EnemyCatalog)EditorGUILayout.ObjectField(catalog, typeof(EnemyCatalog), false);
            if (EditorGUI.EndChangeCheck()) RefreshCatalog();
            GUILayout.Label(new GUIContent("스폰 검사", "선택 사항: 이 스테이지에 팩션별 스폰이 있는지 검사합니다. 에셋 연결은 변경하지 않습니다."), GUILayout.Width(65));
            EditorGUI.BeginChangeCheck();
            previewStage = (StageData)EditorGUILayout.ObjectField(previewStage, typeof(StageData), false);
            if (EditorGUI.EndChangeCheck()) RefreshIssues();
        }
        if (previewStage != null && previewStage.waves != asset)
            EditorGUILayout.HelpBox($"{previewStage.name}에 연결된 웨이브와 현재 편집 대상이 다릅니다. 여기서는 스폰만 검사합니다.", MessageType.Warning);
    }

    private void DrawWaveList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(220)))
        {
            EditorGUILayout.LabelField("웨이브 순서", EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(waveScroll))
            {
                waveScroll = scroll.scrollPosition;
                waveList.DoLayoutList();
            }
            if (GUILayout.Button("+ 웨이브"))
            {
                selectedWave = StageWavesEditing.AddWave(data.FindProperty("waves"));
                CommitStructure();
            }
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(data.FindProperty("waves").arraySize == 0))
            {
                var waves = data.FindProperty("waves");
                if (GUILayout.Button("복제"))
                {
                    selectedWave = StageWavesEditing.Duplicate(waves, selectedWave);
                    CommitStructure();
                }
                if (GUILayout.Button("삭제") && EditorUtility.DisplayDialog("웨이브 삭제",
                        $"W{selectedWave + 1:00}과 안의 모든 편성을 삭제할까요? Ctrl+Z로 되돌릴 수 있습니다.", "삭제", "취소"))
                {
                    waves.DeleteArrayElementAtIndex(selectedWave);
                    CommitStructure();
                }
                MoveButton(waves, selectedWave, -1, true);
                MoveButton(waves, selectedWave, 1, true);
            }
            EditorGUILayout.LabelField("손잡이를 드래그해 순서 변경", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void DrawWaveRow(Rect rect, int index, bool active, bool focused)
    {
        if (asset.waves == null || index >= asset.waves.Count) return;
        var wave = asset.waves[index];
        bool selected = index == selectedWave;
        if (selected) EditorGUI.DrawRect(rect, UIPalette.OutlineFocus);
        var style = selected ? selectedWaveStyle : waveStyle;
        string marker = issues.Any(issue => issue.Wave == index) ? " !" : "";
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 26), $"W{index + 1:00}  {wave?.note}{marker}", style);
        GUI.Label(new Rect(rect.x, rect.y + 26, rect.width, 24),
            $"{wave?.factions?.Count ?? 0} 팩션 · {StageWavesEditing.RangeLabel(wave)}", style);
    }

    private void DrawWaveDetails()
    {
        var waves = data.FindProperty("waves");
        if (waves.arraySize == 0)
        {
            EditorGUILayout.HelpBox("+ 웨이브를 누르면 고블린·언데드 후보 그룹이 하나씩 만들어집니다. 유닛을 추가해 편성을 시작하세요.", MessageType.Info);
            return;
        }
        selectedWave = Mathf.Clamp(selectedWave, 0, waves.arraySize - 1);
        var wave = waves.GetArrayElementAtIndex(selectedWave);
        EditorGUILayout.LabelField($"W{selectedWave + 1:00}  ·  출전 {StageWavesEditing.RangeLabel(asset.waves[selectedWave])}", EditorStyles.largeLabel);
        EditorGUILayout.PropertyField(wave.FindPropertyRelative("note"), new GUIContent("웨이브 메모"));
        EditorGUILayout.HelpBox("각 팩션에서 후보 그룹 1개만 같은 확률로 선택합니다. 유닛은 표의 위 → 아래 순서로 출전합니다.", MessageType.None);
        var factions = wave.FindPropertyRelative("factions");
        for (int f = 0; f < factions.arraySize; f++) DrawFaction(factions, f);
        using (new EditorGUI.DisabledScope(factions.arraySize >= StageWaves.MaxFactions))
        {
            if (GUILayout.Button("+ 팩션 추가"))
            {
                int next = Enumerable.Range(0, StageData.FactionCount).FirstOrDefault(id =>
                    !Enumerable.Range(0, factions.arraySize).Any(i => factions.GetArrayElementAtIndex(i).FindPropertyRelative("faction").intValue == id));
                StageWavesEditing.AddFaction(factions, next);
                CommitStructure();
            }
        }
        EditorGUILayout.Space(10);
    }

    private void DrawFaction(SerializedProperty factions, int index)
    {
        var faction = factions.GetArrayElementAtIndex(index);
        var id = faction.FindPropertyRelative("faction");
        var groups = faction.FindPropertyRelative("groups");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect stripe = GUILayoutUtility.GetRect(5, 23, GUILayout.Width(5));
                EditorGUI.DrawRect(stripe, id.intValue >= 0 && id.intValue < StageData.FactionCount
                    ? StageData.FactionColors[id.intValue] : UIPalette.ButtonDanger);
                GUILayout.Label("팩션", GUILayout.Width(30));
                Rect popup = GUILayoutUtility.GetRect(100, 22, GUILayout.ExpandWidth(true));
                if (GUI.Button(popup, StageWavesEditing.FactionName(id.intValue), EditorStyles.popup))
                    ShowFactionMenu(popup, factions, index);
                GUILayout.Label($"후보 {groups.arraySize}개", GUILayout.Width(72));
                if (GUILayout.Button("팩션 삭제", GUILayout.Width(76)) && EditorUtility.DisplayDialog("팩션 삭제",
                        $"{StageWavesEditing.FactionName(id.intValue)}의 모든 후보를 삭제할까요?", "삭제", "취소"))
                {
                    factions.DeleteArrayElementAtIndex(index);
                    CommitStructure();
                }
            }
            for (int g = 0; g < groups.arraySize; g++) DrawGroup(groups, g, id.intValue);
            if (GUILayout.Button("+ 후보 그룹"))
            {
                StageWavesEditing.AddGroup(groups);
                CommitStructure();
            }
        }
        EditorGUILayout.Space(5);
    }

    private void ShowFactionMenu(Rect rect, SerializedProperty factions, int index)
    {
        var menu = new GenericMenu();
        string path = factions.GetArrayElementAtIndex(index).FindPropertyRelative("faction").propertyPath;
        var used = Enumerable.Range(0, factions.arraySize).Where(i => i != index)
            .Select(i => factions.GetArrayElementAtIndex(i).FindPropertyRelative("faction").intValue).ToHashSet();
        for (int f = 0; f < StageData.FactionCount; f++)
        {
            int value = f;
            var label = new GUIContent(StageData.FactionNames[f] + (used.Contains(f) ? " (이미 사용 중)" : ""));
            if (used.Contains(f)) menu.AddDisabledItem(label);
            else menu.AddItem(label, data.FindProperty(path).intValue == f, () => SetInt(path, value));
        }
        menu.DropDown(rect);
    }

    private void SetInt(string path, int value)
    {
        if (asset == null || EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.IsOpenForEdit(asset)) return;
        using var edit = new SerializedObject(asset);
        var property = edit.FindProperty(path);
        if (property == null) return;
        property.intValue = value;
        edit.ApplyModifiedProperties();
        Rebuild();
    }

    private void DrawGroup(SerializedProperty groups, int index, int faction)
    {
        var group = groups.GetArrayElementAtIndex(index);
        using (new EditorGUILayout.VerticalScope(groupStyle))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"후보 {index + 1}  ·  {100f / groups.arraySize:0.#}%", EditorStyles.boldLabel, GUILayout.Width(120));
                EditorGUILayout.PropertyField(group.FindPropertyRelative("note"), GUIContent.none);
                MoveButton(groups, index, -1);
                MoveButton(groups, index, 1);
                if (GUILayout.Button(new GUIContent("복제", "이 후보의 유닛과 수량을 복제합니다."), GUILayout.Width(40)))
                {
                    StageWavesEditing.Duplicate(groups, index);
                    CommitStructure();
                }
                if (GUILayout.Button("삭제", GUILayout.Width(40)) && EditorUtility.DisplayDialog("후보 그룹 삭제",
                        $"후보 {index + 1}을 삭제할까요? Ctrl+Z로 되돌릴 수 있습니다.", "삭제", "취소"))
                {
                    groups.DeleteArrayElementAtIndex(index);
                    CommitStructure();
                }
            }
            var enemies = group.FindPropertyRelative("enemies");
            GetUnitList(enemies, faction).DoLayoutList();
            long total = 0;
            for (int i = 0; i < enemies.arraySize; i++)
                total += Mathf.Max(0, enemies.GetArrayElementAtIndex(i).FindPropertyRelative("count").intValue);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"이 후보 선택 시 {total:N0}명", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ 유닛", GUILayout.Width(70)))
                {
                    StageWavesEditing.AddUnit(enemies);
                    CommitStructure();
                }
            }
        }
    }

    private ReorderableList GetUnitList(SerializedProperty enemies, int faction)
    {
        string path = enemies.propertyPath;
        if (unitLists.TryGetValue(path, out var cached)) return cached;
        var list = new ReorderableList(data, enemies, true, true, false, false) { elementHeight = 27, headerHeight = 23 };
        list.drawHeaderCallback = rect =>
        {
            GUI.Label(new Rect(rect.x + 12, rect.y, rect.width - 180, rect.height), "출전 순서 / 유닛 (이름·ID 검색)");
            GUI.Label(new Rect(rect.xMax - 150, rect.y, 50, rect.height), "수량");
        };
        list.drawElementCallback = (rect, index, active, focused) => DrawUnitRow(rect, enemies, index, faction);
        list.onReorderCallback = _ => CommitStructure();
        list.drawNoneElementCallback = rect => GUI.Label(rect, "+ 유닛을 눌러 후보를 편성하세요.", EditorStyles.centeredGreyMiniLabel);
        unitLists.Add(path, list);
        return list;
    }

    private void DrawUnitRow(Rect rect, SerializedProperty enemies, int index, int faction)
    {
        var entry = enemies.GetArrayElementAtIndex(index);
        var id = entry.FindPropertyRelative("unitId");
        var count = entry.FindPropertyRelative("count");
        rect.y += 3;
        rect.height = 20;
        GUI.Label(new Rect(rect.x, rect.y, 25, 20), $"{index + 1}", EditorStyles.miniLabel);
        var unit = units.FirstOrDefault(u => u.Id == id.stringValue);
        string label = string.IsNullOrWhiteSpace(id.stringValue) ? "유닛 선택…" : unit?.Label ?? $"⚠ {id.stringValue}";
        Rect pickerRect = new(rect.x + 25, rect.y, rect.width - 187, 20);
        using (new EditorGUI.DisabledScope(catalog == null))
        {
            if (GUI.Button(pickerRect, new GUIContent(label, "이 팩션의 유닛을 이름 또는 ID로 검색합니다. 기존 ID는 선택하기 전까지 보존합니다."), EditorStyles.popup))
            {
                string propertyPath = id.propertyPath;
                StageWaves target = asset;
                picker = new UnitPicker(pickerState, units.Where(u => !string.IsNullOrWhiteSpace(u.Id))
                    .GroupBy(u => u.Id).Where(g => g.Count() == 1).Select(g => g.First())
                    .Where(u => u.Faction == faction).ToList(), selected =>
                {
                    if (target == null || EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.IsOpenForEdit(target)) return;
                    using var edit = new SerializedObject(target);
                    var property = edit.FindProperty(propertyPath);
                    if (property == null) return;
                    property.stringValue = selected;
                    edit.ApplyModifiedProperties();
                    Rebuild();
                });
                picker.Show(pickerRect);
            }
        }
        Rect countRect = new(rect.xMax - 155, rect.y, 50, 20);
        EditorGUI.BeginChangeCheck();
        int value = EditorGUI.IntField(countRect, count.intValue);
        if (EditorGUI.EndChangeCheck()) count.intValue = Mathf.Max(0, value);
        using (new EditorGUI.DisabledScope(index == 0))
            if (GUI.Button(new Rect(rect.xMax - 100, rect.y, 24, 20), new GUIContent("↑", "먼저 출전")))
            { enemies.MoveArrayElement(index, index - 1); CommitStructure(); }
        using (new EditorGUI.DisabledScope(index == enemies.arraySize - 1))
            if (GUI.Button(new Rect(rect.xMax - 75, rect.y, 24, 20), new GUIContent("↓", "나중에 출전")))
            { enemies.MoveArrayElement(index, index + 1); CommitStructure(); }
        if (GUI.Button(new Rect(rect.xMax - 50, rect.y, 24, 20), new GUIContent("+", "이 행 복제")))
        { StageWavesEditing.Duplicate(enemies, index); CommitStructure(); }
        if (GUI.Button(new Rect(rect.xMax - 25, rect.y, 24, 20), new GUIContent("×", "이 행 삭제 · Ctrl+Z로 복구")))
        { enemies.DeleteArrayElementAtIndex(index); CommitStructure(); }
    }

    private void MoveButton(SerializedProperty array, int index, int direction, bool wave = false)
    {
        using (new EditorGUI.DisabledScope(index + direction < 0 || index + direction >= array.arraySize))
        {
            if (!GUILayout.Button(new GUIContent(direction < 0 ? "↑" : "↓", "순서 변경"), GUILayout.Width(24))) return;
            array.MoveArrayElement(index, index + direction);
            if (wave) selectedWave = index + direction;
            CommitStructure();
        }
    }

    private void DrawIssues()
    {
        int errors = issues.Count(i => i.Severity == MessageType.Error);
        int warnings = issues.Count - errors;
        showIssues = EditorGUILayout.Foldout(showIssues, issues.Count == 0 ? "검사 완료 · 문제 없음" :
            $"편성 검사 · 오류 {errors} / 경고 {warnings}  (클릭해서 펼치기)", true);
        if (!showIssues) return;
        using (var scroll = new EditorGUILayout.ScrollViewScope(issueScroll, GUILayout.MaxHeight(145)))
        {
            issueScroll = scroll.scrollPosition;
            foreach (var issue in issues)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(issue.Message, issue.Severity);
                    if (issue.Wave >= 0 && GUILayout.Button("이동", GUILayout.Width(42), GUILayout.Height(30)))
                        SelectWave(issue.Wave);
                }
            }
        }
    }

    private void Save()
    {
        if (asset == null) return;
        // All fields commit immediately; save only this asset, not unrelated dirty assets.
        GUI.FocusControl(null);
        data?.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(asset);
        Repaint();
    }

    private void CreateAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject("웨이브 에셋 만들기", "NewStage_Waves", "asset",
            "저장할 위치를 선택하세요.", "Assets/04_Data/04_Stages");
        if (string.IsNullOrEmpty(path)) return;
        var created = CreateInstance<StageWaves>();
        AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
        using (var edit = new SerializedObject(created))
        {
            StageWavesEditing.AddWave(edit.FindProperty("waves"));
            edit.ApplyModifiedPropertiesWithoutUndo();
        }
        StageData link = asset == null ? previewStage : null;
        if (link != null && AssetDatabase.IsOpenForEdit(link))
        {
            Undo.RecordObject(link, "스테이지에 웨이브 연결");
            link.waves = created;
            EditorUtility.SetDirty(link);
            AssetDatabase.SaveAssetIfDirty(link);
        }
        AssetDatabase.SaveAssetIfDirty(created);
        SetAsset(created, link);
        Selection.activeObject = created;
        EditorGUIUtility.PingObject(created);
    }

    private sealed class UnitPicker : AdvancedDropdown
    {
        private readonly List<StageWavesEditing.Unit> choices;
        private readonly System.Action<string> selected;
        public UnitPicker(AdvancedDropdownState state, List<StageWavesEditing.Unit> choices, System.Action<string> selected)
            : base(state)
        {
            this.choices = choices;
            this.selected = selected;
            minimumSize = new Vector2(380, 300);
        }
        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("유닛 검색 · 이름 / ID");
            for (int i = 0; i < choices.Count; i++)
                root.AddChild(new AdvancedDropdownItem(choices[i].Label) { id = i });
            if (choices.Count == 0)
                root.AddChild(new AdvancedDropdownItem("이 팩션의 유닛이 없습니다. 카탈로그를 확인하세요.") { enabled = false });
            return root;
        }
        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item.id >= 0 && item.id < choices.Count) selected(choices[item.id].Id);
        }
    }
}
