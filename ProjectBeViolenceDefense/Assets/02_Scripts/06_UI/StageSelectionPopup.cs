using System;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// 스테이지 선택 화면: 왼쪽에 위아래로 천천히 떠 있는 주인공 그림과 대사 띠, 오른쪽에 위에서부터 스테이지 카드 1~5.
/// 실제 클리어 기록, 현재 도전할 스테이지, 입장 가능, 잠김 상태를 구분한다.
/// 진행도마다 대사와 주인공 표정 그림이 바뀐다(<see cref="moods"/>, 진행도 1부터 순서대로).
/// 배치는 StageSelectionPopup.prefab에 있다.
public sealed class StageSelectionPopup : MonoBehaviour
{
    [Serializable]
    public struct Card
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image plate;
        public TextMeshProUGUI number;
        public RawImage thumbnail;
        public TextMeshProUGUI label;
        public Image chip;
        public Image chipIcon;
        public TextMeshProUGUI chipText;
        public Button button;
        public Image buttonImage;
        public TextMeshProUGUI buttonText;
        public Image buttonLock;
    }

    [Serializable]
    public struct Mood
    {
        public string lineKey;
        public Texture2D portrait;
    }

    [SerializeField] private TextMeshProUGUI title;
    [Tooltip("선택. 상단 바의 환경설정 버튼과 창")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private Card[] cards = Array.Empty<Card>();
    [SerializeField] private Texture2D[] thumbnails = Array.Empty<Texture2D>();
    [SerializeField] private Sprite checkIcon;
    [SerializeField] private Sprite lockIcon;

    [Header("Heroine")]
    [SerializeField] private RawImage heroine;
    [SerializeField] private Mood[] moods = Array.Empty<Mood>();
    [SerializeField] private float floatHeight = 14f;
    [SerializeField] private float floatPeriod = 3.2f;
    [SerializeField] private TextMeshProUGUI speaker;
    [SerializeField] private TextMeshProUGUI line;
    [SerializeField] private CanvasGroup lineGroup;

    [SerializeField] private float currentScale = 1.04f;

    private const float LockedAlpha = 0.72f;
    private StageContent[] stages = Array.Empty<StageContent>();
    private Action<int> select;
    private StageProgress progress;
    private StageProgressMenu progressMenu;
    private Vector2 heroineHome;
    private bool heroineHomeSet;
    private MotionHandle heroineFloat;
    private Vector2[] cardHome = Array.Empty<Vector2>();

    public int UnlockedStages => progress?.UnlockedStageCount(stages.Length) ?? 0;

    internal void Bind(StageContent[] stages, Action<int> select, StageProgress progress, UIManager ui)
    {
        this.stages = stages;
        this.select = select;
        this.progress = progress;
        progressMenu ??= new StageProgressMenu(ui, title, progress, Refresh);
        title.text = GameText.Get("stage_select.title");
        if (settingsButton != null && settingsPanel != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(settingsPanel.Open);
            var settingsLabel = settingsButton.GetComponentInChildren<TextMeshProUGUI>();
            if (settingsLabel != null)
                settingsLabel.text = GameText.Get("settings.button");
        }
        speaker.text = GameText.Get("stage_select.speaker");
        cardHome = new Vector2[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            cardHome[i] = cards[i].root.anchoredPosition;
            cards[i].button.onClick.RemoveAllListeners();
            cards[i].button.onClick.AddListener(() => Enter(index));
            AddHover(cards[i]);
        }
        Refresh();
        if (isActiveAndEnabled)
            PlayIntro();
    }

    private void Enter(int index)
    {
        if (progress != null && progress.IsUnlocked(index + 1, stages.Length))
            select?.Invoke(index);
    }

    private void Refresh()
    {
        int currentStage = progress?.CurrentStage(stages.Length) ?? 1;
        for (int i = 0; i < cards.Length; i++)
        {
            Card c = cards[i];
            bool exists = i < stages.Length && stages[i] != null;
            c.root.gameObject.SetActive(exists);
            if (!exists)
                continue;
            bool cleared = progress.IsCleared(i + 1), locked = !progress.IsUnlocked(i + 1, stages.Length);
            bool current = i + 1 == currentStage && !cleared;
            c.number.text = (i + 1).ToString("00");
            c.label.text = stages[i].DisplayName;
            if (i < thumbnails.Length && thumbnails[i] != null)
                c.thumbnail.texture = thumbnails[i];
            c.thumbnail.color = locked ? new Color(0.78f, 0.74f, 0.74f) : Color.white;

            c.group.alpha = locked ? LockedAlpha : 1f;
            c.plate.color = current ? Opaque(UIPalette.LightSurface) : UIPalette.LightSurface;
            c.root.localScale = Vector3.one * (current ? currentScale : 1f);
            c.number.color = c.label.color = locked ? UIPalette.TextOnLightMuted : UIPalette.TextOnLight;

            c.chip.color = locked ? Fade(UIPalette.TextOnLightMuted, 0.18f) : UIPalette.ChipOnLight;
            c.chipText.color = locked ? UIPalette.TextOnLightMuted : UIPalette.TextOnLight;
            c.chipText.text = GameText.Get(cleared ? "stage_select.state.cleared" : locked ? "stage_select.state.locked" : current ? "stage_select.state.current" : "stage_select.state.available");
            c.chipIcon.gameObject.SetActive(cleared || locked);
            c.chipIcon.sprite = cleared ? checkIcon : lockIcon;
            c.chipIcon.color = cleared ? UIPalette.TextOnLight : UIPalette.TextOnLightMuted;

            c.button.interactable = !locked;
            c.buttonImage.color = locked ? UIPalette.ButtonSecondary : current ? UIPalette.ButtonPrimary : Fade(UIPalette.ButtonPrimary, 0.75f);
            c.buttonText.gameObject.SetActive(!locked);
            c.buttonText.text = GameText.Get("stage_select.enter");
            c.buttonText.color = UIPalette.TextOnAccent;
            c.buttonLock.gameObject.SetActive(locked);
            c.buttonLock.color = UIPalette.TextPrimary;
        }
        if (moods.Length > 0)
            ShowMood(moods[Mathf.Clamp(currentStage - 1, 0, moods.Length - 1)]);
    }

    private void ShowMood(Mood mood)
    {
        line.text = GameText.Get(mood.lineKey);
        if (mood.portrait != null)
            heroine.texture = mood.portrait;
    }

    /// 씬 와이프가 걷힐 때처럼, 이미 열린 화면의 등장 연출을 다시 튼다.
    public void ReplayIntro()
    {
        if (isActiveAndEnabled)
            PlayIntro();
    }

    private void OnEnable()
    {
        PlayIntro();
        StartFloat();
    }

    private void OnDisable()
    {
        if (heroineFloat.IsActive())
            heroineFloat.Cancel();
        if (heroineHomeSet)
            heroine.rectTransform.anchoredPosition = heroineHome;
    }

    // 주인공은 제자리에서 위아래로 천천히 떠 있다.
    private void StartFloat()
    {
        if (heroine == null)
            return;
        if (!heroineHomeSet)
        {
            heroineHome = heroine.rectTransform.anchoredPosition;
            heroineHomeSet = true;
        }
        if (heroineFloat.IsActive())
            heroineFloat.Cancel();
        heroineFloat = LMotion.Create(heroineHome.y, heroineHome.y + floatHeight, floatPeriod * 0.5f)
            .WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPositionY(heroine.rectTransform);
    }

    private void PlayIntro()
    {
        if (cardHome.Length != cards.Length || cards.Length == 0)
            return;
        // 카드는 위에서부터 차례로 오른쪽에서 밀려 들어오고, 대사 띠는 살짝 늦게 떠오른다.
        for (int i = 0; i < cards.Length; i++)
        {
            Card c = cards[i];
            float delay = 0.05f * i;
            float targetAlpha = i >= UnlockedStages ? LockedAlpha : 1f;
            LMotion.Create(cardHome[i] + new Vector2(80f, 0f), cardHome[i], 0.35f).WithEase(Ease.OutCubic).WithDelay(delay)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToAnchoredPosition(c.root).AddTo(gameObject);
            LMotion.Create(0f, targetAlpha, 0.3f).WithDelay(delay).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(c.group, (a, g) => g.alpha = a).AddTo(gameObject);
        }
        if (lineGroup != null)
            LMotion.Create(0f, 1f, 0.35f).WithDelay(0.25f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(lineGroup, (a, g) => g.alpha = a).AddTo(gameObject);
    }

    // 열린 카드에 마우스를 올리면 버튼이 살짝 커진다.
    private static void AddHover(Card card)
    {
        var trigger = card.button.GetComponent<EventTrigger>() ?? card.button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();
        var rect = (RectTransform)card.button.transform;
        void Add(EventTriggerType type, float scale)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ =>
            {
                if (card.button.interactable)
                    LMotion.Create(rect.localScale, Vector3.one * scale, 0.12f).WithEase(Ease.OutQuad)
                        .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToLocalScale(rect);
            });
            trigger.triggers.Add(entry);
        }
        Add(EventTriggerType.PointerEnter, 1.06f);
        Add(EventTriggerType.PointerExit, 1f);
    }

    private static Color Opaque(Color c) => new(c.r, c.g, c.b, 1f);
    private static Color Fade(Color c, float a) => new(c.r, c.g, c.b, c.a * a);
}
