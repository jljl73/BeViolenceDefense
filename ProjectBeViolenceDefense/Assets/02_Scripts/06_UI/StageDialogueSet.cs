using System;
using UnityEngine;

/// <summary>
/// 스테이지별 대화 묶음: 입장 직후(전투 전)와 승리 직후(결과 창 전)에 대화창으로 재생한다.
/// 배열 순서는 스테이지 번호(1번부터). 대사 문구는 텍스트 테이블 키로 연결한다.
/// </summary>
[CreateAssetMenu(menuName = "Be폭력 디펜스/Stage Dialogue Set", fileName = "StageDialogues")]
public sealed class StageDialogueSet : ScriptableObject
{
    [Serializable]
    public struct Stage
    {
        public DialogueHud.Line[] intro;
        public DialogueHud.Line[] outro;
    }

    [SerializeField] private Stage[] stages = Array.Empty<Stage>();

    public DialogueHud.Line[] Intro(int stageNumber) => Has(stageNumber) ? stages[stageNumber - 1].intro : null;
    public DialogueHud.Line[] Outro(int stageNumber) => Has(stageNumber) ? stages[stageNumber - 1].outro : null;

    private bool Has(int stageNumber) => stageNumber >= 1 && stageNumber <= stages.Length;
}
