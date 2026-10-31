using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MayaAdventure
{
    /// <summary>
    /// Reads a file from StreamingAssets on every platform. On Android those files live inside the
    /// APK (a jar: URL), so they must be fetched with UnityWebRequest instead of File IO.
    /// </summary>
    public static class StreamingFile
    {
        public static string PathOf(string relative) => Path.Combine(Application.streamingAssetsPath, relative);

        /// <summary>Returns the bytes, or null if the file doesn't exist.</summary>
        public static async Task<byte[]> ReadAllBytes(string relative)
        {
            string path = PathOf(relative);
            if (!path.Contains("://"))
            {
                if (!File.Exists(path)) return null;
                return await Task.Run(() => File.ReadAllBytes(path));
            }

            using (var req = UnityWebRequest.Get(path))
            {
                var done = new TaskCompletionSource<bool>();
                req.SendWebRequest().completed += _ => done.TrySetResult(true);
                await done.Task;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Maya] Could not read {path}: {req.error}");
                    return null;
                }
                return req.downloadHandler.data;
            }
        }

        /// <summary>Base URI for glTF files so relative textures (Textures/colormap.png) resolve.</summary>
        public static Uri UriOf(string relative)
        {
            string path = PathOf(relative);
            return path.Contains("://") ? new Uri(path) : new Uri(Path.GetFullPath(path));
        }
    }
}
