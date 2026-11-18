using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
internal static class CrowdPlayValidation
{
    const string Key = "CrowdPlayValidation.Running";
    static double started;
    static bool begun;
    static VoxelDefensePrototype flow;
    static DefenseBattle battle;
    static Camera camera;
    static Vector3 position;
    static Quaternion rotation;
    static float size;
    static CrowdPlayValidation() { EditorApplication.update += Update; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/01_Scenes/02_Gameplay/GameScene.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Update()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (!begun)
            {
                flow = UnityEngine.Object.FindFirstObjectByType<VoxelDefensePrototype>();
                if (flow == null) return;
                battle = (DefenseBattle)typeof(VoxelDefensePrototype).GetField("battle", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow);
                if (battle == null) return;
                typeof(VoxelDefensePrototype).GetMethod("StartBattle", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(flow, null);
                camera = (Camera)typeof(VoxelDefensePrototype).GetField("boardCamera", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow);
                position = camera.transform.position; rotation = camera.transform.rotation; size = camera.orthographicSize;
                Time.timeScale = 8;
                started = EditorApplication.timeSinceStartup;
                begun = true;
            }
            else if (battle.CurrentPhase != DefenseBattle.Phase.Combat)
            {
                if (battle.CurrentPhase != DefenseBattle.Phase.Preparation || battle.Enemies.Count != 0) throw new Exception("Scene combat failed to clear.");
                if (Vector3.Distance(camera.transform.position, position)>0.001f || Quaternion.Angle(camera.transform.rotation, rotation)>0.01f || Mathf.Abs(camera.orthographicSize-size)>0.001f) throw new Exception("Camera changed on round clear.");
                var ui = (UIManager)typeof(VoxelDefensePrototype).GetField("ui", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow);
                if (!ui.HudLayer.gameObject.activeSelf || GameObject.Find("Round Victory") != null) throw new Exception("Victory presentation remains.");
                typeof(VoxelDefensePrototype).GetMethod("StartBattle", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(flow, null);
                if(battle.CurrentPhase != DefenseBattle.Phase.Combat) throw new Exception("Next round is blocked.");
                Debug.Log($"VICTORY_REMOVAL_PASS: camera unchanged, HUD visible, next round starts immediately; actual GameScene round cleared, ticks={battle.TickIndex}, core={battle.CoreHealth}, remaining={battle.Enemies.Count}");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
            else if (EditorApplication.timeSinceStartup-started>90) throw new Exception("Scene battle timed out.");
        }
        catch(Exception e) { Debug.LogException(e); SessionState.SetBool(Key,false); EditorApplication.Exit(1); }
    }
}
