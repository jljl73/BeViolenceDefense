using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

internal sealed class DefenseHud
{
    [Serializable]
    internal sealed class Art
    {
        public Sprite Panel;
        public Sprite Button;
        public Sprite Start;
        public Sprite Restart;
        public Sprite AttackBuff;
        public Sprite Heal;
        public Sprite MoveSpeedBuff;
        public Sprite Round;
        public Sprite Core;
        public Sprite Wall;
        public Sprite Enemy;
        public Sprite Mana;
        public Sprite Lock;
        [Tooltip("선택. 비우면 방어막 슬롯은 Wall 아이콘을 쓴다.")]
        public Sprite ArmorBuff;
        [Tooltip("선택. 비우면 도발 슬롯은 Enemy 아이콘에 도발 색을 입혀 쓴다.")]
        public Sprite Taunt;
        [Tooltip("선택. 넣으면 스킬 슬롯 뒤에 프레임을 깔고, 조준 중인 스킬은 선택 프레임으로 바꾼다.")]
        public Sprite SlotFrame;
        public Sprite SlotFrameSelected;
        [Tooltip("스킬 아이콘이 이미 채색된 그림이면 켠다. 켜면 아이콘에 의미 색을 곱하지 않는다.")]
        public bool ColoredSkillIcons;
    }

    private static readonly Vector2 SpellSlotSize = new(110, 124);
    // 슬롯 5개를 행동 바(600px) 가운데에 놓는다. 프레임(128px)이 겹치지 않는 간격.
    private const float SpellSlotSpacing = 124f;
    private const float FirstSpellSlotX = -3f;
    private static readonly Vector2 ActionsSize = new(600, 184);
    private const float HealthBarThickness = 5f;

    private readonly UIManager ui;
    private readonly Art art;
    private readonly RectTransform markers;
    private readonly RectTransform defeatPopup;
    private readonly TextMeshProUGUI round;
    private readonly TextMeshProUGUI phase;
    private readonly TextMeshProUGUI lineup;
    private int lineupRound = -1;
    private readonly CombatBalance combatBalance;
    private readonly RectTransform coreStatus;
    private readonly RectTransform manaGauge;
    private readonly RectTransform roundStatus;
    private bool externalBattleInfo;
    private bool externalBuildPanel;
    private readonly RectTransform noticeArea;
    private readonly TextMeshProUGUI core;
    private readonly Image coreFill;
    private readonly RectTransform wallSupply;
    private readonly Image[] wallIcons = new Image[DefenseGrid.WallLimit];
    private readonly TextMeshProUGUI wallCount;
    private readonly TextMeshProUGUI notice;
    private readonly RectTransform actionBar;
    private readonly RectTransform preparePanel;
    private readonly RectTransform spellBar;
    private readonly TextMeshProUGUI mana;
    private readonly Image manaFill;
    private readonly HudButton startBattle;
    private readonly HudButton restartButton;
    private readonly RectTransform speedPanel;
    private readonly HudButton normalSpeed;
    private readonly HudButton doubleSpeed;
    private readonly SpellSlot attackBuff;
    private readonly SpellSlot heal;
    private readonly SpellSlot moveSpeedBuff;
    private readonly SpellSlot armorBuff;
    private readonly SpellSlot taunt;
    private readonly TextMeshProUGUI actionTip;
    private readonly HudButton help;
    private readonly TextMeshProUGUI helpTip;
    private readonly List<Image> healthMarkers = new();
    private readonly Texture2D encounterIcon;
    private readonly List<RawImage> encounterMarkers = new();
    private int visibleMarkers;
    private bool helpPinned;
    private string noticeText = "";
    private float noticeUntil;

