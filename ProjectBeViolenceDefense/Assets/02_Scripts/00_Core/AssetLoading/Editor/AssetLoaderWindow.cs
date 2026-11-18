using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>플레이 중 AssetLoader 가 들고 있는 에셋과 참조 수, 메모리를 보여준다.</summary>
internal sealed class AssetLoaderWindow : EditorWindow
{
    private Vector2 scroll;
    private string filter = string.Empty;

    [MenuItem("Tools/Asset Loader/Loaded Assets")]
    private static void Open() => GetWindow<AssetLoaderWindow>("Loaded Assets");

    private void OnInspectorUpdate()
    {
        if (Application.isPlaying)
            Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서 AssetLoader 가 들고 있는 에셋 목록이 표시됩니다.", MessageType.Info);
            return;
        }

        var assets = AssetLoader.GetLoadedAssets().OrderBy(a => a.Path, StringComparer.Ordinal).ToList();
        long total = 0;
        foreach (var asset in assets)
            if (asset.Asset != null)
                total += Profiler.GetRuntimeMemorySizeLong(asset.Asset);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label($"{assets.Count}개 · {EditorUtility.FormatBytes(total)}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.Width(180));
            if (GUILayout.Button("Unload Unused", EditorStyles.toolbarButton))
                AssetLoader.UnloadUnusedAsync().Forget();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var asset in assets)
        {
            if (filter.Length > 0 && asset.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(asset.Path, GUILayout.ExpandWidth(true));
                GUILayout.Label(asset.Type.Name, GUILayout.Width(100));
                GUILayout.Label(asset.IsLoading ? "로딩 중" : $"ref {asset.RefCount}", GUILayout.Width(60));
                GUILayout.Label(asset.Asset != null ? EditorUtility.FormatBytes(Profiler.GetRuntimeMemorySizeLong(asset.Asset)) : "-", GUILayout.Width(70));
                using (new EditorGUI.DisabledScope(asset.Asset == null))
                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
                        EditorGUIUtility.PingObject(asset.Asset);
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox("프리팹 크기는 프리팹 본체만 계산되며, 참조하는 텍스처·메시는 각 에셋을 따로 로드했을 때만 표시됩니다.", MessageType.None);
    }
}
