using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
internal static class RouteFlowPlayValidation
{
    const string Key="RouteFlowPlayValidation.Running";
    const string Folder="/tmp/continuous-route-frames";
    static VoxelDefensePrototype flow;
    static DefenseBoardView view;
    static DefenseGrid grid;
    static Camera camera;
    static int frame, minimumVisible=int.MaxValue;
    static Vector3 focus;
    static List<Vector3> previous;
    static RouteFlowPlayValidation(){EditorApplication.update+=Update;}
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/01_Scenes/02_Gameplay/GameScene.unity");
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static object Field(string name)=>typeof(VoxelDefensePrototype).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow);
    static void Update()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(flow==null)
            {
                flow=UnityEngine.Object.FindFirstObjectByType<VoxelDefensePrototype>();if(flow==null)return;
                view=(DefenseBoardView)Field("view");grid=(DefenseGrid)Field("grid");camera=(Camera)Field("boardCamera");
                if(view==null)return;
                flow.enabled=false;
                view.ShowPlanningTiles(grid.Routes);
                focus=grid.Center;
                foreach(var path in grid.Routes)
                {
                    bool found=false;
                    for(int i=2;i<path.Length-2;i++)
                        if(Vector3.Angle(path[i]-path[i-1],path[i+1]-path[i])>45f){focus=path[i];found=true;break;}
                    if(found)break;
                }
                Directory.CreateDirectory(Folder);Capture("overview.png",false);
            }
            var markers=((List<MeshRenderer>)typeof(DefenseBoardView).GetField("routeMarkers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view));
            view.AdvanceRouteMarkers(1f/15f);
            var active=markers.Where(m=>m.enabled).ToArray();
            minimumVisible=Math.Min(minimumVisible,active.Length);
            if(active.Length<grid.Routes.Count*2)throw new Exception("The route stream disappeared.");
            var positions=active.Select(m=>m.transform.localPosition).ToList();
            if(previous!=null)
                foreach(var p in previous)
                {
                    if(grid.Routes.Any(r=>Vector3.Distance(p,r[^1])<.5f))continue;
                    if(positions.Min(q=>Vector3.Distance(q,p))>.06f)throw new Exception("A visible interior arrow disappeared or jumped.");
                }
            previous=positions;
            if(frame<45)Capture($"frame-{frame:D3}.png",true);
            if(frame++<90)return;
            var before=active.Select(m=>m.transform.localPosition).ToArray();
            view.AdvanceRouteMarkers(0f);
            for(int i=0;i<active.Length;i++)if(active[i].transform.localPosition!=before[i])throw new Exception("Paused arrows moved.");
            view.ShowPlanningTiles(null);
            if(markers.Any(m=>m.enabled))throw new Exception("Hidden routes left stale arrows.");
            view.ShowBattleRoutes(grid.Routes);
            view.AdvanceRouteMarkers(.1f);
            if(!markers.Any(m=>m.enabled))throw new Exception("Battle route arrows did not reactivate.");
            Capture("battle-routes.png",false);
            Debug.Log($"ROUTE_FLOW_PLAY_PASS frames={frame} minimumVisible={minimumVisible} routes={grid.Routes.Count}; continuous wraps, pause, hide/rebuild, battle tint passed");
            SessionState.SetBool(Key,false);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
    static void Capture(string name,bool close)
    {
        var position=camera.transform.position;float size=camera.orthographicSize;
        if(close){camera.transform.position=flow.transform.TransformPoint(focus)-camera.transform.forward*20f;camera.orthographicSize=3f;}
        var rt=new RenderTexture(960,640,24);var target=camera.targetTexture;var old=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(960,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();
        File.WriteAllBytes(Folder+"/"+name,image.EncodeToPNG());
        camera.targetTexture=target;RenderTexture.active=old;camera.transform.position=position;camera.orthographicSize=size;
        UnityEngine.Object.Destroy(image);rt.Release();UnityEngine.Object.Destroy(rt);
    }
}
