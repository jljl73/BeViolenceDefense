using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 도발: 고른 유닛이 거대해지며 최대 체력이 늘고, 반경 안의 다른 진영 유닛을 교전 중이거나 벽 너머여도 끌어당긴다.
public sealed class TauntTests
{
    private static DefenseGrid OpenGrid(params Vector2Int[] walls) => new(17, 17,
        new RectInt(8, 15, 2, 2), new[] { new Vector2Int(2, 2), new Vector2Int(14, 2) }, new[] { 0, 1 }, walls);

    private static EnemyUnit Unit(int side, int order, float x, float z, int maxHealth = 50) =>
        new(new EnemyFaction("Test", Color.white, maxHealth: maxHealth, awarenessRange: 4f, bodyRadius: 0.19f), side,
            new[] { new Vector3(x, 0, z), new Vector3(x, 0, z + 1) }, order);

    private static void StepApproach(CombatPositioning combat, EnemyUnit unit, int tick)
    {
        combat.TrackApproachProgress(unit, DefenseBattle.TickDuration);
        if (tick % 8 == 0)
            combat.Plan();
        if ((unit.Target ?? unit.CombatQueue?.Target) is { } target)
            combat.Move(unit, target, DefenseBattle.TickDuration);
    }

    [Test]
    public void TauntRaisesMaxHealthAndTakesTheBonusBackWhenItEnds()
    {
        EnemyUnit giant = Unit(0, 0, 4, 4, maxHealth: 51);
        giant.ApplyTaunt(DefenseBattle.TauntDuration);
        int bonus = Mathf.CeilToInt(51 * EnemyUnit.TauntHealthBonusPercent);
        Assert.That(giant.IsTaunting, Is.True);
        Assert.That(giant.MaxHealth, Is.EqualTo(51 + bonus));
        Assert.That(giant.Health, Is.EqualTo(51 + bonus));

        // 다시 걸어도 시간만 새로 채우고 추가 체력은 겹치지 않는다.
        giant.ApplyTaunt(DefenseBattle.TauntDuration);
        Assert.That(giant.MaxHealth, Is.EqualTo(51 + bonus));

        giant.QueueDamage(20);
        giant.ResolveDamage();
        for (int tick = 0; tick < DefenseBattle.TauntDuration * DefenseBattle.TicksPerSecond + 1; tick++)
            giant.TickTimers(DefenseBattle.TickDuration);
        Assert.That(giant.IsTaunting, Is.False);
        Assert.That(giant.MaxHealth, Is.EqualTo(51));
        Assert.That(giant.Health, Is.EqualTo(51 - 20));

        // 추가 체력보다 적게 남았을 때 도발이 끝나도 그것만으로는 죽지 않는다.
        giant.ApplyTaunt(DefenseBattle.TauntDuration);
        giant.QueueDamage(giant.Health - 3);
        giant.ResolveDamage();
        for (int tick = 0; tick < DefenseBattle.TauntDuration * DefenseBattle.TicksPerSecond + 1; tick++)
            giant.TickTimers(DefenseBattle.TickDuration);
        Assert.That(giant.Health, Is.EqualTo(1));
    }

    [Test]
    public void TauntPullsAUnitOutOfAFightAlreadyUnderWay()
    {
        EnemyUnit fighter = Unit(0, 0, 4, 4);
        EnemyUnit foe = Unit(1, 1, 5.2f, 4);
        EnemyUnit taunter = Unit(1, 2, 4, 9.5f);
        var combat = new CombatPositioning(OpenGrid(), new[] { fighter, foe, taunter });
        combat.Plan();
        Assert.That(fighter.Target, Is.SameAs(foe), "The fixture must start with the fighter engaged.");

        taunter.ApplyTaunt(DefenseBattle.TauntDuration);
        fighter.ApplyTauntedBy(taunter, DefenseBattle.TauntDuration);
        combat.Plan();
        Assert.That(fighter.Target, Is.SameAs(taunter), "A taunted unit leaves its fight for the taunter.");
        Assert.That(combat.CanAttack(fighter, foe), Is.False);
    }

