using UnityEditor; using UnityEngine; using System.Text; using System.Linq; using System.Collections.Generic;
// APPLY=false: report only. true: move/delete and save prefabs.
bool APPLY = false;
const float FootTol = 0.1f;   // 정점이 그리드 안으로 이만큼 넘어오면 침범
const float ViewTol = 0.45f;  // 카메라 시점 투영이 이만큼 넘어오면 가림
const float MaxPush = 3f;
var dir = Quaternion.Euler(55, 315, 0) * Vector3.forward;
var sb = new StringBuilder();
foreach (var n in new[] { "GoldenMaple", "Snowfield", "Graveyard", "Capital" })
{
    var path = $"Assets/03_Prefabs/05_Stages/Stage_{n}.prefab";
    var root = PrefabUtility.LoadPrefabContents(path);
    var st = (StageData)new SerializedObject(root.GetComponent<StageContent>()).FindProperty("stage").objectReferenceValue;
    var obs = new HashSet<Vector2Int>(st.BlockedCells());
    float x0 = -0.5f, x1 = st.width - 0.5f, z0 = -0.5f, z1 = st.depth - 0.5f;
    float Pen(float x, float z) => (x > x0 && x < x1 && z > z0 && z < z1) ? Mathf.Min(x - x0, x1 - x, z - z0, z1 - z) : 0f;
    int moved = 0, deleted = 0;
    var props = root.transform.Find("Stage Environment/Props").Cast<Transform>().ToList();
    foreach (var p in props)
    {
        var pts = new List<Vector3>();
        foreach (var mf in p.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null && mf.GetComponent<Renderer>() != null)
            {
                var v = mf.sharedMesh.vertices; int step = Mathf.Max(1, v.Length / 400);
                for (int i = 0; i < v.Length; i += step) pts.Add(mf.transform.TransformPoint(v[i]));
            }
        foreach (var sk in p.GetComponentsInChildren<SkinnedMeshRenderer>())
        { var b = sk.bounds; pts.Add(b.min); pts.Add(b.max); }
        if (pts.Count == 0) continue;
        (float foot, float view) Measure(Vector3 off)
        {
            float f = 0, w = 0;
            foreach (var q0 in pts)
            {
                var q = q0 + off;
                f = Mathf.Max(f, Pen(q.x, q.z));
                var g = q + dir * (Mathf.Max(0, q.y) / -dir.y);
                w = Mathf.Max(w, Pen(g.x, g.z));
            }
            return (f, w);
        }
        bool Bad((float foot, float view) m) => m.foot > FootTol || m.view > ViewTol;
        var m0 = Measure(Vector3.zero);
        if (!Bad(m0)) continue;
        var pos = p.position; var nm = p.name;
        var c = new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));
        bool centerIn = Pen(pos.x, pos.z) > 0;
        // 가장 가까운 바깥 방향으로 민다.
        var normals = new[] { (pos.x - x0, Vector3.left), (x1 - pos.x, Vector3.right), (pos.z - z0, Vector3.back), (z1 - pos.z, Vector3.forward) };
        Vector3 best = Vector3.zero; float bestDist = float.MaxValue;
        if (!centerIn || !obs.Contains(c))
            foreach (var (_, nrm) in normals.OrderBy(t => centerIn ? t.Item1 : -Mathf.Abs(t.Item1)))
            {
                for (float d = 0.25f; d <= MaxPush; d += 0.25f)
                    if (!Bad(Measure(nrm * d))) { if (d < bestDist) { bestDist = d; best = nrm * d; } break; }
            }
        string tag;
        if (best != Vector3.zero) { tag = $"MOVE {bestDist:F2}"; moved++; if (APPLY) p.position += best; }
        else { tag = "DEL"; deleted++; if (APPLY) Object.DestroyImmediate(p.gameObject); }
        sb.AppendLine($"  {tag} {nm} ({pos.x:F1},{pos.z:F1}) foot {m0.foot:F2} view {m0.view:F2}");
    }
    sb.Insert(0, "");
    sb.AppendLine($"== {n} {st.width}x{st.depth}: move {moved}, delete {deleted} / props {props.Count}");
    if (APPLY) PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
}
return sb.ToString();
