using UnityEngine;
using UnityEngine.Rendering;

// 몸의 굴곡과 분리된 부드러운 원기둥 보호막. 메시/재질의 수명은 보드가 관리한다.
internal sealed class UnitShieldSurface
{
    private static readonly int Strength = Shader.PropertyToID("_Strength");
    private static readonly int Phase = Shader.PropertyToID("_Phase");
    private static readonly int Impact = Shader.PropertyToID("_Impact");
    private static readonly int HitPoint = Shader.PropertyToID("_HitPoint");
    private readonly MeshRenderer renderer;
    private readonly MaterialPropertyBlock properties = new();

    public UnitShieldSurface(Transform owner, Mesh mesh, Material material)
    {
        var root = new GameObject("Shield Cylinder", typeof(MeshFilter), typeof(MeshRenderer)) { layer = owner.gameObject.layer };
        root.transform.SetParent(owner, false);
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = 44;
        renderer.enabled = false;
    }

    public static Mesh CreateMesh()
    {
        // 뚜껑과 선 없이 측면만 만든다. 양 끝과 측면의 경계는 셰이더에서 흐린다.
        const int segments = 64;
        var vertices = new Vector3[(segments + 1) * 2];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i * 2] = radial - Vector3.up * .5f;
            vertices[i * 2 + 1] = radial + Vector3.up * .5f;
            normals[i * 2] = normals[i * 2 + 1] = radial;
            uv[i * 2] = new Vector2((float)i / segments, 0f);
            uv[i * 2 + 1] = new Vector2((float)i / segments, 1f);
            if (i == segments) continue;
            int t = i * 6, v = i * 2;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
        }
        var mesh = new Mesh { name = "Soft Shield Cylinder", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }

    public void Sync(Bounds bounds, float strength, float phase, float impact, Vector3 incomingDirection)
    {
        float bodyHeight = Mathf.Max(.1f, bounds.size.y);
        float radius = Mathf.Max(bodyHeight * .38f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.18f);
        float height = bodyHeight * 1.25f;
        Transform transform = renderer.transform;
        transform.SetPositionAndRotation(bounds.center + Vector3.up * (bodyHeight * .115f), Quaternion.identity);
        Vector3 parentScale = transform.parent.lossyScale;
        transform.localScale = new Vector3(radius / parentScale.x, height / parentScale.y, radius / parentScale.z);
        Vector3 incoming = Vector3.ProjectOnPlane(incomingDirection, Vector3.up).normalized;
        Vector3 point = bounds.center + incoming * radius;
        properties.SetFloat(Strength, strength);
        properties.SetFloat(Phase, phase);
        properties.SetFloat(Impact, impact);
        properties.SetVector(HitPoint, new Vector4(point.x, point.y, point.z, height * .65f));
        renderer.SetPropertyBlock(properties);
        renderer.enabled = strength > .001f;
    }

    public void Hide() => renderer.enabled = false;
}
