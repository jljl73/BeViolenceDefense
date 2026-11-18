using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보드 주변 배경(지면·나무·떨어지는 잎)을 실행 시 생성한다. 게임 규칙에는 관여하지 않는 장식이다.
/// 보드 칸 좌표계(VoxelDefensePrototype 기준)와 같은 원점에 둔다.
/// </summary>
public sealed class BoardEnvironment : MonoBehaviour
{
    [SerializeField] private Shader litShader;
    [SerializeField] private Shader particleShader;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private int seed = 11;

    [Header("지면")]
    [SerializeField] private float groundHeight = -0.25f;
    [SerializeField] private float groundSize = 96f;
    [SerializeField] private Color groundLight = new Color32(160, 92, 60, 255);
    [SerializeField] private Color groundDark = new Color32(122, 64, 42, 255);

    [Header("나무")]
    [Tooltip("나무를 흩뿌리는 범위 (보드 중심에서 한 변의 절반)")]
    [SerializeField] private float scatterRange = 26f;
    [SerializeField] private float treeSpacing = 1.45f;
    [Tooltip("보드 가장자리에서 띄우는 거리. 카메라 쪽은 나무가 보드를 가리지 않도록 더 띄운다.")]
    [SerializeField] private float farClearance = 1.4f;
    [SerializeField] private float nearClearance = 4.6f;
    [Range(0f, 1f)] [SerializeField] private float bushChance = 0.25f;
    [SerializeField] private Color trunkColor = new Color32(92, 58, 42, 255);
    [SerializeField] private Color[] leafColors =
    {
        new Color32(242, 128, 42, 255),
        new Color32(228, 96, 38, 255),
        new Color32(246, 160, 56, 255),
        new Color32(206, 70, 36, 255)
    };

    [Header("먼 곳 안개")]
    [Tooltip("직교 카메라에서는 Unity 안개가 깨지므로, 화면 위쪽(카메라에서 먼 쪽) 배경에 이 색을 직접 섞는다.")]
    [SerializeField] private Color hazeColor = new Color32(226, 169, 133, 255);
    [Tooltip("보드 중심에서 카메라 반대 방향으로 잰 거리")]
    [SerializeField] private float hazeStart = 4f;
    [SerializeField] private float hazeEnd = 14f;
    [Range(0f, 1f)] [SerializeField] private float hazeStrength = 0.85f;

    [Header("떨어지는 잎")]
    [SerializeField] private float leafRate = 14f;

    private readonly List<Object> created = new();
    private Vector3 center;
    private Vector3 toFar;

    private void Awake()
    {
        var random = new System.Random(seed);
        center = new((DefenseGrid.Width - 1) * 0.5f, 0f, (DefenseGrid.Depth - 1) * 0.5f);
        // 카메라 반대쪽(화면 위)과 카메라 쪽(화면 아래)을 구분한다.
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        toFar = cam != null
            ? Vector3.ProjectOnPlane(transform.InverseTransformDirection(cam.transform.forward), Vector3.up).normalized
            : Vector3.forward;
        CreateGround(random);
        CreateTrees(random);
        CreateFallingLeaves();
    }

