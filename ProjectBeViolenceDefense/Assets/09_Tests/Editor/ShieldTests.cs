using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ShieldTests
{
    private static EnemyUnit Unit(int armor = 0, int side = 0, int order = 0) =>
        new(new EnemyFaction("Test", Color.white, maxHealth: int.MaxValue, armor: armor), side,
            new[] { Vector3.zero, Vector3.forward }, order);

    [TestCase(25, 0, 5)]
    [TestCase(25, 5, 4)]
    [TestCase(26, 5, 5)]
    [TestCase(1, 0, 1)]
    [TestCase(0, 0, 0)]
    [TestCase(5, 10, 0)]
    [TestCase(int.MaxValue, 0, 429496730)]
    public void ShieldReducesDamageAfterBaseArmorAndRoundsUp(int incoming, int armor, int expected)
    {
        EnemyUnit unit = Unit(armor);
        unit.ApplyArmorBuff(DefenseBattle.BuffDuration);
        unit.QueueDamage(incoming);
        unit.ResolveDamage();
        Assert.That(unit.MaxHealth - unit.Health, Is.EqualTo(expected));
    }

    [Test]
    public void RecastRefreshesDurationWithoutStackingAndExpiryRestoresBaseArmor()
    {
        EnemyUnit unit = Unit(5);
        unit.ApplyArmorBuff(6f);
        unit.TickTimers(4f);
        unit.ApplyArmorBuff(6f);
        unit.ApplyArmorBuff(1f);
        Assert.That(unit.ArmorBuffStacks, Is.EqualTo(1));
        Assert.That(unit.ArmorBuffRemaining, Is.EqualTo(6f));
        unit.TickTimers(0f);
        Assert.That(unit.ArmorBuffRemaining, Is.EqualTo(6f), "Paused simulation must preserve the shield.");
        unit.QueueDamage(25);
        unit.ResolveDamage();
        Assert.That(unit.MaxHealth - unit.Health, Is.EqualTo(4));

        for (int tick = 0; tick < 361; tick++)
            unit.TickTimers(DefenseBattle.TickDuration);
        Assert.That(unit.ArmorBuffStacks, Is.Zero);
        Assert.That(unit.ArmorBuffRemaining, Is.Zero);
        unit.QueueDamage(25);
        unit.ResolveDamage();
        Assert.That(unit.MaxHealth - unit.Health, Is.EqualTo(24));
    }

    [Test]
    public void SimultaneousHitsAreReducedIndividuallyAndStillTriggerRetaliation()
    {
        EnemyUnit victim = Unit();
        EnemyUnit attacker = Unit(side: 1, order: 1);
        victim.ApplyArmorBuff(6f);
        victim.QueueDamage(26, attacker);
        victim.QueueDamage(26, attacker);
        victim.ResolveDamage();
        Assert.That(victim.MaxHealth - victim.Health, Is.EqualTo(12));
        Assert.That(victim.RetaliationTarget, Is.SameAs(attacker));
        Assert.That(victim.ConsumeRetaliationRequest(), Is.SameAs(attacker));
    }

    [Test]
    public void QueuedShieldAppliesAtNextTickOnlyToUnitsInAreaAndCostsMana()
    {
        var grid = new DefenseGrid();
        var factions = new[]
        {
            new EnemyFaction("A", Color.white, spawnCount: 1, moveSpeed: 0f),
            new EnemyFaction("B", Color.white, spawnCount: 1, moveSpeed: 0f)
        };
        var battle = new DefenseBattle(grid, factions);
        battle.StartRound();
        for (int i = 0; i < 60 && battle.Enemies.Count < 2; i++)
            battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(battle.Enemies.Count, Is.EqualTo(2));
        EnemyUnit target = battle.Enemies[0];
        float mana = battle.Mana;
        var recipients = new List<EnemyUnit>();
        battle.SpellResolved += (_, _, units) => recipients.AddRange(units);
        battle.QueueSpell(DefenseBattle.Spell.ArmorBuff, target.Position, 0);
        Assert.That(target.ArmorBuffStacks, Is.Zero);
        Assert.That(battle.Mana, Is.EqualTo(mana));
        battle.Tick(DefenseBattle.TickDuration, false);
        Assert.That(recipients, Is.EquivalentTo(new[] { target }));
        Assert.That(target.ArmorBuffStacks, Is.EqualTo(1));
        Assert.That(battle.Enemies.Skip(1).All(unit => unit.ArmorBuffStacks == 0), Is.True);
        Assert.That(battle.Mana, Is.InRange(mana - 24f, mana - 23f));
        Assert.That(battle.CommandHistory.Single().Spell, Is.EqualTo(DefenseBattle.Spell.ArmorBuff));
    }
}
