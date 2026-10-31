using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Particle effects: glowing sparkles (coins, hits, stars, splashes) and soft dust puffs (landings, running).
    /// Two shared particle systems emit everything, so all effects together cost two draw calls.
    /// </summary>
    public static class Debris
    {
        static ParticleSystem sparkles, dust;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => sparkles = dust = null;

        static Texture2D Soft(int n, float hardness)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Pow(a, hardness);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            return t;
        }

        static ParticleSystem Make(string name, bool additive, float gravity)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, additive ? 1f : 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, additive ? 0f : 1.4f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(additive ? 1f : 0.55f, 0f), new GradientAlphaKey(additive ? 0.9f : 0.4f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Art.Particle(Soft(64, additive ? 2.2f : 1.2f), additive);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>A burst of glowing sparkles in <paramref name="color"/>.</summary>
        public static void Burst(Vector3 position, Color color, int count = 10, float speed = 4f)
        {
            if (sparkles == null) sparkles = Make("FX Sparkles", true, 0.6f);
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            var bright = new Color(Mathf.Min(1f, color.r * 1.3f + 0.15f), Mathf.Min(1f, color.g * 1.3f + 0.15f), Mathf.Min(1f, color.b * 1.3f + 0.15f), 1f);
            for (int i = 0; i < count; i++)
            {
                p.position = position + Random.insideUnitSphere * 0.15f;
                p.velocity = Random.onUnitSphere * speed * Random.Range(0.4f, 1f) + Vector3.up * speed * 0.5f;
                p.startSize = Random.Range(0.12f, 0.3f);
                p.startLifetime = Random.Range(0.35f, 0.75f);
                p.startColor = Color.Lerp(color, bright, Random.value);
                sparkles.Emit(p, 1);
            }
            // A soft flash in the middle.
            p.position = position;
            p.velocity = Vector3.zero;
            p.startSize = 0.5f + count * 0.04f;
            p.startLifetime = 0.25f;
            p.startColor = new Color(bright.r, bright.g, bright.b, 0.8f);
            sparkles.Emit(p, 1);
        }

        /// <summary>Soft dust puffs at the feet (landings, running, ground pounds).</summary>
        public static void Dust(Vector3 position, int count = 6, float spread = 1f)
        {
            if (dust == null) dust = Make("FX Dust", false, -0.05f);
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                p.position = position + dir * Random.Range(0.1f, 0.4f) * spread + Vector3.up * 0.1f;
                p.velocity = dir * Random.Range(0.6f, 1.8f) * spread + Vector3.up * Random.Range(0.2f, 0.7f);
                p.startSize = Random.Range(0.35f, 0.7f) * spread;
                p.startLifetime = Random.Range(0.45f, 0.8f);
                p.startColor = new Color(0.92f, 0.88f, 0.8f, 1f);
                dust.Emit(p, 1);
            }
        }
    }
}
