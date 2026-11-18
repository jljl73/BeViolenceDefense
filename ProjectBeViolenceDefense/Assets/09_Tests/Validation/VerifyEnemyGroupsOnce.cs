#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class VerifyEnemyGroupsOnce
{
    [MenuItem("Tools/Verify Enemy Groups Once")]
    private static void Verify()
    {
        var grid = new DefenseGrid();
        var factions = new[] { new EnemyFaction("Coral", Color.red), new EnemyFaction("Teal", Color.cyan) };
        var attacker = new EnemyUnit(factions[0], 0, new[] { new Vector3(7, 0, 3.9f), new Vector3(7, 0, 4) }, 0);
        var defender = new EnemyUnit(factions[1], 1, new[] { new Vector3(7, 0, 5), new Vector3(7, 0, 6) }, 1);
        attacker.Target = defender;
        attacker.AttackSlot = 0;
        var positioning = new CombatPositioning(grid, new[] { attacker, defender }, DefenseBattle.UnitSpacing);
        positioning.Plan();
        Require(positioning.CanAttack(attacker, defender), "A clear in-range attacker must be able to strike from its current position.");
        positioning.Move(attacker, defender, 0.2f);
        Require(attacker.Position.Equals(attacker.RoutePosition), "An in-range attacker must not move to a different slot.");

        var armoredFaction = new EnemyFaction("Armored", Color.gray, maxHealth: 100, armor: 3, moveSpeed: 2f);
        var armored = new EnemyUnit(armoredFaction, 1,
            new[] { Vector3.zero, Vector3.forward * 10f }, 0);
        armored.QueueDamage(3);
        armored.ResolveDamage();
        Require(armored.Health == 100, "Attack minus armor must stop at zero damage.");
        armored.ApplyArmorBuff(DefenseBattle.BuffDuration);
        armored.QueueDamage(8);
        armored.QueueDamage(10);
        armored.ResolveDamage();
        Require(armored.Health == 98, "Armor buff must add fixed armor per hit.");
        armored.ApplyMoveSpeedBuff(DefenseBattle.BuffDuration);
        Require(Mathf.Approximately(armored.StepToward(Vector3.forward * 10f, 1f).z, 3f),
            "Move speed buff must scale the unit's base speed.");
        armored.TickTimers(DefenseBattle.BuffDuration);
        Require(armored.ArmorBuffStacks == 0 && armored.MoveSpeedBuffStacks == 0,
            "Buffs must expire using battle delta time.");

        var battle = new DefenseBattle(grid, factions);
        battle.StartRound();
        battle.Tick(0.36f, false);
        Require(battle.Enemies.Count == 2 && battle.Enemies.Select(e => e.Faction).Distinct().Count() == 2,
            "Each spawn point must release one enemy at a time.");
        EnemyUnit wounded = battle.Enemies[0];
        wounded.QueueDamage(30);
        wounded.ResolveDamage();
        Require(battle.CastAreaSpell(DefenseBattle.Spell.Heal, wounded.Position, 0).StartsWith("Healed") &&
            wounded.Health == 33, "Area heal must restore a rounded-up share of maximum health.");
        for (int i = 0; i < 40; i++) battle.Tick(1f / 60f, false);
        Require(battle.Enemies.Count == 4 && battle.Enemies.GroupBy(e => e.Faction).All(g => g.Count() == 2),
            "Each spawn point must release the next enemy after the interval.");
        Require(battle.Enemies.Select(e => e.Position).Distinct().Count() == 4, "Enemies in a column must not overlap.");
        var gate = new Vector2Int(8, 7);
        Require(grid.Routes.All(route => route.Contains(DefenseGrid.CellPosition(gate)) &&
            route.Where(point => point.z >= gate.y).All(point => point.x == gate.x)),
            "Both default routes must pass the gate and follow a straight line to the core.");
        Require(grid.TryPlace(gate) == DefenseGrid.BuildResult.BlocksRoute, "The shared gate must stay open.");
        var centerlineStart = new Vector3(8, 0, 2);
        var centerlineEnd = new Vector3(10, 0, 2);
        Require(grid.HasLineOfSight(centerlineStart, centerlineEnd) && grid.CanPursue(centerlineStart, centerlineEnd),
            "The open centerline must allow vision and pursuit.");
        var wallCell = new Vector2Int(9, 2);
        Require(grid.TryPlace(wallCell) == DefenseGrid.BuildResult.Placed, "The open approach must accept a new wall.");
        Require(!grid.HasLineOfSight(centerlineStart, centerlineEnd) && !grid.CanPursue(centerlineStart, centerlineEnd),
            "A built wall must block vision and pursuit.");
        Require(grid.TryRemove(wallCell), "The test wall must be removed before combat.");

        battle = new DefenseBattle(grid, factions);
        int forecast = battle.ForecastEncounterCells().Count;
        int expectedEnemies = battle.EnemiesPerRound;
        bool pursued = false;
        int kills = 0;
        int contacts = 0;
        float minimumSpacing = float.PositiveInfinity;
        battle.EnemyRemoved += enemy =>
        {
            if (enemy.ReachedCore) contacts++;
            else kills++;
        };
        battle.StartRound();
        for (int i = 0; i < 3600 && battle.CurrentPhase == DefenseBattle.Phase.Combat; i++)
        {
            battle.Tick(1f / 60f, false);
            pursued |= battle.Enemies.Any(e => e.Target != null && !e.Position.Equals(e.RoutePosition) &&
                Vector3.Distance(e.Position, e.Target.Position) > e.Faction.AttackRange);
            Require(battle.Enemies.Where(e => e.Target is { IsAlive: true }).GroupBy(e => e.Target)
                .All(group => group.Count() <= CombatPositioning.MaxAttackersPerTarget), "A target must not exceed the attacker limit.");
            for (int first = 0; first < battle.Enemies.Count; first++)
            for (int second = first + 1; second < battle.Enemies.Count; second++)
                minimumSpacing = Mathf.Min(minimumSpacing,
                    Vector3.Distance(battle.Enemies[first].Position, battle.Enemies[second].Position));
        }
        Require(pursued, "Default groups must pursue enemies beyond attack range.");
        Require(minimumSpacing >= DefenseBattle.UnitSpacing - 0.001f, "Units must keep their spacing through turns and merges.");
        Require(kills > 0 && forecast == 1, "Default groups must fight and forecast a single first encounter.");
        Require(battle.CurrentPhase == DefenseBattle.Phase.Preparation && kills + contacts == expectedEnemies &&
            battle.CoreHealth == DefenseBattle.CoreMaxHealth - contacts,
            "The round must resolve every unit and count contacts correctly.");
        Require(battle.EnemiesPerRound == expectedEnemies + 5, "The next round must gain five reinforcements.");
        Debug.Log($"ENEMY_GROUPS_PASS: contacts={contacts}, kills={kills}, forecast={forecast}");
    }

    private static void Require(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
    }
}
#endif