    public DefenseHud(UIManager ui, UnityAction restart, UnityAction start, Action<DefenseBattle.Spell> cast,
        Action<int> setBattleSpeed, Texture2D encounterIcon, Art art, IReadOnlyList<EnemyFaction> factions)
    {
        this.ui = ui;
        this.encounterIcon = encounterIcon;
        this.art = art ?? new Art();
        markers = Rect("Board Markers", ui.HudLayer, Vector2.zero, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Stretch(markers);

        Vector2 topCenter = new(0.5f, 1f);
        RectTransform status = roundStatus = Rect("Round Status", ui.HudLayer, topCenter, topCenter,
            new Vector2(0, -22), new Vector2(220, 75));
        round = Label("Round", status, 0, 0, 220, 40, 30, TextAlignmentOptions.Center);
        phase = Label("Phase", status, 0, -41, 220, 28, 20, TextAlignmentOptions.Center);
        phase.color = UIPalette.TextSecondary;
        lineup = Label("Round Lineup", status, -190, -70, 600, 26, 18, TextAlignmentOptions.Center);
        combatBalance = new CombatBalance(status, factions);

        coreStatus = Rect("Core Status", ui.HudLayer, Vector2.up, Vector2.up,
            new Vector2(55, -35), new Vector2(204, 72));
        AddIcon("Core Gem", coreStatus, this.art.Core, new Vector2(0, -3), 46, UIPalette.ButtonPrimary);
        core = Label("Core", coreStatus, 62, 0, 142, 42, 31);
        coreFill = Bar("Core Health", coreStatus, Vector2.up, Vector2.up,
            new Vector2(62, -48), new Vector2(120, 5), UIPalette.ButtonPrimary);

        wallSupply = Rect("Wall Supply", ui.HudLayer, Vector2.one, Vector2.one,
            new Vector2(-55, -33), new Vector2(150, 84));
        for (int i = 0; i < wallIcons.Length; i++)
            wallIcons[i] = AddIcon($"Wall {i + 1}", wallSupply, this.art.Wall,
                new Vector2(64 + (i % 4) * 21, -(i / 4) * 16), 16, UIPalette.TextPrimary);
        wallCount = Label("Walls Left", wallSupply, 0, -35, 150, 28, 20, TextAlignmentOptions.Right);

        noticeArea = Rect("Notice", ui.HudLayer, Vector2.up, Vector2.up,
            new Vector2(55, -110), new Vector2(500, 50));
        notice = Label("Message", noticeArea, 0, 0, 500, 50, 20);
        noticeArea.gameObject.SetActive(false);

        speedPanel = Rect("Battle Speed", ui.HudLayer, Vector2.one, Vector2.one,
            new Vector2(-50, -32), new Vector2(132, 60));
        normalSpeed = ActionButton("1x", speedPanel, Vector2.zero, new Vector2(56, 60),
            () => setBattleSpeed(1), caption: "1×", flat: true);
        doubleSpeed = ActionButton("2x", speedPanel, new Vector2(70, 0), new Vector2(56, 60),
            () => setBattleSpeed(2), caption: "2×", flat: true);
        AddSpeedUnderline(normalSpeed);
        AddSpeedUnderline(doubleSpeed);

        RectTransform helpDock = Rect("Help Dock", ui.HudLayer, Vector2.zero, Vector2.zero,
            new Vector2(46, 38), new Vector2(50, 50));
        help = ActionButton("Controls", helpDock, Vector2.zero, helpDock.sizeDelta,
            () => helpPinned = !helpPinned, caption: "?", flat: true);
        helpTip = Tip("Controls Tip", helpDock, Vector2.up, Vector2.zero, new Vector2(0, 8), new Vector2(420, 82),
            TextAlignmentOptions.Left);

        Vector2 bottomCenter = new(0.5f, 0f);
        actionBar = Rect("Actions", ui.HudLayer, bottomCenter, bottomCenter, new Vector2(0, 46), ActionsSize);
        preparePanel = Rect("Prepare", actionBar, Vector2.up, Vector2.up, Vector2.zero, ActionsSize);
        startBattle = ActionButton("Start Battle", preparePanel, new Vector2(222, -44), new Vector2(156, 140),
            start, this.art.Start, GameText.Get("key.space"), 43, flat: true);
        RectTransform startRect = startBattle.Button.image.rectTransform;
        startBattle.Icon.rectTransform.anchoredPosition = new Vector2(56.5f, -19);
        startBattle.Caption.rectTransform.anchoredPosition = new Vector2(0, -103);
        startBattle.Caption.fontSize = 19;
        TextMeshProUGUI startLabel = Label("Start Label", startRect, 0, -76, 156, 29, 23, TextAlignmentOptions.Center);
        startLabel.text = GameText.Get("ui.start");
        startLabel.color = UIPalette.ButtonPrimary;
        AddFocusRing(startBattle, new Vector2(43, -6), 70);

        spellBar = Rect("Spell Bar", actionBar, Vector2.up, Vector2.up, Vector2.zero, ActionsSize);
        attackBuff = CreateSpellSlot("Attack Buff", 0, "1", this.art.AttackBuff,
            DefenseBattle.Spell.AttackBuff, UIPalette.Attack, cast);
        heal = CreateSpellSlot("Heal", 1, "2", this.art.Heal,
            DefenseBattle.Spell.Heal, UIPalette.Heal, cast);
        moveSpeedBuff = CreateSpellSlot("Move Speed Buff", 2, "3", this.art.MoveSpeedBuff,
            DefenseBattle.Spell.MoveSpeedBuff, UIPalette.MoveSpeed, cast);
        armorBuff = CreateSpellSlot("Armor Buff", 3, "4", this.art.ArmorBuff != null ? this.art.ArmorBuff : this.art.Wall,
            DefenseBattle.Spell.ArmorBuff, UIPalette.Armor, cast);
        taunt = CreateSpellSlot("Taunt", 4, "5", this.art.Taunt != null ? this.art.Taunt : this.art.Enemy,
            DefenseBattle.Spell.Taunt, UIPalette.Taunt, cast, tintIcon: this.art.Taunt == null);

        manaGauge = Rect("Mana Gauge", spellBar, Vector2.up, Vector2.up,
            new Vector2(82, -138), new Vector2(436, 29));
        AddIcon("Mana Drop", manaGauge, this.art.Mana, new Vector2(0, -4), 20, UIPalette.Mana);
        manaFill = Bar("Track", manaGauge, Vector2.up, Vector2.up,
            new Vector2(28, -12), new Vector2(294, 5), UIPalette.Mana);
        mana = Label("Mana", manaGauge, 333, 0, 103, 28, 23);
        mana.color = UIPalette.Mana;

        actionTip = Tip("Action Tip", actionBar, topCenter, bottomCenter,
            new Vector2(0, 5), new Vector2(600, 70), TextAlignmentOptions.Bottom);

        Vector2 center = Vector2.one * 0.5f;
        defeatPopup = Panel("Defeat", ui.PopupLayer, center, center, Vector2.zero,
            new Vector2(290, 196), UIPalette.PanelRaised);
        defeatPopup.gameObject.SetActive(false);
        AddIcon("Broken Core", defeatPopup, this.art.Core, new Vector2(120, -13), 50, UIPalette.ButtonDanger);
        TextMeshProUGUI title = Label("Title", defeatPopup, 20, -60, 250, 38, 28, TextAlignmentOptions.Center);
        title.text = GameText.Get("result.core_destroyed");
        restartButton = ActionButton("Restart", defeatPopup, new Vector2(89, -105), new Vector2(112, 72),
            restart, this.art.Restart, "R", 39);
    }

    /// <summary>
    /// 별도 좌상단 코어 HUD(LeftCoreHud)를 쓰는 씬에서 호출한다. 기존 코어 패널을 숨기고,
    /// 알림이 새 패널과 겹치지 않도록 알림 위치를 오른쪽으로 옮긴다.
    /// </summary>
    public void UseExternalCorePanel(Vector2 noticePosition)
    {
        coreStatus.gameObject.SetActive(false);
        noticeArea.anchoredPosition = noticePosition;
    }

    /// <summary>
    /// 별도 마나 바(ManaBarHud)를 쓰는 씬에서 호출한다. 기존 마나 게이지를 숨기고 새 바를
    /// 전투 스킬 바의 같은 자리에 붙여, 스킬 바와 함께 보이고 숨겨지게 한다.
    /// </summary>
    public void UseExternalManaBar(RectTransform bar, Vector2 position)
    {
        manaGauge.gameObject.SetActive(false);
        bar.SetParent(spellBar, false);
        bar.anchorMin = bar.anchorMax = Vector2.up;
        bar.pivot = Vector2.up;
        bar.anchoredPosition = position;
    }

    /// <summary>
    /// 별도 전투 정보 HUD(BattleInfoHud)를 쓰는 씬에서 호출한다. 상단 중앙 라운드 표시, 우상단 배속 버튼,
    /// 하단 시작 버튼을 숨긴다(같은 기능을 새 HUD가 맡는다).
    /// </summary>
    public void UseExternalBattleInfo()
    {
        externalBattleInfo = true;
        roundStatus.gameObject.SetActive(false);
        // 벽 개수 표시는 새 우상단 패널과 겹치지 않게 그 아래로 내린다.
        wallSupply.anchoredPosition = new Vector2(wallSupply.anchoredPosition.x, -132);
    }

    /// <summary>건설 모드에서 자리를 내주는 하단 행동 UI(시작 버튼·스킬 바).</summary>
    public RectTransform ActionBar => actionBar;

    /// <summary>별도 건설 패널(BuildModeHud)을 쓰는 씬에서 호출한다. 우상단의 벽 개수 표시를 숨긴다(패널이 대신 보여 준다).</summary>
    public void UseExternalBuildPanel() => externalBuildPanel = true;

    public void Notify(string message)
    {
        noticeText = message;
        noticeUntil = Time.unscaledTime + 2.5f;
    }

    // 좌우 세력 바에 보여 줄 두 진영: 이번 라운드에 나오는 진영 중 앞의 둘.
    // shortcut: 한 라운드에 3종족 이상이 나오면 셋째부터 바에서 빠진다. 그런 편성이 생기면 N분할 바로 바꾼다.
    internal static (int Left, int Right) ShownSides(DefenseBattle battle)
    {
        int left = -1, right = -1;
        for (int side = 0; side < battle.FactionCount && right < 0; side++)
        {
            if (battle.RoundSpawnCount(side) == 0)
                continue;
            if (left < 0)
                left = side;
            else
                right = side;
        }
        if (left < 0)
            return (0, 1);
        return right < 0 ? (left, left == 0 ? 1 : 0) : (left, right);
    }

    // 예: "Goblin Grunt ×6   vs   Zombie ×4". 연속한 같은 유닛은 묶어서 보여준다. 나오지 않는 진영은 뺀다.
    private static string LineupText(DefenseBattle battle)
    {
        var sides = new List<string>();
        for (int side = 0; side < battle.FactionCount; side++)
        {
            if (battle.RoundSpawnCount(side) == 0)
                continue;
            var parts = new List<string>();
            string last = null;
            int count = 0;
            for (int i = 0; i < battle.RoundSpawnCount(side); i++)
            {
                string name = battle.RoundUnit(side, i).DisplayName;
                if (name == last)
                {
                    count++;
                    continue;
                }
                if (last != null)
                    parts.Add($"{last} ×{count}");
                last = name;
                count = 1;
            }
            if (last != null)
                parts.Add($"{last} ×{count}");
            string color = ColorUtility.ToHtmlStringRGB(battle.RoundUnit(side, 0).Color);
            sides.Add($"<color=#{color}>{string.Join(", ", parts)}</color>");
        }
        return sides.Count > 0 ? string.Join(GameText.Get("hud.versus"), sides) : "-";
    }

    public void Refresh(DefenseBattle battle, DefenseGrid grid, DefenseBoardView view,
        DefenseBattle.Spell? aimedSpell, Vector3? spellCenter, int rangeStep, int battleSpeed,
        EnemyUnit tauntTarget = null)
    {
        bool inCombat = battle.CurrentPhase == DefenseBattle.Phase.Combat;
        // 승리도 패배처럼 판이 끝난 상태로 그린다.
        bool won = battle.CurrentPhase == DefenseBattle.Phase.Victory;
        bool defeated = won || battle.CurrentPhase == DefenseBattle.Phase.Defeat;
        round.text = GameText.Format("hud.wave", battle.Round);
        phase.text = won ? GameText.Get("hud.victory") : defeated ? GameText.Get("hud.defeat") : aimedSpell.HasValue ? GameText.Format("hud.aim_speed", BattleClock.AimSpeed) : inCombat ? GameText.Get("hud.combat") : GameText.Get("hud.preparation");
        phase.color = aimedSpell.HasValue ? UIPalette.TextPrimary : UIPalette.TextSecondary;
        combatBalance.Refresh(battle.Enemies, inCombat, ShownSides(battle));
        lineup.gameObject.SetActive(!inCombat && !defeated);
        if (!inCombat && !defeated && lineupRound != battle.Round)
        {
            lineupRound = battle.Round;
            lineup.text = LineupText(battle);
        }
        int displayedCoreHealth = battle.CoreHealth + view.PendingCoreDamage;
        core.text = $"{displayedCoreHealth} <size=65%>/ {DefenseBattle.CoreMaxHealth}</size>";
        coreFill.color = displayedCoreHealth <= DefenseBattle.CoreMaxHealth / 4
            ? UIPalette.ButtonDanger : UIPalette.ButtonPrimary;
        SetBarFraction(coreFill, (float)displayedCoreHealth / DefenseBattle.CoreMaxHealth);
        wallSupply.gameObject.SetActive(!inCombat && !defeated && !externalBuildPanel);
        if (!inCombat && !defeated)
        {
            int wallsLeft = DefenseGrid.WallLimit - grid.WallCount;
            wallCount.text = GameText.Format("hud.walls", wallsLeft, DefenseGrid.WallLimit);
            for (int i = 0; i < wallIcons.Length; i++)
                wallIcons[i].color = i < wallsLeft ? UIPalette.TextPrimary : UIPalette.TextDisabled;
        }
        bool showNotice = !defeated && Time.unscaledTime < noticeUntil && noticeText.Length > 0;
        notice.transform.parent.gameObject.SetActive(showNotice);
        if (showNotice)
            notice.text = noticeText;

        bool blocked = ui.HasOpenPopup;
        RefreshActions(battle, aimedSpell, spellCenter, rangeStep, inCombat, defeated, blocked, tauntTarget);
        speedPanel.gameObject.SetActive(inCombat && !externalBattleInfo);
        RefreshSpeedButton(normalSpeed, battleSpeed == 1, blocked);
        RefreshSpeedButton(doubleSpeed, battleSpeed == 2, blocked);

        help.Button.gameObject.SetActive(!defeated);
        help.Button.interactable = !blocked;
        StyleFlatButton(help, help.ShowTip ? UIPalette.TextPrimary : UIPalette.TextSecondary);
        SetTip(helpTip, !blocked && !defeated && (help.ShowTip || helpPinned)
            ? GameText.Format("help.layout", GameText.Get(inCombat ? "help.combat" : "help.build"),
                GameText.Get(aimedSpell.HasValue ? "help.radius" : "help.zoom"))
            : null);
        StyleButton(restartButton, UIPalette.ButtonPrimary, UIPalette.TextOnAccent);

        RefreshEncounterMarkers(battle, view);

        visibleMarkers = 0;
        foreach (EnemyUnit enemy in battle.Enemies)
        {
            if (ShowsUnitHealth(enemy))
                AddHealthMarker(view.ScreenPoint(enemy, 0.76f), enemy);
        }
        for (int i = visibleMarkers; i < healthMarkers.Count; i++)
            healthMarkers[i].transform.parent.gameObject.SetActive(false);
    }

    public void ShowDefeat() => ui.ShowPopup(defeatPopup, dismissOnEscape: false);

    private sealed class CombatBalance
    {
        private readonly RectTransform root;
        private readonly Image leftFill;
        private readonly Image rightFill;
        private readonly Image divider;
        private readonly TextMeshProUGUI leftCount;
        private readonly TextMeshProUGUI rightCount;
        private readonly IReadOnlyList<EnemyFaction> factions;
        private (int Left, int Right) shown = (-1, -1);
        private string leftFormat;
        private string rightFormat;

        public CombatBalance(RectTransform parent, IReadOnlyList<EnemyFaction> factions)
        {
            this.factions = factions;
            Vector2 topCenter = new(0.5f, 1f);
            root = Rect("Combat Balance", parent, topCenter, topCenter, new Vector2(0, -70), new Vector2(500, 30));
            leftCount = Label("Left Alive", root, 0, -4, 112, 25, 19, TextAlignmentOptions.Right);
            rightCount = Label("Right Alive", root, 388, -4, 112, 25, 19);
            TextMeshProUGUI caption = Label("Metric", root, 122, 0, 256, 17, 15, TextAlignmentOptions.Center);
            caption.text = GameText.Get("hud.field_health");
            caption.color = UIPalette.TextSecondary;

            leftFill = Bar("Field Health", root, Vector2.up, Vector2.up, new Vector2(122, -18),
                new Vector2(256, 8), factions[0].Color);
            var track = (RectTransform)leftFill.transform.parent;
            rightFill = Graphic("Right Fill", track, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero, factions[1].Color);
            divider = Graphic("Divider", track, Vector2.zero, new Vector2(0.5f, 0),
                new Vector2(0, -2), new Vector2(3, 12), UIPalette.TextPrimary);
            root.gameObject.SetActive(false);
        }

        public void Refresh(IReadOnlyList<EnemyUnit> enemies, bool inCombat, (int Left, int Right) sides)
        {
            root.gameObject.SetActive(inCombat);
            if (!inCombat)
                return;

            if (sides != shown)
            {
                shown = sides;
                leftFormat = factions[sides.Left].DisplayName + "  {0}";
                rightFormat = "{0}  " + factions[sides.Right].DisplayName;
                leftFill.color = factions[sides.Left].Color;
                rightFill.color = factions[sides.Right].Color;
            }

            int leftAlive = 0;
            int rightAlive = 0;
            float leftHealth = 0f;
            float rightHealth = 0f;
            foreach (EnemyUnit enemy in enemies)
            {
                if (!enemy.IsAlive)
                    continue;

                if (enemy.FactionIndex == sides.Left)
                {
                    leftAlive++;
                    leftHealth += enemy.Health;
                }
                else if (enemy.FactionIndex == sides.Right)
                {
                    rightAlive++;
                    rightHealth += enemy.Health;
                }
            }

            leftCount.SetText(leftFormat, leftAlive);
            rightCount.SetText(rightFormat, rightAlive);
            float totalHealth = leftHealth + rightHealth;
            float split = totalHealth > 0f ? leftHealth / totalHealth : 0f;
            SetBarFraction(leftFill, split);
            rightFill.rectTransform.anchorMin = new Vector2(totalHealth > 0f ? split : 1f, 0f);
            divider.enabled = totalHealth > 0f;
            divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = new Vector2(split, 0f);
        }
    }

    private static void RefreshSpeedButton(HudButton control, bool selected, bool blocked)
    {
        control.Button.interactable = !blocked;
        StyleFlatButton(control, selected || control.Focused ? UIPalette.OutlineFocus : UIPalette.TextSecondary, selected);
    }

    private static void AddSpeedUnderline(HudButton control)
    {
        control.Decoration = Graphic("Selected Speed", control.Button.image.rectTransform, Vector2.up, Vector2.up,
            new Vector2(3, -56), new Vector2(50, 3), UIPalette.OutlineFocus);
    }

    private static void AddFocusRing(HudButton control, Vector2 position, int diameter)
    {
        RectTransform rect = Rect("Focus Ring", control.Button.image.rectTransform, Vector2.up, Vector2.up,
            position, new Vector2(diameter, diameter));
        var ring = rect.gameObject.AddComponent<HudRing>();
        ring.color = UIPalette.OutlineFocus;
        ring.raycastTarget = false;
        control.Decoration = ring;
    }

    private void RefreshEncounterMarkers(DefenseBattle battle, DefenseBoardView view)
    {
        int visible = 0;
        // 준비 단계의 예상 교전 칸, 그리고 웨이브 시작 직후 잠깐 남겨 두는 교전 예고 칸에 아이콘을 띄운다.
        if (battle.CurrentPhase != DefenseBattle.Phase.Defeat && encounterIcon != null)
        {
            foreach (Vector2Int cell in view.PlannedEncounters)
            {
                Vector3 screen = view.ScreenPoint(DefenseGrid.CellPosition(cell) + Vector3.up * 0.6f);
                if (screen.z <= 0 ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(markers, screen, null, out Vector2 position))
                {
                    continue;
                }

                if (visible == encounterMarkers.Count)
                {
                    Vector2 center = Vector2.one * 0.5f;
                    var image = Rect("Predicted Encounter", markers, center, center, Vector2.zero, new Vector2(36, 36))
                        .gameObject.AddComponent<RawImage>();
                    image.texture = encounterIcon;
                    image.raycastTarget = false;
                    encounterMarkers.Add(image);
                }

                RawImage marker = encounterMarkers[visible++];
                marker.rectTransform.anchoredPosition = position;
                marker.gameObject.SetActive(true);
            }
        }

        for (int i = visible; i < encounterMarkers.Count; i++)
            encounterMarkers[i].gameObject.SetActive(false);
    }

    private void RefreshActions(DefenseBattle battle, DefenseBattle.Spell? aimedSpell, Vector3? spellCenter,
        int rangeStep, bool inCombat, bool defeated, bool blocked, EnemyUnit tauntTarget)
    {
        actionBar.gameObject.SetActive(!defeated);
        preparePanel.gameObject.SetActive(!inCombat && !externalBattleInfo);
        startBattle.Button.interactable = !blocked;
        StyleFlatButton(startBattle, UIPalette.ButtonPrimary, marked: true, shortcut: UIPalette.TextSecondary);
        spellBar.gameObject.SetActive(inCombat);

        if (!inCombat)
        {
            SetTip(actionTip, null);
            return;
        }

        mana.text = $"{Mathf.FloorToInt(battle.Mana)} <size=80%>/ {DefenseBattle.MaxMana}</size>";
        SetBarFraction(manaFill, battle.Mana / DefenseBattle.MaxMana);
        bool spellHovered = attackBuff.Control.Hovered || heal.Control.Hovered ||
            moveSpeedBuff.Control.Hovered || armorBuff.Control.Hovered || taunt.Control.Hovered;
        string attackTip = RefreshSpell(attackBuff, battle, blocked, aimedSpell, rangeStep, spellHovered);
        string healTip = RefreshSpell(heal, battle, blocked, aimedSpell, rangeStep, spellHovered);
        string speedTip = RefreshSpell(moveSpeedBuff, battle, blocked, aimedSpell, rangeStep, spellHovered);
        string armorTip = RefreshSpell(armorBuff, battle, blocked, aimedSpell, rangeStep, spellHovered);
        string tauntTip = RefreshSpell(taunt, battle, blocked, aimedSpell, rangeStep, spellHovered);
        if (!blocked && aimedSpell == DefenseBattle.Spell.Taunt)
        {
            string tint = ColorUtility.ToHtmlStringRGB(taunt.Tint);
            int pulled = 0;
            if (tauntTarget != null)
                foreach (EnemyUnit enemy in battle.Enemies)
                    if (DefenseBattle.IsTauntable(tauntTarget, enemy))
                        pulled++;
            string reason = battle.SpellBlockReason(DefenseBattle.Spell.Taunt, 0);
            string controls = reason ?? (tauntTarget != null ? GameText.Get("aim.taunt_ready") : GameText.Get("aim.taunt_pick"));
            SetTip(actionTip, GameText.Format("aim.taunt", tint, taunt.Name, DefenseBattle.TauntRadius,
                DefenseBattle.ManaCost(DefenseBattle.Spell.Taunt), pulled, controls));
        }
        else if (!blocked && aimedSpell is { } spell)
        {
            SpellSlot slot = spell switch
            {
                DefenseBattle.Spell.AttackBuff => attackBuff,
                DefenseBattle.Spell.Heal => heal,
                DefenseBattle.Spell.MoveSpeedBuff => moveSpeedBuff,
                _ => armorBuff
            };
            float radius = DefenseBattle.AreaSpellRadius(rangeStep);
            int targets = 0;
            if (spellCenter is { } center)
            {
                foreach (EnemyUnit enemy in battle.Enemies)
                    if (DefenseBattle.IsInAreaSpellRange(enemy, center, radius) && DefenseBattle.CanReceiveAreaSpell(enemy, spell))
                        targets++;
            }

            string tint = ColorUtility.ToHtmlStringRGB(slot.Tint);
            int cost = DefenseBattle.ManaCost(spell, rangeStep);
            string reason = battle.SpellBlockReason(spell, rangeStep);
            string controls = reason == null ? GameText.Get("aim.area_ready") : GameText.Format("aim.area_blocked", reason);
            SetTip(actionTip, GameText.Format("aim.area", tint, slot.Name, radius, cost, targets, controls));
        }
        else
        {
            SetTip(actionTip, attackTip ?? healTip ?? speedTip ?? armorTip ?? tauntTip);
        }
    }

    private string RefreshSpell(SpellSlot slot, DefenseBattle battle, bool blocked,
        DefenseBattle.Spell? aimedSpell, int aimedRangeStep, bool spellHovered)
    {
        bool aimed = aimedSpell == slot.Spell;
        int rangeStep = aimed ? aimedRangeStep : battle.InitialSpellRangeStep(slot.Spell);
        string reason = battle.SpellBlockReason(slot.Spell, rangeStep);
        bool ready = !blocked && reason == null;
        slot.Control.Button.interactable = ready;
        Color ink = art.ColoredSkillIcons && !slot.TintIcon
            ? (ready ? Color.white : new Color(0.55f, 0.5f, 0.58f, 0.8f))
            : (ready ? slot.Tint : UIPalette.TextDisabled);
        StyleFlatButton(slot.Control, ink, aimed && !blocked, ready ? UIPalette.TextPrimary : UIPalette.TextDisabled);
        if (slot.Frame != null)
        {
            slot.Frame.sprite = aimed && !blocked && art.SlotFrameSelected != null ? art.SlotFrameSelected : art.SlotFrame;
            slot.Frame.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.6f);
        }
        slot.Cost.color = ready ? UIPalette.Mana : UIPalette.TextSecondary;
        slot.Cost.SetText("{0}", DefenseBattle.ManaCost(slot.Spell, rangeStep));

        if (blocked || !(spellHovered ? slot.Control.Hovered : slot.Control.Focused))
            return null;

        if (reason != null)
            return $"{slot.Effect}\n{reason}";
        return slot.Effect;
    }

