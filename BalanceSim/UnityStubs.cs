// 전투 시뮬레이션을 Unity 밖에서 돌리기 위한 최소 UnityEngine 대체 구현.
// 동작은 Unity 의 공개 동작(근사 == 비교, MoveTowards, RoundToInt 반올림 방식 등)에 맞춘다.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : Attribute { public MinAttribute(float m) { } }

    public class Object { }
    public class GameObject : Object { }
    public class Material : Object { }
    public class RuntimeAnimatorController : Object { }

    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color(Color32 c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }

    public static class Mathf
    {
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Abs(float f) => Math.Abs(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Epsilon = float.Epsilon;
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f);
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 down => new(0, -1, 0);
        public static Vector3 right => new(1, 0, 0);
        public static Vector3 left => new(-1, 0, 0);
        public static Vector3 forward => new(0, 0, 1);
        public static Vector3 back => new(0, 0, -1);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public Vector3 normalized { get { float m = magnitude; return m > 1E-05f ? this / m : zero; } }
        public void Normalize() => this = normalized;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 a, Vector3 b) { float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z; return dx * dx + dy * dy + dz * dz < 9.99999944E-11f; }
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public bool Equals(Vector3 o) => x.Equals(o.x) && y.Equals(o.y) && z.Equals(o.z);
        public override bool Equals(object o) => o is Vector3 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 ClampMagnitude(Vector3 v, float max) { float s = v.sqrMagnitude; if (s > max * max) { float m = (float)Math.Sqrt(s); return v / m * max; } return v; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { float s = Dot(n, n); if (s < Mathf.Epsilon) return v; return v - n * (Dot(v, n) / s); }
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDelta)
        {
            float tx = target.x - current.x, ty = target.y - current.y, tz = target.z - current.z;
            float sq = tx * tx + ty * ty + tz * tz;
            if (sq == 0 || (maxDelta >= 0 && sq <= maxDelta * maxDelta)) return target;
            float d = (float)Math.Sqrt(sq);
            return new(current.x + tx / d * maxDelta, current.y + ty / d * maxDelta, current.z + tz / d * maxDelta);
        }
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => new(0, 0);
        public static Vector2Int one => new(1, 1);
        public static Vector2Int up => new(0, 1);
        public static Vector2Int down => new(0, -1);
        public static Vector2Int left => new(-1, 0);
        public static Vector2Int right => new(1, 0);
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.x - b.x, a.y - b.y);
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
        public bool Equals(Vector2Int o) => this == o;
        public override bool Equals(object o) => o is Vector2Int v && v == this;
        public override int GetHashCode() => x ^ (y << 2);
        public override string ToString() => $"({x}, {y})";
    }

    public struct RectInt
    {
        public int x, y, width, height;
        public RectInt(int x, int y, int width, int height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public int xMin => Math.Min(x, x + width);
        public int xMax => Math.Max(x, x + width);
        public int yMin => Math.Min(y, y + height);
        public int yMax => Math.Max(y, y + height);
        public Vector2 center => new(x + width / 2f, y + height / 2f);
        public Vector2Int size => new(width, height);
        public bool Contains(Vector2Int p) => p.x >= xMin && p.y >= yMin && p.x < xMax && p.y < yMax;
        public IEnumerable<Vector2Int> allPositionsWithin
        {
            get { for (int j = yMin; j < yMax; j++) for (int i = xMin; i < xMax; i++) yield return new Vector2Int(i, j); }
        }
    }

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 origin, Vector3 direction) { this.origin = origin; this.direction = direction.normalized; }
    }

    public struct Bounds
    {
        public Vector3 center, extents;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; extents = size * 0.5f; }
        public Vector3 min => center - extents;
        public Vector3 max => center + extents;
        public void Expand(float amount) { float h = amount * 0.5f; extents = new Vector3(extents.x + h, extents.y + h, extents.z + h); }
        public bool IntersectRay(Ray ray, out float distance)
        {
            float tmin = float.NegativeInfinity, tmax = float.PositiveInfinity;
            float[] o = { ray.origin.x, ray.origin.y, ray.origin.z };
            float[] d = { ray.direction.x, ray.direction.y, ray.direction.z };
            float[] lo = { min.x, min.y, min.z };
            float[] hi = { max.x, max.y, max.z };
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(d[i]) < 1e-12f)
                {
                    if (o[i] < lo[i] || o[i] > hi[i]) { distance = 0; return false; }
                    continue;
                }
                float t1 = (lo[i] - o[i]) / d[i], t2 = (hi[i] - o[i]) / d[i];
                if (t1 > t2) (t1, t2) = (t2, t1);
                tmin = Math.Max(tmin, t1);
                tmax = Math.Min(tmax, t2);
            }
            if (tmax < 0 || tmin > tmax) { distance = 0; return false; }
            distance = Math.Max(tmin, 0f);
            return true;
        }
    }

    public static class Debug
    {
        public static void Log(object m) => Console.WriteLine(m);
        public static void LogWarning(object m) => Console.WriteLine("WARN " + m);
        public static void LogError(object m) => Console.WriteLine("ERR " + m);
    }
}
