using System;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 대화창: 하단의 대화 상자, 왼쪽의 화자 초상, 이름표, 건너뛰기 버튼.
/// UIManager의 팝업으로 뜨므로 열려 있는 동안 보드 입력과 전투 조작이 막히고, HUD는 숨긴다.
/// 글자는 한 자씩 나오며, 클릭·Enter·Space로 넘기고 S 또는 버튼으로 전체를 건너뛴다.
/// 배치는 DialogueHud.prefab에 있다. Popup 레이어의 직계 자식이어야 한다.
/// </summary>
public sealed class DialogueHud : MonoBehaviour
{
    public enum Expression { Neutral, Happy, Surprised, Troubled, Pout, Teary, Breakdown, Shock }

    [Serializable]
    public struct Line
    {
        public string speakerKey;
        public Expression expression;
        public string textKey;
        [Tooltip("선택. 주인공이 아닌 화자의 초상. 넣으면 표정 대신 이 그림을 쓴다.")]
        public Texture2D portrait;
    }

    /// <summary>표정 하나의 움직이는 초상. 프레임을 격자로 이어 붙인 텍스처 한 장.</summary>
    [Serializable]
    public struct PortraitSheet
    {
        public Texture2D texture;
        public int columns;
        public int rows;
        public int frames;
    }

    [Header("Portrait (left)")]
    [SerializeField] private RectTransform portrait;
    [SerializeField] private RawImage portraitImage;
    [SerializeField] private CanvasGroup portraitGroup;
    [Tooltip("Expression 순서(Neutral, Happy, Surprised, Troubled, Pout, Teary, Breakdown, Shock)의 초상 그림")]
    [SerializeField] private Texture2D[] portraits = Array.Empty<Texture2D>();
    [Tooltip("선택. Expression 순서의 움직이는 초상 시트. 해당 표정의 시트가 있으면 정지 그림 대신 반복 재생한다.")]
    [SerializeField] private PortraitSheet[] portraitSheets = Array.Empty<PortraitSheet>();
    [SerializeField] private float portraitFps = 14f;

    [Header("Box (bottom)")]
    [SerializeField] private RectTransform box;
    [SerializeField] private CanvasGroup boxGroup;
    [SerializeField] private RectTransform nameTab;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private RectTransform nextMark;
    [Tooltip("화면 전체를 덮는 투명 버튼. 누르면 다음 대사로 넘어간다.")]
    [SerializeField] private Button advanceButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI skipCaption;

    [Header("Timing (unscaled seconds)")]
    [SerializeField] private float charactersPerSecond = 45f;
    [SerializeField] private float boxOffset = 320f;
    [SerializeField] private float portraitOffset = 360f;

    private UIManager ui;
    private CanvasGroup hudGroup;
    private Line[] lines = Array.Empty<Line>();
    private Action finished;
    private int index;
    private float shownCharacters;
    private int totalCharacters;
    private Vector2 boxShown, portraitShown, nextShown;
    private bool initialized;
    private PortraitSheet playingSheet;
    private float sheetStart;
    private int shownSheetFrame = -1;
    private int shownFace = -1;
    private Texture shownPortrait;
    private Vector2 portraitImageSize;
    private MotionHandle boxMove, boxFade, portraitMove, portraitFade, portraitHop, nextBob, tabPop, hudFade;

    public bool IsOpen => gameObject.activeSelf;
    private bool Typing => shownCharacters < totalCharacters;

    private void Initialize()
    {
        if (initialized)
            return;
        ui = FindFirstObjectByType<UIManager>();
        hudGroup = ui.HudLayer.GetComponent<CanvasGroup>();
        if (hudGroup == null)
            hudGroup = ui.HudLayer.gameObject.AddComponent<CanvasGroup>();
        boxShown = box.anchoredPosition;
        portraitShown = portrait.anchoredPosition;
        portraitImageSize = portraitImage.rectTransform.rect.size;
        nextShown = nextMark.anchoredPosition;
        skipCaption.text = GameText.Get("dialogue.skip");
        advanceButton.onClick.AddListener(Advance);
        skipButton.onClick.AddListener(Close);
        initialized = true;
    }

