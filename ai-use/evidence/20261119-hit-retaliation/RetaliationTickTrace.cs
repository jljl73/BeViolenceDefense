using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
internal static class RetaliationTickTrace
{
    static StreamWriter writer;
    static readonly Dictionary<EnemyUnit, EnemyUnit> remembered = new();
    internal static int RangedRecognitions, RangedResponses;
    internal static float MinimumClearance = float.PositiveInfinity;
    internal static string Folder => "/tmp/be-retaliation-play";
    internal static void Record(DefenseBattle battle)
    {
        if (writer == null)
        {
            Directory.CreateDirectory(Folder);
            writer = new StreamWriter(Folder + "/trace.csv");
            writer.WriteLine("round,tick,id,unit,x,z,health,target,retaliation,remaining,attacks");
        }
        for (int i = 0; i < battle.Enemies.Count; i++)
        {
            var u = battle.Enemies[i];
            writer.WriteLine($"{battle.Round},{battle.TickIndex},{u.SpawnOrder},{u.Faction.UnitId},{u.Position.x:F5},{u.Position.z:F5},{u.Health},{u.Target?.SpawnOrder},{u.RetaliationTarget?.SpawnOrder},{u.RetaliationRemaining:F4},{u.AttacksPerformed}");
            remembered.TryGetValue(u, out var previous);
            if (u.RetaliationTarget is {} source && previous != source && source.Faction.AttackRange >= 3f)
            {
                RangedRecognitions++;
                if (u.Target == source) RangedResponses++;
                Debug.Log($"RANGED_HIT_REACTION: round={battle.Round}, tick={battle.TickIndex}, victim={u.SpawnOrder}/{u.Faction.UnitId}, source={source.SpawnOrder}/{source.Faction.UnitId}, target={u.Target?.SpawnOrder}");
            }
            remembered[u] = u.RetaliationTarget;
            for (int j = i+1; j < battle.Enemies.Count; j++)
                MinimumClearance = Mathf.Min(MinimumClearance, Vector3.Distance(u.Position,battle.Enemies[j].Position) - CombatPositioning.RequiredSpacing(u.Faction,battle.Enemies[j].Faction));
        }
        writer.Flush();
    }
}
