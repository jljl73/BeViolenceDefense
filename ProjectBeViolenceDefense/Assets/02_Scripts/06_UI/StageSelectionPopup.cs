using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageSelectionPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI subtitle;
    [SerializeField] private Button[] buttons;
    [SerializeField] private TextMeshProUGUI[] labels;

    public void Bind(StageContent[] stages, Action<int> select)
    {
        title.text = GameText.Get("stage_select.title");
        subtitle.text = GameText.Get("stage_select.subtitle");
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            bool available = i < stages.Length && stages[i] != null;
            buttons[i].gameObject.SetActive(available);
            buttons[i].onClick.RemoveAllListeners();
            if (!available)
                continue;
            labels[i].text = stages[i].DisplayName;
            buttons[i].onClick.AddListener(() => select(index));
        }
    }
}
