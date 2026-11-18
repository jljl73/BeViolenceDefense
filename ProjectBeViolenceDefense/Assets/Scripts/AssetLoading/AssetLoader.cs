using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Resources 폴더 기반 에셋 로더.
/// 경로는 Resources 폴더 기준이며 확장자를 붙이지 않는다. (예: Assets/Resources/Prefabs/Enemy.prefab → "Prefabs/Enemy")
/// 경로 상수는 AssetPaths 에 자동 생성된다. (예: AssetPaths.Prefabs.Enemy)
///
/// 메모리 관리:
///  - 모든 로드는 AssetScope 를 통해 이루어지고, 같은 에셋은 한 번만 로드되어 참조 카운트로 공유된다.
///  - 스코프를 Dispose 하면 그 스코프가 잡고 있던 참조가 해제된다.
///  - 참조가 0 이 된 에셋은 캐시에서 빠지고, UnloadUnusedAsync() 나 씬 전환 시 실제 메모리에서 내려간다.
/// </summary>
public static class AssetLoader
{
    private const string ResourcesSegment = "/Resources/";

    private sealed class Entry
    {
        public Entry(string path, Type type)
        {
            Path = path;
            Type = type;
        }

        public readonly string Path;
        public readonly Type Type;
        public Object Asset;
        public int RefCount;
        public bool IsLoading;
        public UniTask<Object> Loading;
    }

    /// <summary>디버그용 캐시 스냅샷.</summary>
    public readonly struct LoadedAsset
    {
        public LoadedAsset(string path, Type type, Object asset, int refCount, bool isLoading)
        {
            Path = path;
            Type = type;
            Asset = asset;
            RefCount = refCount;
            IsLoading = isLoading;
        }

        public string Path { get; }
        public Type Type { get; }
        public Object Asset { get; }
        public int RefCount { get; }
        public bool IsLoading { get; }
    }

    private static readonly Dictionary<(string Path, Type Type), Entry> Entries = new();
    private static HashSet<string> knownPaths;
    private static AssetScope global;

    /// <summary>게임이 끝날 때까지 유지되는 스코프. 공용 UI, 폰트, 사운드 등에 사용.</summary>
    public static AssetScope Global => global == null || global.IsDisposed ? global = new AssetScope("Global") : global;

