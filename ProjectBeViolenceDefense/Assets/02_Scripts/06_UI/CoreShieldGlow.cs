using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
internal sealed class CoreShieldGlow : MaskableGraphic
{
    private const float BurstDuration = 0.22f;
    private const int Padding = 20;

    private Graphic[] gaugeTiles;
    private RenderTexture glowTexture;
    private bool charged;
    private int shownBreaks;
    private float burstRemaining;

    public override Texture mainTexture => glowTexture != null ? glowTexture : base.mainTexture;

    public void UseGaugeArt(Graphic[] tiles) => gaugeTiles = tiles;

    public void Refresh(bool showCharge, int breakCount, float deltaTime)
    {
        if (charged == showCharge && shownBreaks == breakCount && burstRemaining <= 0f)
            return;

        charged = showCharge;
        burstRemaining = shownBreaks != breakCount ? BurstDuration : Mathf.Max(0f, burstRemaining - deltaTime);
        shownBreaks = breakCount;
        enabled = charged || burstRemaining > 0f;
        if (!enabled)
            return;

        if (glowTexture == null && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
        {
            CreateGlowTexture();
            SetMaterialDirty();
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        if (glowTexture == null)
            return;

        Rect bounds = GlowBounds(rectTransform.rect, 0f);
        if (charged)
            DrawGlow(vertices, bounds, 0.88f, Color.Lerp(UIPalette.CoreShield, UIPalette.TextPrimary, 0.12f));
        if (burstRemaining > 0f)
        {
            float life = burstRemaining / BurstDuration;
            DrawGlow(vertices, GlowBounds(rectTransform.rect, 12f * (1f - life * life)),
                life * life, Color.Lerp(UIPalette.CoreShield, UIPalette.TextPrimary, life * 0.8f));
        }
    }

    private void CreateGlowTexture()
    {
        Rect gauge = rectTransform.rect;
        Rect bounds = GlowBounds(gauge, 0f);
        int width = Mathf.CeilToInt(bounds.width);
        int height = Mathf.CeilToInt(bounds.height);
        var material = new Material(Resources.Load<Shader>("CoreShieldGlow"));
        var mask = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var scratch = RenderTexture.GetTemporary(mask.descriptor);
        var blurred = RenderTexture.GetTemporary(mask.descriptor);
        mask.filterMode = scratch.filterMode = blurred.filterMode = FilterMode.Bilinear;
        mask.wrapMode = scratch.wrapMode = blurred.wrapMode = TextureWrapMode.Clamp;

        glowTexture = new RenderTexture(mask.descriptor)
        {
            name = "Core Health Glow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var previousTarget = RenderTexture.active;
        try
        {
            Graphics.Blit(Texture2D.blackTexture, mask);
            material.SetVector("_GaugeRect", ToUv(gauge, bounds));
            material.SetFloat("_UseFillColor", gaugeTiles != null ? 1f : 0f);
            if (gaugeTiles == null)
                AddTile(Texture2D.whiteTexture, gauge, new Rect(0f, 0f, 1f, 1f));
            else
            {
                // 마스크는 체력이 가득 찬 게이지와 같은 타일 위치와 UV를 사용한다.
                var corners = new Vector3[4];
                foreach (Graphic graphic in gaugeTiles)
                {
                    if (graphic is not RawImage { texture: { } tile } image)
                        continue;
                    image.rectTransform.GetWorldCorners(corners);
                    Vector3 min = rectTransform.InverseTransformPoint(corners[0]);
                    Vector3 max = rectTransform.InverseTransformPoint(corners[2]);
                    AddTile(tile, Rect.MinMaxRect(min.x, min.y, max.x, max.y), image.uvRect);
                }
            }

            material.SetVector("_BlurStep", new Vector4(1f / width, 0f));
            Graphics.Blit(mask, scratch, material, 1);
            material.SetVector("_BlurStep", new Vector4(0f, 1f / height));
            Graphics.Blit(scratch, blurred, material, 1);
            material.SetTexture("_GaugeMask", mask);
            Graphics.Blit(blurred, glowTexture, material, 2);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            RenderTexture.ReleaseTemporary(mask);
            RenderTexture.ReleaseTemporary(scratch);
            RenderTexture.ReleaseTemporary(blurred);
            Destroy(material);
        }

        void AddTile(Texture tile, Rect area, Rect uv)
        {
            material.SetTexture("_TileTex", tile);
            material.SetVector("_TileRect", ToUv(area, bounds));
            material.SetVector("_TileUv", new Vector4(uv.x, uv.y, uv.width, uv.height));
            Graphics.Blit(mask, scratch, material, 0);
            (mask, scratch) = (scratch, mask);
        }
    }

    private static Vector4 ToUv(Rect area, Rect bounds) => new(
        (area.x - bounds.x) / bounds.width, (area.y - bounds.y) / bounds.height,
        area.width / bounds.width, area.height / bounds.height);

    private static Rect GlowBounds(Rect gauge, float spread)
    {
        float margin = Padding + spread;
        return Rect.MinMaxRect(gauge.xMin - margin, gauge.yMin - margin, gauge.xMax + margin, gauge.yMax + margin);
    }

    private static void DrawGlow(VertexHelper vertices, Rect bounds, float opacity, Color tint)
    {
        int start = vertices.currentVertCount;
        Color32 glowColor = new Color(tint.r, tint.g, tint.b, opacity);
        vertices.AddVert(bounds.min, glowColor, Vector2.zero);
        vertices.AddVert(new Vector2(bounds.xMin, bounds.yMax), glowColor, Vector2.up);
        vertices.AddVert(bounds.max, glowColor, Vector2.one);
        vertices.AddVert(new Vector2(bounds.xMax, bounds.yMin), glowColor, Vector2.right);
        vertices.AddTriangle(start, start + 1, start + 2);
        vertices.AddTriangle(start + 2, start + 3, start);
    }

    protected override void OnDestroy()
    {
        if (glowTexture != null)
        {
            glowTexture.Release();
            Destroy(glowTexture);
        }
        base.OnDestroy();
    }
}
