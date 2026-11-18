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

    private const int SpellSlotCount = 6;
    private const int SpellSlotWidth = 108;
    private const int SpellSlotHeight = 114;
    private const int SpellSlotGap = 10;
    private const int SpellRowWidth = SpellSlotCount * (SpellSlotWidth + SpellSlotGap) - SpellSlotGap;
    private static readonly Vector2 SpellSlotSize = new(SpellSlotWidth, SpellSlotHeight);
    private static readonly Vector2 SpellBarSize = new(SpellRowWidth, 160);
    private static readonly Vector2 PrepareSize = new(110, 110);

    private readonly UIManager ui;
    private readonly Art art;
    private readonly RectTransform markers;
    private readonly RectTransform defeatPopup;
    private readonly TextMeshProUGUI round;
    private readonly TextMeshProUGUI phase;
    private readonly TextMeshProUGUI core;
    private readonly Image coreFill;
    private readonly Image countIcon;
    private readonly TextMeshProUGUI count;
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

        RectTransform status = Panel("Status", ui.HudLayer, Vector2.up, new Vector2(28, -24), new Vector2(254, 108));
        AddIcon("Round Flag", status, this.art.Round, new Vector2(13, -10), 27, UIPalette.TextPrimary);
        round = Label("Round", status, 45, -9, 65, 28, 23);
        phase = Label("Phase", status, 111, -9, 129, 28, 17, TextAlignmentOptions.Right);
        phase.color = UIPalette.TextSecondary;

        AddIcon("Core Gem", status, this.art.Core, new Vector2(13, -48), 32, UIPalette.ButtonPrimary);
        core = Label("Core", status, 46, -49, 192, 22, 18, TextAlignmentOptions.Right);
        coreFill = Bar("Core Health", status, Vector2.up, Vector2.up,
            new Vector2(56, -77), new Vector2(182, 10), UIPalette.ButtonPrimary);

        RectTransform counter = Panel("Phase Counter", ui.HudLayer, Vector2.up, new Vector2(28, -140),
            new Vector2(126, 50));
        countIcon = AddIcon("Counter Icon", counter, this.art.Wall, new Vector2(10, -9), 32, UIPalette.TextSecondary);
        count = Label("Count", counter, 49, -10, 65, 30, 20, TextAlignmentOptions.Center);

        RectTransform noticePanel = Panel("Notice", ui.HudLayer, Vector2.up, new Vector2(28, -199),
            new Vector2(414, 48), UIPalette.PanelBrown);
        notice = Label("Message", noticePanel, 12, -2, 390, 43, 17, TextAlignmentOptions.Center);
        noticePanel.gameObject.SetActive(false);

        speedPanel = Panel("Battle Speed", ui.HudLayer, Vector2.one, new Vector2(-28, -24),
            new Vector2(156, 58));
        normalSpeed = ActionButton("1x", speedPanel, new Vector2(8, -8), new Vector2(66, 42),
            () => setBattleSpeed(1), caption: "1×");
        doubleSpeed = ActionButton("2x", speedPanel, new Vector2(82, -8), new Vector2(66, 42),
            () => setBattleSpeed(2), caption: "2×");

        RectTransform helpDock = Rect("Help Dock", ui.HudLayer, Vector2.zero, Vector2.zero,
            new Vector2(28, 24), new Vector2(47, 47));
        help = ActionButton("Controls", helpDock, Vector2.zero, helpDock.sizeDelta,
            () => helpPinned = !helpPinned, caption: "?");
        helpTip = Tip("Controls Tip", helpDock, Vector2.up, Vector2.zero, new Vector2(0, 8), new Vector2(360, 82));

        Vector2 bottomCenter = new(0.5f, 0f);
        actionBar = Rect("Actions", ui.HudLayer, bottomCenter, bottomCenter, new Vector2(0, 24), PrepareSize);
        preparePanel = Panel("Prepare", actionBar, Vector2.up, Vector2.zero, PrepareSize);
        startBattle = ActionButton("Start Battle", preparePanel, new Vector2(12, -12), new Vector2(86, 86),
            start, this.art.Start, "Space", 48);

        spellBar = Rect("Spell Bar", actionBar, Vector2.up, Vector2.up, Vector2.zero, SpellBarSize);
        attackBuff = CreateSpellSlot("Attack Buff", 0, "1", this.art.AttackBuff,
            DefenseBattle.Spell.AttackBuff, UIPalette.Attack, cast);
        heal = CreateSpellSlot("Heal", 1, "2", this.art.Heal,
            DefenseBattle.Spell.Heal, UIPalette.Heal, cast);
        moveSpeedBuff = CreateSpellSlot("Move Speed Buff", 2, "3", this.art.MoveSpeedBuff,
            DefenseBattle.Spell.MoveSpeedBuff, UIPalette.MoveSpeed, cast);
        for (int i = 3; i < SpellSlotCount; i++)
            CreateLockedSlot(i);

        RectTransform manaGauge = Rect("Mana Gauge", spellBar, Vector2.up, Vector2.up,
            new Vector2(0, -SpellSlotHeight - 8), new Vector2(SpellRowWidth, 38));
        AddIcon("Mana Drop", manaGauge, this.art.Mana, new Vector2(0, -1), 36, UIPalette.Mana);
        int frameWidth = SpellRowWidth - 138;
        Image manaFrame = Graphic("Mana Frame", manaGauge, Vector2.up, Vector2.up,
            new Vector2(41, -2), new Vector2(frameWidth, 34), UIPalette.Outline, this.art.Button);
        manaFrame.pixelsPerUnitMultiplier = 1.8f;
        manaFill = Bar("Track", manaFrame.rectTransform, Vector2.up, Vector2.up,
            new Vector2(2, -2), new Vector2(frameWidth - 4, 30), UIPalette.Mana, this.art.Button);
        mana = Label("Mana", manaGauge, SpellRowWidth - 93, 0, 93, 38, 23, TextAlignmentOptions.Right);
        mana.color = UIPalette.Mana;

        actionTip = Tip("Action Tip", actionBar, new Vector2(0.5f, 1), bottomCenter,
            new Vector2(0, 8), new Vector2(354, 60));

        defeatPopup = Panel("Defeat", ui.PopupLayer, Vector2.one * 0.5f, Vector2.zero,
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

    public void Refresh(DefenseBattle battle, DefenseGrid grid, DefenseBoardView view, EnemyUnit selected,
        DefenseBattle.Spell? aimedSpell, int battleSpeed)
    {
        bool inCombat = battle.CurrentPhase == DefenseBattle.Phase.Combat;
        bool defeated = battle.CurrentPhase == DefenseBattle.Phase.Defeat;
        round.text = battle.Round.ToString();
        phase.text = defeated ? "Defeat" : inCombat ? "Battle" : "Build";
        core.text = $"{battle.CoreHealth}/{DefenseBattle.CoreMaxHealth}";
        coreFill.color = battle.CoreHealth <= DefenseBattle.CoreMaxHealth / 4 ? UIPalette.ButtonDanger : UIPalette.ButtonPrimary;
        SetBarFraction(coreFill, (float)battle.CoreHealth / DefenseBattle.CoreMaxHealth);
        countIcon.sprite = inCombat ? art.Enemy : art.Wall;
        countIcon.enabled = countIcon.sprite != null;
        count.transform.parent.gameObject.SetActive(!defeated);
        count.text = inCombat ? $"{battle.SpawnCount}/{DefenseBattle.EnemiesPerRound}" : $"{grid.WallCount}/{DefenseGrid.WallLimit}";
        bool showNotice = !defeated && Time.unscaledTime < noticeUntil && noticeText.Length > 0;
        notice.transform.parent.gameObject.SetActive(showNotice);
        if (showNotice)
            notice.text = noticeText;

        bool blocked = ui.HasOpenPopup;
        RefreshActions(battle, selected, aimedSpell, inCombat, defeated, blocked);
        speedPanel.gameObject.SetActive(!defeated);
        RefreshSpeedButton(normalSpeed, battleSpeed == 1, blocked);
        RefreshSpeedButton(doubleSpeed, battleSpeed == 2, blocked);

        help.Button.gameObject.SetActive(!defeated);
        help.Button.interactable = !blocked;
        StyleButton(help, UIPalette.ButtonSecondary, UIPalette.TextPrimary);
        SetTip(helpTip, !blocked && !defeated && (help.ShowTip || helpPinned)
            ? (inCombat ? "Click an enemy to target Heal" : "Left click builds · Right click removes") +
              "\nWASD pan · Wheel zoom · R restart"
            : null);
        StyleButton(restartButton, UIPalette.ButtonPrimary, UIPalette.TextOnAccent);

        RefreshEncounterMarkers(battle, view);

        visibleMarkers = 0;
        foreach (EnemyUnit enemy in battle.Enemies)
        {
            if (enemy.Health == EnemyUnit.MaxHealth && enemy.HealFlash <= 0 &&
                enemy.AttackBuffRemaining <= 0 && enemy.MoveSpeedBuffRemaining <= 0 && enemy != selected)
                continue;

            Color color = enemy.HealFlash > 0 ? UIPalette.Heal : enemy.Faction.Color;
            AddMarker(view.ScreenPoint(enemy, 0.76f), (float)enemy.Health / EnemyUnit.MaxHealth, color,
                enemy.AttackBuffRemaining / DefenseBattle.BuffDuration,
                enemy.MoveSpeedBuffRemaining / DefenseBattle.BuffDuration, enemy == selected);
        }
        for (int i = visibleMarkers; i < unitMarkers.Count; i++)
            unitMarkers[i].Root.gameObject.SetActive(false);
    }

    public void ShowDefeat() => ui.ShowPopup(defeatPopup, dismissOnEscape: false);

    private static void RefreshSpeedButton(HudButton control, bool selected, bool blocked)
    {
        control.Button.interactable = !blocked;
        StyleButton(control, selected ? UIPalette.PanelRaised : UIPalette.ButtonDisabled,
            selected ? UIPalette.TextPrimary : UIPalette.TextSecondary, selected);
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

    private void RefreshActions(DefenseBattle battle, EnemyUnit selected, DefenseBattle.Spell? aimedSpell,
        bool inCombat, bool defeated, bool blocked)
    {
        actionBar.gameObject.SetActive(!defeated);
        actionBar.sizeDelta = inCombat ? SpellBarSize : PrepareSize;
        preparePanel.gameObject.SetActive(!inCombat);
        startBattle.Button.interactable = !blocked;
        StyleButton(startBattle, UIPalette.ButtonPrimary, UIPalette.TextOnAccent);
        spellBar.gameObject.SetActive(inCombat);

        if (!inCombat)
        {
            SetTip(actionTip, !blocked && startBattle.ShowTip ? "Start battle" : null);
            return;
        }

        mana.text = $"{Mathf.FloorToInt(battle.Mana)}/{DefenseBattle.MaxMana}";
        SetBarFraction(manaFill, battle.Mana / DefenseBattle.MaxMana);
        bool spellHovered = attackBuff.Control.Hovered || heal.Control.Hovered || moveSpeedBuff.Control.Hovered;
        string attackTip = RefreshSpell(attackBuff, battle, selected, blocked, aimedSpell, spellHovered);
        string healTip = RefreshSpell(heal, battle, selected, blocked, aimedSpell, spellHovered);
        string speedTip = RefreshSpell(moveSpeedBuff, battle, selected, blocked, aimedSpell, spellHovered);
        SetTip(actionTip, attackTip ?? healTip ?? speedTip ??
            (!blocked && aimedSpell.HasValue ? "Click to cast · Esc to cancel" : null));
    }

    private static string RefreshSpell(SpellSlot slot, DefenseBattle battle, EnemyUnit selected, bool blocked,
        DefenseBattle.Spell? aimedSpell, bool spellHovered)
    {
        string reason = battle.SpellBlockReason(selected, slot.Spell);
        bool ready = !blocked && reason == null;
        bool aimed = ready && aimedSpell == slot.Spell;
        slot.Control.Button.interactable = ready;
        StyleButton(slot.Control, ready ? UIPalette.PanelRaised : UIPalette.ButtonDisabled,
            ready ? slot.Tint : UIPalette.TextSecondary, aimed,
            ready ? UIPalette.TextSecondary : UIPalette.TextDisabled);
        slot.Cost.color = ready ? UIPalette.Mana : UIPalette.TextSecondary;
        slot.Charges.color = ready ? UIPalette.TextPrimary : UIPalette.TextSecondary;
        slot.Charges.text = $"×{battle.CastsLeft(slot.Spell)}";

        if (blocked || !(spellHovered ? slot.Control.Hovered : slot.Control.Focused))
            return null;

        if (aimed)
            return $"{slot.Effect}\nClick to cast · Esc to cancel";
        if (reason != null)
            return $"{slot.Effect}\n{reason}";
        return slot.Effect;
    }

    private static Vector2 SpellSlotPosition(int index) => new(index * (SpellSlotWidth + SpellSlotGap), 0);

    private void CreateLockedSlot(int index)
    {
        Image outline = Graphic($"Locked Slot {index + 1}", spellBar, Vector2.up, Vector2.up,
            SpellSlotPosition(index), SpellSlotSize, UIPalette.Outline, art.Button);
        outline.raycastTarget = true;
        Inset(outline.rectTransform, UIPalette.Panel, art.Button);
        const int iconSize = 38;
        AddIcon("Lock", outline.rectTransform, art.Lock,
            new Vector2((SpellSlotWidth - iconSize) * 0.5f, (iconSize - SpellSlotHeight) * 0.5f),
            iconSize, UIPalette.TextDisabled);
    }

    private SpellSlot CreateSpellSlot(string name, int index, string shortcut, Sprite sprite,
        DefenseBattle.Spell spell, Color tint, Action<DefenseBattle.Spell> cast)
    {
        HudButton control = ActionButton(name, spellBar, SpellSlotPosition(index), SpellSlotSize,
            () => cast(spell), sprite, shortcut, 61);
        RectTransform rect = (RectTransform)control.Button.transform;
        control.Caption.rectTransform.anchoredPosition = new Vector2(10, -7);
        control.Caption.rectTransform.sizeDelta = new Vector2(18, 20);
        control.Icon.rectTransform.anchoredPosition = new Vector2(24, -15);
        RectTransform stats = Graphic("Spell Stats", rect, Vector2.up, Vector2.up,
            new Vector2(7, -80), new Vector2(94, 26), UIPalette.BarTrack, art.Button).rectTransform;
        stats.GetComponent<Image>().pixelsPerUnitMultiplier = 2.4f;
        AddIcon("Mana Cost", stats, art.Mana, new Vector2(7, -5), 16, UIPalette.Mana);
        TextMeshProUGUI cost = Label("Cost", stats, 25, -2, 28, 21, 16);
        cost.text = DefenseBattle.ManaCost(spell).ToString();
        TextMeshProUGUI charges = Label("Charges", stats, 56, -2, 30, 21, 16, TextAlignmentOptions.Right);
        string effect = spell switch
        {
            DefenseBattle.Spell.AttackBuff => $"Attack buff · +{EnemyUnit.AttackBonus} damage",
            DefenseBattle.Spell.Heal => $"Heal · +{DefenseBattle.HealAmount} HP",
            _ => $"Move speed · +{EnemyUnit.MoveSpeedBonusPercent}%"
        };
        return new SpellSlot(control, spell, tint, cost, charges, effect);
    }

    private HudButton ActionButton(string name, RectTransform parent, Vector2 position, Vector2 size,
        UnityAction onClick = null, Sprite sprite = null, string caption = "", int iconSize = 0)
    {
        Image outline = Graphic(name, parent, Vector2.up, Vector2.up, position, size, UIPalette.Outline, art.Button);
        outline.raycastTarget = true;
        var button = outline.gameObject.AddComponent<Button>();
        button.targetGraphic = outline;
        button.transition = Selectable.Transition.None;
        if (onClick != null)
            button.onClick.AddListener(onClick);
        Image face = Inset(outline.rectTransform, UIPalette.ButtonSecondary, art.Button);
        Image icon = AddIcon("Icon", outline.rectTransform, sprite,
            new Vector2((size.x - iconSize) * 0.5f, -7), iconSize, UIPalette.TextPrimary);
        TextMeshProUGUI label = sprite != null
            ? Label("Shortcut", outline.rectTransform, 0, 23 - size.y, size.x, 18, 15, TextAlignmentOptions.Center)
            : Label("Label", outline.rectTransform, 0, 0, size.x, size.y, 22, TextAlignmentOptions.Center);
        label.text = sprite != null || iconSize == 0 ? caption : name;
        return new HudButton(button, face, icon, label);
    }

    private static void StyleButton(HudButton control, Color face, Color ink, bool marked = false, Color? shortcut = null)
    {
        control.Button.image.color = marked || (control.Focused && control.Button.interactable)
            ? UIPalette.OutlineFocus
            : control.Hovered ? UIPalette.OutlineStrong : UIPalette.Outline;
        control.Face.color = face;
        control.Icon.color = ink;
        control.Caption.color = shortcut ?? ink;
    }

    private RectTransform Panel(string name, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size,
        Color? color = null) => Panel(name, parent, anchor, anchor, position, size, color ?? UIPalette.Panel);

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

    private TextMeshProUGUI Tip(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform panel = Panel(name, parent, anchor, pivot, position, size, UIPalette.PanelRaised);
        TextMeshProUGUI label = Label("Tip Text", panel, 10, -4, size.x - 20, size.y - 8, 17, TextAlignmentOptions.Center);
        panel.gameObject.SetActive(false);
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
        public readonly string Effect;

        public SpellSlot(HudButton control, DefenseBattle.Spell spell, Color tint, TextMeshProUGUI cost, TextMeshProUGUI charges, string effect)
        {
            Control = control;
            Spell = spell;
            Tint = tint;
            Cost = cost;
            Charges = charges;
            Effect = effect;
        }
    }

    private void AddMarker(Vector3 screen, float health, Color healthColor, float attackBuff, float speedBuff, bool selected)
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
        marker.Highlight.SetActive(selected);
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
        public readonly GameObject Highlight;
        public readonly Image Health;
        public readonly Image AttackBuff;
        public readonly Image SpeedBuff;

        public UnitMarker(RectTransform parent)
        {
            Vector2 center = Vector2.one * 0.5f;
            Root = Rect("Enemy Marker", parent, center, center, Vector2.zero, Vector2.zero);
            Highlight = Graphic("Selected", Root, center, center, Vector2.zero,
                new Vector2(30, 11), UIPalette.OutlineFocus).gameObject;
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
        Vector2 position, Vector2 size, Color color, Sprite surface = null)
    {
        Image background = Graphic(name, parent, anchor, pivot, position, size, UIPalette.BarTrack, surface);
        if (surface != null)
        {
            background.pixelsPerUnitMultiplier = 2f;
            background.gameObject.AddComponent<Mask>();
        }
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