    [Test]
    public void TauntReachesThroughAWallAndTheUnitWalksAroundIt()
    {
        var wall = Enumerable.Range(0, 11).Select(x => new Vector2Int(x, 7)).ToArray();
        DefenseGrid grid = OpenGrid(wall);
        EnemyUnit pulled = Unit(0, 0, 4, 5);
        EnemyUnit taunter = Unit(1, 1, 4, 9);
        Assert.That(grid.HasLineOfSight(pulled.Position, taunter.Position), Is.False, "The wall must hide the taunter.");

        var combat = new CombatPositioning(grid, new[] { pulled, taunter });
        combat.Plan();
        Assert.That(pulled.Target, Is.Null, "Without taunt the unit cannot see past the wall.");

        taunter.ApplyTaunt(DefenseBattle.TauntDuration);
        pulled.ApplyTauntedBy(taunter, DefenseBattle.TauntDuration);
        bool reached = false;
        for (int tick = 0; tick < 1800 && !reached; tick++)
        {
            StepApproach(combat, pulled, tick);
            reached = combat.CanAttack(pulled, taunter);
        }
        Assert.That(reached, Is.True, "The taunted unit must find the way around the wall and start attacking.");
        Assert.That(grid.IsWalkable(DefenseGrid.PositionToCell(pulled.Position)), Is.True);
    }

    [Test]
    public void TauntCastPullsOnlyOpponentsInRadiusAndCostsMana()
    {
        var grid = new DefenseGrid(17, 17, new RectInt(8, 15, 2, 2),
            new[] { new Vector2Int(6, 2), new Vector2Int(10, 2) }, new[] { 0, 1 }, null);
        var sides = new[]
        {
            new EnemyFaction("A", Color.white, maxHealth: 400, spawnCount: 6),
            new EnemyFaction("B", Color.white, maxHealth: 400, spawnCount: 6)
        };
        var battle = new DefenseBattle(grid, sides);
        battle.StartRound();

        EnemyUnit taunter = null;
        for (int tick = 0; tick < 1200 && taunter == null; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            taunter = battle.Enemies.FirstOrDefault(unit => battle.Enemies.Any(other => DefenseBattle.IsTauntable(unit, other)));
        }
        Assert.That(taunter, Is.Not.Null, "The fixture must bring opposing units within the taunt radius.");
        while (battle.Mana < DefenseBattle.ManaCost(DefenseBattle.Spell.Taunt))
            battle.Tick(DefenseBattle.TickDuration, false);

        // 마법은 틱 시작, 이동 전에 적용된다. 그 시점의 위치로 대상을 정한다.
        var expected = battle.Enemies.Where(other => DefenseBattle.IsTauntable(taunter, other)).ToList();
        float manaBefore = battle.Mana;
        var resolved = new List<EnemyUnit>();
        battle.SpellResolved += (_, _, recipients) => resolved.AddRange(recipients);
        battle.QueueTaunt(taunter);
        battle.Tick(DefenseBattle.TickDuration, false);

        Assert.That(resolved.First(), Is.SameAs(taunter));
        Assert.That(resolved.Skip(1), Is.EquivalentTo(expected));
        Assert.That(resolved.Skip(1).All(unit => unit.FactionIndex != taunter.FactionIndex), Is.True);
        Assert.That(taunter.IsTaunting, Is.True);
        Assert.That(taunter.MaxHealth, Is.EqualTo(400 + 200));
        Assert.That(battle.Mana, Is.EqualTo(manaBefore - DefenseBattle.ManaCost(DefenseBattle.Spell.Taunt) +
            7f * DefenseBattle.TickDuration).Within(0.001f));
        Assert.That(battle.CommandHistory.Last().TargetSpawnOrder, Is.EqualTo(taunter.SpawnOrder));
    }

    [Test]
    public void SpellsHaveNoPerBattleCastLimit()
    {
        var sides = new[]
        {
            new EnemyFaction("A", Color.white, maxHealth: 2000, spawnCount: 9),
            new EnemyFaction("B", Color.white, maxHealth: 2000, spawnCount: 9)
        };
        var battle = new DefenseBattle(new DefenseGrid(), sides);
        battle.StartRound();
        int casts = 0;
        for (int tick = 0; tick < 60 * 60 && casts < 5 && battle.CurrentPhase == DefenseBattle.Phase.Combat; tick++)
        {
            battle.Tick(DefenseBattle.TickDuration, false);
            if (battle.Enemies.Count == 0 || battle.Mana < DefenseBattle.ManaCost(DefenseBattle.Spell.AttackBuff))
                continue;
            EnemyUnit target = battle.Enemies[0];
            int previousStacks = target.AttackBuffStacks;
            battle.CastAreaSpell(DefenseBattle.Spell.AttackBuff, target.Position, 0);
            Assert.That(target.AttackBuffStacks, Is.EqualTo(previousStacks + 1), $"Cast {casts + 1} failed.");
            casts++;
        }
        Assert.That(casts, Is.EqualTo(5));
    }
}
