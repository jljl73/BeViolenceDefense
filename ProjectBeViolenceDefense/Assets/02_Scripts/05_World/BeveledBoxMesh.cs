using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 윗면 네 모서리를 비스듬히 깎은 상자 메시. 피벗은 상자 중심이며 아랫면은 보이지 않아 만들지 않는다.
/// </summary>
internal static class BeveledBoxMesh
{
    public static Mesh Create(string name, Vector3 size, float bevel)
    {
        Vector3 h = size * 0.5f;
        bevel = Mathf.Clamp(bevel, 0.001f, Mathf.Min(h.x, h.y, h.z) * 0.9f);
        float top = h.y;
        float shoulder = h.y - bevel;
        float ix = h.x - bevel;
        float iz = h.z - bevel;

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        // 윗면
        Quad(new(-ix, top, -iz), new(-ix, top, iz), new(ix, top, iz), new(ix, top, -iz));

        for (int side = 0; side < 4; side++)
        {
            // 0:+X 1:+Z 2:-X 3:-Z. 각 변을 같은 방식으로 돌려가며 만든다.
            Quaternion turn = Quaternion.Euler(0f, -90f * side, 0f);
            bool alongZ = side % 2 == 0;
            float halfAcross = alongZ ? h.x : h.z;
            float halfAlong = alongZ ? h.z : h.x;
            float insetAlong = alongZ ? iz : ix;

            // 경사면
            Quad(turn * new Vector3(halfAcross - bevel, top, -insetAlong),
                turn * new Vector3(halfAcross - bevel, top, insetAlong),
                turn * new Vector3(halfAcross, shoulder, insetAlong),
                turn * new Vector3(halfAcross, shoulder, -insetAlong));

            // 모서리 꼭짓점 (이 변과 다음 변 사이)
            Vector3 corner = turn * new Vector3(halfAcross - bevel, top, insetAlong);
            Vector3 edgeA = turn * new Vector3(halfAcross, shoulder, insetAlong);
            Vector3 outer = turn * new Vector3(halfAcross, shoulder, halfAlong);
            Vector3 edgeB = turn * new Vector3(halfAcross - bevel, shoulder, halfAlong);
            Triangle(corner, edgeA, outer);
            Triangle(corner, outer, edgeB);

            // 옆면
            Quad(turn * new Vector3(halfAcross, -h.y, -halfAlong),
                turn * new Vector3(halfAcross, -h.y, halfAlong),
                turn * new Vector3(halfAcross, shoulder, halfAlong),
                turn * new Vector3(halfAcross, shoulder, -halfAlong));
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            // 볼록한 상자이므로 중심에서 바깥을 향하도록 감는 방향을 맞춘다.
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, a + b + c) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            // 윗면·경사면은 위에서 투영하고, 옆면은 가로축과 높이로 투영한다.
            bool wall = Mathf.Abs(normal.normalized.y) < 0.1f;
            bool facesX = Mathf.Abs(normal.x) > Mathf.Abs(normal.z);
            int start = vertices.Count;
            foreach (Vector3 v in new[] { a, b, c })
            {
                vertices.Add(v);
                float u = (wall && facesX ? v.z / size.z : v.x / size.x) + 0.5f;
                uvs.Add(new Vector2(u, wall ? v.y / size.y + 0.5f : v.z / size.z + 0.5f));
            }
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}
