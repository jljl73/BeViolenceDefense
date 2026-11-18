using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyCatalog))]
internal sealed class EnemyCatalogEditor : Editor
{
    private const float StatWidth = 38f;
    private static readonly (string Property, GUIContent Label)[] Columns =
    {
        ("maxHealth", new GUIContent("체력", "최대 체력")),
        ("damage", new GUIContent("공격", "공격 1회의 피해량")),
        ("attackInterval", new GUIContent("간격", "공격 간격(초)")),
        ("attackRange", new GUIContent("사거리", "공격 사거리(칸)")),
        ("awarenessRange", new GUIContent("인지", "인지 사거리(칸). 실제 인지 사거리는 공격 사거리 이상입니다.")),
        ("moveSpeed", new GUIContent("속도", "이동 속도(칸/초)")),
        ("armor", new GUIContent("방어", "공격 1회마다 줄이는 피해. 공격력 이하가 되면 피해는 0입니다."))
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty entries = serializedObject.FindProperty("entries");

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("유닛", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            foreach (var column in Columns)
                EditorGUILayout.LabelField(column.Label, GUILayout.Width(StatWidth));
        }

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string displayName = entry.FindPropertyRelative("displayName").stringValue;
            using (new EditorGUILayout.HorizontalScope())
            {
                entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded,
                    string.IsNullOrWhiteSpace(displayName) ? "새 유닛" : displayName, true, EditorStyles.foldout);
                foreach (var column in Columns)
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative(column.Property),
                        GUIContent.none, GUILayout.Width(StatWidth));
            }

            if (entry.isExpanded && DrawDetails(entry))
            {
                entries.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        if (GUILayout.Button("유닛 추가"))
        {
            int index = entries.arraySize;
            entries.arraySize++;
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.isExpanded = true;
            entry.FindPropertyRelative("id").stringValue = "";
            entry.FindPropertyRelative("displayName").stringValue = "";
            entry.FindPropertyRelative("model").objectReferenceValue = null;
            entry.FindPropertyRelative("animations").objectReferenceValue = null;
            entry.FindPropertyRelative("skin").objectReferenceValue = null;
            entry.FindPropertyRelative("modelScale").floatValue = 1f;
            entry.FindPropertyRelative("maxHealth").intValue = 50;
            entry.FindPropertyRelative("damage").intValue = 10;
            entry.FindPropertyRelative("attackInterval").floatValue = 0.65f;
            entry.FindPropertyRelative("attackRange").floatValue = 1.5f;
            entry.FindPropertyRelative("awarenessRange").floatValue = 2.2f;
            entry.FindPropertyRelative("moveSpeed").floatValue = 1.7f;
            entry.FindPropertyRelative("armor").intValue = 0;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool DrawDetails(SerializedProperty entry)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("id"), new GUIContent("유닛 ID"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("displayName"), new GUIContent("이름"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("model"), new GUIContent("모델"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("animations"),
                new GUIContent("컨트롤러 오버라이드", "비어 있으면 모델에 연결된 애니메이터 컨트롤러를 사용합니다."));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("skin"),
                new GUIContent("머티리얼 오버라이드", "비어 있으면 모델의 기존 머티리얼을 사용합니다."));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("modelScale"), new GUIContent("모델 배율"));
            return GUILayout.Button("이 유닛 삭제");
        }
    }
}
