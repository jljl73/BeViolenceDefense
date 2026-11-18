using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 결과 패널(승리·패배 공용): 화면을 어둡게 깔고, 왼쪽에 주인공 그림, 오른쪽에 결과 패널을 띄운다.
/// VoxelDefensePrototype 이 <see cref="Bind"/>로 승리·패배 판정에 연결한다. 연결 전 <see cref="testKey"/>가 켜져 있으면
/// <see cref="testKeyCode"/> 키로 예시 값을 띄우고 닫는다. 주인공 그림은 정지 그림이거나, 프레임을 격자로 이어 붙인 시트를 반복 재생한다.
/// 배치는 VictoryPanelHud.prefab / DefeatPanelHud.prefab에 있다. 패배 패널은 캡션 필드를 비워 두고 자기 글자를 쓴다.
/// </summary>
public sealed class VictoryPanelHud : MonoBehaviour
{
    [SerializeField] private GameObject content;
    [SerializeField] private CanvasGroup dim;

    [Header("Character (left)")]
    [SerializeField] private RectTransform character;
    [SerializeField] private CanvasGroup characterGroup;
    [SerializeField] private RectTransform glow;
    [Tooltip("캐릭터 주변에 떠 있는 잎. 띄워져 있는 동안 좌우로 살랑인다.")]
    [SerializeField] private RectTransform[] leaves = System.Array.Empty<RectTransform>();
    [Tooltip("선택. 프레임을 왼쪽 위부터 오른쪽·아래로 이어 붙인 시트. 지정하면 정지 그림 대신 반복 재생한다.")]
    [SerializeField] private Texture2D characterSheet;
    [SerializeField, Min(1)] private int sheetColumns = 1;
    [SerializeField, Min(1)] private int sheetRows = 1;
    [SerializeField, Min(0)] private int sheetFrames;
    [SerializeField] private float sheetFps = 12f;

    [Header("Result panel (right)")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI retryCaption;
    [SerializeField] private TextMeshProUGUI coreCaption;
    [SerializeField] private TextMeshProUGUI nextCaption;
    [SerializeField] private TextMeshProUGUI leakedCaption;
    [SerializeField] private TextMeshProUGUI fallenCaption;
    [SerializeField] private TextMeshProUGUI coreText;
    [SerializeField] private TextMeshProUGUI leakedText;
    [SerializeField] private TextMeshProUGUI fallenText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button retryButton;

    [Header("Motion (unscaled seconds)")]
    [SerializeField] private float characterOffset = 420f;
    [SerializeField] private float panelOffset = 260f;
    [SerializeField] private float bobHeight = 10f;

    [Header("Test")]
    [Tooltip("켜면 V 키로 예시 값의 승리 패널을 띄우고 닫는다. 승리 판정을 연결하면 끈다.")]
    [SerializeField] private bool testKey = true;
    [SerializeField] private Key testKeyCode = Key.V;

    private Vector2 characterShown, panelShown;
    private bool initialized, bound;
    private float[] leafAngles;
    private MotionHandle[] leafSway = System.Array.Empty<MotionHandle>();
    private RawImage characterImage;
    private float sheetStart;
    private int shownSheetFrame = -1;
    private MotionHandle dimFade, characterMove, characterFade, characterBob, panelMove, panelFade, glowSpin, glowPulse;

    public bool IsOpen => content.activeSelf;

    private void Awake()
    {
        Initialize();
        // Bind 전(UI 테스트)에는 두 버튼 모두 패널만 닫는다. Awake 순서상 Bind 가 먼저 불렸을 수 있다.
        if (bound)
            return;
        nextButton.onClick.AddListener(Hide);
        if (retryButton != null)
            retryButton.onClick.AddListener(Hide);
    }

    /// <summary>승리 판정에 연결한다. next 가 null 이면 Next 버튼을 숨긴다. 테스트 키는 끈다.</summary>
    /// 패배 패널처럼 보조 버튼이 없는 패널은 주 버튼(next 자리)에 다시 하기를 연결한다.
    public void Bind(System.Action next, System.Action retry)
    {
        bound = true;
        testKey = false;
        nextButton.onClick.RemoveAllListeners();
        nextButton.gameObject.SetActive(next != null);
        if (next != null)
            nextButton.onClick.AddListener(() => next());
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.gameObject.SetActive(retry != null);
            if (retry != null)
                retryButton.onClick.AddListener(() => retry());
        }
    }

    private void Initialize()
    {
        if (initialized)
            return;
        characterShown = character.anchoredPosition;
        panelShown = panel.anchoredPosition;
        leafAngles = new float[leaves.Length];
        for (int i = 0; i < leaves.Length; i++)
            leafAngles[i] = leaves[i].localEulerAngles.z;
        leafSway = new MotionHandle[leaves.Length];
        characterImage = character.GetComponent<RawImage>();
        SetCaption(titleText, "result.complete");
        SetCaption(retryCaption, "result.retry");
        SetCaption(coreCaption, "result.core_health");
        SetCaption(nextCaption, "result.next");
        SetCaption(leakedCaption, "result.leaked");
        SetCaption(fallenCaption, "result.fallen");
        content.SetActive(false);
        initialized = true;
    }