    /// <summary>대사 묶음을 재생한다. 끝나거나 건너뛰면 <paramref name="onFinished"/>를 부른다.</summary>
    public void Play(Line[] dialogue, Action onFinished = null)
    {
        if (dialogue == null || dialogue.Length == 0)
        {
            onFinished?.Invoke();
            return;
        }

        Initialize();
        Cancel();
        lines = dialogue;
        finished = onFinished;
        index = 0;
        shownFace = -1;
        shownPortrait = null;
        ui.ShowPopup((RectTransform)transform, dismissOnEscape: false);

        hudFade = Fade(hudGroup, 0f, 0.2f, 0f);
        box.anchoredPosition = boxShown + Vector2.down * boxOffset;
        boxGroup.alpha = 0f;
        boxMove = LMotion.Create(box.anchoredPosition.y, boxShown.y, 0.35f)
            .WithEase(Ease.OutCubic).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPositionY(box);
        boxFade = Fade(boxGroup, 1f, 0.25f, 0f);
        portrait.anchoredPosition = portraitShown + Vector2.left * portraitOffset;
        portraitGroup.alpha = 0f;
        portraitMove = LMotion.Create(portrait.anchoredPosition.x, portraitShown.x, 0.45f)
            .WithEase(Ease.OutBack).WithDelay(0.1f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPositionX(portrait);
        portraitFade = Fade(portraitGroup, 1f, 0.25f, 0.1f);
        nextBob = LMotion.Create(nextShown.y, nextShown.y - 8f, 0.45f)
            .WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPositionY(nextMark);
        ShowLine(false);
    }

    private void ShowLine(bool hop)
    {
        Line line = lines[index];
        string speaker = GameText.Get(line.speakerKey);
        bool speakerChanged = nameText.text != speaker;
        nameText.text = speaker;
        nameTab.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
        int face = (int)line.expression;
        bool custom = line.portrait != null;
        bool hasSheet = !custom && face < portraitSheets.Length && portraitSheets[face].texture != null && portraitSheets[face].frames > 0;
        Texture2D next = custom ? line.portrait : hasSheet ? portraitSheets[face].texture : face < portraits.Length ? portraits[face] : null;
        bool faceChanged = next != null && next != shownPortrait;
        if (faceChanged)
        {
            shownFace = face;
            shownPortrait = next;
            portraitImage.texture = next;
            // 다른 화자의 그림은 높이를 맞추고 그림 비율대로 폭을 정한다.
            RectTransform imageRect = portraitImage.rectTransform;
            if (imageRect.anchorMin == imageRect.anchorMax)
                imageRect.sizeDelta = custom
                    ? new Vector2(portraitImageSize.y * next.width / next.height, portraitImageSize.y)
                    : portraitImageSize;
            portraitImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            playingSheet = hasSheet ? portraitSheets[face] : default;
            sheetStart = Time.unscaledTime;
            shownSheetFrame = -1;
            PlayPortraitSheet();
        }

        bodyText.text = GameText.Get(line.textKey);
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
        totalCharacters = bodyText.textInfo.characterCount;
        shownCharacters = 0f;
        nextMark.gameObject.SetActive(false);

        if (hop && faceChanged)
        {
            // 표정이 바뀌면 초상이 살짝 튄다.
            TryCancel(portraitHop);
            portrait.localScale = Vector3.one * 0.96f;
            portraitHop = LMotion.Create(portrait.localScale, Vector3.one, 0.3f)
                .WithEase(Ease.OutBack).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(portrait);
        }
        if (speakerChanged && nameTab.gameObject.activeSelf)
        {
            TryCancel(tabPop);
            nameTab.localScale = Vector3.one * 0.85f;
            tabPop = LMotion.Create(nameTab.localScale, Vector3.one, 0.25f)
                .WithEase(Ease.OutBack).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(nameTab);
        }
    }

    // 시트의 프레임은 왼쪽 위에서 오른쪽으로, 위에서 아래로 놓여 있다.
    private void PlayPortraitSheet()
    {
        if (playingSheet.texture == null || playingSheet.frames <= 0)
            return;
        int frame = Mathf.FloorToInt((Time.unscaledTime - sheetStart) * portraitFps) % playingSheet.frames;
        if (frame == shownSheetFrame)
            return;
        shownSheetFrame = frame;
        float w = 1f / playingSheet.columns, h = 1f / playingSheet.rows;
        portraitImage.uvRect = new Rect(frame % playingSheet.columns * w, 1f - (frame / playingSheet.columns + 1) * h, w, h);
    }

    private void Update()
    {
        PlayPortraitSheet();
        if (Typing)
        {
            shownCharacters = Mathf.Min(totalCharacters, shownCharacters + charactersPerSecond * Time.unscaledDeltaTime);
            bodyText.maxVisibleCharacters = Mathf.FloorToInt(shownCharacters);
            if (!Typing)
                nextMark.gameObject.SetActive(true);
        }

        // Enter·Space는 선택된 넘김 버튼의 Submit으로 들어온다. 여기서는 건너뛰기 키만 본다.
        if (Keyboard.current?.sKey.wasPressedThisFrame == true)
            Close();
    }

    /// <summary>글자가 나오는 중이면 한 번에 다 보여 주고, 다 나왔으면 다음 대사로 넘어간다.</summary>
    public void Advance()
    {
        if (Typing)
        {
            shownCharacters = totalCharacters;
            bodyText.maxVisibleCharacters = totalCharacters;
            nextMark.gameObject.SetActive(true);
            return;
        }

        index++;
        if (index >= lines.Length)
            Close();
        else
            ShowLine(true);
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        Cancel();
        hudGroup.alpha = 1f;
        portrait.localScale = Vector3.one;
        nameTab.localScale = Vector3.one;
        ui.CloseTopPopup();
        Action done = finished;
        finished = null;
        done?.Invoke();
    }

    private static MotionHandle Fade(CanvasGroup group, float to, float time, float delay) =>
        LMotion.Create(group.alpha, to, time)
            .WithDelay(delay).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAlpha(group);

    private void Cancel()
    {
        TryCancel(boxMove);
        TryCancel(boxFade);
        TryCancel(portraitMove);
        TryCancel(portraitFade);
        TryCancel(portraitHop);
        TryCancel(nextBob);
        TryCancel(tabPop);
        TryCancel(hudFade);
    }

    private static void TryCancel(MotionHandle handle)
    {
        if (handle.IsActive())
            handle.Cancel();
    }

    private void OnDestroy()
    {
        Cancel();
        if (hudGroup != null)
            hudGroup.alpha = 1f;
    }
}
