using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Short phone vibrations (Handheld.Vibrate is far too long for a hit, so Android's Vibrator is called directly).</summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static bool tried;
#endif
#if UNITY_ANDROID
        // Never called: referencing Handheld.Vibrate makes Unity add the VIBRATE permission to the manifest.
        static void NeedsVibratePermission() => Handheld.Vibrate();
#endif

        public static void Pulse(int milliseconds)
        {
            if (!GameSettings.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!tried)
                {
                    tried = true;
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                vibrator?.Call("vibrate", (long)milliseconds);
            }
            catch (System.Exception)
            {
                vibrator = null; // no vibrator or no permission: stay quiet
            }
#endif
        }
    }
}
