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
        [SerializeField] private string displayName;
        [SerializeField] private Color color;
        [SerializeField, Min(0)] private int spawnCount;

        public FactionSlot() { }
        public FactionSlot(string displayName, Color color)
        {
            this.displayName = displayName;
            this.color = color;
        }

        public Color Color => color;

        public EnemyFaction GetFaction(EnemyCatalog catalog) => string.IsNullOrWhiteSpace(unitId)
            ? new EnemyFaction(displayName, color, spawnCount: 0)
            : catalog.GetFaction(unitId, displayName, color, spawnCount);
    }

    [SerializeField] private Camera boardCamera;
    [SerializeField] private Shader voxelShader;
    [SerializeField] private UIManager ui;
    [SerializeField] private Texture2D encounterIcon;
    [SerializeField] private Sprite routeChevron;
    [SerializeField] private EnemyCatalog enemyCatalog;
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
    private FactionSlot greenHorde = new("Goblin", StageData.FactionColors[0]);
    [SerializeField, FormerlySerializedAs("teal")]
    private FactionSlot undead = new("Undead", StageData.FactionColors[1]);
    [SerializeField] private FactionSlot beasts = new("Beasts", StageData.FactionColors[2]);
    [SerializeField] private FactionSlot dragonAndDemon = new("Dragon", StageData.FactionColors[3]);
    [SerializeField, Tooltip("라운드별 편성. 마지막 라운드 다음에는 처음부터 반복한다. 비어 있으면 종족 슬롯과 기존 증원 규칙을 쓴다.")]
    private RoundLineup[] rounds = System.Array.Empty<RoundLineup>();
    [SerializeField, Tooltip("보드 크기·코어·스폰·고정 장애물. Tools/Stage Editor에서 편집한다. 비우면 기본 지형을 쓴다.")]
    private StageData stage;

    [System.Serializable]
    private sealed class SpawnGroup
    {
        [SerializeField, Tooltip("Enemy Catalog의 유닛 ID")]
        private string unitId;
        [SerializeField, Min(0)] private int count = 1;

        public string UnitId => unitId;
        public int Count => count;
    }

    [System.Serializable]
    private sealed class RoundLineup
    {
        [SerializeField] private string note;
        [SerializeField, FormerlySerializedAs("coral"), Tooltip("고블린 스폰. 위에서부터 순서대로 나온다.")]
        private SpawnGroup[] greenHorde = System.Array.Empty<SpawnGroup>();
        [SerializeField, FormerlySerializedAs("teal"), Tooltip("언데드 스폰. 위에서부터 순서대로 나온다.")]
        private SpawnGroup[] undead = System.Array.Empty<SpawnGroup>();
        [SerializeField, Tooltip("짐승 스폰. 위에서부터 순서대로 나온다.")]
        private SpawnGroup[] beasts = System.Array.Empty<SpawnGroup>();
        [SerializeField, Tooltip("드래곤·악마 스폰. 위에서부터 순서대로 나온다.")]
        private SpawnGroup[] dragonAndDemon = System.Array.Empty<SpawnGroup>();

        public SpawnGroup[] Side(int index) => index switch
        {
            0 => greenHorde,
            1 => undead,
            2 => beasts,
            _ => dragonAndDemon
        } ?? System.Array.Empty<SpawnGroup>();
    }

    private DefenseGrid grid;
    private DefenseBoardView view;
    private DefenseBattle battle;
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

    private int SpellRangeStep => Mathf.RoundToInt(aimedRangeScroll);

    private void Awake()
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

        grid = CreateGrid(out StageData usedStage);
        view = new DefenseBoardView(transform, boardCamera, voxelShader, grid, factions, routeChevron, boardTheme,
            enemyCatalog, usedStage);
        battle = new DefenseBattle(grid, factions, BuildRoundLineup());
        battle.EnemySpawned += view.AddEnemy;
        battle.EnemyRemoved += enemy => view.RemoveEnemy(enemy, battle.CoreHealth);
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
            battleInfoHud.Bind(StartBattle, SetBattleSpeed, () => hud.Notify("Settings are not available yet."));
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

    // 라운드 편성을 진영(편)별 스폰 순서로 펼친다. 같은 유닛 종류는 한 객체를 공유한다.
    private System.Func<int, IReadOnlyList<EnemyFaction>[]> BuildRoundLineup()
    {
        if (rounds == null || rounds.Length == 0)
            return null;

        var units = new Dictionary<(string, int), EnemyFaction>();
        var lineups = new IReadOnlyList<EnemyFaction>[rounds.Length][];
        for (int r = 0; r < rounds.Length; r++)
        {
            lineups[r] = new IReadOnlyList<EnemyFaction>[StageData.FactionCount];
            for (int side = 0; side < StageData.FactionCount; side++)
            {
                var queue = new List<EnemyFaction>();
                foreach (SpawnGroup group in rounds[r].Side(side))
                {
                    if (group == null || group.Count <= 0)
                        continue;
                    if (!units.TryGetValue((group.UnitId, side), out EnemyFaction unit))
                    {
                        units[(group.UnitId, side)] = unit = enemyCatalog.GetUnit(group.UnitId, Slot(side).Color, $"Round {r + 1}");
                        if (enemyCatalog.TryGetFactionGroup(group.UnitId, out EnemyFactionGroup race) && (int)race != side)
                            Debug.LogWarning($"Round {r + 1}: '{group.UnitId}' is {race} but listed under faction {side}. " +
                                $"It fights for faction {side}.", this);
                    }
                    if (unit == null)
                        continue;
                    for (int i = 0; i < group.Count; i++)
                        queue.Add(unit);
                }
                lineups[r][side] = queue;
            }
        }
        for (int side = 0; side < StageData.FactionCount; side++)
            if (!HasSpawn(side) && System.Array.Exists(lineups, lineup => lineup[side].Count > 0))
                Debug.LogWarning($"Faction {side} units are in the round lineup, but this stage has no spawn for " +
                    "them. They will not appear.", this);
        return round => lineups[(round - 1) % lineups.Length];
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
        if ((boardInputAllowed || battle.CurrentPhase == DefenseBattle.Phase.Defeat) &&
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
                    hud.Notify("Round cleared. Adjust walls before the next battle.");
                    if (leftCoreHud != null)
                        leftCoreHud.Cheer();
                }
                else if (battle.CurrentPhase == DefenseBattle.Phase.Defeat)
                {
                    hud.ShowDefeat();
                }
                break;
        }
    }

    private void SetBattleSpeed(int speed)
    {
        if (!ui.HasOpenPopup && battle.CurrentPhase != DefenseBattle.Phase.Defeat)
            battleSpeed = speed;
    }

    private void StartBattle()
    {
        if (ui.HasOpenPopup || battle.CurrentPhase != DefenseBattle.Phase.Preparation)
            return;

        buildMode = false;
        battle.StartRound();
        battleClock.Reset();
        view.ShowPlanningTiles();
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
        if (aimedSpell.HasValue)
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
                hud.Notify("That tile is occupied.");
                break;
            case DefenseGrid.BuildResult.LimitReached:
                hud.Notify("Wall limit reached. Remove a wall to free a slot.");
                break;
            case DefenseGrid.BuildResult.BlocksRoute:
                hud.Notify("Each spawn needs an open path to the core.");
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

        if (!trackingRightButton && aimedSpell is { } spell && mouse?.leftButton.wasPressedThisFrame == true &&
            TryPickAreaSpellCenter(mouse, out Vector3 center))
            CastAimedSpell(spell, center);
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

    private void Restart() => SceneManager.LoadScene(gameObject.scene.buildIndex);

    private void LateUpdate()
    {
        if (ui.HasOpenPopup || battle.CurrentPhase != DefenseBattle.Phase.Combat)
            SetAimedSpell(null);

        view.AdvanceEffects(battleClock.DeltaTime);
        view.AdvanceCoreAnimation(battleClock.DeltaTime, battleClock.PlaybackSpeed);
        Vector3? spellCenter = aimedSpell.HasValue && TryPickAreaSpellCenter(Mouse.current, out Vector3 center)
            ? center
            : null;
        int rangeStep = SpellRangeStep;
        view.ShowAreaSpellPreview(aimedSpell, spellCenter, DefenseBattle.AreaSpellRadius(rangeStep));
        hud.Refresh(battle, grid, view, aimedSpell, spellCenter, rangeStep, battleSpeed);
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
