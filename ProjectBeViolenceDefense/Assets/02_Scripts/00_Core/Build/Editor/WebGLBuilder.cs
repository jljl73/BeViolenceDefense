using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// WebGL 빌드. 메뉴 또는 CLI(-executeMethod WebGLBuilder.Build)로 실행한다.
// 빌드 동안만 WebGL 전용 설정을 바꾸고 끝나면 원래 PlayerSettings 값으로 되돌린다.
public static class WebGLBuilder
{
    private const string OutputPath = "Builds/WebGL";
    private const string DevelopmentOutputPath = "Builds/WebGL-Dev";

    [MenuItem("Tools/Build/WebGL")]
    public static void Build() => BuildWith(development: false);

    // 오류 추적용: Development 빌드 + 전체 예외 스택. 용량이 크고 느리므로 배포에 쓰지 않는다.
    [MenuItem("Tools/Build/WebGL (Development)")]
    public static void BuildDevelopment() => BuildWith(development: true);

    private static void BuildWith(bool development)
    {
        var compression = PlayerSettings.WebGL.compressionFormat;
        var fallback = PlayerSettings.WebGL.decompressionFallback;
        var exceptions = PlayerSettings.WebGL.exceptionSupport;
        bool succeeded = false;

        try
        {
            // Gzip + 압축 해제 대체: 서버의 Content-Encoding 설정 없이도 대부분의 호스팅(itch.io 등)에서 열린다.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = development
                ? WebGLExceptionSupport.FullWithStacktrace
                : WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = development ? DevelopmentOutputPath : OutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[WebGLBuilder] {summary.result} / {summary.totalSize / (1024f * 1024f):F1} MB / {summary.totalTime} / errors {summary.totalErrors}");
            succeeded = summary.result == BuildResult.Succeeded;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = fallback;
            PlayerSettings.WebGL.exceptionSupport = exceptions;
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(succeeded ? 0 : 1);
    }

    // 큰 2D 일러스트(긴 변 1024px 이상: 초상·스토리·타이틀)는 WebGL에서만 Crunch 압축해 다운로드 용량을 줄인다. 해상도는 유지한다.
    private const string SpriteArtFolder = "Assets/05_Art/00_Sprites";
    private const int CrunchQuality = 75;
    private const int MinCrunchSize = 1024;

    [MenuItem("Tools/Build/Apply WebGL Texture Overrides")]
    public static void ApplyTextureOverrides()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteArtFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                continue;
            // 작은 UI 아이콘은 용량 이득이 적고 Crunch 손실이 눈에 띄어 제외한다.
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            TextureImporterPlatformSettings webgl = importer.GetPlatformTextureSettings("WebGL");
            if (Mathf.Max(width, height) < MinCrunchSize)
            {
                // 이전에 이 메뉴가 넣은 설정이면 지운다.
                if (webgl.overridden && webgl.crunchedCompression && webgl.compressionQuality == CrunchQuality)
                {
                    importer.ClearPlatformTextureSettings("WebGL");
                    importer.SaveAndReimport();
                }
                continue;
            }

            TextureImporterPlatformSettings fallback = importer.GetDefaultPlatformTextureSettings();
            if (webgl.overridden && webgl.crunchedCompression && webgl.compressionQuality == CrunchQuality)
                continue;

            webgl.overridden = true;
            webgl.maxTextureSize = fallback.maxTextureSize;
            webgl.resizeAlgorithm = fallback.resizeAlgorithm;
            webgl.format = TextureImporterFormat.Automatic;
            webgl.textureCompression = TextureImporterCompression.Compressed;
            webgl.crunchedCompression = true;
            webgl.compressionQuality = CrunchQuality;
            importer.SetPlatformTextureSettings(webgl);
            importer.SaveAndReimport();
            changed++;
        }
        Debug.Log($"[WebGLBuilder] WebGL texture overrides applied: {changed}");

        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }
}
