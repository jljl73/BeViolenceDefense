using UnityEngine;

/// <summary>실제 클리어 기록과 입장 제한 해제를 분리해서 저장한다. 스킬도 이 클리어 기록을 읽는다.</summary>
internal sealed class StageProgress
{
    internal const string DefaultPrefix = "BeViolenceDefense.Progress.";
    private readonly string prefix;
    private int clearedStages;
    public bool AllStagesUnlocked { get; private set; }

    // 테스트는 별도 접두사를 사용해 실제 플레이어의 저장 데이터를 건드리지 않는다.
    public StageProgress(string preferencePrefix = DefaultPrefix)
    {
        prefix = preferencePrefix;
        if (!PlayerPrefs.HasKey(prefix + "ClearedStages"))
        {
            // 구 버전은 2·3·4번 승리만 1 << stageNumber로 저장했다.
            // 기록되지 않은 스테이지를 클리어했다고 추정하지 않는다.
            int legacy = PlayerPrefs.GetInt(prefix + "SpellStagesCleared", 0);
            PlayerPrefs.SetInt(prefix + "ClearedStages", (legacy & 0x1c) >> 1);
            PlayerPrefs.Save();
        }
        clearedStages = PlayerPrefs.GetInt(prefix + "ClearedStages", 0) & 0x3fffffff;
        AllStagesUnlocked = PlayerPrefs.GetInt(prefix + "AllStagesUnlocked", 0) == 1;
    }

    public bool IsCleared(int stageNumber) => Valid(stageNumber) && (clearedStages & (1 << (stageNumber - 1))) != 0;

    public int UnlockedStageCount(int stageCount)
    {
        stageCount = Mathf.Clamp(stageCount, 0, 30);
        if (AllStagesUnlocked) return stageCount;
        int open = Mathf.Min(1, stageCount);
        for (int stage = 1; stage <= stageCount; stage++)
            if (IsCleared(stage)) open = Mathf.Min(stage + 1, stageCount);
        return open;
    }

    public bool IsUnlocked(int stageNumber, int stageCount) => Valid(stageNumber) && stageNumber <= UnlockedStageCount(stageCount);

    public int CurrentStage(int stageCount)
    {
        int open = UnlockedStageCount(stageCount);
        for (int stage = 1; stage <= open; stage++)
            if (!IsCleared(stage)) return stage;
        return open;
    }

    public void RecordClear(int stageNumber)
    {
        if (!Valid(stageNumber) || IsCleared(stageNumber)) return;
        clearedStages |= 1 << (stageNumber - 1);
        Save();
    }

    public void UnlockAllStages()
    {
        AllStagesUnlocked = true;
        Save();
    }

    public void Reset()
    {
        clearedStages = 0;
        AllStagesUnlocked = false;
        PlayerPrefs.DeleteKey(prefix + "SpellStagesCleared");
        // 0을 저장해 구 버전 기록이 다시 이관되지 않도록 한다. 오디오 등 다른 설정은 보존한다.
        Save();
    }

    private void Save()
    {
        PlayerPrefs.SetInt(prefix + "ClearedStages", clearedStages);
        PlayerPrefs.SetInt(prefix + "AllStagesUnlocked", AllStagesUnlocked ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static bool Valid(int stageNumber) => stageNumber is >= 1 and <= 30;
}
