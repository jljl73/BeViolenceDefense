using System;
using System.IO;
using UnityEngine;
internal static class FlankTickTrace {
 static StreamWriter writer; static CombatPositioning combat;
 static string V(Vector3? p)=>p is {} v?$"{v.x:F5},{v.z:F5}":",";
 internal static string Folder=>Environment.GetEnvironmentVariable("BE_FLANK_OUT") ?? "/tmp/be-flank-after";
 internal static void Record(DefenseBattle battle, DefenseGrid grid){
 if(writer==null){combat=new CombatPositioning(grid,battle.Enemies);Directory.CreateDirectory(Folder);writer=new StreamWriter(Folder+"/unity-trace.csv");writer.WriteLine("tick,id,x,z,hx,hz,target,queue,slot,dx,dz,sx,sz,rx,rz,stall,attacks,canAttack");}
 foreach(var u in battle.Enemies)writer.WriteLine($"{battle.TickIndex},{u.SpawnOrder},{V(u.Position)},{V(u.Heading)},{u.Target?.SpawnOrder},{u.CombatQueue?.Target.SpawnOrder},{u.AttackSlot},{V(u.CombatDestination)},{V(u.CombatSteerWaypoint)},{V(u.RouteYieldDestination)},{u.CombatStalledTime:F3},{u.AttacksPerformed},{(u.Target!=null&&combat.CanAttack(u,u.Target)?1:0)}");
 writer.Flush();
 }
}
