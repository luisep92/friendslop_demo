using System.Collections.Generic;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Shared flat arrow mesh (shaft + triangular head), pointing +Z, about 0.4 m long and 0.04 m thick.
    /// Flat shaded: every face has its own vertices.
    /// </summary>
    public static class ArrowMesh
    {
        private const float HalfThickness = 0.02f;

        // Outline in XZ, counter-clockwise seen from above.
        private static readonly Vector2[] Outline =
        {
            new Vector2(-0.06f, -0.2f),
            new Vector2(0.06f, -0.2f),
            new Vector2(0.06f, 0.02f),
            new Vector2(0.16f, 0.02f),
            new Vector2(0f, 0.2f),
            new Vector2(-0.16f, 0.02f),
            new Vector2(-0.06f, 0.02f)
        };

        // Shaft quad (0, 1, 2, 6) and head triangle (5, 3, 4), as outline indices.
        private static readonly int[] CapTriangles = { 0, 6, 2, 0, 2, 1, 5, 4, 3 };

        private static Mesh _mesh;

        public static Mesh Get()
        {
            if (_mesh == null)
                _mesh = Build();
            return _mesh;
        }

        private static Mesh Build()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i < CapTriangles.Length; i += 3)
            {
                Vector2 a = Outline[CapTriangles[i]];
                Vector2 b = Outline[CapTriangles[i + 1]];
                Vector2 c = Outline[CapTriangles[i + 2]];
                AddTriangle(At(a, HalfThickness), At(b, HalfThickness), At(c, HalfThickness), Vector3.up, vertices, normals, triangles);
                AddTriangle(At(a, -HalfThickness), At(b, -HalfThickness), At(c, -HalfThickness), Vector3.down, vertices, normals, triangles);
            }

            for (int i = 0; i < Outline.Length; i++)
            {
                Vector2 a = Outline[i];
                Vector2 b = Outline[(i + 1) % Outline.Length];
                // Counter-clockwise outline: outward normal is on the right of the edge.
                var outward = new Vector3(b.y - a.y, 0f, -(b.x - a.x)).normalized;
                AddTriangle(At(a, HalfThickness), At(b, HalfThickness), At(b, -HalfThickness), outward, vertices, normals, triangles);
                AddTriangle(At(a, HalfThickness), At(b, -HalfThickness), At(a, -HalfThickness), outward, vertices, normals, triangles);
            }

            var mesh = new Mesh { name = "Arrow (runtime)" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 At(Vector2 point, float y) => new Vector3(point.x, y, point.y);

        /// <summary>Adds a triangle facing normal, fixing the winding if needed (Unity front faces are clockwise).</summary>
        private static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal,
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
                (b, c) = (c, b);

            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}
