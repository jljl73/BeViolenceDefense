using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 정보 HUD 3종: 상단 중앙(스테이지·타이머·진영별 적 수), 우상단(설정·배속), 우하단(다음 웨이브 호출 버튼).
/// 배치는 BattleInfoHud.prefab에 있고, 이 컴포넌트는 값과 상태 그림만 갱신한다.
/// VoxelDefensePrototype에 이 컴포넌트가 연결된 씬에서만 기존 라운드 표시·배속 버튼·시작 버튼 대신 쓰인다.
/// </summary>
public sealed class BattleInfoHud : MonoBehaviour
{
    [Header("Top center")]
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI leftCount;
    [SerializeField] private TextMeshProUGUI rightCount;
    [SerializeField] private RectTransform contestTrack;
    [SerializeField] private Image leftBar;
    [SerializeField] private Image rightBar;
    [SerializeField] private RectTransform clashMark;

    [Header("Top right")]
    [SerializeField] private GameObject speed1xArt;
    [SerializeField] private GameObject speed2xArt;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button speed1xButton;
    [SerializeField] private Button speed2xButton;

    [Header("Wave call")]
    [SerializeField] private Button waveButton;
    [SerializeField] private GameObject waveDefaultArt;
    [SerializeField] private GameObject waveFocusArt;
    [SerializeField] private GameObject waveDisabledArt;
    [SerializeField] private TextMeshProUGUI waveCaption;

    private bool waveHovered;
    private bool wasInCombat;
    private float combatSeconds;
    private int shownSeconds = -1;
    private int shownStage = -1;
    private int shownLeft = -1;
    private int shownRight = -1;

    /// <summary>버튼 동작을 연결한다. 씬을 여는 쪽(VoxelDefensePrototype)이 한 번 호출한다.</summary>
    public void Bind(UnityAction callWave, Action<int> setSpeed, UnityAction openSettings)
    {
        waveCaption.text = GameText.Format("hud.next_wave", GameText.Get("key.space"));
        waveButton.onClick.AddListener(callWave);
        speed1xButton.onClick.AddListener(() => setSpeed(1));
        speed2xButton.onClick.AddListener(() => setSpeed(2));
        settingsButton.onClick.AddListener(openSettings);
        var events = waveButton.gameObject.AddComponent<EventTrigger>();
        AddTrigger(events, EventTriggerType.PointerEnter, () => waveHovered = true);
        AddTrigger(events, EventTriggerType.PointerExit, () => waveHovered = false);
    }

    /// <summary>매 프레임 전투 상태를 넘긴다.</summary>
    public void Refresh(int stage, bool inCombat, bool canCallWave, int left, int right,
        Color leftColor, Color rightColor, int battleSpeed)
    {
        if (stage != shownStage)
        {
            stageText.text = GameText.Format("hud.wave", stage);
            shownStage = stage;
        }

        if (inCombat && !wasInCombat)
            combatSeconds = 0f;
        if (inCombat)
            combatSeconds += Time.unscaledDeltaTime * Mathf.Max(1, battleSpeed);
        wasInCombat = inCombat;
        int seconds = Mathf.FloorToInt(combatSeconds);
        if (seconds != shownSeconds)
        {
            timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            shownSeconds = seconds;
        }

        if (left != shownLeft || right != shownRight)
        {
            leftCount.text = left.ToString();
            rightCount.text = right.ToString();
            shownLeft = left;
            shownRight = right;
        }
        leftBar.color = leftColor;
        rightBar.color = rightColor;
        int total = left + right;
        float share = total > 0 ? (float)left / total : 0.5f;
        float width = contestTrack.rect.width;
        leftBar.rectTransform.sizeDelta = new Vector2(width * share, leftBar.rectTransform.sizeDelta.y);
        rightBar.rectTransform.sizeDelta = new Vector2(width * (1f - share), rightBar.rectTransform.sizeDelta.y);
        clashMark.anchoredPosition = new Vector2(width * share, clashMark.anchoredPosition.y);

        speed1xArt.SetActive(battleSpeed != 2);
        speed2xArt.SetActive(battleSpeed == 2);

        waveButton.interactable = canCallWave;
        waveDisabledArt.SetActive(!canCallWave);
        waveFocusArt.SetActive(canCallWave && waveHovered);
        waveDefaultArt.SetActive(canCallWave && !waveHovered);
        waveCaption.color = canCallWave ? UIPalette.TextPrimary : UIPalette.TextDisabled;
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }
}
