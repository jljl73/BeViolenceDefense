using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

internal sealed class DefenseHud
{
    private static readonly Color32 PanelColor = new(26, 33, 42, 240);
    private static readonly Color32 TextColor = new(238, 244, 247, 255);
    private static readonly Color32 ManaColor = new(100, 196, 255, 255);
    private static readonly Color32 DisabledActionColor = new(54, 66, 80, 255);
    private static readonly Font Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private readonly UIManager ui;
    private readonly RectTransform markers;
    private readonly RectTransform defeatPopup;
    private readonly Text round;
    private readonly Text core;
    private readonly Image coreFill;
    private readonly Text detail;
    private readonly Text controls;
    private readonly Text notice;
    private readonly RectTransform actionBar;
    private readonly Text mana;
    private readonly Image manaFill;
    private readonly Button startBattle;
    private readonly RectTransform speedPanel;
    private readonly (Button Button, Text Caption) normalSpeed;
    private readonly (Button Button, Text Caption) doubleSpeed;
    private readonly (Button Button, Text Caption, Text Status) attackBuff;
    private readonly (Button Button, Text Caption, Text Status) heal;
    private readonly (Button Button, Text Caption, Text Status) moveSpeedBuff;
    private readonly List<UnitMarker> unitMarkers = new();
    private readonly Texture2D encounterIcon;
    private readonly List<RawImage> encounterMarkers = new();
    private int visibleMarkers;
    private string noticeText = "";
    private float noticeUntil;

