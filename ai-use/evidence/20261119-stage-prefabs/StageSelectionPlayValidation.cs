using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class StageSelectionPlayValidation
{
    const string Key="StageSelectionPlayValidation.Running";
    const string Scene="Assets/01_Scenes/02_Gameplay/GameScene.unity";
    const string Folder="/tmp/stage-selection-captures";
    static int index,phase,frames;
    static double start;
    static StageSelectionController controller;
    static VoxelDefensePrototype game;
    static UIManager ui;
    static DefenseBattle battle;
    static StageSelectionPlayValidation(){EditorApplication.update+=Update;}
    public static void Run(){EditorSceneManager.OpenScene(Scene);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static object Field(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
    static void Invoke(string method)=>typeof(VoxelDefensePrototype).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null);
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Update()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(start==0){start=EditorApplication.timeSinceStartup;Directory.CreateDirectory(Folder);}
            if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Stage play validation timed out.");
            if(controller==null)
            {
                controller=Object.FindFirstObjectByType<StageSelectionController>();if(controller==null)return;
                game=Object.FindFirstObjectByType<VoxelDefensePrototype>();ui=Object.FindFirstObjectByType<UIManager>();frames=0;
            }
            frames++;
            if(phase==0)
            {
                if(frames<8)return;
                Check(controller.StageCount==4&&ui.HasOpenPopup,"Initial four-stage modal not open");
                Check(!game.enabled&&Field(game,"battle")==null&&Object.FindObjectsByType<StageContent>(FindObjectsSortMode.None).Length==0,"Game initialized before choosing a stage");
                Check(ui.BlocksBoardPointer(Vector2.zero),"Selection modal did not block board input");
                controller.SelectStage(-1);controller.SelectStage(4);Check(controller.CurrentStage==null,"Invalid index loaded a stage");
                var popup=(StageSelectionPopup)Field(controller,"popup");var buttons=(Button[])Field(popup,"buttons");
                Check(buttons.Length==4&&buttons.All(b=>b.gameObject.activeInHierarchy),"Four choice buttons not visible");
                if(index==0)Capture((Camera)((GameObject)Field(controller,"selectionCamera")).GetComponent<Camera>(),"selection.png",true);
                buttons[index].onClick.Invoke();buttons[(index+1)%4].onClick.Invoke();
                Check(controller.SelectedStage==index,"Double click changed the chosen stage");
                phase=1;frames=0;return;
            }
            if(phase==1)
            {
                if(frames<8)return;
                Check(game.enabled&&!ui.HasOpenPopup,"Game did not enter preparation");
                Check(Object.FindObjectsByType<StageContent>(FindObjectsSortMode.None).Length==1,"Stage content duplicated");
                Check(Object.FindObjectsByType<UIManager>(FindObjectsSortMode.None).Length==1,"UIManager duplicated");
                Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"EventSystem duplicated");
                Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a=>a.isActiveAndEnabled)==1,"AudioListener duplicated");
                var content=controller.CurrentStage;
                Check((Camera)Field(game,"boardCamera")==content.BoardCamera,"Wrong stage camera");
                var grid=(DefenseGrid)Field(game,"grid");Check(grid.Width==(index==0?25:17),"Wrong stage terrain");
                battle=(DefenseBattle)Field(game,"battle");Check(battle.CurrentPhase==DefenseBattle.Phase.Preparation,"Not in preparation");
                Capture(content.BoardCamera,$"stage-{index}.png",true);
                Invoke("StartBattle");phase=2;return;
            }
            if(phase==2)
            {
                if(battle.TickIndex<75)return;
                Check(battle.Enemies.Count>0,"No enemies spawned in selected stage");
                Debug.Log($"STAGE_PLAY_PASS index={index} stage={controller.CurrentStage.name} width={((DefenseGrid)Field(game,"grid")).Width} ticks={battle.TickIndex} units={battle.Enemies.Count}");
                Invoke("Restart");controller=null;game=null;ui=null;battle=null;phase=3;return;
            }
            if(phase==3)
            {
                if(frames<8||controller.CurrentStage==null)return;
                Check(controller.SelectedStage==index&&!ui.HasOpenPopup,"Retry did not preserve selected stage");
                battle=(DefenseBattle)Field(game,"battle");Check(battle.CurrentPhase==DefenseBattle.Phase.Preparation&&battle.TickIndex==0&&battle.Enemies.Count==0,"Retry did not reset combat");
                Check(Object.FindObjectsByType<StageContent>(FindObjectsSortMode.None).Length==1,"Retry duplicated content");
                Debug.Log($"STAGE_RETRY_PASS index={index}");
                if(++index==4){Debug.Log("STAGE_SELECTION_PLAY_PASS 4 buttons, modal gating, per-stage spawn, retry, singleton UI/audio, double-click guard");SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
                SceneManager.LoadScene(Scene);controller=null;game=null;ui=null;battle=null;phase=0;
            }
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
    static void Capture(Camera camera,string name,bool withUi)
    {
        var canvas=ui.GetComponent<Canvas>();var mode=canvas.renderMode;var world=canvas.worldCamera;float plane=canvas.planeDistance;
        int mask=camera.cullingMask;camera.cullingMask=~0;
        if(withUi){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();}
        var rt=new RenderTexture(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name,image.EncodeToPNG());
        camera.targetTexture=previous;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=world;canvas.planeDistance=plane;camera.cullingMask=mask;
        Object.Destroy(image);rt.Release();Object.Destroy(rt);
    }
}
