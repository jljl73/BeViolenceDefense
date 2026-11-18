using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 에셋 참조를 묶어서 관리하는 단위. Dispose 하면 이 스코프로 로드한 에셋 참조가 모두 해제된다.
///
/// <code>
/// // 컴포넌트가 파괴될 때 자동 해제
/// private AssetScope assets;
/// private void Awake() => assets = AssetLoader.CreateScope(this);
///
/// var enemy = await assets.InstantiateAsync&lt;EnemyUnit&gt;(AssetPaths.Prefabs.Enemy, transform);
/// var icons = await assets.LoadFolderAsync&lt;Sprite&gt;(AssetPaths.Sprites.Icons.Folder);
/// </code>
/// </summary>
public sealed class AssetScope : IDisposable
{
    private readonly List<(string Path, Type Type)> handles = new();
    private readonly CancellationTokenSource lifetime = new();

    internal AssetScope(string name) => Name = name;

    public string Name { get; }
    public bool IsDisposed { get; private set; }

    /// <summary>이 스코프가 잡고 있는 참조 수.</summary>
    public int Count => handles.Count;

    /// <summary>스코프가 해제되면 취소되는 토큰. 이 스코프에 묶인 다른 비동기 작업에 넘겨 쓰면 된다.</summary>
    public CancellationToken Token => lifetime.Token;

    public T Load<T>(string path) where T : Object
    {
        ThrowIfDisposed();
        var asset = AssetLoader.Load(path, typeof(T), logMissing: true);
        if (asset == null)
            return null;

        handles.Add((AssetLoader.Normalize(path), typeof(T)));
        return (T)asset;
    }

    public UniTask<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
        => LoadAsyncCore<T>(path, logMissing: true, cancellationToken);

    /// <summary>폴더 안에서 T 타입인 에셋만 모두 로드한다. 다른 타입 파일은 건너뛴다.</summary>
    public T[] LoadFolder<T>(string folder, bool recursive = false) where T : Object
    {
        ThrowIfDisposed();
        var paths = GetFolderPaths(folder, recursive);
        var result = new List<T>(paths.Count);
        foreach (var path in paths)
        {
            var asset = AssetLoader.Load(path, typeof(T), logMissing: false);
            if (asset == null)
                continue;

            handles.Add((path, typeof(T)));
            result.Add((T)asset);
        }
        return result.ToArray();
    }

    /// <summary>폴더 안에서 T 타입인 에셋만 병렬로 모두 로드한다. 다른 타입 파일은 건너뛴다.</summary>
    public async UniTask<T[]> LoadFolderAsync<T>(string folder, bool recursive = false, CancellationToken cancellationToken = default) where T : Object
    {
        ThrowIfDisposed();
        var paths = GetFolderPaths(folder, recursive);
        var tasks = new UniTask<T>[paths.Count];
        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = LoadAsyncCore<T>(paths[i], logMissing: false, cancellationToken);

        var loaded = await UniTask.WhenAll(tasks);
        var result = new List<T>(loaded.Length);
        foreach (var asset in loaded)
            if (asset != null)
                result.Add(asset);
        return result.ToArray();
    }

    public async UniTask<GameObject> InstantiateAsync(string path, Transform parent = null, CancellationToken cancellationToken = default)
    {
        var prefab = await LoadAsync<GameObject>(path, cancellationToken);
        return prefab != null ? Object.Instantiate(prefab, parent) : null;
    }

    public async UniTask<T> InstantiateAsync<T>(string path, Transform parent = null, CancellationToken cancellationToken = default) where T : Component
    {
        var prefab = await LoadAsync<T>(path, cancellationToken);
        return prefab != null ? Object.Instantiate(prefab, parent) : null;
    }

    /// <summary>스코프를 유지한 채 에셋 하나의 참조만 해제한다.</summary>
    public void Release<T>(string path) where T : Object
    {
        path = AssetLoader.Normalize(path);
        for (var i = handles.Count - 1; i >= 0; i--)
        {
            if (handles[i].Type != typeof(T) || handles[i].Path != path)
                continue;

            handles.RemoveAt(i);
            AssetLoader.Release(path, typeof(T));
            return;
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        lifetime.Cancel();
        foreach (var (path, type) in handles)
            AssetLoader.Release(path, type);
        handles.Clear();
    }

    private async UniTask<T> LoadAsyncCore<T>(string path, bool logMissing, CancellationToken cancellationToken) where T : Object
    {
        ThrowIfDisposed();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        var asset = await AssetLoader.LoadAsync(path, typeof(T), logMissing, linked.Token);
        if (asset == null)
            return null;

        path = AssetLoader.Normalize(path);
        if (IsDisposed)
        {
            AssetLoader.Release(path, typeof(T));
            throw new OperationCanceledException(lifetime.Token);
        }

        handles.Add((path, typeof(T)));
        return (T)asset;
    }

    private static IReadOnlyList<string> GetFolderPaths(string folder, bool recursive)
    {
        var paths = AssetLoader.GetPaths(folder, recursive);
        if (paths.Count == 0)
            Debug.LogWarning($"[AssetLoader] 폴더에 에셋이 없습니다: Resources/{AssetLoader.Normalize(folder)} (AssetPaths 가 최신인지 확인하세요)");
        return paths;
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException($"AssetScope({Name})");
    }
}
