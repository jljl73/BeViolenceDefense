using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 전투 결정론 검사. 같은 편성·같은 틱의 마법이면 프레임 속도와 관계없이 틱마다 같은 상태여야 한다.
// 수치는 카탈로그와 무관하게 고정해서, 밸런스를 바꿔도 이 테스트는 결정론만 본다.
public sealed class SimulationDeterminismTests
{
    private static readonly Color CoralColor = new(1f, 0.45f, 0.34f);
    private static readonly Color TealColor = new(0.27f, 0.87f, 0.81f);

    private static IReadOnlyList<EnemyFaction>[] Lineup()
    {
        var grunt = new EnemyFaction("Grunt", CoralColor, 1.5f, 50, 10, 0.65f, 0, 2.2f, 1.7f, 0, "test-grunt", bodyRadius: 0.34f);
        var slinger = new EnemyFaction("Slinger", CoralColor, 3f, 35, 8, 0.8f, 0, 3.5f, 1.7f, 0, "test-slinger", bodyRadius: 0.28f);
        var zombie = new EnemyFaction("Zombie", TealColor, 1.5f, 90, 15, 1f, 0, 2.2f, 1.2f, 0, "test-zombie", bodyRadius: 0.31f);
        var armor = new EnemyFaction("Armor", TealColor, 1.5f, 180, 14, 1.2f, 0, 2.2f, 1.1f, 3, "test-armor", bodyRadius: 0.48f);
        var coral = new List<EnemyFaction> { grunt, grunt, slinger, grunt, slinger, grunt };
        var teal = new List<EnemyFaction> { zombie, armor, zombie, zombie };
        return new IReadOnlyList<EnemyFaction>[] { coral, teal };
    }

    private static readonly DefenseBattle.SpellCommand[] Spells =
    {
        new(240, DefenseBattle.Spell.AttackBuff, new Vector3(8f, 0f, 9f), 0),
        new(600, DefenseBattle.Spell.Heal, new Vector3(8f, 0f, 10f), 0),
        new(420, DefenseBattle.Spell.Taunt, default, 0, targetSpawnOrder: 2),
    };

    private static DefenseBattle NewBattle()
    {
        var sides = new[] { new EnemyFaction("Coral", CoralColor), new EnemyFaction("Teal", TealColor) };
        IReadOnlyList<EnemyFaction>[] lineup = Lineup();
        var battle = new DefenseBattle(new DefenseGrid(), sides, _ => lineup);
        battle.StartRound();
        return battle;
    }

    // 게임 루프와 같은 방식: BattleClock 이 정한 수만큼 고정 틱을 돌린다.
    private static List<ulong> RunWithFrames(Func<int, (float Dt, int Speed, bool Aim)> frame,
        IEnumerable<DefenseBattle.SpellCommand> spells, out DefenseBattle battle)
    {
        battle = NewBattle();
        foreach (DefenseBattle.SpellCommand spell in spells)
            battle.ScheduleSpell(spell);

        var clock = new BattleClock();
        clock.Reset();
        var hashes = new List<ulong>();
        for (int i = 0; i < 400000 && battle.CurrentPhase == DefenseBattle.Phase.Combat; i++)
        {
            var (dt, speed, aim) = frame(i);
            clock.BeginFrame(dt, speed, aim);
            for (int t = 0; t < clock.StepCount && battle.CurrentPhase == DefenseBattle.Phase.Combat; t++)
            {
                battle.Tick(clock.StepDeltaTime, pauseMovement: false);
                hashes.Add(StateHash(battle));
            }
        }
        return hashes;
    }

    private static ulong StateHash(DefenseBattle battle)
    {
        ulong hash = 14695981039346656037UL;
        void Mix(long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                hash *= 1099511628211UL;
            }
        }

