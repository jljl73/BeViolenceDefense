using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 좌상단 코어 HUD: 주인공 초상 + 코어 체력. 평소에는 초상이 대기 동작을 반복하고, 코어가 피해를 입으면 패널이 흔들리며 놀란 표정이 된다.
/// 배치는 LeftCoreHud.prefab(575×360 원본 좌표, 루트만 균일 배율)에 들어 있고, 이 컴포넌트는 값과 표정만 갱신한다.
/// VoxelDefensePrototype에 이 컴포넌트가 연결된 씬에서만 기존 코어 패널 대신 쓰인다.
/// </summary>
public sealed class LeftCoreHud : MonoBehaviour
{
    public enum Expression { Neutral, Happy, Surprised, Troubled, Pout }

    [Serializable]
    public sealed class FaceSet
    {
        public Expression expression;
        public Texture2D[] tiles = Array.Empty<Texture2D>();
    }

    [SerializeField] private RectTransform body;
    [SerializeField] private RawImage[] portraitTiles = Array.Empty<RawImage>();
    [SerializeField] private FaceSet[] faces = Array.Empty<FaceSet>();
    [Tooltip("무표정일 때 반복 재생하는 대기 프레임. 프레임마다 초상 타일 수만큼 이어 붙인 배열.")]
    [SerializeField] private Texture2D[] idleFrames = Array.Empty<Texture2D>();
    [SerializeField] private float idleFps = 16f;
    [Tooltip("선택. 초상 타일 위에 겹친 같은 배치의 타일. 대기 프레임 사이를 부드럽게 섞는 데 쓴다.")]
    [SerializeField] private RawImage[] portraitBlendTiles = Array.Empty<RawImage>();
    [SerializeField] private Graphic[] flashTargets = Array.Empty<Graphic>();
    [SerializeField] private TextMeshProUGUI coreText;
    [SerializeField] private RectTransform fillClip;
    [SerializeField] private RectTransform trailClip;
    [SerializeField] private Graphic[] fillTiles = Array.Empty<Graphic>();
    [SerializeField] private float clipWidth = 255f;

    [Header("Timing (unscaled seconds)")]
    [SerializeField] private float hitFaceTime = 0.9f;
    [SerializeField] private float cheerTime = 2.5f;
    [SerializeField] private float shakeTime = 0.35f;
    [SerializeField] private float shakePixels = 7f;
    [SerializeField] private float trailDelay = 0.35f;
    [SerializeField] private float trailSpeed = 0.6f;
    [SerializeField, Range(0f, 1f)] private float flashStrength = 0.55f;
    [SerializeField, Range(0f, 1f)] private float troubledBelow = 0.5f;
    [SerializeField, Range(0f, 1f)] private float dangerBelow = 0.25f;

    private int lastHealth = -1;
    private int maxHealth = 1;
    private float trailRatio = 1f;
    private float hitUntil;
    private float cheerUntil;
    private float shakeUntil;
    private float trailHoldUntil;
    private Vector2 bodyHome;
    private Expression shown = (Expression)(-1);
    private int shownIdleFrame = -1;
    private Color[] flashBase = Array.Empty<Color>();
    private string secondaryHex;

    private void Awake()
    {
        if (body != null)
            bodyHome = body.anchoredPosition;
        flashBase = new Color[flashTargets.Length];
        for (int i = 0; i < flashTargets.Length; i++)
            flashBase[i] = flashTargets[i] != null ? flashTargets[i].color : Color.white;
        secondaryHex = ColorUtility.ToHtmlStringRGB(UIPalette.TextSecondary);
        SetExpression(Expression.Neutral);
    }

