using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
var results = new List<object>();
foreach (string name in new[] { "CozyWindmillCottage", "ToppledStoneTower" })
{
    string path = "Assets/03_Prefabs/04_World/" + name + "_2x2.prefab";
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var root = UnityEngine.Object.Instantiate(prefab);
    try {
        var editorType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("StageEditorWindow")).First(t => t != null);
        var footprint = (Vector2Int)editorType.GetMethod("FootprintFromName", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { prefab.name });
        if (footprint != new Vector2Int(2,2)) throw new Exception("Invalid footprint");
        var stage = ScriptableObject.CreateInstance<StageData>();
        stage.obstacles.Add(new StageData.Obstacle { prefab = prefab, cell = new Vector2Int(2,2), size = footprint });
        if (stage.BlockedCells().Count() != 4 || stage.CreateGrid().FixedWalls.Count != 4) throw new Exception("Invalid blocked cells");
        UnityEngine.Object.DestroyImmediate(stage);
        var rs = root.GetComponentsInChildren<Renderer>();
        var bounds = rs[0].bounds;
        foreach (var r in rs.Skip(1)) bounds.Encapsulate(r.bounds);
        if (Mathf.Abs(bounds.size.x - 1.92f) > .001f || bounds.size.z > 1.921f || Mathf.Abs(bounds.min.y) > .001f) throw new Exception("Invalid model bounds");
        if (rs.Any(r => r.sharedMaterials.Any(m => m == null || m.shader.name != "Universal Render Pipeline/Lit" || m.mainTexture == null))) throw new Exception("Invalid material");
        var preview = new PreviewRenderUtility();
        try {
            preview.AddSingleGO(root);
            preview.camera.transform.position = bounds.center + new Vector3(3.5f, 4.5f, -5.5f);
            preview.camera.transform.LookAt(bounds.center);
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = Mathf.Max(bounds.size.y, 2f) * .8f;
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 100f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.17f,.19f,.22f,1);
            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
            preview.lights[1].intensity = .7f;
            preview.ambientColor = new Color(.5f,.5f,.5f);
            preview.BeginPreview(new Rect(0,0,768,768), GUIStyle.none);
            preview.Render(true);
            var texture = preview.EndPreview();
            var rt = RenderTexture.GetTemporary(768,768,0,RenderTextureFormat.ARGB32);
            Graphics.Blit(texture,rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(768,768,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,768,768),0,0); image.Apply();
            File.WriteAllBytes("../ai-use/evidence/20261119-structures/" + name + ".png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        } finally { preview.Cleanup(); }
        results.Add(new { path, footprint = footprint.ToString(), blockedCells = 4, boundsSize = bounds.size.ToString("F4"), bottom = bounds.min.y, materialsValid = true });
    } finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
}
return results.ToArray();
