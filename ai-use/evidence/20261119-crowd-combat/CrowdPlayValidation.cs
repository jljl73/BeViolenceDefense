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
                Time.timeScale = 8;
                started = EditorApplication.timeSinceStartup;
                begun = true;
            }
            else if (battle.CurrentPhase != DefenseBattle.Phase.Combat)
            {
                if (battle.CurrentPhase != DefenseBattle.Phase.Preparation || battle.Enemies.Count != 0) throw new Exception("Scene combat failed to clear.");
                Debug.Log($"CROWD_PLAYMODE_PASS: actual GameScene round cleared, ticks={battle.TickIndex}, core={battle.CoreHealth}, remaining={battle.Enemies.Count}");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
            else if (EditorApplication.timeSinceStartup-started>90) throw new Exception("Scene battle timed out.");
        }
        catch(Exception e) { Debug.LogException(e); SessionState.SetBool(Key,false); EditorApplication.Exit(1); }
    }
}