    private SpellSlot CreateSpellSlot(string name, int index, string shortcut, Sprite sprite,
        DefenseBattle.Spell spell, Color tint, Action<DefenseBattle.Spell> cast, bool tintIcon = false)
    {
        bool coloredIcon = art.ColoredSkillIcons && !tintIcon;
        int iconSize = coloredIcon ? 112 : 82;
        HudButton control = ActionButton(name, spellBar, new Vector2(FirstSpellSlotX + index * SpellSlotSpacing, 0),
            SpellSlotSize, () => cast(spell), sprite, shortcut, iconSize, flat: true);
        RectTransform rect = (RectTransform)control.Button.transform;
        AddFocusRing(control, new Vector2(-3, 6), 116);
        control.Icon.preserveAspect = coloredIcon;
        if (coloredIcon)
        {
            float centerY = art.SlotFrame != null ? -52f : -48f;
            control.Icon.rectTransform.anchoredPosition =
                new Vector2((SpellSlotSize.x - iconSize) * 0.5f, centerY + iconSize * 0.5f);
        }
        Image frame = null;
        if (art.SlotFrame != null)
        {
            // 프레임 아트(128px, 고정 비율)를 아이콘 중심에 맞춰 깔고, 포커스 링 대신 선택 프레임으로 상태를 보여 준다.
            frame = Graphic("Frame", rect, Vector2.up, Vector2.up, new Vector2(-9, 12), new Vector2(128, 128), Color.white);
            frame.sprite = art.SlotFrame;
            frame.transform.SetAsFirstSibling();
            UnityEngine.Object.Destroy(control.Decoration.gameObject);
            control.Decoration = null;
        }
        control.Caption.rectTransform.anchoredPosition = new Vector2(99, 2);
        control.Caption.rectTransform.sizeDelta = new Vector2(18, 22);
        control.Caption.fontSize = 19;
        int costY = frame != null ? -118 : -105;
        // 사용 횟수 제한이 없으므로 마나 비용만 슬롯 아래 가운데에 보여 준다.
        TextMeshProUGUI cost = Label("Cost", rect, 36, costY, 38, 24, 21, TextAlignmentOptions.Center);
        cost.text = DefenseBattle.ManaCost(spell).ToString();
        var (displayName, description) = spell switch
        {
            DefenseBattle.Spell.AttackBuff => (GameText.Get("spell.attack.name"),
                GameText.Format("spell.attack.description", DefenseBattle.BuffDuration, EnemyUnit.AttackBonus)),
            DefenseBattle.Spell.Heal => (GameText.Get("spell.heal.name"),
                GameText.Format("spell.heal.description", DefenseBattle.HealPercent * 100)),
            DefenseBattle.Spell.MoveSpeedBuff => (GameText.Get("spell.move_speed.name"),
                GameText.Format("spell.move_speed.description", DefenseBattle.BuffDuration, EnemyUnit.MoveSpeedBonusPercent * 100)),
            DefenseBattle.Spell.ArmorBuff => (GameText.Get("spell.armor.name"),
                GameText.Format("spell.armor.description", DefenseBattle.BuffDuration, EnemyUnit.ShieldDamageReductionPercent)),
            _ => (GameText.Get("spell.taunt.name"),
                GameText.Format("spell.taunt.description", DefenseBattle.TauntDuration,
                    EnemyUnit.TauntHealthBonusPercent * 100, DefenseBattle.TauntRadius))
        };
        return new SpellSlot(control, spell, tint, cost, displayName, $"{displayName} · {description}", frame, tintIcon);
    }

