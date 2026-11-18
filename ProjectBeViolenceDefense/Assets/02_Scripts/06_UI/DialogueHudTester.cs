using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 대화창 UI 테스트용. T 키를 누르면 예시 대사를 재생한다.
/// 실제 대화 흐름을 연결하면 이 컴포넌트는 씬에서 지운다.
/// </summary>
public sealed class DialogueHudTester : MonoBehaviour
{
    [SerializeField] private DialogueHud dialogue;
    private static readonly DialogueHud.Line[] Intro =
    {
        new() { speakerKey = "speaker.wizard", expression = DialogueHud.Expression.Neutral, textKey = "dialogue.intro.1" },
        new() { speakerKey = "speaker.wizard", expression = DialogueHud.Expression.Troubled, textKey = "dialogue.intro.2" },
        new() { speakerKey = "speaker.wizard", expression = DialogueHud.Expression.Surprised, textKey = "dialogue.intro.3" },
        new() { speakerKey = "speaker.wizard", expression = DialogueHud.Expression.Happy, textKey = "dialogue.intro.4" },
        new() { speakerKey = "speaker.wizard", expression = DialogueHud.Expression.Teary, textKey = "dialogue.intro.5" },
        new() { speakerKey = "speaker.wizard", expression = DialogueHud.Expression.Shock, textKey = "dialogue.intro.6" },
        new() { speakerKey = "speaker.wizard", expression = DialogueHud.Expression.Breakdown, textKey = "dialogue.intro.7" },
    };

    private void Update()
    {
        if (dialogue != null && !dialogue.IsOpen && Keyboard.current?.tKey.wasPressedThisFrame == true)
            dialogue.Play(Intro);
    }
}
