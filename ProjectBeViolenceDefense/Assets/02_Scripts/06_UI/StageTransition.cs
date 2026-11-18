using System;
using System.Collections;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 스테이지 입장 연출(타이틀 카드).
///  1. 덮기: 금색·크림·자주색 사선 띠가 차례로 오른쪽에서 밀려와 화면을 덮는다.
///  2. 타이틀: 왼쪽에 스테이지 그림이 사선 창으로 밀려 들어오고, 오른쪽에 번호가 솟아오른 뒤 리본이 펼쳐지며 이름이 뜬다.
///  3. 덮인 동안 onCovered 에서 스테이지를 만든다(생성 때 멈칫하는 프레임을 가린다).
///  4. 걷기: 자주색 띠가 타이틀을 실은 채 왼쪽으로 빠지고 크림·금색 띠가 뒤따르며 보드가 드러난다.
/// UI Canvas 맨 위에 두며, 배치는 StageTransition.prefab에 있다.
public sealed class StageTransition : MonoBehaviour
{
    [Header("Bands (기울어진 띠, 아래에서 위 순서)")]
    [SerializeField] private RectTransform bandGold;
    [SerializeField] private RectTransform bandCream;
    [SerializeField] private RectTransform bandMain;

    [Header("Backdrop")]
    [SerializeField] private CanvasGroup backdrop;
    [SerializeField] private RawImage stripes;
    [SerializeField] private CanvasGroup halftone;
    [SerializeField] private RectTransform ghostNumber;
    [SerializeField] private TextMeshProUGUI ghostNumberText;

    [Header("Stage art (왼쪽 사선 창)")]
    [SerializeField] private RectTransform artBand;
    [SerializeField] private RectTransform artEdgeGold;
    [SerializeField] private RectTransform artEdgeCream;
    [SerializeField] private RawImage art;
    [Tooltip("스테이지 번호 순서(1번부터)")]
    [SerializeField] private Texture[] stageArt = Array.Empty<Texture>();

    [Header("Title (오른쪽)")]
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private CanvasGroup labelGroup;
    [SerializeField] private RectTransform number;
    [SerializeField] private TextMeshProUGUI numberText;
    [SerializeField] private RectTransform line;
    [SerializeField] private RectTransform ribbon;
    [SerializeField] private CanvasGroup ribbonGroup;
    [SerializeField] private TextMeshProUGUI stageName;
    [SerializeField] private CanvasGroup nameGroup;
    [SerializeField] private RectTransform ring;
    [SerializeField] private CanvasGroup ringGroup;
    [SerializeField] private RectTransform[] sparkles = Array.Empty<RectTransform>();
    [SerializeField] private RectTransform[] ticks = Array.Empty<RectTransform>();

    [Header("Timing (초)")]
    [SerializeField] private float coverTime = 0.42f;
    [SerializeField] private float bandStagger = 0.07f;
    [SerializeField] private float titleTime = 1f;
    [SerializeField] private float holdTime = 0.55f;
    [SerializeField] private float revealTime = 0.5f;
    [SerializeField] private float revealStagger = 0.045f;
    [Tooltip("띠가 화면 밖에서 기다리는 거리(기준 해상도 px)")]
    [SerializeField] private float offscreen = 3500f;
    [Tooltip("녹화·캡처용. 켜면 Time.timeScale 과 captureFramerate 를 따른다.")]
    [SerializeField] private bool useScaledTime;

    private CompositeMotionHandle motions;
    private Vector2 artHome, edgeGoldHome, edgeCreamHome, ghostHome, numberHome;
    private Vector2[] tickHome = Array.Empty<Vector2>();
    private bool homesSet;

    public bool IsPlaying { get; private set; }
    public bool UseScaledTime { get => useScaledTime; set => useScaledTime = value; }

    private IMotionScheduler Scheduler => useScaledTime ? MotionScheduler.Update : MotionScheduler.UpdateIgnoreTimeScale;
    private float Delta => useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;

    private void RememberHomes()
    {
        if (homesSet)
            return;
        homesSet = true;
        motions = new CompositeMotionHandle();
        artHome = artBand.anchoredPosition;
        edgeGoldHome = artEdgeGold.anchoredPosition;
        edgeCreamHome = artEdgeCream.anchoredPosition;
        ghostHome = ghostNumber.anchoredPosition;
        numberHome = number.anchoredPosition;
        tickHome = new Vector2[ticks.Length];
        for (int i = 0; i < ticks.Length; i++)
            tickHome[i] = ticks[i].anchoredPosition;
    }

