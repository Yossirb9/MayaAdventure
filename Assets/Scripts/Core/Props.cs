using System.Collections.Generic;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Spawns Kenney Platformer Kit models (CC0, www.kenney.nl) from Assets/Resources/Kenney.
    /// Every model has its origin at the bottom centre, so <c>position</c> is where it stands.
    /// </summary>
    public static class Props
    {
        public enum Col { None, Box, Mesh }

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static bool Has(string model) => Load(model) != null;

        static GameObject Load(string model)
        {
            if (cache.TryGetValue(model, out var prefab) && prefab != null) return prefab;
            prefab = Resources.Load<GameObject>("Kenney/" + model);
            if (prefab == null) Debug.LogWarning($"[Maya] Missing Kenney model: {model}");
            return cache[model] = prefab;
        }

        public static GameObject Spawn(string model, Transform parent, Vector3 localPos, float scale, float yaw = 0f,
                                       Col col = Col.None, bool shadows = true) =>
            Spawn(model, parent, localPos, Vector3.one * scale, yaw, col, shadows);

        public static GameObject Spawn(string model, Transform parent, Vector3 localPos, Vector3 scale, float yaw = 0f,
                                       Col col = Col.None, bool shadows = true)
        {
            var prefab = Load(model);
            GameObject go = null;
            if (Nature.Substitutes(model))
            {
                // Newer Quaternius models replace the Kenney ones where a match exists (fitted into the same box).
                var wrapper = new GameObject(model);
                wrapper.transform.SetParent(parent, false);
                if (Nature.TrySubstitute(model, prefab, wrapper.transform, parent != null ? parent.TransformPoint(localPos) : localPos))
                    go = wrapper;
                else
                    Object.Destroy(wrapper);
            }
            if (go != null)
            {
                // substituted
            }
            else if (prefab != null)
            {
                go = Object.Instantiate(prefab, parent, false);
                go.name = model;
            }
            else
            {
                // Fallback so the level still plays if the kit is missing.
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(go.GetComponent<Collider>());
                go.name = model + " (missing)";
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

            if (model == "chest") go.AddComponent<TreasureChest>(); // every chest can be punched open
            switch (col)
            {
                case Col.Box:
                    AddBoxCollider(go);
                    break;
                case Col.Mesh:
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    break;
            }
            return go;
        }

        /// <summary>One box collider on the root that encloses all child meshes (in root space).</summary>
        public static BoxCollider AddBoxCollider(GameObject go)
        {
            var root = go.transform;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            var box = go.AddComponent<BoxCollider>();
            if (any)
            {
                box.center = b.center;
                box.size = b.size;
            }
            return box;
        }

        /// <summary>Multiplies the base colour of every material (works for glTF, URP Lit and Standard shaders).</summary>
        public static void Tint(GameObject go, Color color)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    // Multiply the material's own colour: the Quaternius models are coloured by it, not by a texture.
                    var c = m == null ? Color.white
                          : m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor")
                          : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                          : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    var tinted = new Color(c.r * color.r, c.g * color.g, c.b * color.b, c.a);
                    r.GetPropertyBlock(block, i);
                    block.SetColor("baseColorFactor", tinted);
                    block.SetColor("_BaseColor", tinted);
                    block.SetColor("_Color", tinted);
                    r.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
