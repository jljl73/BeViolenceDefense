using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 환경설정 창: 해상도(◀ ▶로 고르고 적용), 통합·BGM·SFX 볼륨(슬라이더, 바로 반영·저장).
/// 게임 씬에서는 UIManager 팝업으로 열어 보드 입력을 막고 Esc로 닫는다. 타이틀처럼 UIManager가 없는 씬에서는 스스로 열고 닫는다.
/// 볼륨은 SoundManager 가 PlayerPrefs 에 저장하고, 해상도는 Unity 가 기억한다. 배치는 SettingsPanel.prefab에 있다.
/// </summary>
public sealed class SettingsPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform card;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI resolutionCaption;
    [SerializeField] private TextMeshProUGUI resolutionValue;
    [SerializeField] private Button resolutionPrev;
    [SerializeField] private Button resolutionNext;
    [SerializeField] private Button resolutionApply;
    [SerializeField] private TextMeshProUGUI resolutionApplyLabel;
    [SerializeField] private TextMeshProUGUI masterCaption;
    [SerializeField] private Slider master;
    [SerializeField] private TextMeshProUGUI masterValue;
    [SerializeField] private TextMeshProUGUI bgmCaption;
    [SerializeField] private Slider bgm;
    [SerializeField] private TextMeshProUGUI bgmValue;
    [SerializeField] private TextMeshProUGUI sfxCaption;
    [SerializeField] private Slider sfx;
    [SerializeField] private TextMeshProUGUI sfxValue;
    [SerializeField] private Button close;
    [SerializeField] private TextMeshProUGUI closeLabel;
    [Tooltip("게임 씬: 팝업으로 열어 보드 입력을 막는다. 비우면 스스로 연다.")]
    [SerializeField] private UIManager ui;

    private readonly List<Vector2Int> resolutions = new();
    private int resolutionIndex;
    private bool open;
    // 다른 팝업(스테이지 선택 등)이 떠 있으면 그 화면을 가리지 않도록 팝업 스택에 넣지 않고 위에 겹쳐 연다.
    private bool asPopup;

    public bool IsOpen => open;

    private void Awake()
    {
        title.text = GameText.Get("settings.title");
        resolutionCaption.text = GameText.Get("settings.resolution");
        resolutionApplyLabel.text = GameText.Get("settings.apply");
        masterCaption.text = GameText.Get("settings.master");
        bgmCaption.text = GameText.Get("settings.bgm");
        sfxCaption.text = GameText.Get("settings.sfx");
        closeLabel.text = GameText.Get("settings.close");
        resolutionPrev.onClick.AddListener(() => StepResolution(-1));
        resolutionNext.onClick.AddListener(() => StepResolution(1));
        resolutionApply.onClick.AddListener(ApplyResolution);
        close.onClick.AddListener(Close);
        master.onValueChanged.AddListener(v => { SoundManager.Instance?.SetMasterVolume(v); ShowPercent(masterValue, v); });
        bgm.onValueChanged.AddListener(v => { SoundManager.Instance?.SetVolume(SoundChannel.Bgm, v); ShowPercent(bgmValue, v); });
        sfx.onValueChanged.AddListener(v =>
        {
            SoundManager.Instance?.SetVolume(SoundChannel.Sfx, v);
            SoundManager.Instance?.SetVolume(SoundChannel.Ui, v);
            ShowPercent(sfxValue, v);
        });
        if (!open)
            gameObject.SetActive(false);
    }

    public void Open()
    {
        if (open)
            return;
        open = true;
        var sound = SoundManager.Instance;
        if (sound != null)
        {
            master.SetValueWithoutNotify(sound.MasterVolume);
            bgm.SetValueWithoutNotify(sound.BgmVolume);
            sfx.SetValueWithoutNotify(sound.SfxVolume);
        }
        ShowPercent(masterValue, master.value);
        ShowPercent(bgmValue, bgm.value);
        ShowPercent(sfxValue, sfx.value);
        LoadResolutions();

        asPopup = ui != null && !ui.HasOpenPopup;
        if (asPopup)
            ui.ShowPopup((RectTransform)transform);
        else
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }
        group.alpha = 0f;
        LMotion.Create(0f, 1f, 0.15f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToAlpha(group).AddTo(gameObject);
        LMotion.Create(Vector3.one * 0.92f, Vector3.one, 0.22f).WithEase(Ease.OutBack)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToLocalScale(card).AddTo(gameObject);
    }

    public void Close()
    {
        if (!open)
            return;
        open = false;
        SoundManager.Instance?.SaveSettings();
        if (asPopup)
        {
            // 팝업 스택 맨 위가 이 창일 때만 닫는다(Esc 로 이미 닫힌 경우는 그대로).
            if (gameObject.activeSelf)
                ui.CloseTopPopup();
        }
        else
            gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // UIManager 가 Esc 로 닫은 경우에도 상태와 저장을 맞춘다.
        if (open)
        {
            open = false;
            SoundManager.Instance?.SaveSettings();
        }
    }

    private void Update()
    {
        if (!asPopup && open && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            Close();
    }

    private static void ShowPercent(TextMeshProUGUI label, float v) => label.text = $"{Mathf.RoundToInt(v * 100f)}%";

    private void LoadResolutions()
    {
        resolutions.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (size.x >= 1024 && !resolutions.Contains(size))
                resolutions.Add(size);
        }
        var current = new Vector2Int(Screen.width, Screen.height);
        if (!resolutions.Contains(current))
            resolutions.Add(current);
        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        resolutionIndex = resolutions.IndexOf(current);
        ShowResolution();
    }

    private void StepResolution(int step)
    {
        if (resolutions.Count == 0)
            return;
        resolutionIndex = (resolutionIndex + step + resolutions.Count) % resolutions.Count;
        ShowResolution();
    }

    private void ShowResolution()
    {
        Vector2Int r = resolutions[resolutionIndex];
        bool current = r.x == Screen.width && r.y == Screen.height;
        resolutionValue.text = $"{r.x} × {r.y}";
        resolutionApply.interactable = !current;
    }

    private void ApplyResolution()
    {
        Vector2Int r = resolutions[resolutionIndex];
        Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
        resolutionApply.interactable = false;
    }
}
