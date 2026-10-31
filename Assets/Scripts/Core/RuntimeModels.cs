using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Loads animated GLB files from StreamingAssets once (legacy animation) and instantiates
    /// them as many times as needed. Used for the Kenney characters that play the enemies.
    /// </summary>
    public static class RuntimeModels
    {
        static readonly Dictionary<string, Task<GltfImport>> loads = new Dictionary<string, Task<GltfImport>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => loads.Clear();

        static Task<GltfImport> Load(string relativePath)
        {
            if (!loads.TryGetValue(relativePath, out var task))
                loads[relativePath] = task = LoadAsync(relativePath);
            return task;
        }

        static async Task<GltfImport> LoadAsync(string relativePath)
        {
            byte[] data = await StreamingFile.ReadAllBytes(relativePath);
            if (data == null)
            {
                Debug.LogWarning($"[Maya] Model not found: {StreamingFile.PathOf(relativePath)}");
                return null;
            }
            var gltf = new GltfImport();
            var settings = new ImportSettings { AnimationMethod = AnimationMethod.Legacy };
            return await gltf.Load(data, StreamingFile.UriOf(relativePath), settings) ? gltf : null;
        }

        /// <summary>Instantiates the model under <paramref name="parent"/>; returns its legacy Animation (may be null).</summary>
        public static async Task<Animation> Instantiate(string relativePath, Transform parent)
        {
            try
            {
                var gltf = await Load(relativePath);
                if (gltf == null || parent == null) return null;
                if (!await gltf.InstantiateMainSceneAsync(parent) || parent == null) return null;
                var anim = parent.GetComponentInChildren<Animation>(true);
                if (anim != null)
                {
                    anim.cullingType = AnimationCullingType.AlwaysAnimate;
                    foreach (AnimationState s in anim) s.wrapMode = WrapMode.Loop;
                }
                return anim;
            }
            catch (Exception e)
            {
                if (Application.isPlaying && parent != null) Debug.LogException(e);
                return null;
            }
        }
    }
}
