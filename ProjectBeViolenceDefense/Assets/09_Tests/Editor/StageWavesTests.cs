using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 스테이지 웨이브 SO 규칙(팩션 2~4개, 팩션별 그룹 하나 선택)과 마지막 웨이브 클리어 시 승리.
public sealed class StageWavesTests
{
    private static StageWaves.FactionEntry Faction(int faction, int groupCount) => new()
    {
        faction = faction,
        groups = Enumerable.Range(0, groupCount)
            .Select(g => new StageWaves.Group { note = $"{faction}-{g}" })
            .ToList()
    };

    private static StageWaves Waves(params StageWaves.FactionEntry[] factions)
    {
        var waves = ScriptableObject.CreateInstance<StageWaves>();
        waves.waves.Add(new StageWaves.Wave { factions = factions.ToList() });
        return waves;
    }

    [Test]
    public void ValidateAcceptsTwoToFourFactions()
    {
        Assert.That(Waves(Faction(0, 1), Faction(1, 2)).Validate(), Is.Null);
        Assert.That(Waves(Faction(0, 1), Faction(1, 1), Faction(2, 1), Faction(3, 1)).Validate(), Is.Null);
    }

    [Test]
    public void ValidateRejectsBadWaves()
    {
        Assert.That(ScriptableObject.CreateInstance<StageWaves>().Validate(), Is.Not.Null, "웨이브 0개");
        Assert.That(Waves(Faction(0, 1)).Validate(), Is.Not.Null, "팩션 1개");
        Assert.That(Waves(Faction(0, 1), Faction(1, 1), Faction(2, 1), Faction(3, 1), Faction(0, 1)).Validate(),
            Is.Not.Null, "팩션 5개");
        Assert.That(Waves(Faction(0, 1), Faction(0, 1)).Validate(), Is.Not.Null, "팩션 중복");
        Assert.That(Waves(Faction(0, 1), Faction(4, 1)).Validate(), Is.Not.Null, "팩션 범위");
        Assert.That(Waves(Faction(0, 1), Faction(1, 0)).Validate(), Is.Not.Null, "그룹 0개");
    }

    [Test]
    public void PickGroupsChoosesOneGroupPerFaction()
    {
        StageWaves waves = Waves(Faction(1, 3), Faction(3, 2));
        var asked = new List<int>();
        StageWaves.Group[] picked = waves.PickGroups(0, n =>
        {
            asked.Add(n);
            return n - 1;
        });

        Assert.That(asked, Is.EqualTo(new[] { 3, 2 }), "팩션마다 자기 그룹 수 안에서 한 번씩 고른다.");
        Assert.That(picked.Length, Is.EqualTo(StageData.FactionCount));
        Assert.That(picked[1].note, Is.EqualTo("1-2"));
        Assert.That(picked[3].note, Is.EqualTo("3-1"));
        Assert.That(picked[0], Is.Null, "웨이브에 없는 팩션");
        Assert.That(picked[2], Is.Null, "웨이브에 없는 팩션");
    }

