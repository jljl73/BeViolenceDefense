using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class UnityAiSfxBatch
{
    static UnityAiSfxBatch() { if(File.Exists("/tmp/unity-ai-sfx-resume")) EditorApplication.delayCall += Run; }
    static readonly string Report = "/tmp/unity-ai-sfx-report.txt";
    static void Note(string s) { File.AppendAllText(Report, DateTime.UtcNow.ToString("o")+" "+s+"\n"); Debug.Log("SFX_BATCH "+s); }
    static Type Find(string n) => AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n)).First(t=>t!=null);
    [Serializable] public class Job { public string id; public float duration; public string prompt; }
    [Serializable] public class Jobs { public Job[] items; }
    public static async void Run()
    {
        try
        {
            Note("START");
            using(var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
            {
                var api=Find("Unity.AI.Generators.Tools.AssetGenerators");
                var task=(Task)api.GetMethod("GetAvailableModelsAsync").Invoke(null,new object[]{true,cancel.Token});
                await task;
                foreach(var m in (IEnumerable)task.GetType().GetProperty("Result").GetValue(task))
                {
                    var desc=(string)m.GetType().GetField("Description").GetValue(m);
                    if(desc.IndexOf("sound",StringComparison.OrdinalIgnoreCase)>=0)
                        Note("MODEL "+m.GetType().GetField("ModelId").GetValue(m)+" "+desc);
                }
                var jobs=JsonUtility.FromJson<Jobs>(File.ReadAllText("/Users/mignon/BeViolenceDefense/docs/audio/unity-ai-sfx-batch.json"));
                foreach(var job in jobs.items)
                {
                var expected="Assets/Audio/SFX/UnityAI/"+job.id+".wav";
                if(File.Exists(expected)&&new FileInfo(expected).Length>1000) {Note("SKIP downloaded "+job.id);continue;}
                if(File.Exists(expected)) AssetDatabase.DeleteAsset(expected);
                Note("GENERATING "+job.id);
                var settingsType=Find("Unity.AI.Generators.Tools.SoundSettings");
                var settings=Activator.CreateInstance(settingsType);
                settingsType.GetField("DurationInSeconds").SetValue(settings,job.duration);
                var paramType=Find("Unity.AI.Generators.Tools.GenerationParameters`1").MakeGenericType(settingsType);
                var p=Activator.CreateInstance(paramType);
                paramType.GetField("AssetType").SetValue(p,typeof(AudioClip));
                paramType.GetField("Prompt").SetValue(p,job.prompt);
                paramType.GetField("SavePath").SetValue(p,"Assets/Audio/SFX/UnityAI/"+job.id+".wav");
                paramType.GetField("ModelId").SetValue(p,"elevenlabs-sound-effects-v2");
                paramType.GetField("Settings").SetValue(p,settings);
                paramType.GetField("PermissionCheckAsync").SetValue(p,(Func<string,long,Task>)((path,cost)=>{Note("QUOTE "+path+" points="+cost);return Task.CompletedTask;}));
                Directory.CreateDirectory("Assets/Audio/SFX/UnityAI");
                var method=api.GetMethods().Single(m=>m.Name=="GenerateAsync"&&m.IsGenericMethodDefinition);
                var handle=method.MakeGenericMethod(settingsType).Invoke(null,new[]{p,(object)cancel.Token});
                var download=(Task)handle.GetType().GetProperty("DownloadTask").GetValue(handle);
                var completed=await Task.WhenAny(download,Task.Delay(TimeSpan.FromMinutes(3)));
                if(completed!=download)throw new TimeoutException("Unity AI download did not finish within 3 minutes");
                await download;
                foreach(var msg in (IEnumerable)handle.GetType().GetProperty("Messages").GetValue(handle)) Note("FEEDBACK "+msg);
                var clip=download.GetType().GetProperty("Result").GetValue(download) as AudioClip;
                Note("RESULT "+(clip==null?"null":AssetDatabase.GetAssetPath(clip)+" samples="+clip.samples+" frequency="+clip.frequency));
                AssetDatabase.SaveAssets();
                if(clip==null)throw new Exception("Generation returned no clip: "+job.id);
                }
                File.Delete("/tmp/unity-ai-sfx-resume");
                Note("END_BATCH");
                EditorApplication.Exit(0);
            }
        }
        catch(Exception e) { File.Delete("/tmp/unity-ai-sfx-resume"); Note("ERROR "+e); EditorApplication.Exit(3); }
    }
}
