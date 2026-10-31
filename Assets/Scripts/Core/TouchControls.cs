using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// On-screen controls for phones: a floating joystick on the left half, jump (A),
    /// pound/dive (B) and punch (C) buttons on the right, camera drag on the free right side, a pause button,
    /// and respawn / new-game buttons on the pause screen. Feeds <see cref="Controls"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class TouchControls : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public static Vector2 Move { get; private set; }
        public static Vector2 Look { get; private set; }
        public static bool ADown { get; private set; }
        public static bool AHeld { get; private set; }
        public static bool BDown { get; private set; }
        public static bool BHeld { get; private set; }
        /// <summary>Two-finger pinch on the camera side: positive zooms in.</summary>
        public static float Pinch { get; private set; }
        public static bool PauseTap { get; private set; }
        public static bool AnyTap { get; private set; }
        public static bool RespawnTap { get; private set; }
        public static bool NewGameTap { get; private set; }

        const int None = -1;
        int stickFinger = None, lookFinger = None, aFinger = None, bFinger = None, pinchFinger = None;
        float pinchDistance;
        Vector2 stickOrigin, stickPos;
        Texture2D circle, ring;
        GUIStyle label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
            Move = Look = Vector2.zero;
            Pinch = 0f;
            ADown = AHeld = BDown = BHeld = PauseTap = AnyTap = RespawnTap = NewGameTap = false;
        }

        public static void Enable(GameObject host)
        {
            if (!(Application.isMobilePlatform || Input.touchSupported && Application.platform == RuntimePlatform.Android)) return;
            Active = true;
            Input.simulateMouseWithTouches = false; // touches must not rotate the mouse camera
            Input.multiTouchEnabled = true;
            host.AddComponent<TouchControls>();
        }

        // Layout in screen pixels (origin bottom-left, like Input.touches).
        float S => Screen.height / 1080f;
        // Button size and side come from the settings (left-handed: buttons on the left, stick on the right).
        float K => GameSettings.ButtonScales[GameSettings.ButtonSize];
        float Side(float fromRight) => GameSettings.LeftHanded ? fromRight : Screen.width - fromRight;
        Vector2 APos => new Vector2(Side(175f * S * K), 200f * S * K);
        Vector2 BPos => new Vector2(Side(385f * S * K), 120f * S * K);
        Vector2 PausePos => new Vector2(Screen.width * 0.5f, Screen.height - 70f * S);
        Vector2 RespawnPos => new Vector2(Screen.width * 0.5f - 300f * S, 170f * S);
        Vector2 NewGamePos => new Vector2(Screen.width * 0.5f + 300f * S, 170f * S);
        float ARadius => 105f * S * K;
        float BRadius => 88f * S * K;
        float StickRadius => 120f * S;

        bool Inside(Vector2 p, Vector2 c, float r) => (p - c).sqrMagnitude < r * r * 1.3f;

        void Update()
        {
            Pinch = 0f;
            ADown = BDown = PauseTap = AnyTap = RespawnTap = NewGameTap = false;
            var look = Vector2.zero;
            var gm = GameManager.I;
            bool playing = gm != null && gm.State == GameState.Playing;
            bool paused = gm != null && gm.State == GameState.Paused;

            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                var p = t.position;
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        if (!playing) { AnyTap = true; break; }
                        if (Inside(p, PausePos, 60f * S)) { PauseTap = true; break; }
                        if (Inside(p, APos, ARadius)) { aFinger = t.fingerId; ADown = true; break; }
                        if (Inside(p, BPos, BRadius)) { bFinger = t.fingerId; BDown = true; break; }
                        bool stickSide = GameSettings.LeftHanded ? p.x > Screen.width * 0.55f : p.x < Screen.width * 0.45f;
                        if (stickSide && stickFinger == None)
                        {
                            stickFinger = t.fingerId;
                            stickOrigin = p;
                            stickPos = p;
                        }
                        else if (lookFinger == None)
                        {
                            lookFinger = t.fingerId;
                        }
                        else if (pinchFinger == None)
                        {
                            pinchFinger = t.fingerId; // second finger on the camera side: pinch zoom
                            pinchDistance = -1f;
                        }
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (t.fingerId == stickFinger) stickPos = p;
                        else if (t.fingerId == lookFinger) look += t.deltaPosition / S * 0.22f;
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (t.fingerId == stickFinger) stickFinger = None;
                        if (t.fingerId == lookFinger) lookFinger = None;
                        if (t.fingerId == aFinger) aFinger = None;
                        if (t.fingerId == bFinger) bFinger = None;
                        if (t.fingerId == pinchFinger) pinchFinger = None;
                        break;
                }
            }

            if (lookFinger != None && pinchFinger != None)
            {
                Vector2 a = Vector2.zero, b = Vector2.zero;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.fingerId == lookFinger) a = t.position;
                    else if (t.fingerId == pinchFinger) b = t.position;
                }
                float d = Vector2.Distance(a, b) / S;
                if (pinchDistance > 0f) Pinch = (d - pinchDistance) * 0.02f;
                pinchDistance = d;
                look = Vector2.zero; // zooming, not turning
            }

            AHeld = aFinger != None;
            BHeld = bFinger != None;
            Look = look;
            Move = stickFinger != None ? Vector2.ClampMagnitude((stickPos - stickOrigin) / StickRadius, 1f) : Vector2.zero;
            if (!playing)
            {
                stickFinger = lookFinger = aFinger = bFinger = pinchFinger = None;
                Move = Vector2.zero;
            }
        }

        // ---------- drawing ----------

        void EnsureTextures()
        {
            if (circle != null) return;
            circle = Disc(128, false);
            ring = Disc(128, true);
            label = new GUIStyle
            {
                font = GameFont.Get(out var fontStyle),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = fontStyle,
                normal = { textColor = Color.white },
            };
        }

        static Texture2D Disc(int n, bool hollow)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01((1f - d) * n * 0.5f);
                if (hollow) a *= Mathf.Clamp01((d - 0.82f) * n * 0.5f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            return t;
        }

        Hud hud;

        void Circle(Vector2 screenCenter, float radius, Color fill, string text, int size)
        {
            hud.TouchCircle(screenCenter, radius, fill, circle, ring, text, size);
        }

        /// <summary>Called by the Hud each frame: draws the buttons into its canvas.</summary>
        public void DrawUi(Hud target)
        {
            var gm = GameManager.I;
            if (gm == null) return;
            hud = target;
            EnsureTextures();
            float s = S;

            if (gm.State != GameState.Playing) return;

            Circle(APos, ARadius, new Color(0.25f, 0.75f, 0.35f, AHeld ? 0.75f : 0.45f), "קפיצה", (int)(34 * s));
            Circle(BPos, BRadius, new Color(0.9f, 0.45f, 0.2f, BHeld ? 0.75f : 0.45f), "מכה / הלמות", (int)(25 * s));
            Circle(PausePos, 48f * s, new Color(0f, 0f, 0f, 0.35f), "II", (int)(34 * s));

            if (stickFinger != None)
            {
                Circle(stickOrigin, StickRadius, new Color(1f, 1f, 1f, 0.15f), null, 0);
                Circle(stickOrigin + Vector2.ClampMagnitude(stickPos - stickOrigin, StickRadius), 55f * s, new Color(1f, 1f, 1f, 0.55f), null, 0);
            }
            else
            {
                var hint = new Vector2(GameSettings.LeftHanded ? Screen.width - 260f * s : 260f * s, 230f * s);
                Circle(hint, StickRadius, new Color(1f, 1f, 1f, 0.08f), null, 0);
                Circle(hint, 55f * s, new Color(1f, 1f, 1f, 0.3f), null, 0);
            }
        }
    }
}
