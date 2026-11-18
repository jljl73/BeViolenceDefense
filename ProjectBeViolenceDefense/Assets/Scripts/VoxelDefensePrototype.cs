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
    [SerializeField] private DefenseHud.Art hudArt = new();
    [SerializeField] private EnemyFaction coral = new("Coral", new Color32(255, 115, 87, 255), 0.9f);
    [SerializeField] private EnemyFaction teal = new("Teal", new Color32(69, 222, 206, 255), 0.9f);

    private DefenseGrid grid;
    private DefenseBoardView view;
    private DefenseBattle battle;
    private DefenseHud hud;
    private EnemyUnit selected;
    private DefenseBattle.Spell? aimedSpell;
    private int battleSpeed = 1;

    private void Awake()
    {
        grid = new DefenseGrid();
        var factions = new[] { coral, teal };
        view = new DefenseBoardView(transform, boardCamera, voxelShader, grid, factions, routeChevron);
        battle = new DefenseBattle(grid, factions);
        battle.EnemySpawned += view.AddEnemy;
        battle.EnemyRemoved += RemoveEnemy;
        hud = new DefenseHud(ui, Restart, StartBattle, UseSpell, SetBattleSpeed, encounterIcon, hudArt);
        RefreshPlanningTiles();
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
                battle.RecoverMana(Time.deltaTime * battleSpeed);
                if (boardInputAllowed)
                    ReadCombatInput(keyboard);

                bool pauseMovement = boardInputAllowed && aimedSpell.HasValue;
                // Use separate ticks so 2x runs the same movement and attack checks twice.
                for (int tick = 0; tick < battleSpeed && battle.CurrentPhase == DefenseBattle.Phase.Combat; tick++)
                    battle.Tick(Time.deltaTime, pauseMovement);

                foreach (EnemyUnit enemy in battle.Enemies)
                    view.SyncEnemy(enemy);
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
            aimedSpell = null;
            hud.Notify("");
            return;
        }

        if (keyboard?.digit1Key.wasPressedThisFrame == true)
        {
            UseSpell(DefenseBattle.Spell.AttackBuff);
            return;
        }
        if (keyboard?.digit3Key.wasPressedThisFrame == true)
        {
            UseSpell(DefenseBattle.Spell.MoveSpeedBuff);
            return;
        }

        if (aimedSpell is { } spell)
        {
            if (keyboard?.digit2Key.wasPressedThisFrame == true)
                UseSpell(DefenseBattle.Spell.Heal);
            else if (mouse?.leftButton.wasPressedThisFrame == true && TryPickAreaBuffCenter(mouse, out Vector3 center))
            {
                aimedSpell = null;
                hud.Notify(battle.CastAreaBuff(spell, center));
            }
            return;
        }

        if (mouse?.leftButton.wasPressedThisFrame == true && !ui.BlocksBoardPointer(mouse.position.ReadValue()))
        {
            selected = null;
            Vector2 pointer = mouse.position.ReadValue();
            float radius = Mathf.Clamp(Screen.height * 0.027f, 12, 32);
            float nearest = radius * radius;
            foreach (EnemyUnit enemy in battle.Enemies)
            {
                Vector3 screen = view.ScreenPoint(enemy, 0.48f);
                float distance = ((Vector2)screen - pointer).sqrMagnitude;
                if (screen.z > 0 && distance < nearest)
                {
                    selected = enemy;
                    nearest = distance;
                }
            }
        }

        if (keyboard?.digit2Key.wasPressedThisFrame == true)
            UseSpell(DefenseBattle.Spell.Heal);
    }

    private bool TryPickAreaBuffCenter(Mouse mouse, out Vector3 center)
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

        if (spell == DefenseBattle.Spell.Heal)
        {
            aimedSpell = null;
            hud.Notify(battle.CastHeal(selected));
            return;
        }

        string blocked = battle.SpellBlockReason(null, spell);
        if (blocked == null)
            aimedSpell = spell;
        hud.Notify(blocked ?? "");
    }

    private void RemoveEnemy(EnemyUnit enemy)
    {
        if (selected == enemy)
            selected = null;
        view.RemoveEnemy(enemy);
    }

    private void Restart() => SceneManager.LoadScene(gameObject.scene.buildIndex);

    private void LateUpdate()
    {
        if (ui.HasOpenPopup || battle.CurrentPhase != DefenseBattle.Phase.Combat)
            aimedSpell = null;

        view.ShowAreaBuffPreview(aimedSpell, aimedSpell.HasValue && TryPickAreaBuffCenter(Mouse.current, out Vector3 center)
            ? center
            : null);
        hud.Refresh(battle, grid, view, selected, aimedSpell, battleSpeed);
    }

    private void OnDestroy() => view?.ReleaseMaterials();
}