    private static DefenseBattle Battle(int roundLimit)
    {
        EnemyFaction[] sides = Enumerable.Range(0, StageData.FactionCount)
            .Select(i => new EnemyFaction(StageData.FactionNames[i], StageData.FactionColors[i], spawnCount: 0))
            .ToArray();
        var cells = new[] { new Vector2Int(7, 2), new Vector2Int(9, 2) };
        var grid = new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2), cells, new[] { 0, 1 }, null);
        return new DefenseBattle(grid, sides, _ => sides
            .Select(side => (IReadOnlyList<EnemyFaction>)new List<EnemyFaction> { side, side })
            .ToArray(), roundLimit);
    }

    private static void PlayRound(DefenseBattle battle)
    {
        battle.StartRound();
        for (int t = 0; t < 60 * 120 && battle.CurrentPhase == DefenseBattle.Phase.Combat; t++)
            battle.Tick(DefenseBattle.TickDuration, pauseMovement: false);
    }

    [Test]
    public void ClearingTheLastWaveWins()
    {
        DefenseBattle battle = Battle(roundLimit: 2);
        PlayRound(battle);
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation));
        Assert.That(battle.Round, Is.EqualTo(2));

        PlayRound(battle);
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Victory));
        Assert.That(battle.Round, Is.EqualTo(2));
        Assert.That(battle.Enemies, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void CoreShieldAbsorbsOneArrivalPerWave(int firstWaveCount)
    {
        var invader = new EnemyFaction("Invader", Color.white);
        var grid = new DefenseGrid(7, 7, new RectInt(3, 5, 1, 1), new[] { new Vector2Int(3, 1) }, new[] { 0 }, null);
        var battle = new DefenseBattle(grid, new[] { invader }, round => new IReadOnlyList<EnemyFaction>[]
        {
            Enumerable.Repeat(invader, round == 1 ? firstWaveCount : 2).ToArray()
        }, roundLimit: 2);
        int arrivals = 0;
        int hits = 0;
        battle.EnemyRemoved += enemy => { if (enemy.ReachedCore) arrivals++; };
        battle.CoreHit += () => hits++;

        Assert.That(battle.HasCoreShield, Is.False);
        battle.StartRound();
        Assert.That(battle.HasCoreShield, Is.True);
        PlayRound(battle);
        Assert.That(arrivals, Is.EqualTo(firstWaveCount));
        Assert.That(hits, Is.Zero);
        Assert.That(battle.CoreHealth, Is.EqualTo(DefenseBattle.CoreMaxHealth));
        Assert.That(battle.HasCoreShield, Is.EqualTo(firstWaveCount == 0));

        battle.StartRound();
        Assert.That(battle.HasCoreShield, Is.True);
        PlayRound(battle);
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Victory));
        Assert.That(battle.HasCoreShield, Is.False);
        Assert.That(arrivals, Is.EqualTo(firstWaveCount + 2));
        Assert.That(hits, Is.EqualTo(1));
        Assert.That(battle.CoreHealth, Is.EqualTo(DefenseBattle.CoreMaxHealth - 1));
    }

    [Test]
    public void FactionWithSeveralSpawnsUsesOnlyThePickedOne()
    {
        EnemyFaction[] sides = Enumerable.Range(0, StageData.FactionCount)
            .Select(i => new EnemyFaction(StageData.FactionNames[i], StageData.FactionColors[i], spawnCount: 0))
            .ToArray();
        // 고블린(0) 스폰 2개, 언데드(1) 스폰 1개.
        var cells = new[] { new Vector2Int(3, 2), new Vector2Int(13, 2), new Vector2Int(8, 2) };
        var grid = new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2), cells, new[] { 0, 0, 1 }, null);
        var asked = new List<int>();
        var battle = new DefenseBattle(grid, sides, _ => sides
            .Select(side => (IReadOnlyList<EnemyFaction>)new List<EnemyFaction> { side, side, side })
            .ToArray(), pickSpawn: n =>
            {
                asked.Add(n);
                return 1;
            });

        Assert.That(asked, Is.EqualTo(new[] { 2 }), "스폰이 여럿인 진영에서만 고른다.");
        Vector3[][] routes = battle.RoundRoutes();
        Assert.That(routes[0], Is.Null, "고르지 않은 스폰");
        Assert.That(routes[1], Is.Not.Null);
        Assert.That(routes[2], Is.Not.Null, "스폰이 하나인 진영");

        var goblinStarts = new HashSet<Vector2Int>();
        battle.EnemySpawned += enemy =>
        {
            if (enemy.FactionIndex == 0)
                goblinStarts.Add(DefenseGrid.PositionToCell(enemy.Position));
        };
        PlayRound(battle);
        Assert.That(goblinStarts, Is.EqualTo(new HashSet<Vector2Int> { cells[1] }));
        Assert.That(asked, Is.EqualTo(new[] { 2, 2 }), "다음 라운드 준비 단계에서 다시 고른다.");
    }

    [Test]
    public void NoRoundLimitKeepsGoing()
    {
        DefenseBattle battle = Battle(roundLimit: 0);
        PlayRound(battle);
        Assert.That(battle.CurrentPhase, Is.EqualTo(DefenseBattle.Phase.Preparation));
        Assert.That(battle.Round, Is.EqualTo(2));
    }
}
