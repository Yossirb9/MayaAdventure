using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MayaAdventure.EditorTools
{
    /// <summary>
    /// Records the trailer: enters Play mode and lets <see cref="TrailerDirector"/> play the game, writing frames and a
    /// sound log to Builds/trailer (Tools~/make_trailer.py turns them into an MP4).
    /// Batch: -executeMethod MayaAdventure.EditorTools.TrailerRecorder.Run (without -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class TrailerRecorder
    {
        const string Flag = "MayaTrailerActive";
        static TrailerDirector director;
        static double startedAt;

        // Play mode reloads the scripts: re-attach after every reload while recording.
        static TrailerRecorder()
        {
            if (SessionState.GetBool(Flag, false)) EditorApplication.update += Tick;
        }

        [MenuItem("Maya/Record Trailer")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (startedAt == 0) startedAt = EditorApplication.timeSinceStartup;
            if (director == null)
            {
                if (GameManager.I == null || GameManager.I.Player == null) return;
                director = new GameObject("Trailer Director").AddComponent<TrailerDirector>();
                Debug.Log("[Maya] Recording trailer...");
                return;
            }
            if (director.Done || EditorApplication.timeSinceStartup - startedAt > 3600)
            {
                Debug.Log($"[Maya] Trailer recorded: {director.Frame} frames");
                SessionState.SetBool(Flag, false);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(director.Done ? 0 : 1);
            }
        }
    }
}
