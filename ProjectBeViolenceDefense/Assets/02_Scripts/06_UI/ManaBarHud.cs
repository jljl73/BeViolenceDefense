using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하단 마나 바: 빈 홈 위에 채움 그림을 RectMask2D로 잘라 왼쪽부터 보여 준다. 마나를 쓰면 줄어든 만큼 잠시 잔상이 남는다.
/// 배치는 ManaBarHud.prefab에 있고, DefenseHud가 전투 스킬 바 아래(기존 마나 게이지 자리)로 옮겨 붙인다.
/// VoxelDefensePrototype에 이 컴포넌트가 연결된 씬에서만 기존 마나 게이지 대신 쓰인다.
/// </summary>
public sealed class ManaBarHud : MonoBehaviour
{
    [SerializeField] private RectTransform fillClip;
    [SerializeField] private RectTransform trailClip;
    [SerializeField] private float fillWidth = 457f;
    [SerializeField] private TextMeshProUGUI manaText;

    [Header("Spend trail (unscaled seconds)")]
    [SerializeField] private float trailDelay = 0.25f;
    [SerializeField] private float trailSpeed = 0.8f;

    private float shownRatio = -1f;
    private float trailRatio;
    private float trailHoldUntil;
    private int shownMana = -1;
    private int shownMax = -1;

    /// <summary>매 프레임 현재 마나를 넘긴다.</summary>
    public void Refresh(float mana, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(mana / max) : 0f;
        if (shownRatio >= 0f && ratio < shownRatio - 0.01f)
            trailHoldUntil = Time.unscaledTime + trailDelay;
        if (shownRatio < 0f || ratio > trailRatio)
            trailRatio = ratio;
        shownRatio = ratio;

        int whole = Mathf.FloorToInt(mana), maxWhole = Mathf.RoundToInt(max);
        if (manaText != null && (whole != shownMana || maxWhole != shownMax))
        {
            manaText.text = $"{whole} <size=75%>/ {maxWhole}</size>";
            shownMana = whole;
            shownMax = maxWhole;
        }
    }

    private void Update()
    {
        if (shownRatio < 0f)
            return;
        if (Time.unscaledTime >= trailHoldUntil)
            trailRatio = Mathf.MoveTowards(trailRatio, shownRatio, trailSpeed * Time.unscaledDeltaTime);
        SetClip(fillClip, shownRatio);
        SetClip(trailClip, Mathf.Max(trailRatio, shownRatio));
    }

    private void SetClip(RectTransform clip, float ratio)
    {
        if (clip == null)
            return;
        clip.gameObject.SetActive(ratio > 0f);
        clip.sizeDelta = new Vector2(fillWidth * ratio, clip.sizeDelta.y);
    }
}
