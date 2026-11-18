using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
class Bench
{
 static void Main()
 {
  foreach(int count in new[]{9,20,40}) foreach(bool walls in new[]{false,true})
  {
   var grid = new DefenseGrid();
   if(walls) foreach(var p in new[]{new Vector2Int(6,5),new Vector2Int(10,5),new Vector2Int(7,4),new Vector2Int(9,4)}) grid.TryPlace(p);
   var sides = new[]{new EnemyFaction("A",new Color(1f,1f,1f),spawnCount:count),new EnemyFaction("B",new Color(1f,1f,1f),spawnCount:count, maxHealth:90,damage:15,attackInterval:1f,moveSpeed:1.2f,bodyRadius:0.48f)};
   var battle=new DefenseBattle(grid,sides); battle.StartRound();
   long stationary=0, active=0; float min=99; int attacks=0,kills=0; battle.EnemyRemoved+=e=>{attacks+=e.AttacksPerformed;if(!e.ReachedCore) kills++;};
   var sw=System.Diagnostics.Stopwatch.StartNew();
   while(battle.CurrentPhase==DefenseBattle.Phase.Combat && battle.TickIndex<14400)
   {
    battle.Tick(DefenseBattle.TickDuration,false);
    foreach(var u in battle.Enemies)
    {
     if((u.Target??u.CombatQueue?.Target)!=null){active++;if(u.CanAttack && u.Position.Equals(u.PreviousPosition))stationary++;}
     foreach(var v in battle.Enemies) if(u.SpawnOrder<v.SpawnOrder) min=Math.Min(min,Vector3.Distance(u.Position,v.Position)-CombatPositioning.RequiredSpacing(u.Faction,v.Faction));
    }
   }
   Console.WriteLine($"count={count} walls={walls} phase={battle.CurrentPhase} ticks={battle.TickIndex} stalledReadySeconds={stationary/60.0:F2} engagedSeconds={active/60.0:F2} attacks={attacks} kills={kills} core={battle.CoreHealth} clearanceMargin={min:F5} ms={sw.ElapsedMilliseconds}");
  }
 }
}
