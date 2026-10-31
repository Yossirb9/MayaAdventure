using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MayaAdventure
{
    /// <summary>Procedural materials, textures, primitives and meshes for the level.</summary>
    public static class Art
    {
        public enum Col { None, Default, Mesh }

        static Shader standard;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        /// <summary>URP Lit (the game renders with URP); the built-in Standard shader if URP is missing.</summary>
        static Shader Standard => standard != null ? standard : (standard = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

        /// <summary>Sets gloss / metal on either URP Lit or Standard.</summary>
        static void Surface(Material m, float gloss, float metallic)
        {
            m.SetFloat("_Smoothness", gloss);
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metallic);
        }

        // Unity 6 can enter Play mode without a domain reload; drop caches that point at
        // objects destroyed when the previous play session ended.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            materials.Clear();
            textures.Clear();
            standard = null;
            starMesh = null;
            coneMesh = null;
            lathes.Clear();
        }

        // ---------- materials ----------

        public static Material Solid(Color c, float gloss = 0.15f, float metallic = 0f)
        {
            string key = $"solid{c}{gloss}{metallic}";
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { color = c, name = key };
            Surface(m, gloss, metallic);
            m.enableInstancing = true;
            return materials[key] = m;
        }

        public static Material Glow(Color c, float intensity = 1f, float gloss = 0.7f, float metallic = 0.3f)
        {
            string key = $"glow{c}{intensity}{gloss}{metallic}";
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { color = c, name = key };
            Surface(m, gloss, metallic);
            m.enableInstancing = true;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return materials[key] = m;
        }

        public static Material Textured(string texName, Vector2 tiling, float gloss = 0.1f)
        {
            string key = $"tex{texName}{tiling}{gloss}";
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { name = key, mainTexture = Tex(texName), mainTextureScale = tiling };
            Surface(m, gloss, 0f);
            return materials[key] = m;
        }

        /// <summary>Lawn: subtle mottled grass texture multiplied by <paramref name="tint"/>.</summary>
        public static Material Lawn(Color tint, float tiling)
        {
            string key = $"lawn{tint}{tiling}";
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            var lawn = Shader.Find("Maya/Lawn");
            if (lawn != null)
            {
                // Painted in world space with sunny / dry / lush patches (tiling no longer matters).
                bool snow = tint.grayscale > 0.85f;
                m = new Material(lawn) { name = key };
                // Warmer, more natural greens than the minty block tint, with dry and deep patches.
                var grass = Color.Lerp(tint, new Color(0.36f, 0.62f, 0.24f) * (tint.grayscale / 0.55f), 0.6f);
                m.SetColor("_BaseColor", snow ? tint : grass);
                m.SetColor("_DryColor", snow ? tint * 1.03f : Color.Lerp(grass, new Color(0.8f, 0.76f, 0.38f), 0.5f));
                m.SetColor("_LushColor", snow ? new Color(tint.r * 0.88f, tint.g * 0.92f, tint.b, 1f) : Color.Lerp(grass * 0.62f, new Color(0.08f, 0.3f, 0.16f), 0.35f));
                return materials[key] = m;
            }
            m = new Material(Standard) { name = key, mainTexture = Tex("lawn"), mainTextureScale = new Vector2(tiling, tiling), color = tint };
            Surface(m, 0.08f, 0f);
            return materials[key] = m;
        }

        /// <summary>Unlit particle material (URP particles shader), additive or alpha blended.</summary>
        public static Material Particle(Texture2D tex, bool additive)
        {
            string key = $"particle{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(tex)}{additive}";
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            // Our own tiny particle shader: URP's particle shader drew opaque squares on some phones.
            var shader = Shader.Find("Maya/Particle");
            if (shader == null) return null;
            m = new Material(shader) { name = key };
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.renderQueue = (int)RenderQueue.Transparent;
            return materials[key] = m;
        }

        public static Material Water()
        {
            const string key = "water";
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { name = key, mainTexture = Tex("water"), mainTextureScale = new Vector2(80, 80) };
            m.color = new Color(1f, 1f, 1f, 0.82f);
            Surface(m, 0.9f, 0f);
            m.SetFloat("_Surface", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            return materials[key] = m;
        }

        // ---------- objects ----------

        /// <summary>Creates a primitive under <paramref name="parent"/> using local coordinates.</summary>
        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
                                      Material mat, Col col = Col.Default, Quaternion? localRot = null, bool shadows = true)
        {
            // A cylinder's default collider is a capsule, which turns flat discs into balls: use the real mesh.
            if (type == PrimitiveType.Cylinder && col == Col.Default) col = Col.Mesh;
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            ApplyCollider(go, col);
            return go;
        }

        public static GameObject MeshObject(string name, Mesh mesh, Transform parent, Vector3 localPos, Vector3 localScale,
                                            Material mat, Col col = Col.None, Quaternion? localRot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (col != Col.None) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static void ApplyCollider(GameObject go, Col col)
        {
            if (col == Col.Default) return;
            var existing = go.GetComponent<Collider>();
            if (existing != null) UnityEngine.Object.Destroy(existing);
            if (col == Col.Mesh) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }

        // ---------- meshes ----------

        static Mesh starMesh, coneMesh;

        /// <summary>Puffy five-pointed star in the XY plane, facing +Z, flat shaded.</summary>
        public static Mesh StarMesh()
        {
            if (starMesh != null) return starMesh;
            const float outer = 0.6f, inner = 0.27f, depth = 0.22f;
            var ring = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                ring[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var front = new Vector3(0, 0, depth);
            var back = new Vector3(0, 0, -depth);
            for (int i = 0; i < 10; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % 10];
                AddTri(verts, tris, front, b, a);
                AddTri(verts, tris, back, a, b);
            }
            starMesh = new Mesh { name = "Star" };
            starMesh.SetVertices(verts);
            starMesh.SetTriangles(tris, 0);
            starMesh.RecalculateNormals();
            starMesh.RecalculateBounds();
            return starMesh;
        }

        /// <summary>Cone with base radius 0.5 at y=0 and apex at y=1.</summary>
        public static Mesh ConeMesh()
        {
            if (coneMesh != null) return coneMesh;
            const int seg = 24;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var apex = Vector3.up;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                AddTri(verts, tris, apex, p1, p0);
                AddTri(verts, tris, Vector3.zero, p0, p1);
            }
            coneMesh = new Mesh { name = "Cone" };
            coneMesh.SetVertices(verts);
            coneMesh.SetTriangles(tris, 0);
            coneMesh.RecalculateNormals();
            coneMesh.RecalculateBounds();
            return coneMesh;
        }

        static readonly Dictionary<string, Mesh> lathes = new Dictionary<string, Mesh>();

        /// <summary>
        /// Smooth surface of revolution around Y: <paramref name="profile"/> is (radius, height) from bottom to top.
        /// Cached under <paramref name="key"/>.
        /// </summary>
        public static Mesh LatheMesh(string key, Vector2[] profile, int seg = 32)
        {
            if (lathes.TryGetValue(key, out var cached) && cached != null) return cached;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int rows = profile.Length;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                for (int j = 0; j < rows; j++)
                {
                    verts.Add(new Vector3(c * profile[j].x, profile[j].y, s * profile[j].x));
                    uvs.Add(new Vector2(i / (float)seg, j / (float)(rows - 1)));
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < rows - 1; j++)
                {
                    int a = i * rows + j, b = (i + 1) * rows + j;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(b); tris.Add(a + 1); tris.Add(b + 1);
                }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            lathes[key] = mesh;
            return mesh;
        }

        /// <summary>A smooth dome / cap profile: radius <paramref name="r"/>, height <paramref name="h"/>, a little rim underneath.</summary>
        public static Vector2[] DomeProfile(float r, float h, float rim = 0.08f, int steps = 10)
        {
            var pts = new List<Vector2> { new Vector2(0f, 0f), new Vector2(r * 0.85f, 0f), new Vector2(r, rim * 0.5f) };
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps * Mathf.PI * 0.5f;
                pts.Add(new Vector2(Mathf.Cos(t) * r, rim + Mathf.Sin(t) * h));
            }
            return pts.ToArray();
        }

        static void AddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            t.Add(v.Count); v.Add(a);
            t.Add(v.Count); v.Add(b);
            t.Add(v.Count); v.Add(c);
        }

        // ---------- textures ----------

        public static Texture2D Tex(string name)
        {
            if (textures.TryGetValue(name, out var t) && t != null) return t;
            switch (name)
            {
                case "grass":
                    t = Make(128, (u, v) =>
                    {
                        float n = Fbm(u, v, 1);
                        var c = Color.Lerp(new Color(0.28f, 0.58f, 0.18f), new Color(0.47f, 0.78f, 0.27f), n);
                        if (Hash((int)(u * 128), (int)(v * 128), 9) < 0.06f) c *= 0.82f;
                        return c;
                    });
                    break;
                case "lawn":
                    t = Make(256, (u, v) =>
                    {
                        float n = Fbm(u, v, 21) * 0.55f + VNoise(u, v, 32, 22) * 0.45f;
                        float g = Mathf.Lerp(0.72f, 1.12f, n);
                        if (Hash((int)(u * 256), (int)(v * 256), 23) < 0.05f) g *= 0.86f;
                        if (Hash((int)(u * 256), (int)(v * 256), 24) > 0.985f) g *= 1.12f;
                        return new Color(g * 0.97f, g, g * 0.95f);
                    });
                    break;
                case "sand":
                    t = Make(128, (u, v) => Color.Lerp(new Color(0.86f, 0.76f, 0.52f), new Color(0.96f, 0.89f, 0.68f),
                        Fbm(u, v, 2) * 0.8f + Hash((int)(u * 128), (int)(v * 128), 3) * 0.2f));
                    break;
                case "rock":
                    t = Make(128, (u, v) =>
                    {
                        float n = Fbm(u, v, 4);
                        float crack = Mathf.Abs(VNoise(u, v, 6, 5) - 0.5f) < 0.03f ? 0.75f : 1f;
                        return Color.Lerp(new Color(0.42f, 0.37f, 0.33f), new Color(0.66f, 0.6f, 0.53f), n) * crack;
                    });
                    break;
                case "brick":
                    t = Make(128, (u, v) =>
                    {
                        int px = (int)(u * 128), py = (int)(v * 128);
                        int row = py / 16;
                        int sx = px + (row % 2) * 16;
                        bool mortar = py % 16 < 2 || sx % 32 < 2;
                        if (mortar) return new Color(0.78f, 0.74f, 0.68f);
                        float h = Hash((sx / 32) % 4, row, 7);
                        var b = Color.Lerp(new Color(0.62f, 0.58f, 0.55f), new Color(0.82f, 0.78f, 0.72f), h);
                        return b * (0.9f + 0.1f * Fbm(u, v, 8));
                    });
                    break;
                case "plank":
                    t = Make(128, (u, v) =>
                    {
                        int py = (int)(v * 128);
                        bool gap = py % 32 < 2;
                        float grain = VNoise(u * 0.25f, v * 4f % 1f, 8, 11);
                        var c = Color.Lerp(new Color(0.55f, 0.36f, 0.2f), new Color(0.74f, 0.52f, 0.3f), grain);
                        return gap ? c * 0.55f : c;
                    });
                    break;
                case "checker":
                    t = Make(64, (u, v) => ((int)(u * 2) + (int)(v * 2)) % 2 == 0
                        ? new Color(1f, 0.55f, 0.75f) : new Color(1f, 0.93f, 0.97f), FilterMode.Bilinear);
                    break;
                case "water":
                    t = Make(128, (u, v) =>
                    {
                        float n = Fbm(u, v, 12);
                        var c = Color.Lerp(new Color(0.12f, 0.45f, 0.78f), new Color(0.35f, 0.75f, 0.95f), n * n);
                        return c;
                    });
                    break;
                case "question":
                    t = PixelBlock(true);
                    break;
                case "usedblock":
                    t = PixelBlock(false);
                    break;
                default:
                    t = Texture2D.whiteTexture;
                    break;
            }
            return textures[name] = t;
        }

        static Texture2D PixelBlock(bool question)
        {
            string[] glyph =
            {
                "..####..",
                ".##..##.",
                ".....##.",
                "....##..",
                "...##...",
                "...##...",
                "........",
                "...##...",
                "...##...",
            };
            var fill = question ? new Color(1f, 0.78f, 0.15f) : new Color(0.55f, 0.36f, 0.2f);
            var edge = question ? new Color(0.85f, 0.45f, 0.05f) : new Color(0.35f, 0.22f, 0.12f);
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                var c = fill;
                if (x == 0 || y == 0 || x == 15 || y == 15) c = edge;
                if ((x == 2 || x == 13) && (y == 2 || y == 13)) c = edge;
                if (question)
                {
                    int gx = x - 4, gy = 12 - y;
                    if (gx >= 0 && gx < 8 && gy >= 0 && gy < glyph.Length)
                    {
                        if (glyph[gy][gx] == '#') c = Color.white;
                        else if (gx > 0 && gy > 0 && glyph[gy - 1][gx - 1] == '#') c = edge;
                    }
                }
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        static Texture2D Make(int size, Func<float, float, Color> f, FilterMode filter = FilterMode.Trilinear)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = filter,
                anisoLevel = 4,
            };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = f(x / (float)size, y / (float)size);
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // Tileable value noise: lattice wraps at `cells`, so textures repeat seamlessly.
        static float VNoise(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, cells), xb = Mod(x0 + 1, cells), ya = Mod(y0, cells), yb = Mod(y0 + 1, cells);
            float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx);
            float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float u, float v, int seed) =>
            VNoise(u, v, 4, seed) * 0.5f + VNoise(u, v, 8, seed + 1) * 0.3f + VNoise(u, v, 16, seed + 2) * 0.2f;

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xffffff) / (float)0xffffff;
            }
        }
    }
}