    public DefenseHud(UIManager ui, UnityAction restart, UnityAction start, Action<DefenseBattle.Spell> cast,
        Action<int> setBattleSpeed, Texture2D encounterIcon)
    {
        this.ui = ui;
        this.encounterIcon = encounterIcon;
        markers = Rect("Board Markers", ui.HudLayer, Vector2.zero, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Stretch(markers);

        Image status = Graphic("Status", ui.HudLayer, Vector2.up, Vector2.up, new Vector2(30, -24),
            new Vector2(460, 190), PanelColor);
        round = Label("Round", status.rectTransform, 16, -10, 426, 30, 24);
        core = Label("Core", status.rectTransform, 16, -44, 426, 26, 20);
        coreFill = Bar("Core Health", status.rectTransform, Vector2.up, Vector2.up,
            new Vector2(16, -79), new Vector2(240, 9), DefenseBoardView.CoreColor);
        detail = Label("Phase Detail", status.rectTransform, 16, -97, 426, 26, 20);
        controls = Label("Controls", status.rectTransform, 16, -126, 426, 26, 20);
        notice = Label("Notice", status.rectTransform, 16, -155, 426, 26, 18);

        speedPanel = Graphic("Battle Speed", ui.HudLayer, Vector2.one, Vector2.one, new Vector2(-30, -24),
            new Vector2(164, 62), PanelColor).rectTransform;
        normalSpeed = ActionButton("1x", speedPanel, new Vector2(12, -12), new Vector2(64, 38),
            ManaColor, "1x", () => setBattleSpeed(1));
        doubleSpeed = ActionButton("2x", speedPanel, new Vector2(88, -12), new Vector2(64, 38),
            DisabledActionColor, "2x", () => setBattleSpeed(2));

        Text shortcutHint = Label("Camera and Restart Hint", ui.HudLayer, 0, 0, 490, 30, 20);
        shortcutHint.rectTransform.anchorMin = shortcutHint.rectTransform.anchorMax = Vector2.zero;
        shortcutHint.rectTransform.pivot = Vector2.zero;
        shortcutHint.rectTransform.anchoredPosition = new Vector2(30, 16);
        shortcutHint.text = "WASD  /  Pan     Wheel  /  Zoom     R  /  Restart";

        Vector2 bottomCenter = new(0.5f, 0f);
        actionBar = Graphic("Actions", ui.HudLayer, bottomCenter, bottomCenter, new Vector2(0, 24),
            new Vector2(296, 92), PanelColor).rectTransform;
        mana = Label("Mana Amount", actionBar, 20, -10, 800, 26, 21);
        mana.alignment = TextAnchor.MiddleCenter;
        mana.color = ManaColor;
        manaFill = Bar("Mana Gauge", actionBar, Vector2.up, Vector2.up,
            new Vector2(20, -44), new Vector2(800, 11), ManaColor);
        startBattle = ActionButton("Start Battle", actionBar, new Vector2(20, -16), new Vector2(256, 60),
            DefenseBoardView.CoreColor, "Start Battle  /  Space", start).Button;
        attackBuff = SpellButton("Attack Buff", new Vector2(20, -70), $"Attack Buff +{EnemyUnit.AttackBonus}  /  1",
            () => cast(DefenseBattle.Spell.AttackBuff));
        heal = SpellButton("Heal", new Vector2(292, -70), $"Heal +{DefenseBattle.HealAmount}  /  2",
            () => cast(DefenseBattle.Spell.Heal));
        moveSpeedBuff = SpellButton("Move Speed Buff", new Vector2(564, -70),
            $"Move Speed +{EnemyUnit.MoveSpeedBonusPercent}%  /  3", () => cast(DefenseBattle.Spell.MoveSpeedBuff));

        Image panel = Graphic("Defeat", ui.PopupLayer, Vector2.one * 0.5f, Vector2.one * 0.5f,
            Vector2.zero, new Vector2(410, 190), PanelColor);
        defeatPopup = panel.rectTransform;
        defeatPopup.gameObject.SetActive(false);
        Text title = Label("Title", defeatPopup, 24, -22, 362, 54, 30);
        title.alignment = TextAnchor.MiddleCenter;
        title.text = "CORE DESTROYED";

        ActionButton("Restart", defeatPopup, new Vector2(95, -99), new Vector2(220, 55),
            DefenseBoardView.CoreColor, "Restart  /  R", restart);
    }

    public void Notify(string message)
    {
        noticeText = message;
        noticeUntil = Time.unscaledTime + 2.5f;
    }

    public void Refresh(DefenseBattle battle, DefenseGrid grid, DefenseBoardView view, EnemyUnit selected,
        DefenseBattle.Spell? aimedSpell, int battleSpeed)
    {
        string phase = battle.CurrentPhase switch
        {
            DefenseBattle.Phase.Combat => "BATTLE",
            DefenseBattle.Phase.Defeat => "DEFEAT",
            _ => "PREPARATION"
        };
        round.text = $"ROUND {battle.Round}  /  {phase}";
        core.text = $"CORE   {battle.CoreHealth} / {DefenseBattle.CoreMaxHealth}";
        SetBarFraction(coreFill, (float)battle.CoreHealth / DefenseBattle.CoreMaxHealth);
        notice.text = Time.unscaledTime < noticeUntil ? noticeText : "";
        RefreshActions(battle, selected, aimedSpell);
        speedPanel.gameObject.SetActive(battle.CurrentPhase != DefenseBattle.Phase.Defeat);
        RefreshSpeedButton(normalSpeed, battleSpeed == 1);
        RefreshSpeedButton(doubleSpeed, battleSpeed == 2);

        switch (battle.CurrentPhase)
        {
            case DefenseBattle.Phase.Preparation:
                detail.text = $"WALLS   {grid.WallCount} / {DefenseGrid.WallLimit}";
                controls.text = "Left click: Build  /  Right click: Remove";
                break;
            case DefenseBattle.Phase.Combat:
                detail.text = $"Spawned {battle.SpawnCount}/{DefenseBattle.EnemiesPerRound}";
                string healHint = selected == null
                    ? "Select an enemy to heal"
                    : $"{selected.Faction.DisplayName} selected   HP {selected.Health}/{EnemyUnit.MaxHealth}";
                controls.text = aimedSpell.HasValue
                    ? "Movement paused  /  Click: Cast  /  Esc: Cancel"
                    : healHint;
                break;
            case DefenseBattle.Phase.Defeat:
                detail.text = controls.text = notice.text = "";
                break;
        }

        RefreshEncounterMarkers(battle, view);

        visibleMarkers = 0;
        foreach (EnemyUnit enemy in battle.Enemies)
        {
            Color color = enemy.HealFlash > 0 ? DefenseBoardView.HealColor : enemy.Faction.Color;
            AddMarker(view.ScreenPoint(enemy, 0.96f), (float)enemy.Health / EnemyUnit.MaxHealth, color,
                enemy.AttackBuffRemaining / DefenseBattle.BuffDuration,
                enemy.MoveSpeedBuffRemaining / DefenseBattle.BuffDuration, enemy == selected);
        }
        for (int i = visibleMarkers; i < unitMarkers.Count; i++)
            unitMarkers[i].Root.gameObject.SetActive(false);
    }

    public void ShowDefeat() => ui.ShowPopup(defeatPopup, dismissOnEscape: false);

    private void RefreshSpeedButton((Button Button, Text Caption) control, bool selected)
    {
        control.Button.interactable = !ui.HasOpenPopup;
        control.Button.image.color = selected ? ManaColor : DisabledActionColor;
        control.Caption.color = selected ? PanelColor : TextColor;
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

    private void RefreshActions(DefenseBattle battle, EnemyUnit selected, DefenseBattle.Spell? aimedSpell)
    {
        bool inCombat = battle.CurrentPhase == DefenseBattle.Phase.Combat;
        bool blockedByPopup = ui.HasOpenPopup;
        actionBar.gameObject.SetActive(battle.CurrentPhase != DefenseBattle.Phase.Defeat);
        actionBar.sizeDelta = inCombat ? new Vector2(840, 146) : new Vector2(296, 92);
        startBattle.gameObject.SetActive(battle.CurrentPhase == DefenseBattle.Phase.Preparation);
        startBattle.interactable = !blockedByPopup;
        attackBuff.Button.gameObject.SetActive(inCombat);
        heal.Button.gameObject.SetActive(inCombat);
        moveSpeedBuff.Button.gameObject.SetActive(inCombat);
        mana.gameObject.SetActive(inCombat);
        manaFill.transform.parent.gameObject.SetActive(inCombat);

        if (!inCombat)
            return;

        mana.text = $"MANA  {Mathf.FloorToInt(battle.Mana)} / {DefenseBattle.MaxMana}";
        SetBarFraction(manaFill, battle.Mana / DefenseBattle.MaxMana);
        RefreshSpell(attackBuff, DefenseBattle.Spell.AttackBuff, battle, selected, blockedByPopup, aimedSpell);
        RefreshSpell(heal, DefenseBattle.Spell.Heal, battle, selected, blockedByPopup, aimedSpell);
        RefreshSpell(moveSpeedBuff, DefenseBattle.Spell.MoveSpeedBuff, battle, selected, blockedByPopup, aimedSpell);
    }

    private static void RefreshSpell((Button Button, Text Caption, Text Status) control,
        DefenseBattle.Spell spell, DefenseBattle battle, EnemyUnit selected, bool blockedByPopup,
        DefenseBattle.Spell? aimedSpell)
    {
        string reason = battle.SpellBlockReason(selected, spell);
        bool ready = !blockedByPopup && reason == null;
        string readyStatus = aimedSpell == spell ? "Aiming" : "Ready";
        string status = blockedByPopup ? "Menu open" : reason ?? readyStatus;
        control.Button.interactable = ready;
        control.Button.image.color = ready ? ManaColor : DisabledActionColor;
        control.Caption.color = control.Status.color = ready ? PanelColor : TextColor;
        control.Status.text = $"{DefenseBattle.ManaCost(spell)} mana  /  {battle.CastsLeft(spell)} left  /  {status}";
    }

    private (Button Button, Text Caption, Text Status) SpellButton(string name, Vector2 position,
        string title, UnityAction onClick)
    {
        var (button, caption) = ActionButton(name, actionBar, position, new Vector2(256, 60),
            DisabledActionColor, title, onClick);
        var rect = (RectTransform)button.transform;
        caption.rectTransform.sizeDelta = new Vector2(256, 28);
        Text status = Label("Availability", rect, 2, -28, 252, 28, 15);
        status.alignment = TextAnchor.MiddleCenter;
        return (button, caption, status);
    }

    private static (Button Button, Text Caption) ActionButton(string name, RectTransform parent, Vector2 position,
        Vector2 size, Color color, string title, UnityAction onClick)
    {
        Image image = Graphic(name, parent, Vector2.up, Vector2.up, position, size, color);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.disabledColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(onClick);
        Text caption = Label("Label", image.rectTransform, 0, 0, size.x, size.y, 22);
        caption.color = PanelColor;
        caption.alignment = TextAnchor.MiddleCenter;
        caption.text = title;
        return (button, caption);
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
                new Vector2(52, 13), Color.white).gameObject;
            Health = Bar("Health", Root, center, center, Vector2.zero, new Vector2(46, 7), Color.white);
            AttackBuff = Bar("Attack Buff", Root, center, center, new Vector2(0, 13), new Vector2(46, 5),
                DefenseBoardView.CoreColor);
            SpeedBuff = Bar("Move Speed Buff", Root, center, center, new Vector2(0, 13), new Vector2(46, 5),
                DefenseBoardView.MoveSpeedBuffColor);
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
        Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, parent, anchor, pivot, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image Bar(string name, RectTransform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, Color color)
    {
        Image background = Graphic(name, parent, anchor, pivot, position, size, new Color32(14, 20, 30, 255));
        Image fill = Graphic("Fill", background.rectTransform, Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.zero, color);
        fill.rectTransform.anchorMax = Vector2.one;
        return fill;
    }

    private static void SetBarFraction(Image fill, float fraction) =>
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);

    private static Text Label(string name, RectTransform parent, float x, float y, float width, float height, int size)
    {
        var text = Rect(name, parent, Vector2.up, Vector2.up, new Vector2(x, y), new Vector2(width, height))
            .gameObject.AddComponent<Text>();
        text.font = Font;
        text.fontSize = size;
        text.color = TextColor;
        text.alignment = TextAnchor.MiddleLeft;
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