    /// <summary>매 프레임 현재 코어 체력을 넘긴다. 값이 줄어들면 피격 반응을 재생한다.</summary>
    public void Refresh(int current, int max)
    {
        maxHealth = Mathf.Max(1, max);
        if (lastHealth >= 0 && current < lastHealth)
            PlayHit();
        if (current != lastHealth)
        {
            coreText.text = $"{current} <size=64%><color=#{secondaryHex}>/{maxHealth}</color></size>";
            if (lastHealth < 0)
                trailRatio = Mathf.Clamp01((float)current / maxHealth);
            lastHealth = current;
        }
    }

    /// <summary>라운드 정리 등 좋은 일이 있을 때 잠시 웃는 얼굴.</summary>
    public void Cheer() => cheerUntil = Time.unscaledTime + cheerTime;

    public void PlayHit()
    {
        float now = Time.unscaledTime;
        hitUntil = now + hitFaceTime;
        shakeUntil = now + shakeTime;
        trailHoldUntil = now + trailDelay;
        cheerUntil = 0f;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float ratio = lastHealth < 0 ? 1f : Mathf.Clamp01((float)lastHealth / maxHealth);

        if (trailRatio < ratio)
            trailRatio = ratio;
        else if (now >= trailHoldUntil)
            trailRatio = Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.unscaledDeltaTime);
        SetClip(fillClip, ratio);
        SetClip(trailClip, trailRatio);
        Color fillColor = ratio <= dangerBelow ? UIPalette.ButtonDanger : Color.white;
        foreach (Graphic tile in fillTiles)
            if (tile != null)
                tile.color = fillColor;

        float shake = now < shakeUntil ? (shakeUntil - now) / shakeTime : 0f;
        if (body != null)
            body.anchoredPosition = bodyHome + (shake > 0f
                ? new Vector2(Mathf.Sin(now * 90f), Mathf.Cos(now * 70f) * 0.5f) * shakePixels * shake
                : Vector2.zero);
        for (int i = 0; i < flashTargets.Length; i++)
            if (flashTargets[i] != null)
                flashTargets[i].color = Color.Lerp(flashBase[i], UIPalette.ButtonDanger, shake * flashStrength);

        Expression want =
            now < hitUntil ? Expression.Surprised :
            now < cheerUntil ? Expression.Happy :
            lastHealth == 0 || ratio <= troubledBelow ? Expression.Troubled :
            Expression.Neutral;
        SetExpression(want);
        if (want == Expression.Neutral)
            PlayIdle(now);
    }

    private void PlayIdle(float now)
    {
        int tiles = portraitTiles.Length;
        int frames = tiles > 0 ? idleFrames.Length / tiles : 0;
        if (frames == 0)
            return;
        float position = now * idleFps;
        int frame = Mathf.FloorToInt(position) % frames;
        bool blend = portraitBlendTiles.Length == tiles;
        if (frame != shownIdleFrame)
        {
            int next = (frame + 1) % frames;
            for (int i = 0; i < tiles; i++)
            {
                portraitTiles[i].texture = idleFrames[frame * tiles + i];
                if (blend)
                    portraitBlendTiles[i].texture = idleFrames[next * tiles + i];
            }
            shownIdleFrame = frame;
        }
        if (blend)
            SetBlend(position - Mathf.Floor(position));
    }

    private void SetBlend(float alpha)
    {
        foreach (RawImage tile in portraitBlendTiles)
        {
            tile.enabled = alpha > 0f;
            tile.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void SetClip(RectTransform clip, float ratio)
    {
        if (clip == null)
            return;
        clip.gameObject.SetActive(ratio > 0f);
        clip.sizeDelta = new Vector2(clipWidth * ratio, clip.sizeDelta.y);
    }

    private void SetExpression(Expression expression)
    {
        if (expression == shown)
            return;
        FaceSet set = Array.Find(faces, f => f.expression == expression);
        if (set == null || set.tiles.Length != portraitTiles.Length)
            return;
        for (int i = 0; i < portraitTiles.Length; i++)
            portraitTiles[i].texture = set.tiles[i];
        shown = expression;
        shownIdleFrame = -1;
        SetBlend(0f);
    }
}
