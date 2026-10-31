using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MayaAdventure
{
    /// <summary>
    /// Camera post-processing through a global URP Volume: bloom, ACES tone mapping, colour grading,
    /// vignette and an underwater tint. Keeps the old field names so level themes drive it unchanged.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public float Threshold = 1.0f;
        public float Bloom = 0.5f;
        public float Exposure = 1f;
        public float Saturation = 1.1f;
        public float Contrast = 1.05f;
        public float Vignette = 0.3f;
        public Color Tint = Color.white;
        public float Underwater;
        public Color UnderwaterColor = new Color(0.2f, 0.55f, 0.75f);

        Volume volume;
        VolumeProfile profile;
        Bloom bloom;
        ColorAdjustments color;
        Vignette vignette;

        void Awake()
        {
            var data = GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = profile.Add<Bloom>(true);
            bloom.highQualityFiltering.Override(false);
            bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            color = profile.Add<ColorAdjustments>(true);
            vignette = profile.Add<Vignette>(true);
            vignette.smoothness.Override(0.45f);

            var go = new GameObject("Post Volume");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        void LateUpdate()
        {
            if (profile == null) return;
            float u = Underwater;
            bloom.threshold.Override(Threshold);
            bloom.intensity.Override(Bloom * 1.3f);
            color.postExposure.Override(Mathf.Log(Mathf.Max(0.05f, Exposure), 2f));
            color.contrast.Override((Contrast - 1f) * 100f + u * 5f);
            color.saturation.Override((Saturation - 1f) * 100f - u * 15f);
            color.colorFilter.Override(Color.Lerp(Tint, UnderwaterColor * 1.6f, u * 0.8f));
            vignette.intensity.Override(Vignette + u * 0.15f);
        }

        void OnDestroy()
        {
            if (profile != null) Destroy(profile);
        }
    }
}
