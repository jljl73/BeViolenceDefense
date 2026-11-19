using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>선택 화면에서만 여는 진행 관리. 변경은 설명을 읽고 확인한 뒤 적용한다.</summary>
internal sealed class StageProgressMenu
{
    private readonly UIManager ui;
    private readonly TMP_FontAsset font;
    private readonly Sprite panelSprite;
    private readonly GameObject backgroundTemplate;
    private readonly RectTransform menu;
    private readonly RectTransform confirmation;
    private readonly TextMeshProUGUI confirmationTitle;
    private readonly TextMeshProUGUI confirmationBody;
    private readonly Button confirmButton;
    private readonly TextMeshProUGUI confirmLabel;
    private Action pending;

    public StageProgressMenu(UIManager ui, TextMeshProUGUI heading, StageProgress progress, Action refresh)
    {
        this.ui = ui;
        font = heading.font;
        panelSprite = heading.transform.parent.GetComponent<Image>()?.sprite;
        backgroundTemplate = heading.GetComponentInParent<StageSelectionPopup>(true).transform.Find("Background")?.gameObject;
        menu = Panel("Stage Progress Menu");
        Label(menu, "progress.title", new Vector2(0, 190), new Vector2(650, 60), 36);
        Label(menu, "progress.description", new Vector2(0, 122), new Vector2(650, 70), 23, UIPalette.TextSecondary);
        // 닫기/취소를 먼저 만들어 키보드 기본 선택이 파괴적 동작이 되지 않게 한다.
        Button(menu, "progress.close", new Vector2(0, -190), new Vector2(280, 58), UIPalette.ButtonSecondary,
            () => ui.CloseTopPopup());
        Button(menu, "progress.unlock_all", new Vector2(0, 25), new Vector2(570, 66), UIPalette.ButtonPrimary,
            () => Confirm("progress.unlock_title", "progress.unlock_body", "progress.unlock_all", false, () =>
            {
                progress.UnlockAllStages();
                refresh();
            }));
        Button(menu, "progress.reset", new Vector2(0, -75), new Vector2(570, 66), UIPalette.ButtonSecondary,
            () => Confirm("progress.reset_title", "progress.reset_body", "progress.reset", true, () =>
            {
                progress.Reset();
                refresh();
            }));

        confirmation = Panel("Confirm Stage Progress Change");
        confirmationTitle = Label(confirmation, "progress.title", new Vector2(0, 160), new Vector2(650, 65), 34);
        confirmationBody = Label(confirmation, "progress.description", new Vector2(0, 30), new Vector2(650, 170), 25);
        Button(confirmation, "progress.cancel", new Vector2(-155, -165), new Vector2(270, 64), UIPalette.ButtonSecondary,
            () => { pending = null; ui.CloseTopPopup(); });
        confirmButton = Button(confirmation, "progress.reset", new Vector2(155, -165), new Vector2(270, 64), UIPalette.ButtonDanger,
            () =>
            {
                Action action = pending;
                pending = null;
                if (action == null) return;
                action();
                ui.CloseTopPopup();
                ui.CloseTopPopup();
            });
        confirmLabel = confirmButton.GetComponentInChildren<TextMeshProUGUI>();

        var open = Button((RectTransform)heading.transform.parent, "progress.title", Vector2.zero,
            new Vector2(230, 58), UIPalette.ButtonSecondary, () => ui.ShowPopup(menu));
        var rect = (RectTransform)open.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
        rect.pivot = new Vector2(1, .5f);
        rect.anchoredPosition = new Vector2(-32, 0);
    }

    private void Confirm(string titleKey, string bodyKey, string actionKey, bool destructive, Action action)
    {
        pending = action;
        confirmationTitle.text = GameText.Get(titleKey);
        confirmationBody.text = GameText.Get(bodyKey);
        confirmLabel.text = GameText.Get(actionKey);
        confirmLabel.color = destructive ? UIPalette.TextPrimary : UIPalette.TextOnAccent;
        confirmButton.image.color = destructive ? UIPalette.ButtonDanger : UIPalette.ButtonPrimary;
        ui.ShowPopup(confirmation);
    }

    private RectTransform Panel(string name)
    {
        var rect = Rect(name, ui.PopupLayer, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.gameObject.AddComponent<Image>().color = UIPalette.Background;
        if (backgroundTemplate != null)
            UnityEngine.Object.Instantiate(backgroundTemplate, rect, false);
        var veil = Rect("Veil", rect, Vector2.zero, Vector2.zero);
        veil.anchorMin = Vector2.zero;
        veil.anchorMax = Vector2.one;
        veil.gameObject.AddComponent<Image>().color = UIPalette.Overlay;
        var body = Rect("Panel", rect, Vector2.zero, new Vector2(760, 520));
        var image = body.gameObject.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = UIPalette.Panel;
        var outline = body.gameObject.AddComponent<Outline>();
        outline.effectColor = UIPalette.OutlineStrong;
        outline.effectDistance = new Vector2(2, -2);
        rect.gameObject.SetActive(false);
        return rect;
    }

    private Button Button(RectTransform parent, string key, Vector2 position, Vector2 size, Color color, Action action)
    {
        var rect = Rect(key, parent, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = UIPalette.OutlineFocus;
        colors.selectedColor = UIPalette.OutlineFocus;
        button.colors = colors;
        button.onClick.AddListener(() => action());
        var label = Label(rect, key, Vector2.zero, size - new Vector2(24, 8), 25,
            color == UIPalette.ButtonPrimary ? UIPalette.TextOnAccent : UIPalette.TextPrimary);
        label.raycastTarget = false;
        return button;
    }

    private TextMeshProUGUI Label(RectTransform parent, string key, Vector2 position, Vector2 size, int fontSize, Color? color = null)
    {
        var rect = Rect(key, parent, position, size);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = GameText.Get(key);
        label.fontSize = fontSize;
        label.color = color ?? UIPalette.TextPrimary;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Rect(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
