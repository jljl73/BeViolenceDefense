using UnityEngine;

internal sealed class StageSpellUnlocks
{
    private const string ClearedStagesKey = "BeViolenceDefense.Progress.SpellStagesCleared";
    private int clearedStages = PlayerPrefs.GetInt(ClearedStagesKey);

    public int MissingStage(DefenseBattle.Spell spell)
    {
        int stage = spell switch
        {
            DefenseBattle.Spell.MoveSpeedBuff => 2,
            DefenseBattle.Spell.ArmorBuff => 3,
            DefenseBattle.Spell.Taunt => 4,
            _ => 0
        };
        return (clearedStages & (1 << stage)) == 0 ? stage : 0;
    }

    public void RecordClear(int stageNumber)
    {
        if (stageNumber is < 2 or > 4)
            return;

        int updated = clearedStages | (1 << stageNumber);
        if (updated == clearedStages)
            return;

        clearedStages = updated;
        PlayerPrefs.SetInt(ClearedStagesKey, clearedStages);
        PlayerPrefs.Save();
    }
}