    // Enter Play Mode 옵션으로 도메인 리로드를 꺼도 이전 플레이의 캐시가 남지 않도록 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Entries.Clear();
        knownPaths = null;
        global = null;
    }

    /// <summary>수동으로 Dispose 해야 하는 스코프를 만든다.</summary>
    public static AssetScope CreateScope(string name = null) => new(name ?? "Scope");

    /// <summary>owner 가 파괴될 때 자동으로 해제되는 스코프를 만든다.</summary>
    public static AssetScope CreateScope(Component owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        var scope = new AssetScope(owner.name);
        owner.GetCancellationTokenOnDestroy().Register(scope.Dispose);
        return scope;
    }

    /// <summary>폴더 안의 에셋 경로 목록. folder 가 빈 문자열이면 Resources 최상위.</summary>
    public static IReadOnlyList<string> GetPaths(string folder, bool recursive = false)
    {
        folder = Normalize(folder);
        var prefix = folder.Length == 0 ? string.Empty : folder + "/";
        var result = new List<string>();
        foreach (var path in AssetPaths.All)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            if (!recursive && path.IndexOf('/', prefix.Length) >= 0)
                continue;
            result.Add(path);
        }
        return result;
    }

    /// <summary>AssetPaths 기준으로 에셋이 존재하는지 확인한다.</summary>
    public static bool Exists(string path)
    {
        knownPaths ??= new HashSet<string>(AssetPaths.All, StringComparer.Ordinal);
        return knownPaths.Contains(Normalize(path));
    }

    /// <summary>참조가 없는 에셋을 실제 메모리에서 내린다. 스테이지 전환, 큰 UI 닫기 직후 등에 호출.</summary>
    public static UniTask UnloadUnusedAsync() => Resources.UnloadUnusedAssets().ToUniTask();

    /// <summary>모든 참조를 강제로 끊는다. 게임 재시작처럼 전부 정리할 때만 사용.</summary>
    public static void ReleaseAll()
    {
        global?.Dispose();
        global = null;
        Entries.Clear();
    }

    public static IReadOnlyList<LoadedAsset> GetLoadedAssets()
    {
        var list = new List<LoadedAsset>(Entries.Count);
        foreach (var entry in Entries.Values)
            list.Add(new LoadedAsset(entry.Path, entry.Type, entry.Asset, entry.RefCount, entry.IsLoading));
        return list;
    }

    internal static Object Load(string path, Type type, bool logMissing)
    {
        var entry = Acquire(Normalize(path), type);
        if (entry.Asset == null)
            entry.Asset = Resources.Load(entry.Path, type);

        if (entry.Asset != null)
            return entry.Asset;

        if (logMissing)
            ReportMissing(entry);
        Release(entry);
        return null;
    }

    internal static async UniTask<Object> LoadAsync(string path, Type type, bool logMissing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = Acquire(Normalize(path), type);
        try
        {
            if (entry.Asset == null)
            {
                // 같은 에셋을 동시에 요청해도 Resources 로드는 한 번만 일어난다.
                if (!entry.IsLoading)
                {
                    entry.IsLoading = true;
                    entry.Loading = LoadFromResourcesAsync(entry).Preserve();
                }
                await entry.Loading.AttachExternalCancellation(cancellationToken);
            }
        }
        catch
        {
            Release(entry);
            throw;
        }

        if (entry.Asset != null)
            return entry.Asset;

        if (logMissing)
            ReportMissing(entry);
        Release(entry);
        return null;
    }

    internal static void Release(string path, Type type)
    {
        if (Entries.TryGetValue((Normalize(path), type), out var entry))
            Release(entry);
    }

    internal static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        path = path.Trim().Replace('\\', '/');

        // "Assets/Resources/Prefabs/Enemy.prefab" 처럼 프로젝트 경로를 그대로 넣어도 동작하게 한다.
        if (path.StartsWith("Assets/", StringComparison.Ordinal))
        {
            var index = path.LastIndexOf(ResourcesSegment, StringComparison.Ordinal);
            if (index >= 0)
            {
                path = path.Substring(index + ResourcesSegment.Length);
                var extension = System.IO.Path.GetExtension(path);
                if (extension.Length > 0)
                    path = path.Substring(0, path.Length - extension.Length);
            }
        }

        return path.Trim('/');
    }

    private static Entry Acquire(string path, Type type)
    {
        if (path.Length == 0)
            throw new ArgumentException("에셋 경로가 비어 있습니다.", nameof(path));

        var key = (path, type);
        if (!Entries.TryGetValue(key, out var entry))
        {
            entry = new Entry(path, type);
            Entries.Add(key, entry);
        }
        entry.RefCount++;
        return entry;
    }

    private static void Release(Entry entry)
    {
        if (--entry.RefCount > 0)
            return;

        // ResetStatics/ReleaseAll 이후 남은 옛 참조가 새 항목을 지우지 않도록 같은 객체인지 확인한다.
        var key = (entry.Path, entry.Type);
        if (Entries.TryGetValue(key, out var current) && current == entry)
            Entries.Remove(key);
    }

    private static async UniTask<Object> LoadFromResourcesAsync(Entry entry)
    {
        try
        {
            var asset = await Resources.LoadAsync(entry.Path, entry.Type);
            // 로딩 중에 동기 Load 가 먼저 끝났을 수 있다.
            if (entry.Asset == null)
                entry.Asset = asset;
            return entry.Asset;
        }
        finally
        {
            entry.IsLoading = false;
        }
    }

    private static void ReportMissing(Entry entry)
        => Debug.LogError($"[AssetLoader] 에셋을 찾을 수 없습니다: Resources/{entry.Path} ({entry.Type.Name})");
}