    private HudButton ActionButton(string name, RectTransform parent, Vector2 position, Vector2 size,
        UnityAction onClick = null, Sprite sprite = null, string caption = "", int iconSize = 0, bool flat = false)
    {
        Image outline = Graphic(name, parent, Vector2.up, Vector2.up, position, size,
            flat ? Color.clear : UIPalette.Outline, flat ? null : art.Button);
        outline.raycastTarget = true;
        var button = outline.gameObject.AddComponent<Button>();
        button.targetGraphic = outline;
        button.transition = Selectable.Transition.None;
        if (onClick != null)
            button.onClick.AddListener(onClick);
        Image face = flat ? null : Inset(outline.rectTransform, UIPalette.ButtonSecondary, art.Button);
        Image icon = AddIcon("Icon", outline.rectTransform, sprite,
            new Vector2((size.x - iconSize) * 0.5f, -7), iconSize, UIPalette.TextPrimary);
        TextMeshProUGUI label = sprite != null
            ? Label("Shortcut", outline.rectTransform, 0, 23 - size.y, size.x, 18, 15, TextAlignmentOptions.Center)
            : Label("Label", outline.rectTransform, 0, 0, size.x, size.y, 22, TextAlignmentOptions.Center);
        label.text = caption;
        return new HudButton(button, face, icon, label);
    }

