using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 스테이지 클리어(승리) 패널: 화면을 어둡게 깔고, 왼쪽에 기뻐하는 주인공 그림, 오른쪽에 결과 패널을 띄운다.
/// VoxelDefensePrototype 이 <see cref="Bind"/>로 승리 판정에 연결한다. 연결 전 <see cref="testKey"/>가 켜져 있으면 V 키로 예시 값을 띄우고 닫는다.
/// 배치는 VictoryPanelHud.prefab에 있다.
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

    private Vector2 characterShown, panelShown;
    private bool initialized, bound;
    private float[] leafAngles;
    private MotionHandle[] leafSway = System.Array.Empty<MotionHandle>();
    private MotionHandle dimFade, characterMove, characterFade, characterBob, panelMove, panelFade, glowSpin, glowPulse;

    public bool IsOpen => content.activeSelf;

    private void Awake()
    {
        Initialize();
        // Bind 전(UI 테스트)에는 두 버튼 모두 패널만 닫는다. Awake 순서상 Bind 가 먼저 불렸을 수 있다.
        if (bound)
            return;
        nextButton.onClick.AddListener(Hide);
        retryButton.onClick.AddListener(Hide);
    }

    /// <summary>승리 판정에 연결한다. next 가 null 이면 Next 버튼을 숨긴다. 테스트 키는 끈다.</summary>
    public void Bind(System.Action next, System.Action retry)
    {
        bound = true;
        testKey = false;
        nextButton.onClick.RemoveAllListeners();
        retryButton.onClick.RemoveAllListeners();
        nextButton.gameObject.SetActive(next != null);
        if (next != null)
            nextButton.onClick.AddListener(() => next());
        retryButton.onClick.AddListener(() => retry());
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
        titleText.text = GameText.Get("result.complete");
        retryCaption.text = GameText.Get("result.retry");
        coreCaption.text = GameText.Get("result.core_health");
        nextCaption.text = GameText.Get("result.next");
        leakedCaption.text = GameText.Get("result.leaked");
        fallenCaption.text = GameText.Get("result.fallen");
        content.SetActive(false);
        initialized = true;
    }

    private void Update()
    {
        if (!testKey || Keyboard.current?.vKey.wasPressedThisFrame != true)
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

        dim.alpha = 0f;
        dimFade = Fade(dim, 1f, 0.25f, 0f);

        character.anchoredPosition = characterShown + Vector2.left * characterOffset;
        characterGroup.alpha = 0f;
        characterMove = LMotion.Create(character.anchoredPosition.x, characterShown.x, 0.5f)
            .WithEase(Ease.OutBack).WithDelay(0.1f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPositionX(character);
        characterFade = Fade(characterGroup, 1f, 0.25f, 0.1f);
        // 등장 뒤에는 위아래로 가볍게 들썩인다.
        characterBob = LMotion.Create(characterShown.y, characterShown.y + bobHeight, 0.9f)
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
