#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Editor-only trailer recorder: plays the real game from a script (scripted input, invincible Maya), moves a
    /// cinematic camera, and writes every frame at a fixed 30 fps to Builds/trailer/frames plus a log of every sound
    /// effect, so the soundtrack can be rebuilt in sync. Started by MayaAdventure.EditorTools.TrailerRecorder.
    /// </summary>
    [DefaultExecutionOrder(-3000)]
    public class TrailerDirector : MonoBehaviour
    {
        public const int Fps = 30, Width = 1920, Height = 1080;
        public static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/trailer"));

        GameManager gm;
        Camera cam;
        ThirdPersonCamera tpc;
        Canvas canvas;
        RenderTexture rt;
        Texture2D grab;
        public int Frame { get; private set; }
        public bool Done { get; private set; }

        // Scripted input (applied at the start of each frame, before Maya reads it).
        bool pJump, pPound, pPunch, jumpHold, poundHold;
        Vector3 move;

        // Camera
        enum CamMode { Game, Follow, Orbit, Fixed }
        CamMode mode = CamMode.Game;
        float camYaw, camPitch = 14f, camDist = 6f, camHeight = 1.1f, orbitSpeed;
        Vector3 fixedPos, smoothPos, smoothLook, focusOffset;
        Transform focus, duel;
        bool cut = true;
        bool recording = true; // false while a world loads off camera

        readonly StringBuilder events = new StringBuilder();
        readonly HashSet<AudioClip> clips = new HashSet<AudioClip>();

        MayaController P => gm.Player;

        void Start()
        {
            gm = GameManager.I;
            cam = Camera.main;
            tpc = cam.GetComponent<ThirdPersonCamera>();
            Directory.CreateDirectory(Path.Combine(OutDir, "frames"));
            foreach (var f in Directory.GetFiles(Path.Combine(OutDir, "frames"), "*.jpg")) File.Delete(f);
            rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            grab = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.enabled = false; // rendered by hand, once per frame
            canvas = gm.Hud.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            Time.captureFramerate = Fps;
            Sfx.Listener = (clip, volume) =>
            {
                if (!recording) return;
                clips.Add(clip);
                events.Append($"{Frame / (float)Fps:F3}\t{clip.name}\t{volume:F2}\n");
            };
            ScriptedInput.Active = true;
            gameObject.AddComponent<TrailerCapture>().Director = this;
            StartCoroutine(Script());
        }

        void Update()
        {
            ScriptedInput.JumpDown = pJump;
            ScriptedInput.PoundDown = pPound;
            ScriptedInput.PunchDown = pPunch;
            pJump = pPound = pPunch = false;
            ScriptedInput.JumpHeld = jumpHold;
            ScriptedInput.PoundHeld = poundHold;
            ScriptedInput.WorldMove = move;
        }

        void LateUpdate()
        {
            if (mode == CamMode.Game) return;
            float dt = Time.deltaTime;
            var target = focus != null ? focus.position + focusOffset : P.transform.position + Vector3.up * camHeight;
            float duelDist = camDist;
            if (duel != null)
            {
                // Duel: look side-on at the middle of the line between Maya and her opponent, so both stay in frame.
                Vector3 a = P.transform.position, b = duel.position;
                target = (a + b) * 0.5f + Vector3.up * camHeight;
                float line = YawTo(a, b), c1 = line + 90f, c2 = line - 90f;
                float want = Mathf.Abs(Mathf.DeltaAngle(camYaw, c1)) < Mathf.Abs(Mathf.DeltaAngle(camYaw, c2)) ? c1 : c2;
                camYaw = Mathf.MoveTowardsAngle(camYaw, want, 420f * dt);
                duelDist = camDist + Flat(a - b).magnitude * 0.55f;
            }
            Vector3 pos;
            if (mode == CamMode.Fixed) pos = fixedPos;
            else
            {
                if (mode == CamMode.Orbit) camYaw += orbitSpeed * dt;
                var back = Quaternion.Euler(camPitch, camYaw, 0f) * Vector3.back;
                float dist = duelDist;
                // Pull in in front of walls and trees, but not for enemies walking through the shot.
                foreach (var hit in Physics.SphereCastAll(target, 0.35f, back, dist, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    if (hit.distance > 0f && hit.collider.GetComponentInParent<Enemy>() == null && hit.collider.GetComponentInParent<MayaController>() == null)
                        dist = Mathf.Min(dist, Mathf.Max(1.2f, hit.distance - 0.2f));
                pos = target + back * dist;
            }
            if (cut)
            {
                smoothPos = pos;
                smoothLook = target;
                cut = false;
            }
            float k = 1f - Mathf.Exp(-6f * dt);
            smoothPos = Vector3.Lerp(smoothPos, pos, k);
            smoothLook = Vector3.Lerp(smoothLook, target, 1f - Mathf.Exp(-9f * dt));
            cam.transform.SetPositionAndRotation(smoothPos, Quaternion.LookRotation(smoothLook - smoothPos));
        }

        /// <summary>Called by <see cref="TrailerCapture"/> after every other LateUpdate.</summary>
        public void Capture()
        {
            if (!recording) return;
            canvas.planeDistance = cam.nearClipPlane + 0.05f;
            cam.Render();
            RenderTexture.active = rt;
            grab.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            grab.Apply(false);
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(OutDir, "frames", $"f{Frame:D5}.jpg"), grab.EncodeToJPG(92));
            Frame++;
        }

        // ---------- camera helpers ----------

        void GameCam()
        {
            mode = CamMode.Game;
            tpc.enabled = true;
        }

        void Follow(float yaw, float dist, float pitch, float height = 1.1f, bool hardCut = true)
        {
            tpc.enabled = false;
            mode = CamMode.Follow;
            focus = duel = null;
            camYaw = yaw;
            camDist = dist;
            camPitch = pitch;
            camHeight = height;
            cut = hardCut;
        }

        void Duel(Transform other, float yaw, float dist, float pitch, float height)
        {
            Follow(yaw, dist, pitch, height);
            duel = other;
        }

        void Orbit(float yaw, float dist, float pitch, float speed, Transform around = null, Vector3 offset = default)
        {
            Follow(yaw, dist, pitch);
            mode = CamMode.Orbit;
            orbitSpeed = speed;
            focus = around;
            focusOffset = offset;
        }

        void Fixed(Vector3 position)
        {
            tpc.enabled = false;
            mode = CamMode.Fixed;
            focus = null;
            fixedPos = position;
            cut = true;
        }

        // ---------- action helpers ----------

        static IEnumerator Wait(float s)
        {
            yield return new WaitForSeconds(s);
        }

        IEnumerator UntilGrounded(float max)
        {
            for (float t = 0f; t < max && !P.Grounded; t += Time.deltaTime) yield return null;
        }

        IEnumerator TripleJump(Vector3 dir, bool hold = false)
        {
            move = dir;
            yield return Wait(0.5f);
            for (int i = 0; i < 3; i++)
            {
                yield return UntilGrounded(2.5f);
                pJump = true;
                jumpHold = true;
                yield return Wait(i == 2 ? 0.45f : 0.25f);
                if (!hold || i < 2) jumpHold = false;
                yield return null;
            }
        }

        IEnumerator WalkTo(Vector3 point, float max)
        {
            for (float t = 0f; t < max; t += Time.deltaTime)
            {
                var d = point - P.transform.position;
                d.y = 0f;
                if (d.magnitude < 0.6f) break;
                move = d.normalized;
                yield return null;
            }
            move = Vector3.zero;
        }

        IEnumerator Enter(int level)
        {
            gm.EnterLevel(level);
            yield return Wait(1.15f);
        }

        static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        // ---------- the trailer: ~20 seconds, a taste of every world - island, sky, sea, king ----------

        void Place(Vector3 position, float yaw)
        {
            move = Vector3.zero;
            jumpHold = poundHold = false;
            P.Teleport(position, yaw, false);
        }

        /// <summary>Load a world off camera (no frames written), so worlds hard-cut into each other.</summary>
        IEnumerator CutTo(int level)
        {
            recording = false;
            move = Vector3.zero;
            jumpHold = poundHold = false;
            gm.EnterLevel(level);
            yield return Wait(1.25f);
            // Let the spawn guard (real-time) finish before we move Maya ourselves.
            yield return new WaitForSecondsRealtime(0.7f);
        }

        IEnumerator RunTo(Vector3 point, float within, float max)
        {
            for (float t = 0f; t < max; t += Time.deltaTime)
            {
                var d = point - P.transform.position;
                d.y = 0f;
                if (d.magnitude < within) break;
                move = d.normalized;
                yield return null;
            }
            move = Vector3.zero;
        }

        IEnumerator Script()
        {
            while (P == null || P.GetComponentInChildren<Animation>() == null) yield return null;
            recording = false;
            yield return Wait(0.4f);

            // 1. Title (1.6 s): Maya on the menu reacts to a tap.
            GameCam();
            recording = true;
            yield return Wait(0.3f);
            P.TitleReact();
            yield return Wait(1.3f);
            recording = false;
            gm.StartNewGame();
            yield return null;
            yield return new WaitForSecondsRealtime(0.7f);

            // 2. Sunny Island (3.5 s): a Grumble in the way - jump in and punch it away.
            var start = new Vector3(8f, 0f, -18.5f);
            var gate = new Vector3(-8f, 0f, -24f);
            var dir = (gate - start).normalized;
            Place(start, YawTo(start, gate));
            var grumble = Grumble.Build(LevelBuilder.root, start + dir * 5.2f);
            Follow(YawTo(start, gate) - 35f, 5.2f, 11f);
            yield return Wait(0.2f);
            recording = true;
            move = dir;
            yield return Wait(0.25f);
            pJump = true;
            for (float t = 0f; t < 2f && grumble != null && !grumble.Dead; t += Time.deltaTime)
            {
                if ((grumble.transform.position - P.transform.position).magnitude < 2.1f) break;
                yield return null;
            }
            move = Vector3.zero;
            Follow(YawTo(start, gate) + 75f, 4.4f, 8f, 1.1f, false);
            for (int i = 0; i < 3; i++)
            {
                if (grumble != null) P.transform.rotation = Quaternion.Euler(0f, YawTo(P.transform.position, grumble.transform.position), 0f);
                pPunch = true;
                yield return Wait(0.3f);
            }
            yield return Wait(0.5f);

            // 3. Sky Fortress (4 s): wing cap, triple jump, glide between the floating islands.
            yield return CutTo(3);
            Place(new Vector3(-1f, 0f, 2f), 90f);
            P.GivePower(PowerKind.Wing, 25f);
            Follow(-30f, 7f, 7f, 1.5f);
            yield return null;
            recording = true;
            yield return TripleJump(Vector3.right, true);
            yield return Wait(1.4f);
            jumpHold = false;
            P.GivePower(PowerKind.Wing, 0.01f);

            // 4. Coral Lagoon (4 s): dive down the coin trail to the sunken treasure.
            yield return CutTo(2);
            var treasure = new Vector3(18f, -9.4f, -6f);
            var dive = new Vector3(10f, -2.2f, -14f);
            Place(dive, YawTo(dive, treasure));
            Follow(YawTo(dive, treasure) - 20f, 4.6f, 16f, 0.8f);
            yield return Wait(0.3f);
            recording = true;
            poundHold = true;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                var d = treasure - P.transform.position;
                if (d.magnitude < 1.2f) break;
                poundHold = d.y < -0.5f;
                d.y = 0f;
                move = d.magnitude > 0.3f ? d.normalized : Vector3.zero;
                yield return null;
            }
            move = Vector3.zero;
            poundHold = false;
            yield return Wait(0.6f);

            // 5. Twilight Woods (6 s): King Grumble - jump his shockwave, then stomp him down to his last hit.
            yield return CutTo(4);
            var king = Enemy.All.OfType<KingGrumble>().First();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(KingGrumble).GetField("maxHp", flags).SetValue(king, 3);
            typeof(KingGrumble).GetField("hp", flags).SetValue(king, 2); // already hit once
            var kp = king.transform.position;
            var arena = kp + new Vector3(4.5f, 0f, -4.5f);
            Place(arena, YawTo(arena, kp));
            Duel(king.transform, YawTo(arena, kp) + 90f, 7.5f, 24f, 1.8f);
            yield return Wait(0.2f);
            recording = true;
            king.SlamNow();
            // Jump over the ring as it reaches her.
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                float r = king.WaveRadius;
                if (r > 0f && r >= Vector3.Distance(Flat(king.WaveCenter), Flat(P.transform.position)) - 2.2f) break;
                yield return null;
            }
            pJump = true;
            jumpHold = true;
            yield return Wait(0.35f);
            jumpHold = false;
            yield return UntilGrounded(1.5f);
            for (int hit = 0; hit < 2 && !king.Dead; hit++)
            {
                // Run in, jump, and steer onto his head; bounce off and come down again.
                int hpBefore = king.Hp;
                yield return RunTo(king.transform.position, 2.4f, 0.8f);
                pJump = true;
                jumpHold = true;
                for (float t = 0f; t < 1.4f && king.Hp == hpBefore && !king.Dead; t += Time.deltaTime)
                {
                    var d = king.transform.position - P.transform.position;
                    d.y = 0f;
                    move = d.magnitude > 0.2f ? d.normalized * Mathf.Clamp01(d.magnitude / 1.5f) : Vector3.zero;
                    yield return null;
                }
                jumpHold = false;
                move = Vector3.zero;
                if (king.Hp == hpBefore && !king.Dead)
                {
                    // Missed (he moved): drop onto him so the fight keeps its rhythm.
                    Place(king.transform.position + Vector3.up * 3.2f, P.transform.eulerAngles.y);
                    yield return Wait(0.5f);
                }
                yield return Wait(0.2f);
                yield return UntilGrounded(1.2f);
            }
            Orbit(P.transform.eulerAngles.y + 180f, 4.2f, 8f, 30f);
            yield return Wait(1.2f);

            // 6. End card on the title screen.
            yield return CutTo(0);
            gm.QuitToTitle();
            GameCam();
            yield return Wait(0.2f);
            recording = true;
            P.Celebrate();
            gm.Hud.Caption("מאיה ומסע הכוכבים", 2.6f, "5 עולמות · 25 כוכבים · עכשיו באנדרואיד");
            yield return Wait(2.6f);

            Finish();
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void Finish()
        {
            ScriptedInput.Active = false;
            Sfx.Listener = null;
            File.WriteAllText(Path.Combine(OutDir, "events.tsv"), events.ToString());
            var audioDir = Path.Combine(OutDir, "audio");
            Directory.CreateDirectory(audioDir);
            var music = gm.Sfx.MusicClip;
            if (music != null) clips.Add(music);
            foreach (var clip in clips) WriteWav(clip, Path.Combine(audioDir, clip.name + ".wav"));
            File.WriteAllText(Path.Combine(OutDir, "info.txt"), $"frames={Frame}\nfps={Fps}\nmusic={(music != null ? music.name : "")}\n");
            Done = true;
        }

        static void WriteWav(AudioClip clip, string path)
        {
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) return;
            using var w = new BinaryWriter(File.Create(path));
            int bytes = data.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + bytes);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)clip.channels);
            w.Write(clip.frequency);
            w.Write(clip.frequency * clip.channels * 2);
            w.Write((short)(clip.channels * 2));
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(bytes);
            foreach (var s in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }
}
#endif
