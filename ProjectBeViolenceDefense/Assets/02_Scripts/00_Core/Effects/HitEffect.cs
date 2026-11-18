using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 공격이 맞는 순간 대상 위치에 잠깐 터지는 타격 이펙트.
/// 별도 텍스처 없이 코드로 만든 스프라이트(섬광·충격파 고리·파편)를 카메라를 향해 띄워 그린다.
/// </summary>
internal sealed class HitEffect : MonoBehaviour
{
    private const float Duration = 0.22f;
    private const float FlashDuration = 0.065f;
    private const float RingDuration = 0.15f;
    private const float FlashSize = 0.62f;
    private const float RingSize = 0.85f;
    private const int SparkCount = 5;
    private const float SparkSpeed = 3.2f;
    private const float SparkDrag = 9f;
    private const float SparkGravity = 6f;
    // 유닛 모델 안으로 파묻히지 않도록 카메라 쪽으로 당겨 그린다.
    private const float TowardCamera = 0.6f;
    private const int SortingOrder = 50;

    private static Sprite flashSprite;
    private static Sprite ringSprite;
    private static Sprite sparkSprite;

    private readonly SpriteRenderer[] sparks = new SpriteRenderer[SparkCount];
    private readonly Vector2[] sparkVelocity = new Vector2[SparkCount];
    private SpriteRenderer flash;
    private SpriteRenderer ring;
    private Transform view;
    private Color tint;
    private float colorEmphasis;
    private float age;

    public bool IsPlaying => gameObject.activeSelf;

    public static HitEffect Create(Transform parent, Camera camera)
    {
        EnsureSprites();
        var effect = new GameObject("Hit Effect").AddComponent<HitEffect>();
        effect.transform.SetParent(parent, false);
        effect.view = camera.transform;
        effect.ring = effect.Layer("Ring", ringSprite, 0);
        for (int i = 0; i < SparkCount; i++)
            effect.sparks[i] = effect.Layer("Spark", sparkSprite, 1);
        effect.flash = effect.Layer("Flash", flashSprite, 2);
        effect.gameObject.SetActive(false);
        return effect;
    }

    public void Play(Vector3 worldPosition, Color tint, float size = 1f, float colorEmphasis = 0f)
    {
        this.tint = tint;
        this.colorEmphasis = colorEmphasis;
        age = 0f;
        transform.SetPositionAndRotation(worldPosition - view.forward * TowardCamera, view.rotation);
        transform.localScale = Vector3.one * size;
        flash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));

        float start = Random.Range(0f, 360f);
        for (int i = 0; i < SparkCount; i++)
        {
            float angle = (start + i * 360f / SparkCount + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
            sparkVelocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (SparkSpeed * Random.Range(0.7f, 1.15f));
            sparks[i].transform.localPosition = Vector3.zero;
        }

        gameObject.SetActive(true);
        Animate(0f);
    }

    public void Advance(float deltaTime)
    {
        transform.rotation = view.rotation;
        if (deltaTime <= 0f)
            return;

        age += deltaTime;
        if (age >= Duration)
        {
            gameObject.SetActive(false);
            return;
        }

        Animate(deltaTime);
    }

    private void Animate(float deltaTime)
    {
        float t = age / Duration;
        float burst = 1f - (1f - t) * (1f - t);
        float fade = 1f - t;

        float flashFade = Mathf.Clamp01(1f - age / FlashDuration);
        flash.transform.localScale = Vector3.one * (FlashSize * Mathf.Lerp(0.4f, 1f, flashFade));
        flash.color = WithAlpha(Color.Lerp(Color.white, tint, colorEmphasis), flashFade * flashFade);

        ring.transform.localScale = Vector3.one * (RingSize * Mathf.Lerp(0.3f, 1f, burst));
        float ringFade = Mathf.Clamp01(1f - age / RingDuration);
        ring.color = WithAlpha(tint, ringFade * 0.65f);

        float drag = Mathf.Exp(-SparkDrag * deltaTime);
        float sparkSize = Mathf.Lerp(0.16f, 0.04f, t);
        Color sparkColor = WithAlpha(Color.Lerp(Color.white, tint, Mathf.Lerp(0.45f, 1f, colorEmphasis)), fade);
        for (int i = 0; i < SparkCount; i++)
        {
            Vector2 velocity = sparkVelocity[i] * drag;
            velocity.y -= SparkGravity * deltaTime;
            sparkVelocity[i] = velocity;

            Transform spark = sparks[i].transform;
            spark.localPosition += (Vector3)(velocity * deltaTime);
            spark.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            spark.localScale = new Vector3(sparkSize * 2.6f, sparkSize, 1f);
            sparks[i].color = sparkColor;
        }
    }

    private SpriteRenderer Layer(string name, Sprite sprite, int order)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(transform, false);
        renderer.sprite = sprite;
        renderer.sortingOrder = SortingOrder + order;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void EnsureSprites()
    {
        if (flashSprite == null)
        {
            // 십자 섬광 + 중심 광채
            flashSprite = MakeSprite("Hit Flash", 64, (u, v, r) =>
            {
                float cross = Mathf.Max(
                    Mathf.Exp(-Mathf.Abs(u) * 16f) * (1f - Mathf.Abs(v)),
                    Mathf.Exp(-Mathf.Abs(v) * 16f) * (1f - Mathf.Abs(u)));
                float glow = Mathf.Pow(Mathf.Clamp01(1f - r), 3f);
                return Mathf.Clamp01(cross + glow);
            });
        }

        if (ringSprite == null)
            ringSprite = MakeSprite("Hit Ring", 64, (u, v, r) => Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.1f));

        if (sparkSprite == null)
            sparkSprite = MakeSprite("Hit Spark", 32, (u, v, r) => Mathf.Clamp01((1f - Mathf.Abs(u) - Mathf.Abs(v)) * 3f));
    }

    private static Sprite MakeSprite(string name, int size, System.Func<float, float, float, float> alpha)
    {
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
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float a = alpha(u, v, Mathf.Sqrt(u * u + v * v));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
