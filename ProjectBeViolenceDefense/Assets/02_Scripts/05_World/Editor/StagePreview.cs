using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Stage Editor의 게임 화면 미리보기. 게임과 같은 DefenseBoardView로 보드를 만들고, 열린 씬의
/// Voxel Defense Prototype(카메라 각도·보드 테마·진영 색)과 방향광·Global Volume을 따라 그린다.
/// </summary>
internal sealed class StagePreview
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColor = Shader.PropertyToID("_Color");

    private PreviewRenderUtility utility;
    private Material highlightMaterial;
    private Transform root;
    private DefenseBoardView view;
    private (Vector3 Focus, float Scale)? cameraState;
    private Transform ghost;
    private GameObject ghostPrefab;
    private int ghostRotation = -1;
    private Vector2Int ghostSize;
    private Transform highlight;

    // 미리보기 설정을 어디서 읽었는지. 씬에 Voxel Defense Prototype이 없으면 기본값을 쓴다.
    public string Source { get; private set; }
    public string Error { get; private set; }

    public void Rebuild(StageData stage)
    {
        EnsureUtility();
        if (view != null)
            cameraState = view.CameraState;
        Clear();
        Error = null;

        VoxelDefensePrototype prototype = Object.FindAnyObjectByType<VoxelDefensePrototype>(FindObjectsInactive.Include);
        SerializedObject settings = prototype != null ? new SerializedObject(prototype) : null;
        Source = prototype != null ? $"{prototype.gameObject.scene.name} / {prototype.name}" : null;
        ApplyCamera(settings);

        var theme = settings?.FindProperty("boardTheme").boxedValue as BoardTheme ?? new BoardTheme();
        var shader = settings?.FindProperty("voxelShader").objectReferenceValue as Shader;
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        var chevron = settings?.FindProperty("routeChevron").objectReferenceValue as Sprite;
        string[] slots = { "greenHorde", "undead", "beasts", "dragonAndDemon" };
        var factions = new EnemyFaction[StageData.FactionCount];
        for (int i = 0; i < factions.Length; i++)
            factions[i] = Faction(settings, slots[i], StageData.FactionNames[i], StageData.FactionColors[i]);

        root = new GameObject("Stage Preview").transform;
        utility.AddSingleGO(root.gameObject);
        try
        {
            DefenseGrid grid = stage.CreateGrid();
            view = new DefenseBoardView(root, utility.camera, shader, grid, factions, chevron, theme,
                catalog: null, stage: stage);
            // 준비 단계 화면처럼 경로와 예상 교전 타일을 보여 준다.
            if (stage.Validate(grid) == null)
                view.ShowPlanningTiles(grid.Routes, new DefenseBattle(grid, factions).ForecastEncounterCells());
            if (cameraState is { } state)
                view.CameraState = state;
        }
        catch (System.Exception exception)
        {
            Error = exception.Message;
            Clear();
        }
    }

    public void Render(Rect rect)
    {
        if (utility == null || view == null || rect.width < 2f || rect.height < 2f)
            return;
        utility.BeginPreview(rect, GUIStyle.none);
        view.FitCamera();
        utility.Render(true);
        GUI.DrawTexture(rect, utility.EndPreview(), ScaleMode.StretchToFill, false);
    }

    public bool TryPickCell(Rect rect, Vector2 mouse, out Vector2Int cell)
    {
        cell = default;
        return view != null && view.TryPickCell(Pointer(rect, mouse), out cell);
    }

    public void Zoom(Rect rect, Vector2 mouse, float scrollDelta)
    {
        if (view == null)
            return;
        view.QueueZoom(scrollDelta, Pointer(rect, mouse));
        for (int i = 0; i < 40; i++)
            view.AdvanceZoom(0.05f);
    }

    public void Drag(Rect rect, Vector2 previousMouse, Vector2 mouse) =>
        view?.DragCamera(Pointer(rect, previousMouse), Pointer(rect, mouse));

    // 놓을 칸 표시와, 장애물 도구면 실제 프리팹(놓을 수 없으면 붉게)을 보여 준다.
    public void ShowBrush(RectInt footprint, Color color, bool allowed, bool obstacle, GameObject prefab, int rotation)
    {
        if (root == null)
            return;
        if (highlight == null)
        {
            highlight = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            DefenseBoardView.Release(highlight.GetComponent<Collider>());
            highlight.name = "Brush Footprint";
            highlight.SetParent(root, false);
            highlight.GetComponent<MeshRenderer>().sharedMaterial = highlightMaterial;
        }
        highlight.gameObject.SetActive(true);
        highlight.localPosition = new Vector3(footprint.x + (footprint.width - 1) * 0.5f, 0.03f,
            footprint.y + (footprint.height - 1) * 0.5f);
        highlight.localScale = new Vector3(footprint.width - 0.04f, 0.04f, footprint.height - 0.04f);
        highlight.GetComponent<MeshRenderer>().SetPropertyBlock(Tint(color));

        if (!obstacle)
        {
            HideGhost();
            return;
        }

        if (ghost == null || ghostPrefab != prefab || ghostRotation != rotation || ghostSize != footprint.size)
        {
            HideGhost();
            DestroyGhost();
            var placement = new StageData.Obstacle { prefab = prefab, cell = footprint.position, size = footprint.size, rotation = rotation };
            if (prefab != null)
            {
                ghost = Object.Instantiate(prefab, root, false).transform;
                DefenseBoardView.FitObstacle(ghost, placement);
            }
            else
            {
                ghost = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                DefenseBoardView.Release(ghost.GetComponent<Collider>());
                ghost.SetParent(root, false);
                ghost.localScale = new Vector3(DefenseGrid.WallWidth, DefenseGrid.WallHeight, DefenseGrid.WallWidth);
            }
            ghost.name = "Brush Ghost";
            ghostPrefab = prefab;
            ghostRotation = rotation;
            ghostSize = footprint.size;
        }

        ghost.gameObject.SetActive(true);
        Vector3 center = new StageData.Obstacle { cell = footprint.position, size = footprint.size }.Center;
        ghost.localPosition = prefab != null ? center : center + Vector3.up * (DefenseGrid.WallHeight * 0.5f);
        foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>())
            renderer.SetPropertyBlock(allowed ? null : Tint(new Color(1f, 0.35f, 0.3f, 1f)));
    }

    public void HideBrush()
    {
        if (highlight != null)
            highlight.gameObject.SetActive(false);
        HideGhost();
    }

    public void Dispose()
    {
        Clear();
        utility?.Cleanup();
        utility = null;
        DefenseBoardView.Release(highlightMaterial);
        highlightMaterial = null;
    }

    private void EnsureUtility()
    {
        if (utility != null)
            return;
        utility = new PreviewRenderUtility();
        // URP는 Preview 카메라에 후처리(톤매핑·블룸·비네트)를 적용하지 않으므로 게임 카메라로 그린다.
        utility.camera.cameraType = CameraType.Game;
        // 게임처럼 HDR로 그린 뒤 톤매핑해야 색이 같다.
        utility.camera.allowHDR = true;
        highlightMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };

        Light sceneLight = null;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional && light.enabled)
                sceneLight = light;
        if (sceneLight != null)
        {
            Light main = utility.lights[0];
            main.transform.rotation = sceneLight.transform.rotation;
            main.color = sceneLight.color;
            main.intensity = sceneLight.intensity;
            main.shadows = sceneLight.shadows;
            utility.lights[1].intensity = 0f;
        }
        utility.ambientColor = RenderSettings.ambientMode == AmbientMode.Flat
            ? RenderSettings.ambientLight
            : RenderSettings.ambientSkyColor;

        // 씬의 Global Volume을 복사해 같은 후처리(톤매핑·블룸 등)를 건다.
        foreach (Volume volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (!volume.isGlobal || !volume.enabled)
                continue;
            utility.AddSingleGO(Object.Instantiate(volume.gameObject));
            break;
        }
    }

    private void ApplyCamera(SerializedObject settings)
    {
        Camera camera = utility.camera;
        var source = settings?.FindProperty("boardCamera").objectReferenceValue as Camera;
        camera.transform.rotation = source != null ? source.transform.rotation : Quaternion.Euler(55f, -45f, 0f);
        camera.orthographic = true;
        camera.nearClipPlane = source != null ? source.nearClipPlane : 0.3f;
        camera.farClipPlane = source != null ? source.farClipPlane : 1000f;
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (source != null && source.TryGetComponent(out UniversalAdditionalCameraData sourceData))
        {
            data.renderPostProcessing = sourceData.renderPostProcessing;
            data.antialiasing = sourceData.antialiasing;
        }
        else
        {
            data.renderPostProcessing = true;
        }
    }

    private static EnemyFaction Faction(SerializedObject settings, string slot, string fallbackName, Color fallbackColor)
    {
        SerializedProperty property = settings?.FindProperty(slot);
        if (property == null)
            return new EnemyFaction(fallbackName, fallbackColor);
        string name = GameText.Get(property.FindPropertyRelative("displayNameKey").stringValue);
        return new EnemyFaction(string.IsNullOrEmpty(name) ? fallbackName : name,
            property.FindPropertyRelative("color").colorValue);
    }

    // 미리보기 사각형 안의 마우스 위치를 카메라 픽셀 좌표로 바꾼다 (게임 입력과 같은 좌표계).
    private Vector2 Pointer(Rect rect, Vector2 mouse) => new(
        (mouse.x - rect.x) / rect.width * utility.camera.pixelWidth,
        (1f - (mouse.y - rect.y) / rect.height) * utility.camera.pixelHeight);

    private static MaterialPropertyBlock Tint(Color color)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColor, color);
        block.SetColor(LegacyColor, color);
        return block;
    }

    private void HideGhost()
    {
        if (ghost != null)
            ghost.gameObject.SetActive(false);
    }

    private void DestroyGhost()
    {
        if (ghost != null)
            Object.DestroyImmediate(ghost.gameObject);
        ghost = null;
        ghostPrefab = null;
        ghostRotation = -1;
    }

    private void Clear()
    {
        view?.ReleaseMaterials();
        view = null;
        if (root != null)
            Object.DestroyImmediate(root.gameObject);
        root = null;
        ghost = null;
        ghostPrefab = null;
        ghostRotation = -1;
        highlight = null;
        if (utility == null)
            return;
        // DefenseBoardView가 카메라 아래에 붙인 조준 어둡게 판 등.
        var cameraChildren = new List<GameObject>();
        foreach (Transform child in utility.camera.transform)
            cameraChildren.Add(child.gameObject);
        foreach (GameObject child in cameraChildren)
            Object.DestroyImmediate(child);
    }
}
