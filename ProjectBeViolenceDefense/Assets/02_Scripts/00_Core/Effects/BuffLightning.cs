using UnityEngine;
using UnityEngine.Rendering;

// 시전 결과를 받은 유닛의 화면 모델을 따라가는 짧은 낙뢰. 전투 상태는 변경하지 않는다.
internal sealed class BuffLightning
{
    private const float FallDuration = 0.065f;
    private const float Duration = 0.38f;
    private const int PointCount = 8;
    private readonly Transform board;
    private readonly Transform camera;
    private readonly LineRenderer glow;
    private readonly LineRenderer core;
    private readonly LineRenderer impact;
    private readonly Vector3[] path = new Vector3[PointCount];
    private Transform target;
    private Color color;
    private float age;
    private float size;
    public GameObject Root { get; }
    public bool IsPlaying => Root.activeSelf;

    public BuffLightning(Transform board, Transform camera, Material material, int sortingOrder)
    {
        this.board = board;
        this.camera = camera;
        Root = new GameObject("Buff Lightning");
        Root.transform.SetParent(board, false);
        glow = Stroke("Colored Glow", material, sortingOrder, PointCount);
        core = Stroke("White Core", material, sortingOrder + 1, PointCount);
        impact = Stroke("Contact Flash", material, sortingOrder + 2, 9);
        Root.SetActive(false);
    }

    public void Play(Transform target, Color color, float scale, int seed)
    {
        this.target = target;
        this.color = color;
        size = Mathf.Max(0.45f, scale);
        age = 0f;
        // 화면 위까지 길게 이어지는 낙뢰. 작은 유닛도 높은 하늘에서 내려오게 한다.
        float height = Mathf.Max(18f, 32f * scale);
        for (int i = 0; i < PointCount; i++)
        {
            float t = i / (float)(PointCount - 1);
            // 양 끝은 유닛 위에 고정하고, 유닛별로 다른 꺾임을 만든다.
            float bend = i == 0 || i == PointCount - 1 ? 0f :
                Mathf.Sin(seed * 1.71f + i * 2.43f) * 0.23f * size;
            path[i] = Vector3.up * (0.45f * scale + height * (1f - t)) + Vector3.right * bend;
        }
        Root.SetActive(true);
        Draw();
    }

    public void Advance(float deltaTime)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Stop();
            return;
        }
        age += deltaTime;
        if (age >= Duration)
            Stop();
        else
            Draw();
    }

    private void Stop()
    {
        target = null;
        Root.SetActive(false);
    }

    private void Draw()
    {
        // 부모나 유닛의 회전에 끌려가지 않고, 항상 보드의 위에서 아래로 내린다.
        Root.transform.position = target.position - camera.forward * (0.08f * size);
        Root.transform.rotation = Quaternion.identity;
        float tip = Mathf.Clamp01((age + 0.004f) / FallDuration) * (PointCount - 1);
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FallDuration, Duration, age));
        float echo = 1f + 0.35f * Mathf.Exp(-Mathf.Pow((age - 0.16f) / 0.025f, 2f));
        for (int i = 0; i < PointCount; i++)
        {
            float point = Mathf.Min(i, tip);
            int index = Mathf.Min((int)point, PointCount - 2);
            Vector3 local = Vector3.Lerp(path[index], path[index + 1], point - index);
            Vector3 offset = board.up * local.y + camera.right * local.x;
            glow.SetPosition(i, offset);
            core.SetPosition(i, offset);
        }
        glow.widthMultiplier = 0.20f * size * Mathf.Lerp(0.6f, 1f, fade);
        core.widthMultiplier = 0.048f * size;
        SetColor(glow, color, fade * 1.2f * echo);
        SetColor(core, Color.Lerp(Color.white, color, 0.15f), fade * echo);

        float contactAge = age - FallDuration;
        float contact = contactAge < 0f ? 0f : Mathf.Pow(Mathf.Clamp01(1f - contactAge / 0.20f), 2f);
        impact.enabled = contact > 0f;
        impact.widthMultiplier = 0.075f * size * contact;
        SetColor(impact, Color.Lerp(Color.white, color, 0.35f), contact * 1.4f);
        Vector3 hit = board.up * path[^1].y;
        float reach = size * Mathf.Lerp(0.38f, 0.18f, Mathf.Clamp01(contactAge / 0.20f));
        Vector3 horizontal = camera.right * reach;
        Vector3 vertical = camera.up * reach * 1.35f;
        impact.SetPosition(0, hit - horizontal);
        impact.SetPosition(1, hit);
        impact.SetPosition(2, hit + vertical);
        impact.SetPosition(3, hit);
        impact.SetPosition(4, hit + horizontal);
        impact.SetPosition(5, hit);
        impact.SetPosition(6, hit - vertical);
        impact.SetPosition(7, hit);
        impact.SetPosition(8, hit - horizontal);
    }

    private static void SetColor(LineRenderer line, Color tint, float alpha)
    {
        tint.a = alpha;
        line.startColor = line.endColor = tint;
    }

    private LineRenderer Stroke(string name, Material material, int order, int points)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(Root.transform, false);
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.positionCount = points;
        line.sortingOrder = order;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }
}
