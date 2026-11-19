using UnityEngine;
using UnityEngine.Rendering;

internal sealed class UnitBuffAura
{
    private static readonly int State = Shader.PropertyToID("_Buffs");
    private static readonly int Phase = Shader.PropertyToID("_Phase");
    private static readonly int Heal = Shader.PropertyToID("_Heal");
    private static readonly int Impact = Shader.PropertyToID("_Impact");
    private readonly MeshRenderer renderer;
    private readonly Transform camera;
    private readonly MaterialPropertyBlock properties = new();
    private readonly Material shieldMaterial;
    private readonly Material attackMaterial;
    private readonly Material speedMaterial;
    private readonly Mesh speedMesh;
    private UnitSpeedAura speed;
    private UnitAttackAura attack;
    private readonly Mesh shieldMesh;
    private UnitShieldSurface shield;
    private Vector4 visible;
    private float phase;

    public UnitBuffAura(Transform owner, Camera camera, Mesh mesh, Material material, Mesh shieldMesh, Material shieldMaterial, Material attackMaterial, Mesh speedMesh, Material speedMaterial)
    {
        this.camera = camera.transform;
        this.shieldMaterial = shieldMaterial;
        this.attackMaterial = attackMaterial;
        this.speedMaterial = speedMaterial;
        this.speedMesh = speedMesh;
        this.shieldMesh = shieldMesh;
        var root = new GameObject("Persistent Buff Aura", typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(owner, false);
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 43;
    }

    public void Sync(EnemyUnit unit, Renderer[] body, float deltaTime, float impact, Vector3 hitDirection)
    {
        phase += deltaTime;
        Vector4 wanted = new(Strength(unit.AttackBuffRemaining), Strength(unit.MoveSpeedBuffRemaining),
            Strength(unit.ArmorBuffRemaining), Strength(unit.TauntRemaining));
        for (int i = 0; i < 4; i++)
            visible[i] = wanted[i] > visible[i] ? wanted[i] : Mathf.MoveTowards(visible[i], wanted[i], deltaTime * 6f);
        renderer.enabled = visible.w > .001f || unit.HealFlash > 0f;
        if (visible.z <= .001f) shield?.Hide();
        if (visible.x <= .001f) attack?.Hide();
        if (visible.y <= .001f) speed?.Hide();
        if (!renderer.enabled && visible.z <= .001f && visible.x <= .001f && visible.y <= .001f) return;
        Bounds bounds = new(renderer.transform.parent.position + Vector3.up * .2f, Vector3.one * .4f);
        bool found = false;
        foreach (Renderer part in body)
        {
            if (part == null || !part.enabled) continue;
            if (!found) { bounds = part.bounds; found = true; }
            else bounds.Encapsulate(part.bounds);
        }
        if (visible.z > .001f)
        {
            shield ??= new UnitShieldSurface(renderer.transform.parent, shieldMesh, shieldMaterial);
            Vector3 incoming = renderer.transform.parent.parent.TransformDirection(-hitDirection);
            shield.Sync(bounds, visible.z, phase + unit.SpawnOrder * .37f,
                Mathf.Clamp01(impact / .16f), incoming);
        }
        if (visible.x > .001f)
        {
            attack ??= new UnitAttackAura(renderer.transform.parent, shieldMesh, attackMaterial);
            attack.Sync(bounds, visible.x, phase + unit.SpawnOrder * .37f,
                Mathf.Clamp01(unit.AttackFlash / .14f));
        }
        if (visible.y > .001f)
        {
            speed ??= new UnitSpeedAura(renderer.transform.parent, speedMesh, speedMaterial);
            speed.Sync(bounds, visible.y, phase + unit.SpawnOrder * .37f, deltaTime);
        }
        if (!renderer.enabled) return;
        // Billboard 크기도 카메라에 투영된 몸 크기를 사용한다. 내려다보는 각도에서
        // 월드 높이를 그대로 쓰면 입자 구름이 몸보다 지나치게 커진다.
        Vector3 up = camera.up;
        Vector3 right = camera.right;
        float height = Mathf.Max(.22f, Vector3.Dot(bounds.size, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z))));
        float width = Vector3.Dot(bounds.size, new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z)));
        width = Mathf.Clamp(width, height * .45f, height * .95f);
        Transform transform = renderer.transform;
        transform.SetPositionAndRotation(bounds.center - camera.forward * (width * .12f), camera.rotation);
        Vector3 parentScale = transform.parent.lossyScale;
        transform.localScale = new Vector3(width * 1.55f / parentScale.x, height * 1.45f / parentScale.y, 1f / parentScale.z);
        properties.SetVector(State, visible);
        properties.SetFloat(Phase, phase + unit.SpawnOrder * .37f);
        properties.SetFloat(Heal, Mathf.Clamp01(unit.HealFlash / .55f));
        properties.SetFloat(Impact, unit.ArmorBuffStacks > 0 ? Mathf.Clamp01(impact / .16f) : 0f);
        renderer.SetPropertyBlock(properties);
    }
    private float Strength(float remaining) => remaining <= 0f ? 0f :
        remaining < 1f ? .6f + .4f * (.5f + .5f * Mathf.Sin(phase * 8f)) : 1f;
    public void Hide()
    {
        renderer.enabled = false;
        shield?.Hide();
        attack?.Hide();
        speed?.Hide();
    }
}
