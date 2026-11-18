using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
internal static class RadiiPlayValidation
{
 const string Key="RadiiPlayValidation.Running";
 static VoxelDefensePrototype flow; static DefenseBattle battle; static bool captured; static float minimum=99; static double started;
 static RadiiPlayValidation(){EditorApplication.update+=Update;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/01_Scenes/02_Gameplay/GameScene.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 static object Field(string name)=>typeof(VoxelDefensePrototype).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow);
 static void Update()
 {
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  try
  {
   if(flow==null){flow=UnityEngine.Object.FindFirstObjectByType<VoxelDefensePrototype>();if(flow==null)return;}
   if(battle==null)
   {
    battle=(DefenseBattle)Field("battle");if(battle==null)return;
    typeof(VoxelDefensePrototype).GetMethod("StartBattle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(flow,null);
    var catalog=(EnemyCatalog)Field("enemyCatalog");
    var goblin=catalog.GetUnit("goblin-grunt-1500",Color.white,"validation");
    var zombie=catalog.GetUnit("zombie",Color.white,"validation");
    if(Mathf.Abs(zombie.BodyRadius-0.312f)>0.0001f)throw new Exception("Model scale was not applied to body radius.");
    Debug.Log($"CATALOG_RADII_PASS: goblin={goblin.BodyRadius:F3}, zombie={zombie.BodyRadius:F3}, spacing={CombatPositioning.RequiredSpacing(goblin,zombie):F3}");
    Time.timeScale=4;started=EditorApplication.timeSinceStartup;
   }
   for(int i=0;i<battle.Enemies.Count;i++)for(int j=i+1;j<battle.Enemies.Count;j++)minimum=Mathf.Min(minimum,Vector3.Distance(battle.Enemies[i].Position,battle.Enemies[j].Position)-CombatPositioning.RequiredSpacing(battle.Enemies[i].Faction,battle.Enemies[j].Faction));
   if(!captured && battle.Enemies.Count>=6 && battle.Enemies[0].AttacksPerformed>0){Capture();captured=true;}
   if(battle.CurrentPhase!=DefenseBattle.Phase.Combat)
   {
    if(minimum < -0.0001f || !captured)throw new Exception("Spacing or capture failed.");
    Debug.Log($"RADII_PLAYMODE_PASS: clearanceMargin={minimum:F5}, round={battle.Round}, core={battle.CoreHealth}, remaining={battle.Enemies.Count}");
    SessionState.SetBool(Key,false);EditorApplication.Exit(0);
   }
   if(EditorApplication.timeSinceStartup-started>90)throw new Exception("Timed out.");
  }catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
 }
 static void Capture()
 {
  var camera=(Camera)Field("boardCamera");var oldPos=camera.transform.position;var oldSize=camera.orthographicSize;
  Vector3 center=Vector3.zero;foreach(var e in battle.Enemies)center+=e.Position;center/=battle.Enemies.Count;
  camera.transform.position=flow.transform.TransformPoint(center)-camera.transform.forward*20;camera.orthographicSize=5;
  var rt=new RenderTexture(1600,900,24);var previous=camera.targetTexture;var active=RenderTexture.active;
  camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
  var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes("/tmp/be-radii-combat.png",texture.EncodeToPNG());
  camera.targetTexture=previous;RenderTexture.active=active;camera.transform.position=oldPos;camera.orthographicSize=oldSize;
  UnityEngine.Object.Destroy(texture);rt.Release();UnityEngine.Object.Destroy(rt);
 }
}
