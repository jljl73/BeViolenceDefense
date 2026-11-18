using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseBoardView
{
    private const float CameraPanSpeed = 6f;
    private const float CameraDistance = 20f;
    private const float CameraFocusOffset = 0.9f;
    private const float ZoomStep = 0.9f;
    private const float ZoomResponse = 18f;
    private const float MaxZoomFrameTime = 0.05f;
    private const float ZoomSnapDistance = 0.0005f;
    private const float MinViewScale = 0.55f;
    private const float MaxViewScale = 1.45f;
    private const float TileSize = 0.94f;
    private const float RouteMarkerSize = 0.68f;
    private const float RouteLaneSpacing = 0.49f;
    private const float RouteMarkerHeight = 0.02f;
    private const float SpawnPadHeight = 0.09f;

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
    private readonly IReadOnlyList<EnemyFaction> factions;
    private readonly Sprite routeChevron;
    private readonly HashSet<Vector2Int> spawnCells;
    private readonly List<SpriteRenderer> routeMarkers = new();
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
    private readonly GameObject wallPrefab;
    private readonly Color fixedWallTint;
    private readonly Material attackBuffPreviewMaterial;
    private readonly Material moveSpeedBuffPreviewMaterial;
    private readonly MeshRenderer areaBuffPreview;
    private readonly Mesh areaBuffPreviewMesh;
    private Vector3 cameraFocus;
    private float viewScale = 1f;
    private float targetViewScale = 1f;
    private Vector2? zoomAnchorViewport;

    public IReadOnlyCollection<Vector2Int> PlannedEncounters { get; private set; } = System.Array.Empty<Vector2Int>();

    public DefenseBoardView(Transform root, Camera camera, Shader shader, DefenseGrid grid,
        IReadOnlyList<EnemyFaction> factions, Sprite routeChevron, BoardTheme theme)
    {
        this.root = root;
        this.camera = camera;
        this.shader = shader;
        this.factions = factions;
        this.routeChevron = routeChevron;
        spawnCells = new HashSet<Vector2Int>(grid.SpawnCells);
        // Aim slightly toward the near corner to leave room for the battle controls.
        Vector3 boardForward = root.InverseTransformDirection(camera.transform.forward);
        cameraFocus = grid.Center - Vector3.ProjectOnPlane(boardForward, Vector3.up).normalized * CameraFocusOffset;
        PositionCamera();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = theme.background;
        FitCamera();

        Material foundation = Palette(theme.foundation);
        tiles = new[] { Palette(theme.tileA), Palette(theme.tileB) };
        routeTile = Palette(theme.routeTile);
        encounterTile = Palette(theme.encounterTile);
        buildWall = Palette(theme.buildWall);
        Material fixedWall = Palette(theme.fixedWall);
        trim = Palette(theme.trim);
        wallPrefab = theme.wallPrefab;
        fixedWallTint = theme.fixedWallTint;
        Material core = Palette(CoreColor);

        Box("Board base", root, grid.Center - Vector3.up * 0.55f,
            new Vector3(DefenseGrid.Width, 0.6f, DefenseGrid.Depth), foundation);
        for (int x = 0; x < DefenseGrid.Width; x++)
        {
            for (int z = 0; z < DefenseGrid.Depth; z++)
            {
                floor[x, z] = Box($"Tile {x},{z}", root, new Vector3(x, -0.14f, z),
                    new Vector3(TileSize, 0.28f, TileSize), tiles[(x + z) % tiles.Length]).GetComponent<MeshRenderer>();
            }
        }

        foreach (Vector2Int cell in grid.FixedWalls)
            wallViews.Add(cell, CreateWall(cell, $"Fixed Wall {cell.x},{cell.y}", fixedWall));

        for (int i = 0; i < grid.SpawnCells.Count; i++)
        {
            EnemyFaction faction = factions[i];
            Vector3 position = DefenseGrid.CellPosition(grid.SpawnCells[i]);
            Box($"{faction.DisplayName} spawn pad", root, position + Vector3.up * 0.035f,
                new Vector3(0.9f, 0.07f, 0.9f), FactionMaterial(faction));
            Box("Spawn pad center", root, position + Vector3.up * 0.075f, new Vector3(0.55f, 0.03f, 0.55f), foundation);
        }

        CreateCore(grid, foundation, fixedWall, core);
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

    private void CreateCore(DefenseGrid grid, Material foundation, Material wall, Material core)
    {
        var model = new GameObject("Core").transform;
        model.SetParent(root, false);
        model.localPosition = grid.CorePosition;

        float width = grid.CoreSize.x - 0.04f;
        float depth = grid.CoreSize.y - 0.04f;
        const float rim = 0.16f;
        Box("Core base", model, Vector3.up * 0.1f, new Vector3(width, 0.2f, depth), foundation);
        Box("Core platform", model, Vector3.up * 0.26f, new Vector3(width - rim, 0.12f, depth - rim), trim);

        // The rim meshes are part of the core model and sit entirely within its footprint.
        float sideX = (width - rim) * 0.5f;
        float sideZ = (depth - rim) * 0.5f;
        Box("Core rim left", model, new Vector3(-sideX, 0.29f, 0f), new Vector3(rim, 0.26f, depth), wall);
        Box("Core rim right", model, new Vector3(sideX, 0.29f, 0f), new Vector3(rim, 0.26f, depth), wall);
        Box("Core rim front", model, new Vector3(0f, 0.29f, -sideZ), new Vector3(width - rim * 2f, 0.26f, rim), wall);
        Box("Core rim back", model, new Vector3(0f, 0.29f, sideZ), new Vector3(width - rim * 2f, 0.26f, rim), wall);

        Box("Core body", model, Vector3.up * 0.56f, new Vector3(0.86f, 0.52f, 0.86f), core);
        Transform crown = Box("Core crown", model, Vector3.up * 0.94f, new Vector3(0.58f, 0.24f, 0.58f), core);
        crown.localRotation = Quaternion.Euler(0, 45, 0);
    }

    public void FitCamera() => camera.orthographicSize = Mathf.Max(9.4f, 9.5f / camera.aspect) * viewScale;

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

        int markerCount = routes == null ? 0 : DrawRoutes(routes);
        for (int i = markerCount; i < routeMarkers.Count; i++)
            routeMarkers[i].enabled = false;

        foreach (Vector2Int cell in PlannedEncounters)
            floor[cell.x, cell.y].sharedMaterial = encounterTile;
    }

    private int DrawRoutes(IReadOnlyList<Vector3[]> routes)
    {
        var stepsByCell = new Dictionary<Vector2Int, List<(int FactionIndex, Vector3 Direction)>>();
        for (int factionIndex = 0; factionIndex < routes.Count; factionIndex++)
        {
            Vector3[] path = routes[factionIndex];
            // The last waypoint is the contact point on the core, not a floor tile.
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2Int cell = DefenseGrid.PositionToCell(path[i]);
                if (!stepsByCell.TryGetValue(cell, out var steps))
                {
                    steps = new List<(int, Vector3)>(factions.Count);
                    stepsByCell.Add(cell, steps);
                }
                steps.Add((factionIndex, (path[i + 1] - path[i]).normalized));
            }
        }

        int markerCount = 0;
        foreach (var (cell, steps) in stepsByCell)
        {
            floor[cell.x, cell.y].sharedMaterial = routeTile;
            if (routeChevron == null)
                continue;

            // Both arrows use one lateral axis so opposite headings stay in separate lanes.
            Vector3 side = Vector3.Cross(Vector3.up, steps[0].Direction);
            float height = RouteMarkerHeight + (spawnCells.Contains(cell) ? SpawnPadHeight : 0f);
            Vector3 center = DefenseGrid.CellPosition(cell) + Vector3.up * height;
            for (int i = 0; i < steps.Count; i++)
            {
                var (factionIndex, direction) = steps[i];
                float offset = (i - (steps.Count - 1) * 0.5f) * RouteLaneSpacing;
                SpriteRenderer marker = RouteMarker(markerCount++);
                marker.transform.localPosition = center + side * offset;
                marker.transform.localRotation = Quaternion.LookRotation(Vector3.down,
                    Vector3.Cross(Vector3.down, direction));
                marker.color = factions[factionIndex].Color;
                marker.enabled = true;
            }
        }

        return markerCount;
    }

    private SpriteRenderer RouteMarker(int index)
    {
        if (index < routeMarkers.Count)
            return routeMarkers[index];

        var marker = new GameObject("Route Chevron").AddComponent<SpriteRenderer>();
        marker.transform.SetParent(root, false);
        marker.transform.localScale = Vector3.one * RouteMarkerSize;
        marker.sprite = routeChevron;
        routeMarkers.Add(marker);
        return marker;
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

    private Material FactionMaterial(EnemyFaction faction)
    {
        if (!factionMaterials.TryGetValue(faction, out Material material))
        {
            material = Palette(faction.Color);
            factionMaterials.Add(faction, material);
        }
        return material;
    }

    public void AddEnemy(EnemyUnit enemy)
    {
        Material material = FactionMaterial(enemy.Faction);
        Transform model = new GameObject($"{enemy.Faction.DisplayName} enemy").transform;
        model.SetParent(root, false);
        model.localScale = Vector3.one * 0.75f;
        Box("Body", model, new Vector3(0, 0.29f, 0), new Vector3(0.42f, 0.43f, 0.38f), material);
        Transform head = Box("Head", model, new Vector3(0, 0.62f, 0), new Vector3(0.31f, 0.27f, 0.31f), material);
        if (enemy.FactionIndex == 0)
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
        if (wallPrefab != null)
        {
            Transform woodWall = Object.Instantiate(wallPrefab, root, false).transform;
            woodWall.name = label;
            woodWall.localPosition = DefenseGrid.CellPosition(cell);
            // 고정 벽은 배치한 벽과 구분되도록 어둡게 칠한다.
            if (material != buildWall)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", fixedWallTint);
                foreach (Renderer renderer in woodWall.GetComponentsInChildren<Renderer>())
                    renderer.SetPropertyBlock(block);
            }
            return woodWall;
        }

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
