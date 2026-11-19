using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// 타이틀 화면: 흰 바탕과 도형 위에서 주인공이 화면 쪽으로 손을 뻗으며 도망쳐 오고, 뒤에서 화난 몬스터들이 쫓아온다(달리기 들썩임).
/// 제목 로고(그림)가 톡 튀어나오고 시작·설정·종료 버튼이 차례로 나타난다.
/// 시작을 누르면 프롤로그 그림책을 펼치고, 그림책이 끝나면 게임 씬(스테이지 선택)을 연다.
/// 배치는 TitleScene 에 있다.
public sealed class TitleScreen : MonoBehaviour
{
    [SerializeField] private string gameScene = "GameScene";
    [SerializeField] private RawImage cover;
    [SerializeField] private RectTransform character;
    [SerializeField] private RectTransform monsters;
    [SerializeField] private RectTransform speedLines;
    [SerializeField] private CanvasGroup titleGroup;
    [Tooltip("제목 로고 그림")]
    [SerializeField] private RectTransform logo;
    [SerializeField] private CanvasGroup buttonsGroup;
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI startLabel;
    [SerializeField] private Button quitButton;
    [SerializeField] private TextMeshProUGUI quitLabel;
    [SerializeField] private StorybookPrologue prologue;
    [SerializeField] private Button settingsButton;
    [SerializeField] private SettingsPanel settingsPanel;
    [Tooltip("선택. 있으면 그림책이 끝날 때 사선 띠 와이프로 게임 씬에 넘어간다.")]
    [SerializeField] private SceneWipe wipe;
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;

    private bool started;

    private void Start()
    {
        startLabel.text = GameText.Get("title.start");
        quitLabel.text = GameText.Get("title.quit");
        startButton.onClick.AddListener(Begin);
        quitButton.onClick.AddListener(Quit);
        if (settingsButton != null && settingsPanel != null)
        {
            settingsButton.onClick.AddListener(settingsPanel.Open);
            var settingsLabel = settingsButton.GetComponentInChildren<TextMeshProUGUI>();
            if (settingsLabel != null)
                settingsLabel.text = GameText.Get("settings.button");
        }
        if (music != null)
            SoundManager.Instance?.PlayBgm(music, musicVolume);

        // 표지는 아주 천천히 다가오고, 제목이 떨어져 내려와 자리를 잡은 뒤 리본과 버튼이 나온다.
        var sched = MotionScheduler.UpdateIgnoreTimeScale;
        LMotion.Create(0f, 1f, 0.8f).WithScheduler(sched).BindToAlpha(titleGroup).AddTo(gameObject);
        // 로고는 작게 시작해 톡 튀어나온 뒤, 숨 쉬듯 살짝 오르내린다.
        Vector2 home = logo.anchoredPosition;
        LMotion.Create(new Vector3(0.3f, 0.3f, 1f), Vector3.one, 0.5f).WithEase(Ease.OutBack).WithDelay(0.1f).WithScheduler(sched)
            .BindToLocalScale(logo).AddTo(gameObject);
        LMotion.Create(home.y, home.y + 8f, 1.8f).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithDelay(0.7f).WithScheduler(sched)
            .BindToAnchoredPositionY(logo).AddTo(gameObject);
        // 달리는 들썩임: 주인공은 빠르게 오르내리며 살짝 기울고, 몬스터는 박자를 어긋나게 뛰며, 속도선은 떨린다.
        if (character != null)
        {
            Vector2 stand = character.anchoredPosition;
            LMotion.Create(stand + new Vector2(-260f, 0f), stand, 0.55f).WithEase(Ease.OutBack).WithScheduler(sched)
                .WithOnComplete(() => LMotion.Create(stand.y, stand.y + 16f, 0.32f).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo)
                    .WithScheduler(sched).BindToAnchoredPositionY(character).AddTo(gameObject))
                .BindToAnchoredPosition(character).AddTo(gameObject);
            LMotion.Create(-1.2f, 1.2f, 0.64f).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(sched)
                .BindToLocalEulerAnglesZ(character).AddTo(gameObject);
        }
        if (monsters != null)
        {
            Vector2 at = monsters.anchoredPosition;
            LMotion.Create(at.y, at.y + 12f, 0.24f).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(sched)
                .BindToAnchoredPositionY(monsters).AddTo(gameObject);
            LMotion.Create(at.x - 14f, at.x + 14f, 1.3f).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(sched)
                .BindToAnchoredPositionX(monsters).AddTo(gameObject);
        }
        if (speedLines != null)
            LMotion.Create(0f, -36f, 0.18f).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(sched)
                .BindToAnchoredPositionX(speedLines).AddTo(gameObject);
        buttonsGroup.alpha = 0f;
        LMotion.Create(0f, 1f, 0.5f).WithDelay(1.3f).WithScheduler(sched).BindToAlpha(buttonsGroup).AddTo(gameObject);
    }

    private void Begin()
    {
        if (started)
            return;
        started = true;
        buttonsGroup.interactable = false;
        LMotion.Create(1f, 0f, 0.35f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToAlpha(titleGroup).AddTo(gameObject);
        LMotion.Create(1f, 0f, 0.35f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToAlpha(buttonsGroup).AddTo(gameObject);
        if (prologue != null)
            prologue.Play(LoadGame);
        else
            LoadGame();
    }

    private void LoadGame()
    {
        if (wipe != null)
            SceneWipe.Go(wipe, gameScene, "wipe.select.label", "wipe.select.caption");
        else
            SceneManager.LoadScene(gameScene);
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
