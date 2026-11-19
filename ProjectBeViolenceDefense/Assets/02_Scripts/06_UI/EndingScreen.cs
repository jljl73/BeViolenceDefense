using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 엔딩: 그림책(프롤로그와 같은 책장 넘김)을 끝까지 넘기면 책이 덮이고, 그 위에 엔딩 크레딧이 한 줄씩 떠오른다.
/// 크레딧이 다 나오면 "타이틀로" 버튼으로 타이틀 씬에 돌아간다. 배치는 EndingScene에 있다.
/// </summary>
public sealed class EndingScreen : MonoBehaviour
{
    [SerializeField] private StorybookPrologue book;
    [SerializeField] private CanvasGroup credits;
    [SerializeField] private RectTransform logo;
    [Tooltip("위에서부터 차례로 떠오르는 줄")]
    [SerializeField] private CanvasGroup[] lines = System.Array.Empty<CanvasGroup>();
    [Tooltip("lines 와 같은 순서의 문구 키. 비운 칸은 글자를 바꾸지 않는다.")]
    [SerializeField] private string[] lineKeys = System.Array.Empty<string>();
    [SerializeField] private CanvasGroup buttonGroup;
    [SerializeField] private Button toTitle;
    [SerializeField] private TextMeshProUGUI toTitleLabel;
    [SerializeField] private string titleScene = "TitleScene";
    [SerializeField] private SceneWipe wipe;
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField] private float lineStagger = 0.45f;

    private bool leaving;

    private void Start()
    {
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i].alpha = 0f;
            if (i < lineKeys.Length && !string.IsNullOrEmpty(lineKeys[i]))
                lines[i].GetComponentInChildren<TextMeshProUGUI>().text = GameText.Get(lineKeys[i]);
        }
        toTitleLabel.text = GameText.Get("credits.to_title");
        toTitle.onClick.AddListener(Leave);
        credits.alpha = 0f;
        credits.blocksRaycasts = false;
        credits.gameObject.SetActive(true);
        buttonGroup.alpha = 0f;
        buttonGroup.interactable = false;
        if (music != null)
            SoundManager.Instance?.PlayBgm(music, musicVolume);
        book.Play(ShowCredits);
    }

    private void ShowCredits()
    {
        var clock = MotionScheduler.UpdateIgnoreTimeScale;
        credits.blocksRaycasts = true;
        LMotion.Create(0f, 1f, 0.6f).WithScheduler(clock).BindToAlpha(credits).AddTo(gameObject);
        logo.localScale = Vector3.one * 0.6f;
        LMotion.Create(Vector3.one * 0.6f, Vector3.one, 0.6f).WithEase(Ease.OutBack).WithDelay(0.3f).WithScheduler(clock)
            .BindToLocalScale(logo).AddTo(gameObject);
        float delay = 1.0f;
        foreach (CanvasGroup line in lines)
        {
            var rect = (RectTransform)line.transform;
            Vector2 home = rect.anchoredPosition;
            LMotion.Create(0f, 1f, 0.5f).WithDelay(delay).WithScheduler(clock).BindToAlpha(line).AddTo(gameObject);
            LMotion.Create(home + new Vector2(0f, -24f), home, 0.5f).WithEase(Ease.OutCubic).WithDelay(delay).WithScheduler(clock)
                .BindToAnchoredPosition(rect).AddTo(gameObject);
            delay += lineStagger;
        }
        LMotion.Create(0f, 1f, 0.5f).WithDelay(delay + 0.4f).WithScheduler(clock)
            .WithOnComplete(() => buttonGroup.interactable = true).BindToAlpha(buttonGroup).AddTo(gameObject);
    }

    private void Leave()
    {
        if (leaving)
            return;
        leaving = true;
        if (wipe != null)
            SceneWipe.Go(wipe, titleScene, "wipe.title.label", "wipe.title.caption");
        else
            SceneManager.LoadScene(titleScene);
    }
}
