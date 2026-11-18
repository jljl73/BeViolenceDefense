using LitMotion;
using LitMotion.Extensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    private const float MaxViewScale = 1.2f;
    private const float TileHeight = 0.28f;
    private const float RouteLaneSpacing = 0.49f;
    private const float RouteMarkerHeight = 0.02f;
    private const float RouteMarkerTravel = 0.36f;
    private const float RouteMarkerCyclesPerSecond = 0.5f;
    private const float SingleLaneRouteWidth = 1.3f;
    private const float RouteMarkerFadePortion = 0.18f;
    private const float BattleRouteTileTint = 0.3f;
    private const float BattleRouteMarkerTint = 0.6f;
    private const float SpawnPadHeight = 0.09f;
    private const float GoblinTurnSpeed = 1440f;
    private const float RunStopGrace = 0.08f;
    private const float AnimationBlendTime = 0.08f;
    // Approximate planted-foot travel per second in the unscaled Run clip.
    private const float GoblinRunStrideSpeed = 1.55f;
    // 스킬 조준 중 화면 딤. 범위 표시는 딤보다 나중에 그려서 밝게 보이게 한다.
    private const float AimDimAlpha = 0.55f;
    private const float AimDimFadeTime = 0.12f;
    private const int AimDimSortingOrder = 40;
    private const int AimOverlayRenderQueue = 3100;
    // Run 상태가 없는 모델(좀비)은 Walk 클립으로 이동한다. 발이 미끄러지면 이 값을 조절한다.
    private const float WalkStrideSpeed = 0.9f;
    // 별도 Idle 클립이 없는 적은 Walk를 느리게 반복해 대기 중의 완전한 정지를 피한다.
    private const float StationaryWalkSpeed = 0.25f;
    // 공격 애니메이션에서 무기가 닿는 지점 (0~1). 이펙트가 빠르거나 늦으면 이 값만 조절한다.
    private const float HitImpactTime = 0.45f;
    private const float HitHeight = 0.5f;
    private const float HitForwardOffset = 0.2f;
    private const int MaxHitEffects = 24;
    private const float CoreStrikeWindup = 0.24f;
    private const float CoreStrikeFadeDuration = 0.25f;
    private const float CoreStrikeFlashScale = 1.50f;
    private const float CoreBodyFlashDuration = 0.17f;
    private const float CoreBodyFlashBrightness = 1.36f;
    private const float CoreShockwaveHeight = 0.44f;
    private const float SpellBodyFlashDuration = 0.26f;
    private const float SpellBodyFlashBrightness = 1.8f;
    private const float BuffMarkSpacing = 0.20f;
    private const float BuffMarkOffset = 0.32f;
    private const float BuffMarkDarkeningPerStack = 0.14f;
    private const float BuffMarkMinimumBrightness = 0.58f;

    private static readonly Plane BoardGround = new(Vector3.up, Vector3.zero);
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int WalkState = Animator.StringToHash("Base Layer.Walk");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int CoreHitState = Animator.StringToHash("Base Layer.Hit");
    private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
    private static readonly int AttackState = Animator.StringToHash("Base Layer.Attack");

    public static readonly Color32 CoreColor = new(255, 209, 112, 255);
    private static readonly Color CoreDamageColor = new Color32(255, 58, 66, 255);
    public static readonly Color32 MoveSpeedBuffColor = new(102, 218, 255, 255);

    private sealed class EnemyView
    {
        public Transform Root;
        public Transform Head;
        public Renderer[] Renderers;
        public Renderer FactionRing;
        public (Renderer Renderer, int Slot, Color BaseColor)[] TintSlots;
        public Animator Animator;
        public int AnimationState;
        public int MoveState;
        public float StrideSpeed;
        public int AttacksPlayed;
        public int HitsSeen;
        public (EnemyUnit Target, bool Boosted, bool TargetArmored) PendingHit;
        public LineRenderer[] MoveSpeedTrails;
        public Transform BuffMarks;
        public MeshRenderer[] BuffMarkRenderers;
        public int[] BuffMarkStacks;
        public float StoppedTime;
        public Material PreviewMaterial;
        public (Renderer Renderer, Material[] Original, Material[] Preview, int Order)[] PreviewSurfaces;
        public Color SpellFlashColor;
        public float SpellFlashRemaining;
    }

    private readonly struct RouteMarkerMotion
    {
        public readonly Vector3 Center;
        public readonly Vector3 Direction;
        public readonly Color ArrowColor;
        public readonly Color FloorColor;
        public readonly float WidthScale;

        public RouteMarkerMotion(Vector3 center, Vector3 direction, Color arrowColor, Color floorColor,
            float widthScale)
        {
            Center = center;
            Direction = direction;
            ArrowColor = arrowColor;
            FloorColor = floorColor;
            WidthScale = widthScale;
        }
    }

    private sealed class CoreStrike
    {
        public EnemyView View;
        public Vector3 From;
        public Vector3 Stance;
        public Vector3 Impact;
        public Vector3 Heading;
        public bool Animated;
        public bool HasHit;
        public float Age;
        public float AttackLength;
        public float SampledPose;
        public int CoreDamage;
    }

    private readonly Transform root;
    private readonly Vector3 coreShockwaveCenter;
    private readonly Camera camera;
    private readonly Shader shader;
    private readonly IReadOnlyList<EnemyFaction> factions;
    private readonly EnemyCatalog catalog;
    private readonly Sprite routeChevron;
    private readonly HashSet<Vector2Int> spawnCells;
    private readonly IReadOnlyList<int> spawnFactions;
    private readonly int boardWidth;
    private readonly int boardDepth;
    private readonly List<MeshRenderer> routeMarkers = new();
    private readonly List<RouteMarkerMotion> routeMarkerMotions = new();
    private float routeMarkerPhase = 0.5f;
    private readonly Mesh routeMarkerMesh;
    private readonly Material routeMarkerMaterial;
    private readonly List<Material> materials = new();
    private readonly Dictionary<Material, Material> aimOverlayMaterials = new();
    private readonly SpriteRenderer aimDim;
    private float aimDimAlpha;
    private readonly List<Mesh> meshes = new();
    private readonly Dictionary<Vector2Int, Transform> wallViews = new();
    private readonly Dictionary<EnemyUnit, EnemyView> enemyViews = new();
    private readonly List<HitEffect> hitEffects = new();
    private readonly List<CoreStrike> coreStrikes = new();
    private readonly Dictionary<Material, Material> fadeMaterials = new();
    private readonly SpellEffects spellEffects;
    private readonly MaterialPropertyBlock tintBlock = new();
    private readonly Dictionary<EnemyFaction, Material> factionMaterials = new();
    private readonly MeshRenderer[,] floor;
    private readonly Material[] tiles;
    private readonly Material routeTile;
    private readonly Material[] battleRouteTiles;
    private readonly Material encounterTile;
    private readonly Material buildWall;
    private readonly Material trim;
    private readonly GameObject wallPrefab;
    private readonly Color fixedWallTint;
    private readonly Material attackBuffPreviewMaterial;
    private readonly Material moveSpeedBuffPreviewMaterial;
    private readonly Material healPreviewMaterial;
    private readonly Material armorBuffPreviewMaterial;
    private readonly Material attackBuffMarkMaterial;
    private readonly Material moveSpeedBuffMarkMaterial;
    private readonly Material armorBuffMarkMaterial;
    private readonly Quaternion buffMarkRotation;
    private readonly Dictionary<DefenseBattle.Spell, Material> targetSilhouettes = new();
    private readonly Dictionary<(Material Preview, Material Source), Material> texturedSilhouettes = new();
    private readonly MeshRenderer areaSpellPreview;
    private readonly Mesh areaSpellPreviewMesh;
    private readonly List<int> areaSpellTriangles = new(64 * 6);
    private readonly Mesh factionRingMesh;
    private int nextHitEffect;
    private int observedCoreHealth = DefenseBattle.CoreMaxHealth;
    private Animator coreAnimator;
    private (Renderer Renderer, int Slot, Color BaseColor)[] coreTintSlots;
    private float coreFlashRemaining;
    private bool coreHitPlaying;
    private Vector3 cameraFocus;
    private float viewScale = 1f;
    private float targetViewScale = 1f;
    private Vector2? zoomAnchorViewport;

    public IReadOnlyCollection<Vector2Int> PlannedEncounters { get; private set; } = System.Array.Empty<Vector2Int>();
    public int PendingCoreDamage { get; private set; }

    public DefenseBoardView(Transform root, Camera camera, Shader shader, DefenseGrid grid,
        IReadOnlyList<EnemyFaction> factions, Sprite routeChevron, BoardTheme theme, EnemyCatalog catalog,
        StageData stage = null)
    {
        this.catalog = catalog;
        this.root = root;
        coreShockwaveCenter = grid.CorePosition + Vector3.up * CoreShockwaveHeight;
        this.camera = camera;
        this.shader = shader;
        this.factions = factions;
        this.routeChevron = routeChevron;
        spawnCells = new HashSet<Vector2Int>(grid.SpawnCells);
        spawnFactions = grid.SpawnFactions;
        boardWidth = grid.Width;
        boardDepth = grid.Depth;
        floor = new MeshRenderer[boardWidth, boardDepth];
        // Aim slightly toward the near corner to leave room for the battle controls.
        Vector3 boardForward = root.InverseTransformDirection(camera.transform.forward);
        buffMarkRotation = Quaternion.LookRotation(-Vector3.ProjectOnPlane(boardForward, Vector3.up));
        cameraFocus = grid.Center - Vector3.ProjectOnPlane(boardForward, Vector3.up).normalized * CameraFocusOffset;
        PositionCamera();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = theme.background;
        FitCamera();

        Material foundation = Palette(theme.foundation);
        tiles = new[] { Palette(theme.tileA, theme.tileTexture), Palette(theme.tileB, theme.tileTexture) };
        routeTile = Palette(theme.routeTile, theme.tileTexture);
        battleRouteTiles = new[]
        {
            Palette(Color.Lerp(theme.tileA, theme.routeTile, BattleRouteTileTint), theme.tileTexture),
            Palette(Color.Lerp(theme.tileB, theme.routeTile, BattleRouteTileTint), theme.tileTexture)
        };
        routeMarkerMesh = CreateRouteMarkerMesh();
        meshes.Add(routeMarkerMesh);
        routeMarkerMaterial = Palette(Color.white);
        routeMarkerMaterial.enableInstancing = true;
        encounterTile = Palette(theme.encounterTile, theme.tileTexture);
        buildWall = Palette(theme.buildWall);
        Material fixedWall = Palette(theme.fixedWall);
        trim = Palette(theme.trim);
        wallPrefab = theme.wallPrefab;
        fixedWallTint = theme.fixedWallTint;
        Material core = Palette(CoreColor);

        Box("Board base", root, grid.Center - Vector3.up * 0.55f,
            new Vector3(boardWidth, 0.6f, boardDepth), foundation);
        float tileSize = 1f - theme.tileGap;
        var tileScale = new Vector3(tileSize, TileHeight, tileSize);
        Mesh tileMesh = theme.tileBevel > 0f ? Beveled("Board Tile", tileScale, theme.tileBevel) : null;
        for (int x = 0; x < boardWidth; x++)
        {
            for (int z = 0; z < boardDepth; z++)
            {
                floor[x, z] = Box($"Tile {x},{z}", root, new Vector3(x, -TileHeight * 0.5f, z),
                    tileScale, tiles[(x + z) % tiles.Length], tileMesh).GetComponent<MeshRenderer>();
            }
        }

        if (theme.borderWidth > 0f)
            CreateBorder(grid, theme);

        if (stage != null)
            CreateObstacles(grid, stage, fixedWall);
        else
            foreach (Vector2Int cell in grid.FixedWalls)
                wallViews.Add(cell, CreateWall(cell, $"Fixed Wall {cell.x},{cell.y}", fixedWall));

        for (int i = 0; i < grid.SpawnCells.Count; i++)
        {
            EnemyFaction faction = factions[spawnFactions[i]];
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
        var preview = new GameObject("Area Spell Range", typeof(MeshFilter), typeof(MeshRenderer));
        preview.transform.SetParent(root, false);
        preview.GetComponent<MeshFilter>().sharedMesh = areaSpellPreviewMesh;
        areaSpellPreview = preview.GetComponent<MeshRenderer>();
        attackBuffPreviewMaterial = core;
        moveSpeedBuffPreviewMaterial = Palette(MoveSpeedBuffColor);
        healPreviewMaterial = Palette(UIPalette.Heal);
        armorBuffPreviewMaterial = Palette(UIPalette.Armor);
        attackBuffMarkMaterial = Palette(Color.Lerp(theme.tileA, UIPalette.Attack, 0.72f));
        moveSpeedBuffMarkMaterial = Palette(Color.Lerp(theme.tileA, UIPalette.MoveSpeed, 0.72f));
        armorBuffMarkMaterial = Palette(Color.Lerp(theme.tileA, UIPalette.Armor, 0.72f));
        Shader silhouetteShader = Resources.Load<Shader>("SpellTargetSilhouette");
        foreach (DefenseBattle.Spell spell in System.Enum.GetValues(typeof(DefenseBattle.Spell)))
        {
            var silhouette = new Material(silhouetteShader) { name = $"{spell} Target Silhouette" };
            Color color = SpellEffects.SpellColor(spell);
            silhouette.SetColor("_SilhouetteColor", color);
            silhouette.SetColor("_OutlineColor", UIPalette.Background);
            targetSilhouettes.Add(spell, silhouette);
            materials.Add(silhouette);
        }
        areaSpellPreview.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        areaSpellPreview.receiveShadows = false;
        areaSpellPreview.enabled = false;
        var spellStroke = new Material(Shader.Find("Sprites/Default")) { name = "Spell Effect Stroke" };
        materials.Add(spellStroke);
        spellEffects = new SpellEffects(root, camera, spellStroke, AimDimSortingOrder + 1);
        aimDim = CreateAimDim();
    }

    // 장애물 프리팹은 차지한 칸들의 중심에 한 번만 만들고, 각 칸을 같은 Transform으로 등록해 클릭 판정에 쓴다.
    private void CreateObstacles(DefenseGrid grid, StageData stage, Material fixedWall)
    {
        foreach (StageData.Obstacle obstacle in stage.obstacles)
        {
            Transform view;
            if (obstacle.prefab != null)
            {
                view = Object.Instantiate(obstacle.prefab, root, false).transform;
                view.name = $"{obstacle.prefab.name} {obstacle.cell.x},{obstacle.cell.y}";
                FitObstacle(view, obstacle);
            }
            else
            {
                view = null;
            }

            foreach (Vector2Int cell in obstacle.Footprint.allPositionsWithin)
            {
                // grid가 범위 밖·코어·스폰 칸을 버렸으면 표시도 하지 않는다.
                if (!grid.IsFixedWall(cell) || wallViews.ContainsKey(cell))
                    continue;
                wallViews.Add(cell, view != null ? view : CreateWall(cell, $"Fixed Wall {cell.x},{cell.y}", fixedWall));
            }
        }
    }

    // 자수정 산맥처럼 한 칸 길이로 줄여 임포트한 프리팹도 차지한 칸 길이에 맞춰 균일하게 키운 뒤 칸 중심에 놓는다.
    internal static void FitObstacle(Transform model, StageData.Obstacle obstacle)
    {
        int cells = obstacle.rotation % 2 == 0 ? obstacle.size.x : obstacle.size.y;
        float length = LocalLengthX(model);
        if (length > 0.01f)
            model.localScale *= (cells - (1f - DefenseGrid.WallWidth)) / length;
        model.SetLocalPositionAndRotation(obstacle.Center, Quaternion.Euler(0f, obstacle.rotation * 90f, 0f));
    }

    // Stage Editor 미리보기가 다시 만들 때 카메라 위치와 줌을 유지한다.
    internal (Vector3 Focus, float Scale) CameraState
    {
        get => (cameraFocus, targetViewScale);
        set
        {
            cameraFocus = value.Focus;
            viewScale = targetViewScale = value.Scale;
            zoomAnchorViewport = null;
            PositionCamera();
            FitCamera();
        }
    }

    // Stage Editor 미리보기(에디터 모드)에서도 쓰므로 Play Mode가 아니면 즉시 지운다.
    internal static void Release(Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(target);
        else
            Object.DestroyImmediate(target);
    }

    // 프리팹 루트 기준 메시 전체의 X 길이 (루트 자신의 스케일 제외).
    private static float LocalLengthX(Transform model)
    {
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;
            Bounds bounds = filter.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                Vector3 point = bounds.min + Vector3.Scale(bounds.size, local);
                float x = model.InverseTransformPoint(filter.transform.TransformPoint(point)).x;
                min = Mathf.Min(min, x);
                max = Mathf.Max(max, x);
            }
        }
        return max > min ? max - min : 0f;
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
        float halfX = boardWidth * 0.5f + width * 0.5f;
        float halfZ = boardDepth * 0.5f + width * 0.5f;

        var longSize = new Vector3(boardWidth + width * 2f, height, width);
        var shortSize = new Vector3(width, height, boardDepth);
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
        float platformTop = 0.32f;
        const float platformThickness = 0.12f;
        if (theme.corePlatformPrefab != null)
        {
            // 테마가 받침 모델을 주면 기본 블록 받침대 대신 그것을 놓고, 캐릭터는 그 위에 세운다.
            GameObject platform = Object.Instantiate(theme.corePlatformPrefab, model, false);
            platform.name = "Core platform model";
            // 모델은 가로·세로 1 기준으로 만들어 두고, 코어 영역의 짧은 변에 맞춰 키운다.
            float fit = Mathf.Min(width, depth) * theme.corePlatformScale;
            platform.transform.localScale *= fit;
            platformTop = theme.corePlatformTop * fit;
        }
        else
            CreateCorePedestal(model, width, depth, rim, platformTop, platformThickness, foundation, wall);

        if (theme.coreCharacterPrefab != null)
        {
            CreateCoreCharacter(model, theme, platformTop);
            return;
        }

        Transform coreBody = Box("Core body", model, Vector3.up * 0.56f, new Vector3(0.86f, 0.52f, 0.86f), core);
        Transform crown = Box("Core crown", model, Vector3.up * 0.94f, new Vector3(0.58f, 0.24f, 0.58f), core);
        crown.localRotation = Quaternion.Euler(0, 45, 0);
        coreTintSlots = CollectTintSlots(new Renderer[] { coreBody.GetComponent<Renderer>(), crown.GetComponent<Renderer>() });
    }

    private void CreateCorePedestal(Transform model, float width, float depth, float rim, float platformTop,
        float platformThickness, Material foundation, Material wall)
    {
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
    }

    private void CreateCoreCharacter(Transform model, BoardTheme theme, float platformTop)
    {
        GameObject character = Object.Instantiate(theme.coreCharacterPrefab, model, false);
        character.transform.SetLocalPositionAndRotation(Vector3.up * platformTop, Quaternion.LookRotation(Vector3.back));
        character.transform.localScale *= theme.coreCharacterScale;
        Renderer[] body = character.GetComponentsInChildren<SkinnedMeshRenderer>();
        coreTintSlots = CollectTintSlots(body.Length > 0 ? body : character.GetComponentsInChildren<Renderer>());
        if (character.GetComponentInChildren<Animator>() is { } animator && animator.runtimeAnimatorController != null)
        {
            coreAnimator = animator;
            if (animator.HasState(0, IdleState))
                animator.Play(IdleState, 0, 0f);
        }
    }

    private void PlayCoreHit()
    {
        coreFlashRemaining = CoreBodyFlashDuration;
        ApplyCoreFlash();
        if (coreAnimator == null || !coreAnimator.HasState(0, CoreHitState))
            return;

        coreAnimator.CrossFadeInFixedTime(CoreHitState, AnimationBlendTime, 0, 0f);
        coreHitPlaying = true;
    }

    private void ApplyCoreFlash()
    {
        float strength = Mathf.SmoothStep(0f, 1f, coreFlashRemaining / CoreBodyFlashDuration);
        foreach (var (renderer, slot, baseColor) in coreTintSlots)
        {
            if (strength <= 0f)
            {
                renderer.SetPropertyBlock(null, slot);
                continue;
            }

            Color color = Color.Lerp(baseColor, CoreDamageColor * CoreBodyFlashBrightness, strength);
            color.a = baseColor.a;
            tintBlock.SetColor(BaseColor, color);
            renderer.SetPropertyBlock(tintBlock, slot);
        }
    }

    public void AdvanceCoreAnimation(float deltaTime, float playbackSpeed)
    {
        if (coreAnimator == null)
            return;

        coreAnimator.speed = deltaTime > 0f ? playbackSpeed : 0f;
        if (!coreHitPlaying)
            return;

        AnimatorStateInfo state = GetPlaybackState(coreAnimator);
        if (state.fullPathHash != CoreHitState || state.normalizedTime < 1f)
            return;

        coreHitPlaying = false;
        if (coreAnimator.HasState(0, IdleState))
            coreAnimator.CrossFadeInFixedTime(IdleState, AnimationBlendTime, 0, 0f);
    }

    public void FitCamera()
    {
        float boardScale = Mathf.Max(boardWidth, boardDepth) / 12f;
        camera.orthographicSize = Mathf.Max(9.4f, 9.5f / camera.aspect) * boardScale * viewScale;
    }

    public void QueueZoom(float scrollDelta, Vector2 pointer)
    {
        targetViewScale = Mathf.Clamp(targetViewScale * Mathf.Pow(ZoomStep, scrollDelta), MinViewScale, MaxViewScale);
        zoomAnchorViewport = TryPickCell(pointer, out Vector2Int cell) &&
            cell.x >= 0 && cell.x < boardWidth && cell.y >= 0 && cell.y < boardDepth
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
        cameraFocus.x = ClampPanAxis(cameraFocus.x + offset.x, boardWidth - 1f);
        cameraFocus.z = ClampPanAxis(cameraFocus.z + offset.z, boardDepth - 1f);
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
        => ShowRoutes(routes, encounters, false);

    public void ShowBattleRoutes(IReadOnlyList<Vector3[]> routes)
        => ShowRoutes(routes, null, true);

    private void ShowRoutes(IReadOnlyList<Vector3[]> routes,
        IReadOnlyCollection<Vector2Int> encounters, bool subdued)
    {
        PlannedEncounters = encounters ?? System.Array.Empty<Vector2Int>();
        for (int x = 0; x < boardWidth; x++)
            for (int z = 0; z < boardDepth; z++)
                floor[x, z].sharedMaterial = tiles[(x + z) % tiles.Length];

        int markerCount = routes == null ? 0 : DrawRoutes(routes, subdued);
        for (int i = markerCount; i < routeMarkers.Count; i++)
            routeMarkers[i].enabled = false;

        foreach (Vector2Int cell in PlannedEncounters)
            floor[cell.x, cell.y].sharedMaterial = encounterTile;
        AdvanceRouteMarkers(0f);
    }

    public void AdvanceRouteMarkers(float deltaTime)
    {
        routeMarkerPhase = Mathf.Repeat(routeMarkerPhase + deltaTime * RouteMarkerCyclesPerSecond, 1f);
        for (int i = 0; i < routeMarkers.Count; i++)
        {
            MeshRenderer marker = routeMarkers[i];
            if (!marker.enabled)
                continue;

            RouteMarkerMotion motion = routeMarkerMotions[i];
            float phase = routeMarkerPhase;
            float travel = Mathf.SmoothStep(0f, 1f, phase);
            marker.transform.localPosition = motion.Center + motion.Direction * ((travel - 0.5f) * RouteMarkerTravel);
            float fade = Mathf.SmoothStep(0f, 1f,
                Mathf.Min(phase, 1f - phase) / RouteMarkerFadePortion);
            marker.transform.localScale = new Vector3(motion.WidthScale * fade, fade, fade);
            tintBlock.SetColor(BaseColor, Color.Lerp(motion.FloorColor, motion.ArrowColor, fade));
            marker.SetPropertyBlock(tintBlock);
        }
    }

    private int DrawRoutes(IReadOnlyList<Vector3[]> routes, bool subdued)
    {
        var stepsByCell = new Dictionary<Vector2Int, List<(int FactionIndex, Vector3 Direction)>>();
        // factionIndex는 여기서 스폰(경로) 인덱스이고, 색은 spawnFactions로 찾는다.
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
            floor[cell.x, cell.y].sharedMaterial = subdued
                ? battleRouteTiles[(cell.x + cell.y) % battleRouteTiles.Length]
                : routeTile;
            if (routeChevron == null)
                continue;

            // Both arrows use one lateral axis so opposite headings stay in separate lanes.
            Vector3 side = Vector3.Cross(Vector3.up, steps[0].Direction);
            float height = RouteMarkerHeight + (spawnCells.Contains(cell) ? SpawnPadHeight : 0f);
            Vector3 center = DefenseGrid.CellPosition(cell) + Vector3.up * height;
            Color floorColor = floor[cell.x, cell.y].sharedMaterial.color;
            for (int i = 0; i < steps.Count; i++)
            {
                var (factionIndex, direction) = steps[i];
                float offset = (i - (steps.Count - 1) * 0.5f) * RouteLaneSpacing;
                int markerIndex = markerCount++;
                MeshRenderer marker = RouteMarker(markerIndex);
                Vector3 markerCenter = center + side * offset;
                marker.transform.localPosition = markerCenter;
                marker.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
                Color color = factions[spawnFactions[factionIndex]].Color;
                if (subdued)
                    color = Color.Lerp(floorColor, color, BattleRouteMarkerTint);
                marker.enabled = true;
                routeMarkerMotions[markerIndex] = new RouteMarkerMotion(markerCenter, direction, color, floorColor,
                    steps.Count == 1 ? SingleLaneRouteWidth : 1f);
            }
        }

        return markerCount;
    }

    private MeshRenderer RouteMarker(int index)
    {
        if (index < routeMarkers.Count)
            return routeMarkers[index];

        var marker = new GameObject("Route Arrow", typeof(MeshFilter), typeof(MeshRenderer)).GetComponent<MeshRenderer>();
        marker.transform.SetParent(root, false);
        marker.GetComponent<MeshFilter>().sharedMesh = routeMarkerMesh;
        marker.sharedMaterial = routeMarkerMaterial;
        marker.shadowCastingMode = ShadowCastingMode.Off;
        marker.receiveShadows = false;
        routeMarkers.Add(marker);
        routeMarkerMotions.Add(default);
        return marker;
    }

    private static Mesh CreateRouteMarkerMesh()
    {
        const float height = 0.08f;
        // The old route icon was a hollow chevron. Extrude its two narrow arms instead of filling the middle.
        Vector2[] outline =
        {
            new(-0.23f, -0.12f), new(-0.13f, -0.12f), new(0f, 0.05f),
            new(0.13f, -0.12f), new(0.23f, -0.12f), new(0f, 0.15f)
        };
        var vertices = new List<Vector3>();
        foreach (Vector2 point in outline)
            vertices.Add(new Vector3(point.x, height, point.y));
        var triangles = new List<int>
        {
            0, 2, 1, 0, 5, 2, // left arm
            2, 4, 3, 2, 5, 4  // right arm
        };

        for (int i = 0; i < outline.Length; i++)
        {
            int next = (i + 1) % outline.Length;
            int side = vertices.Count;
            Vector2 a = outline[i];
            Vector2 b = outline[next];
            vertices.Add(new Vector3(a.x, 0f, a.y));
            vertices.Add(new Vector3(b.x, 0f, b.y));
            vertices.Add(new Vector3(b.x, height, b.y));
            vertices.Add(new Vector3(a.x, height, a.y));
            triangles.Add(side);
            triangles.Add(side + 3);
            triangles.Add(side + 2);
            triangles.Add(side);
            triangles.Add(side + 2);
            triangles.Add(side + 1);
        }

        var mesh = new Mesh { name = "Route Arrow", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        return mesh;
    }

    public void PlaceWall(Vector2Int cell)
    {
        Transform wall = CreateWall(cell, $"Wall {cell.x},{cell.y}", buildWall);
        wallViews.Add(cell, wall);
        PlayWallPlaced(wall);
    }

    public void RemoveWall(Vector2Int cell)
    {
        if (wallViews.Remove(cell, out Transform wall))
            PlayWallRemoved(wall);
    }

    // ── 벽 설치·철거 연출 ──
    // 판정은 이미 끝난 뒤의 보여 주기다. 준비 단계에서만 일어나므로 전투 시계와 무관하게 실시간으로 재생한다.
    private const float WallDropHeight = 0.9f;
    private const float WallDropTime = 0.13f;
    private const float WallSettleTime = 0.28f;
    private const float WallRemoveTime = 0.16f;
    private static readonly Color WallFlash = new(1f, 0.82f, 0.42f);
    private Transform buildGhost;
    private MaterialPropertyBlock ghostBlock;
    private Renderer[] ghostRenderers;

    private void PlayWallPlaced(Transform wall)
    {
        Vector3 rest = wall.localPosition;
        Vector3 baseScale = wall.localScale;
        wall.localPosition = rest + Vector3.up * WallDropHeight;
        wall.localScale = Vector3.Scale(baseScale, new Vector3(0.9f, 1.15f, 0.9f));
        LMotion.Create(rest.y + WallDropHeight, rest.y, WallDropTime)
            .WithEase(Ease.InQuad)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() => SpawnWallChips(rest, 8))
            .BindToLocalPositionY(wall)
            .AddTo(wall.gameObject);
        // 착지: 납작하게 눌렸다가 튀어 오른다.
        LMotion.Create(new Vector3(1.22f, 0.68f, 1.22f), Vector3.one, WallSettleTime)
            .WithEase(Ease.OutBack)
            .WithDelay(WallDropTime)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(wall, (squash, target) => target.localScale = Vector3.Scale(baseScale, squash))
            .AddTo(wall.gameObject);

        Renderer[] renderers = wall.GetComponentsInChildren<Renderer>();
        Color settled = wallPrefab != null ? Color.white : buildWall.color;
        var block = new MaterialPropertyBlock();
        LMotion.Create(1f, 0f, 0.35f)
            .WithDelay(WallDropTime)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() =>
            {
                foreach (Renderer renderer in renderers)
                    if (renderer != null)
                        renderer.SetPropertyBlock(null);
            })
            .Bind(flash =>
            {
                block.SetColor(BaseColor, Color.Lerp(settled, WallFlash * 1.5f, flash));
                foreach (Renderer renderer in renderers)
                    if (renderer != null)
                        renderer.SetPropertyBlock(block);
            })
            .AddTo(wall.gameObject);
    }

    private void PlayWallRemoved(Transform wall)
    {
        Vector3 rest = wall.localPosition;
        Vector3 baseScale = wall.localScale;
        GameObject target = wall.gameObject;
        SpawnWallChips(rest, 6);
        LMotion.Create(0f, 1f, WallRemoveTime)
            .WithEase(Ease.InCubic)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() => Object.Destroy(target))
            .Bind(wall, (t, tr) =>
            {
                tr.localScale = Vector3.Scale(baseScale, new Vector3(1f - 0.45f * t, 1f - t, 1f - 0.45f * t));
                tr.localPosition = rest + Vector3.down * (0.1f * t);
            })
            .AddTo(target);
    }

    // 착지 자리에서 사방으로 튀는 작은 돌 조각.
    private void SpawnWallChips(Vector3 center, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.value * 0.6f) / count * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float size = Random.Range(0.08f, 0.15f);
            float distance = Random.Range(0.55f, 0.95f);
            float height = Random.Range(0.25f, 0.5f);
            Vector3 start = center + direction * 0.3f + Vector3.up * 0.08f;
            Transform chip = Box("Wall chip", root, start, Vector3.one * size, trim);
            chip.localRotation = Random.rotation;
            GameObject chipObject = chip.gameObject;
            LMotion.Create(0f, 1f, Random.Range(0.32f, 0.5f))
                .WithEase(Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => Object.Destroy(chipObject))
                .Bind(chip, (t, tr) =>
                {
                    tr.localPosition = start + direction * (distance * t) + Vector3.up * (height * 4f * t * (1f - t));
                    tr.localScale = Vector3.one * (size * (1f - t * t));
                })
                .AddTo(chipObject);
        }
    }

    /// <summary>놓을 칸 미리보기. 놓을 수 있으면 금색, 없으면(길을 막거나 한도 초과) 붉은색으로 떠 있는 벽을 보여 준다.</summary>
    public void ShowBuildGhost(Vector2Int cell, bool canPlace)
    {
        if (buildGhost == null)
        {
            buildGhost = CreateWall(cell, "Wall ghost", buildWall);
            ghostRenderers = buildGhost.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in ghostRenderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            ghostBlock = new MaterialPropertyBlock();
            buildGhost.localScale *= 0.82f;
        }

        buildGhost.gameObject.SetActive(true);
        float bob = 0.16f + Mathf.Sin(Time.unscaledTime * 5f) * 0.05f;
        buildGhost.localPosition = DefenseGrid.CellPosition(cell) + Vector3.up * bob;
        ghostBlock.SetColor(BaseColor, canPlace ? WallFlash * 1.25f : new Color(1.2f, 0.32f, 0.3f));
        foreach (Renderer renderer in ghostRenderers)
            renderer.SetPropertyBlock(ghostBlock);
    }

    public void HideBuildGhost()
    {
        if (buildGhost != null)
            buildGhost.gameObject.SetActive(false);
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
        return point.x >= -0.5f && point.x < boardWidth - 0.5f &&
            point.z >= -0.5f && point.z < boardDepth - 0.5f;
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

        Material silhouette = material != null ? targetSilhouettes[spell.Value] : null;
        foreach (var entry in enemyViews)
        {
            bool eligible = silhouette != null && DefenseBattle.IsInAreaSpellRange(entry.Key, point, radius) &&
                DefenseBattle.CanReceiveAreaSpell(entry.Key, spell.Value);
            SetEnemyPreviewMaterial(entry.Value, eligible ? silhouette : null);
        }
    }

    public void PlayAreaSpell(DefenseBattle.Spell spell, Vector3 center, float radius,
        IReadOnlyList<EnemyUnit> recipients)
    {
        if (recipients.Count == 0)
            return;

        spellEffects.PlayArea(spell, center, radius);
        Color flashColor = SpellEffects.SpellColor(spell);
        foreach (EnemyUnit enemy in recipients)
        {
            if (enemyViews.TryGetValue(enemy, out EnemyView view))
            {
                view.SpellFlashColor = flashColor;
                view.SpellFlashRemaining = SpellBodyFlashDuration;
                ApplyEnemyTint(view);
            }
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

    private void SetEnemyPreviewMaterial(EnemyView view, Material material)
    {
        if (view.PreviewMaterial == material)
            return;

        view.PreviewMaterial = material;
        if (view.FactionRing != null)
            view.FactionRing.enabled = material == null;

        if (view.PreviewSurfaces == null)
        {
            view.PreviewSurfaces = new (Renderer, Material[], Material[], int)[view.Renderers.Length];
            for (int i = 0; i < view.Renderers.Length; i++)
            {
                Renderer renderer = view.Renderers[i];
                Material[] original = renderer.sharedMaterials;
                view.PreviewSurfaces[i] = (renderer, original, new Material[original.Length], renderer.sortingOrder);
            }
        }

        foreach (var (renderer, original, preview, order) in view.PreviewSurfaces)
        {
            if (material != null)
            {
                for (int i = 0; i < original.Length; i++)
                    preview[i] = TexturedSilhouette(material, original[i]);
            }
            renderer.sharedMaterials = material != null ? preview : original;
            renderer.sortingOrder = material != null ? AimDimSortingOrder + 1 : order;
        }
    }

    private Material TexturedSilhouette(Material preview, Material source)
    {
        if (source == null)
            return preview;

        var key = (preview, source);
        if (texturedSilhouettes.TryGetValue(key, out Material result))
            return result;

        result = new Material(preview) { name = $"{preview.name} ({source.name})" };
        if (source.mainTexture != null)
        {
            result.SetTexture("_SourceMap", source.mainTexture);
            result.SetTextureScale("_SourceMap", source.mainTextureScale);
            result.SetTextureOffset("_SourceMap", source.mainTextureOffset);
        }
        if (source.HasProperty(BaseColor))
            result.SetColor("_SourceColor", source.GetColor(BaseColor));
        texturedSilhouettes.Add(key, result);
        materials.Add(result);
        return result;
    }

    private void ApplyEnemyTint(EnemyView view)
    {
        float flashStrength = Mathf.SmoothStep(0f, 1f, view.SpellFlashRemaining / SpellBodyFlashDuration);
        Color flashTint = view.SpellFlashColor * SpellBodyFlashBrightness;
        foreach (var (renderer, slot, baseColor) in view.TintSlots)
        {
            if (flashStrength == 0f)
            {
                renderer.SetPropertyBlock(null, slot);
                continue;
            }

            Color tint = Color.Lerp(baseColor, flashTint, flashStrength);
            tint.a = baseColor.a;
            tintBlock.SetColor(BaseColor, tint);
            renderer.SetPropertyBlock(tintBlock, slot);
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
        bool hasView = enemyViews.TryGetValue(enemy, out EnemyView view);
        Vector3 shown = hasView ? view.Root.localPosition : enemy.Position;
        Vector3 worldPoint = root.TransformPoint(shown + Vector3.up * height);
        if (hasView && view.Animator != null)
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
        EnemyCatalog.UnitVisual look = default;
        GameObject prefab = catalog != null && catalog.TryGetVisual(enemy.Faction.UnitId, out look) ? look.Model : null;
        if (prefab != null)
        {
            GameObject visual = Object.Instantiate(prefab, model);
            ApplyUnitVisual(visual, look);

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
            view.FactionRing = renderer;
        }
        else
        {
            view = CreateBlockEnemy(model, material, enemy.FactionIndex);
        }
        view.TintSlots = CollectTintSlots(view.Renderers);
        enemyViews.Add(enemy, view);
        SyncEnemy(enemy);
    }

    private static (Renderer, int, Color)[] CollectTintSlots(Renderer[] renderers)
    {
        var slots = new List<(Renderer, int, Color)>();
        foreach (Renderer renderer in renderers)
        {
            Material[] shared = renderer.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                Material material = shared[i];
                if (material != null && material.HasProperty(BaseColor))
                    slots.Add((renderer, i, material.GetColor(BaseColor)));
            }
        }
        return slots.ToArray();
    }

    // 팩션에 지정한 모델은 FBX 를 그대로 쓰므로 애니메이터, 머티리얼, 크기를 여기서 맞춘다.
    private static void ApplyUnitVisual(GameObject visual, EnemyCatalog.UnitVisual look)
    {
        visual.transform.localScale = Vector3.one * look.ModelScale;

        if (look.Animations != null)
        {
            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = visual.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = look.Animations;
            animator.Rebind();
        }

        if (look.Skin != null)
        {
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = look.Skin;
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

    public void RemoveEnemy(EnemyUnit enemy, int currentCoreHealth)
    {
        // 코어 체력이 이미 0이면 같은 틱의 추가 도착은 HUD에서 뺄 피해가 없다.
        int coreDamage = observedCoreHealth - currentCoreHealth;
        observedCoreHealth = currentCoreHealth;
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
        if (view.BuffMarks != null)
            view.BuffMarks.gameObject.SetActive(false);
        if (enemy.ReachedCore)
        {
            // 코어 피해를 처리한 적의 모델만 남겨 타격과 퇴장 연출을 재생한다.
            BeginCoreStrike(enemy, view, coreDamage);
            return;
        }
        Object.Destroy(view.Root.gameObject);
    }

    private void BeginCoreStrike(EnemyUnit enemy, EnemyView view, int coreDamage)
    {
        SetEnemyPreviewMaterial(view, null);
        view.SpellFlashRemaining = 0f;
        ApplyEnemyTint(view);

        Vector3 heading = enemy.Heading.normalized;
        view.Root.localRotation = Quaternion.LookRotation(heading);
        bool animated = view.Animator != null && view.Animator.HasState(0, AttackState);

        var strike = new CoreStrike
        {
            View = view,
            From = view.Root.localPosition,
            Stance = enemy.Position - heading * 0.32f,
            Impact = enemy.Position + heading * (animated ? 0.58f : 0.16f) +
                Vector3.up * (animated ? 0.85f : 0.45f),
            Heading = heading,
            Animated = animated,
            CoreDamage = coreDamage
        };
        if (animated)
        {
            Animator animator = view.Animator;
            animator.speed = 1f;
            animator.CrossFadeInFixedTime(AttackState, AnimationBlendTime, 0, 0f);
            animator.Update(0f);
            strike.AttackLength = GetPlaybackState(animator).length;
        }
        if (view.Animator != null)
            view.Animator.speed = 0f;
        PoseCoreStrike(strike, 0f);
        PendingCoreDamage += coreDamage;
        coreStrikes.Add(strike);
    }

    private void AdvanceCoreStrikes(float deltaTime)
    {
        bool coreImpacted = false;
        for (int i = coreStrikes.Count - 1; i >= 0; i--)
        {
            CoreStrike strike = coreStrikes[i];
            strike.Age += deltaTime;
            float impactProgress = Mathf.Clamp01(strike.Age / CoreStrikeWindup);
            float fadeProgress = Mathf.Clamp01((strike.Age - CoreStrikeWindup) / CoreStrikeFadeDuration);
            PoseCoreStrike(strike, impactProgress, fadeProgress);

            if (!strike.HasHit && impactProgress >= 1f)
            {
                strike.HasHit = true;
                PendingCoreDamage -= strike.CoreDamage;
                coreImpacted = true;
                NextHitEffect().Play(root.TransformPoint(strike.Impact), CoreDamageColor, CoreStrikeFlashScale,
                    colorEmphasis: 0.85f);
                PrepareCoreStrikeFade(strike.View);
            }
            if (!strike.HasHit)
                continue;

            if (fadeProgress < 1f)
            {
                SetCoreStrikeOpacity(strike.View, 1f - Mathf.SmoothStep(0f, 1f, fadeProgress));
                continue;
            }

            Object.Destroy(strike.View.Root.gameObject);
            coreStrikes.RemoveAt(i);
        }
        if (coreImpacted)
        {
            PlayCoreHit();
            spellEffects.PlayCoreShockwave(coreShockwaveCenter, CoreColor);
        }
    }

    private static void PoseCoreStrike(CoreStrike strike, float impactProgress, float fadeProgress = 0f)
    {
        EnemyView view = strike.View;
        float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(impactProgress * 3f));
        view.Root.localPosition = Vector3.Lerp(strike.From, strike.Stance, settle);
        if (strike.Animated)
        {
            // Advance the blend and attack together on the scripted core impact timeline.
            float pose = HitImpactTime * impactProgress + 0.33f * fadeProgress;
            Animator animator = view.Animator;
            animator.speed = 1f;
            animator.Update((pose - strike.SampledPose) * strike.AttackLength);
            animator.speed = 0f;
            strike.SampledPose = pose;
        }
        else
        {
            float lunge = Mathf.SmoothStep(0f, 1f, impactProgress) * (1f - fadeProgress);
            view.Root.localPosition += strike.Heading * (0.24f * lunge);
        }
    }

    private void PrepareCoreStrikeFade(EnemyView view)
    {
        if (view.FactionRing != null)
            view.FactionRing.enabled = false;
        foreach (Renderer renderer in view.Renderers)
        {
            Material[] shared = renderer.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                Material original = shared[i];
                if (original == null || !original.HasProperty(BaseColor) || !original.HasProperty("_Surface"))
                    continue;

                if (!fadeMaterials.TryGetValue(original, out Material fade))
                {
                    fade = new Material(original);
                    fade.SetFloat("_Surface", 1f);
                    fade.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    fade.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    fade.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    fade.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    fade.SetFloat("_ZWrite", 0f);
                    fade.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    fade.DisableKeyword("_ALPHAMODULATE_ON");
                    fade.SetShaderPassEnabled("ShadowCaster", false);
                    fade.renderQueue = (int)RenderQueue.Transparent;
                    fadeMaterials.Add(original, fade);
                    materials.Add(fade);
                }
                shared[i] = fade;
            }
            renderer.sharedMaterials = shared;
        }
    }

    private void SetCoreStrikeOpacity(EnemyView view, float opacity)
    {
        foreach (var (renderer, slot, baseColor) in view.TintSlots)
        {
            Color color = baseColor;
            color.a *= opacity;
            tintBlock.SetColor(BaseColor, color);
            renderer.SetPropertyBlock(tintBlock, slot);
        }
    }

    public void AdvanceEffects(float deltaTime)
    {
        foreach (HitEffect effect in hitEffects)
            if (effect.IsPlaying)
                effect.Advance(deltaTime);
        spellEffects.Advance(deltaTime);
        if (deltaTime <= 0f)
            return;

        if (coreFlashRemaining > 0f)
        {
            coreFlashRemaining = Mathf.Max(0f, coreFlashRemaining - deltaTime);
            ApplyCoreFlash();
        }
        AdvanceCoreStrikes(deltaTime);
        foreach (EnemyView view in enemyViews.Values)
        {
            if (view.SpellFlashRemaining <= 0f)
                continue;

            view.SpellFlashRemaining = Mathf.Max(0f, view.SpellFlashRemaining - deltaTime);
            ApplyEnemyTint(view);
        }
    }

    // interpolation: 직전 틱(0)과 현재 틱(1) 사이 어디를 그릴지. deltaTime 은 화면용 전투 시간이다.
    public void SyncEnemy(EnemyUnit enemy, float deltaTime = 0f, float playbackSpeed = 1f, float interpolation = 1f)
    {
        EnemyView view = enemyViews[enemy];
        Vector3 shown = Vector3.Lerp(enemy.PreviousPosition, enemy.Position, interpolation);
        Vector3 step = shown - view.Root.localPosition;
        view.Root.localPosition = shown;
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
        SyncBuffMarks(view, enemy);
        bool movingWithBuff = enemy.MoveSpeedBuffStacks > 0 && IsMoving(step, deltaTime, playbackSpeed);
        spellEffects.SyncMoveTrail(view.Root, step, movingWithBuff, ref view.MoveSpeedTrails);
    }

    private void SyncBuffMarks(EnemyView view, EnemyUnit enemy)
    {
        int attack = enemy.AttackBuffStacks;
        int speed = enemy.MoveSpeedBuffStacks;
        int armor = enemy.ArmorBuffStacks;
        if (attack == 0 && speed == 0 && armor == 0)
        {
            if (view.BuffMarks != null)
                view.BuffMarks.gameObject.SetActive(false);
            return;
        }

        if (view.BuffMarks == null)
        {
            var marks = new GameObject("Buff Marks").transform;
            marks.SetParent(view.Root, false);
            view.BuffMarkRenderers = new[]
            {
                AddBuffMark("Attack", marks, -BuffMarkSpacing, attackBuffMarkMaterial),
                AddBuffMark("Move Speed", marks, 0f, moveSpeedBuffMarkMaterial),
                AddBuffMark("Armor", marks, BuffMarkSpacing, armorBuffMarkMaterial)
            };
            view.BuffMarkStacks = new int[3];
            view.BuffMarks = marks;
        }

        view.BuffMarks.localRotation = Quaternion.Inverse(view.Root.localRotation) * buffMarkRotation;
        SyncBuffMark(view, 0, attack);
        SyncBuffMark(view, 1, speed);
        SyncBuffMark(view, 2, armor);
        view.BuffMarks.gameObject.SetActive(true);
    }

    private void SyncBuffMark(EnemyView view, int index, int stacks)
    {
        MeshRenderer mark = view.BuffMarkRenderers[index];
        mark.gameObject.SetActive(stacks > 0);
        if (view.BuffMarkStacks[index] == stacks)
            return;

        view.BuffMarkStacks[index] = stacks;
        if (stacks <= 1)
        {
            mark.SetPropertyBlock(null);
            return;
        }

        float brightness = Mathf.Max(BuffMarkMinimumBrightness, 1f - (stacks - 1) * BuffMarkDarkeningPerStack);
        Color baseColor = mark.sharedMaterial.color;
        tintBlock.SetColor(BaseColor, new Color(baseColor.r * brightness, baseColor.g * brightness,
            baseColor.b * brightness, baseColor.a));
        mark.SetPropertyBlock(tintBlock);
    }

    private static MeshRenderer AddBuffMark(string label, Transform marks, float x, Material material)
    {
        Transform mark = Box(label, marks, new Vector3(x, SpawnPadHeight + 0.02f, BuffMarkOffset),
            new Vector3(0.095f, 0.012f, 0.055f), material);
        var renderer = mark.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
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

        AnimatorStateInfo state = GetPlaybackState(view.Animator);
        return state.fullPathHash != AttackState || state.normalizedTime >= HitImpactTime;
    }

    private static AnimatorStateInfo GetPlaybackState(Animator animator) =>
        animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);

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
        AnimatorStateInfo state = GetPlaybackState(animator);
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
        // 화면 보간 step은 틱이 없는 프레임에 0이 될 수 있으므로 실제 틱 이동량으로 상태와 속도를 정한다.
        Vector3 tickStep = enemy.Position - enemy.PreviousPosition;
        bool moving = IsMoving(tickStep, deltaTime, playbackSpeed);
        view.StoppedTime = moving ? 0f : view.StoppedTime + deltaTime;
        bool newAttack = view.AttacksPlayed != enemy.AttacksPerformed;
        if (newAttack)
            view.AttacksPlayed = enemy.AttacksPerformed;
        newAttack &= animator.HasState(0, AttackState);
        if (newAttack)
        {
            view.AnimationState = AttackState;
            animator.CrossFadeInFixedTime(AttackState, AnimationBlendTime, 0, 0f);
        }
        else
        {
            AnimatorStateInfo currentState = GetPlaybackState(animator);
            bool attackFinished = currentState.fullPathHash == AttackState && currentState.normalizedTime >= 1f;
            if (moving || view.AnimationState != AttackState || attackFinished)
            {
                // Keep the run phase across brief gaps between movement steps.
                bool resumeRun = view.AnimationState == view.MoveState && view.StoppedTime < RunStopGrace;
                int state = moving || resumeRun ? view.MoveState :
                    animator.HasState(0, IdleState) ? IdleState : WalkState;
                if (view.AnimationState != state)
                {
                    // The first pose has no prior presentation state to blend from.
                    if (view.AnimationState == 0)
                        animator.Play(state, 0, 0f);
                    else
                        animator.CrossFadeInFixedTime(state, AnimationBlendTime, 0, 0f);
                    view.AnimationState = state;
                }
            }
        }

        Vector3 travelHeading = step.sqrMagnitude > 0f ? step : tickStep;
        Vector3 facing = view.AnimationState == AttackState ? enemy.AttackHeading :
            moving ? travelHeading : enemy.Heading;
        Quaternion heading = Quaternion.LookRotation(facing);
        view.Root.localRotation = newAttack || deltaTime <= 0f ? heading :
            Quaternion.RotateTowards(view.Root.localRotation, heading, GoblinTurnSpeed * deltaTime);

        if (view.AnimationState == AttackState)
            animator.speed = playbackSpeed * AttackPlaybackScale(animator, enemy);
        else if (view.AnimationState == view.MoveState && moving)
            animator.speed = tickStep.magnitude * playbackSpeed /
                (DefenseBattle.TickDuration * view.StrideSpeed * animator.transform.localScale.z);
        else
            animator.speed = playbackSpeed * (view.AnimationState == IdleState ? 1f : StationaryWalkSpeed);
    }

    public void ReleaseMaterials()
    {
        Release(areaSpellPreviewMesh);
        Release(factionRingMesh);
        if (aimDim != null && aimDim.sprite != null)
        {
            Release(aimDim.sprite.texture);
            Release(aimDim.sprite);
        }
        foreach (Material material in materials)
            Release(material);
        foreach (Mesh mesh in meshes)
            Release(mesh);
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

    // mesh를 주면 그 크기 그대로 쓰는 메시이므로 size로 늘리지 않는다.
    private static Transform Box(string label, Transform parent, Vector3 position, Vector3 size, Material material,
        Mesh mesh = null)
    {
        GameObject cube;
        if (mesh == null)
        {
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Release(cube.GetComponent<BoxCollider>());
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
