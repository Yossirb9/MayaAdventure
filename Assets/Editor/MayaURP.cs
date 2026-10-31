using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MayaAdventure.EditorTools
{
    /// <summary>
    /// Moves the project to URP: one pipeline asset per quality level (Low / Normal / High, matching the
    /// in-game graphics setting), SSAO on High, light layers for Maya's fill light, and re-imports the
    /// glTF models so their materials use the URP shader graphs.
    /// Batch: -executeMethod MayaAdventure.EditorTools.MayaURP.Setup -quit
    /// </summary>
    public static class MayaURP
    {
        static readonly string[] Levels = { "Low", "Normal", "High" };

        [MenuItem("Maya/Setup URP")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Settings");
            var assets = new UniversalRenderPipelineAsset[Levels.Length];
            for (int i = 0; i < Levels.Length; i++)
            {
                string path = $"Assets/Settings/URP-{Levels[i]}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null)
                {
                    var create = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
                    var data = (ScriptableRendererData)create.Invoke(null, new object[] { path, RendererType.UniversalRenderer, true, "Renderer" });
                    asset = UniversalRenderPipelineAsset.Create(data);
                    AssetDatabase.CreateAsset(asset, path);
                }
                Configure(asset, i);
                assets[i] = asset;
            }
            AddSsao(assets[2]);
            AssetDatabase.SaveAssets();

            SetupQualityLevels(assets);
            GraphicsSettings.defaultRenderPipeline = assets[2];
            DropBuiltInShaders();
            AssetDatabase.SaveAssets();

            // Re-import the glTF models so their materials are generated for URP.
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Resources" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".glb") || p.EndsWith(".gltf")) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Maya] URP setup done.");
        }

        /// <summary>Every shader the game looks up by name at runtime (Shader.Find only sees shaders that are in the build).</summary>
        public static readonly string[] RuntimeShaders =
        {
            "Universal Render Pipeline/Lit", "Maya/Particle", "Maya/Water", "Maya/Sky", "Maya/Character",
            "TextMeshPro/Mobile/Distance Field", "TextMeshPro/Distance Field",
        };

        /// <summary>Adds <see cref="RuntimeShaders"/> to Graphics Settings > Always Included Shaders.</summary>
        public static void EnsureRuntimeShaders()
        {
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in RuntimeShaders)
            {
                var shader = Shader.Find(name);
                if (shader == null) { Debug.LogError("[Maya] Shader not found: " + name); continue; }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                Debug.Log("[Maya] Always including " + name);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Bakes the UI font (Secular One + every character the game uses) into a static TextMeshPro atlas, so phones
        /// never build glyphs at runtime (a dynamic atlas dropped letters like yod and vav on Android).
        /// </summary>
        [MenuItem("Maya/Bake UI Font")]
        public static void BakeTmpFont()
        {
            const string path = "Assets/Resources/Fonts/SecularOne SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/SecularOne.ttf");
            if (font == null) { Debug.LogError("[Maya] SecularOne.ttf missing"); return; }
            // Every character in the scripts, plus the fixed set (digits, punctuation).
            var chars = new System.Collections.Generic.SortedSet<char>(GameFont.Charset);
            foreach (var file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
                foreach (char ch in File.ReadAllText(file))
                    if (ch >= 0x590 && ch <= 0x5FF) chars.Add(ch);
            var text = new string(new System.Collections.Generic.List<char>(chars).ToArray());

            AssetDatabase.DeleteAsset(path);
            var fa = TMPro.TMP_FontAsset.CreateFontAsset(font, 64, 7, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                                                         TMPro.AtlasPopulationMode.Dynamic, false);
            fa.name = "SecularOne SDF";
            fa.TryAddCharacters(text, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[Maya] font lacks: " + missing);
            fa.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;
            // Strings are drawn in visual (reversed) order, so the font's right-to-left pair kerning and ligatures
            // would hit the wrong letter pairs and pile letters on top of each other. Drop them.
            fa.fontFeatureTable?.glyphPairAdjustmentRecords?.Clear();
            fa.fontFeatureTable?.MarkToBaseAdjustmentRecords?.Clear();
            fa.fontFeatureTable?.MarkToMarkAdjustmentRecords?.Clear();
            fa.fontFeatureTable?.ligatureRecords?.Clear();
            fa.fontFeatureTable?.multipleSubstitutionRecords?.Clear();
            AssetDatabase.CreateAsset(fa, path);
            fa.atlasTexture.name = "SecularOne SDF Atlas";
            fa.material.name = "SecularOne SDF Material";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Maya] Baked UI font: {fa.characterTable.Count} characters");
        }

        /// <summary>TextMeshPro's shaders and settings (the "TMP Essential Resources" package that ships with uGUI).</summary>
        [MenuItem("Maya/Import TMP Essentials")]
        public static void ImportTmp()
        {
            if (!Directory.Exists("Assets/TextMesh Pro"))
            {
                var pkg = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                if (pkg.Length > 0) AssetDatabase.ImportPackage(pkg[0], false);
                AssetDatabase.Refresh();
            }
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "TextMeshPro/Mobile/Distance Field", "TextMeshPro/Distance Field" })
            {
                var shader = Shader.Find(name);
                if (shader == null) { Debug.LogWarning("[Maya] missing " + name); continue; }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[Maya] TMP ready.");
        }

        /// <summary>Characters and the platformer kit keep legacy Animation clips (the game drives them by name).</summary>
        [MenuItem("Maya/Configure Models")]
        public static void ConfigureModels()
        {
            AssetDatabase.Refresh();
            // Texture files next to the glTF models: normal maps and data maps need the right importer settings,
            // and phones get them compressed (glTFast references these assets instead of embedding raw copies).
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Nature", "Assets/Resources/Characters", "Assets/Resources/Platformer" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                bool normal = n == "normal" || n.EndsWith("_normal"); // not "Leaves_NormalTree"!
                bool data = n.Contains("metallic") || n.Contains("roughness") || n.Contains("occlusion");
                bool changed = false;
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != type) { ti.textureType = type; changed = true; }
                if (data && ti.sRGBTexture) { ti.sRGBTexture = false; changed = true; }
                int max = p.Contains("/Characters/") ? 2048 : 1024;
                if (ti.maxTextureSize != max) { ti.maxTextureSize = max; changed = true; }
                var android = ti.GetPlatformTextureSettings("Android");
                if (!android.overridden || android.maxTextureSize != max / 2 * (p.Contains("/Characters/") ? 2 : 1))
                {
                    android.overridden = true;
                    android.maxTextureSize = p.Contains("/Characters/") ? max : max;
                    android.format = TextureImporterFormat.ASTC_6x6;
                    ti.SetPlatformTextureSettings(android);
                    changed = true;
                }
                if (changed) ti.SaveAndReimport();
            }
            // Re-import every model now that its textures exist as compressed assets.
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Resources/Nature", "Assets/Resources/Characters", "Assets/Resources/Platformer" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".gltf") || p.EndsWith(".glb")) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            }
            foreach (var folder in new[] { "Assets/Resources/Characters", "Assets/Resources/Platformer" })
                foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (!p.EndsWith(".glb") && !p.EndsWith(".gltf")) continue;
                    var importer = AssetImporter.GetAtPath(p);
                    if (importer == null) continue;
                    var so = new SerializedObject(importer);
                    var method = so.FindProperty("importSettings.animationMethod");
                    if (method == null || method.intValue == 1) continue;
                    method.intValue = 1; // Legacy
                    so.ApplyModifiedPropertiesWithoutUndo();
                    importer.SaveAndReimport();
                }
            Debug.Log("[Maya] Models configured.");
        }

        static void Configure(UniversalRenderPipelineAsset a, int level)
        {
            a.renderScale = level == 0 ? 0.6f : level == 1 ? 0.8f : 1f;
            a.msaaSampleCount = level == 0 ? 1 : level == 1 ? 2 : 4;
            a.supportsHDR = level > 0;
            a.supportsCameraDepthTexture = true; // water depth tint and shoreline foam
            a.supportsCameraOpaqueTexture = false;
            a.shadowDistance = level == 0 ? 30f : level == 1 ? 55f : 95f;
            a.shadowCascadeCount = level == 0 ? 1 : level == 1 ? 2 : 4;
            a.mainLightShadowmapResolution = level == 0 ? 1024 : level == 1 ? 2048 : 4096;
            a.maxAdditionalLightsCount = level == 0 ? 2 : 6;
            // Lower-resolution shadow maps need more bias, or flat surfaces get striped "shadow acne".
            a.shadowDepthBias = level == 0 ? 2f : level == 1 ? 1.6f : 1.2f;
            a.shadowNormalBias = level == 0 ? 1.6f : level == 1 ? 1.3f : 1f;

            var so = new SerializedObject(a);
            Set(so, "m_SoftShadowsSupported", level > 0);
            Set(so, "m_SupportsLightLayers", true);           // Maya's fill light uses a rendering layer
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_MainLightShadowsSupported", true);
            var mode = so.FindProperty("m_AdditionalLightsRenderingMode");
            if (mode != null) mode.intValue = (int)LightRenderingMode.PerPixel;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a);
        }

        static void Set(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
            else Debug.LogWarning($"[Maya] URP asset has no {name}");
        }

        static void AddSsao(UniversalRenderPipelineAsset asset)
        {
            var data = asset.rendererDataList[0];
            foreach (var f in data.rendererFeatures)
                if (f is ScreenSpaceAmbientOcclusion) return;
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ssao, data);
            data.rendererFeatures.Add(ssao);
            // Keep the renderer's feature id map in sync (normally done by the inspector).
            var validate = typeof(ScriptableRendererData).GetMethod("ValidateRendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            validate?.Invoke(data, null);
            data.SetDirty();
            EditorUtility.SetDirty(data);
        }

        /// <summary>Re-applies <see cref="Configure"/> to the existing pipeline assets (no re-imports).</summary>
        [MenuItem("Maya/Update Quality Assets")]
        public static void UpdateQualityAssets()
        {
            for (int i = 0; i < Levels.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"Assets/Settings/URP-{Levels[i]}.asset");
                if (asset != null) Configure(asset, i);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Maya] Quality assets updated.");
        }

        /// <summary>Removes the built-in pipeline shaders the old setup forced into every build.</summary>
        static void DropBuiltInShaders()
        {
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var shader = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                string n = shader != null ? shader.name : "";
                if (shader == null || n == "Standard" || n.StartsWith("glTF/") || n.StartsWith("Legacy Shaders/Particles") || n == "Skybox/Procedural")
                    list.DeleteArrayElementAtIndex(i);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetupQualityLevels(UniversalRenderPipelineAsset[] assets)
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qs);
            var list = so.FindProperty("m_QualitySettings");
            while (list.arraySize < assets.Length) list.InsertArrayElementAtIndex(list.arraySize);
            while (list.arraySize > assets.Length) list.DeleteArrayElementAtIndex(list.arraySize - 1);
            for (int i = 0; i < assets.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = Levels[i];
                e.FindPropertyRelative("customRenderPipeline").objectReferenceValue = assets[i];
                var vsync = e.FindPropertyRelative("vSyncCount");
                if (vsync != null) vsync.intValue = 0;
            }
            so.FindProperty("m_CurrentQuality").intValue = 2;
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null)
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var pair = perPlatform.GetArrayElementAtIndex(i);
                    string platform = pair.FindPropertyRelative("first").stringValue;
                    pair.FindPropertyRelative("second").intValue = platform == "Android" || platform == "iPhone" ? 1 : 2;
                }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