    private float Haze(Vector3 offset) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hazeStart, hazeEnd, Vector3.Dot(offset, toFar))) * hazeStrength;

    private void CreateGround(System.Random random)
    {
        const int size = 256;
        var texture = Track(new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp });
        float ox = (float)random.NextDouble() * 100f;
        float oz = (float)random.NextDouble() * 100f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float patch = Mathf.PerlinNoise(ox + u * 9f, oz + v * 9f) * 0.7f +
                    Mathf.PerlinNoise(ox + u * 31f, oz + v * 31f) * 0.3f;
                Color soil = Color.Lerp(groundLight, groundDark, Mathf.SmoothStep(0.35f, 0.75f, patch));
                var offset = new Vector3((u - 0.5f) * groundSize, 0f, (v - 0.5f) * groundSize);
                pixels[y * size + x] = Color.Lerp(soil, hazeColor, Haze(offset));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        Material material = Lit(Color.white, 0.05f);
        material.SetTexture("_BaseMap", texture);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ground.name = "Ground";
        Destroy(ground.GetComponent<Collider>());
        ground.transform.SetParent(transform, false);
        ground.transform.localPosition = new Vector3(center.x, groundHeight, center.z);
        ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ground.transform.localScale = new Vector3(groundSize, groundSize, 1f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private void CreateTrees(System.Random random)
    {
        Material trunk = Lit(trunkColor, 0.1f);
        var leaves = new Material[leafColors.Length];
        for (int i = 0; i < leaves.Length; i++)
            leaves[i] = Lit(leafColors[i], 0.1f);

        var blobs = new Mesh[3];
        for (int i = 0; i < blobs.Length; i++)
            blobs[i] = Track(LumpySphere($"Leaf Blob {i}", random));
        Mesh trunkMesh = Track(TrunkMesh());

        var trees = new GameObject("Trees").transform;
        trees.SetParent(transform, false);
        var placed = new List<Vector2>();
        float halfBoard = DefenseGrid.Width * 0.5f + 0.5f;
        int attempts = Mathf.CeilToInt(scatterRange * scatterRange * 4f / (treeSpacing * treeSpacing)) * 3;
        for (int i = 0; i < attempts; i++)
        {
            var offset = new Vector3(Range(random, -scatterRange, scatterRange), 0f, Range(random, -scatterRange, scatterRange));
            float outside = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.z)) - halfBoard;
            bool nearSide = Vector3.Dot(offset, toFar) < 0f;
            if (outside < (nearSide ? nearClearance : farClearance))
                continue;

            var spot = new Vector2(offset.x, offset.z);
            bool crowded = false;
            foreach (Vector2 other in placed)
            {
                if ((other - spot).sqrMagnitude < treeSpacing * treeSpacing)
                {
                    crowded = true;
                    break;
                }
            }
            if (crowded)
                continue;

            placed.Add(spot);
            Vector3 position = center + offset + Vector3.up * groundHeight;
            bool bush = random.NextDouble() < bushChance;
            // 카메라 쪽 가까운 자리는 덤불만 두어 보드를 덜 가린다.
            if (nearSide && outside < nearClearance + 2f)
                bush = true;
            Transform tree = CreateTree(trees, position, bush, random, trunkMesh, trunk, blobs, leaves);
            ApplyHaze(tree, Haze(offset));
        }
    }

    private void ApplyHaze(Transform tree, float haze)
    {
        if (haze <= 0.001f)
            return;

        var block = new MaterialPropertyBlock();
        foreach (MeshRenderer renderer in tree.GetComponentsInChildren<MeshRenderer>())
        {
            block.SetColor("_BaseColor", Color.Lerp(renderer.sharedMaterial.color, hazeColor, haze));
            renderer.SetPropertyBlock(block);
        }
    }

    private static Transform CreateTree(Transform parent, Vector3 position, bool bush, System.Random random,
        Mesh trunkMesh, Material trunk, Mesh[] blobs, Material[] leaves)
    {
        var tree = new GameObject(bush ? "Bush" : "Tree").transform;
        tree.SetParent(parent, false);
        tree.localPosition = position;
        tree.localRotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f);
        float scale = bush ? Range(random, 0.5f, 0.8f) : Range(random, 1.0f, 1.5f);
        tree.localScale = Vector3.one * scale;
        Material leaf = leaves[random.Next(leaves.Length)];

        float canopyHeight = 0f;
        if (!bush)
        {
            float trunkHeight = Range(random, 0.8f, 1.15f);
            Part("Trunk", tree, trunkMesh, trunk, Vector3.zero, new Vector3(0.3f, trunkHeight, 0.3f));
            canopyHeight = trunkHeight + 0.45f;
        }

        // 큰 덩어리 하나와 작은 덩어리 몇 개로 잎 뭉치를 만든다.
        float main = Range(random, 1.3f, 1.6f);
        Part("Leaves", tree, blobs[random.Next(blobs.Length)], leaf, Vector3.up * (canopyHeight + main * 0.35f),
            new Vector3(main, main * 0.85f, main));
        int extra = random.Next(3, 6);
        for (int i = 0; i < extra; i++)
        {
            float angle = Range(random, 0f, Mathf.PI * 2f);
            float size = main * Range(random, 0.6f, 0.8f);
            var offset = new Vector3(Mathf.Cos(angle) * main * 0.42f, canopyHeight + Range(random, -0.1f, 0.45f),
                Mathf.Sin(angle) * main * 0.42f);
            Part("Leaves", tree, blobs[random.Next(blobs.Length)], leaf, offset, new Vector3(size, size * 0.85f, size));
        }
        return tree;
    }

    private static void Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private void CreateFallingLeaves()
    {
        var go = new GameObject("Falling Leaves", typeof(ParticleSystem));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = center + Vector3.up * 7f;
        var particles = go.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 전투 배속·일시정지와 무관한 배경 효과라 실시간으로 움직인다.
        var main = particles.main;
        main.useUnscaledTime = true;
        main.loop = true;
        main.prewarm = true;
        main.duration = 10f;
        main.startLifetime = 11f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var colors = new Gradient();
        colors.SetKeys(
            new[] { new GradientColorKey(leafColors[0], 0f), new GradientColorKey(leafColors[leafColors.Length - 1], 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };

        var emission = particles.emission;
        emission.rateOverTime = leafRate;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(scatterRange * 1.6f, 1f, scatterRange * 1.6f);

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.75f, -0.45f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.3f;

        var rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        // 땅에 닿기 전에 사라지도록 마지막에 줄어든다.
        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));

        var material = Track(new Material(particleShader));
        material.color = Color.white;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particles.Play();
    }

    // 정이십면체를 한 번 나누고 꼭짓점을 흔들어 각진 잎 덩어리를 만든다. 면마다 꼭짓점을 따로 둬 평면 음영을 낸다.
    private static Mesh LumpySphere(string name, System.Random random)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var points = new List<Vector3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
            new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
            new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1)
        };
        int[] faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        var midpoints = new Dictionary<long, int>();
        var subdivided = new List<int>();
        for (int i = 0; i < faces.Length; i += 3)
        {
            int a = faces[i], b = faces[i + 1], c = faces[i + 2];
            int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
            subdivided.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }

        for (int i = 0; i < points.Count; i++)
            points[i] = points[i].normalized * (0.5f * Range(random, 0.86f, 1.08f));

        var vertices = new Vector3[subdivided.Count];
        var triangles = new int[subdivided.Count];
        for (int i = 0; i < subdivided.Count; i += 3)
        {
            Vector3 a = points[subdivided[i]], b = points[subdivided[i + 1]], c = points[subdivided[i + 2]];
            // 바깥을 향하도록 감는 방향을 맞춘다.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                (b, c) = (c, b);
            vertices[i] = a;
            vertices[i + 1] = b;
            vertices[i + 2] = c;
            triangles[i] = i;
            triangles[i + 1] = i + 1;
            triangles[i + 2] = i + 2;
        }

        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;

        int Midpoint(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (!midpoints.TryGetValue(key, out int index))
            {
                index = points.Count;
                points.Add((points[a].normalized + points[b].normalized) * 0.5f);
                midpoints.Add(key, index);
            }
            return index;
        }
    }

    // 밑동이 굵은 육각 기둥. 피벗은 밑면 중심이고 높이 1, 지름 1이다.
    private static Mesh TrunkMesh()
    {
        const int sides = 6;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = new(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            int start = vertices.Count;
            vertices.Add(d0 * 0.5f);
            vertices.Add(d0 * 0.32f + Vector3.up);
            vertices.Add(d1 * 0.32f + Vector3.up);
            vertices.Add(d1 * 0.5f);
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        var mesh = new Mesh { name = "Tree Trunk" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material Lit(Color color, float smoothness)
    {
        var material = Track(new Material(litShader) { color = color });
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    private T Track<T>(T asset) where T : Object
    {
        created.Add(asset);
        return asset;
    }

    private static float Range(System.Random random, float min, float max) =>
        min + (float)random.NextDouble() * (max - min);

    private void OnDestroy()
    {
        foreach (Object asset in created)
            Destroy(asset);
    }
}
