using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 진영 = 종족(4개). 스폰이 있는 종족만 나오고, 서로 다른 종족끼리는 모두 적대한다.
public sealed class FourFactionBattleTests
{
    private static EnemyFaction[] Sides() => Enumerable.Range(0, StageData.FactionCount)
        .Select(i => new EnemyFaction(StageData.FactionNames[i], StageData.FactionColors[i], spawnCount: 0))
        .ToArray();

    private static IReadOnlyList<EnemyFaction>[] Lineup(EnemyFaction[] sides) => sides
        .Select(side => (IReadOnlyList<EnemyFaction>)new List<EnemyFaction> { side, side })
        .ToArray();

    private static DefenseGrid Grid(params int[] spawnFactions)
    {
        // 서로 인지 거리 안에 붙여 둔 스폰. 열린 보드에서도 종족끼리 바로 만난다.
        var cells = new[] { new Vector2Int(7, 2), new Vector2Int(9, 2), new Vector2Int(7, 4), new Vector2Int(9, 4) };
        return new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2), cells.Take(spawnFactions.Length).ToArray(),
            spawnFactions, null, cellSize: 1f);
    }

    [Test]
    public void AllFourRacesSpawnAndFightEachOther()
    {
        EnemyFaction[] sides = Sides();
        var battle = new DefenseBattle(Grid(0, 1, 2, 3), sides, _ => Lineup(sides));
        Assert.That(battle.EnemiesPerRound, Is.EqualTo(8));

        battle.StartRound();
        var attacked = new HashSet<(int, int)>();
        for (int t = 0; t < 60 * 120 && battle.CurrentPhase == DefenseBattle.Phase.Combat; t++)
        {
            battle.Tick(DefenseBattle.TickDuration, pauseMovement: false);
            foreach (EnemyUnit enemy in battle.Enemies)
                if (enemy.Target != null)
                {
                    Assert.That(enemy.Target.FactionIndex, Is.Not.EqualTo(enemy.FactionIndex), "같은 종족을 노렸다.");
                    attacked.Add((enemy.FactionIndex, enemy.Target.FactionIndex));
                }
        }

        Assert.That(battle.SpawnCount, Is.EqualTo(8));
        Assert.That(attacked, Is.Not.Empty, "종족 간 교전이 없었다.");
    }

    [Test]
    public void RacesWithoutSpawnAreSkipped()
    {
        // Stage1 처럼 고블린(0)·언데드(1) 스폰만 있는 스테이지.
        EnemyFaction[] sides = Sides();
        var battle = new DefenseBattle(Grid(0, 1), sides, _ => Lineup(sides));
        Assert.That(battle.EnemiesPerRound, Is.EqualTo(4));
        Assert.That(battle.RoundSpawnCount(2), Is.Zero);
        Assert.That(battle.RoundSpawnCount(3), Is.Zero);

        battle.StartRound();
        for (int t = 0; t < 60 * 120 && battle.CurrentPhase == DefenseBattle.Phase.Combat; t++)
            battle.Tick(DefenseBattle.TickDuration, pauseMovement: false);
        Assert.That(battle.CurrentPhase, Is.Not.EqualTo(DefenseBattle.Phase.Combat), "라운드가 끝나지 않았다.");
    }
}
