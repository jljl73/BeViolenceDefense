internal sealed class StageSpellUnlocks
{
    private readonly StageProgress progress;

    public StageSpellUnlocks(StageProgress progress = null) => this.progress = progress ?? new StageProgress();

    public int MissingStage(DefenseBattle.Spell spell)
    {
        int stage = spell switch
        {
            DefenseBattle.Spell.AttackBuff => 2,
            DefenseBattle.Spell.ArmorBuff => 3,
            DefenseBattle.Spell.Taunt => 4,
            _ => 0
        };
        return progress.IsCleared(stage) ? 0 : stage;
    }

    public void RecordClear(int stageNumber) => progress.RecordClear(stageNumber);
}
