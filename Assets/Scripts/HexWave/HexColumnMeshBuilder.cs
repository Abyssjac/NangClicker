using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    /// <summary>Flat-top column with a stable 1.5m top datum, hard side normals and metre-density side UVs.</summary>
    public static class HexColumnMeshBuilder
    {
        public const float TopDatum = 1.5f;
        public static string Key(float radius, float depth) =>
            "HexColumn_R" + Mathf.RoundToInt(radius * 1000) + "_D" + Mathf.RoundToInt(depth * 1000);
        public static Mesh Build(float radius, float depth)
        {
            radius = Mathf.Max(0.01f, radius); depth = Mathf.Max(0.05f, depth);
            var vertices = new List<Vector3>(38); var normals = new List<Vector3>(38);
            var uv = new List<Vector2>(38); var triangles = new List<int>(72);
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? TopDatum : TopDatum - depth;
                var normal = cap == 0 ? Vector3.up : Vector3.down;
                vertices.Add(new Vector3(0, y, 0)); normals.Add(normal); uv.Add(Vector2.one * 0.5f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3;
                    var p = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
                    vertices.Add(p); normals.Add(normal); uv.Add(new Vector2(p.x, p.z) / (2 * radius) + Vector2.one * 0.5f);
                }
                int start = cap * 7;
                for (int i = 0; i < 6; i++)
                {
                    triangles.Add(start);
                    triangles.Add(start + (cap == 0 ? (i + 1) % 6 + 1 : i + 1));
                    triangles.Add(start + (cap == 0 ? i + 1 : (i + 1) % 6 + 1));
                }
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3, b = (i + 1) * Mathf.PI / 3;
                var p = new Vector3(Mathf.Cos(a) * radius, TopDatum - depth, Mathf.Sin(a) * radius);
                var q = new Vector3(Mathf.Cos(b) * radius, TopDatum - depth, Mathf.Sin(b) * radius);
                var n = new Vector3(Mathf.Cos((a + b) * 0.5f), 0, Mathf.Sin((a + b) * 0.5f));
                int start = vertices.Count;
                vertices.Add(p); vertices.Add(q); vertices.Add(q + Vector3.up * depth); vertices.Add(p + Vector3.up * depth);
                for (int j = 0; j < 4; j++) normals.Add(n);
                float u = i * radius;
                uv.Add(new Vector2(u, depth)); uv.Add(new Vector2(u + radius, depth));
                uv.Add(new Vector2(u + radius, 0)); uv.Add(new Vector2(u, 0));
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
            var mesh = new Mesh { name = Key(radius, depth) };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