    private static void StyleButton(HudButton control, Color face, Color ink)
    {
        control.Button.image.color = control.Focused && control.Button.interactable
            ? UIPalette.OutlineFocus
            : control.Hovered ? UIPalette.OutlineStrong : UIPalette.Outline;
        control.Face.color = face;
        control.Icon.color = ink;
        control.Caption.color = ink;
    }

    private static void StyleFlatButton(HudButton control, Color ink, bool marked = false, Color? shortcut = null)
    {
        if (control.Decoration != null)
            control.Decoration.enabled = marked || (control.Focused && control.Button.interactable);
        control.Icon.color = ink;
        control.Caption.color = shortcut ?? ink;
    }

    private RectTransform Panel(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 position,
        Vector2 size, Color color)
    {
        RectTransform outline = Graphic(name, parent, anchor, pivot, position, size, UIPalette.Outline, art.Panel).rectTransform;
        Inset(outline, color, art.Panel);
        return outline;
    }

    private static Image Inset(RectTransform outline, Color color, Sprite surface)
    {
        Image face = Graphic("Face", outline, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, color, surface);
        Stretch(face.rectTransform);
        face.rectTransform.offsetMin = new Vector2(2, 3);
        face.rectTransform.offsetMax = new Vector2(-2, -2);
        return face;
    }

