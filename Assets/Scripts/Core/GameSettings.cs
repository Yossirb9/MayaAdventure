using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Player options from the settings menu, stored in PlayerPrefs.</summary>
    public static class GameSettings
    {
        public static bool Sound = true, Music = true;
        /// <summary>0 = light (best for older phones), 1 = normal, 2 = high.</summary>
        public static int Quality = 1;
        /// <summary>0 = close, 1 = medium, 2 = far.</summary>
        public static int CameraZoom = 0;

        public static readonly string[] QualityNames = { "חסכונית", "רגילה", "גבוהה" };
        public static readonly string[] ZoomNames = { "קרובה", "בינונית", "רחוקה" };
        public static readonly float[] ZoomDistances = { 4.6f, 6.2f, 8f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Load();

        public static void Load()
        {
            Sound = PlayerPrefs.GetInt("maya_sound", 1) == 1;
            Music = PlayerPrefs.GetInt("maya_music", 1) == 1;
            Quality = Mathf.Clamp(PlayerPrefs.GetInt("maya_quality", Application.isMobilePlatform ? 1 : 2), 0, 2);
            CameraZoom = Mathf.Clamp(PlayerPrefs.GetInt("maya_zoom", 0), 0, 2);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("maya_sound", Sound ? 1 : 0);
            PlayerPrefs.SetInt("maya_music", Music ? 1 : 0);
            PlayerPrefs.SetInt("maya_quality", Quality);
            PlayerPrefs.SetInt("maya_zoom", CameraZoom);
            PlayerPrefs.Save();
        }
    }
}
