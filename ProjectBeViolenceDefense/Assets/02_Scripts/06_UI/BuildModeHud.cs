using System;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 벽 건설 모드 HUD: 화면 왼쪽 중간의 토글 버튼과, 건설 모드에서 하단 중앙으로 올라오는 건설 패널.
/// 건설 모드가 켜지면 하단의 기존 행동 UI(스킬·시작)를 아래로 내리고 건설 패널을 올린다. 움직임은 LitMotion으로 한다.
/// 배치는 BuildModeHud.prefab에 있고, VoxelDefensePrototype에 이 컴포넌트가 연결된 씬에서만 쓰인다.
/// </summary>
public sealed class BuildModeHud : MonoBehaviour
{
    public enum Tool { Place, Remove }

    [Header("Toggle (left middle)")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private CanvasGroup toggleGroup;
    [Tooltip("켜진 상태의 버튼 그림. 꺼진 그림 위에 겹쳐 두고 건설 모드일 때만 켠다.")]
    [SerializeField] private GameObject toggleOnArt;
    [SerializeField] private TextMeshProUGUI toggleCaption;

    [Header("Panel (bottom center)")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup panelGroup;
    [Tooltip("남은 벽 표시. 쓴 벽 그림 위에 겹친 남은 벽 그림들이며, 남은 개수만큼만 켠다.")]
    [SerializeField] private GameObject[] wallPips = Array.Empty<GameObject>();
    [SerializeField] private TextMeshProUGUI wallCount;
    [SerializeField] private Button placeButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private GameObject placeSelectedArt;
    [SerializeField] private GameObject removeSelectedArt;

    [Header("Motion (unscaled seconds)")]
    [SerializeField] private float openTime = 0.32f;
    [SerializeField] private float closeTime = 0.2f;
    [Tooltip("닫혔을 때 패널이 화면 아래로 내려가 있는 거리")]
    [SerializeField] private float hiddenOffset = 260f;

    private RectTransform replaced;
    private CanvasGroup replacedGroup;
    private float panelShownY;
    private float replacedShownY;
    private bool open;
    private bool initialized;
    private MotionHandle panelMove, panelFade, replacedMove, replacedFade, togglePop;

    /// <summary>버튼 동작을 연결한다. <paramref name="replacedBar"/>는 건설 모드에서 자리를 내주는 기존 하단 UI다.</summary>
    public void Bind(Action toggle, Action<Tool> selectTool, RectTransform replacedBar)
    {
        toggleButton.onClick.AddListener(() => toggle());
        placeButton.onClick.AddListener(() => selectTool(Tool.Place));
        removeButton.onClick.AddListener(() => selectTool(Tool.Remove));
        replaced = replacedBar;
        if (replaced != null)
        {
            replacedGroup = replaced.GetComponent<CanvasGroup>();
            if (replacedGroup == null)
                replacedGroup = replaced.gameObject.AddComponent<CanvasGroup>();
            replacedShownY = replaced.anchoredPosition.y;
        }
    }

    /// <summary>매 프레임 상태를 넘긴다. 열림 상태가 바뀌면 패널이 올라오거나 내려간다.</summary>
    public void Refresh(bool available, bool isOpen, int wallsLeft, int wallLimit, Tool tool)
    {
        if (!initialized)
        {
            panelShownY = panel.anchoredPosition.y;
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, panelShownY - hiddenOffset);
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
            initialized = true;
        }

        toggleButton.interactable = available;
        toggleOnArt.SetActive(isOpen);
        toggleGroup.alpha = available ? 1f : 0.5f;
        toggleCaption.color = !available ? UIPalette.TextDisabled : isOpen ? UIPalette.OutlineFocus : UIPalette.TextPrimary;

        for (int i = 0; i < wallPips.Length; i++)
            wallPips[i].SetActive(i < wallsLeft);
        wallCount.text = $"{wallsLeft} <size=70%>/ {wallLimit}</size>";
        placeSelectedArt.SetActive(tool == Tool.Place);
        removeSelectedArt.SetActive(tool == Tool.Remove);

        if (isOpen != open)
        {
            open = isOpen;
            Animate(isOpen);
        }
    }

    private void Animate(bool show)
    {
        Cancel();
        float time = show ? openTime : closeTime;
        panel.gameObject.SetActive(true);
        panelGroup.blocksRaycasts = show;
        panelMove = LMotion.Create(panel.anchoredPosition.y, show ? panelShownY : panelShownY - hiddenOffset, time)
            .WithEase(show ? Ease.OutBack : Ease.InCubic)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() => { if (!open) panel.gameObject.SetActive(false); })
            .BindToAnchoredPositionY(panel);
        panelFade = LMotion.Create(panelGroup.alpha, show ? 1f : 0f, time * 0.8f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAlpha(panelGroup);

        if (replaced != null)
        {
            // 기존 하단 UI는 반대로: 건설 패널이 올라올 때 내려가고, 닫히면 돌아온다.
            replacedGroup.blocksRaycasts = !show;
            replacedMove = LMotion.Create(replaced.anchoredPosition.y, show ? replacedShownY - hiddenOffset : replacedShownY, time)
                .WithEase(show ? Ease.InCubic : Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAnchoredPositionY(replaced);
            replacedFade = LMotion.Create(replacedGroup.alpha, show ? 0f : 1f, time * 0.8f)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAlpha(replacedGroup);
        }

        // 토글 버튼은 눌린 느낌으로 살짝 튄다.
        Transform button = toggleGroup.transform;
        button.localScale = Vector3.one * 0.9f;
        togglePop = LMotion.Create(button.localScale, Vector3.one, 0.3f)
            .WithEase(Ease.OutBack)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToLocalScale(button);
    }

    private void Cancel()
    {
        TryCancel(panelMove);
        TryCancel(panelFade);
        TryCancel(replacedMove);
        TryCancel(replacedFade);
        TryCancel(togglePop);
    }

    private static void TryCancel(MotionHandle handle)
    {
        if (handle.IsActive())
            handle.Cancel();
    }

    private void OnDestroy() => Cancel();
}
