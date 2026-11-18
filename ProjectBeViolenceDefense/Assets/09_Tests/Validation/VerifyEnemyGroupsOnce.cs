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
        var factions = new[] { new EnemyFaction("Coral", Color.red, 0.9f), new EnemyFaction("Teal", Color.cyan, 0.9f) };
        var battle = new DefenseBattle(grid, factions);
        battle.StartRound();
        battle.Tick(0.36f, true);
        Require(battle.Enemies.Count == 9 && battle.Enemies.Select(e => e.Position).Distinct().Count() == 9,
            "First pulse must spawn 9 enemies at distinct positions.");
        for (int group = 0; group < 3; group++)
            Require(battle.Enemies.Skip(group * 3).Take(3).Select(e => e.Faction).Distinct().Count() == 1,
                "A spawn group must belong to one faction.");
        for (int i = 0; i < 120; i++) battle.Tick(1f / 60f, true);
        Require(battle.Enemies.Count == 18 && battle.Enemies.GroupBy(e => e.Faction).All(g => g.Count() == 9),
            "Two pulses must produce 18 enemies, equally split.");
        Require(battle.Enemies.Select(e => e.Position).Distinct().Count() == 18, "The next pulse must have separate start positions while movement is paused.");
        Require(!grid.HasLineOfSight(new Vector3(6, 0, 2), new Vector3(8, 0, 2)), "A divider must block vision.");
        Require(!grid.CanPursue(new Vector3(6, 0, 2), new Vector3(8, 0, 2)), "A divider must block pursuit.");

        battle = new DefenseBattle(grid, factions);
        int forecast = battle.ForecastEncounterCells().Count;
        bool pursued = false;
        int kills = 0;
        int contacts = 0;
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
        }
        Require(pursued, "Default groups must pursue enemies beyond attack range.");
        Require(kills > 0 && forecast > 0, "Default groups must fight and forecast encounters.");
        Require(battle.CurrentPhase == DefenseBattle.Phase.Preparation && kills + contacts == 18 &&
            battle.CoreHealth == DefenseBattle.CoreMaxHealth - contacts,
            "The round must resolve every unit and count contacts correctly.");
        Debug.Log($"ENEMY_GROUPS_PASS: contacts={contacts}, kills={kills}, forecast={forecast}");
    }

    private static void Require(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
    }
}
#endif
