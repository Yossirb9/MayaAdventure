using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MayaAdventure.EditorTools
{
    /// <summary>
    /// First-open setup: creates Assets/Scenes/Main.unity, registers it in Build Settings and
    /// adds the shaders the game creates at runtime to "Always Included Shaders" so builds render.
    /// Can be re-run from the menu: Maya > Setup Project.
    /// </summary>
    [InitializeOnLoad]
    static class MayaSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static MayaSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                UpgradeRendering();
                if (File.Exists(ScenePath)) return;
                Setup();
            };
        }

        [MenuItem("Maya/Setup Project")]
        public static void Setup()
        {
            if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Maya's Adventure";

            UpgradeRendering();
            Debug.Log("[Maya] Project ready. Press Play to start Maya's Adventure.");
        }

        /// <summary>Linear colour space + the shaders the game creates at runtime (for builds).</summary>
        static void UpgradeRendering()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("[Maya] Switched the project to Linear colour space.");
            }
            AddAlwaysIncludedShaders("Universal Render Pipeline/Lit", "Universal Render Pipeline/Particles/Unlit", "Maya/Water", "Maya/Sky");
        }

        static void AddAlwaysIncludedShaders(params string[] names)
        {
            var settings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            foreach (var name in names)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
