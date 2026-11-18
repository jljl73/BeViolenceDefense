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
    }

    private const int LockedSpellCount = 3;
    private static readonly Vector2 SpellSlotSize = new(110, 124);
    private static readonly Vector2 ActionsSize = new(600, 184);

    private readonly UIManager ui;
    private readonly Art art;
    private readonly RectTransform markers;
    private readonly RectTransform defeatPopup;
    private readonly TextMeshProUGUI round;
    private readonly TextMeshProUGUI phase;
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
    private readonly TextMeshProUGUI actionTip;
    private readonly HudButton help;
    private readonly TextMeshProUGUI helpTip;
    private readonly List<UnitMarker> unitMarkers = new();
    private readonly Texture2D encounterIcon;
    private readonly List<RawImage> encounterMarkers = new();
    private int visibleMarkers;
    private bool helpPinned;
    private string noticeText = "";
    private float noticeUntil;

    public DefenseHud(UIManager ui, UnityAction restart, UnityAction start, Action<DefenseBattle.Spell> cast,
        Action<int> setBattleSpeed, Texture2D encounterIcon, Art art)
    {
        this.ui = ui;
        this.encounterIcon = encounterIcon;
        this.art = art ?? new Art();
        markers = Rect("Board Markers", ui.HudLayer, Vector2.zero, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Stretch(markers);

        Vector2 topCenter = new(0.5f, 1f);
        RectTransform status = Rect("Round Status", ui.HudLayer, topCenter, topCenter,
            new Vector2(0, -35), new Vector2(220, 75));
        round = Label("Round", status, 0, 0, 220, 40, 30, TextAlignmentOptions.Center);
        phase = Label("Phase", status, 0, -41, 220, 28, 20, TextAlignmentOptions.Center);
        phase.color = UIPalette.TextSecondary;

        RectTransform coreStatus = Rect("Core Status", ui.HudLayer, Vector2.up, Vector2.up,
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

        RectTransform noticeArea = Rect("Notice", ui.HudLayer, Vector2.up, Vector2.up,
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
            start, this.art.Start, "Space", 43, flat: true);
        RectTransform startRect = startBattle.Button.image.rectTransform;
        startBattle.Icon.rectTransform.anchoredPosition = new Vector2(56.5f, -19);
        startBattle.Caption.rectTransform.anchoredPosition = new Vector2(0, -103);
        startBattle.Caption.fontSize = 19;
        TextMeshProUGUI startLabel = Label("Start Label", startRect, 0, -76, 156, 29, 23, TextAlignmentOptions.Center);
        startLabel.text = "Start battle";
        startLabel.color = UIPalette.ButtonPrimary;
        AddFocusRing(startBattle, new Vector2(43, -6), 70);

        spellBar = Rect("Spell Bar", actionBar, Vector2.up, Vector2.up, Vector2.zero, ActionsSize);
        attackBuff = CreateSpellSlot("Attack Buff", 0, "1", this.art.AttackBuff,
            DefenseBattle.Spell.AttackBuff, UIPalette.Attack, cast);
        heal = CreateSpellSlot("Heal", 1, "2", this.art.Heal,
            DefenseBattle.Spell.Heal, UIPalette.Heal, cast);
        moveSpeedBuff = CreateSpellSlot("Move Speed Buff", 2, "3", this.art.MoveSpeedBuff,
            DefenseBattle.Spell.MoveSpeedBuff, UIPalette.MoveSpeed, cast);
        AddIcon("Locked Spells", spellBar, this.art.Lock, new Vector2(550, -42), 17, UIPalette.TextSecondary);
        TextMeshProUGUI locked = Label("Locked Count", spellBar, 573, -39, 40, 23, 19);
        locked.text = $"×{LockedSpellCount}";
        locked.color = UIPalette.TextSecondary;

        RectTransform manaGauge = Rect("Mana Gauge", spellBar, Vector2.up, Vector2.up,
            new Vector2(82, -138), new Vector2(436, 29));
        AddIcon("Mana Drop", manaGauge, this.art.Mana, new Vector2(0, -4), 20, UIPalette.Mana);
        manaFill = Bar("Track", manaGauge, Vector2.up, Vector2.up,
            new Vector2(28, -12), new Vector2(294, 5), UIPalette.Mana);
        mana = Label("Mana", manaGauge, 333, 0, 103, 28, 23);
        mana.color = UIPalette.Mana;

        actionTip = Tip("Action Tip", actionBar, topCenter, bottomCenter,
            new Vector2(0, 5), new Vector2(520, 52), TextAlignmentOptions.Bottom);

        Vector2 center = Vector2.one * 0.5f;
        defeatPopup = Panel("Defeat", ui.PopupLayer, center, center, Vector2.zero,
            new Vector2(290, 196), UIPalette.PanelRaised);
        defeatPopup.gameObject.SetActive(false);
        AddIcon("Broken Core", defeatPopup, this.art.Core, new Vector2(120, -13), 50, UIPalette.ButtonDanger);
        TextMeshProUGUI title = Label("Title", defeatPopup, 20, -62, 250, 32, 28, TextAlignmentOptions.Center);
        title.text = "Core destroyed";
        restartButton = ActionButton("Restart", defeatPopup, new Vector2(89, -105), new Vector2(112, 72),
            restart, this.art.Restart, "R", 39);
    }

    public void Notify(string message)
    {
        noticeText = message;
        noticeUntil = Time.unscaledTime + 2.5f;
    }

    public void Refresh(DefenseBattle battle, DefenseGrid grid, DefenseBoardView view,
        DefenseBattle.Spell? aimedSpell, Vector3? spellCenter, int battleSpeed)
    {
        bool inCombat = battle.CurrentPhase == DefenseBattle.Phase.Combat;
        bool defeated = battle.CurrentPhase == DefenseBattle.Phase.Defeat;
        round.text = $"Round {battle.Round}";
        phase.text = defeated ? "Defeat" : aimedSpell.HasValue ? "Paused" : inCombat ? "Battle" : "Build";
        phase.color = aimedSpell.HasValue ? UIPalette.TextPrimary : UIPalette.TextSecondary;
        core.text = $"{battle.CoreHealth} <size=65%>/ {DefenseBattle.CoreMaxHealth}</size>";
        coreFill.color = battle.CoreHealth <= DefenseBattle.CoreMaxHealth / 4 ? UIPalette.ButtonDanger : UIPalette.ButtonPrimary;
        SetBarFraction(coreFill, (float)battle.CoreHealth / DefenseBattle.CoreMaxHealth);
        wallSupply.gameObject.SetActive(!inCombat && !defeated);
        if (!inCombat && !defeated)
        {
            int wallsLeft = DefenseGrid.WallLimit - grid.WallCount;
            wallCount.text = $"Walls {wallsLeft} / {DefenseGrid.WallLimit}";
            for (int i = 0; i < wallIcons.Length; i++)
                wallIcons[i].color = i < wallsLeft ? UIPalette.TextPrimary : UIPalette.TextDisabled;
        }
        bool showNotice = !defeated && Time.unscaledTime < noticeUntil && noticeText.Length > 0;
        notice.transform.parent.gameObject.SetActive(showNotice);
        if (showNotice)
            notice.text = noticeText;

        bool blocked = ui.HasOpenPopup;
        RefreshActions(battle, aimedSpell, spellCenter, inCombat, defeated, blocked);
        speedPanel.gameObject.SetActive(inCombat);
        RefreshSpeedButton(normalSpeed, battleSpeed == 1, blocked);
        RefreshSpeedButton(doubleSpeed, battleSpeed == 2, blocked);

        help.Button.gameObject.SetActive(!defeated);
        help.Button.interactable = !blocked;
        StyleFlatButton(help, help.ShowTip ? UIPalette.TextPrimary : UIPalette.TextSecondary);
        SetTip(helpTip, !blocked && !defeated && (help.ShowTip || helpPinned)
            ? (inCombat ? "1–3 aim · Left click casts · Esc cancels" : "Left click builds · Right click removes") +
              "\nWASD pan · Wheel zoom · R restart"
            : null);
        StyleButton(restartButton, UIPalette.ButtonPrimary, UIPalette.TextOnAccent);

        RefreshEncounterMarkers(battle, view);

        visibleMarkers = 0;
        foreach (EnemyUnit enemy in battle.Enemies)
        {
            if (enemy.Health == enemy.MaxHealth && enemy.HealFlash <= 0 &&
                enemy.AttackBuffRemaining <= 0 && enemy.MoveSpeedBuffRemaining <= 0)
                continue;

            Color color = enemy.HealFlash > 0 ? UIPalette.Heal : enemy.Faction.Color;
            AddMarker(view.ScreenPoint(enemy, 0.76f), (float)enemy.Health / enemy.MaxHealth, color,
                enemy.AttackBuffRemaining / DefenseBattle.BuffDuration,
                enemy.MoveSpeedBuffRemaining / DefenseBattle.BuffDuration);
        }
        for (int i = visibleMarkers; i < unitMarkers.Count; i++)
            unitMarkers[i].Root.gameObject.SetActive(false);
    }

    public void ShowDefeat() => ui.ShowPopup(defeatPopup, dismissOnEscape: false);

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
        if (battle.CurrentPhase == DefenseBattle.Phase.Preparation && encounterIcon != null)
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
        bool inCombat, bool defeated, bool blocked)
    {
        actionBar.gameObject.SetActive(!defeated);
        preparePanel.gameObject.SetActive(!inCombat);
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
        bool spellHovered = attackBuff.Control.Hovered || heal.Control.Hovered || moveSpeedBuff.Control.Hovered;
        string attackTip = RefreshSpell(attackBuff, battle, blocked, aimedSpell, spellHovered);
        string healTip = RefreshSpell(heal, battle, blocked, aimedSpell, spellHovered);
        string speedTip = RefreshSpell(moveSpeedBuff, battle, blocked, aimedSpell, spellHovered);
        if (!blocked && aimedSpell is { } spell)
        {
            SpellSlot slot = spell == attackBuff.Spell ? attackBuff : spell == heal.Spell ? heal : moveSpeedBuff;
            int targets = 0;
            if (spellCenter is { } center)
            {
                foreach (EnemyUnit enemy in battle.Enemies)
                    if (DefenseBattle.IsInAreaSpellRange(enemy, center) && DefenseBattle.CanReceiveAreaSpell(enemy, spell))
                        targets++;
            }

            string tint = ColorUtility.ToHtmlStringRGB(slot.Tint);
            SetTip(actionTip, $"<color=#{tint}>◆ {slot.Name}</color>  ·  Targets {targets}  ·  Click to cast  ·  Esc");
        }
        else
        {
            SetTip(actionTip, attackTip ?? healTip ?? speedTip);
        }
    }

    private static string RefreshSpell(SpellSlot slot, DefenseBattle battle, bool blocked,
        DefenseBattle.Spell? aimedSpell, bool spellHovered)
    {
        string reason = battle.SpellBlockReason(slot.Spell);
        bool ready = !blocked && reason == null;
        bool aimed = ready && aimedSpell == slot.Spell;
        slot.Control.Button.interactable = ready;
        StyleFlatButton(slot.Control, ready ? slot.Tint : UIPalette.TextDisabled, aimed,
            ready ? UIPalette.TextPrimary : UIPalette.TextDisabled);
        slot.Cost.color = ready ? UIPalette.Mana : UIPalette.TextSecondary;
        slot.Charges.color = ready ? UIPalette.TextSecondary : UIPalette.TextDisabled;
        slot.Charges.text = $"×{battle.CastsLeft(slot.Spell)}";

        if (blocked || !(spellHovered ? slot.Control.Hovered : slot.Control.Focused))
            return null;

        if (reason != null)
            return $"{slot.Effect}\n{reason}";
        return slot.Effect;
    }

    private SpellSlot CreateSpellSlot(string name, int index, string shortcut, Sprite sprite,
        DefenseBattle.Spell spell, Color tint, Action<DefenseBattle.Spell> cast)
    {
        HudButton control = ActionButton(name, spellBar, new Vector2(77 + index * 168, 0), SpellSlotSize,
            () => cast(spell), sprite, shortcut, 90, flat: true);
        RectTransform rect = (RectTransform)control.Button.transform;
        AddFocusRing(control, new Vector2(-3, 6), 116);
        control.Caption.rectTransform.anchoredPosition = new Vector2(99, 2);
        control.Caption.rectTransform.sizeDelta = new Vector2(18, 22);
        control.Caption.fontSize = 19;
        TextMeshProUGUI cost = Label("Cost", rect, 13, -105, 38, 24, 21, TextAlignmentOptions.Right);
        cost.text = DefenseBattle.ManaCost(spell).ToString();
        TextMeshProUGUI charges = Label("Charges", rect, 60, -105, 40, 24, 21);
        string effect = spell switch
        {
            DefenseBattle.Spell.AttackBuff => $"Attack buff · +{EnemyUnit.AttackBonus} damage",
            DefenseBattle.Spell.Heal => $"Area heal · +{DefenseBattle.HealAmount} HP to injured enemies",
            _ => $"Move speed · +{EnemyUnit.MoveSpeedBonusPercent}%"
        };
        return new SpellSlot(control, spell, tint, cost, charges, name, effect);
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
        public readonly TextMeshProUGUI Charges;
        public readonly string Name;
        public readonly string Effect;

        public SpellSlot(HudButton control, DefenseBattle.Spell spell, Color tint, TextMeshProUGUI cost,
            TextMeshProUGUI charges, string name, string effect)
        {
            Control = control;
            Spell = spell;
            Tint = tint;
            Cost = cost;
            Charges = charges;
            Name = name;
            Effect = effect;
        }
    }

    private void AddMarker(Vector3 screen, float health, Color healthColor, float attackBuff, float speedBuff)
    {
        if (screen.z <= 0 ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(markers, screen, null, out Vector2 position))
            return;

        if (visibleMarkers == unitMarkers.Count)
            unitMarkers.Add(new UnitMarker(markers));

        UnitMarker marker = unitMarkers[visibleMarkers++];
        marker.Root.anchoredPosition = position;
        marker.Root.gameObject.SetActive(true);
        SetBarFraction(marker.Health, health);
        marker.Health.color = healthColor;
        marker.AttackBuff.transform.parent.gameObject.SetActive(attackBuff > 0);
        SetBarFraction(marker.AttackBuff, attackBuff);
        var speedBar = (RectTransform)marker.SpeedBuff.transform.parent;
        speedBar.gameObject.SetActive(speedBuff > 0);
        speedBar.anchoredPosition = new Vector2(0, attackBuff > 0 ? 20 : 13);
        SetBarFraction(marker.SpeedBuff, speedBuff);
    }

    private sealed class UnitMarker
    {
        public readonly RectTransform Root;
        public readonly Image Health;
        public readonly Image AttackBuff;
        public readonly Image SpeedBuff;

        public UnitMarker(RectTransform parent)
        {
            Vector2 center = Vector2.one * 0.5f;
            Root = Rect("Enemy Marker", parent, center, center, Vector2.zero, Vector2.zero);
            Health = Bar("Health", Root, center, center, Vector2.zero, new Vector2(26, 5), UIPalette.TextPrimary);
            AttackBuff = Bar("Attack Buff", Root, center, center, new Vector2(0, 13), new Vector2(26, 4),
                UIPalette.Attack);
            SpeedBuff = Bar("Move Speed Buff", Root, center, center, new Vector2(0, 13), new Vector2(26, 4),
                UIPalette.MoveSpeed);
        }
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
