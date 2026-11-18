using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Collections.Generic;
if (!EditorApplication.isPlaying) throw new Exception("Not playing");
var results = new List<object>();
foreach (string name in new[] { "CozyWindmillCottage", "ToppledStoneTower" })
{
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/04_World/" + name + "_2x2.prefab");
    for (int rotation = 0; rotation < 4; rotation++) {
        var root = UnityEngine.Object.Instantiate(prefab);
        try {
            var obstacle = new StageData.Obstacle { prefab = prefab, cell = new Vector2Int(3,3), size = new Vector2Int(2,2), rotation = rotation };
            typeof(StageData).Assembly.GetType("DefenseBoardView").GetMethod("FitObstacle", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { root.transform, obstacle });
            var renderers = root.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            if (b.size.x > 1.921f || b.size.z > 1.921f || Mathf.Abs(b.min.y) > .001f || Vector2.Distance(new Vector2(b.center.x,b.center.z), new Vector2(3.5f,3.5f)) > .001f) throw new Exception("Fit failed: " + name);
            results.Add(new { name, rotation, passed = true, bounds = b.ToString(), frame = Time.frameCount });
        } finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
return results.ToArray();

