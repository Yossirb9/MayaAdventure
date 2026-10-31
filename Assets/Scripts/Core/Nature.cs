using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MayaAdventure
{
    /// <summary>
    /// The Quaternius kits (CC0): Stylized Nature MegaKit (Resources/Nature) and Ultimate Platformer Pack
    /// (Resources/Platformer). <see cref="Props.Spawn"/> swaps the old Kenney models for these, fitted into the
    /// same box, so every level upgrades without changing its layout. Also scatters instanced grass on islands.
    /// </summary>
    public static class Nature
    {
        enum Fit { Height, Footprint, Box }

        struct Sub
        {
            public string[] Models;
            public Fit Fit;
            public float Scale;
            public Color? Tint;
        }

        static readonly Color SnowTint = new Color(1.45f, 1.6f, 1.75f);

        static readonly Dictionary<string, Sub> subs = new Dictionary<string, Sub>
        {
            { "tree", new Sub { Models = new[] { "Nature/CommonTree_1", "Nature/CommonTree_2", "Nature/CommonTree_3", "Nature/CommonTree_4", "Nature/CommonTree_5" }, Fit = Fit.Height, Scale = 1.15f } },
            { "tree-pine", new Sub { Models = new[] { "Nature/Pine_1", "Nature/Pine_2", "Nature/Pine_3", "Nature/Pine_4", "Nature/Pine_5" }, Fit = Fit.Height, Scale = 1.15f } },
            { "tree-pine-snow", new Sub { Models = new[] { "Nature/Pine_1", "Nature/Pine_2", "Nature/Pine_3", "Nature/Pine_5" }, Fit = Fit.Height, Scale = 1.15f, Tint = SnowTint } },
            { "tree-snow", new Sub { Models = new[] { "Nature/Pine_2", "Nature/Pine_4" }, Fit = Fit.Height, Scale = 1.1f, Tint = SnowTint } },
            { "flowers", new Sub { Models = new[] { "Nature/Flower_3_Group", "Nature/Flower_4_Group" }, Fit = Fit.Footprint, Scale = 0.7f } },
            { "flowers-tall", new Sub { Models = new[] { "Nature/Flower_4_Group", "Nature/Flower_3_Group" }, Fit = Fit.Height, Scale = 1f } },
            { "grass", new Sub { Models = new[] { "Nature/Grass_Common_Tall", "Nature/Grass_Wispy_Tall" }, Fit = Fit.Height, Scale = 1.6f } },
            { "plant", new Sub { Models = new[] { "Nature/Fern_1", "Nature/Plant_1", "Nature/Plant_7" }, Fit = Fit.Footprint, Scale = 1f } },
            { "mushrooms", new Sub { Models = new[] { "Nature/Mushroom_Common", "Nature/Mushroom_Laetiporus" }, Fit = Fit.Height, Scale = 1.3f } },
            { "rocks", new Sub { Models = new[] { "Nature/Rock_Medium_1", "Nature/Rock_Medium_2", "Nature/Rock_Medium_3" }, Fit = Fit.Footprint, Scale = 1.1f } },
            { "stones", new Sub { Models = new[] { "Nature/Pebble_Round_1", "Nature/Pebble_Round_2", "Nature/Pebble_Round_3" }, Fit = Fit.Footprint, Scale = 0.7f } },
            { "crate", new Sub { Models = new[] { "Platformer/Cube_Crate" }, Fit = Fit.Box, Scale = 1f } },
            { "crate-item", new Sub { Models = new[] { "Platformer/Cube_Question" }, Fit = Fit.Box, Scale = 1f } },
            { "brick", new Sub { Models = new[] { "Platformer/Cube_Bricks" }, Fit = Fit.Box, Scale = 1f } },
            { "chest", new Sub { Models = new[] { "Platformer/Chest" }, Fit = Fit.Footprint, Scale = 1f } },
            { "flag", new Sub { Models = new[] { "Platformer/Goal_Flag" }, Fit = Fit.Height, Scale = 1f } },
            { "spring", new Sub { Models = new[] { "Platformer/Bouncer" }, Fit = Fit.Box, Scale = 1f } },
            { "bomb", new Sub { Models = new[] { "Platformer/Bomb" }, Fit = Fit.Height, Scale = 1f } },
            { "heart", new Sub { Models = new[] { "Platformer/Heart" }, Fit = Fit.Height, Scale = 1f } },
            { "coin-gold", new Sub { Models = new[] { "Platformer/Coin" }, Fit = Fit.Height, Scale = 1f } },
            { "star", new Sub { Models = new[] { "Platformer/Star" }, Fit = Fit.Height, Scale = 1.1f } },
            { "spike-block", new Sub { Models = new[] { "Platformer/Cube_Spikes" }, Fit = Fit.Box, Scale = 1f } },
            { "saw", new Sub { Models = new[] { "Platformer/Hazard_Saw" }, Fit = Fit.Height, Scale = 1f } },
        };

        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<GameObject, Bounds> bounds = new Dictionary<GameObject, Bounds>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            prefabs.Clear();
            bounds.Clear();
        }

        public static bool Substitutes(string kenneyModel) => subs.ContainsKey(kenneyModel);

        public static GameObject Load(string path)
        {
            if (prefabs.TryGetValue(path, out var p) && p != null) return p;
            return prefabs[path] = Resources.Load<GameObject>(path);
        }

        /// <summary>Mesh bounds of a prefab in its own root space.</summary>
        public static Bounds LocalBounds(GameObject prefab)
        {
            if (bounds.TryGetValue(prefab, out var b)) return b;
            bool any = false;
            var root = prefab.transform;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                Encapsulate(ref b, ref any, mf.sharedMesh, root.worldToLocalMatrix * mf.transform.localToWorldMatrix);
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Encapsulate(ref b, ref any, smr.sharedMesh, root.worldToLocalMatrix * smr.transform.localToWorldMatrix);
            if (!any) b = new Bounds(Vector3.up * 0.5f, Vector3.one);
            return bounds[prefab] = b;
        }

        static void Encapsulate(ref Bounds b, ref bool any, Mesh mesh, Matrix4x4 m)
        {
            if (mesh == null) return;
            var mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }

        /// <summary>
        /// Instantiates the replacement for a Kenney model under <paramref name="wrapper"/>, fitted into the Kenney
        /// model's own bounds (so the wrapper keeps the old scale and every collider callers add still fits).
        /// </summary>
        public static bool TrySubstitute(string kenneyModel, GameObject kenneyPrefab, Transform wrapper, Vector3 seedPos)
        {
            if (!subs.TryGetValue(kenneyModel, out var sub)) return false;
            int pick = Mathf.Abs(Mathf.RoundToInt(seedPos.x * 7.3f + seedPos.z * 3.1f + seedPos.y * 1.7f)) % sub.Models.Length;
            var prefab = Load(sub.Models[pick]);
            if (prefab == null) return false;
            var target = kenneyPrefab != null ? LocalBounds(kenneyPrefab) : new Bounds(Vector3.up * 0.5f, Vector3.one);
            var src = LocalBounds(prefab);
            Vector3 k;
            switch (sub.Fit)
            {
                case Fit.Box:
                    k = new Vector3(target.size.x / Mathf.Max(1e-3f, src.size.x), target.size.y / Mathf.Max(1e-3f, src.size.y), target.size.z / Mathf.Max(1e-3f, src.size.z));
                    break;
                case Fit.Footprint:
                    k = Vector3.one * (Mathf.Max(target.size.x, target.size.z) / Mathf.Max(1e-3f, Mathf.Max(src.size.x, src.size.z)));
                    break;
                default:
                    k = Vector3.one * (target.size.y / Mathf.Max(1e-3f, src.size.y));
                    break;
            }
            k *= sub.Scale;
            var go = Object.Instantiate(prefab, wrapper, false);
            go.name = prefab.name;
            go.transform.localScale = k;
            // Bottom-centre of the new model on the bottom-centre of the old one.
            var bottom = new Vector3(src.center.x * k.x, src.min.y * k.y, src.center.z * k.z);
            go.transform.localPosition = new Vector3(target.center.x, target.min.y, target.center.z) - bottom;
            if (sub.Tint.HasValue) Props.Tint(go, sub.Tint.Value);
            return true;
        }

        // ---------- instanced ground cover ----------

        public enum Cover { Meadow, Dusk, Beach, Snow }

        static readonly Color MeadowGrassTint = new Color(0.8f, 0.86f, 0.62f);

        /// <summary>Scatters grass, clover, flowers and pebbles over a flat island top (drawn with GPU instancing).</summary>
        public static void Scatter(Transform parent, Vector3 center, float radius, float top, Cover cover, int seed)
        {
            float density = GameSettings.Quality == 2 ? 0.2f : GameSettings.Quality == 1 ? 0.11f : 0.045f;
            if (cover == Cover.Snow || cover == Cover.Beach) density *= 0.35f;
            string[] kinds = cover switch
            {
                Cover.Snow => new[] { "Nature/Pebble_Round_1", "Nature/Pebble_Round_2", "Nature/Grass_Wispy_Short" },
                Cover.Beach => new[] { "Nature/Clover_1", "Nature/Pebble_Round_3", "Nature/Grass_Wispy_Short", "Nature/Plant_7" },
                Cover.Dusk => new[] { "Nature/Grass_Common_Short", "Nature/Grass_Wispy_Short", "Nature/Fern_1", "Nature/Mushroom_Common", "Nature/Clover_2" },
                _ => new[] { "Nature/Grass_Common_Short", "Nature/Grass_Common_Short", "Nature/Grass_Wispy_Short", "Nature/Clover_1",
                             "Nature/Flower_3_Single", "Nature/Flower_4_Single", "Nature/Clover_2", "Nature/Grass_Common_Short" },
            };
            var field = new GameObject("GroundCover").AddComponent<GrassField>();
            field.transform.SetParent(parent, false);
            var rng = new System.Random(seed);
            int count = Mathf.RoundToInt(Mathf.PI * radius * radius * density);
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * (radius - 0.8f);
                var p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                p.y = top;
                string kind = kinds[rng.Next(kinds.Length)];
                float s = 0.55f + (float)rng.NextDouble() * 0.6f;
                if (kind.Contains("Pebble")) s *= 0.35f;
                if (kind.Contains("Flower") || kind.Contains("Fern") || kind.Contains("Mushroom")) s *= 0.6f;
                // Grass tufts in a softer, warmer green so they sit into the lawn instead of glowing on top of it.
                Color? tint = cover == Cover.Snow ? SnowTint : kind.Contains("Grass") && cover == Cover.Meadow ? MeadowGrassTint : (Color?)null;
                field.Add(Load(kind), Matrix4x4.TRS(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * s), tint);
            }
        }
    }

    /// <summary>Draws many copies of small decoration meshes with GPU instancing, in cells near the camera.</summary>
    public class GrassField : MonoBehaviour
    {
        const float CellSize = 10f, DrawDistance = 55f;

        class Batch
        {
            public Mesh Mesh;
            public int SubMesh;
            public Material Material;
            public readonly Dictionary<Vector2Int, List<Matrix4x4>> Cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
        }

        static readonly Dictionary<(Material, bool), Material> instanced = new Dictionary<(Material, bool), Material>();
        readonly List<Batch> batches = new List<Batch>();
        readonly Dictionary<(Mesh, int), Batch> lookup = new Dictionary<(Mesh, int), Batch>();
        readonly Plane[] frustum = new Plane[6];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instanced.Clear();

        public void Add(GameObject prefab, Matrix4x4 placement, Color? tint)
        {
            if (prefab == null) return;
            var root = prefab.transform;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null) continue;
                var local = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var m = transform.localToWorldMatrix * placement * local;
                var cell = new Vector2Int(Mathf.FloorToInt(m.m03 / CellSize), Mathf.FloorToInt(m.m23 / CellSize));
                var mats = mr.sharedMaterials;
                for (int s = 0; s < mf.sharedMesh.subMeshCount && s < mats.Length; s++)
                {
                    if (!lookup.TryGetValue((mf.sharedMesh, s), out var b))
                    {
                        b = new Batch { Mesh = mf.sharedMesh, SubMesh = s, Material = Instanced(mats[s], tint.HasValue) };
                        if (tint.HasValue) b.Material.SetColor("baseColorFactor", tint.Value);
                        lookup[(mf.sharedMesh, s)] = b;
                        batches.Add(b);
                    }
                    if (!b.Cells.TryGetValue(cell, out var list)) b.Cells[cell] = list = new List<Matrix4x4>();
                    list.Add(m);
                }
            }
        }

        static Material Instanced(Material source, bool tinted)
        {
            if (source == null) return null;
            if (instanced.TryGetValue((source, tinted), out var m) && m != null) return m;
            m = new Material(source) { enableInstancing = true };
            return instanced[(source, tinted)] = m;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, frustum);
            var cp = cam.transform.position;
            foreach (var b in batches)
            {
                if (b.Material == null) continue;
                var rp = new RenderParams(b.Material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = 0 };
                foreach (var kv in b.Cells)
                {
                    var c = new Vector3((kv.Key.x + 0.5f) * CellSize, cp.y, (kv.Key.y + 0.5f) * CellSize);
                    if ((new Vector2(c.x - cp.x, c.z - cp.z)).sqrMagnitude > DrawDistance * DrawDistance) continue;
                    var list = kv.Value;
                    var bb = new Bounds(new Vector3(c.x, list[0].m13, c.z), new Vector3(CellSize + 4f, 8f, CellSize + 4f));
                    if (!GeometryUtility.TestPlanesAABB(frustum, bb)) continue;
                    for (int start = 0; start < list.Count; start += 500)
                        Graphics.RenderMeshInstanced(rp, b.Mesh, b.SubMesh, list, Mathf.Min(500, list.Count - start), start);
                }
            }
        }
    }
}