        Mix(battle.TickIndex);
        Mix(BitConverter.SingleToInt32Bits(battle.Mana));
        Mix(battle.SpawnCount);
        Mix(battle.CoreHealth);
        foreach (EnemyUnit enemy in battle.Enemies)
        {
            Mix(enemy.SpawnOrder);
            Mix(BitConverter.SingleToInt32Bits(enemy.Position.x));
            Mix(BitConverter.SingleToInt32Bits(enemy.Position.z));
            Mix(enemy.Health);
            Mix(enemy.AttacksPerformed);
            Mix(enemy.AttackBuffStacks);
            Mix(enemy.MaxHealth);
            Mix(BitConverter.SingleToInt32Bits(enemy.TauntRemaining));
            Mix(enemy.ActiveTaunter?.SpawnOrder ?? -1);
            Mix(enemy.Target?.SpawnOrder ?? -1);
            Mix(enemy.RetaliationTarget?.SpawnOrder ?? -1);
            Mix(BitConverter.SingleToInt32Bits(enemy.RetaliationRemaining));
            Mix(enemy.HasRetaliationRequest ? 1 : 0);
            Mix(enemy.PursuitTarget?.SpawnOrder ?? -1);
            Mix(BitConverter.SingleToInt32Bits(enemy.LastKnownTargetPosition.x));
            Mix(BitConverter.SingleToInt32Bits(enemy.LastKnownTargetPosition.z));
            Mix(BitConverter.SingleToInt32Bits(enemy.LostTargetSearchTime));
            Mix(enemy.CombatPathTarget?.SpawnOrder ?? -1);
            Mix(enemy.CombatPathIndex);
            Mix(enemy.CombatPath.Count);
            Mix(enemy.CombatPathSearch ? 1 : 0);
            Mix(BitConverter.SingleToInt32Bits(enemy.CombatPathAnchor.x));
            Mix(BitConverter.SingleToInt32Bits(enemy.CombatPathAnchor.z));
            Mix(BitConverter.SingleToInt32Bits(enemy.PathRetryRemaining));
            Mix(enemy.ReturnRouteOffset);
            Mix(enemy.UnreachableTarget?.SpawnOrder ?? -1);
            Mix(BitConverter.SingleToInt32Bits(enemy.UnreachableTargetPosition.x));
            Mix(BitConverter.SingleToInt32Bits(enemy.UnreachableTargetPosition.z));
            Mix(BitConverter.SingleToInt32Bits(enemy.UnreachableFromPosition.x));
            Mix(BitConverter.SingleToInt32Bits(enemy.UnreachableFromPosition.z));
            Mix(enemy.UnreachableNavigationVersion);
            foreach (Vector3 point in enemy.CombatPath)
            {
                Mix(BitConverter.SingleToInt32Bits(point.x));
                Mix(BitConverter.SingleToInt32Bits(point.z));
            }
        }
        return hash;
    }

    private static void AssertSameTicks(List<ulong> expected, List<ulong> actual, string label)
    {
        Assert.That(actual.Count, Is.EqualTo(expected.Count), $"{label}: 진행한 틱 수가 다르다.");
        for (int t = 0; t < expected.Count; t++)
            Assert.That(actual[t], Is.EqualTo(expected[t]), $"{label}: 틱 {t}의 상태가 다르다.");
    }

    [Test]
    public void SameTicksAcrossFramePatterns()
    {
        var jitter = new System.Random(7);
        var patterns = new (string Name, Func<int, (float, int, bool)> Frame)[]
        {
            ("30fps", _ => (1f / 30f, 1, false)),
            ("144fps", _ => (1f / 144f, 1, false)),
            ("20~144fps", _ => (1f / (20f + (float)jitter.NextDouble() * 124f), 1, false)),
            ("2배속·슬로우·멈칫", i => (i % 97 == 0 ? 0.5f : 1f / 75f, i % 400 < 150 ? 2 : 1, i % 400 >= 300 && i % 400 < 340)),
        };

        List<ulong> reference = RunWithFrames(_ => (1f / 60f, 1, false), Spells, out DefenseBattle referenceBattle);
        Assert.That(referenceBattle.CurrentPhase, Is.Not.EqualTo(DefenseBattle.Phase.Combat), "전투가 끝나지 않았다.");
        Assert.That(referenceBattle.CommandHistory.Count, Is.EqualTo(Spells.Length), "예약한 마법이 모두 적용되지 않았다.");

        foreach (var (name, frame) in patterns)
            AssertSameTicks(reference, RunWithFrames(frame, Spells, out _), name);
    }

    [Test]
    public void CommandHistoryReplaysTheSameBattle()
    {
        List<ulong> original = RunWithFrames(_ => (1f / 60f, 1, false), Spells, out DefenseBattle battle);
        var history = new List<DefenseBattle.SpellCommand>(battle.CommandHistory);

        List<ulong> replay = RunWithFrames(_ => (1f / 50f, 1, false), history, out _);
        AssertSameTicks(original, replay, "리플레이");
    }
}