    private static TextMeshProUGUI Tip(string name, RectTransform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, TextAlignmentOptions alignment)
    {
        RectTransform area = Rect(name, parent, anchor, pivot, position, size);
        TextMeshProUGUI label = Label("Tip Text", area, 0, 0, size.x, size.y, 21, alignment);
        area.gameObject.SetActive(false);
        return label;
    }

    private static void SetTip(TextMeshProUGUI label, string text)
    {
        label.transform.parent.gameObject.SetActive(text != null);
        if (text != null)
            label.text = text;
    }

    private static Image AddIcon(string name, RectTransform parent, Sprite sprite, Vector2 position, int size, Color color)
    {
        Image icon = Graphic(name, parent, Vector2.up, Vector2.up, position, new Vector2(size, size), color);
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.enabled = sprite != null;
        return icon;
    }

    private sealed class HudButton
    {
        public readonly Button Button;
        public readonly Image Face;
        public readonly Image Icon;
        public readonly TextMeshProUGUI Caption;
        public MaskableGraphic Decoration;
        public bool Hovered { get; private set; }
        public bool Focused { get; private set; }
        public bool ShowTip => Button.gameObject.activeInHierarchy && (Hovered || Focused);

        public HudButton(Button button, Image face, Image icon, TextMeshProUGUI caption)
        {
            Button = button;
            Face = face;
            Icon = icon;
            Caption = caption;
            EventTrigger events = button.gameObject.AddComponent<EventTrigger>();
            Bind(events, EventTriggerType.PointerEnter, _ => Hovered = true);
            Bind(events, EventTriggerType.PointerExit, _ => Hovered = false);
            Bind(events, EventTriggerType.Select, data => Focused = data is not PointerEventData);
            Bind(events, EventTriggerType.Deselect, _ => Focused = false);
        }

