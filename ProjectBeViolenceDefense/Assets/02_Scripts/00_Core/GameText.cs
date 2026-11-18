using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>플레이어에게 표시하는 문구를 텍스트 테이블의 키로 찾는다.</summary>
public static class GameText
{
    private const string ResourcePath = "GameText.ko";

    [Serializable]
    private sealed class Table
    {
        public Entry[] entries = Array.Empty<Entry>();
    }

    [Serializable]
    private struct Entry
    {
        public string key;
        public string text;
    }

    private static Dictionary<string, string> entries;
    private static readonly HashSet<string> reportedKeys = new(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        entries = null;
        reportedKeys.Clear();
    }

    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        entries ??= Load();
        if (entries.TryGetValue(key, out string text))
            return text;

        Report(key, $"텍스트 테이블에 '{key}' 키가 없습니다.");
        return key;
    }

    public static string Format(string key, params object[] arguments)
    {
        try
        {
            return string.Format(Get(key), arguments);
        }
        catch (FormatException exception)
        {
            Report(key, $"텍스트 테이블의 '{key}' 서식이 잘못됐습니다. {exception.Message}");
            return key;
        }
    }

    private static Dictionary<string, string> Load()
    {
        var results = new Dictionary<string, string>(StringComparer.Ordinal);
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogError($"텍스트 테이블을 찾을 수 없습니다. ({ResourcePath})");
            return results;
        }

        Table table;
        try
        {
            table = JsonUtility.FromJson<Table>(asset.text);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError($"텍스트 테이블을 읽을 수 없습니다. {exception.Message}", asset);
            return results;
        }

        foreach (Entry entry in table?.entries ?? Array.Empty<Entry>())
        {
            if (string.IsNullOrWhiteSpace(entry.key) || entry.text == null || !results.TryAdd(entry.key, entry.text))
                Debug.LogError($"텍스트 테이블에 비어 있거나 중복된 항목이 있습니다. ({entry.key})", asset);
        }
        return results;
    }

    private static void Report(string key, string message)
    {
        if (reportedKeys.Add(key))
            Debug.LogError(message);
    }
}
