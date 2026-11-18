using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseBoardView
{
    private const float CameraPanSpeed = 6f;
    private const float CameraDistance = 20f;
    private const float CameraFocusOffset = 0.9f;
    private const float CameraPanInset = 4f;
    private const float ZoomStep = 0.9f;
    private const float ZoomResponse = 18f;
    private const float MaxZoomFrameTime = 0.05f;
    private const float ZoomSnapDistance = 0.0005f;
    private const float MinViewScale = 0.35f;
    private const float MaxViewScale = 1.45f;
    private const float TileHeight = 0.28f;
    private const float RouteMarkerSize = 0.68f;
    private const float RouteLaneSpacing = 0.49f;
    private const float RouteMarkerHeight = 0.02f;
    private const float SpawnPadHeight = 0.09f;
    private const float GoblinTurnSpeed = 1440f;
    private const float RunStopGrace = 0.08f;
    // Approximate planted-foot travel per second in the unscaled Run clip.
    private const float GoblinRunStrideSpeed = 1.55f;
    // 스킬 조준 중 화면 딤. 범위 표시는 딤보다 나중에 그려서 밝게 보이게 한다.
    private const float AimDimAlpha = 0.55f;
    private const float AimDimFadeTime = 0.12f;
    private const int AimDimSortingOrder = 40;
    private const int AimOverlayRenderQueue = 3100;
    // Run 상태가 없는 모델(좀비)은 Walk 클립으로 이동한다. 발이 미끄러지면 이 값을 조절한다.
    private const float WalkStrideSpeed = 0.9f;
    // 공격 애니메이션에서 무기가 닿는 지점 (0~1). 이펙트가 빠르거나 늦으면 이 값만 조절한다.
    private const float HitImpactTime = 0.45f;
    private const float HitHeight = 0.5f;
    private const float HitForwardOffset = 0.2f;
    private const int MaxHitEffects = 24;

    private static readonly Plane BoardGround = new(Vector3.up, Vector3.zero);
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int WalkState = Animator.StringToHash("Base Layer.Walk");
    private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
    private static readonly int AttackState = Animator.StringToHash("Base Layer.Attack");

    public static readonly Color32 CoreColor = new(255, 209, 112, 255);
    public static readonly Color32 MoveSpeedBuffColor = new(102, 218, 255, 255);

    private sealed class EnemyView
    {
        public Transform Root;
        public Transform Head;
        public Renderer[] Renderers;
        public Animator Animator;
        public int AnimationState;
        public int MoveState;
        public float StrideSpeed;
        public int AttacksPlayed;
        public int HitsSeen;
        public (EnemyUnit Target, bool Boosted, bool TargetArmored) PendingHit;
        public LineRenderer[] MoveSpeedTrails;
        public float StoppedTime;
        public Material PreviewMaterial;
        public bool PreviewEligible;
        public MeshRenderer PreviewRing;
        public LineRenderer PreviewOutline;
    }

    private readonly Transform root;
    private readonly Camera camera;
    private readonly Shader shader;
    private readonly IReadOnlyList<EnemyFaction> factions;
    private readonly Sprite routeChevron;
    private readonly HashSet<Vector2Int> spawnCells;
    private readonly List<SpriteRenderer> routeMarkers = new();
    private readonly List<Material> materials = new();
    private readonly Dictionary<Material, Material> aimOverlayMaterials = new();
    private readonly SpriteRenderer aimDim;
    private float aimDimAlpha;
    private readonly List<Mesh> meshes = new();
    private readonly Dictionary<Vector2Int, Transform> wallViews = new();
    private readonly Dictionary<EnemyUnit, EnemyView> enemyViews = new();
    private readonly List<HitEffect> hitEffects = new();
    private readonly SpellEffects spellEffects;
    private readonly SpellCastMarkers spellCastMarkers;
    private readonly MaterialPropertyBlock enemyTint = new();
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
    private readonly Material healPreviewMaterial;
    private readonly Material armorBuffPreviewMaterial;
    private readonly Material nearbyPreviewMaterial;
    private readonly Material attackOutlineMaterial;
    private readonly Material healOutlineMaterial;
    private readonly Material moveSpeedOutlineMaterial;
    private readonly Material armorOutlineMaterial;
    private readonly Material nearbyOutlineMaterial;
    private readonly MeshRenderer areaSpellPreview;
    private readonly Mesh areaSpellPreviewMesh;
    private readonly List<int> areaSpellTriangles = new(64 * 6);
    private readonly Mesh factionRingMesh;
    private readonly Mesh previewRingMesh;
    private int nextHitEffect;
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
        tiles = new[] { Palette(theme.tileA, theme.tileTexture), Palette(theme.tileB, theme.tileTexture) };
        routeTile = Palette(theme.routeTile, theme.tileTexture);
        encounterTile = Palette(theme.encounterTile, theme.tileTexture);
        buildWall = Palette(theme.buildWall);
        Material fixedWall = Palette(theme.fixedWall);
        trim = Palette(theme.trim);
        wallPrefab = theme.wallPrefab;
        fixedWallTint = theme.fixedWallTint;
        Material core = Palette(CoreColor);

        Box("Board base", root, grid.Center - Vector3.up * 0.55f,
            new Vector3(DefenseGrid.Width, 0.6f, DefenseGrid.Depth), foundation);
        float tileSize = 1f - theme.tileGap;
        var tileScale = new Vector3(tileSize, TileHeight, tileSize);
        Mesh tileMesh = theme.tileBevel > 0f ? Beveled("Board Tile", tileScale, theme.tileBevel) : null;
        for (int x = 0; x < DefenseGrid.Width; x++)
        {
            for (int z = 0; z < DefenseGrid.Depth; z++)
            {
                floor[x, z] = Box($"Tile {x},{z}", root, new Vector3(x, -TileHeight * 0.5f, z),
                    tileScale, tiles[(x + z) % tiles.Length], tileMesh).GetComponent<MeshRenderer>();
            }
        }

        if (theme.borderWidth > 0f)
            CreateBorder(grid, theme);

        foreach (Vector2Int cell in grid.FixedWalls)
            wallViews.Add(cell, CreateWall(cell, $"Fixed Wall {cell.x},{cell.y}", fixedWall));

        for (int i = 0; i < grid.SpawnCells.Count; i++)
        {
            EnemyFaction faction = factions[i];
            Vector3 position = DefenseGrid.CellPosition(grid.SpawnCells[i]);
            var padScale = new Vector3(0.9f, 0.07f, 0.9f);
            Mesh padMesh = theme.tileBevel > 0f ? Beveled("Spawn Pad", padScale, Mathf.Min(theme.tileBevel, 0.03f)) : null;
            Box($"{faction.DisplayName} spawn pad", root, position + Vector3.up * 0.035f,
                padScale, FactionMaterial(faction), padMesh);
            Box("Spawn pad center", root, position + Vector3.up * 0.075f, new Vector3(0.55f, 0.03f, 0.55f), foundation);
        }

        CreateCore(grid, theme, foundation, fixedWall, core);
        areaSpellPreviewMesh = CreateRingMesh("Area Spell Range", DefenseBattle.DefaultAreaSpellRadius, 0.075f, 64);
        factionRingMesh = CreateRingMesh("Faction Ring", 0.23f, 0.075f, 24);
        previewRingMesh = CreateRingMesh("Spell Target Ring", 0.48f, 0.12f, 32);
        var preview = new GameObject("Area Spell Range", typeof(MeshFilter), typeof(MeshRenderer));
        preview.transform.SetParent(root, false);
        preview.GetComponent<MeshFilter>().sharedMesh = areaSpellPreviewMesh;
        areaSpellPreview = preview.GetComponent<MeshRenderer>();
        attackBuffPreviewMaterial = core;
        moveSpeedBuffPreviewMaterial = Palette(MoveSpeedBuffColor);
        healPreviewMaterial = Palette(UIPalette.Heal);
        armorBuffPreviewMaterial = Palette(UIPalette.Armor);
        nearbyPreviewMaterial = Palette(UIPalette.TextPrimary);
        attackOutlineMaterial = UnlitPalette(UIPalette.Attack);
        healOutlineMaterial = UnlitPalette(UIPalette.Heal);
        moveSpeedOutlineMaterial = UnlitPalette(UIPalette.MoveSpeed);
        armorOutlineMaterial = UnlitPalette(UIPalette.Armor);
        nearbyOutlineMaterial = UnlitPalette(UIPalette.TextPrimary);
        areaSpellPreview.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        areaSpellPreview.receiveShadows = false;
        areaSpellPreview.enabled = false;
        var spellStroke = new Material(Shader.Find("Sprites/Default")) { name = "Spell Effect Stroke" };
        materials.Add(spellStroke);
        spellEffects = new SpellEffects(root, camera, spellStroke, AimDimSortingOrder + 1);
        spellCastMarkers = new SpellCastMarkers(camera, root, AimDimSortingOrder + 2);
        aimDim = CreateAimDim();
    }

    // 보드 바깥을 두르는 테두리. 칸 안쪽을 침범하지 않아 선택과 경로에 영향이 없다.
    private void CreateBorder(DefenseGrid grid, BoardTheme theme)
    {
        Material material = Palette(theme.border);
        float width = theme.borderWidth;
        const float bottom = -0.85f;
        const float top = 0.06f;
        float height = top - bottom;
        float bevel = Mathf.Max(theme.tileBevel, 0.04f);
        float y = (top + bottom) * 0.5f;
        Vector3 center = grid.Center;
        float halfX = DefenseGrid.Width * 0.5f + width * 0.5f;
        float halfZ = DefenseGrid.Depth * 0.5f + width * 0.5f;

        var longSize = new Vector3(DefenseGrid.Width + width * 2f, height, width);
        var shortSize = new Vector3(width, height, DefenseGrid.Depth);
        Mesh longMesh = Beveled("Board Border Long", longSize, bevel);
        Mesh shortMesh = Beveled("Board Border Short", shortSize, bevel);
        Box("Border near", root, new Vector3(center.x, y, center.z - halfZ), longSize, material, longMesh);
        Box("Border far", root, new Vector3(center.x, y, center.z + halfZ), longSize, material, longMesh);
        Box("Border left", root, new Vector3(center.x - halfX, y, center.z), shortSize, material, shortMesh);
        Box("Border right", root, new Vector3(center.x + halfX, y, center.z), shortSize, material, shortMesh);
    }

    private Mesh Beveled(string name, Vector3 size, float bevel)
    {
        Mesh mesh = BeveledBoxMesh.Create(name, size, bevel);
        meshes.Add(mesh);
        return mesh;
    }

    private void CreateCore(DefenseGrid grid, BoardTheme theme, Material foundation, Material wall, Material core)
    {
        var model = new GameObject("Core").transform;
        model.SetParent(root, false);
        model.localPosition = grid.CorePosition;

        float width = grid.CoreSize.x - 0.04f;
        float depth = grid.CoreSize.y - 0.04f;
        const float rim = 0.16f;
        const float platformTop = 0.32f;
        const float platformThickness = 0.12f;
        Box("Core base", model, Vector3.up * 0.1f, new Vector3(width, 0.2f, depth), foundation);
        Box("Core platform", model, Vector3.up * (platformTop - platformThickness * 0.5f),
            new Vector3(width - rim, platformThickness, depth - rim), trim);

        // The rim meshes are part of the core model and sit entirely within its footprint.
        float sideX = (width - rim) * 0.5f;
        float sideZ = (depth - rim) * 0.5f;
        Box("Core rim left", model, new Vector3(-sideX, 0.29f, 0f), new Vector3(rim, 0.26f, depth), wall);
        Box("Core rim right", model, new Vector3(sideX, 0.29f, 0f), new Vector3(rim, 0.26f, depth), wall);
        Box("Core rim front", model, new Vector3(0f, 0.29f, -sideZ), new Vector3(width - rim * 2f, 0.26f, rim), wall);
        Box("Core rim back", model, new Vector3(0f, 0.29f, sideZ), new Vector3(width - rim * 2f, 0.26f, rim), wall);

        if (theme.coreCharacterPrefab != null)
        {
            GameObject character = Object.Instantiate(theme.coreCharacterPrefab, model, false);
            character.transform.SetLocalPositionAndRotation(Vector3.up * platformTop, Quaternion.LookRotation(Vector3.back));
            character.transform.localScale *= theme.coreCharacterScale;
            if (character.GetComponentInChildren<Animator>() is { } animator && animator.runtimeAnimatorController != null)
            {
                // Hold the frame where the caster stands upright with its staff.
                if (animator.HasState(0, WalkState))
                    animator.Play(WalkState, 0, 0.25f);
                animator.speed = 0f;
                animator.Update(0f);
            }
            return;
        }

        Box("Core body", model, Vector3.up * 0.56f, new Vector3(0.86f, 0.52f, 0.86f), core);
        Transform crown = Box("Core crown", model, Vector3.up * 0.94f, new Vector3(0.58f, 0.24f, 0.58f), core);
        crown.localRotation = Quaternion.Euler(0, 45, 0);
    }

    public void FitCamera()
    {
        float boardScale = Mathf.Max(DefenseGrid.Width, DefenseGrid.Depth) / 12f;
        camera.orthographicSize = Mathf.Max(9.4f, 9.5f / camera.aspect) * boardScale * viewScale;
    }

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

        Vector3 offset = Vector3.zero;
        if (zoomAnchorViewport is Vector2 viewport)
        {
            Ray ray = BoardRay(camera.ViewportToScreenPoint(viewport));
            if (BoardGround.Raycast(ray, out float distance))
            {
                // The orthographic ground offset scales with the view. Translate by the portion the zoom removes.
                offset = (ray.GetPoint(distance) - cameraFocus) * (1f - newScale / viewScale);
            }
        }

        viewScale = newScale;
        MoveCamera(offset);
        FitCamera();
    }

    public void PanCamera(Vector2 input, float deltaTime)
    {
        Vector2 direction = Vector2.ClampMagnitude(input, 1f);
        Quaternion yaw = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
        Vector3 offset = root.InverseTransformDirection(yaw * new Vector3(direction.x, 0f, direction.y));
        MoveCamera(offset * (CameraPanSpeed * deltaTime));
    }

    public void DragCamera(Vector2 previousPointer, Vector2 pointer)
    {
        Ray previousRay = BoardRay(previousPointer);
        Ray currentRay = BoardRay(pointer);
        if (BoardGround.Raycast(previousRay, out float previousDistance) &&
            BoardGround.Raycast(currentRay, out float currentDistance))
            MoveCamera(previousRay.GetPoint(previousDistance) - currentRay.GetPoint(currentDistance));
    }

    private void MoveCamera(Vector3 offset)
    {
        cameraFocus.x = ClampPanAxis(cameraFocus.x + offset.x, DefenseGrid.Width - 1f);
        cameraFocus.z = ClampPanAxis(cameraFocus.z + offset.z, DefenseGrid.Depth - 1f);
        PositionCamera();
    }

    private float ClampPanAxis(float position, float lastTile)
    {
        float inset = Mathf.Min(CameraPanInset * viewScale, lastTile * 0.5f);
        return Mathf.Clamp(position, inset, lastTile - inset);
    }

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

        foreach (Vector2Int wall in wallViews.Keys)
            if (DefenseGrid.WallBounds(wall).IntersectRay(ray, out float wallDistance) && wallDistance < distance)
                return false;

        point = ray.GetPoint(distance);
        return point.x >= -0.5f && point.x < DefenseGrid.Width - 0.5f &&
            point.z >= -0.5f && point.z < DefenseGrid.Depth - 0.5f;
    }

    public void ShowAreaSpellPreview(DefenseBattle.Spell? spell, Vector3? center, float radius)
    {
        Material material = center.HasValue ? spell switch
        {
            DefenseBattle.Spell.MoveSpeedBuff => moveSpeedBuffPreviewMaterial,
            DefenseBattle.Spell.Heal => healPreviewMaterial,
            DefenseBattle.Spell.AttackBuff => attackBuffPreviewMaterial,
            DefenseBattle.Spell.ArmorBuff => armorBuffPreviewMaterial,
            _ => null
        } : null;

        UpdateAimDim(spell.HasValue);
        Vector3 point = center.GetValueOrDefault();
        areaSpellPreview.enabled = material != null;
        if (material != null)
        {
            Material ring = AimOverlay(material);
            if (areaSpellPreview.sharedMaterial != ring)
                areaSpellPreview.sharedMaterial = ring;
            areaSpellPreview.transform.localPosition = point + Vector3.up * 0.04f;
            float scale = radius / DefenseBattle.DefaultAreaSpellRadius;
            areaSpellPreview.transform.localScale = new Vector3(scale, 1f, scale);
            UpdateAreaSpellRing(point, radius);
        }

        foreach (var entry in enemyViews)
        {
            bool inRange = material != null && DefenseBattle.IsInAreaSpellRange(entry.Key, point, radius);
            bool eligible = inRange && DefenseBattle.CanReceiveAreaSpell(entry.Key, spell.Value);
            SetEnemyPreviewTint(entry.Value, inRange ? material : null, eligible);
        }
    }

    public void PlayAreaSpell(DefenseBattle.Spell spell, Vector3 center, float radius,
        IReadOnlyList<EnemyUnit> recipients)
    {
        if (recipients.Count == 0)
            return;

        spellEffects.PlayArea(spell, center, radius);
        foreach (EnemyUnit enemy in recipients)
        {
            if (enemyViews.TryGetValue(enemy, out EnemyView view))
                spellCastMarkers.Play(spell, view.Root, EnemyMarkerPoint(enemy, 0.76f),
                    DefenseHud.UnitMarkerViewportClearance(enemy));
        }
    }

    // 카메라 바로 앞에 화면을 덮는 반투명 판. HUD는 화면 오버레이라 영향을 받지 않는다.
    private SpriteRenderer CreateAimDim()
    {
        var texture = new Texture2D(1, 1) { name = "Aim Dim" };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        var dim = new GameObject("Aim Dim").AddComponent<SpriteRenderer>();
        dim.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        dim.transform.SetParent(camera.transform, false);
        dim.sortingOrder = AimDimSortingOrder;
        dim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dim.receiveShadows = false;
        dim.enabled = false;
        return dim;
    }

    // 조준 표시나 페이드는 UI처럼 실시간으로 움직여야 하므로 느려진 전투 시간 대신 unscaled 시간을 쓴다.
    private void UpdateAimDim(bool aiming)
    {
        float target = aiming ? AimDimAlpha : 0f;
        aimDimAlpha = Mathf.MoveTowards(aimDimAlpha, target, AimDimAlpha / AimDimFadeTime * Time.unscaledDeltaTime);
        aimDim.enabled = aimDimAlpha > 0f;
        if (!aimDim.enabled)
            return;

        Color color = UIPalette.Background;
        color.a = aimDimAlpha;
        aimDim.color = color;
        float distance = camera.nearClipPlane + 0.05f;
        float height = camera.orthographic
            ? camera.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        aimDim.transform.localPosition = new Vector3(0f, 0f, distance);
        aimDim.transform.localRotation = Quaternion.identity;
        aimDim.transform.localScale = new Vector3(height * camera.aspect * 1.1f, height * 1.1f, 1f);
    }

    // 범위 원은 다른 오브젝트와 머티리얼을 공유하므로, 그리는 순서만 늦춘 복제본을 쓴다.
    private Material AimOverlay(Material source)
    {
        if (!aimOverlayMaterials.TryGetValue(source, out Material overlay))
        {
            overlay = new Material(source) { name = source.name + " (Aim Overlay)", renderQueue = AimOverlayRenderQueue };
            materials.Add(overlay);
            aimOverlayMaterials.Add(source, overlay);
        }
        return overlay;
    }

    private void UpdateAreaSpellRing(Vector3 center, float radius)
    {
        areaSpellTriangles.Clear();
        const int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            // Leave a small break where the ground ring passes through a wall footprint.
            float angle = (i + 0.5f) * (2f * Mathf.PI / segments);
            Vector3 midpoint = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            bool onWall = false;
            foreach (Vector2Int cell in wallViews.Keys)
            {
                Vector3 wall = DefenseGrid.CellPosition(cell);
                float margin = DefenseGrid.WallWidth * 0.5f + 0.1f;
                if (Mathf.Abs(midpoint.x - wall.x) <= margin && Mathf.Abs(midpoint.z - wall.z) <= margin)
                {
                    onWall = true;
                    break;
                }
            }

            if (onWall)
                continue;

            int inner = i * 2;
            int next = (i + 1) % segments * 2;
            areaSpellTriangles.Add(inner);
            areaSpellTriangles.Add(next);
            areaSpellTriangles.Add(inner + 1);
            areaSpellTriangles.Add(inner + 1);
            areaSpellTriangles.Add(next);
            areaSpellTriangles.Add(next + 1);
        }

        areaSpellPreviewMesh.SetTriangles(areaSpellTriangles, 0, false);
    }

    private void SetEnemyPreviewTint(EnemyView view, Material material, bool eligible)
    {
        if (view.PreviewMaterial == material && view.PreviewEligible == eligible)
            return;

        view.PreviewMaterial = material;
        view.PreviewEligible = eligible;
        view.PreviewRing.enabled = material != null;
        view.PreviewOutline.enabled = material != null;

        if (material == null)
        {
            foreach (Renderer renderer in view.Renderers)
                renderer.SetPropertyBlock(null);
            return;
        }

        view.PreviewRing.sharedMaterial = eligible ? material : nearbyPreviewMaterial;
        view.PreviewRing.transform.localScale = Vector3.one * (eligible ? 1.2f : 1f);
        view.PreviewOutline.sharedMaterial = eligible
            ? material == attackBuffPreviewMaterial ? attackOutlineMaterial
                : material == healPreviewMaterial ? healOutlineMaterial
                : material == armorBuffPreviewMaterial ? armorOutlineMaterial : moveSpeedOutlineMaterial
            : nearbyOutlineMaterial;
        view.PreviewOutline.widthMultiplier = eligible ? 0.085f : 0.055f;
        PositionPreviewOutline(view);
        Color tint = eligible ? Color.Lerp(material.color, UIPalette.TextPrimary, 0.5f) : UIPalette.TextPrimary;
        float strength = eligible ? 0.62f : 0.22f;
        foreach (Renderer renderer in view.Renderers)
        {
            enemyTint.SetColor(BaseColor, Color.Lerp(renderer.sharedMaterial.color, tint, strength));
            renderer.SetPropertyBlock(enemyTint);
        }
    }

    private static Mesh CreateRingMesh(string name, float radius, float thickness, int segments)
    {
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

        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
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

    public Vector3 ScreenPoint(EnemyUnit enemy, float height) =>
        camera.WorldToScreenPoint(EnemyMarkerPoint(enemy, height));

    private Vector3 EnemyMarkerPoint(EnemyUnit enemy, float height)
    {
        Vector3 worldPoint = root.TransformPoint(enemy.Position + Vector3.up * height);
        if (enemyViews.TryGetValue(enemy, out EnemyView view) && view.Animator != null)
        {
            foreach (Renderer renderer in view.Renderers)
                worldPoint.y = Mathf.Max(worldPoint.y, renderer.bounds.max.y + 0.05f);
        }
        return worldPoint;
    }

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
        model.localPosition = enemy.Position;
        EnemyView view;
        GameObject prefab = enemy.Faction.Model;
        if (prefab != null)
        {
            GameObject visual = Object.Instantiate(prefab, model);
            ApplyFactionModel(visual, enemy.Faction);

            view = new EnemyView
            {
                Root = model,
                Animator = visual.GetComponentInChildren<Animator>(),
                Renderers = visual.GetComponentsInChildren<Renderer>()
            };
            if (view.Animator != null)
            {
                bool hasRun = view.Animator.HasState(0, RunState);
                view.MoveState = hasRun ? RunState : WalkState;
                view.StrideSpeed = hasRun ? GoblinRunStrideSpeed : WalkStrideSpeed;
            }

            var ring = new GameObject("Faction Ring", typeof(MeshFilter), typeof(MeshRenderer));
            ring.transform.SetParent(model, false);
            ring.transform.localPosition = Vector3.up * 0.02f;
            ring.GetComponent<MeshFilter>().sharedMesh = factionRingMesh;
            var renderer = ring.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        else
        {
            view = CreateBlockEnemy(model, material, enemy.FactionIndex);
        }
        var previewRing = new GameObject("Spell Target Ring", typeof(MeshFilter), typeof(MeshRenderer));
        previewRing.transform.SetParent(model, false);
        previewRing.transform.localPosition = Vector3.up * 0.12f;
        previewRing.GetComponent<MeshFilter>().sharedMesh = previewRingMesh;
        view.PreviewRing = previewRing.GetComponent<MeshRenderer>();
        view.PreviewRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        view.PreviewRing.receiveShadows = false;
        view.PreviewRing.enabled = false;
        view.PreviewOutline = new GameObject("Spell Unit Outline").AddComponent<LineRenderer>();
        view.PreviewOutline.transform.SetParent(model, false);
        view.PreviewOutline.useWorldSpace = false;
        view.PreviewOutline.loop = true;
        view.PreviewOutline.positionCount = 40;
        view.PreviewOutline.numCornerVertices = 2;
        view.PreviewOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        view.PreviewOutline.receiveShadows = false;
        view.PreviewOutline.enabled = false;
        enemyViews.Add(enemy, view);
        SyncEnemy(enemy);
    }

    private void PositionPreviewOutline(EnemyView view)
    {
        Bounds bounds = view.Renderers[0].bounds;
        for (int i = 1; i < view.Renderers.Length; i++)
            bounds.Encapsulate(view.Renderers[i].bounds);

        Vector3 right = camera.transform.right;
        Vector3 up = camera.transform.up;
        Vector3 extent = bounds.extents;
        float radiusX = Mathf.Clamp(Mathf.Abs(right.x) * extent.x + Mathf.Abs(right.y) * extent.y +
            Mathf.Abs(right.z) * extent.z + 0.1f, 0.38f, 0.75f);
        float radiusY = Mathf.Clamp(Mathf.Abs(up.x) * extent.x + Mathf.Abs(up.y) * extent.y +
            Mathf.Abs(up.z) * extent.z + 0.12f, 0.55f, 0.95f);

        LineRenderer outline = view.PreviewOutline;
        outline.transform.position = bounds.center;
        outline.transform.rotation = camera.transform.rotation;
        for (int i = 0; i < outline.positionCount; i++)
        {
            float angle = i * (2f * Mathf.PI / outline.positionCount);
            outline.SetPosition(i, new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f));
        }
    }

    // 팩션에 지정한 모델은 FBX 를 그대로 쓰므로 애니메이터, 머티리얼, 크기를 여기서 맞춘다.
    private static void ApplyFactionModel(GameObject visual, EnemyFaction faction)
    {
        visual.transform.localScale = Vector3.one * faction.ModelScale;

        if (faction.Animations != null)
        {
            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = visual.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = faction.Animations;
            animator.Rebind();
        }

        if (faction.Skin != null)
        {
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = faction.Skin;
                renderer.sharedMaterials = materials;
            }
        }
    }

    private EnemyView CreateBlockEnemy(Transform model, Material material, int factionIndex)
    {
        model.localScale = Vector3.one * 0.83f;
        Box("Body", model, new Vector3(0, 0.29f, 0), new Vector3(0.42f, 0.43f, 0.38f), material);
        Transform head = Box("Head", model, new Vector3(0, 0.62f, 0), new Vector3(0.31f, 0.27f, 0.31f), material);
        if (factionIndex == 0)
        {
            Box("Crest", head, new Vector3(0, 0.65f, 0), new Vector3(0.28f, 0.34f, 0.7f), trim);
        }
        else
        {
            Box("Shoulder", model, new Vector3(-0.21f, 0.43f, 0), new Vector3(0.15f, 0.14f, 0.25f), trim);
            Box("Shoulder", model, new Vector3(0.21f, 0.43f, 0), new Vector3(0.15f, 0.14f, 0.25f), trim);
        }

        return new EnemyView
        {
            Root = model,
            Head = head,
            Renderers = model.GetComponentsInChildren<MeshRenderer>()
        };
    }

    public void RemoveEnemy(EnemyUnit enemy)
    {
        if (!enemyViews.Remove(enemy, out EnemyView view))
            return;

        // 공격과 동시에 쓰러진 경우에도 타격은 보여준다.
        PlayHit(view, enemy);
        if (view.HitsSeen != enemy.AttacksPerformed)
        {
            view.PendingHit = enemy.LastAttack;
            PlayHit(view, enemy);
        }
        if (view.MoveSpeedTrails != null)
        {
            foreach (LineRenderer trail in view.MoveSpeedTrails)
                trail.enabled = false;
        }
        Object.Destroy(view.Root.gameObject);
    }

    public void AdvanceEffects(float deltaTime)
    {
        foreach (HitEffect effect in hitEffects)
            if (effect.IsPlaying)
                effect.Advance(deltaTime);
        spellEffects.Advance(deltaTime);
        spellCastMarkers.Advance(deltaTime);
    }

    public void SyncEnemy(EnemyUnit enemy, float deltaTime = 0f, float playbackSpeed = 1f)
    {
        EnemyView view = enemyViews[enemy];
        Vector3 step = enemy.Position - view.Root.localPosition;
        view.Root.localPosition = enemy.Position;
        if (view.Animator != null)
        {
            SyncGoblinMotion(view, enemy, step, deltaTime, playbackSpeed);
        }
        else
        {
            view.Root.localRotation = Quaternion.LookRotation(enemy.Heading);
            if (view.Head != null)
                view.Head.localPosition = new Vector3(0, 0.62f, enemy.AttackFlash * 0.7f);
        }
        SyncHit(view, enemy);
        bool movingWithBuff = enemy.MoveSpeedBuffStacks > 0 && IsMoving(step, deltaTime, playbackSpeed);
        spellEffects.SyncMoveTrail(view.Root, step, movingWithBuff, ref view.MoveSpeedTrails);
    }

    // 공격 애니메이션이 타격 지점에 닿는 순간 대상 위치에 이펙트를 띄운다.
    private void SyncHit(EnemyView view, EnemyUnit enemy)
    {
        bool attacked = view.HitsSeen != enemy.AttacksPerformed;
        if (view.PendingHit.Target != null && (attacked || ImpactReached(view)))
            PlayHit(view, enemy);

        if (!attacked)
            return;

        view.HitsSeen = enemy.AttacksPerformed;
        view.PendingHit = enemy.LastAttack;
        if (view.Animator == null)
            PlayHit(view, enemy);
    }

    private static bool ImpactReached(EnemyView view)
    {
        if (view.Animator == null)
            return true;

        AnimatorStateInfo state = view.Animator.GetCurrentAnimatorStateInfo(0);
        return state.fullPathHash != AttackState || state.normalizedTime >= HitImpactTime;
    }

    private void PlayHit(EnemyView view, EnemyUnit attacker)
    {
        var (target, boosted, targetArmored) = view.PendingHit;
        view.PendingHit = default;
        if (target == null)
            return;

        Vector3 toAttacker = attacker.Position - target.Position;
        toAttacker.y = 0f;
        Vector3 point = target.Position + Vector3.up * HitHeight + Vector3.ClampMagnitude(toAttacker, HitForwardOffset);
        NextHitEffect().Play(root.TransformPoint(point), attacker.Faction.Color);
        if (boosted)
            spellEffects.PlayAttack(attacker.Position, target.Position);
        if (targetArmored)
            spellEffects.PlayArmorImpact(target.Position);
    }

    private HitEffect NextHitEffect()
    {
        foreach (HitEffect effect in hitEffects)
            if (!effect.IsPlaying)
                return effect;

        if (hitEffects.Count < MaxHitEffects)
        {
            HitEffect created = HitEffect.Create(root, camera);
            hitEffects.Add(created);
            return created;
        }

        nextHitEffect = (nextHitEffect + 1) % hitEffects.Count;
        return hitEffects[nextHitEffect];
    }

    // 공격 클립이 공격 간격보다 길면 그만큼 빠르게 재생해서 다음 공격 전에 끝나게 한다.
    private static float AttackPlaybackScale(Animator animator, EnemyUnit enemy)
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != AttackState || state.length <= 0f)
            return 1f;
        return Mathf.Max(1f, state.length / (enemy.Faction.AttackInterval * 0.9f));
    }

    private static bool IsMoving(Vector3 step, float deltaTime, float playbackSpeed)
    {
        float movementThreshold = 0.001f * Mathf.Min(1f, playbackSpeed);
        return deltaTime > 0f && step.sqrMagnitude > movementThreshold * movementThreshold;
    }

    private static void SyncGoblinMotion(EnemyView view, EnemyUnit enemy, Vector3 step, float deltaTime, float playbackSpeed)
    {
        Animator animator = view.Animator;
        bool moving = IsMoving(step, deltaTime, playbackSpeed);
        view.StoppedTime = moving ? 0f : view.StoppedTime + deltaTime;
        bool newAttack = view.AttacksPlayed != enemy.AttacksPerformed;
        if (newAttack)
        {
            view.AttacksPlayed = enemy.AttacksPerformed;
            view.AnimationState = AttackState;
            animator.Play(AttackState, 0, 0f);
        }
        else
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            bool attackFinished = currentState.fullPathHash == AttackState && currentState.normalizedTime >= 1f;
            if (moving || view.AnimationState != AttackState || attackFinished)
            {
                // Keep the run phase across brief gaps between movement steps.
                bool resumeRun = view.AnimationState == view.MoveState && view.StoppedTime < RunStopGrace;
                int state = moving || resumeRun ? view.MoveState : WalkState;
                if (view.AnimationState != state)
                {
                    view.AnimationState = state;
                    animator.Play(state, 0, 0f);
                }
            }
        }

        Vector3 facing = view.AnimationState == AttackState ? enemy.AttackHeading : moving ? step : enemy.Heading;
        Quaternion heading = Quaternion.LookRotation(facing);
        view.Root.localRotation = newAttack || deltaTime <= 0f ? heading :
            Quaternion.RotateTowards(view.Root.localRotation, heading, GoblinTurnSpeed * deltaTime);

        animator.speed = view.AnimationState == AttackState ? playbackSpeed * AttackPlaybackScale(animator, enemy) : 0f;
        if (view.AnimationState == view.MoveState && moving)
            animator.speed = step.magnitude * playbackSpeed / (deltaTime * view.StrideSpeed * animator.transform.localScale.z);
    }

    public void ReleaseMaterials()
    {
        spellCastMarkers.ReleaseSprites();
        Object.Destroy(areaSpellPreviewMesh);
        Object.Destroy(factionRingMesh);
        if (aimDim != null && aimDim.sprite != null)
        {
            Object.Destroy(aimDim.sprite.texture);
            Object.Destroy(aimDim.sprite);
        }
        Object.Destroy(previewRingMesh);
        foreach (Material material in materials)
            Object.Destroy(material);
        foreach (Mesh mesh in meshes)
            Object.Destroy(mesh);
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

    private Material Palette(Color color, Texture2D texture = null)
    {
        var material = new Material(shader) { color = color };
        material.SetFloat("_Smoothness", 0.15f);
        if (texture != null)
            material.SetTexture("_BaseMap", texture);
        materials.Add(material);
        return material;
    }

    private Material UnlitPalette(Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = color };
        materials.Add(material);
        return material;
    }

    // mesh를 주면 그 크기 그대로 쓰는 메시이므로 size로 늘리지 않는다.
    private static Transform Box(string label, Transform parent, Vector3 position, Vector3 size, Material material,
        Mesh mesh = null)
    {
        GameObject cube;
        if (mesh == null)
        {
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(cube.GetComponent<BoxCollider>());
            cube.transform.localScale = size;
        }
        else
        {
            cube = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            cube.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        cube.name = label;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cube.transform;
    }
}
