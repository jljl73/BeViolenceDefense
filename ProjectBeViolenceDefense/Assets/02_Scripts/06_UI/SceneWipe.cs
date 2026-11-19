using System.Collections;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// 씬을 넘길 때의 모션그래픽 와이프(스테이지 입장 연출과 같은 언어).
/// 금색·크림·자주색 사선 띠가 차례로 화면을 덮고, 가운데 리본에 문구가 톡 튀어나온다. 덮인 동안 다음 씬을 불러오고,
/// 다 불러오면 자주색 띠가 리본을 실은 채 왼쪽으로 빠지며 새 씬이 드러난다. 자기 캔버스를 갖고 씬을 넘어 살아남는다.
/// 배치는 SceneWipe.prefab에 있다.
public sealed class SceneWipe : MonoBehaviour
{
    [SerializeField] private RectTransform bandGold;
    [SerializeField] private RectTransform bandCream;
    [SerializeField] private RectTransform bandMain;
    [SerializeField] private RawImage stripes;
    [SerializeField] private CanvasGroup title;
    [SerializeField] private RectTransform ribbon;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI caption;
    [SerializeField] private RectTransform[] sparkles = System.Array.Empty<RectTransform>();

    [SerializeField] private float coverTime = 0.42f;
    [SerializeField] private float bandStagger = 0.07f;
    [SerializeField] private float holdTime = 0.7f;
    [SerializeField] private float revealTime = 0.5f;
    [SerializeField] private float offscreen = 3500f;

    private static readonly IMotionScheduler Clock = MotionScheduler.UpdateIgnoreTimeScale;

    /// 와이프를 띄우고 sceneName 으로 넘어간다. labelKey/captionKey 는 GameText 키.
    public static void Go(SceneWipe prefab, string sceneName, string labelKey, string captionKey)
    {
        var wipe = Instantiate(prefab);
        DontDestroyOnLoad(wipe.gameObject);
        wipe.StartCoroutine(wipe.Run(sceneName, labelKey, captionKey));
    }

    private IEnumerator Run(string sceneName, string labelKey, string captionKey)
    {
        label.text = GameText.Get(labelKey);
        caption.text = GameText.Get(captionKey);
        title.alpha = 0f;
        foreach (RectTransform s in sparkles)
            s.localScale = Vector3.zero;

        var away = new Vector2(offscreen, 0f);
        bandGold.anchoredPosition = bandCream.anchoredPosition = bandMain.anchoredPosition = away;
        Slide(bandGold, away, Vector2.zero, coverTime, 0f, Ease.OutCubic);
        Slide(bandCream, away, Vector2.zero, coverTime, bandStagger, Ease.OutCubic);
        Slide(bandMain, away, Vector2.zero, coverTime, bandStagger * 2f, Ease.OutCubic);
        yield return new WaitForSecondsRealtime(coverTime * 0.75f + bandStagger * 2f);

        // 리본이 가운데에서 펼쳐지고 반짝이가 튀어나온다.
        LMotion.Create(0f, 1f, 0.15f).WithScheduler(Clock).BindToAlpha(title).AddTo(gameObject);
        LMotion.Create(new Vector3(0.15f, 0.8f, 1f), Vector3.one, 0.45f).WithEase(Ease.OutBack).WithScheduler(Clock).BindToLocalScale(ribbon).AddTo(gameObject);
        LMotion.Create(70f, 10f, 0.6f).WithEase(Ease.OutCubic).WithScheduler(Clock).Bind(caption, (v, t) => t.characterSpacing = v).AddTo(gameObject);
        for (int i = 0; i < sparkles.Length; i++)
        {
            RectTransform s = sparkles[i];
            LMotion.Create(Vector3.zero, Vector3.one, 0.35f).WithEase(Ease.OutBack).WithDelay(0.2f + 0.07f * i).WithScheduler(Clock)
                .BindToLocalScale(s).AddTo(gameObject);
            LMotion.Create(-90f, 0f, 0.5f).WithEase(Ease.OutCubic).WithDelay(0.2f + 0.07f * i).WithScheduler(Clock)
                .BindToLocalEulerAnglesZ(s).AddTo(gameObject);
        }

        float start = Time.unscaledTime;
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        while (load != null && !load.isDone)
            yield return null;
        // 새 씬의 첫 렌더가 끝나도록 두 프레임 더 기다린다.
        yield return null;
        yield return null;
        float left = holdTime - (Time.unscaledTime - start);
        if (left > 0f)
            yield return new WaitForSecondsRealtime(left);

        LMotion.Create(1f, 0f, 0.15f).WithScheduler(Clock).BindToAlpha(title).AddTo(gameObject);
        var gone = new Vector2(-offscreen, 0f);
        Slide(bandMain, Vector2.zero, gone, revealTime, 0.1f, Ease.InCubic);
        Slide(bandCream, Vector2.zero, gone, revealTime, 0.1f + 0.045f, Ease.InCubic);
        Slide(bandGold, Vector2.zero, gone, revealTime, 0.1f + 0.09f, Ease.InCubic);
        yield return new WaitForSecondsRealtime(0.1f + revealTime * 0.6f);
        // 새 화면의 등장 연출은 띠가 걷히는 순간에 맞춰 다시 튼다.
        FindFirstObjectByType<StageSelectionPopup>()?.ReplayIntro();
        yield return new WaitForSecondsRealtime(revealTime * 0.4f + 0.12f);
        Destroy(gameObject);
    }

    private void Update()
    {
        Rect uv = stripes.uvRect;
        uv.x -= Time.unscaledDeltaTime * 0.06f;
        stripes.uvRect = uv;
    }

    private void Slide(RectTransform rect, Vector2 from, Vector2 to, float time, float delay, Ease ease) =>
        LMotion.Create(from, to, time).WithEase(ease).WithDelay(delay).WithScheduler(Clock).BindToAnchoredPosition(rect).AddTo(gameObject);
}
