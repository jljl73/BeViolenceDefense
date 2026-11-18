using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class SpellEffects
{
    private const float ImpactDuration = 0.28f;
    private const float UnitEffectScale = 1.5f;
    private static readonly Vector2[] AreaRing = CreateCircle();
    private static readonly Vector2[] ShieldLeft =
    {
        new(0f, 0.2f), new(-0.2f, 0.12f), new(-0.16f, -0.04f), new(-0.1f, -0.13f), new(0f, -0.2f)
    };
    private static readonly Vector2[] ShieldRight =
    {
        new(0f, -0.2f), new(0.1f, -0.13f), new(0.16f, -0.04f), new(0.2f, 0.12f), new(0f, 0.2f)
    };
    private static readonly Vector2[] Slash =
    {
        new(-0.31f, -0.12f), new(-0.14f, -0.11f), new(0.07f, -0.04f),
        new(0.23f, 0.07f), new(0.33f, 0.21f)
    };

    private readonly Transform board;
    private readonly Transform camera;
    private readonly Material material;
    private readonly int pulseSortingOrder;
    private readonly List<Pulse> playing = new();
    private readonly Stack<Pulse> available = new();

    public SpellEffects(Transform board, Camera camera, Material material, int pulseSortingOrder)
    {
        this.board = board;
        this.camera = camera.transform;
        this.material = material;
        this.pulseSortingOrder = pulseSortingOrder;
    }

    public void PlayArea(DefenseBattle.Spell spell, Vector3 center, float radius)
    {
        Vector3 point = board.TransformPoint(center + Vector3.up * 0.055f);
        Play(AreaRing, point, Vector3.zero, SpellColor(spell), 0.42f, 0.20f,
            rotation: board.rotation * Quaternion.Euler(90f, 0f, 0f), scale: radius);
    }

    public void PlayCoreShockwave(Vector3 center, Color color)
    {
        Play(AreaRing, board.TransformPoint(center), Vector3.zero, color, 0.32f, 0.12f,
            rotation: board.rotation * Quaternion.Euler(90f, 0f, 0f), scale: 0.50f, scaleGrowth: 0.86f);
    }

    public static Color SpellColor(DefenseBattle.Spell spell) => spell switch
    {
        DefenseBattle.Spell.AttackBuff => UIPalette.Attack,
        DefenseBattle.Spell.MoveSpeedBuff => UIPalette.MoveSpeed,
        DefenseBattle.Spell.ArmorBuff => UIPalette.Armor,
        _ => UIPalette.Heal
    };

    public void PlayAttack(Vector3 attacker, Vector3 target)
    {
        Vector3 direction = board.TransformDirection((target - attacker).normalized);
        Vector3 point = board.TransformPoint(attacker + Vector3.up * 0.52f) - camera.forward * 0.38f;
        Vector3 projected = camera.InverseTransformDirection(direction);
        float angle = Mathf.Atan2(projected.y, projected.x) * Mathf.Rad2Deg - 22f;
        Play(Slash, point + direction * 0.24f, direction * 0.31f, UIPalette.Attack,
            ImpactDuration, 0.105f, rotation: camera.rotation * Quaternion.Euler(0f, 0f, angle));
    }

    public void PlayArmorImpact(Vector3 defender)
    {
        Vector3 point = board.TransformPoint(defender + Vector3.up * 0.52f) - camera.forward * 0.36f;
        Play(ShieldLeft, point, Vector3.zero, UIPalette.Armor, ImpactDuration, 0.080f);
        Play(ShieldRight, point, Vector3.zero, UIPalette.Armor, ImpactDuration, 0.080f);
    }

    public void SyncMoveTrail(Transform unit, Vector3 step, bool moving, ref LineRenderer[] trails)
    {
        if (!moving)
        {
            if (trails != null)
            {
                foreach (LineRenderer trail in trails)
                    trail.enabled = false;
            }
            return;
        }

        if (trails == null)
        {
            trails = new[] { CreateStroke("Move Speed Trail", unit), CreateStroke("Move Speed Trail", unit) };
            Color head = UIPalette.MoveSpeed;
            head.a = 0.45f;
            Color tail = head;
            tail.a = 0f;
            foreach (LineRenderer trail in trails)
            {
                trail.useWorldSpace = true;
                trail.positionCount = 2;
                trail.startWidth = 0.055f;
                trail.endWidth = 0.012f;
                trail.startColor = head;
                trail.endColor = tail;
            }
        }

        Vector3 direction = board.TransformDirection(step.normalized);
        Vector3 side = Vector3.Cross(board.up, direction) * 0.15f;
        Vector3 back = unit.position + board.up * 0.08f - direction * 0.26f;
        for (int i = 0; i < trails.Length; i++)
        {
            LineRenderer trail = trails[i];
            Vector3 start = back + (i == 0 ? side : -side);
            trail.SetPosition(0, start);
            trail.SetPosition(1, start - direction * 0.30f);
            trail.enabled = true;
        }
    }

    public void Advance(float deltaTime)
    {
        for (int i = playing.Count - 1; i >= 0; i--)
        {
            Pulse pulse = playing[i];
            pulse.Age += deltaTime;
            if (pulse.Age >= pulse.Duration)
            {
                pulse.Line.enabled = false;
                available.Push(pulse);
                playing[i] = playing[^1];
                playing.RemoveAt(playing.Count - 1);
            }
            else
            {
                Draw(pulse);
            }
        }
    }

    private void Play(Vector2[] shape, Vector3 point, Vector3 motion, Color color, float duration,
        float width, Quaternion? rotation = null, float scale = UnitEffectScale, float scaleGrowth = 0f)
    {
        Pulse pulse = available.Count > 0 ? available.Pop() : new Pulse(CreateStroke("Spell Pulse", board));
        pulse.Line.sortingOrder = pulseSortingOrder;
        pulse.Point = point;
        pulse.Motion = motion;
        pulse.Color = color;
        pulse.Duration = duration;
        pulse.Width = width;
        pulse.Scale = scale;
        pulse.ScaleGrowth = scaleGrowth;
        pulse.Age = 0f;
        pulse.Line.transform.rotation = rotation ?? camera.rotation;
        pulse.Line.transform.localScale = Vector3.one * scale;
        pulse.Line.positionCount = shape.Length;
        for (int i = 0; i < shape.Length; i++)
            pulse.Line.SetPosition(i, shape[i]);
        Draw(pulse);
        pulse.Line.enabled = true;
        playing.Add(pulse);
    }

    private static void Draw(Pulse pulse)
    {
        float t = pulse.Age / pulse.Duration;
        float travel = 1f - (1f - t) * (1f - t);
        pulse.Line.transform.position = pulse.Point + pulse.Motion * travel;
        if (pulse.ScaleGrowth > 0f)
            pulse.Line.transform.localScale = Vector3.one * (pulse.Scale + pulse.ScaleGrowth * travel);
        pulse.Line.widthMultiplier = pulse.Width * Mathf.Lerp(1f, 0.45f, t);
        Color color = Color.Lerp(pulse.Color, UIPalette.TextPrimary, 0.30f * (1f - t));
        color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, t));
        pulse.Line.startColor = pulse.Line.endColor = color;
    }

    private LineRenderer CreateStroke(string name, Transform parent)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        line.useWorldSpace = false;
        line.sharedMaterial = material;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private static Vector2[] CreateCircle()
    {
        const int segments = 64;
        var points = new Vector2[segments + 1];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (2f * Mathf.PI / segments);
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        points[^1] = points[0];
        return points;
    }

    private sealed class Pulse
    {
        public readonly LineRenderer Line;
        public Vector3 Point;
        public Vector3 Motion;
        public Color Color;
        public float Duration;
        public float Width;
        public float Scale;
        public float ScaleGrowth;
        public float Age;

        public Pulse(LineRenderer line) => Line = line;
    }
}
