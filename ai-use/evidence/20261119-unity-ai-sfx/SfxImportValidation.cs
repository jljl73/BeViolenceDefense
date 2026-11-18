using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class SfxImportValidation
{
    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var files=Directory.GetFiles("Assets/Audio/SFX/UnityAI","*.wav");
            int valid=0;
            foreach(var path in files)
            {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if(clip==null||clip.samples==0||clip.length<.04f||clip.channels!=1)throw new Exception("Invalid clip "+path);
                clip.LoadAudioData();
                var samples=new float[clip.samples*clip.channels];
                if(!clip.GetData(samples,0))throw new Exception("Cannot read audio "+path);
                float peak=0;foreach(var v in samples)peak=Math.Max(peak,Math.Abs(v));
                if(peak<.001f||peak>=1)throw new Exception("Invalid signal "+path+" peak="+peak);
                Debug.Log("SFX_IMPORT_OK "+path+" duration="+clip.length+" channels="+clip.channels+" peak="+peak);
                valid++;
            }
            if(valid!=23)throw new Exception("Expected 23 clips, got "+valid);
            Debug.Log("SFX_IMPORT_PASS "+valid);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogError(e);EditorApplication.Exit(1);}
    }
}