    /// startCovered: 다시 하기처럼 씬이 새로 열리자마자 들어갈 때는 덮인 상태에서 시작한다.
    public void Play(int stageNumber, string displayName, Action onCovered, bool startCovered = false)
    {
        RememberHomes();
        StopAllCoroutines();
        motions.Cancel();
        IsPlaying = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        string digits = stageNumber.ToString("00");
        numberText.text = ghostNumberText.text = digits;
        label.text = GameText.Get("stage_transition.label");
        stageName.text = displayName;
        if (stageNumber >= 1 && stageNumber <= stageArt.Length && stageArt[stageNumber - 1] != null)
            art.texture = stageArt[stageNumber - 1];
        StartCoroutine(Run(onCovered, startCovered));
    }

    private void OnDisable()
    {
        if (homesSet)
            motions.Cancel();
    }

    private void Update()
    {
        // 바탕 줄무늬는 천천히 흐른다.
        Rect uv = stripes.uvRect;
        uv.x -= Delta * 0.06f;
        stripes.uvRect = uv;
    }

    private IEnumerator Run(Action onCovered, bool startCovered)
    {
        HideTitle();
        var away = new Vector2(offscreen, 0f);
        if (startCovered)
            bandGold.anchoredPosition = bandCream.anchoredPosition = bandMain.anchoredPosition = Vector2.zero;
        else
        {
            bandGold.anchoredPosition = bandCream.anchoredPosition = bandMain.anchoredPosition = away;
            Slide(bandGold, away, Vector2.zero, coverTime, 0f, Ease.OutCubic);
            Slide(bandCream, away, Vector2.zero, coverTime, bandStagger, Ease.OutCubic);
            Slide(bandMain, away, Vector2.zero, coverTime, bandStagger * 2f, Ease.OutCubic);
            // 자주색 띠가 화면을 거의 덮었을 때 타이틀을 시작한다.
            yield return Wait(coverTime * 0.72f + bandStagger * 2f);
        }

        ShowTitle();
        yield return Wait(titleTime);

        // 타이틀이 자리를 잡은 뒤 스테이지를 만든다. 생성 뒤 두 프레임을 더 기다려 첫 렌더가 끝나게 한다.
        onCovered?.Invoke();
        yield return null;
        yield return null;
        yield return Wait(holdTime);

        // 화면 구석에 맞춰 둔 망점은 띠가 움직이기 시작할 때 지운다(잘린 면이 보이지 않게).
        Fade(halftone, 1f, 0f, 0.15f, 0f);
        var gone = new Vector2(-offscreen, 0f);
        Slide(bandMain, Vector2.zero, gone, revealTime, 0f, Ease.InCubic);
        Slide(bandCream, Vector2.zero, gone, revealTime, revealStagger, Ease.InCubic);
        Slide(bandGold, Vector2.zero, gone, revealTime, revealStagger * 2f, Ease.InCubic);
        yield return Wait(revealTime + revealStagger * 2f);

        motions.Cancel();
        IsPlaying = false;
        gameObject.SetActive(false);
    }

    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Delta)
            yield return null;
    }

    private void HideTitle()
    {
        backdrop.alpha = 0f;
        halftone.alpha = 1f;
        artBand.anchoredPosition = artHome + ArtAway;
        artEdgeGold.anchoredPosition = edgeGoldHome + ArtAway;
        artEdgeCream.anchoredPosition = edgeCreamHome + ArtAway;
        labelGroup.alpha = ribbonGroup.alpha = nameGroup.alpha = ringGroup.alpha = 0f;
        number.anchoredPosition = numberHome + NumberBelow;
        line.localScale = new Vector3(0f, 1f, 1f);
        foreach (RectTransform s in sparkles)
            s.localScale = Vector3.zero;
        foreach (RectTransform t in ticks)
            t.localScale = Vector3.zero;
    }

    private static readonly Vector2 ArtAway = new(-1900f, 0f);
    private static readonly Vector2 NumberBelow = new(0f, -300f);

    private void ShowTitle()
    {
        // 바탕: 줄무늬·망점·큰 숫자가 떠오르고, 큰 숫자는 천천히 옆으로 흐른다.
        Fade(backdrop, 0f, 1f, 0.35f, 0f);
        Slide(ghostNumber, ghostHome + new Vector2(160f, 0f), ghostHome, 3.2f, 0f, Ease.OutCubic);

        // 스테이지 그림 창이 왼쪽에서 밀려 들어오고, 가장자리 선 두 줄이 조금씩 늦게 따라온다.
        Slide(artBand, artHome + ArtAway, artHome, 0.6f, 0f, Ease.OutQuart);
        Slide(artEdgeGold, edgeGoldHome + ArtAway, edgeGoldHome, 0.6f, 0.06f, Ease.OutQuart);
        Slide(artEdgeCream, edgeCreamHome + ArtAway, edgeCreamHome, 0.6f, 0.13f, Ease.OutQuart);
        LMotion.Create(Vector3.one * 1.14f, Vector3.one, 3.4f).WithEase(Ease.OutCubic).WithScheduler(Scheduler)
            .BindToLocalScale(art.rectTransform).AddTo(motions);

        // 머리글은 자간이 좁혀지며 나타난다.
        Fade(labelGroup, 0f, 1f, 0.25f, 0.2f);
        LMotion.Create(70f, 14f, 0.6f).WithEase(Ease.OutCubic).WithDelay(0.2f).WithScheduler(Scheduler)
            .Bind(label, (v, t) => t.characterSpacing = v).AddTo(motions);

        // 번호는 창 아래에서 솟아오른다.
        Slide(number, numberHome + NumberBelow, numberHome, 0.5f, 0.28f, Ease.OutBack);

        LMotion.Create(0f, 1f, 0.4f).WithEase(Ease.OutCubic).WithDelay(0.42f).WithScheduler(Scheduler)
            .Bind(line, (v, t) => t.localScale = new Vector3(v, 1f, 1f)).AddTo(motions);

        // 리본이 가운데에서 펼쳐지고, 이름이 뒤따른다.
        Fade(ribbonGroup, 0f, 1f, 0.12f, 0.52f);
        LMotion.Create(new Vector3(0.15f, 0.8f, 1f), Vector3.one, 0.45f).WithEase(Ease.OutBack).WithDelay(0.52f)
            .WithScheduler(Scheduler).BindToLocalScale(ribbon).AddTo(motions);
        Fade(nameGroup, 0f, 1f, 0.2f, 0.72f);
        LMotion.Create(Vector3.one * 1.25f, Vector3.one, 0.3f).WithEase(Ease.OutCubic).WithDelay(0.72f)
            .WithScheduler(Scheduler).BindToLocalScale(stageName.rectTransform).AddTo(motions);

        // 리본이 펼쳐질 때 링이 퍼져 나간다.
        // (시작 값이 예약 즉시 적용되므로, 진행도 0 에서는 숨긴 채로 둔다.)
        LMotion.Create(0f, 1f, 0.55f).WithDelay(0.56f).WithScheduler(Scheduler)
            .Bind(ringGroup, (p, g) => g.alpha = p <= 0f ? 0f : 0.6f * (1f - p)).AddTo(motions);
        LMotion.Create(Vector3.one * 0.35f, Vector3.one * 1.9f, 0.55f).WithEase(Ease.OutCubic).WithDelay(0.56f)
            .WithScheduler(Scheduler).BindToLocalScale(ring).AddTo(motions);

        // 반짝이는 하나씩 돌면서 튀어나온 뒤 계속 깜빡인다.
        for (int i = 0; i < sparkles.Length; i++)
        {
            RectTransform s = sparkles[i];
            float delay = 0.7f + 0.07f * i;
            LMotion.Create(Vector3.zero, Vector3.one, 0.35f).WithEase(Ease.OutBack).WithDelay(delay).WithScheduler(Scheduler)
                .WithOnComplete(() => LMotion.Create(Vector3.one, Vector3.one * 0.62f, 0.55f + 0.1f * (s.GetSiblingIndex() % 3))
                    .WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(Scheduler).BindToLocalScale(s).AddTo(motions))
                .BindToLocalScale(s).AddTo(motions);
            LMotion.Create(-90f, 0f, 0.5f).WithEase(Ease.OutCubic).WithDelay(delay).WithScheduler(Scheduler)
                .BindToLocalEulerAnglesZ(s).AddTo(motions);
        }

        // 구석의 빗금 표식은 차례로 박힌다.
        for (int i = 0; i < ticks.Length; i++)
        {
            float delay = 0.45f + 0.045f * i;
            LMotion.Create(Vector3.zero, Vector3.one, 0.22f).WithEase(Ease.OutBack).WithDelay(delay).WithScheduler(Scheduler)
                .BindToLocalScale(ticks[i]).AddTo(motions);
            Slide(ticks[i], tickHome[i] + new Vector2(40f, 0f), tickHome[i], 0.3f, delay, Ease.OutCubic);
        }
    }

    private void Slide(RectTransform rect, Vector2 from, Vector2 to, float time, float delay, Ease ease) =>
        LMotion.Create(from, to, time).WithEase(ease).WithDelay(delay).WithScheduler(Scheduler).BindToAnchoredPosition(rect).AddTo(motions);

    private void Fade(CanvasGroup group, float from, float to, float time, float delay) =>
        LMotion.Create(from, to, time).WithDelay(delay).WithScheduler(Scheduler).BindToAlpha(group).AddTo(motions);
}
