using NUnit.Framework;
using UnityEngine;

public sealed class RouteFlowPathTests
{
    [Test]
    public void RoundedCornerPreservesEndpointsAndStaysInsideWalkableCells()
    {
        var path = new RouteFlowPath(new[] { Vector3.zero, Vector3.right, Vector3.right + Vector3.forward }, new[] { 1f, 1f, 1f });
        path.Sample(0f, out var start, out var firstDirection, out _);
        path.Sample(path.Length, out var end, out var lastDirection, out _);
        Assert.That(start, Is.EqualTo(Vector3.zero));
        Assert.That(end, Is.EqualTo(Vector3.right + Vector3.forward));
        Assert.That(Vector3.Angle(firstDirection, Vector3.right), Is.LessThan(0.01f));
        Assert.That(Vector3.Angle(lastDirection, Vector3.forward), Is.LessThan(0.01f));
        for (float distance = 0; distance <= path.Length; distance += 0.005f)
        {
            path.Sample(distance, out var position, out _, out _);
            Assert.That(position.x >= 0.5f || position.z <= 0.5f, Is.True, "The corner must not cut through the diagonally adjacent wall.");
            Assert.That(position.x, Is.InRange(0f, 1f));
            Assert.That(position.z, Is.InRange(0f, 1f));
        }
    }

    [Test]
    public void DistanceSamplingDoesNotSlowDownOrSnapHeadingAtBends()
    {
        var path = new RouteFlowPath(new[] { Vector3.zero, Vector3.right, Vector3.right + Vector3.forward, Vector3.right * 2 + Vector3.forward }, new[] { 1f, 1f, 1f, 1f });
        const float step = 0.005f;
        path.Sample(0f, out var previous, out var heading, out _);
        for (float distance = step; distance < path.Length; distance += step)
        {
            path.Sample(distance, out var position, out var direction, out _);
            Assert.That(Vector3.Distance(previous, position), Is.EqualTo(step).Within(0.00005f));
            Assert.That(Vector3.Angle(heading, direction), Is.LessThan(3f));
            previous = position;
            heading = direction;
        }
    }

    [Test]
    public void SharedLaneWidthChangesContinuouslyAcrossTheMerge()
    {
        var path = new RouteFlowPath(new[] { Vector3.zero, Vector3.forward, Vector3.forward * 2 }, new[] { 2f, 1f, 1f });
        float previous = 2f;
        for (float distance = 0; distance <= path.Length; distance += 0.01f)
        {
            path.Sample(distance, out _, out _, out float width);
            Assert.That(width, Is.InRange(1f, 2f));
            Assert.That(Mathf.Abs(width - previous), Is.LessThan(0.02f));
            previous = width;
        }
    }

    [Test]
    public void DuplicateAndEmptyWaypointsDoNotProduceInvalidSamples()
    {
        var path = new RouteFlowPath(new[] { Vector3.zero, Vector3.zero, Vector3.forward }, new[] { 1f, 1f, 1f });
        Assert.That(path.Length, Is.EqualTo(1f).Within(0.00001f));
        path.Sample(0.5f, out var position, out var direction, out _);
        Assert.That(position, Is.EqualTo(Vector3.forward * 0.5f));
        Assert.That(direction, Is.EqualTo(Vector3.forward));
        var empty = new RouteFlowPath(System.Array.Empty<Vector3>(), System.Array.Empty<float>());
        empty.Sample(0f, out position, out direction, out float width);
        Assert.That(empty.Length, Is.Zero);
        Assert.That(position, Is.EqualTo(Vector3.zero));
        Assert.That(direction, Is.EqualTo(Vector3.forward));
        Assert.That(width, Is.EqualTo(1f));
    }
}
