using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public sealed class VoxelDefensePrototype : MonoBehaviour
{
    private const float RightDragThreshold = 6f;

    [System.Serializable]
    private sealed class FactionSlot
    {
        [SerializeField, Tooltip("Enemy Catalog의 유닛 ID. 비우면 이 종족은 라운드 편성으로만 나온다.")]
        private string unitId;
        [SerializeField] private string displayNameKey;
        [SerializeField] private Color color;
        [SerializeField, Min(0)] private int spawnCount;

        public FactionSlot() { }
        public FactionSlot(string displayNameKey, Color color)
        {
            this.displayNameKey = displayNameKey;
            this.color = color;
        }

        public Color Color => color;

        public EnemyFaction GetFaction(EnemyCatalog catalog)
        {
            string displayName = GameText.Get(displayNameKey);
            return string.IsNullOrWhiteSpace(unitId)
                ? new EnemyFaction(displayName, color, spawnCount: 0)
                : catalog.GetFaction(unitId, displayName, color, spawnCount);
        }
    }

    [SerializeField] private Camera boardCamera;
    [SerializeField] private Shader voxelShader;
    [SerializeField] private UIManager ui;
    [SerializeField] private Texture2D encounterIcon;
    [SerializeField] private Sprite routeChevron;
    [SerializeField] private EnemyCatalog enemyCatalog;
    [SerializeField, Tooltip("임시 적 타격 효과음. 비우면 타격음 없이 재생한다.")]
    private AudioClip hitSound;
    [SerializeField] private DefenseHud.Art hudArt = new();
    [Tooltip("선택. 연결하면 기존 코어 패널 대신 초상이 있는 좌상단 코어 HUD를 쓴다.")]
    [SerializeField] private LeftCoreHud leftCoreHud;
    [Tooltip("선택. 연결하면 기존 마나 게이지 대신 하단 마나 바 아트를 쓴다.")]
    [SerializeField] private ManaBarHud manaBarHud;
    [Tooltip("선택. 연결하면 상단 중앙 정보·우상단 설정/배속·우하단 웨이브 호출 버튼을 새 HUD로 쓴다.")]
    [SerializeField] private BattleInfoHud battleInfoHud;
    [Tooltip("선택. 연결하면 유닛이 쓰러진 자리에 스테이지 입자(낙엽·눈)를 흩날린다.")]
    [SerializeField] private StageAmbience stageAmbience;
    [Tooltip("선택. 연결하면 벽은 건설 모드(왼쪽 토글 버튼, B)에서만 놓고 철거한다.")]
    [SerializeField] private BuildModeHud buildModeHud;
    [SerializeField] private BoardTheme boardTheme = new();
    // 진영 = 종족. 순서는 EnemyFactionGroup 과 같다.
    [SerializeField, FormerlySerializedAs("coral")]
    private FactionSlot greenHorde = new("faction.goblin", StageData.FactionColors[0]);
    [SerializeField, FormerlySerializedAs("teal")]
    private FactionSlot undead = new("faction.undead", StageData.FactionColors[1]);
    [SerializeField] private FactionSlot beasts = new("faction.beast", StageData.FactionColors[2]);
    [SerializeField] private FactionSlot dragonAndDemon = new("faction.dragon_demon", StageData.FactionColors[3]);
    [SerializeField, Tooltip("보드 크기·코어·스폰·고정 장애물·웨이브 편성. Tools/Stage Editor에서 편집한다. 비우면 기본 지형을 쓴다.")]
    private StageData stage;
    [Tooltip("선택. 연결하면 마지막 웨이브를 클리어했을 때 승리 패널을 띄운다.")]
    [SerializeField] private VictoryPanelHud victoryPanelHud;

    private DefenseGrid grid;
    private DefenseBoardView view;
    private DefenseBattle battle;
    private BattleSoundPlayer soundPlayer;
    private DefenseHud hud;
    private readonly BattleClock battleClock = new();
    private DefenseBattle.Spell? aimedSpell;
    private float aimedRangeScroll;
    private int battleSpeed = 1;
    private bool buildMode;
    private BuildModeHud.Tool buildTool;
    private bool trackingRightButton;
    private bool draggingCamera;
    private Vector2 previousDragPointer;
    private Vector2? rightClickPointer;
    private StageData usedStage;
    private int fallenEnemies;

    private int SpellRangeStep => Mathf.RoundToInt(aimedRangeScroll);

    [SerializeField] private bool waitForStageSelection;
    private System.Action restartStage;

    private void Awake()
    {
        if (waitForStageSelection)
        {
            if (victoryPanelHud != null)
                victoryPanelHud.Bind(next: null, retry: Restart);
            enabled = false;
            return;
        }
        InitializeBattle();
    }

    public void InitializeStage(StageContent content, System.Action retry)
    {
        if (view != null)
            throw new System.InvalidOperationException("A stage has already been initialized.");
        stage = content.Data;
        boardTheme = content.Theme;
        boardCamera = content.BoardCamera;
        stageAmbience = content.Ambience;
        restartStage = retry;
        enabled = true;
        InitializeBattle();
    }

    private void InitializeBattle()
    {
        if (enemyCatalog == null)
        {
            Debug.LogError("Assign an Enemy Catalog to VoxelDefensePrototype.", this);
            enabled = false;
            return;
        }

        var factions = new EnemyFaction[StageData.FactionCount];
        for (int i = 0; i < factions.Length; i++)
        {
            factions[i] = Slot(i).GetFaction(enemyCatalog);
            if (factions[i] == null)
            {
                enabled = false;
                return;
            }
        }

        grid = CreateGrid(out usedStage);
        soundPlayer = gameObject.AddComponent<BattleSoundPlayer>();
        soundPlayer.Initialize(hitSound);
        view = new DefenseBoardView(transform, boardCamera, voxelShader, grid, factions, routeChevron, boardTheme,
            enemyCatalog, usedStage, soundPlayer.PlayHit);
        StageWaves waves = UsableWaves();
        battle = new DefenseBattle(grid, factions, BuildRoundLineup(waves), waves != null ? waves.waves.Count : 0);
        battle.EnemySpawned += view.AddEnemy;
        battle.EnemyRemoved += enemy => view.RemoveEnemy(enemy, battle.CoreHealth);
        battle.EnemyRemoved += enemy =>
        {
            if (!enemy.ReachedCore)
                fallenEnemies++;
        };
        if (victoryPanelHud != null)
            victoryPanelHud.Bind(next: null, retry: Restart);
        battle.SpellResolved += OnSpellResolved;
        hud = new DefenseHud(ui, Restart, StartBattle, UseSpell, SetBattleSpeed, encounterIcon, hudArt, factions);
        if (leftCoreHud != null)
            hud.UseExternalCorePanel(new Vector2(320, -236));
        if (manaBarHud != null)
            hud.UseExternalManaBar((RectTransform)manaBarHud.transform, new Vector2(33, hudArt.SlotFrame != null ? -146 : -130));
        if (buildModeHud != null)
        {
            hud.UseExternalBuildPanel();
            buildModeHud.Bind(ToggleBuildMode, tool => buildTool = tool, hud.ActionBar);
        }
        if (stageAmbience != null)
            battle.EnemyRemoved += enemy => stageAmbience.Burst(transform.TransformPoint(enemy.Position));
        if (battleInfoHud != null)
        {
            hud.UseExternalBattleInfo();
            battleInfoHud.Bind(StartBattle, SetBattleSpeed, () => hud.Notify(GameText.Get("notice.settings_pending")));
        }
        RefreshPlanningTiles();
    }

    private FactionSlot Slot(int faction) => faction switch
    {
        0 => greenHorde,
        1 => undead,
        2 => beasts,
        _ => dragonAndDemon
    };

    private DefenseGrid CreateGrid(out StageData usedStage)
    {
        usedStage = null;
        if (stage == null)
            return new DefenseGrid();

        DefenseGrid stageGrid = stage.CreateGrid();
        string error = stage.Validate(stageGrid);
        if (error == null)
        {
            usedStage = stage;
            return stageGrid;
        }

        Debug.LogError($"Stage '{stage.name}' is invalid ({error}). Using the default terrain.", this);
        return new DefenseGrid();
    }

    // 스테이지의 웨이브 편성. 없거나 잘못되었으면 null(기존 증원 규칙으로 끝없이 진행).
    private StageWaves UsableWaves()
    {
        StageWaves waves = usedStage != null ? usedStage.waves : null;
        if (waves == null)
            return null;

        string error = waves.Validate();
        if (error != null)
        {
            Debug.LogError($"Waves '{waves.name}' are invalid ({error}). Using the default reinforcement rules.", waves);
            return null;
        }

        foreach (StageWaves.Wave wave in waves.waves)
            foreach (StageWaves.FactionEntry entry in wave.factions)
                if (!HasSpawn(entry.faction))
                    Debug.LogWarning($"Waves '{waves.name}' ({wave.note}): faction {entry.faction} has no spawn on " +
                        "this stage. Its units will not appear.", waves);
        return waves;
    }

    // 매 준비 단계(라운드 시작 전)에 팩션별로 그룹 하나를 무작위로 골라 스폰 순서로 펼친다.
    // 무작위는 전투 입력(편성)을 정할 때만 쓰므로 전투 결정론에는 영향이 없다. 같은 유닛 종류는 한 객체를 공유한다.
    private System.Func<int, IReadOnlyList<EnemyFaction>[]> BuildRoundLineup(StageWaves waves)
    {
        if (waves == null)
            return null;

        var units = new Dictionary<(string, int), EnemyFaction>();
        return round =>
        {
            StageWaves.Group[] groups = waves.PickGroups(round - 1, n => Random.Range(0, n));
            var lineup = new IReadOnlyList<EnemyFaction>[StageData.FactionCount];
            for (int side = 0; side < StageData.FactionCount; side++)
            {
                var queue = new List<EnemyFaction>();
                lineup[side] = queue;
                if (groups[side] == null)
                    continue;
                foreach (StageWaves.Entry entry in groups[side].enemies)
                {
                    if (entry == null || entry.count <= 0)
                        continue;
                    if (!units.TryGetValue((entry.unitId, side), out EnemyFaction unit))
                    {
                        units[(entry.unitId, side)] = unit = enemyCatalog.GetUnit(entry.unitId, Slot(side).Color, $"Wave {round}");
                        if (enemyCatalog.TryGetFactionGroup(entry.unitId, out EnemyFactionGroup race) && (int)race != side)
                            Debug.LogWarning($"Wave {round}: '{entry.unitId}' is {race} but listed under faction {side}. " +
                                $"It fights for faction {side}.", waves);
                    }
                    if (unit == null)
                        continue;
                    for (int i = 0; i < entry.count; i++)
                        queue.Add(unit);
                }
            }
            return lineup;
        };
    }

    private bool HasSpawn(int faction)
    {
        foreach (int spawnFaction in grid.SpawnFactions)
            if (spawnFaction == faction)
                return true;
        return false;
    }

    private void Update()
    {
        view.FitCamera();
        Keyboard keyboard = Keyboard.current;
        bool boardInputAllowed = !ui.HasOpenPopup;
        if ((boardInputAllowed || battle.CurrentPhase is DefenseBattle.Phase.Defeat or DefenseBattle.Phase.Victory) &&
            keyboard?.rKey.wasPressedThisFrame == true)
        {
            Restart();
            return;
        }

        if (boardInputAllowed)
        {
            ReadCameraInput(keyboard);
            view.AdvanceZoom(Time.unscaledDeltaTime);
            if (battle.CurrentPhase == DefenseBattle.Phase.Combat)
                ReadCombatInput(keyboard);
        }
        else
        {
            ResetRightButton();
        }

        battleClock.BeginFrame(Time.deltaTime, battleSpeed, aimedSpell.HasValue);
        switch (battle.CurrentPhase)
        {
            case DefenseBattle.Phase.Preparation:
                if (!boardInputAllowed)
                    break;

                if (buildModeHud != null && keyboard?.bKey.wasPressedThisFrame == true)
                    ToggleBuildMode();
                ReadBuildInput();
                if (keyboard?.spaceKey.wasPressedThisFrame == true)
                    StartBattle();
                break;
            case DefenseBattle.Phase.Combat:
                // 고정 틱만 돌린다. 마나 회복과 큐에 넣은 마법도 틱 안에서 처리된다.
                for (int tick = 0; tick < battleClock.StepCount && battle.CurrentPhase == DefenseBattle.Phase.Combat; tick++)
                    battle.Tick(battleClock.StepDeltaTime, pauseMovement: false);

                if (battleClock.DeltaTime > 0f)
                    foreach (EnemyUnit enemy in battle.Enemies)
                        view.SyncEnemy(enemy, battleClock.DeltaTime, battleClock.PlaybackSpeed, battleClock.Interpolation);
                if (battle.CurrentPhase == DefenseBattle.Phase.Preparation)
                {
                    RefreshPlanningTiles();
                    hud.Notify(GameText.Get("notice.wave_cleared"));
                    if (leftCoreHud != null)
                        leftCoreHud.Cheer();
                }
                else if (battle.CurrentPhase == DefenseBattle.Phase.Defeat)
                {
                    hud.ShowDefeat();
                }
                else if (battle.CurrentPhase == DefenseBattle.Phase.Victory)
                {
                    ShowVictory();
                }
                break;
        }
    }

    private void ShowVictory()
    {
        if (leftCoreHud != null)
            leftCoreHud.Cheer();
        if (victoryPanelHud == null)
        {
            hud.Notify(GameText.Get("notice.victory_restart"));
            return;
        }
        int coreMax = DefenseBattle.CoreMaxHealth;
        victoryPanelHud.Show(usedStage != null ? GameText.Get(usedStage.displayNameKey) : null, battle.CoreHealth, coreMax,
            coreMax - battle.CoreHealth, fallenEnemies);
    }

    private void SetBattleSpeed(int speed)
    {
        if (!ui.HasOpenPopup && battle.CurrentPhase is not (DefenseBattle.Phase.Defeat or DefenseBattle.Phase.Victory))
            battleSpeed = speed;
    }

    private void StartBattle()
    {
        if (ui.HasOpenPopup || battle.CurrentPhase != DefenseBattle.Phase.Preparation)
            return;

        buildMode = false;
        battle.StartRound();
        battleClock.Reset();
        view.ShowBattleRoutes(grid.Routes);
        hud.Notify("");
    }

    private void ReadCameraInput(Keyboard keyboard)
    {
        rightClickPointer = null;
        if (keyboard != null)
        {
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            if (input != Vector2.zero)
                view.PanCamera(input, Time.unscaledDeltaTime);
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            ResetRightButton();
            return;
        }

        Vector2 pointer = mouse.position.ReadValue();
        ReadRightButton(mouse, pointer);
        float scrollDelta = mouse.scroll.ReadValue().y;
        // 도발은 반경이 고정이라 조준 중에도 휠로 카메라를 확대·축소한다.
        if (aimedSpell is { } aimed && DefenseBattle.IsAreaSpell(aimed))
            aimedRangeScroll = Mathf.Clamp(aimedRangeScroll + scrollDelta,
                DefenseBattle.MinSpellRangeStep, DefenseBattle.MaxSpellRangeStep);
        else if (scrollDelta != 0f)
            view.QueueZoom(scrollDelta, pointer);
    }

    private void ReadRightButton(Mouse mouse, Vector2 pointer)
    {
        if (mouse.rightButton.wasPressedThisFrame)
        {
            trackingRightButton = boardCamera.pixelRect.Contains(pointer) && !ui.BlocksBoardPointer(pointer);
            draggingCamera = false;
            previousDragPointer = pointer;
        }

        if (!trackingRightButton)
            return;

        if (!mouse.rightButton.isPressed && !mouse.rightButton.wasReleasedThisFrame)
        {
            ResetRightButton();
            return;
        }

        if ((pointer - previousDragPointer).sqrMagnitude >= RightDragThreshold * RightDragThreshold)
            draggingCamera = true;

        if (draggingCamera && pointer != previousDragPointer)
        {
            view.DragCamera(previousDragPointer, pointer);
            previousDragPointer = pointer;
        }

        if (!mouse.rightButton.isPressed)
        {
            bool clicked = !draggingCamera && boardCamera.pixelRect.Contains(pointer) && !ui.BlocksBoardPointer(pointer);
            ResetRightButton();
            if (clicked)
                rightClickPointer = previousDragPointer;
        }
    }

    private void ResetRightButton()
    {
        trackingRightButton = false;
        draggingCamera = false;
        rightClickPointer = null;
    }

    private void ToggleBuildMode()
    {
        if (ui.HasOpenPopup || battle.CurrentPhase != DefenseBattle.Phase.Preparation)
            return;

        buildMode = !buildMode;
        buildTool = BuildModeHud.Tool.Place;
        hud.Notify("");
    }

    // 놓을 칸 미리보기: 건설 모드에서 놓기 도구일 때 마우스 아래 칸에 벽을 띄운다. 길 계산은 칸이나 벽 배치가 바뀔 때만 다시 한다.
    private Vector2Int ghostCell = new(-1, -1);
    private int ghostWallCount = -1;
    private DefenseGrid.BuildResult ghostResult;

    private void UpdateBuildGhost()
    {
        Mouse mouse = Mouse.current;
        bool placing = buildModeHud != null && buildMode && buildTool == BuildModeHud.Tool.Place
            && battle.CurrentPhase == DefenseBattle.Phase.Preparation && !ui.HasOpenPopup && mouse != null;
        if (!placing)
        {
            view.HideBuildGhost();
            return;
        }

        Vector2 pointer = mouse.position.ReadValue();
        if (ui.BlocksBoardPointer(pointer) || !view.TryPickCell(pointer, out Vector2Int cell))
        {
            view.HideBuildGhost();
            return;
        }

        if (cell != ghostCell || grid.WallCount != ghostWallCount)
        {
            ghostCell = cell;
            ghostWallCount = grid.WallCount;
            ghostResult = grid.CheckPlace(cell);
        }
        if (ghostResult is DefenseGrid.BuildResult.Outside or DefenseGrid.BuildResult.Occupied)
            view.HideBuildGhost();
        else
            view.ShowBuildGhost(cell, ghostResult == DefenseGrid.BuildResult.Placed);
    }

    private void ReadBuildInput()
    {
        // 건설 모드 HUD가 있는 씬에서는 모드가 켜져 있을 때만 보드 클릭이 벽을 바꾼다.
        if (buildModeHud != null && !buildMode)
            return;

        Mouse mouse = Mouse.current;
        if (trackingRightButton || mouse == null || (!mouse.leftButton.wasPressedThisFrame && !rightClickPointer.HasValue))
            return;

        Vector2 pointer = rightClickPointer ?? mouse.position.ReadValue();
        if (ui.BlocksBoardPointer(pointer) || !view.TryPickCell(pointer, out Vector2Int cell))
        {
            return;
        }

        if (rightClickPointer.HasValue || (buildModeHud != null && buildTool == BuildModeHud.Tool.Remove))
        {
            if (grid.TryRemove(cell))
            {
                view.RemoveWall(cell);
                RefreshPlanningTiles();
                hud.Notify("");
            }
            return;
        }

        switch (grid.TryPlace(cell))
        {
            case DefenseGrid.BuildResult.Placed:
                view.PlaceWall(cell);
                RefreshPlanningTiles();
                hud.Notify("");
                break;
            case DefenseGrid.BuildResult.Occupied:
                hud.Notify(GameText.Get("notice.cell_occupied"));
                break;
            case DefenseGrid.BuildResult.LimitReached:
                hud.Notify(GameText.Get("notice.no_walls"));
                break;
            case DefenseGrid.BuildResult.BlocksRoute:
                hud.Notify(GameText.Get("notice.keep_routes"));
                break;
        }
    }

    private void RefreshPlanningTiles() => view.ShowPlanningTiles(grid.Routes, battle.ForecastEncounterCells());

    private void ReadCombatInput(Keyboard keyboard)
    {
        Mouse mouse = Mouse.current;
        if (aimedSpell.HasValue &&
            (keyboard?.escapeKey.wasPressedThisFrame == true || rightClickPointer.HasValue))
        {
            SetAimedSpell(null);
            hud.Notify("");
            return;
        }

        if (keyboard?.digit1Key.wasPressedThisFrame == true)
        {
            UseSpell(DefenseBattle.Spell.AttackBuff);
            return;
        }
        if (keyboard?.digit2Key.wasPressedThisFrame == true)
        {
            UseSpell(DefenseBattle.Spell.Heal);
            return;
        }
        if (keyboard?.digit3Key.wasPressedThisFrame == true)
        {
            UseSpell(DefenseBattle.Spell.MoveSpeedBuff);
            return;
        }
        if (keyboard?.digit4Key.wasPressedThisFrame == true)
        {
            UseSpell(DefenseBattle.Spell.ArmorBuff);
            return;
        }
        if (keyboard?.digit5Key.wasPressedThisFrame == true)
        {
            UseSpell(DefenseBattle.Spell.Taunt);
            return;
        }

        if (trackingRightButton || aimedSpell is not { } spell || mouse?.leftButton.wasPressedThisFrame != true)
            return;
        if (spell == DefenseBattle.Spell.Taunt)
        {
            Vector2 pointer = mouse.position.ReadValue();
            if (ui.BlocksBoardPointer(pointer))
                return;
            if (view.TryPickEnemy(pointer, battle.Enemies, out EnemyUnit target))
                CastTaunt(target);
            else
                hud.Notify(GameText.Get("notice.pick_taunt_target"));
            return;
        }
        if (TryPickAreaSpellCenter(mouse, out Vector3 center))
            CastAimedSpell(spell, center);
    }

    private void CastTaunt(EnemyUnit target)
    {
        string blocked = battle.SpellBlockReason(DefenseBattle.Spell.Taunt, 0);
        if (blocked != null)
        {
            hud.Notify(blocked);
            return;
        }

        // 바로 적용하지 않고 다음 틱 시작에 적용한다. 결과는 OnSpellResolved 로 돌아온다.
        battle.QueueTaunt(target);
        SetAimedSpell(null);
    }

    private EnemyUnit HoveredTauntTarget()
    {
        Mouse mouse = Mouse.current;
        if (aimedSpell != DefenseBattle.Spell.Taunt || mouse == null)
            return null;
        Vector2 pointer = mouse.position.ReadValue();
        return !ui.BlocksBoardPointer(pointer) && view.TryPickEnemy(pointer, battle.Enemies, out EnemyUnit target)
            ? target
            : null;
    }

    private void CastAimedSpell(DefenseBattle.Spell spell, Vector3 center)
    {
        int rangeStep = SpellRangeStep;
        string blocked = battle.SpellBlockReason(spell, rangeStep);
        if (blocked != null)
        {
            hud.Notify(blocked);
            return;
        }

        // 바로 적용하지 않고 다음 틱 시작에 적용한다. 결과는 OnSpellResolved 로 돌아온다.
        battle.QueueSpell(spell, center, rangeStep);
        SetAimedSpell(null);
    }

    private void OnSpellResolved(DefenseBattle.SpellCommand command, string message,
        IReadOnlyList<EnemyUnit> recipients)
    {
        hud.Notify(message);
        // 도발의 첫 수신자는 도발 유닛이다. 시전 시점의 위치를 둘레로 반경을 보여준다.
        if (command.Spell == DefenseBattle.Spell.Taunt)
        {
            if (recipients.Count > 0)
                view.PlayAreaSpell(command.Spell, recipients[0].Position, DefenseBattle.TauntRadius, recipients);
            return;
        }
        view.PlayAreaSpell(command.Spell, command.Center, DefenseBattle.AreaSpellRadius(command.RangeStep), recipients);
    }

    private bool TryPickAreaSpellCenter(Mouse mouse, out Vector3 center)
    {
        center = default;
        if (mouse == null)
            return false;

        Vector2 pointer = mouse.position.ReadValue();
        return !ui.BlocksBoardPointer(pointer) && view.TryPickBoardPoint(pointer, out center);
    }

    private void UseSpell(DefenseBattle.Spell spell)
    {
        if (ui.HasOpenPopup || battle.CurrentPhase != DefenseBattle.Phase.Combat)
            return;

        if (aimedSpell == spell)
            return;

        int rangeStep = battle.InitialSpellRangeStep(spell);
        string blocked = battle.SpellBlockReason(spell, rangeStep);
        if (blocked == null)
            SetAimedSpell(spell, rangeStep);
        hud.Notify(blocked ?? "");
    }

    private void SetAimedSpell(DefenseBattle.Spell? spell, int rangeStep = 0)
    {
        aimedSpell = spell;
        aimedRangeScroll = rangeStep;
    }

    private void Restart()
    {
        if (restartStage != null)
            restartStage();
        else
            SceneManager.LoadScene(gameObject.scene.buildIndex);
    }

    private void LateUpdate()
    {
        if (ui.HasOpenPopup || battle.CurrentPhase != DefenseBattle.Phase.Combat)
            SetAimedSpell(null);

        view.AdvanceRouteMarkers(battle.CurrentPhase switch
        {
            DefenseBattle.Phase.Preparation => Time.unscaledDeltaTime,
            DefenseBattle.Phase.Combat => battleClock.DeltaTime,
            _ => 0f
        });
        view.AdvanceEffects(battleClock.DeltaTime);
        view.AdvanceCoreAnimation(battleClock.DeltaTime, battleClock.PlaybackSpeed);
        Vector3? spellCenter = aimedSpell.HasValue && TryPickAreaSpellCenter(Mouse.current, out Vector3 center)
            ? center
            : null;
        int rangeStep = SpellRangeStep;
        EnemyUnit tauntTarget = HoveredTauntTarget();
        view.ShowAreaSpellPreview(aimedSpell, spellCenter, DefenseBattle.AreaSpellRadius(rangeStep), tauntTarget);
        hud.Refresh(battle, grid, view, aimedSpell, spellCenter, rangeStep, battleSpeed, tauntTarget);
        if (leftCoreHud != null)
            leftCoreHud.Refresh(battle.CoreHealth, DefenseBattle.CoreMaxHealth);
        if (manaBarHud != null)
            manaBarHud.Refresh(battle.Mana, DefenseBattle.MaxMana);
        if (buildModeHud != null)
        {
            bool canBuild = !ui.HasOpenPopup && battle.CurrentPhase == DefenseBattle.Phase.Preparation;
            if (!canBuild)
                buildMode = false;
            buildModeHud.Refresh(canBuild, buildMode, DefenseGrid.WallLimit - grid.WallCount, DefenseGrid.WallLimit, buildTool);
            UpdateBuildGhost();
        }
        if (battleInfoHud != null)
        {
            var (leftSide, rightSide) = DefenseHud.ShownSides(battle);
            int left = 0, right = 0;
            foreach (EnemyUnit enemy in battle.Enemies)
            {
                if (!enemy.IsAlive)
                    continue;
                if (enemy.FactionIndex == leftSide)
                    left++;
                else if (enemy.FactionIndex == rightSide)
                    right++;
            }
            bool inCombat = battle.CurrentPhase == DefenseBattle.Phase.Combat;
            bool canCallWave = !ui.HasOpenPopup && battle.CurrentPhase == DefenseBattle.Phase.Preparation;
            battleInfoHud.Refresh(battle.Round, inCombat, canCallWave, left, right, Slot(leftSide).Color,
                Slot(rightSide).Color, battleSpeed);
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            ResetRightButton();
    }

    private void OnDisable()
    {
        ResetRightButton();
        SetAimedSpell(null);
    }

    private void OnDestroy() => view?.ReleaseMaterials();
}
