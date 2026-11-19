using System;
using System.Collections;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// 프롤로그 그림책: 그림 한 장과 문구 한두 줄씩 보여 주고, 클릭·Space·Enter·→ 로 책장을 넘긴다(PageCurl 셰이더).
/// 마지막 장을 넘기면 onFinished 를 부른다. Esc 나 건너뛰기 버튼은 바로 끝낸다.
/// 배치는 TitleScene 의 Prologue 오브젝트에 있다.
public sealed class StorybookPrologue : MonoBehaviour
{
    [Serializable]
    public struct Page
    {
        public Texture2D picture;
        public string textKey;
    }

    [SerializeField] private Page[] pages = Array.Empty<Page>();

    [Header("Layout")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RawImage under;
    [SerializeField] private RawImage over;
    [SerializeField] private TextMeshProUGUI caption;
    [SerializeField] private CanvasGroup captionGroup;
    [SerializeField] private TextMeshProUGUI pageNumber;
    [SerializeField] private CanvasGroup nextHint;
    [SerializeField] private Button advanceArea;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI skipLabel;

    [Header("Timing (초)")]
    [SerializeField] private float fadeIn = 0.6f;
    [SerializeField] private float minShow = 0.6f;
    [SerializeField] private float turnTime = 1.15f;
    [SerializeField] private float textFade = 0.45f;

    [Tooltip("끝날 때 책을 서서히 지운다. 다음 화면 전환 연출이 책을 덮는다면 끈다.")]
    [SerializeField] private bool fadeOnFinish = true;

    [Header("Curl")]
    [SerializeField, Range(-60f, 60f)] private float angle = -20f;
    [SerializeField, Range(0.02f, 0.4f)] private float curlRadius = 0.12f;

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int AngleId = Shader.PropertyToID("_Angle");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");

    private Material curl;
    private Material underPage;
    private Action finished;
    private int index;
    private float shownAt;
    private bool turning;
    private bool done;
    private MotionHandle hintBlink;

    private void Awake()
    {
        curl = new Material(over.material);
        over.material = curl;
        // 아래 페이지도 같은 낡은 종이 모양으로 보이게, 넘기지 않는(진행 0) 복사본을 쓴다.
        underPage = new Material(curl);
        under.material = underPage;
        advanceArea.onClick.AddListener(Advance);
        skipButton.onClick.AddListener(Finish);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (curl != null)
            Destroy(curl);
        if (underPage != null)
            Destroy(underPage);
    }

    public void Play(Action onFinished)
    {
        finished = onFinished;
        done = false;
        index = 0;
        gameObject.SetActive(true);
        skipLabel.text = GameText.Get("story.skip");
        var rect = over.rectTransform.rect;
        float aspect = rect.height > 0f ? rect.width / rect.height : 16f / 9f;
        curl.SetFloat(AspectId, aspect);
        curl.SetFloat(AngleId, angle);
        curl.SetFloat(RadiusId, curlRadius);
        underPage.SetFloat(AspectId, aspect);
        underPage.SetFloat(ProgressId, 0f);
        LMotion.Create(0f, 1f, fadeIn).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToAlpha(group).AddTo(gameObject);
        ShowPage(0);
    }

    private void Update()
    {
        if (done)
            return;
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (keyboard.escapeKey.wasPressedThisFrame)
            Finish();
        else if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            Advance();
    }

    private void ShowPage(int i)
    {
        index = i;
        over.texture = pages[i].picture;
        curl.SetFloat(ProgressId, 0f);
        bool last = i + 1 >= pages.Length;
        // 마지막 장 아래는 책 뒤표지(짙은 갈색 종이)
        under.texture = last ? null : pages[i + 1].picture;
        under.color = last ? new Color(0.24f, 0.17f, 0.12f) : Color.white;
        caption.text = GameText.Get(pages[i].textKey);
        if (pageNumber != null)
            pageNumber.text = $"{i + 1} / {pages.Length}";
        shownAt = Time.unscaledTime;
        LMotion.Create(0f, 1f, textFade).WithDelay(0.15f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAlpha(captionGroup).AddTo(gameObject);
        LMotion.Create(new Vector2(0f, -14f), Vector2.zero, textFade).WithEase(Ease.OutCubic).WithDelay(0.15f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToAnchoredPosition(caption.rectTransform).AddTo(gameObject);
        StartHint();
    }

    private void StartHint()
    {
        if (hintBlink.IsActive())
            hintBlink.Cancel();
        nextHint.alpha = 0f;
        hintBlink = LMotion.Create(0.25f, 1f, 0.55f).WithDelay(minShow).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToAlpha(nextHint).AddTo(gameObject);
    }

    public void Advance()
    {
        if (done || turning || Time.unscaledTime - shownAt < minShow)
            return;
        StartCoroutine(Turn());
    }

    private IEnumerator Turn()
    {
        turning = true;
        if (hintBlink.IsActive())
            hintBlink.Cancel();
        nextHint.alpha = 0f;
        LMotion.Create(captionGroup.alpha, 0f, 0.2f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToAlpha(captionGroup).AddTo(gameObject);
        yield return LMotion.Create(0f, 1f, turnTime).WithEase(Ease.InOutSine).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(curl, (p, m) => m.SetFloat(ProgressId, p)).AddTo(gameObject).ToYieldInstruction();
        turning = false;
        if (index + 1 >= pages.Length)
            Finish();
        else
            ShowPage(index + 1);
    }

    private void Finish()
    {
        if (done)
            return;
        done = true;
        StopAllCoroutines();
        if (!fadeOnFinish)
        {
            finished?.Invoke();
            return;
        }
        LMotion.Create(group.alpha, 0f, 0.35f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() => finished?.Invoke()).BindToAlpha(group).AddTo(gameObject);
    }
}
