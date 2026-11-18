using System;
using System.Collections.Generic;
using UnityEngine;
class Probe {
static EnemyUnit U(int s,int o,float x,float z,float range=1.5f,float radius=.3f)=>new(new EnemyFaction("Test",new Color(),attackRange:range,awarenessRange:4,bodyRadius:radius),s,new[]{new Vector3(x,0,z),new Vector3(x,0,z+1)},o);
static void Main(){
var a=U(0,0,4,4);var t=U(1,1,6,4);var alt=U(1,2,4,7);var us=new List<EnemyUnit>{a,t,alt};
for(int i=0;i<5;i++)us.Add(U(0,3+i,4.7f,2.6f+i*.7f,.1f));
var g=new DefenseGrid(17,17,new RectInt(8,15,2,2),new[]{new Vector2Int(2,2),new Vector2Int(14,2)},new[]{0,1},null);var c=new CombatPositioning(g,us);a.Target=t;
int switched=-1,attacked=-1;float travel=0;
for(int i=0;i<360;i++){
var before=a.Position;
#if RETARGET
c.TrackApproachProgress(a,DefenseBattle.TickDuration);
#endif
if(i%8==0)c.Plan();
if(a.Target!=null)c.Move(a,a.Target,DefenseBattle.TickDuration);
travel+=Vector3.Distance(before,a.Position);
if(a.Target==alt&&switched<0)switched=i;
if(a.Target!=null&&c.CanAttack(a,a.Target)&&attacked<0)attacked=i;

}
Console.WriteLine($"firstSwitchTick={switched}, firstAttackTick={attacked}, finalTarget={a.Target?.SpawnOrder}, totalTravel={travel:F3}, finalPosition={a.Position}");
}}
