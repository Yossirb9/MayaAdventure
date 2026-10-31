using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MayaAdventure.EditorTools
{
    /// <summary>
    /// Batch-mode play test: enters every level, visits every star, fires every cannon and
    /// pokes every enemy, crate and pound target, then writes Builds/smoke.txt with all errors.
    /// Run with -executeMethod MayaAdventure.EditorTools.MayaSmokeTest.Run (without -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class MayaSmokeTest
    {
        const string Flag = "MayaSmokeTestActive";

        // Entering play mode reloads the scripts, so re-attach after every reload while the test runs.
        static MayaSmokeTest()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        static readonly StringBuilder report = new StringBuilder();
        static int errors;
        static IEnumerator<float> script;
        static double resumeAt;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors++;
                report.AppendLine($"[{type}] {msg}\n{stack}");
            }
            else if (type == LogType.Warning && msg.Contains("Maya")) report.AppendLine($"[Warning] {msg}");
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < resumeAt) return;
            if (script == null) script = Script().GetEnumerator();
            if (script.MoveNext()) resumeAt = EditorApplication.timeSinceStartup + script.Current;
            else Finish();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Flag, false);
            report.Insert(0, $"errors: {errors}\n");
            File.WriteAllText("Builds/smoke.txt", report.ToString());
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        static void Log(string s) => report.AppendLine(s);

        /// <summary>Renders the game camera (optionally from a given spot) to Builds/shots/name.png.</summary>
        static void Shot(string name, Vector3? from = null, Vector3? lookAt = null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var pos = cam.transform.position;
            var rot = cam.transform.rotation;
            if (from.HasValue) cam.transform.SetPositionAndRotation(from.Value, Quaternion.LookRotation(lookAt.Value - from.Value));
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            // Put the HUD canvas in front of the camera for the shot (overlay canvases never reach a render texture).
            var hudCanvas = Object.FindAnyObjectByType<Hud>()?.GetComponentInChildren<Canvas>();
            if (hudCanvas != null)
            {
                hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                hudCanvas.worldCamera = cam;
                hudCanvas.planeDistance = 0.5f;
            }
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            if (hudCanvas != null) hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            cam.transform.SetPositionAndRotation(pos, rot);
            Directory.CreateDirectory("Builds/shots");
            File.WriteAllBytes($"Builds/shots/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
        }

        static IEnumerable<float> Script()
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            for (int i = 0; i < 300 && (GameManager.I == null || GameManager.I.Player == null); i++) yield return 0.1f;
            var gm = GameManager.I;
            if (gm == null) { Log("no GameManager"); yield break; }
            yield return 4f; // let Maya's model load
            Shot("title");
            // Title: tap on Maya (react) and drag (spin).
            var tcam = Camera.main;
            var mid = tcam.WorldToScreenPoint(gm.Player.transform.position + Vector3.up * 0.9f);
            float yaw0 = gm.Player.transform.eulerAngles.y;
            gm.TitleTap(mid);
            yield return 0.3f;
            Shot("title_react");
            gm.TitleSpin(200f);
            yield return 0.2f;
            Log($"title: tap at {mid}, spin yaw {yaw0:F0} -> {gm.Player.transform.eulerAngles.y:F0}, maya y {gm.Player.transform.position.y:F2}");
            yield return 1.5f;
            var font = GameFont.Get(out var fontStyle);
            var missing = new StringBuilder();
            foreach (var path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
                foreach (char ch in File.ReadAllText(path))
                    if (ch >= 0x20 && ch != 0x7f && !font.HasCharacter(ch) && ch < 0x2000 && missing.ToString().IndexOf(ch) < 0 &&
                        (ch >= 0x590 && ch <= 0x5ff || char.IsLetterOrDigit(ch) || char.IsPunctuation(ch)))
                        missing.Append(ch);
            Log($"font: {font.name} style={fontStyle} dynamic={font.dynamic} missing=[{missing}]");

            // Shader.Find works for every shader in the editor, but on a phone only for shaders in the build.
            var included = new System.Collections.Generic.HashSet<Shader>();
            var gso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var always = gso.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < always.arraySize; i++)
                if (always.GetArrayElementAtIndex(i).objectReferenceValue is Shader sh) included.Add(sh);
            foreach (var name in MayaURP.RuntimeShaders)
            {
                var sh = Shader.Find(name);
                if (sh == null || !included.Contains(sh)) { errors++; Log($"NOT IN BUILD: shader {name}"); }
            }
            if (missing.Length > 0) errors++;
            for (int q = 0; q < 3; q++)
            {
                GameSettings.Quality = q;
                GameSettings.Sound = q != 1;
                gm.ApplySettings();
                yield return 0.3f;
            }
            Log($"title has progress: {gm.HasProgress}");
            // New game exactly like the menu button, then see where Maya stands.
            gm.StartNewGame();
            for (int i = 0; i < 6; i++)
            {
                yield return 0.5f;
                var mp = gm.Player.transform.position;
                Log($"new game t={i * 0.5f + 0.5f:F1}: maya {mp} grounded={gm.Player.Grounded} swimming={gm.Player.Swimming} state={gm.State} level={gm.LevelIndex}");
            }
            Shot("newgame");
            {
                // What ground is Maya standing on (object, material, shader)?
                var mp0 = gm.Player.transform.position + Vector3.up;
                foreach (var hit in Physics.RaycastAll(mp0, Vector3.down, 5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    var rend = hit.collider.GetComponent<Renderer>();
                    string mats = rend == null ? "-" : string.Join(", ", System.Array.ConvertAll(rend.sharedMaterials, m => m == null ? "null" : m.name + "/" + m.shader.name));
                    Log($"ground under maya: {hit.collider.name} at {hit.distance:F2}: {mats}");
                }
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (r.bounds.Contains(new Vector3(mp0.x, r.bounds.center.y, mp0.z)) && r.bounds.size.x > 20f)
                        Log($"big renderer: {r.name} {r.bounds.size} {string.Join(", ", System.Array.ConvertAll(r.sharedMaterials, m => m == null ? "null" : m.name + "/" + m.shader.name))}");
            }
            gm.ContinueGame();
            yield return 1f;
            Log($"after continue: state={gm.State} level={gm.LevelIndex}");

            // Progress must survive travelling to another world and back.
            {
                PowerStar first = null;
                foreach (var st in Object.FindObjectsByType<PowerStar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (st.Id.StartsWith("0-") && st.gameObject.activeInHierarchy && (first == null || string.CompareOrdinal(st.Id, first.Id) < 0)) first = st;
                string id = first.Id;
                gm.CollectStar(first);
                Coin red = null;
                foreach (var c in Object.FindObjectsByType<Coin>(FindObjectsSortMode.None)) if (c.Red) { red = c; break; }
                if (red != null)
                {
                    // Touch it like a player would, so it is remembered for this session.
                    gm.Player.Teleport(red.transform.position - Vector3.up * 0.85f, 0f, false);
                    yield return 0.3f;
                    gm.Player.Teleport(gm.Player.transform.position + Vector3.up * 0.2f, 0f, false);
                }
                int coins = gm.Coins, starsBefore = gm.Stars, redBefore = gm.RedCoins, lives = gm.Lives;
                int coinObjs = Object.FindObjectsByType<Coin>(FindObjectsSortMode.None).Length;
                yield return 0.5f;
                gm.EnterLevel(1);
                yield return 2.5f;
                gm.EnterLevel(0);
                yield return 2.5f;
                PowerStar again = null;
                foreach (var st in Object.FindObjectsByType<PowerStar>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (st.Id == id) again = st;
                Log($"travel 0->1->0: stars {starsBefore}->{gm.Stars}, coins {coins}->{gm.Coins}, lives {lives}->{gm.Lives}, red {redBefore}->{gm.RedCoins}, " +
                    $"coin objects {coinObjs}->{Object.FindObjectsByType<Coin>(FindObjectsSortMode.None).Length}, star {id} ghost={(again != null && again.AlreadyCollected)}, saved='{PlayerPrefs.GetString("maya_stars_v1")}'");
                if (gm.Stars != starsBefore || again == null || !again.AlreadyCollected || gm.RedCoins != redBefore || gm.Coins != coins ||
                    Object.FindObjectsByType<Coin>(FindObjectsSortMode.None).Length != coinObjs)
                { errors++; Log("PROGRESS LOST after travelling"); }
            }

            for (int k = 0; k < Levels.Count; k++)
            {
                gm.EnterLevel(k);
                yield return 4f;
                var info = (LevelInfo)typeof(GameManager).GetField("level", any).GetValue(gm);
                Shot($"L{k + 1}_view");
                Shot($"L{k + 1}_aerial", info.Start + new Vector3(0f, 22f, -30f), info.Start + new Vector3(0f, 0f, 14f));
                var stars = info.Root.GetComponentsInChildren<PowerStar>(true);
                Log($"music {(gm.Sfx.MusicClip != null ? gm.Sfx.MusicClip.name + " " + gm.Sfx.MusicClip.length.ToString("F1") + "s" : "none")}, voice {(gm.Sfx.SayYesh != null && gm.Sfx.SayWow != null && gm.Sfx.SayOuch != null && gm.Sfx.SayYay != null && gm.Sfx.SayHop != null)}, mission {(gm.Mission != null ? gm.Mission.Title : "-")}, guide {Object.FindAnyObjectByType<Npc>() != null}");
                Log($"\n== Level {k + 1} {info.Name}: stars={stars.Length} (expected {Levels.StarCounts[k]}) enemies={Enemy.All.Count} punchables={Punchable.All.Count} " +
                    $"pound={PoundTarget.All.Count} cannons={Object.FindObjectsByType<Cannon>(FindObjectsSortMode.None).Length} pipes={Object.FindObjectsByType<WarpPipe>(FindObjectsSortMode.None).Length} state={gm.State}");
                if (stars.Length != Levels.StarCounts[k]) { errors++; Log("STAR COUNT MISMATCH"); }

                // Fire every cannon and see where Maya lands.
                foreach (var cannon in Object.FindObjectsByType<Cannon>(FindObjectsSortMode.None))
                {
                    gm.Player.Teleport(cannon.transform.position + Vector3.up * 0.6f, 0f);
                    yield return 6f;
                    var target = (Vector3)typeof(Cannon).GetField("target", any).GetValue(cannon);
                    var p = gm.Player.transform.position;
                    Log($"cannon at {cannon.transform.position} -> landed {p} (target {target}) grounded={gm.Player.Grounded} dist={Vector3.Distance(p, target):F1}");
                }

                // Step on every purple switch: its blocks must appear.
                foreach (var sw in Object.FindObjectsByType<PurpleSwitch>(FindObjectsSortMode.None))
                {
                    gm.Player.Teleport(sw.transform.position + Vector3.up * 0.4f, 0f);
                    yield return 0.8f;
                    float left = (float)typeof(PurpleSwitch).GetField("left", any).GetValue(sw);
                    Log($"purple switch at {sw.transform.position}: maya {gm.Player.transform.position} active={left > 0f}");
                    if (left <= 0f) { errors++; Log("PURPLE SWITCH DID NOT FIRE"); }
                }

                // Visit every star.
                foreach (var s in stars)
                {
                    if (s == null) continue;
                    var sp = s.transform.position;
                    var ground = sp + new Vector3(0f, -1.5f, -2.5f);
                    if (Physics.Raycast(ground + Vector3.up * 2.5f, Vector3.down, out var hit, 10f, ~(1 << 2), QueryTriggerInteraction.Ignore)) ground = hit.point;
                    gm.Player.Teleport(ground + Vector3.up * 0.3f, 0f);
                    yield return 1.5f;
                    var p = gm.Player.transform.position;
                    Log($"star '{s.Title}' at {sp}: maya {p} grounded={gm.Player.Grounded} health={gm.Health}");
                }

                // Punch combo (exercises the bone pose).
                var startPunch = typeof(MayaController).GetMethod("StartPunch", any);
                if (k == 0)
                {
                    gm.Player.Teleport(info.Start + new Vector3(-6f, 0f, -3f), 90f);
                    gm.Cam.Snap();
                    yield return 1.5f;
                    Shot("view");
                    var mp = gm.Player.transform.position;
                    var f = gm.Player.transform.forward;
                    var r = gm.Player.transform.right;
                    Shot("maya_front", mp + f * 2.6f + Vector3.up * 1.3f, mp + Vector3.up * 1f);
                    var faceModel = gm.Player.GetComponentInChildren<MayaModel>();
                    foreach (MayaModel.Face face in System.Enum.GetValues(typeof(MayaModel.Face)))
                    {
                        faceModel.Express(face, 5f);
                        yield return 0.15f;
                        Shot($"face_{face}", gm.Player.transform.position + gm.Player.transform.forward * 1.2f + Vector3.up * 1.45f,
                             gm.Player.transform.position + Vector3.up * 1.38f);
                    }
                    faceModel.Express(MayaModel.Face.Normal, 0f);
                    // Mid-dance (title screen): the happy face must stay straight while she moves.
                    gm.Player.Celebrate();
                    for (int d = 0; d < 3; d++)
                    {
                        yield return 0.45f;
                        var hp = gm.Player.transform.position;
                        var hf = gm.Player.transform.forward;
                        Shot($"face_dance{d}", hp + hf * 1.6f + Vector3.up * 1.4f, hp + Vector3.up * 1.25f);
                    }
                    // New pickup models side by side.
                    {
                        var show = new GameObject("Showcase").transform;
                        var fwd = gm.Player.transform.forward;
                        var side = gm.Player.transform.right;
                        var sp = gm.Player.transform.position + fwd * 4f + Vector3.up * 0.4f;
                        OneUp.Build(show, sp - side * 1.4f).enabled = false;
                        PowerCap.Build(show, sp + Vector3.up * 0.6f, PowerKind.Wing).enabled = false;
                        PowerCap.Build(show, sp + side * 1.4f + Vector3.up * 0.6f, PowerKind.Metal).enabled = false;
                        yield return 0.2f;
                        Shot("models", sp + Vector3.up * 1.2f - fwd * 3.2f, sp + Vector3.up * 0.5f);
                        Object.Destroy(show.gameObject);
                    }
                    // Wardrobe: buy and wear everything, look at her front and back while she runs.
                    {
                        Outfits.AddCoins(400);
                        foreach (var it in Outfits.Items) if (!Outfits.Owns(it.Id)) Outfits.Buy(it.Id);
                        Outfits.Wear("jacket_purple", true);
                        Outfits.Wear("crown", true);
                        Outfits.Wear("cape", true);
                        yield return 0.3f;
                        var pp = gm.Player.transform.position;
                        var pf = gm.Player.transform.forward;
                        Shot("outfit_front", pp + pf * 2.6f + Vector3.up * 1.3f, pp + Vector3.up * 1f);
                        ScriptedInput.Active = true;
                        ScriptedInput.WorldMove = pf;
                        yield return 0.8f;
                        ScriptedInput.WorldMove = Vector3.zero;
                        ScriptedInput.Active = false;
                        pp = gm.Player.transform.position;
                        pf = gm.Player.transform.forward;
                        Shot("outfit_back", pp - pf * 2.8f + Vector3.up * 1.5f, pp + Vector3.up * 1f);
                        Log($"wallet {Outfits.Wallet}, wears crown={Outfits.Wears("crown")} cape={Outfits.Wears("cape")} jacket={Outfits.JacketTint}");
                        foreach (var it in Outfits.Items) Outfits.Wear(it.Id, false);
                    }
                    // A guide talks when Maya walks up; the mission star has its beacon.
                    {
                        var npc = Object.FindAnyObjectByType<Npc>();
                        if (npc == null) { errors++; Log("NO GUIDE in world 1"); }
                        else
                        {
                            gm.Player.Teleport(npc.transform.position + npc.transform.forward * 2f, npc.transform.eulerAngles.y + 180f, false);
                            gm.Cam.Snap();
                            yield return 1f;
                            Shot("npc");
                        }
                        Log($"mission: {(gm.Mission != null ? gm.Mission.Title + " " + gm.Mission.Id : "none")}");
                        gm.Hud.Mission(gm.Mission != null ? gm.Mission.Title : "-", 3f);
                        yield return 0.6f;
                        Shot("mission");
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        // Force the combo step so each shot shows punch, punch, kick regardless of frame timing.
                        typeof(MayaController).GetField("punchCombo", any).SetValue(gm.Player, i == 0 ? 2 : i - 1);
                        typeof(MayaController).GetField("lastPunchEnd", any).SetValue(gm.Player, Time.time);
                        startPunch.Invoke(gm.Player, new object[] { false });
                        yield return i == 2 ? 0.14f : 0.1f;
                        mp = gm.Player.transform.position;
                        var model = gm.Player.GetComponent<MayaModel>();
                        var arm = (Transform)typeof(MayaModel).GetField("armR", any).GetValue(model);
                        var fore = (Transform)typeof(MayaModel).GetField("foreR", any).GetValue(model);
                        var pt = (float)typeof(MayaModel).GetField("punchT", any).GetValue(model);
                        Log($"punch {i}: punchT={pt:F2} arm={(arm != null ? arm.name : "null")} armDir={(arm != null && fore != null ? (fore.position - arm.position).normalized.ToString() : "-")} fwd={gm.Player.transform.forward}");
                        Shot($"punch{i}", mp + r * 3f + f * 0.8f + Vector3.up * 1.2f, mp + Vector3.up * 1f + f * 0.4f);
                        yield return 0.4f;
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    startPunch.Invoke(gm.Player, new object[] { false });
                    yield return 0.38f;
                }
                yield return 0.5f;

                // Powers (and pictures of the wings on land).
                gm.Player.GivePower(PowerKind.Wing, 2f);
                yield return 0.5f;
                if (k == 0)
                {
                    var wp = gm.Player.transform.position;
                    var wf = gm.Player.transform.forward;
                    Shot("wings_back", wp - wf * 2.8f + Vector3.up * 1.6f, wp + Vector3.up * 1.1f);
                    Shot("wings_side", wp + gm.Player.transform.right * 3f + Vector3.up * 1.2f, wp + Vector3.up * 1f);
                }
                gm.Player.GivePower(PowerKind.Metal, 2f);
                yield return 2.5f;
                Log($"power after expiry: {gm.Player.Power}");

                // Pound targets, crates and enemies.
                foreach (var t in PoundTarget.All.ToArray()) { t.OnPound(); t.OnPound(); t.OnPound(); }
                yield return 1f;
                foreach (var c in Punchable.All.ToArray()) c.OnPunched(c.transform.position + Vector3.back);
                yield return 1f;
                int defeated = 0;
                foreach (var e in Enemy.All.ToArray())
                    if (e != null && e.BossMaxHp > 0)
                    {
                        var kp = e.transform.position;
                        var kf = e.transform.forward;
                        Shot($"boss_L{k + 1}", kp + kf * 7.5f + new Vector3(0f, 3.5f, 0f), kp + Vector3.up * 1.8f);
                        Log($"boss {e.BossName} hp {e.BossHp}/{e.BossMaxHp}");
                    }
                foreach (var e in Enemy.All.ToArray())
                {
                    if (e == null) continue;
                    bool hit = e.Hit(HitKind.Punch, e.transform.position + Vector3.back);
                    if (!hit) hit = e.Hit(HitKind.Pound, e.transform.position);
                    if (e is KingGrumble) for (int i = 0; i < 6; i++) { typeof(KingGrumble).GetField("invulnerable", any).SetValue(e, 0f); e.Hit(HitKind.Stomp, e.transform.position); }
                    if (hit) defeated++;
                }
                yield return 2f;
                Log($"defeated {defeated}, enemies left {Enemy.All.Count}, health {gm.Health}, lives {gm.Lives}");
                yield return 1f;
            }
        }
    }
}
