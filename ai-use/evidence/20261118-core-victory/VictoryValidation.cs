using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
internal static class VictoryValidation
{
    private const string Key = "VictoryValidation.Running";
    private static int stage;
    private static double started;
    private static VoxelDefensePrototype flow;
    private static DefenseBoardView view;
    private static Camera camera;
    private static Vector3 position;
    private static Quaternion rotation;
    private static float size;
    static VictoryValidation() { EditorApplication.update += Update; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/01_Scenes/02_Gameplay/GameScene.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static object Field(string name) => typeof(VoxelDefensePrototype).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(flow);
    static void Invoke(string name) => typeof(VoxelDefensePrototype).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(flow, null);
    static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }
    static void Update()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (stage == 0)
            {
                flow = UnityEngine.Object.FindFirstObjectByType<VoxelDefensePrototype>();
                if (flow == null) return;
                view = (DefenseBoardView)Field("view");
                if (view == null) return;
                camera = (Camera)Field("boardCamera");
                view.PanCamera(new Vector2(1f, 0.5f), 0.2f);
                position = camera.transform.position; rotation = camera.transform.rotation; size = camera.orthographicSize;
                Time.timeScale = 0f;
                Invoke("BeginVictory");
                Require(view.IsVictoryPlaying, "Victory did not start.");
                Invoke("StartBattle");
                Require(((DefenseBattle)Field("battle")).CurrentPhase == DefenseBattle.Phase.Preparation, "Start input leaked into victory.");
                started = EditorApplication.timeSinceStartup;
                stage = 1;
            }
            else if (stage == 1 && EditorApplication.timeSinceStartup - started > 1.5)
            {
                Require(view.IsVictoryPlaying, "Victory ended before pose hold.");
                Require(!((UIManager)Field("ui")).HudLayer.gameObject.activeSelf, "HUD visible during shot.");
                Require(camera.orthographicSize < size, "Camera did not close in.");
                Capture("/tmp/be-victory-pose.png");
                stage = 2;
            }
            else if (stage == 2 && EditorApplication.timeSinceStartup - started > 5)
            {
                Require(!view.IsVictoryPlaying, "Victory did not finish automatically.");
                Require(Vector3.Distance(camera.transform.position, position) < 0.001f && Quaternion.Angle(camera.transform.rotation, rotation) < 0.01f && Mathf.Abs(camera.orthographicSize-size) < 0.001f, "Camera was not restored.");
                Require(((UIManager)Field("ui")).HudLayer.gameObject.activeSelf, "HUD not restored.");
                Invoke("BeginVictory");
                view.AdvanceVictory(0.5f);
                Invoke("FinishVictory");
                Require(!view.IsVictoryPlaying && Vector3.Distance(camera.transform.position, position) < 0.001f, "Skip did not restore camera.");
                Invoke("BeginVictory");
                flow.enabled = false;
                Require(!view.IsVictoryPlaying && ((UIManager)Field("ui")).HudLayer.gameObject.activeSelf, "Disable did not clean up.");
                flow.enabled = true;
                Time.timeScale = 1f;
                Invoke("StartBattle");
                var battle = (DefenseBattle)Field("battle");
                // Drive real flow updates so a cleared combat round must trigger its own presentation.
                // Time.deltaTime is zero in the paused frame; wait for a live frame in stage 3.
                stage = 3;
            }
            else if (stage == 3)
            {
                var battle = (DefenseBattle)Field("battle");
                for (int i = 0; i < 20000 && battle.CurrentPhase == DefenseBattle.Phase.Combat; i++)
                    Invoke("Update");
                Require(battle.CurrentPhase == DefenseBattle.Phase.Preparation && view.IsVictoryPlaying,
                    "Actual combat clear did not trigger victory.");
                Invoke("FinishVictory");
                var fallback = new GameObject("Fallback core");
                var shot = new CoreVictoryShot(camera, fallback.transform, null);
                shot.Begin(); shot.Advance(1.5f); shot.End();
                UnityEngine.Object.Destroy(fallback);
                Require(Vector3.Distance(camera.transform.position, position) < 0.001f, "Fallback restore failed.");
                Debug.Log("VICTORY_VALIDATION_PASS: play-mode approach, hold, automatic return, HUD, input lock, repeated skip, disable cleanup, no-animator fallback, paused real-time playback, actual round-clear integration.");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
    private static void Capture(string path)
    {
        var rt = new RenderTexture(1600, 900, 24);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var canvas = ((UIManager)Field("ui")).GetComponent<Canvas>();
        var mode = canvas.renderMode; var worldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 0.1f;
        Canvas.ForceUpdateCanvases();
        camera.targetTexture = rt;
        camera.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active;
        canvas.renderMode = mode; canvas.worldCamera = worldCamera; canvas.planeDistance = distance;
        UnityEngine.Object.Destroy(texture); rt.Release(); UnityEngine.Object.Destroy(rt);
    }
}