    private static void SetCaption(TextMeshProUGUI label, string key)
    {
        if (label != null)
            label.text = GameText.Get(key);
    }

    // 시트의 프레임은 왼쪽 위에서 오른쪽으로, 위에서 아래로 놓여 있다.
    private void PlayCharacterSheet()
    {
        if (characterSheet == null || sheetFrames <= 0 || characterImage == null)
            return;
        int frame = Mathf.FloorToInt((Time.unscaledTime - sheetStart) * sheetFps) % sheetFrames;
        if (frame == shownSheetFrame)
            return;
        shownSheetFrame = frame;
        float w = 1f / sheetColumns, h = 1f / sheetRows;
        characterImage.uvRect = new Rect(frame % sheetColumns * w, 1f - (frame / sheetColumns + 1) * h, w, h);
    }

    private void Update()
    {
        if (IsOpen)
            PlayCharacterSheet();
        if (!testKey || Keyboard.current == null || !Keyboard.current[testKeyCode].wasPressedThisFrame)
            return;
        if (IsOpen)
            Hide();
        else
            Show(GameText.Get("stage.1"), 96, 120, 24, 58);
    }

    /// <summary>결과 값을 채우고 패널을 띄운다.</summary>
    public void Show(string stage, int coreHealth, int coreMax, int leaked, int fallen)
    {
        Initialize();
        Cancel();
        stageText.text = string.IsNullOrWhiteSpace(stage) ? GameText.Get("result.stage") : stage;
        coreText.text = $"{coreHealth} <size=70%>/ {coreMax}</size>";
        leakedText.text = leaked.ToString();
        fallenText.text = fallen.ToString();
        content.SetActive(true);
        if (characterSheet != null && sheetFrames > 0 && characterImage != null)
        {
            characterImage.texture = characterSheet;
            sheetStart = Time.unscaledTime;
            shownSheetFrame = -1;
            PlayCharacterSheet();
        }

        dim.alpha = 0f;
        dimFade = Fade(dim, 1f, 0.25f, 0f);

        character.anchoredPosition = characterShown + Vector2.left * characterOffset;
        characterGroup.alpha = 0f;
        characterMove = LMotion.Create(character.anchoredPosition.x, characterShown.x, 0.5f)
            .WithEase(Ease.OutBack).WithDelay(0.1f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPositionX(character);
        characterFade = Fade(characterGroup, 1f, 0.25f, 0.1f);
        // 등장 뒤에는 위아래로 가볍게 들썩인다.
        // 시트가 스스로 움직이면 들썩임은 줄인다.
        float bob = characterSheet != null && sheetFrames > 0 ? bobHeight * 0.3f : bobHeight;
        characterBob = LMotion.Create(characterShown.y, characterShown.y + bob, 0.9f)
            .WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPositionY(character);
        if (glow != null)
        {
            glowSpin = LMotion.Create(0f, 360f, 40f)
                .WithLoops(-1, LoopType.Restart).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalEulerAnglesZ(glow);
            glowPulse = LMotion.Create(Vector3.one * 0.94f, Vector3.one * 1.06f, 1.6f)
                .WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(glow);
        }

        for (int i = 0; i < leaves.Length; i++)
        {
            float swing = i % 2 == 0 ? 14f : -14f;
            leafSway[i] = LMotion.Create(leafAngles[i] - swing, leafAngles[i] + swing, 1.3f + i * 0.2f)
                .WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalEulerAnglesZ(leaves[i]);
        }

        panel.anchoredPosition = panelShown + Vector2.right * panelOffset;
        panelGroup.alpha = 0f;
        panelGroup.interactable = true;
        panelMove = LMotion.Create(panel.anchoredPosition.x, panelShown.x, 0.4f)
            .WithEase(Ease.OutCubic).WithDelay(0.25f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPositionX(panel);
        panelFade = Fade(panelGroup, 1f, 0.3f, 0.25f);
    }

    public void Hide()
    {
        if (!IsOpen)
            return;
        Cancel();
        panelGroup.interactable = false;
        panelFade = Fade(panelGroup, 0f, 0.15f, 0f);
        characterFade = Fade(characterGroup, 0f, 0.15f, 0f);
        dimFade = LMotion.Create(dim.alpha, 0f, 0.2f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() => content.SetActive(false))
            .BindToAlpha(dim);
    }

    private static MotionHandle Fade(CanvasGroup group, float to, float time, float delay) =>
        LMotion.Create(group.alpha, to, time)
            .WithDelay(delay).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAlpha(group);

    private void Cancel()
    {
        TryCancel(dimFade);
        TryCancel(characterMove);
        TryCancel(characterFade);
        TryCancel(characterBob);
        TryCancel(panelMove);
        TryCancel(panelFade);
        TryCancel(glowSpin);
        TryCancel(glowPulse);
        foreach (MotionHandle handle in leafSway)
            TryCancel(handle);
    }

    private static void TryCancel(MotionHandle handle)
    {
        if (handle.IsActive())
            handle.Cancel();
    }

    private void OnDestroy() => Cancel();
}