        private static void Bind(EventTrigger trigger, EventTriggerType type, UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }
    }

    private sealed class SpellSlot
    {
        public readonly HudButton Control;
        public readonly DefenseBattle.Spell Spell;
        public readonly Color Tint;
        public readonly TextMeshProUGUI Cost;
        public readonly string Name;
        public readonly string Effect;
        public readonly Image Frame;
        // 채색 아이콘 세트에 전용 그림이 없어 흰 아이콘에 의미 색을 입혀야 하는 슬롯.
        public readonly bool TintIcon;

        public SpellSlot(HudButton control, DefenseBattle.Spell spell, Color tint, TextMeshProUGUI cost,
            string name, string effect, Image frame = null, bool tintIcon = false)
        {
            Frame = frame;
            Control = control;
            Spell = spell;
            Tint = tint;
            Cost = cost;
            Name = name;
            Effect = effect;
            TintIcon = tintIcon;
        }
    }

    private static bool ShowsUnitHealth(EnemyUnit enemy) => enemy.Health < enemy.MaxHealth || enemy.HealFlash > 0f;

    private void AddHealthMarker(Vector3 screen, EnemyUnit enemy)
    {
        if (screen.z <= 0 ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(markers, screen, null, out Vector2 position))
            return;

        if (visibleMarkers == healthMarkers.Count)
        {
            Vector2 center = Vector2.one * 0.5f;
            healthMarkers.Add(Bar("Enemy Health", markers, center, center, Vector2.zero,
                new Vector2(26, HealthBarThickness), UIPalette.TextPrimary));
        }

        Image fill = healthMarkers[visibleMarkers++];
        var track = (RectTransform)fill.transform.parent;
        track.anchoredPosition = position;
        track.gameObject.SetActive(true);
        SetBarFraction(fill, (float)enemy.Health / enemy.MaxHealth);
        fill.color = enemy.Faction.Color;
    }

