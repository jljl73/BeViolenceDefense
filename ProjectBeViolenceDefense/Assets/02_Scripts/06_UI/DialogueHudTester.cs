using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 대화창 UI 테스트용. T 키를 누르면 예시 대사를 재생한다.
/// 실제 대화 흐름을 연결하면 이 컴포넌트는 씬에서 지운다.
/// </summary>
public sealed class DialogueHudTester : MonoBehaviour
{
    [SerializeField] private DialogueHud dialogue;
    [SerializeField] private DialogueHud.Line[] lines =
    {
        new() { speaker = "Mage", expression = DialogueHud.Expression.Neutral, text = "Two armies are marching on the core..." },
        new() { speaker = "Mage", expression = DialogueHud.Expression.Troubled, text = "I can't fight either of them.\nNot even a little." },
        new() { speaker = "Mage", expression = DialogueHud.Expression.Surprised, text = "Wait. They hate each other more than they hate me?" },
        new() { speaker = "Mage", expression = DialogueHud.Expression.Happy, text = "If I can make them meet, they will wear each other down!" },
    };

    private void Update()
    {
        if (dialogue != null && !dialogue.IsOpen && Keyboard.current?.tKey.wasPressedThisFrame == true)
            dialogue.Play(lines);
    }
}
