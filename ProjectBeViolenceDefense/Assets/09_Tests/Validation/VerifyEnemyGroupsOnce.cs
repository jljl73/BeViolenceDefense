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
        var battle = new DefenseBattle(grid, factions);
        battle.StartRound();
        battle.Tick(0.36f, false);
        Require(battle.Enemies.Count == 2 && battle.Enemies.Select(e => e.Faction).Distinct().Count() == 2,
            "Each spawn point must release one enemy at a time.");
        for (int i = 0; i < 40; i++) battle.Tick(1f / 60f, false);
        Require(battle.Enemies.Count == 4 && battle.Enemies.GroupBy(e => e.Faction).All(g => g.Count() == 2),
            "Each spawn point must release the next enemy after the interval.");
        Require(battle.Enemies.Select(e => e.Position).Distinct().Count() == 4, "Enemies in a column must not overlap.");
        var dividerStart = new Vector3(8, 0, 2);
        var dividerEnd = new Vector3(10, 0, 2);
        Require(!grid.HasLineOfSight(dividerStart, dividerEnd), "A divider must block vision.");
        Require(!grid.CanPursue(dividerStart, dividerEnd), "A divider must block pursuit.");

        battle = new DefenseBattle(grid, factions);
        int forecast = battle.ForecastEncounterCells().Count;
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
        Require(kills > 0 && forecast > 0, "Default groups must fight and forecast encounters.");
        Require(battle.CurrentPhase == DefenseBattle.Phase.Preparation && kills + contacts == battle.EnemiesPerRound &&
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
