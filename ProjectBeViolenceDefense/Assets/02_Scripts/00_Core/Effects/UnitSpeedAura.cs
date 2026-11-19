using UnityEngine;
using UnityEngine.Rendering;

// 몸을 감싸는 부피 대신, 실제로 지나온 경로 위로 빛이 흐르는 두 갈래의 부드러운 띠.
internal sealed class UnitSpeedAura
{
    private const int Points = 16;
    private static readonly int Strength = Shader.PropertyToID("_Strength");
    private static readonly int Phase = Shader.PropertyToID("_Phase");
    private static readonly int Motion = Shader.PropertyToID("_Motion");
    private static readonly int Height = Shader.PropertyToID("_Height");
    private static readonly int Path = Shader.PropertyToID("_Path");
    private readonly Transform owner;
    private readonly MeshRenderer renderer;
    private readonly MaterialPropertyBlock properties = new();
    private readonly Vector3[] history = new Vector3[48];
    private readonly Vector4[] path = new Vector4[Points];
    private Vector3 previousPosition;
    private Vector3 direction;
    private int historyCount;
    private float motion;

    public UnitSpeedAura(Transform owner, Mesh mesh, Material material)
    {
        this.owner = owner;
        previousPosition = owner.position;
        direction = Vector3.ProjectOnPlane(owner.forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
        Remember(owner.position);
        var root = new GameObject("Speed Flow Aura", typeof(MeshFilter), typeof(MeshRenderer)) { layer = owner.gameObject.layer };
        root.transform.SetParent(owner, false);
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = 46;
        renderer.enabled = false;
    }

    public static Mesh CreateMesh()
    {
        var vertices = new Vector3[Points * 4];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[(Points - 1) * 12];
        for (int strip = 0; strip < 2; strip++)
        for (int i = 0; i < Points; i++)
        {
            float t = (float)i / (Points - 1);
            int v = strip * Points * 2 + i * 2;
            vertices[v] = new Vector3(-1, strip * 2 - 1, t);
            vertices[v + 1] = new Vector3(1, strip * 2 - 1, t);
            uv[v] = new Vector2(t, 0);
            uv[v + 1] = new Vector2(t, 1);
            if (i == Points - 1) continue;
            int tri = (strip * (Points - 1) + i) * 6;
            triangles[tri] = v; triangles[tri + 1] = v + 2; triangles[tri + 2] = v + 1;
            triangles[tri + 3] = v + 1; triangles[tri + 4] = v + 2; triangles[tri + 5] = v + 3;
        }
        var mesh = new Mesh { name = "Speed Flow Ribbons", vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void Remember(Vector3 position)
    {
        int last = Mathf.Min(historyCount, history.Length - 1);
        for (int i = last; i > 0; i--) history[i] = history[i - 1];
        history[0] = position;
        historyCount = Mathf.Min(historyCount + 1, history.Length);
    }

    private Vector3 Behind(float distance)
    {
        Vector3 from = owner.position;
        Vector3 back = -direction;
        for (int i = 0; i < historyCount; i++)
        {
            Vector3 delta = history[i] - from;
            float length = delta.magnitude;
            if (length > .00001f)
            {
                back = delta / length;
                if (distance <= length) return from + back * distance;
                distance -= length;
            }
            from = history[i];
        }
        return from + back * distance;
    }

    public void Sync(Bounds bounds, float strength, float phase, float deltaTime)
    {
        float height = Mathf.Max(.1f, bounds.size.y);
        Vector3 step = Vector3.ProjectOnPlane(owner.position - previousPosition, Vector3.up);
        previousPosition = owner.position;
        if (deltaTime > 0f)
        {
            bool teleported = step.sqrMagnitude > height * height * 4f;
            if (teleported) { historyCount = 0; motion = 0f; }
            float target = teleported ? 0f : Mathf.Clamp01(step.magnitude / (deltaTime * height * 2f));
            motion = Mathf.MoveTowards(motion, target, deltaTime * 5f);
            if (target > .01f) direction = step.normalized;
            if (historyCount == 0 || (owner.position - history[0]).sqrMagnitude >= height * height * .0004f)
                Remember(owner.position);
        }
        Transform transform = renderer.transform;
        transform.SetPositionAndRotation(owner.position, Quaternion.identity);
        Vector3 parentScale = transform.parent.lossyScale;
        transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
        float reach = height * Mathf.Lerp(.42f, 1.65f, motion);
        float lift = bounds.min.y - owner.position.y + height * .22f;
        Bounds localBounds = new(Vector3.zero, Vector3.zero);
        for (int i = 0; i < Points; i++)
        {
            Vector3 point = Behind(reach * i / (Points - 1)) - owner.position + Vector3.up * lift;
            path[i] = new Vector4(point.x, point.y, point.z, 0f);
            localBounds.Encapsulate(point);
        }
        // GPU에서 띠를 옆으로 펼치므로 렌더러의 컬링 영역에도 그 폭을 포함한다.
        localBounds.Expand(height * .8f);
        renderer.localBounds = localBounds;
        properties.SetVectorArray(Path, path);
        properties.SetFloat(Height, height);
        properties.SetFloat(Strength, strength);
        properties.SetFloat(Phase, phase);
        properties.SetFloat(Motion, motion);
        renderer.SetPropertyBlock(properties);
        renderer.enabled = strength > .001f;
    }

    public void Hide()
    {
        renderer.enabled = false;
        motion = 0f;
        historyCount = 0;
        previousPosition = owner.position;
        Remember(owner.position);
    }
}