    private static RectTransform Rect(string name, RectTransform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Graphic(string name, RectTransform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, Color color, Sprite surface = null)
    {
        var image = Rect(name, parent, anchor, pivot, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (surface != null)
        {
            image.sprite = surface;
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    private static Image Bar(string name, RectTransform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, Color color)
    {
        Image background = Graphic(name, parent, anchor, pivot, position, size, UIPalette.BarTrack);
        Image fill = Graphic("Fill", background.rectTransform, Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.zero, color);
        fill.rectTransform.anchorMax = Vector2.one;
        return fill;
    }

    private static void SetBarFraction(Image fill, float fraction) =>
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);

    private static TextMeshProUGUI Label(string name, RectTransform parent, float x, float y, float width, float height, int size,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var text = Rect(name, parent, Vector2.up, Vector2.up, new Vector2(x, y), new Vector2(width, height))
            .gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = UIPalette.TextPrimary;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

internal sealed class HudRing : Image
{
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        const int segments = 48;
        const float thickness = 2.5f;
        Rect bounds = GetPixelAdjustedRect();
        float radius = Mathf.Min(bounds.width, bounds.height) * 0.5f;
        Vector2 center = bounds.center;
        Color32 tint = color;
        vertices.Clear();

        for (int segment = 0; segment < segments; segment++)
        {
            float angle = segment * (2f * Mathf.PI / segments);
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 uv = Vector2.one * 0.5f;
            vertices.AddVert(center + direction * radius, tint, uv + direction * 0.5f);
            vertices.AddVert(center + direction * (radius - thickness), tint, uv + direction * (0.5f - thickness / bounds.width));
        }

        for (int segment = 0; segment < segments; segment++)
        {
            int outer = segment * 2;
            int next = ((segment + 1) % segments) * 2;
            vertices.AddTriangle(outer, outer + 1, next);
            vertices.AddTriangle(next, outer + 1, next + 1);
        }
    }
}
