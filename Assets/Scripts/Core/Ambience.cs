using UnityEngine;

namespace MayaAdventure
{
    public enum AmbienceKind { None, Motes, Snow, Fireflies }

    /// <summary>Camera-following ambient particles: floating pollen, snowfall or fireflies.</summary>
    public class Ambience : MonoBehaviour
    {
        ParticleSystem ps;
        ParticleSystemRenderer psr;
        AmbienceKind kind = AmbienceKind.None;
        Transform cam;
        float heightOffset;
        static Texture2D dot;

        public static Ambience Create(Transform camera)
        {
            var go = new GameObject("Ambience");
            var a = go.AddComponent<Ambience>();
            a.cam = camera;
            a.ps = go.AddComponent<ParticleSystem>();
            a.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            a.psr = go.GetComponent<ParticleSystemRenderer>();
            return a;
        }

        static Texture2D Dot()
        {
            if (dot != null) return dot;
            const int n = 32;
            dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                dot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            dot.Apply();
            return dot;
        }

        public void Set(AmbienceKind k)
        {
            kind = k;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (k == AmbienceKind.None) return;

            bool additive = k != AmbienceKind.Snow;
            var mat = Art.Particle(Dot(), additive);
            if (mat == null) return;
            psr.sharedMaterial = mat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;

            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;
            main.startSpeed = 0f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            var vel = ps.velocityOverLifetime;
            var noise = ps.noise;
            var emission = ps.emission;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;

            switch (k)
            {
                case AmbienceKind.Motes:
                    shape.scale = new Vector3(40f, 8f, 40f);
                    heightOffset = 1f;
                    emission.rateOverTime = 40f;
                    main.startLifetime = 7f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                    main.startColor = new Color(1f, 0.95f, 0.7f, 0.55f);
                    noise.enabled = true;
                    noise.strength = 0.35f;
                    noise.frequency = 0.25f;
                    vel.enabled = false;
                    break;
                case AmbienceKind.Snow:
                    shape.scale = new Vector3(60f, 2f, 60f);
                    heightOffset = 14f;
                    emission.rateOverTime = 420f;
                    main.startLifetime = 9f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                    main.startColor = new Color(1f, 1f, 1f, 0.9f);
                    vel.enabled = true;
                    vel.space = ParticleSystemSimulationSpace.World;
                    vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.6f);
                    vel.y = new ParticleSystem.MinMaxCurve(-2.2f, -1.4f);
                    vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                    noise.enabled = true;
                    noise.strength = 0.5f;
                    noise.frequency = 0.3f;
                    break;
                case AmbienceKind.Fireflies:
                    shape.scale = new Vector3(44f, 6f, 44f);
                    heightOffset = 0f;
                    emission.rateOverTime = 30f;
                    main.startLifetime = 8f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                    main.startColor = new Color(1f, 0.85f, 0.35f, 1f) * 3f;
                    noise.enabled = true;
                    noise.strength = 0.8f;
                    noise.frequency = 0.2f;
                    vel.enabled = false;
                    break;
            }
            ps.Play();
        }

        void LateUpdate()
        {
            if (cam == null || kind == AmbienceKind.None) return;
            var f = cam.forward;
            f.y = 0f;
            var p = cam.position + f.normalized * 10f;
            if (kind != AmbienceKind.Snow)
            {
                var gm = GameManager.I;
                if (gm != null && gm.Player != null) p.y = gm.Player.transform.position.y + 2f;
            }
            transform.position = p + Vector3.up * heightOffset;
        }
    }
}
