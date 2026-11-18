using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageWaves))]
internal sealed class StageWavesEditor : Editor
{
    private bool showRaw;

    public override void OnInspectorGUI()
    {
        var waves = (StageWaves)target;
        EditorGUILayout.LabelField($"{waves.waves?.Count ?? 0}개 웨이브", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("팩션마다 후보 그룹 하나를 선택합니다. 전용 편집기에서 유닛을 검색하고, 순서와 수량을 편집하세요.", MessageType.Info);
        if (GUILayout.Button("웨이브 편집기 열기", GUILayout.Height(32))) StageWavesEditorWindow.Open(waves);
        string error = waves.Validate();
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        showRaw = EditorGUILayout.Foldout(showRaw, "원본 데이터 (고급)", true);
        if (showRaw)
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                DrawDefaultInspector();
    }
}
