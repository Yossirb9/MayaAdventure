using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Lighting, sky, fog, water and colour grading for one level.</summary>
    public class LevelTheme
    {
        public Vector3 SunEuler = new Vector3(48f, -35f, 0f);
        public Color SunColor = new Color(1f, 0.93f, 0.82f);
        public float SunIntensity = 1.35f;
        public float ShadowStrength = 0.82f;

        public Color SkyTop = new Color(0.2f, 0.48f, 0.95f);
        public Color SkyHorizon = new Color(0.72f, 0.88f, 1f);
        public Color SkyBottom = new Color(0.45f, 0.68f, 0.88f);
        public Color SunDisk = new Color(1f, 0.92f, 0.75f);
        public Color CloudLight = Color.white;
        public Color CloudShade = new Color(0.7f, 0.77f, 0.9f);
        public float CloudAmount = 0.45f;
        public float NightStars;

        public Color FogColor = new Color(0.68f, 0.84f, 0.98f);
        public float FogStart = 70f, FogEnd = 320f;

        public Color AmbientSky = new Color(0.56f, 0.68f, 0.92f);
        public Color AmbientEquator = new Color(0.5f, 0.56f, 0.55f);
        public Color AmbientGround = new Color(0.32f, 0.3f, 0.26f);

        public Color WaterShallow = new Color(0.22f, 0.9f, 0.85f, 0.5f);
        public Color WaterDeep = new Color(0.02f, 0.28f, 0.55f, 0.93f);
        public Color UnderwaterFog = new Color(0.08f, 0.38f, 0.52f);

        public float Exposure = 0.95f, Saturation = 1.1f, Contrast = 1.08f, Bloom = 0.45f, Vignette = 0.32f;
        public Color Tint = Color.white;

        public static LevelTheme Sunny() => new LevelTheme();

        public static LevelTheme Snow() => new LevelTheme
        {
            SunEuler = new Vector3(32f, 150f, 0f),
            SunColor = new Color(1f, 0.95f, 0.9f),
            SunIntensity = 1.2f,
            SkyTop = new Color(0.22f, 0.45f, 0.9f),
            SkyHorizon = new Color(0.8f, 0.88f, 0.98f),
            SkyBottom = new Color(0.75f, 0.82f, 0.92f),
            CloudAmount = 0.6f,
            CloudShade = new Color(0.72f, 0.76f, 0.86f),
            FogColor = new Color(0.82f, 0.87f, 0.96f),
            FogStart = 60f, FogEnd = 300f,
            AmbientSky = new Color(0.66f, 0.74f, 0.95f),
            AmbientEquator = new Color(0.62f, 0.66f, 0.74f),
            AmbientGround = new Color(0.55f, 0.57f, 0.62f),
            WaterShallow = new Color(0.55f, 0.85f, 0.95f, 0.55f),
            WaterDeep = new Color(0.08f, 0.25f, 0.45f, 0.95f),
            UnderwaterFog = new Color(0.2f, 0.4f, 0.55f),
            Exposure = 0.92f, Saturation = 1.1f, Contrast = 1.1f, Tint = new Color(0.97f, 0.99f, 1.04f),
        };

        public static LevelTheme Lagoon() => new LevelTheme
        {
            SunEuler = new Vector3(58f, 20f, 0f),
            SunColor = new Color(1f, 0.96f, 0.86f),
            SunIntensity = 1.45f,
            SkyTop = new Color(0.1f, 0.45f, 0.95f),
            SkyHorizon = new Color(0.65f, 0.92f, 1f),
            CloudAmount = 0.35f,
            FogColor = new Color(0.6f, 0.88f, 0.98f),
            FogStart = 80f, FogEnd = 360f,
            WaterShallow = new Color(0.25f, 0.98f, 0.85f, 0.42f),
            WaterDeep = new Color(0.0f, 0.35f, 0.6f, 0.9f),
            UnderwaterFog = new Color(0.05f, 0.45f, 0.55f),
            Saturation = 1.2f,
        };

        public static LevelTheme Sky() => new LevelTheme
        {
            SunEuler = new Vector3(40f, -120f, 0f),
            SunColor = new Color(1f, 0.9f, 0.78f),
            SunIntensity = 1.4f,
            SkyTop = new Color(0.12f, 0.32f, 0.85f),
            SkyHorizon = new Color(0.95f, 0.85f, 0.78f),
            SkyBottom = new Color(0.9f, 0.85f, 0.88f),
            CloudAmount = 0.62f,
            CloudShade = new Color(0.85f, 0.75f, 0.8f),
            FogColor = new Color(0.92f, 0.86f, 0.86f),
            FogStart = 60f, FogEnd = 260f,
            AmbientSky = new Color(0.6f, 0.62f, 0.9f),
            AmbientEquator = new Color(0.75f, 0.62f, 0.6f),
            AmbientGround = new Color(0.45f, 0.4f, 0.45f),
            Tint = new Color(1.03f, 1f, 0.98f),
        };

        public static LevelTheme Dusk() => new LevelTheme
        {
            SunEuler = new Vector3(16f, -60f, 0f),
            SunColor = new Color(1f, 0.62f, 0.42f),
            SunIntensity = 1.5f,
            ShadowStrength = 0.7f,
            SkyTop = new Color(0.12f, 0.1f, 0.35f),
            SkyHorizon = new Color(0.98f, 0.5f, 0.38f),
            SkyBottom = new Color(0.35f, 0.2f, 0.35f),
            SunDisk = new Color(1f, 0.6f, 0.35f),
            CloudLight = new Color(1f, 0.65f, 0.5f),
            CloudShade = new Color(0.35f, 0.25f, 0.45f),
            CloudAmount = 0.4f,
            NightStars = 0.7f,
            FogColor = new Color(0.5f, 0.32f, 0.48f),
            FogStart = 30f, FogEnd = 180f,
            AmbientSky = new Color(0.62f, 0.52f, 0.9f),
            AmbientEquator = new Color(0.72f, 0.5f, 0.58f),
            AmbientGround = new Color(0.34f, 0.28f, 0.38f),
            WaterShallow = new Color(0.4f, 0.5f, 0.85f, 0.55f),
            WaterDeep = new Color(0.05f, 0.08f, 0.3f, 0.95f),
            UnderwaterFog = new Color(0.1f, 0.12f, 0.3f),
            Exposure = 1.2f, Bloom = 0.55f, Saturation = 1.15f, Tint = new Color(1.02f, 0.98f, 1.04f),
        };
    }
}
