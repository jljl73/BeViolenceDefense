using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseBoardView
{
    private const float CameraPanSpeed = 6f;
    private const float CameraDistance = 20f;
    private const float ZoomStep = 0.9f;
    private const float ZoomResponse = 18f;
    private const float MaxZoomFrameTime = 0.05f;
    private const float ZoomSnapDistance = 0.0005f;
    private const float MinViewScale = 0.55f;
    private const float MaxViewScale = 1.45f;

    private static readonly Plane BoardGround = new(Vector3.up, Vector3.zero);

    public static readonly Color32 CoreColor = new(255, 209, 112, 255);
    public static readonly Color32 MoveSpeedBuffColor = new(102, 218, 255, 255);
    public static readonly Color32 HealColor = new(153, 245, 160, 255);

    private sealed class EnemyView
    {
        public Transform Root;
        public Transform Head;
    }

    private readonly Transform root;
    private readonly Camera camera;
    private readonly Shader shader;
    private readonly List<Material> materials = new();
    private readonly Dictionary<Vector2Int, Transform> wallViews = new();
    private readonly Dictionary<EnemyUnit, EnemyView> enemyViews = new();
    private readonly Dictionary<EnemyFaction, Material> factionMaterials = new();
    private readonly MeshRenderer[,] floor = new MeshRenderer[DefenseGrid.Width, DefenseGrid.Depth];
    private readonly Material[] tiles;
    private readonly Material routeTile;
    private readonly Material encounterTile;
    private readonly Material buildWall;
    private readonly Material trim;
    private readonly Material attackBuffPreviewMaterial;
    private readonly Material moveSpeedBuffPreviewMaterial;
    private readonly MeshRenderer areaBuffPreview;
    private readonly Mesh areaBuffPreviewMesh;
    private Vector3 cameraFocus;
    private float viewScale = 1f;
    private float targetViewScale = 1f;
    private Vector2? zoomAnchorViewport;

    public IReadOnlyCollection<Vector2Int> PlannedEncounters { get; private set; } = System.Array.Empty<Vector2Int>();

    public DefenseBoardView(Transform root, Camera camera, Shader shader, DefenseGrid grid)
    {
        this.root = root;
        this.camera = camera;
        this.shader = shader;
        cameraFocus = grid.Center;
        PositionCamera();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(25, 33, 47, 255);
        FitCamera();

        Material foundation = Palette(new Color32(40, 54, 69, 255));
        tiles = new[]
        {
            Palette(new Color32(72, 91, 101, 255)),
            Palette(new Color32(81, 101, 107, 255))
        };
        routeTile = Palette(new Color32(104, 130, 130, 255));
        encounterTile = Palette(new Color32(221, 151, 76, 255));
        buildWall = Palette(new Color32(112, 143, 182, 255));
        Material fixedWall = Palette(new Color32(97, 120, 136, 255));
        trim = Palette(new Color32(178, 198, 199, 255));
        Material core = Palette(CoreColor);

        Box("Board base", root, grid.Center - Vector3.up * 0.55f,
            new Vector3(DefenseGrid.Width, 0.6f, DefenseGrid.Depth), foundation);
        for (int x = 0; x < DefenseGrid.Width; x++)
        {
            for (int z = 0; z < DefenseGrid.Depth; z++)
            {
                floor[x, z] = Box($"Tile {x},{z}", root, new Vector3(x, -0.14f, z),
                    new Vector3(0.97f, 0.28f, 0.97f), tiles[(x + z) % tiles.Length]).GetComponent<MeshRenderer>();
            }
        }

        foreach (Vector2Int cell in grid.FixedWalls)
            wallViews.Add(cell, CreateWall(cell, $"Fixed Wall {cell.x},{cell.y}", fixedWall));

        foreach (Vector2Int cell in grid.SpawnCells)
        {
            Vector3 position = DefenseGrid.CellPosition(cell);
            Box("Spawn pad", root, position + Vector3.up * 0.035f, new Vector3(0.9f, 0.07f, 0.9f), trim);
            Box("Spawn pad center", root, position + Vector3.up * 0.075f, new Vector3(0.55f, 0.03f, 0.55f), foundation);
        }

        Box("Core plinth", root, grid.CorePosition + Vector3.up * 0.13f, new Vector3(0.94f, 0.26f, 0.94f), foundation);
        Box("Core body", root, grid.CorePosition + Vector3.up * 0.55f, new Vector3(0.6f, 0.6f, 0.6f), core);
        Transform crown = Box("Core crown", root, grid.CorePosition + Vector3.up * 0.93f,
            new Vector3(0.42f, 0.24f, 0.42f), core);
        crown.localRotation = Quaternion.Euler(0, 45, 0);
        areaBuffPreviewMesh = CreateAreaBuffPreviewMesh();
        var preview = new GameObject("Area Buff Range", typeof(MeshFilter), typeof(MeshRenderer));
        preview.transform.SetParent(root, false);
        preview.GetComponent<MeshFilter>().sharedMesh = areaBuffPreviewMesh;
        areaBuffPreview = preview.GetComponent<MeshRenderer>();
        attackBuffPreviewMaterial = core;
        moveSpeedBuffPreviewMaterial = Palette(MoveSpeedBuffColor);
        areaBuffPreview.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        areaBuffPreview.receiveShadows = false;
        areaBuffPreview.enabled = false;
    }

    public void FitCamera() => camera.orthographicSize = Mathf.Max(6.8f, 7.8f / camera.aspect) * viewScale;

    public void QueueZoom(float scrollDelta, Vector2 pointer)
    {
        targetViewScale = Mathf.Clamp(targetViewScale * Mathf.Pow(ZoomStep, scrollDelta), MinViewScale, MaxViewScale);
        zoomAnchorViewport = TryPickCell(pointer, out Vector2Int cell) && DefenseGrid.InBounds(cell)
            ? (Vector2)camera.ScreenToViewportPoint(pointer)
            : null;
    }

    public void AdvanceZoom(float deltaTime)
    {
        if (viewScale == targetViewScale || deltaTime <= 0f)
            return;

        float blend = 1f - Mathf.Exp(-ZoomResponse * Mathf.Min(deltaTime, MaxZoomFrameTime));
        float newScale = Mathf.Lerp(viewScale, targetViewScale, blend);
        if (Mathf.Abs(targetViewScale - newScale) < ZoomSnapDistance)
            newScale = targetViewScale;

        if (zoomAnchorViewport is Vector2 viewport)
        {
            Ray ray = BoardRay(camera.ViewportToScreenPoint(viewport));
            if (BoardGround.Raycast(ray, out float distance))
            {
                // The orthographic ground offset scales with the view. Translate by the portion the zoom removes.
                Vector3 offset = (ray.GetPoint(distance) - cameraFocus) * (1f - newScale / viewScale);
                cameraFocus.x += offset.x;
                cameraFocus.z += offset.z;
                PositionCamera();
            }
        }

        viewScale = newScale;
        FitCamera();
    }

    public void PanCamera(Vector2 input, float deltaTime)
    {
        Vector2 direction = Vector2.ClampMagnitude(input, 1f);
        Quaternion yaw = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
        Vector3 offset = root.InverseTransformDirection(yaw * new Vector3(direction.x, 0f, direction.y));
        cameraFocus.x = PanAxis(cameraFocus.x, offset.x * CameraPanSpeed * deltaTime, DefenseGrid.Width - 1f);
        cameraFocus.z = PanAxis(cameraFocus.z, offset.z * CameraPanSpeed * deltaTime, DefenseGrid.Depth - 1f);
        PositionCamera();
    }

    // An anchored zoom-out can put the focus past an edge. Pan inward from there without snapping.
    private static float PanAxis(float focus, float delta, float max) =>
        Mathf.Clamp(focus + delta, Mathf.Min(0f, focus), Mathf.Max(max, focus));

    private void PositionCamera() =>
        camera.transform.position = root.TransformPoint(cameraFocus) - camera.transform.forward * CameraDistance;

    public void ShowPlanningTiles(IReadOnlyList<Vector3[]> routes = null,
        IReadOnlyCollection<Vector2Int> encounters = null)
    {
        PlannedEncounters = encounters ?? System.Array.Empty<Vector2Int>();
        for (int x = 0; x < DefenseGrid.Width; x++)
            for (int z = 0; z < DefenseGrid.Depth; z++)
                floor[x, z].sharedMaterial = tiles[(x + z) % tiles.Length];

        if (routes != null)
            foreach (Vector3[] path in routes)
                foreach (Vector3 point in path)
                    floor[(int)point.x, (int)point.z].sharedMaterial = routeTile;

        foreach (Vector2Int cell in PlannedEncounters)
            floor[cell.x, cell.y].sharedMaterial = encounterTile;
    }

    public void PlaceWall(Vector2Int cell) => wallViews.Add(cell, CreateWall(cell, $"Wall {cell.x},{cell.y}", buildWall));

    public void RemoveWall(Vector2Int cell)
    {
        if (wallViews.Remove(cell, out Transform wall))
            Object.Destroy(wall.gameObject);
    }

    public bool TryPickCell(Vector2 pointer, out Vector2Int cell) => TryPickCell(BoardRay(pointer), out cell);

    public bool TryPickBoardPoint(Vector2 pointer, out Vector3 point)
    {
        Ray ray = BoardRay(pointer);
        point = default;
        if (!BoardGround.Raycast(ray, out float distance))
            return false;

        point = ray.GetPoint(distance);
        return point.x >= -0.5f && point.x < DefenseGrid.Width - 0.5f &&
            point.z >= -0.5f && point.z < DefenseGrid.Depth - 0.5f;
    }

    public void ShowAreaBuffPreview(DefenseBattle.Spell? spell, Vector3? center)
    {
        areaBuffPreview.enabled = center.HasValue;
        if (!center.HasValue)
            return;

        Material material = spell == DefenseBattle.Spell.MoveSpeedBuff
            ? moveSpeedBuffPreviewMaterial
            : attackBuffPreviewMaterial;
        if (areaBuffPreview.sharedMaterial != material)
            areaBuffPreview.sharedMaterial = material;

        areaBuffPreview.transform.localPosition = center.Value + Vector3.up * 0.04f;
    }

    private static Mesh CreateAreaBuffPreviewMesh()
    {
        const int segments = 64;
        const float radius = DefenseBattle.AreaBuffRadius;
        const float thickness = 0.075f;
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (2f * Mathf.PI / segments);
            Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            int inner = i * 2;
            int next = (i + 1) % segments * 2;
            vertices[inner] = direction * (radius - thickness);
            vertices[inner + 1] = direction * radius;

            int index = i * 6;
            triangles[index] = inner;
            triangles[index + 1] = next;
            triangles[index + 2] = inner + 1;
            triangles[index + 3] = inner + 1;
            triangles[index + 4] = next;
            triangles[index + 5] = next + 1;
        }

        var mesh = new Mesh { name = "Area Buff Range", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        return mesh;
    }

    private Ray BoardRay(Vector2 pointer)
    {
        Ray worldRay = camera.ScreenPointToRay(pointer);
        return new Ray(root.InverseTransformPoint(worldRay.origin), root.InverseTransformVector(worldRay.direction));
    }

    private bool TryPickCell(Ray localRay, out Vector2Int cell)
    {
        float nearest = float.PositiveInfinity;
        cell = default;
        foreach (Vector2Int wall in wallViews.Keys)
        {
            if (DefenseGrid.WallBounds(wall).IntersectRay(localRay, out float depth) && depth < nearest)
            {
                cell = wall;
                nearest = depth;
            }
        }

        if (nearest < float.PositiveInfinity)
            return true;

        if (!BoardGround.Raycast(localRay, out float distance))
            return false;

        Vector3 point = localRay.GetPoint(distance);
        cell = DefenseGrid.PositionToCell(point);
        return true;
    }

    public Vector3 ScreenPoint(EnemyUnit enemy, float height) => ScreenPoint(enemy.Position + Vector3.up * height);

    public Vector3 ScreenPoint(Vector3 boardPosition) =>
        camera.WorldToScreenPoint(root.TransformPoint(boardPosition));

    public void AddEnemy(EnemyUnit enemy)
    {
        if (!factionMaterials.TryGetValue(enemy.Faction, out Material material))
        {
            material = Palette(enemy.Faction.Color);
            factionMaterials.Add(enemy.Faction, material);
        }

        Transform model = new GameObject($"{enemy.Faction.DisplayName} enemy").transform;
        model.SetParent(root, false);
        Box("Body", model, new Vector3(0, 0.29f, 0), new Vector3(0.42f, 0.43f, 0.38f), material);
        Transform head = Box("Head", model, new Vector3(0, 0.62f, 0), new Vector3(0.31f, 0.27f, 0.31f), material);
        if (enemy.SpawnOrder % 2 == 0)
        {
            Box("Crest", head, new Vector3(0, 0.65f, 0), new Vector3(0.28f, 0.34f, 0.7f), trim);
        }
        else
        {
            Box("Shoulder", model, new Vector3(-0.21f, 0.43f, 0), new Vector3(0.15f, 0.14f, 0.25f), trim);
            Box("Shoulder", model, new Vector3(0.21f, 0.43f, 0), new Vector3(0.15f, 0.14f, 0.25f), trim);
        }

        enemyViews.Add(enemy, new EnemyView { Root = model, Head = head });
        SyncEnemy(enemy);
    }

    public void RemoveEnemy(EnemyUnit enemy)
    {
        if (enemyViews.Remove(enemy, out EnemyView view))
            Object.Destroy(view.Root.gameObject);
    }

    public void SyncEnemy(EnemyUnit enemy)
    {
        EnemyView view = enemyViews[enemy];
        view.Root.localPosition = enemy.Position;
        view.Root.localRotation = Quaternion.LookRotation(enemy.Heading);
        view.Head.localPosition = new Vector3(0, 0.62f, enemy.AttackFlash * 0.7f);
    }

    public void ReleaseMaterials()
    {
        Object.Destroy(areaBuffPreviewMesh);
        foreach (Material material in materials)
            Object.Destroy(material);
    }

    private Transform CreateWall(Vector2Int cell, string label, Material material)
    {
        var wall = new GameObject(label).transform;
        wall.SetParent(root, false);
        wall.localPosition = DefenseGrid.CellPosition(cell);
        Box("Block", wall, Vector3.up * 0.34f, new Vector3(DefenseGrid.WallWidth, 0.68f, DefenseGrid.WallWidth), material);
        Box("Cap", wall, Vector3.up * 0.73f, new Vector3(0.86f, 0.12f, 0.86f), trim);
        return wall;
    }

    private Material Palette(Color color)
    {
        var material = new Material(shader) { color = color };
        material.SetFloat("_Smoothness", 0.15f);
        materials.Add(material);
        return material;
    }

    private static Transform Box(string label, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = label;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = size;
        Object.Destroy(cube.GetComponent<BoxCollider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cube.transform;
    }
}
