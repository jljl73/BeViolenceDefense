using System.Collections.Generic;
using UnityEngine;

// Presentation-only distance lookup. Rounded corners keep arrows turning without easing their speed.
internal sealed class RouteFlowPath
{
    private const float CornerRadius = 0.28f;
    private const int CornerSamples = 16;
    private readonly List<Vector3> positions = new();
    private readonly List<Vector3> directions = new();
    private readonly List<float> widths = new();
    private readonly List<float> distances = new();
    public float Length => distances.Count == 0 ? 0f : distances[^1];

    public RouteFlowPath(IReadOnlyList<Vector3> points, IReadOnlyList<float> widthScales)
    {
        var clean = new List<Vector3>();
        var scales = new List<float>();
        for (int i = 0; i < points.Count; i++)
        {
            if (clean.Count > 0 && (points[i] - clean[^1]).sqrMagnitude < 0.000001f)
                continue;
            clean.Add(points[i]);
            scales.Add(widthScales[i]);
        }
        if (clean.Count < 2)
            return;

        Add(clean[0], (clean[1] - clean[0]).normalized, scales[0]);
        for (int i = 1; i < clean.Count - 1; i++)
        {
            Vector3 incoming = clean[i] - clean[i - 1];
            Vector3 outgoing = clean[i + 1] - clean[i];
            float radius = Mathf.Min(CornerRadius, Mathf.Min(incoming.magnitude, outgoing.magnitude) * 0.45f);
            Vector3 from = clean[i] - incoming.normalized * radius;
            Vector3 to = clean[i] + outgoing.normalized * radius;
            float fromWidth = Mathf.Lerp(scales[i], scales[i - 1], radius / incoming.magnitude);
            float toWidth = Mathf.Lerp(scales[i], scales[i + 1], radius / outgoing.magnitude);
            Add(from, incoming.normalized, fromWidth);
            for (int sample = 1; sample <= CornerSamples; sample++)
            {
                float t = sample / (float)CornerSamples;
                float u = 1f - t;
                Vector3 position = u * u * from + 2f * u * t * clean[i] + t * t * to;
                Vector3 tangent = (u * (clean[i] - from) + t * (to - clean[i])).normalized;
                float width = u * u * fromWidth + 2f * u * t * scales[i] + t * t * toWidth;
                Add(position, tangent, width);
            }
        }
        Add(clean[^1], (clean[^1] - clean[^2]).normalized, scales[^1]);
    }

    private void Add(Vector3 position, Vector3 direction, float width)
    {
        float length = positions.Count == 0 ? 0f : Vector3.Distance(positions[^1], position);
        if (positions.Count > 0 && length < 0.00001f)
            return;
        float distance = Length + length;
        positions.Add(position);
        directions.Add(direction);
        widths.Add(width);
        distances.Add(distance);
    }

    public void Sample(float distance, out Vector3 position, out Vector3 direction, out float width)
    {
        if (positions.Count == 0)
        {
            position = Vector3.zero;
            direction = Vector3.forward;
            width = 1f;
            return;
        }
        distance = Mathf.Clamp(distance, 0f, Length);
        int low = 0;
        int high = distances.Count - 1;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (distances[middle] <= distance) low = middle;
            else high = middle;
        }
        float t = Mathf.InverseLerp(distances[low], distances[high], distance);
        position = Vector3.Lerp(positions[low], positions[high], t);
        direction = Vector3.Lerp(directions[low], directions[high], t).normalized;
        width = Mathf.Lerp(widths[low], widths[high], t);
    }
}
