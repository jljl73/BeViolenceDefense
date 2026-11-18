using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class VoxelDefensePrototype : MonoBehaviour
{
    private const float RightDragThreshold = 6f;

    [System.Serializable]
    private sealed class FactionSlot
    {
        [SerializeField, Tooltip("Enemy Catalog의 유닛 ID")]
        private string unitId;
        [SerializeField] private string displayName;
        [SerializeField] private Color color;
        [SerializeField, Min(0)] private int spawnCount;

        public Color Color => color;

        public EnemyFaction GetFaction(EnemyCatalog catalog) =>
            catalog.GetFaction(unitId, displayName, color, spawnCount);
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
    [SerializeField] private BoardTheme boardTheme = new();
    [SerializeField] private FactionSlot coral;
    [SerializeField] private FactionSlot teal;
    [SerializeField, Tooltip("라운드별 편성. 마지막 라운드 다음에는 처음부터 반복한다. 비어 있으면 Coral/Teal 슬롯과 기존 증원 규칙을 쓴다.")]
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
        [SerializeField, Tooltip("왼쪽 스폰. 위에서부터 순서대로 나온다.")]
        private SpawnGroup[] coral = System.Array.Empty<SpawnGroup>();
        [SerializeField, Tooltip("오른쪽 스폰. 위에서부터 순서대로 나온다.")]
        private SpawnGroup[] teal = System.Array.Empty<SpawnGroup>();

        public SpawnGroup[] Side(int index) => (index == 0 ? coral : teal) ?? System.Array.Empty<SpawnGroup>();
    }

    private DefenseGrid grid;
    private DefenseBoardView view;
    private DefenseBattle battle;
    private DefenseHud hud;
    private readonly BattleClock battleClock = new();
    private readonly List<EnemyUnit> spellRecipients = new();
    private DefenseBattle.Spell? aimedSpell;
    private float aimedRangeScroll;
    private int battleSpeed = 1;
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

        EnemyFaction coralFaction = coral.GetFaction(enemyCatalog);
        EnemyFaction tealFaction = teal.GetFaction(enemyCatalog);
        if (coralFaction == null || tealFaction == null)
        {
            enabled = false;
            return;
        }

        grid = CreateGrid(out StageData usedStage);
        var factions = new[] { coralFaction, tealFaction };
        view = new DefenseBoardView(transform, boardCamera, voxelShader, grid, factions, routeChevron, boardTheme,
            usedStage);
        battle = new DefenseBattle(grid, factions, BuildRoundLineup());
        battle.EnemySpawned += view.AddEnemy;
        battle.EnemyRemoved += view.RemoveEnemy;
        hud = new DefenseHud(ui, Restart, StartBattle, UseSpell, SetBattleSpeed, encounterIcon, hudArt, factions);
        if (leftCoreHud != null)
            hud.UseExternalCorePanel(new Vector2(320, -236));
        if (manaBarHud != null)
            hud.UseExternalManaBar((RectTransform)manaBarHud.transform, new Vector2(33, hudArt.SlotFrame != null ? -146 : -130));
        if (battleInfoHud != null)
        {
            hud.UseExternalBattleInfo();
            battleInfoHud.Bind(StartBattle, SetBattleSpeed, () => hud.Notify("Settings are not available yet."));
        }
        RefreshPlanningTiles();
    }

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

        var sideColors = new[] { coral.Color, teal.Color };
        var units = new Dictionary<(string, int), EnemyFaction>();
        var lineups = new IReadOnlyList<EnemyFaction>[rounds.Length][];
        for (int r = 0; r < rounds.Length; r++)
        {
            lineups[r] = new IReadOnlyList<EnemyFaction>[2];
            for (int side = 0; side < 2; side++)
            {
                var queue = new List<EnemyFaction>();
                foreach (SpawnGroup group in rounds[r].Side(side))
                {
                    if (group == null || group.Count <= 0)
                        continue;
                    if (!units.TryGetValue((group.UnitId, side), out EnemyFaction unit))
                        units[(group.UnitId, side)] = unit = enemyCatalog.GetUnit(group.UnitId, sideColors[side], $"Round {r + 1}");
                    if (unit == null)
                        continue;
                    for (int i = 0; i < group.Count; i++)
                        queue.Add(unit);
                }
                lineups[r][side] = queue;
            }
        }
        return round => lineups[(round - 1) % lineups.Length];
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

                ReadBuildInput();
                if (keyboard?.spaceKey.wasPressedThisFrame == true)
                    StartBattle();
                break;
            case DefenseBattle.Phase.Combat:
                float deltaTime = battleClock.DeltaTime;
                if (deltaTime <= 0f)
                    break;

                battle.RecoverMana(deltaTime);
                // Use separate ticks so 2x runs the same movement and attack checks twice.
                for (int tick = 0; tick < battleClock.StepCount && battle.CurrentPhase == DefenseBattle.Phase.Combat; tick++)
                    battle.Tick(battleClock.StepDeltaTime, pauseMovement: false);

                foreach (EnemyUnit enemy in battle.Enemies)
                    view.SyncEnemy(enemy, deltaTime, battleClock.PlaybackSpeed);
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

        battle.StartRound();
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

    private void ReadBuildInput()
    {
        Mouse mouse = Mouse.current;
        if (trackingRightButton || mouse == null || (!mouse.leftButton.wasPressedThisFrame && !rightClickPointer.HasValue))
            return;

        Vector2 pointer = rightClickPointer ?? mouse.position.ReadValue();
        if (ui.BlocksBoardPointer(pointer) || !view.TryPickCell(pointer, out Vector2Int cell))
        {
            return;
        }

        if (rightClickPointer.HasValue)
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

        hud.Notify(battle.CastAreaSpell(spell, center, rangeStep, spellRecipients));
        view.PlayAreaSpell(spell, center, DefenseBattle.AreaSpellRadius(rangeStep), spellRecipients);
        spellRecipients.Clear();
        SetAimedSpell(null);
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
        if (battleInfoHud != null)
        {
            int left = 0, right = 0;
            foreach (EnemyUnit enemy in battle.Enemies)
            {
                if (!enemy.IsAlive)
                    continue;
                if (enemy.FactionIndex == 0)
                    left++;
                else
                    right++;
            }
            bool inCombat = battle.CurrentPhase == DefenseBattle.Phase.Combat;
            bool canCallWave = !ui.HasOpenPopup && battle.CurrentPhase == DefenseBattle.Phase.Preparation;
            battleInfoHud.Refresh(battle.Round, inCombat, canCallWave, left, right, coral.Color, teal.Color, battleSpeed);
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
