using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class SpellCastMarkers
{
    private const float Duration = 0.38f;
    private const float BurstDuration = 0.18f;
    private const float WorldDiameter = 0.70f;

    private static readonly Vector2[] Sword =
    {
        new(-0.28f, -0.46f), new(-0.12f, -0.52f), new(0.07f, -0.30f), new(0.23f, -0.37f),
        new(0.34f, -0.24f), new(0.18f, -0.15f), new(0.40f, 0.16f), new(0.46f, 0.51f),
        new(0.10f, 0.40f), new(-0.17f, 0.02f), new(-0.32f, 0.11f), new(-0.42f, -0.04f),
        new(-0.27f, -0.14f), new(-0.46f, -0.36f)
    };
    private static readonly Vector2[] Chevron =
    {
        new(-0.45f, -0.24f), new(0f, 0.14f), new(0.45f, -0.24f),
        new(0.45f, -0.02f), new(0f, 0.36f), new(-0.45f, -0.02f)
    };
    private static readonly Vector2[] Shield =
    {
        new(0f, 0.51f), new(0.46f, 0.34f), new(0.40f, -0.12f), new(0.22f, -0.38f),
        new(0f, -0.51f), new(-0.22f, -0.38f), new(-0.40f, -0.12f), new(-0.46f, 0.34f)
    };

    private readonly Camera camera;
    private readonly Transform board;
    private readonly int sortingOrder;
    private readonly List<Marker> playing = new();
    private readonly Stack<Marker> available = new();
    private readonly Dictionary<DefenseBattle.Spell, Sprite> icons = new();
    private readonly Sprite burst;

    public SpellCastMarkers(Camera camera, Transform board, int sortingOrder)
    {
        this.camera = camera;
        this.board = board;
        this.sortingOrder = sortingOrder;
        foreach (DefenseBattle.Spell spell in System.Enum.GetValues(typeof(DefenseBattle.Spell)))
        {
            Color tint = SpellEffects.SpellColor(spell);
            icons.Add(spell, MakeSprite(spell.ToString(), p => IconPixel(p, spell, tint)));
        }
        burst = MakeSprite("Spell Burst", BurstPixel);
    }

    public void Play(DefenseBattle.Spell spell, Transform unit, Vector3 head, float hudClearance)
    {
        Marker marker = available.Count > 0 ? available.Pop() : CreateMarker();
        marker.Anchor = unit;
        marker.HeadOffset = head - unit.position;
        marker.Head = head;
        marker.HudClearance = hudClearance;
        marker.Tint = SpellEffects.SpellColor(spell);
        marker.Age = 0f;
        marker.Icon.sprite = icons[spell];
        marker.Burst.sprite = burst;
        marker.Root.gameObject.SetActive(true);
        playing.Add(marker);
    }

    public void Advance(float deltaTime)
    {
        for (int i = playing.Count - 1; i >= 0; i--)
        {
            Marker marker = playing[i];
            marker.Age += deltaTime;
            if (marker.Age < Duration)
                continue;

            marker.Anchor = null;
            marker.Root.gameObject.SetActive(false);
            playing.RemoveAt(i);
            available.Push(marker);
        }

        // 머리 위치는 월드에서 추적하고, 표시는 카메라 앞에 그려 모델과 벽에 가려지지 않게 한다.
        float viewHeight = 2f * camera.orthographicSize;
        float diameter = Mathf.Clamp(WorldDiameter / viewHeight, 0.037f, 0.065f);
        float worldDiameter = diameter * viewHeight;
        for (int i = 0; i < playing.Count; i++)
        {
            Marker marker = playing[i];
            if (marker.Anchor != null)
                marker.Head = marker.Anchor.position + marker.HeadOffset;

            Vector3 projected = camera.WorldToViewportPoint(marker.Head);
            bool visible = projected.z > 0f && projected.x >= 0f && projected.x <= 1f &&
                projected.y >= 0f && projected.y <= 1f;
            marker.Root.gameObject.SetActive(visible);
            if (!visible)
                continue;

            float t = marker.Age / Duration;
            float pop = Mathf.Sin(Mathf.PI * Mathf.Clamp01(marker.Age / 0.12f));
            float iconScale = Mathf.Lerp(1f, 0.82f, t) + 0.24f * pop;
            float burstProgress = Mathf.Clamp01(marker.Age / BurstDuration);
            float expansion = 1f - (1f - burstProgress) * (1f - burstProgress);
            float centerY = projected.y + marker.HudClearance + diameter * iconScale * 0.395f + 0.002f;
            float depth = camera.nearClipPlane + 0.12f + Mathf.Min(0.32f, projected.z * 0.007f);
            Vector3 center = camera.ViewportToWorldPoint(new Vector3(projected.x, centerY, depth));
            marker.Root.SetPositionAndRotation(center, camera.transform.rotation);
            marker.Icon.transform.localScale = Vector3.one * (worldDiameter * iconScale);
            marker.Burst.transform.localScale = Vector3.one * (worldDiameter * Mathf.Lerp(1.03f, 1.75f, expansion));

            float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 1f, t));
            Color neutral = Color.white;
            neutral.a = opacity;
            marker.Icon.color = neutral;
            Color glow = Color.Lerp(marker.Tint, UIPalette.TextPrimary, 0.65f);
            glow.a = 1f - burstProgress * burstProgress;
            marker.Burst.color = glow;
        }
    }

    private Marker CreateMarker()
    {
        var root = new GameObject("Spell Cast Marker").transform;
        root.SetParent(board, false);
        return new Marker
        {
            Root = root,
            Burst = CreateLayer("Burst", root, sortingOrder),
            Icon = CreateLayer("Glyph", root, sortingOrder + 1)
        };
    }

    private static SpriteRenderer CreateLayer(string name, Transform root, int order)
    {
        var layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(root, false);
        layer.sortingOrder = order;
        layer.shadowCastingMode = ShadowCastingMode.Off;
        layer.receiveShadows = false;
        return layer;
    }

    private static Color IconPixel(Vector2 p, DefenseBattle.Spell spell, Color tint)
    {
        float radius = p.magnitude;
        if (radius > 0.79f)
            return Color.clear;
        if (radius > 0.72f)
            return UIPalette.Background;
        if (radius > 0.62f)
            return Color.Lerp(tint, UIPalette.TextPrimary, Mathf.InverseLerp(-0.5f, 0.7f, p.y) * 0.65f);

        Color plate = Color.Lerp(UIPalette.Background, tint, 0.12f + Mathf.Max(0f, p.y) * 0.3f);
        bool glyph = spell switch
        {
            DefenseBattle.Spell.AttackBuff => Inside(Sword, p),
            DefenseBattle.Spell.MoveSpeedBuff => Inside(Chevron, p + Vector2.up * 0.26f) ||
                Inside(Chevron, p - Vector2.up * 0.13f),
            DefenseBattle.Spell.ArmorBuff => Inside(Shield, p),
            _ => (Mathf.Abs(p.x) <= 0.15f && Mathf.Abs(p.y) <= 0.47f) ||
                (Mathf.Abs(p.y) <= 0.15f && Mathf.Abs(p.x) <= 0.47f)
        };
        if (!glyph)
            return plate;

        float highlight = Mathf.Lerp(0.16f, 0.86f, Mathf.InverseLerp(-0.42f, 0.46f, p.y));
        if (spell == DefenseBattle.Spell.ArmorBuff && p.x > 0.035f)
            highlight *= 0.5f;
        return Color.Lerp(tint, UIPalette.TextPrimary, highlight);
    }

    private static Color BurstPixel(Vector2 p)
    {
        float radius = p.magnitude;
        float halo = Mathf.Exp(-Mathf.Abs(radius - 0.63f) * 26f) * 0.85f;
        float angle = Mathf.Atan2(p.y, p.x);
        float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f + 0.35f)), 28f) *
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.68f, radius)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.99f, radius)));
        return new Color(1f, 1f, 1f, Mathf.Max(halo, rays));
    }

    private static bool Inside(Vector2[] shape, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = shape.Length - 1; i < shape.Length; j = i++)
        {
            Vector2 a = shape[i];
            Vector2 b = shape[j];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    private static Sprite MakeSprite(string name, System.Func<Vector2, Color> sample)
    {
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = sample(new Vector2(x + 0.5f, y + 0.5f) * (2f / size) - Vector2.one);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
    }

    public void ReleaseSprites()
    {
        foreach (Sprite icon in icons.Values)
            DestroySprite(icon);
        DestroySprite(burst);
    }

    private static void DestroySprite(Sprite sprite)
    {
        DefenseBoardView.Release(sprite.texture);
        DefenseBoardView.Release(sprite);
    }

    private sealed class Marker
    {
        public Transform Root;
        public Transform Anchor;
        public SpriteRenderer Icon;
        public SpriteRenderer Burst;
        public Vector3 Head;
        public Vector3 HeadOffset;
        public float HudClearance;
        public Color Tint;
        public float Age;
    }
}
