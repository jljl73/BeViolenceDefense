using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class VoxelDefensePrototype : MonoBehaviour
{
    [SerializeField] private Camera boardCamera;
    [SerializeField] private Shader voxelShader;
    [SerializeField] private UIManager ui;
    [SerializeField] private Texture2D encounterIcon;
    [SerializeField] private Sprite routeChevron;
    [SerializeField] private GameObject goblinPrefab;
    [SerializeField] private DefenseHud.Art hudArt = new();
    // 1스테이지 임시 밸런스: 고블린 6 vs 좀비 4가 비슷한 전력으로 맞부딪치도록 맞춘 값 (docs/prototype.md 참고).
    [SerializeField] private EnemyFaction coral = new("Coral", new Color32(255, 115, 87, 255), 1.5f, 50, 10, 0.65f, 6);
    [SerializeField] private EnemyFaction teal = new("Teal", new Color32(69, 222, 206, 255), 1.5f, 90, 15, 1f, 4);

    private DefenseGrid grid;
    private DefenseBoardView view;
    private DefenseBattle battle;
    private DefenseHud hud;
    private readonly BattleClock battleClock = new();
    private DefenseBattle.Spell? aimedSpell;
    private int battleSpeed = 1;

    private void Awake()
    {
        grid = new DefenseGrid();
        var factions = new[] { coral, teal };
        view = new DefenseBoardView(transform, boardCamera, voxelShader, grid, factions, routeChevron, goblinPrefab);
        battle = new DefenseBattle(grid, factions);
        battle.EnemySpawned += view.AddEnemy;
        battle.EnemyRemoved += view.RemoveEnemy;
        hud = new DefenseHud(ui, Restart, StartBattle, UseSpell, SetBattleSpeed, encounterIcon, hudArt);
        RefreshPlanningTiles();
    }

    private void Update()
    {
        battleClock.BeginFrame(Time.deltaTime, battleSpeed);
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
        }

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
                if (boardInputAllowed)
                    ReadCombatInput(keyboard);

                float deltaTime = battleClock.DeltaTime;
                if (deltaTime <= 0f)
                    break;

                battle.RecoverMana(deltaTime);
                // Use separate ticks so 2x runs the same movement and attack checks twice.
                for (int tick = 0; tick < battleClock.StepCount && battle.CurrentPhase == DefenseBattle.Phase.Combat; tick++)
                    battle.Tick(battleClock.StepDeltaTime, pauseMovement: false);

                foreach (EnemyUnit enemy in battle.Enemies)
                    view.SyncEnemy(enemy, deltaTime, battleClock.StepCount);
                if (battle.CurrentPhase == DefenseBattle.Phase.Preparation)
                {
                    RefreshPlanningTiles();
                    hud.Notify("Round cleared. Adjust walls before the next battle.");
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
        if (keyboard != null)
        {
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            if (input != Vector2.zero)
                view.PanCamera(input, Time.unscaledDeltaTime);
        }

        Mouse mouse = Mouse.current;
        float scrollDelta = mouse?.scroll.ReadValue().y ?? 0f;
        if (scrollDelta != 0f)
            view.QueueZoom(scrollDelta, mouse.position.ReadValue());
    }

    private void ReadBuildInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || (!mouse.leftButton.wasPressedThisFrame && !mouse.rightButton.wasPressedThisFrame) ||
            ui.BlocksBoardPointer(mouse.position.ReadValue()) ||
            !view.TryPickCell(mouse.position.ReadValue(), out Vector2Int cell))
        {
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
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
            (keyboard?.escapeKey.wasPressedThisFrame == true || mouse?.rightButton.wasPressedThisFrame == true))
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

        if (aimedSpell is { } spell && mouse?.leftButton.wasPressedThisFrame == true &&
            TryPickAreaSpellCenter(mouse, out Vector3 center))
        {
            hud.Notify(battle.CastAreaSpell(spell, center));
            SetAimedSpell(null);
        }
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

        string blocked = battle.SpellBlockReason(spell);
        if (blocked == null)
            SetAimedSpell(spell);
        hud.Notify(blocked ?? "");
    }

    private void SetAimedSpell(DefenseBattle.Spell? spell)
    {
        if (spell.HasValue != aimedSpell.HasValue)
        {
            battleClock.SetPaused(spell.HasValue);
            if (spell.HasValue)
                view.PauseEnemyAnimations();
        }

        aimedSpell = spell;
    }

    private void Restart() => SceneManager.LoadScene(gameObject.scene.buildIndex);

    private void LateUpdate()
    {
        if (ui.HasOpenPopup || battle.CurrentPhase != DefenseBattle.Phase.Combat)
            SetAimedSpell(null);

        view.AdvanceHitEffects(battleClock.DeltaTime);
        Vector3? spellCenter = aimedSpell.HasValue && TryPickAreaSpellCenter(Mouse.current, out Vector3 center)
            ? center
            : null;
        view.ShowAreaSpellPreview(aimedSpell, spellCenter);
        hud.Refresh(battle, grid, view, aimedSpell, spellCenter, battleSpeed);
    }

    private void OnDisable() => SetAimedSpell(null);

    private void OnDestroy() => view?.ReleaseMaterials();
}
