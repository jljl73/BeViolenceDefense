using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
internal static class SoundPlayValidation
{
    const string Key = "SoundPlayValidation.Running";
    const string ScenePath = "Assets/01_Scenes/03_Test/GameScene_LeftCoreHud.unity";
    const string Prefix = "BeViolenceDefense.Audio.";
    static readonly string[] VolumeKeys = { "Master", "Bgm", "Sfx", "Ui" };
    static readonly Dictionary<string,(bool Exists,float Value)> backup = new();
    static bool mutedExists, backedUp;
    static int mutedValue, stage, instanceId, hitFrames, peakHitVoices;
    static float listenerVolume;
    static double started;
    static AudioClip quiet, musicClip, hitClip;
    static VoxelDefensePrototype flow;
    static DefenseBattle battle;
    static SoundPlayValidation() { EditorApplication.update += Update; }
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string message) { if (!ok) throw new Exception(message); }
    static AudioSource Source(SoundManager manager,string name) => manager.transform.Find(name).GetComponent<AudioSource>();
    static AudioSource[] Sources(SoundManager manager) => manager.GetComponentsInChildren<AudioSource>();
    static bool Near(float a,float b) => Mathf.Abs(a-b)<0.0001f;
    static object Field(string name) => typeof(VoxelDefensePrototype).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(flow);
    static void Update()
    {
        if (!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling) return;
        try
        {
            var manager=SoundManager.Instance;
            Check(manager!=null,"Automatic manager creation failed.");
            switch(stage)
            {
                case 0:
                    started=EditorApplication.timeSinceStartup;
                    foreach(string key in VolumeKeys) backup[key]=(PlayerPrefs.HasKey(Prefix+key),PlayerPrefs.GetFloat(Prefix+key,1f));
                    mutedExists=PlayerPrefs.HasKey(Prefix+"Muted"); mutedValue=PlayerPrefs.GetInt(Prefix+"Muted",0);
                    listenerVolume=AudioListener.volume; AudioListener.volume=0; backedUp=true;
                    quiet=AudioClip.Create("Validation Silent SFX",480000,1,48000,false);
                    musicClip=AudioClip.Create("Validation Silent BGM",480000,1,48000,false);
                    manager.StopAll();manager.SetMuted(false);manager.SetMasterVolume(0.5f);
                    manager.SetVolume(SoundChannel.Bgm,0.6f);manager.SetVolume(SoundChannel.Sfx,0.4f);manager.SetVolume(SoundChannel.Ui,0.3f);
                    Check(!manager.PlaySfx(null)&&!manager.PlayBgm(null),"Missing clips were accepted.");
                    for(int i=0;i<SoundManager.MaxSfxVoices;i++)Check(manager.PlaySfx(quiet,0.5f),"SFX pool did not accept capacity.");
                    Check(!manager.PlaySfx(quiet),"SFX pool exceeded capacity.");
                    Check(manager.PlayUi(quiet,0.5f),"UI was starved by the SFX pool.");
                    Check(Near(Source(manager,"SFX 01").volume,0.1f)&&Near(Source(manager,"UI 01").volume,0.075f),"Channel gain calculation failed.");
                    manager.SetMasterVolume(0.8f);
                    Check(Near(Source(manager,"SFX 01").volume,0.16f),"Live gain update lost the per-clip volume.");
                    manager.StopSfx();Check(!Sources(manager).Any(s=>s.name.StartsWith("SFX")&&s.isPlaying),"StopSfx failed.");
                    for(int i=0;i<6;i++)Check(manager.PlaySfx(quiet,1f,6),"Clip cap rejected a valid voice.");
                    Check(!manager.PlaySfx(quiet,1f,6),"Per-clip cap failed.");
                    Check(manager.PlayBgm(musicClip,0.5f),"BGM failed.");
                    Source(manager,"BGM").time=1.5f;
                    manager.PlayBgm(musicClip,0.25f);
                    Check(Source(manager,"BGM").time>1.4f,"Same BGM restarted.");
                    manager.SetMuted(true);
                    Check(Sources(manager).All(s=>s.mute)&&!manager.PlaySfx(quiet),"Mute failed.");
                    manager.SetMuted(false);manager.SetMasterVolume(0.5f);manager.SaveSettings();
                    instanceId=manager.GetInstanceID();
                    new GameObject("Duplicate Sound Manager").AddComponent<SoundManager>();
                    stage=1;
                    Debug.Log("SOUND_API_PASS: pools, UI isolation, per-clip cap, live volume, mute, null clips, BGM continuity");
                    break;
                case 1:
                    Check(UnityEngine.Object.FindObjectsByType<SoundManager>(FindObjectsSortMode.None).Length==1,"Duplicate manager survived.");
                    Check(SoundManager.Instance.GetInstanceID()==instanceId,"Duplicate replaced the original.");
                    Check(Sources(manager).Length==1+SoundManager.MaxSfxVoices+SoundManager.MaxUiVoices,"AudioSources were duplicated.");
                    UnityEngine.Object.Destroy(manager.gameObject);stage=2;
                    break;
                case 2:
                    if(manager.GetInstanceID()==instanceId)return;
                    Check(Near(manager.MasterVolume,0.5f)&&Near(manager.BgmVolume,0.6f)&&Near(manager.SfxVolume,0.4f)&&Near(manager.UiVolume,0.3f)&&!manager.Muted,"Saved volumes did not reload.");
                    manager.PlayBgm(musicClip);manager.PlaySfx(quiet);manager.PlayUi(quiet);
                    instanceId=manager.GetInstanceID();
                    EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
                    stage=3;
                    Debug.Log("SOUND_PERSISTENCE_PASS: recreation reloads all saved volumes");
                    break;
                case 3:
                    flow=UnityEngine.Object.FindFirstObjectByType<VoxelDefensePrototype>(); if(flow==null)return;
                    battle=(DefenseBattle)Field("battle");if(battle==null)return;
                    Check(manager.GetInstanceID()==instanceId,"Scene reload replaced the manager.");
                    Check(manager.CurrentBgm==musicClip&&Source(manager,"BGM").isPlaying,"Scene reload stopped BGM.");
                    Check(!Sources(manager).Any(s=>s.name!="BGM"&&s.isPlaying),"Transient sounds survived scene reload.");
                    Check(flow.GetComponents<AudioSource>().Length==0,"BattleSoundPlayer still owns an independent pool.");
                    hitClip=(AudioClip)Field("hitSound");Check(hitClip!=null,"The existing hit sound was not assigned.");
                    manager.SetMasterVolume(1f);manager.SetVolume(SoundChannel.Sfx,1f);
                    typeof(VoxelDefensePrototype).GetField("battleSpeed",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(flow,2);
                    typeof(VoxelDefensePrototype).GetMethod("StartBattle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(flow,null);
                    stage=4;
                    Debug.Log("SOUND_SCENE_PASS: one manager, BGM retained, transient voices stopped");
                    break;
                case 4:
                    int hits=Sources(manager).Count(s=>s.clip==hitClip&&s.isPlaying);
                    if(hits>0)hitFrames++;
                    peakHitVoices=Mathf.Max(peakHitVoices,hits);
                    Check(hits<=6,"Existing hit cap was exceeded.");
                    foreach(var source in Sources(manager).Where(s=>s.clip==hitClip&&s.isPlaying))Check(Near(source.volume,0.24f),"Existing hit volume changed.");
                    if(battle.CurrentPhase!=DefenseBattle.Phase.Combat)
                    {
                        Check(hitFrames>0,"Actual combat never played through the manager.");
                        Check(battle.CurrentPhase==DefenseBattle.Phase.Preparation,"Actual wave failed.");
                        Debug.Log($"SOUND_PLAYMODE_PASS: hitFrames={hitFrames}, peakHitVoices={peakHitVoices}, core={battle.CoreHealth}, remaining={battle.Enemies.Count}");
                        Finish(0);
                    }
                    break;
            }
            if(EditorApplication.timeSinceStartup-started>120)throw new Exception("Sound validation timed out.");
        }
        catch(Exception e){Debug.LogException(e);Finish(1);}
    }
    static void Finish(int code)
    {
        if(backedUp)
        {
            var manager=SoundManager.Instance;
            manager?.StopAll();
            if(manager!=null)
            {
                manager.SetMasterVolume(backup["Master"].Value);
                manager.SetVolume(SoundChannel.Bgm,backup["Bgm"].Value);
                manager.SetVolume(SoundChannel.Sfx,backup["Sfx"].Value);
                manager.SetVolume(SoundChannel.Ui,backup["Ui"].Value);
                manager.SetMuted(mutedValue!=0);manager.SaveSettings();
            }
            foreach(var pair in backup)
                if(pair.Value.Exists)PlayerPrefs.SetFloat(Prefix+pair.Key,pair.Value.Value);else PlayerPrefs.DeleteKey(Prefix+pair.Key);
            if(mutedExists)PlayerPrefs.SetInt(Prefix+"Muted",mutedValue);else PlayerPrefs.DeleteKey(Prefix+"Muted");
            PlayerPrefs.Save();AudioListener.volume=listenerVolume;
            Debug.Log("SOUND_TEST_SETTINGS_RESTORED");
        }
        SessionState.SetBool(Key,false);EditorApplication.Exit(code);
    }
}
