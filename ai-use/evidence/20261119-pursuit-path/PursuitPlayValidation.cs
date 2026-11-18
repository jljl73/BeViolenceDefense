using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
internal static class PursuitPlayValidation
{
    const string Key = "PursuitPlayValidation.Running";
    static VoxelDefensePrototype flow;
    static DefenseBattle battle;
    static double started;
    static int nextCapture = 400, frame;
    static PursuitPlayValidation() { EditorApplication.update += Update; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/01_Scenes/03_Test/GameScene_LeftCoreHud.unity");
        SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }
    static object Field(string name) => typeof(VoxelDefensePrototype).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow);
    static void StartRound() => typeof(VoxelDefensePrototype).GetMethod("StartBattle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(flow,null);
    static void Update()
    {
        if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (flow == null) { flow = UnityEngine.Object.FindFirstObjectByType<VoxelDefensePrototype>(); if (flow == null) return; }
            if (battle == null)
            {
                battle = (DefenseBattle)Field("battle"); if (battle == null) return;
                typeof(VoxelDefensePrototype).GetField("battleSpeed",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(flow,2);
                started = EditorApplication.timeSinceStartup;
                StartRound();
            }
            if (battle.CurrentPhase == DefenseBattle.Phase.Combat && battle.Round == 3 && battle.TickIndex >= nextCapture && nextCapture <= 1000)
            { Capture(); nextCapture += 12; }
            if (battle.CurrentPhase == DefenseBattle.Phase.Preparation)
            {
                Debug.Log($"PURSUIT_ROUND_PASS: cleared={battle.Round-1}, core={battle.CoreHealth}, remaining={battle.Enemies.Count}");
                if (battle.Round <= 3) StartRound();
                else
                {
                    if (PursuitTickTrace.MinimumClearance < -0.0001f || PursuitTickTrace.RangedResponses < 1)
                        throw new Exception("Spacing or ranged response validation failed.");
                    Debug.Log($"PURSUIT_PLAYMODE_PASS: rounds=3, rangedRecognitions={PursuitTickTrace.RangedRecognitions}, rangedResponses={PursuitTickTrace.RangedResponses}, clearance={PursuitTickTrace.MinimumClearance:F5}, captures={frame}");
                    SessionState.SetBool(Key,false); EditorApplication.Exit(0);
                }
            }
            if (battle.CurrentPhase == DefenseBattle.Phase.Defeat || EditorApplication.timeSinceStartup-started > 180)
                throw new Exception("Play validation did not finish all three rounds.");
        }
        catch(Exception e) { Debug.LogException(e); SessionState.SetBool(Key,false); EditorApplication.Exit(1); }
    }
    static void Capture()
    {
        var camera = (Camera)Field("boardCamera");
        var oldPos = camera.transform.position; var oldSize = camera.orthographicSize;
        camera.transform.position = flow.transform.TransformPoint(new Vector3(8f,0f,5.3f))-camera.transform.forward*20;
        camera.orthographicSize = 3.5f;
        var rt = new RenderTexture(1280,720,24); var previous = camera.targetTexture; var active = RenderTexture.active;
        camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
        Directory.CreateDirectory(PursuitTickTrace.Folder);
        File.WriteAllBytes(PursuitTickTrace.Folder+$"/frame-{frame++:D2}-tick-{battle.TickIndex}.png",texture.EncodeToPNG());
        camera.targetTexture=previous;RenderTexture.active=active;camera.transform.position=oldPos;camera.orthographicSize=oldSize;
        UnityEngine.Object.Destroy(texture);rt.Release();UnityEngine.Object.Destroy(rt);
    }
}
