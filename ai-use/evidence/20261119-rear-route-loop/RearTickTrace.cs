using System;
using System.IO;
using UnityEngine;
internal static class RearTickTrace {
 static StreamWriter writer;
 static string V(Vector3? p)=>p is {} v?$"{v.x:F5},{v.z:F5}":",";
 internal static string Folder=>Environment.GetEnvironmentVariable("BE_REAR_OUT") ?? "/tmp/be-rear-before";
 internal static void Record(DefenseBattle battle){
 if(writer==null){Directory.CreateDirectory(Folder);writer=new StreamWriter(Folder+"/unity-trace.csv");writer.WriteLine("tick,id,x,z,hx,hz,target,queue,slot,dx,dz,sx,sz,rx,rz,stall,attacks");}
 foreach(var u in battle.Enemies)writer.WriteLine($"{battle.TickIndex},{u.SpawnOrder},{V(u.Position)},{V(u.Heading)},{u.Target?.SpawnOrder},{u.CombatQueue?.Target.SpawnOrder},{u.AttackSlot},{V(u.CombatDestination)},{V(u.CombatSteerWaypoint)},{V(u.RouteYieldDestination)},{u.CombatStalledTime:F3},{u.AttacksPerformed}");
 writer.Flush();
 }
}
