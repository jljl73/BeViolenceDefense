using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 머리 위의 작은 정보 영역. 버프의 주된 표현은 몸 주변 오라가 맡는다.
internal sealed class UnitStatusMarker
{
    public readonly RectTransform Root;
    public readonly RectTransform BuffRow;
    private readonly Image border, damage, fill;
    private readonly Image[] icons = new Image[4];
    private readonly UnitBuffTimer[] timers = new UnitBuffTimer[4];
    private readonly TextMeshProUGUI[] stacks = new TextMeshProUGUI[4];
    private readonly RectTransform[] slots = new RectTransform[4];
    private readonly bool coloredIcons;
    private float previous, trailing, hold, flash;
    public EnemyUnit Unit { get; private set; }
    public bool Seen;
    public Rect BarBounds => new(Root.anchoredPosition - new Vector2(26, 6), new Vector2(52, 12));
    public Rect BuffBounds => new(Root.anchoredPosition + new Vector2(-25, -25), new Vector2(54, 18));
    public bool HasBuffs { get; private set; }

    public UnitStatusMarker(RectTransform parent, Sprite[] sprites, bool coloredIcons)
    {
        this.coloredIcons = coloredIcons;
        Root = Rect("Unit Status", parent, Vector2.zero, new Vector2(52, 12));
        border = Image("Outline", Root, Vector2.zero, new Vector2(52, 12), UIPalette.Background);
        Image("Track", Root, Vector2.zero, new Vector2(48, 8), UIPalette.BarTrack);
        damage = Image("Damage Trail", Root, Vector2.zero, new Vector2(48, 8), UIPalette.ButtonPrimary);
        fill = Image("Health", Root, Vector2.zero, new Vector2(48, 8), UIPalette.TextPrimary);
        BuffRow = Rect("Buffs", Root, new Vector2(0, -14), new Vector2(48, 12));
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = Rect("Buff " + i, BuffRow, Vector2.zero, new Vector2(11, 11));
            Image("Backing", slots[i], Vector2.zero, new Vector2(10, 10), UIPalette.Overlay);
            icons[i] = Image("Icon", slots[i], Vector2.zero, new Vector2(7, 7), UIPalette.TextPrimary);
            icons[i].sprite = sprites[i];
            icons[i].preserveAspect = true;
            timers[i] = Rect("Remaining", slots[i], Vector2.zero, new Vector2(12, 12)).gameObject.AddComponent<UnitBuffTimer>();
            timers[i].raycastTarget = false;
            stacks[i] = Rect("Stacks", slots[i], new Vector2(4, -4), new Vector2(12, 10)).gameObject.AddComponent<TextMeshProUGUI>();
            stacks[i].font = TMP_Settings.defaultFontAsset;
            stacks[i].fontSize = 8;
            stacks[i].fontStyle = FontStyles.Bold;
            stacks[i].alignment = TextAlignmentOptions.BottomRight;
            stacks[i].color = UIPalette.TextPrimary;
            stacks[i].raycastTarget = false;
            var shadow = stacks[i].gameObject.AddComponent<Shadow>();
            shadow.effectColor = UIPalette.Background;
            shadow.effectDistance = new Vector2(1, -1);
        }
    }

    public void Bind(EnemyUnit unit)
    {
        Unit = unit;
        previous = trailing = (float)unit.Health / unit.MaxHealth;
        hold = flash = 0f;
        Root.gameObject.SetActive(true);
    }

    public void Refresh(Vector2 head, float deltaTime, float time)
    {
        Seen = true;
        // 아이콘까지 모델의 머리보다 위에 둔다.
        Root.anchoredPosition = head + Vector2.up * 28f;
        float fraction = (float)Unit.Health / Unit.MaxHealth;
        if (fraction < previous) { trailing = Mathf.Max(trailing, previous); hold = .10f; flash = .10f; }
        if (fraction > previous) trailing = fraction;
        previous = fraction;
        hold = Mathf.Max(0f, hold - deltaTime);
        flash = Mathf.Max(0f, flash - deltaTime);
        if (hold <= 0f) trailing = Mathf.MoveTowards(trailing, fraction, deltaTime * 2f);
        SetFraction(fill, fraction);
        SetFraction(damage, Mathf.Max(fraction, trailing));
        fill.color = Color.Lerp(Unit.Faction.Color, UIPalette.TextPrimary, flash / .10f * .65f);
        border.color = fraction <= .25f
            ? Color.Lerp(UIPalette.Background, UIPalette.ButtonDanger, .3f + .35f * (.5f + .5f * Mathf.Sin(time * 4f)))
            : UIPalette.Background;
        int count = 0;
        RefreshBuff(0, Unit.AttackBuffStacks, Unit.AttackBuffRemaining, DefenseBattle.BuffDuration, UIPalette.Attack, time, ref count);
        RefreshBuff(1, Unit.MoveSpeedBuffStacks, Unit.MoveSpeedBuffRemaining, DefenseBattle.BuffDuration, UIPalette.MoveSpeed, time, ref count);
        RefreshBuff(2, Unit.ArmorBuffStacks, Unit.ArmorBuffRemaining, DefenseBattle.BuffDuration, UIPalette.Armor, time, ref count);
        RefreshBuff(3, Unit.IsTaunting ? 1 : 0, Unit.TauntRemaining, DefenseBattle.TauntDuration, UIPalette.Taunt, time, ref count);
        HasBuffs = count > 0;
        BuffRow.anchoredPosition = new Vector2(-(count - 1) * 6f, -14f);
        BuffRow.gameObject.SetActive(HasBuffs);
    }

    private void RefreshBuff(int index, int count, float remaining, float duration, Color color, float time, ref int position)
    {
        slots[index].gameObject.SetActive(count > 0);
        if (count <= 0) return;
        slots[index].anchoredPosition = new Vector2(position++ * 12f, 0);
        color.a = remaining < 1f ? .55f + .35f * (.5f + .5f * Mathf.Sin(time * 8f)) : .85f;
        icons[index].color = coloredIcons && icons[index].sprite != null ? new Color(1, 1, 1, color.a) : color;
        timers[index].color = color;
        timers[index].SetFraction(remaining / duration);
        stacks[index].gameObject.SetActive(count > 1);
        if (count > 1) stacks[index].text = count > 9 ? "9+" : count.ToString();
    }

    private static void SetFraction(Image image, float fraction)
    {
        var rect = image.rectTransform;
        rect.sizeDelta = new Vector2(48f * Mathf.Clamp01(fraction), 8);
        rect.anchoredPosition = new Vector2(-24f + rect.sizeDelta.x * .5f, 0);
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    private static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}

internal sealed class UnitBuffTimer : MaskableGraphic
{
    private float fraction;
    public void SetFraction(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, fraction)) return;
        fraction = value;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        const int segments = 24;
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.width, rect.height) * .5f;
        int count = Mathf.CeilToInt(fraction * segments);
        for (int i = 0; i <= count; i++)
        {
            float angle = Mathf.PI * .5f - Mathf.Min(i / (float)segments, fraction) * Mathf.PI * 2f;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(rect.center + direction * radius, color, Vector2.zero);
            mesh.AddVert(rect.center + direction * (radius - 1.25f), color, Vector2.zero);
            if (i == 0) continue;
            int n = i * 2;
            mesh.AddTriangle(n - 2, n, n - 1);
            mesh.AddTriangle(n, n + 1, n - 1);
        }
    }
}
