using System.Collections.Generic;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Organic islands and blocks built as meshes: a wobbly outline, a flat walkable top with a rounded grass lip,
    /// and a rocky cliff underneath (down to the sea floor, or tapering to a point for floating islands).
    /// Replaces the old grids of square tiles; the walkable top keeps the same size and height.
    /// </summary>
    public static class IslandMesh
    {
        /// <param name="bottom">Cliff bottom; NaN for a floating island with a pointed rock underside.</param>
        public static GameObject Build(string name, Transform parent, Vector3 center, float radius, float top, float bottom,
                                       Material lawn, Material rock, int seed)
        {
            bool floating = float.IsNaN(bottom);
            float depth = floating ? 3.5f + radius * 0.9f : top - bottom;
            int n = Mathf.Clamp(Mathf.RoundToInt(radius * 5f), 36, 180);
            var rng = new System.Random(seed);
            var phase = new float[8];
            for (int i = 0; i < phase.Length; i++) phase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;

            // Edge radius per angle: always a little beyond the old tile edge, never inside the walkable area.
            float Edge(float a)
            {
                float w = Mathf.Sin(a * 2f + phase[0]) * 0.45f + Mathf.Sin(a * 3f + phase[1]) * 0.35f +
                          Mathf.Sin(a * 5f + phase[2]) * 0.25f + Mathf.Sin(a * 9f + phase[3]) * 0.15f;
                return radius + 1.3f + w * Mathf.Min(1.6f, radius * 0.18f);
            }
            float Bump(float a, float y) =>
                Mathf.Sin(a * 7f + y * 0.9f + phase[4]) * 0.35f + Mathf.Sin(a * 13f - y * 1.7f + phase[5]) * 0.2f +
                Mathf.Sin(a * 3f + y * 0.4f + phase[6]) * 0.3f;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var grassTris = new List<int>();
            var rockTris = new List<int>();
            float diameter = (radius + 3f) * 2f;

            // Top: rings from the centre to the lip.
            float[] ringK = { 0f, 0.3f, 0.55f, 0.75f, 0.88f, 0.96f, 1f };
            int topStart = verts.Count;
            for (int ri = 0; ri < ringK.Length; ri++)
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float r = Edge(a) * ringK[ri];
                var p = new Vector3(Mathf.Cos(a) * r, top, Mathf.Sin(a) * r);
                verts.Add(p);
                uvs.Add(new Vector2(p.x / diameter + 0.5f, p.z / diameter + 0.5f));
            }
            for (int ri = 0; ri < ringK.Length - 1; ri++) Strip(grassTris, topStart + ri * (n + 1), topStart + (ri + 1) * (n + 1), n, true);

            // Rounded grass lip folding over the cliff top.
            float[] lipOut = { 0.28f, 0.38f, 0.3f };
            float[] lipDown = { 0.1f, 0.32f, 0.55f };
            int prev = topStart + (ringK.Length - 1) * (n + 1);
            for (int li = 0; li < lipOut.Length; li++)
            {
                int start = verts.Count;
                for (int i = 0; i <= n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    float r = Edge(a) + lipOut[li];
                    var p = new Vector3(Mathf.Cos(a) * r, top - lipDown[li], Mathf.Sin(a) * r);
                    verts.Add(p);
                    uvs.Add(new Vector2(p.x / diameter + 0.5f, p.z / diameter + 0.5f));
                }
                Strip(grassTris, prev, start, n, true);
                prev = start;
            }

            // Cliff: tucked under the lip, rocky bumps, tapering to a point when floating.
            int rings = Mathf.Max(3, Mathf.CeilToInt(depth / 1.1f));
            int cliffTop = -1;
            for (int ri = 0; ri <= rings; ri++)
            {
                float t = ri / (float)rings;
                float y = top - 0.45f - t * (depth - 0.45f);
                float shape = floating ? 1f - Mathf.Pow(t, 1.4f) * 0.97f : 1f + t * 0.08f;
                int start = verts.Count;
                for (int i = 0; i <= n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    float bump = ri == 0 ? 0f : Bump(a, y) * (floating ? 1f - t * 0.6f : 1f);
                    float r = Mathf.Max(0.05f, (Edge(a) - 0.05f + bump) * shape);
                    var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    verts.Add(p);
                    float circumference = Edge(a) * Mathf.PI * 2f;
                    uvs.Add(new Vector2(i / (float)n * circumference / 3.5f, y / 3.5f));
                }
                if (ri == 0) cliffTop = start;
                else Strip(rockTris, start - (n + 1), start, n, true);
            }
            // Close the bottom of grounded islands (hidden under the sea floor anyway) and the floating tip.
            int last = verts.Count - (n + 1);
            int tip = verts.Count;
            verts.Add(new Vector3(0f, top - depth - (floating ? radius * 0.25f : 0f), 0f));
            uvs.Add(new Vector2(0.5f, 0f));
            for (int i = 0; i < n; i++)
            {
                rockTris.Add(last + i);
                rockTris.Add(last + i + 1);
                rockTris.Add(tip);
            }
            // Seam between the lip and the cliff (grass colour so the edge reads as a turf overhang).
            Strip(grassTris, prev, cliffTop, n, true);

            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(grassTris, 0);
            mesh.SetTriangles(rockTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(center.x, 0f, center.z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { lawn, rock };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>A rounded grassy block (replaces the stretched Kenney grass blocks used as steps and ledges).</summary>
        public static GameObject Block(string name, Transform parent, Vector3 center, Vector3 size, Material lawn, Material rock)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            // Turf slab on top, rock body below, both slightly rounded with bevelled boxes.
            float turf = Mathf.Min(0.35f, size.y * 0.35f);
            var body = Art.Prim(PrimitiveType.Cube, "Body", go.transform, new Vector3(0f, -turf * 0.5f, 0f),
                                new Vector3(size.x * 0.96f, size.y - turf, size.z * 0.96f), rock, Art.Col.None);
            body.GetComponent<MeshFilter>().sharedMesh = RoundedBox();
            var cap = Art.Prim(PrimitiveType.Cube, "Turf", go.transform, new Vector3(0f, size.y * 0.5f - turf * 0.5f, 0f),
                               new Vector3(size.x * 1.02f, turf, size.z * 1.02f), lawn, Art.Col.None);
            cap.GetComponent<MeshFilter>().sharedMesh = RoundedBox();
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            return go;
        }

        static Mesh roundedBox;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => roundedBox = null;

        /// <summary>Unit cube with chamfered edges, so blocks catch soft highlights instead of hard corners.</summary>
        static Mesh RoundedBox()
        {
            if (roundedBox != null) return roundedBox;
            const float h = 0.5f, c = 0.1f, k = h - c;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            void Poly(Vector3[] pts)
            {
                // Flat-shaded polygon, wound to face away from the cube centre.
                var centre = Vector3.zero;
                foreach (var p in pts) centre += p;
                centre /= pts.Length;
                int s = v.Count;
                foreach (var p in pts)
                {
                    v.Add(p);
                    uv.Add(new Vector2(p.x + p.z + 0.5f, p.y + 0.5f));
                }
                for (int i = 1; i < pts.Length - 1; i++)
                {
                    var nrm = Vector3.Cross(pts[i] - pts[0], pts[i + 1] - pts[0]);
                    bool outward = Vector3.Dot(nrm, centre) > 0f;
                    tris.Add(s);
                    tris.Add(outward ? s + i : s + i + 1);
                    tris.Add(outward ? s + i + 1 : s + i);
                }
            }
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            for (int a = 0; a < 3; a++)
            {
                var n = axes[a];
                var t1 = axes[(a + 1) % 3];
                var t2 = axes[(a + 2) % 3];
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    var f = n * (h * sgn);
                    Poly(new[] { f + (-t1 - t2) * k, f + (t1 - t2) * k, f + (t1 + t2) * k, f + (-t1 + t2) * k });
                }
                // Edge bevels parallel to n.
                for (int s1 = -1; s1 <= 1; s1 += 2)
                for (int s2 = -1; s2 <= 1; s2 += 2)
                {
                    var e1 = t1 * (h * s1) + t2 * (k * s2);
                    var e2 = t1 * (k * s1) + t2 * (h * s2);
                    Poly(new[] { e1 - n * k, e1 + n * k, e2 + n * k, e2 - n * k });
                }
            }
            // Corner triangles.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Poly(new[] { new Vector3(h * sx, k * sy, k * sz), new Vector3(k * sx, h * sy, k * sz), new Vector3(k * sx, k * sy, h * sz) });
            var mesh = new Mesh { name = "RoundedBox" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return roundedBox = mesh;
        }

        static void Strip(List<int> tris, int a, int b, int n, bool flip)
        {
            for (int i = 0; i < n; i++)
            {
                int a0 = a + i, a1 = a + i + 1, b0 = b + i, b1 = b + i + 1;
                if (!flip)
                {
                    tris.Add(a0); tris.Add(b0); tris.Add(a1);
                    tris.Add(a1); tris.Add(b0); tris.Add(b1);
                }
                else
                {
                    tris.Add(a0); tris.Add(a1); tris.Add(b0);
                    tris.Add(a1); tris.Add(b1); tris.Add(b0);
                }
            }
        }
    }
}
