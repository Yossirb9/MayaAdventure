using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MayaAdventure
{
    public enum GameState { Title, Playing, Paused, Won, GameOver }

    /// <summary>
    /// Boots automatically in any scene: builds the current level, spawns Maya and the camera,
    /// and owns score, health, lives, level progression (portals) and saved progress.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public const int MaxHealth = 8;
        public const int StartLives = 4;
        const string SaveKey = "maya_stars_v1", LevelKey = "maya_level_v1";

        public GameState State { get; private set; } = GameState.Title;
        public int Coins { get; private set; }
        public int RedCoins { get; private set; }
        public int Lives { get; private set; }
        public int Health { get; private set; }

        /// <summary>Stars collected across all levels.</summary>
        public int Stars => collected.Count;
        public int TotalStars => Levels.TotalStars;
        public int LevelIndex { get; private set; }
        public LevelInfo Level => level;
        public string LevelName => level != null ? level.Name : "";
        public int LevelStars => Levels.CountCollected(LevelIndex, collected);
        public int LevelStarTotal => Levels.StarCounts[LevelIndex];
        public int TotalRedCoins => level != null ? level.TotalRedCoins : 0;
        public bool ShowRedCoins => TotalRedCoins > 0 && !redStarRevealed;
        public float WaterY => level != null ? level.WaterY : float.NegativeInfinity;
        public bool CameraUnderwater { get; private set; }

        public MayaController Player { get; private set; }
        public ThirdPersonCamera Cam { get; private set; }
        public Sfx Sfx { get; private set; }
        public Hud Hud { get; private set; }
        public PostFX Post { get; private set; }

        readonly HashSet<string> collected = new HashSet<string>();
        // This session's memory of each world: coins, red coins, chests, blocks and crates already used, so a world
        // doesn't refill behind Maya's back when she leaves and comes back.
        readonly HashSet<string> taken = new HashSet<string>();
        LevelInfo level;
        int devStar = -1;
        Vector3 checkpoint;
        float checkpointYaw;
        int nextLifeAt;
        bool redStarRevealed, loading;
        float breathTimer;
        Material skyMaterial;
        Light sun;
        Ambience ambience;
        Light characterFill;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameManager>() == null)
                new GameObject("Maya Adventure").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Sfx = gameObject.AddComponent<Sfx>();
            Hud = gameObject.AddComponent<Hud>();
            TouchControls.Enable(gameObject);
            SetupQuality();
            LoadProgress();
            savedLevel = Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, 0), 0, Levels.Count - 1); // before the title backdrop overwrites it
            SpawnPlayerAndCamera();
            ResetRun();
            BuildLevel(0, false);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ---------- setup ----------

        static void SetupQuality()
        {
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowDistance = 110f;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.lodBias = 2f;
            QualitySettings.pixelLightCount = 6;
            QualitySettings.softParticles = true;

            if (Application.isMobilePlatform)
            {
                // Phones: steady 60 fps and keep the screen awake; the rest follows the graphics setting.
                Application.targetFrameRate = 60;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }
        }

        /// <summary>Applies the settings menu: sound, music, camera distance and graphics quality.</summary>
        public void ApplySettings()
        {
            AudioListener.volume = GameSettings.Sound ? 1f : 0f;
            Sfx.SetMusic(GameSettings.Music);
            Cam.distance = GameSettings.ZoomDistances[GameSettings.CameraZoom];

            // Each quality level has its own URP asset (Assets/Settings): render scale, shadows, MSAA, SSAO.
            // Render scale only shrinks the 3D image, so the HUD stays sharp. (Never call Screen.SetResolution
            // mid-game: on Android it resets the graphics surface and broke all text.)
            int q = Mathf.Clamp(GameSettings.Quality, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != q) QualitySettings.SetQualityLevel(q, true);
            var camera = Cam.GetComponent<Camera>();
            var cull = new float[32];
            cull[DetailLayer] = DetailDistance[q];
            camera.layerCullDistances = cull;
            camera.layerCullSpherical = true;
        }

        void SpawnPlayerAndCamera()
        {
            sun = RenderSettings.sun;
            if (sun == null) sun = FindAnyObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;

            var go = new GameObject("Maya") { layer = 2 }; // Ignore Raycast: keeps the camera probe off her
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.6f;
            cc.radius = 0.32f;
            cc.skinWidth = 0.04f;
            cc.center = new Vector3(0f, 0.84f, 0f);
            cc.stepOffset = 0.5f;
            cc.slopeLimit = 50f;
            go.AddComponent<MayaModel>();
            Player = go.AddComponent<MayaController>();

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 900f;
            cam.allowHDR = true;
            cam.allowMSAA = true; // the URP asset (per quality level) provides the depth texture for water foam
            Post = cam.GetComponent<PostFX>();
            if (Post == null) Post = cam.gameObject.AddComponent<PostFX>();
            Cam = cam.GetComponent<ThirdPersonCamera>();
            if (Cam == null) Cam = cam.gameObject.AddComponent<ThirdPersonCamera>();
            Cam.Target = Player.transform;
            ambience = Ambience.Create(cam.transform);

            // Soft fill light from the camera that only touches Maya (layer 2), so her face is never in the dark.
            characterFill = new GameObject("Maya Fill Light").AddComponent<Light>();
            characterFill.type = LightType.Directional;
            characterFill.cullingMask = 1 << 2;
            characterFill.renderingLayerMask = (int)MayaModel.FillLayer; // URP lights select renderers by rendering layer
            characterFill.shadows = LightShadows.None;
            characterFill.intensity = 1.05f;
            characterFill.color = new Color(1f, 0.95f, 0.9f);
            characterFill.renderMode = LightRenderMode.ForcePixel;
            ApplySettings();
        }

        void ApplyTheme(LevelTheme t)
        {
            sun.transform.rotation = Quaternion.Euler(t.SunEuler);
            sun.color = t.SunColor;
            sun.intensity = t.SunIntensity;
            sun.shadowStrength = t.ShadowStrength;

            var skyShader = Shader.Find("Maya/Sky");
            if (skyShader != null)
            {
                if (skyMaterial == null) skyMaterial = new Material(skyShader);
                skyMaterial.SetColor("_Top", t.SkyTop);
                skyMaterial.SetColor("_Horizon", t.SkyHorizon);
                skyMaterial.SetColor("_Bottom", t.SkyBottom);
                skyMaterial.SetColor("_SunColor", t.SunDisk);
                skyMaterial.SetColor("_CloudColor", t.CloudLight);
                skyMaterial.SetColor("_CloudShade", t.CloudShade);
                skyMaterial.SetFloat("_CloudAmount", t.CloudAmount);
                skyMaterial.SetFloat("_Stars", t.NightStars);
                RenderSettings.skybox = skyMaterial;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = t.AmbientSky;
            RenderSettings.ambientEquatorColor = t.AmbientEquator;
            RenderSettings.ambientGroundColor = t.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            ApplyFog(false);
            DynamicGI.UpdateEnvironment();

            Post.Exposure = t.Exposure;
            Post.Saturation = t.Saturation;
            Post.Contrast = t.Contrast;
            Post.Bloom = t.Bloom;
            Post.Vignette = t.Vignette;
            Post.Tint = t.Tint;
            Post.UnderwaterColor = t.UnderwaterFog;
        }

        void ApplyFog(bool underwater)
        {
            var t = level != null ? level.Theme : LevelTheme.Sunny();
            if (underwater)
            {
                RenderSettings.fogColor = t.UnderwaterFog;
                RenderSettings.fogStartDistance = 0f;
                RenderSettings.fogEndDistance = 28f;
            }
            else
            {
                RenderSettings.fogColor = t.FogColor;
                RenderSettings.fogStartDistance = t.FogStart;
                RenderSettings.fogEndDistance = t.FogEnd;
            }
        }

        void ResetRun()
        {
            Coins = 0;
            Lives = StartLives;
            Health = MaxHealth;
            nextLifeAt = 50;
        }

        // ---------- levels ----------

        void BuildLevel(int index, bool returning)
        {
            if (level != null)
            {
                level.Root.gameObject.SetActive(false); // so the new level's ground probes ignore it
                Destroy(level.Root.gameObject);
            }
            LevelIndex = Mathf.Clamp(index, 0, Levels.Count - 1);
            level = Levels.Build(LevelIndex);
            RedCoins = 0;
            redStarRevealed = false;
            string redPrefix = LevelIndex + ":red:";
            foreach (var key in taken)
                if (key.StartsWith(redPrefix)) RedCoins++;
            if (level.RedCoinStar != null && TotalRedCoins > 0 && RedCoins >= TotalRedCoins)
            {
                redStarRevealed = true;
                level.RedCoinStar.gameObject.SetActive(true);
            }
            MarkDetails(level.Root);
            Npc.ForLevel(LevelIndex, level);
            UpdateMission();
            ApplyTheme(level.Theme);
            Sfx.SetWorld(LevelIndex);
            ambience.Set(level.Ambience);
            if (State != GameState.Title)
            {
                // Remember where Maya is (the title screen's backdrop world doesn't count).
                savedLevel = LevelIndex;
                PlayerPrefs.SetInt(LevelKey, LevelIndex);
            }
            checkpoint = returning ? level.ReturnSpawn : level.Start;
            checkpointYaw = returning ? level.ReturnYaw : level.StartYaw;
            Physics.SyncTransforms(); // the new world's colliders must exist before Maya lands on them
            Player.Teleport(checkpoint, checkpointYaw, false);
            Cam.Snap();
            CameraUnderwater = false;
            StartCoroutine(SpawnGuard(checkpoint, checkpointYaw));
        }

        /// <summary>Half a second after a world loads, put Maya back on the start if anything moved her away.</summary>
        IEnumerator SpawnGuard(Vector3 spawn, float yaw)
        {
            for (int i = 0; i < 2; i++)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                var p = Player.transform.position;
                // Far off (not just a few running steps) or sunk below the start: something moved her.
                if (new Vector2(p.x - spawn.x, p.z - spawn.z).sqrMagnitude > 100f || p.y < spawn.y - 3f)
                {
                    Debug.LogWarning($"[Maya] Spawn moved to {p}, back to {spawn}");
                    Player.Teleport(spawn, yaw, false);
                    Cam.Snap();
                }
            }
        }

        /// <summary>Travel through a portal (with a short fade).</summary>
        public void EnterLevel(int index)
        {
            if (loading || index == LevelIndex) return;
            StartCoroutine(EnterLevelRoutine(index));
        }

        IEnumerator EnterLevelRoutine(int index)
        {
            loading = true;
            bool returning = index < LevelIndex;
            Sfx.Play(Sfx.Reveal);
            Hud.Fade(1f, 0.45f);
            yield return new WaitForSeconds(0.5f);
            BuildLevel(index, returning);
            Hud.Fade(0f, 0.6f);
            Hud.ShowTitle(level.Name, 3f);
            if (Mission != null) Hud.Mission(Mission.Title, 5f);
            loading = false;
        }

        public bool HasStar(string id) => collected.Contains(id);

        /// <summary>This world's stars, in order.</summary>
        public System.Collections.Generic.List<PowerStar> LevelStarsList()
        {
            var list = new System.Collections.Generic.List<PowerStar>();
            if (level != null) list.AddRange(level.Root.GetComponentsInChildren<PowerStar>(true));
            list.Sort((a, b) => a.Id.Length != b.Id.Length ? a.Id.Length - b.Id.Length : string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>The world's next star to get (its name is the mission), or null when all are collected.</summary>
        public PowerStar Mission { get; private set; }

        /// <summary>Picks the first star of this world not collected yet and lights its beacon.</summary>
        public void UpdateMission()
        {
            if (level == null) return;
            PowerStar best = null;
            foreach (var s in LevelStarsList())
            {
                s.SetBeacon(false);
                if (best == null && !s.AlreadyCollected && !HasStar(s.Id) && s.gameObject.activeInHierarchy) best = s;
            }
            Mission = best;
            if (best != null) best.SetBeacon(true);
        }

        /// <summary>Layer of small decorations (flowers, grass, pebbles) that the camera stops drawing beyond a distance.</summary>
        public const int DetailLayer = 3;
        static readonly float[] DetailDistance = { 35f, 55f, 90f };

        /// <summary>Puts small, purely decorative renderers on <see cref="DetailLayer"/> (no shadows on lower qualities).</summary>
        static void MarkDetails(Transform root)
        {
            bool shadows = GameSettings.Quality >= 2;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.gameObject.layer != 0) continue;
                var size = r.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) > 1.6f) continue;
                if (r.GetComponentInParent<Collider>() != null || r.GetComponentInParent<MonoBehaviour>() != null) continue;
                r.gameObject.layer = DetailLayer;
                if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Id of a world object for <see cref="Taken"/>: its kind and where it was built, in the current world.</summary>
        public static string KeyFor(string kind, Vector3 position) =>
            I == null ? null : $"{I.LevelIndex}:{kind}:{Mathf.RoundToInt(position.x * 4f)},{Mathf.RoundToInt(position.y * 4f)},{Mathf.RoundToInt(position.z * 4f)}";

        /// <summary>Was this coin / chest / block already used earlier in this session?</summary>
        public bool Taken(string key) => key != null && taken.Contains(key);

        public void Take(string key)
        {
            if (key != null) taken.Add(key);
        }

        void LoadProgress()
        {
            collected.Clear();
            string saved = PlayerPrefs.GetString(SaveKey, "");
            foreach (var id in saved.Split(','))
                if (!string.IsNullOrEmpty(id)) collected.Add(id);
        }

        void SaveProgress()
        {
            PlayerPrefs.SetString(SaveKey, string.Join(",", collected));
            PlayerPrefs.Save();
        }

        // ---------- flow ----------

        void Update()
        {
            switch (State)
            {
                case GameState.Title:
                    // The title menu (Hud) calls ContinueGame / StartNewGame. Meanwhile Maya shows off now and then.
                    if (!Cam.TitleMode) PoseForTitle();
                    if (Time.time - titleTouchedAt > 3.5f)
                    {
                        // Turn back to face the camera once nobody is spinning her.
                        var e = Player.transform.eulerAngles;
                        e.y = Mathf.MoveTowardsAngle(e.y, titleFacing, 90f * Time.deltaTime);
                        Player.transform.eulerAngles = e;
                    }
                    if (Time.time > titleDanceAt)
                    {
                        Player.Celebrate();
                        titleDanceAt = Time.time + 6f + Random.value * 3f;
                    }
                    break;
                case GameState.Playing:
                    if (Controls.PauseDown) SetPaused(true);
                    else if (Player.transform.position.y < level.KillY) FellOff();
                    else if (Controls.ClickDown && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
                    Breathe();
#if UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
                    // Developer shortcut (editor only): F2..F6 jump straight to a level.
                    for (int k = 0; k < Levels.Count; k++)
                        if (Input.GetKeyDown(KeyCode.F2 + k)) EnterLevel(k);
                    // F7: hop to the next star of this level, F8 / F9: wing / metal power.
                    if (Input.GetKeyDown(KeyCode.F7))
                    {
                        var stars = level.Root.GetComponentsInChildren<PowerStar>(true);
                        if (stars.Length > 0)
                        {
                            devStar = (devStar + 1) % stars.Length;
                            var sp = stars[devStar].transform.position;
                            Player.Teleport(LevelBuilder.Snap(sp + new Vector3(0f, 0f, -3f), sp.y + 1f) + Vector3.up * 0.2f, 0f);
                            Cam.Snap();
                            Hud.Show($"כוכב {devStar + 1}/{stars.Length}", 1f);
                        }
                    }
                    if (Input.GetKeyDown(KeyCode.F8)) Player.GivePower(PowerKind.Wing, 25f);
                    if (Input.GetKeyDown(KeyCode.F9)) Player.GivePower(PowerKind.Metal, 18f);
#endif
                    break;
                case GameState.Paused:
                    if (Controls.RespawnDown) { SetPaused(false); Player.Teleport(checkpoint, checkpointYaw); Cam.Snap(); }
                    else if (Controls.NewGameDown) { SetPaused(false); NewGame(); }
                    else if (Controls.PauseDown) SetPaused(false); // the pause menu buttons live in the Hud
                    break;
                case GameState.Won:
                    if (Controls.NewGameDown) NewGame();
                    else if (Controls.ConfirmDown) { State = GameState.Playing; LockCursor(true); }
                    break;
                case GameState.GameOver:
                    if (Controls.ConfirmDown) Restart();
                    break;
            }
            bool playing = State == GameState.Playing && !loading;
            Player.InputEnabled = playing;
            Cam.InputEnabled = playing;
        }

        void LateUpdate()
        {
            // Underwater look: dense tinted fog + post tint when the camera dips below the surface.
            bool under = Cam != null && Cam.transform.position.y < WaterY;
            if (under != CameraUnderwater)
            {
                CameraUnderwater = under;
                ApplyFog(under);
            }
            if (Post != null) Post.Underwater = Mathf.MoveTowards(Post.Underwater, under ? 1f : 0f, Time.deltaTime * 4f);
            if (characterFill != null && Cam != null)
                characterFill.transform.rotation = Cam.transform.rotation * Quaternion.Euler(12f, -18f, 0f);
        }

        /// <summary>Under water Maya slowly loses power; at the surface it comes back (like SM64).</summary>
        void Breathe()
        {
            if (Player.HeadUnderwater)
            {
                breathTimer += Time.deltaTime;
                if (breathTimer > 4f)
                {
                    breathTimer = 0f;
                    Health--;
                    Sfx.Play(Sfx.Hurt, 0.4f);
                    if (Health <= 0) LoseLife();
                }
            }
            else if (Player.Swimming)
            {
                breathTimer += Time.deltaTime;
                if (breathTimer > 0.8f)
                {
                    breathTimer = 0f;
                    if (Health < MaxHealth) Health++;
                }
            }
            else
            {
                breathTimer = 0f;
            }
        }

        /// <summary>
        /// Title backdrop: Maya stands a few steps into the world facing the camera, on the left of the frame, with the
        /// level (towers, coins, islands) behind her; the menu sits on the right.
        /// </summary>
        void PoseForTitle()
        {
            if (level == null || Player == null) return;
            var rot = Quaternion.Euler(0f, level.StartYaw, 0f);
            Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
            var m = LevelBuilder.Snap(level.Start + f * 4f, level.Start.y + 6f);
            var camPos = m - f * 2.45f + r * 0.8f + Vector3.up * 1.12f;
            var toCam = camPos - m;
            titleFacing = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
            Player.Teleport(m, titleFacing, false); // looks straight at the player, no respawn blinking
            Cam.TitleShot(camPos, m + f * 5f + r * 1.95f + Vector3.up * 1.2f);
            titleDanceAt = Time.time + 2.5f;
        }

        float titleDanceAt, titleFacing, titleTouchedAt = -10f;

        /// <summary>Title screen: dragging turns Maya around (she turns back to the camera a few seconds later).</summary>
        public void TitleSpin(float dxPixels)
        {
            if (State != GameState.Title || Player == null) return;
            Player.transform.Rotate(0f, -dxPixels * 0.45f * 1080f / Screen.height, 0f, Space.World);
            titleTouchedAt = Time.time;
            titleDanceAt = Mathf.Max(titleDanceAt, Time.time + 4f);
        }

        /// <summary>Title screen: a tap on Maya makes her react.</summary>
        public void TitleTap(Vector2 screen)
        {
            if (State != GameState.Title || Player == null) return;
            var cam = Cam.GetComponent<Camera>();
            var feet = cam.WorldToScreenPoint(Player.transform.position);
            var head = cam.WorldToScreenPoint(Player.transform.position + Vector3.up * 1.75f);
            if (feet.z <= 0f) return;
            float height = Mathf.Abs(head.y - feet.y);
            bool onMaya = screen.y > feet.y - height * 0.1f && screen.y < head.y + height * 0.15f &&
                          Mathf.Abs(screen.x - (feet.x + head.x) * 0.5f) < height * 0.33f;
            if (!onMaya) return;
            Player.TitleReact();
            titleTouchedAt = Time.time;
            titleDanceAt = Time.time + 5f;
        }

        void StartPlaying()
        {
            if (State == GameState.Title || Cam.TitleMode)
            {
                // Leave the title pose: back to the world's start.
                Player.Teleport(checkpoint, checkpointYaw, false);
            }
            State = GameState.Playing;
            LockCursor(true);
            Cam.Snap();
            Hud.OnGameStarted();
            Hud.ShowTitle(level.Name, 3f);
            if (Mission != null) Hud.Mission(Mission.Title, 5f);
        }

        public void Resume() => SetPaused(false);

        public void RespawnFromMenu()
        {
            SetPaused(false);
            Player.Teleport(checkpoint, checkpointYaw);
            Cam.Snap();
        }

        /// <summary>Pause menu: back to the title screen (the current world stays as the backdrop).</summary>
        public void QuitToTitle()
        {
            Time.timeScale = 1f;
            State = GameState.Title;
            savedLevel = LevelIndex;
            LockCursor(false);
            PlayerPrefs.Save();
        }

        IEnumerator NextMissionSoon()
        {
            yield return null; // the collected star is gone by now
            UpdateMission();
            if (Mission != null) Hud.Mission(Mission.Title, 4f);
        }

        /// <summary>Slows the world down for a dramatic moment (real seconds), then back to normal speed.</summary>
        public void SlowMotion(float scale, float seconds) => StartCoroutine(SlowMotionRoutine(scale, seconds));

        IEnumerator SlowMotionRoutine(float scale, float seconds)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(seconds);
            // Don't undo a pause that started meanwhile.
            if (State != GameState.Paused && Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = 1f;
        }

        void SetPaused(bool paused)
        {
            State = paused ? GameState.Paused : GameState.Playing;
            Time.timeScale = paused ? 0f : 1f;
            LockCursor(!paused);
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>After a game over: keep the stars, rebuild the current level.</summary>
        void Restart()
        {
            Time.timeScale = 1f;
            ResetRun();
            BuildLevel(LevelIndex, false);
            StartPlaying();
        }

        public bool HasProgress => collected.Count > 0 || savedLevel > 0;
        int savedLevel;

        /// <summary>Title menu: carry on in the world Maya was last in.</summary>
        public void ContinueGame()
        {
            if (savedLevel != LevelIndex) BuildLevel(savedLevel, false);
            StartPlaying();
        }

        /// <summary>Title menu: wipe the saved stars and start from the first world.</summary>
        public void StartNewGame() => NewGame();

        /// <summary>N: forget all progress and start again from the first level.</summary>
        void NewGame()
        {
            Time.timeScale = 1f;
            collected.Clear();
            taken.Clear();
            SaveProgress();
            savedLevel = 0;
            PlayerPrefs.SetInt(LevelKey, 0);
            ResetRun();
            BuildLevel(0, false);
            StartPlaying();
        }

        // ---------- events ----------

        /// <summary>Blue coins and other multi-coin rewards.</summary>
        public void CollectCoins(int count, Vector3 position)
        {
            Sfx.Play(Sfx.RedCoin, 0.6f);
            for (int i = 0; i < count; i++) AddCoin();
        }

        void AddCoin()
        {
            Outfits.AddCoins(1);
            Coins++;
            Health = Mathf.Min(MaxHealth, Health + 1);
            if (Coins >= nextLifeAt)
            {
                nextLifeAt += 50;
                Lives++;
                Sfx.Play(Sfx.OneUp);
                Hud.Show("חיים נוספים!");
            }
        }

        public void AddLife()
        {
            Lives++;
            Sfx.Play(Sfx.OneUp);
            Hud.Show("חיים נוספים!");
        }

        public void Heal()
        {
            Health = MaxHealth;
            Sfx.Play(Sfx.Checkpoint, 0.6f);
        }

        public void CollectCoin(bool red, Vector3 position)
        {
            Outfits.AddCoins(1);
            Coins++;
            Health = Mathf.Min(MaxHealth, Health + 1);
            if (red)
            {
                RedCoins++;
                Sfx.Play(Sfx.RedCoin);
                if (RedCoins >= TotalRedCoins && level.RedCoinStar != null && !redStarRevealed)
                {
                    redStarRevealed = true;
                    level.RedCoinStar.Reveal();
                    Sfx.Play(Sfx.Reveal);
                    Hud.Show(level.RedCoinHint, 3.5f);
                }
                else
                {
                    Hud.Show($"מטבע אדום {RedCoins}/{TotalRedCoins}", 1.5f);
                }
            }
            else
            {
                Sfx.Play(Sfx.Coin, 0.7f);
            }
            if (Coins >= nextLifeAt)
            {
                nextLifeAt += 50;
                Lives++;
                Sfx.Play(Sfx.OneUp);
                Hud.Show("חיים נוספים!");
            }
        }

        public void CollectStar(PowerStar star)
        {
            Health = MaxHealth;
            Sfx.Play(Sfx.Star);
            if (star.AlreadyCollected)
            {
                Hud.Show($"{star.Title} (כבר נאסף)", 2.5f);
                return;
            }
            collected.Add(star.Id);
            SaveProgress();
            StartCoroutine(NextMissionSoon());
            Debris.Burst(star.transform.position, new Color(1f, 0.9f, 0.35f), 30, 7f);
            Haptics.Pulse(60);
            Sfx.Say(Sfx.SayYesh);
            Hud.Show($"{star.Title} - כוכב {Stars}/{TotalStars}!", 3.5f);
            if (Stars >= TotalStars) StartCoroutine(WinAfterDelay());
        }

        IEnumerator WinAfterDelay()
        {
            yield return new WaitForSeconds(2f);
            if (State != GameState.Playing) yield break;
            State = GameState.Won;
            LockCursor(false);
        }

        public void Damage(int amount, Vector3 source)
        {
            if (Player.Invulnerable || State != GameState.Playing || Player.Power == PowerKind.Metal || ScriptedInput.Active) return;
            Health -= amount;
            Sfx.Play(Sfx.Hurt);
            Sfx.Say(Sfx.SayOuch);
            Player.Knockback(source);
            if (Health <= 0) LoseLife();
        }

        /// <summary>Fall damage (in power-meter wedges), no knockback.</summary>
        public void FallDamage(int wedges)
        {
            if (State != GameState.Playing) return;
            Health -= wedges;
            Sfx.Play(Sfx.Hurt);
            if (Health <= 0) LoseLife();
        }

        void FellOff()
        {
            Sfx.Play(Sfx.Splash);
            Health -= 3;
            if (Health <= 0)
            {
                LoseLife();
                return;
            }
            Hud.Show("אופס! נפלת", 1.5f);
            Player.Teleport(checkpoint, checkpointYaw);
            Cam.Snap();
        }

        void LoseLife()
        {
            Lives--;
            Health = MaxHealth;
            if (Lives < 0)
            {
                State = GameState.GameOver;
                LockCursor(false);
                return;
            }
            Hud.Show("מאיה איבדה חיים", 2f);
            Player.Teleport(checkpoint, checkpointYaw);
            Cam.Snap();
        }

        public void SetCheckpoint(Vector3 position, float yaw)
        {
            checkpoint = position;
            checkpointYaw = yaw;
            Sfx.Play(Sfx.Checkpoint);
            Hud.Show("נקודת ביקורת!", 1.5f);
        }

        /// <summary>Ground-pound landing: hits every enemy and pound switch close by.</summary>
        public void PoundShock(Vector3 position)
        {
            Sfx.Play(Sfx.Pound);
            ThirdPersonCamera.Shake(0.16f);
            Debris.Burst(position + Vector3.up * 0.1f, new Color(1f, 0.9f, 0.6f), 8, 4f);
            Debris.Dust(position, 14, 1.6f);
            foreach (var e in Enemy.All.ToArray())
            {
                Vector3 d = e.Origin - position;
                float r = 2.8f + e.Radius * 0.5f;
                if (d.y > -1.5f && d.y < 1.2f && new Vector2(d.x, d.z).sqrMagnitude < r * r) e.Hit(HitKind.Pound, position);
            }
            foreach (var box in Punchable.All.ToArray())
                if ((box.PunchPoint - position).sqrMagnitude < 2.2f * 2.2f) box.OnPunched(position);
            foreach (var target in PoundTarget.All.ToArray())
                if ((target.transform.position - position).sqrMagnitude < target.Radius * target.Radius) target.OnPound();
        }
    }
}
