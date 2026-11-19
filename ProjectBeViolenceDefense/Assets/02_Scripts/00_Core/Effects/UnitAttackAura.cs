using UnityEngine;
using UnityEngine.Rendering;

// 몸통 가까이 머무는 연결된 열기. 바깥 방어막과 메시를 공유하되 더 좁고 낮게 그린다.
internal sealed class UnitAttackAura
{
    private static readonly int Strength = Shader.PropertyToID("_Strength");
    private static readonly int Phase = Shader.PropertyToID("_Phase");
    private static readonly int Pulse = Shader.PropertyToID("_AttackPulse");
    private readonly MeshRenderer renderer;
    private readonly MaterialPropertyBlock properties = new();

    public UnitAttackAura(Transform owner, Mesh mesh, Material material)
    {
        var root = new GameObject("Attack Heat Aura", typeof(MeshFilter), typeof(MeshRenderer)) { layer = owner.gameObject.layer };
        root.transform.SetParent(owner, false);
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        // 안쪽 산호색이 방어막에 희석되지 않게 마지막에 얇게 겹친다. 바깥 막까지 덮지는 않는다.
        renderer.sortingOrder = 45;
        renderer.enabled = false;
    }

    public void Sync(Bounds bounds, float strength, float phase, float attackPulse)
    {
        float height = Mathf.Max(.1f, bounds.size.y);
        float radius = Mathf.Max(height * .38f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.18f) * .76f;
        Transform transform = renderer.transform;
        transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
        Vector3 parentScale = transform.parent.lossyScale;
        transform.localScale = new Vector3(radius / parentScale.x, height / parentScale.y, radius / parentScale.z);
        properties.SetFloat(Strength, strength);
        properties.SetFloat(Phase, phase);
        properties.SetFloat(Pulse, attackPulse);
        renderer.SetPropertyBlock(properties);
        renderer.enabled = strength > .001f;
    }

    public void Hide() => renderer.enabled = false;
}
