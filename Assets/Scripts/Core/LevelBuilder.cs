using System.Collections.Generic;
using UnityEngine;

namespace MayaAdventure
{
    public class LevelInfo
    {
        public string Name = "";
        public LevelTheme Theme = LevelTheme.Sunny();
        public Transform Root;
        public Vector3 Start;
        public float StartYaw;
        /// <summary>Where Maya appears when she comes back from the next level.</summary>
        public Vector3 ReturnSpawn;
        public float ReturnYaw;
        public float WaterY = float.NegativeInfinity;
        public float KillY = -40f;
        public AmbienceKind Ambience = AmbienceKind.Motes;
        public PowerStar RedCoinStar;
        public string RedCoinHint = "";
        public int TotalStars;
        public int TotalRedCoins;
    }

    /// <summary>
    /// Builds "Sunny Island" in code. Layout (top of main island is y = 0):
    ///   start (0,-22) facing north
    ///   north  (0,14)   spiral tower        -> star 1 on top
    ///   east   (38..70) sky course + island -> star 2
    ///   west   (-16,0)  Grumble meadow      -> star 3 appears after 8 red coins
    /// </summary>
    public static class LevelBuilder
    {
        internal static Transform root;
        /// <summary>Ground cover scattered on islands built from now on (set by each level after Begin).</summary>
        internal static Nature.Cover cover = Nature.Cover.Meadow;
        internal static Material grass, rock, sand, brick, plank, checker, bark, leaves, stone, flagRed, brickBlock;
        internal static readonly Color KenneyGrass = new Color(90f / 255f, 196f / 255f, 135f / 255f);

        static readonly Vector3 TowerCenter = new Vector3(0f, 0f, 14f);
        static readonly Vector3 MeadowCenter = new Vector3(-16f, 0f, 0f);

        /// <summary>Creates the level root and shared materials; every level builder starts with this.</summary>
        internal static Transform Begin(string name)
        {
            root = new GameObject(name).transform;
            cover = Nature.Cover.Meadow;
            grass = Art.Textured("grass", new Vector2(14f, 14f));
            rock = Art.Textured("rock", new Vector2(10f, 2f), 0.05f);
            sand = Art.Textured("sand", new Vector2(18f, 18f));
            brick = Art.Textured("brick", new Vector2(5f, 8f), 0.1f);
            plank = Art.Textured("plank", new Vector2(1f, 1f), 0.15f);
            checker = Art.Textured("checker", new Vector2(2f, 2f), 0.35f);
            bark = Art.Solid(new Color(0.45f, 0.3f, 0.18f), 0.1f);
            leaves = Art.Solid(new Color(0.25f, 0.62f, 0.25f), 0.1f);
            stone = Art.Textured("rock", new Vector2(1f, 1f), 0.1f);
            flagRed = Art.Solid(new Color(1f, 0.35f, 0.55f), 0.2f);
            brickBlock = Art.Textured("brick", new Vector2(1f, 1f), 0.1f);
            return root;
        }

        public static LevelInfo Build()
        {
            Begin("Level 1 - Sunny Island");
            var info = new LevelInfo
            {
                Name = "אי השמש",
                Theme = LevelTheme.Sunny(),
                Root = root,
                Start = new Vector3(0f, 0f, -22f),
                StartYaw = 0f,
                ReturnSpawn = new Vector3(-5f, 0f, -24f),
                ReturnYaw = 90f,
                WaterY = -1f,
                KillY = -30f,
                TotalRedCoins = 8,
                RedCoinHint = "כוכב סודי הופיע באחו!",
            };

            BuildIslandsAndWater();
            BuildTower();
            BuildSkyCourse();
            BuildMeadow(info);
            BuildSouthSide();
            BuildDecor();
            Zones.Sunny();
            DistantIslands(new Color(0.55f, 0.85f, 0.6f), 9);
            Portal.Build(root, new Vector3(-8f, 0f, -24f), 90f, 1, Levels.Required[1]);

            int stars = 0;
            foreach (var s in root.GetComponentsInChildren<PowerStar>(true)) stars++;
            info.TotalStars = stars;
            return info;
        }

        // ---------- helpers ----------

        // Native bounding-box sizes of the Kenney blocks used as level geometry (origin at bottom centre).
        internal static readonly Dictionary<string, Vector3> Native = new Dictionary<string, Vector3>
        {
            { "block-grass", new Vector3(1.08f, 1f, 1.08f) },
            { "block-grass-large", new Vector3(2.08f, 1f, 2.08f) },
            { "block-grass-large-tall", new Vector3(2.08f, 2f, 2.08f) },
            { "block-grass-low-large", new Vector3(2.08f, 0.5f, 2.08f) },
            { "platform", new Vector3(1f, 0.2f, 1f) },
            { "block-moving-large", new Vector3(1f, 0.5f, 1f) },
            { "brick", new Vector3(0.5f, 0.5f, 0.5f) },
            { "block-snow", new Vector3(1.08f, 1f, 1.08f) },
            { "block-snow-large", new Vector3(2.08f, 1f, 2.08f) },
            { "block-snow-large-tall", new Vector3(2.08f, 2f, 2.08f) },
            { "block-snow-low-large", new Vector3(2.08f, 0.5f, 2.08f) },
            { "platform-fortified", new Vector3(1f, 0.21f, 1f) },
            { "block-moving", new Vector3(1f, 0.3f, 1f) },
            { "block-moving-blue", new Vector3(1f, 0.5f, 1f) },
            { "conveyor-belt", new Vector3(1f, 0.35f, 1f) },
            { "crate", new Vector3(0.5f, 0.5f, 0.5f) },
            { "crate-strong", new Vector3(0.5f, 0.5f, 0.5f) },
            { "pipe", new Vector3(1f, 0.56f, 1f) },
        };

        /// <summary>A Kenney block stretched to fill the box (centre, size); textured cube if the kit is missing.</summary>
        internal static GameObject KBox(string model, string name, Vector3 center, Vector3 size, Material fallback, Transform parent = null, float yaw = 0f)
        {
            if ((model.StartsWith("block-grass") || model.StartsWith("block-snow")) && Nature.Load("Nature/Grass_Common_Short") != null)
            {
                bool snow = model.StartsWith("block-snow");
                var top = Art.Lawn(snow ? new Color(0.93f, 0.96f, 1f) : KenneyGrass * 0.82f, 1f);
                var block = IslandMesh.Block(name, parent != null ? parent : root, center, size, top, Art.Textured("rock", new Vector2(0.5f, 0.5f), 0.05f));
                block.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                return block;
            }
            if (!Native.TryGetValue(model, out var n) || !Props.Has(model)) return Box(name, center, size, fallback, parent, yaw);
            var go = Props.Spawn(model, parent != null ? parent : root, center - Vector3.up * (size.y * 0.5f),
                                 new Vector3(size.x / n.x, size.y / n.y, size.z / n.z), yaw, Props.Col.Box);
            go.name = name;
            return go;
        }

        /// <summary>Island made of 4 m Kenney grass blocks whose top is at <paramref name="top"/>.</summary>
        internal static void TileIsland(string name, Vector3 center, float radius, float top) =>
            TileIsland(name, center, radius, top, "block-grass-large-tall", KenneyGrass, -12f);

        internal static void TileIsland(string name, Vector3 center, float radius, float top, string model, Color? lawn, float baseBottom,
                                        bool floating = false)
        {
            var parent = new GameObject(name).transform;
            parent.SetParent(root, false);
            if (Nature.Load("Nature/Grass_Common_Short") != null)
            {
                // Organic island mesh + instanced ground cover (the square-tile look is gone).
                bool snow = model.Contains("snow");
                var tint = lawn ?? KenneyGrass;
                var lawnMat = Art.Lawn(tint.grayscale > 0.85f ? tint : tint * 0.82f, (radius + 3f) * 0.9f);
                var rockMat = snow ? Art.Textured("rock", Vector2.one, 0.2f) : Art.Textured("rock", Vector2.one, 0.05f);
                float bottom = floating ? float.NaN : float.IsNaN(baseBottom) ? top - 4f : baseBottom;
                int seed = name.GetHashCode() ^ Mathf.RoundToInt(center.x * 13f + center.z * 7f);
                IslandMesh.Build(name + "Ground", parent, center, radius, top, bottom, lawnMat, rockMat, seed);
                Nature.Scatter(parent, center, radius, top, cover, seed);
                return;
            }
            const float step = 4.16f;
            int n = Mathf.CeilToInt(radius / step);
            for (int i = -n; i <= n; i++)
            for (int j = -n; j <= n; j++)
            {
                var p = new Vector3(center.x + i * step, top - 4f, center.z + j * step);
                if (new Vector2(i * step, j * step).magnitude > radius) continue;
                Props.Spawn(model, parent, p, 2f, 0f, Props.Col.Box);
            }
            // Seamless lawn over the interior so the tile grid only shows at the cliff edge.
            if (lawn.HasValue)
                Art.Prim(PrimitiveType.Cylinder, "Lawn", parent, new Vector3(center.x, top + 0.004f, center.z),
                         new Vector3((radius - 1.5f) * 2f, 0.002f, (radius - 1.5f) * 2f),
                         Art.Lawn(lawn.Value.grayscale > 0.85f ? lawn.Value : lawn.Value * 0.82f, radius * 0.9f), Art.Col.None, null, false);
            // Rock foundation under the tiles, down to the sea floor (seen when swimming).
            if (baseBottom < top - 4f)
            {
                float h = top - 3.9f - baseBottom;
                Art.Prim(PrimitiveType.Cylinder, "Foundation", parent, new Vector3(center.x, baseBottom + h * 0.5f, center.z),
                         new Vector3(radius * 2f + 2f, h * 0.5f, radius * 2f + 2f), Art.Textured("rock", new Vector2(radius * 0.6f, h * 0.25f), 0.05f), Art.Col.Mesh);
            }
            // Islands never move: merge the tiles into a few static batches (far fewer draw calls on phones).
            StaticBatchingUtility.Combine(parent.gameObject);
        }

        internal static GameObject Box(string name, Vector3 center, Vector3 size, Material m, Transform parent = null, float yaw = 0f) =>
            Art.Prim(PrimitiveType.Cube, name, parent != null ? parent : root, center, size, m, Art.Col.Default, Quaternion.Euler(0f, yaw, 0f));

        /// <summary>Cylinder whose top face is at <paramref name="top"/>.</summary>
        internal static GameObject Disc(string name, Vector3 top, float radius, float thickness, Material m, Transform parent = null) =>
            Art.Prim(PrimitiveType.Cylinder, name, parent != null ? parent : root, top - Vector3.up * (thickness * 0.5f),
                     new Vector3(radius * 2f, thickness * 0.5f, radius * 2f), m, Art.Col.Mesh);

        internal static void Coins(params Vector3[] positions)
        {
            foreach (var p in positions) Coin.Build(root, p);
        }

        internal static void CoinRing(Vector3 center, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                Coin.Build(root, center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        internal static void CoinLine(Vector3 from, Vector3 to, int count)
        {
            for (int i = 0; i < count; i++) Coin.Build(root, Vector3.Lerp(from, to, count == 1 ? 0f : i / (float)(count - 1)));
        }

        // ---------- islands ----------

        static void BuildIslandsAndWater()
        {
            if (Props.Has("block-grass-large-tall"))
            {
                TileIsland("MainIsland", Vector3.zero, 32f, 0f);
                TileIsland("EastIsland", new Vector3(70f, 0f, 0f), 9f, 2f);
            }
            else
            {
                BuildDiscIslands();
            }

            Water(-1f, -12f, LevelTheme.Sunny());
        }

        /// <summary>Swimmable sea: water surface at <paramref name="surface"/>, sandy floor at <paramref name="floor"/>.</summary>
        internal static void Water(float surface, float floor, LevelTheme theme)
        {
            var shader = Shader.Find("Maya/Water");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetColor("_Shallow", theme.WaterShallow);
                mat.SetColor("_Deep", theme.WaterDeep);
                mat.SetColor("_SkyColor", Color.Lerp(theme.SkyHorizon, theme.SkyTop, 0.2f));
                mat.SetColor("_SkyTop", Color.Lerp(theme.SkyHorizon, theme.SkyTop, 0.85f));
            }
            else
            {
                mat = Art.Water();
            }
            var water = Art.Prim(PrimitiveType.Plane, "Water", root, new Vector3(0f, surface, 0f), new Vector3(80f, 1f, 80f), mat, Art.Col.None);
            water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (shader == null) water.AddComponent<WaterSurface>();

            var seabed = Art.Prim(PrimitiveType.Cylinder, "SeaFloor", root, new Vector3(0f, floor - 0.5f, 0f), new Vector3(420f, 0.5f, 420f),
                                  Art.Textured("sand", new Vector2(60f, 60f)), Art.Col.Mesh, null, false);
            seabed.GetComponent<Renderer>().receiveShadows = true;
        }

        /// <summary>Far-away decorative islands on the horizon (no colliders).</summary>
        internal static void DistantIslands(Color lawn, int count, float minR = 150f, float maxR = 240f, string model = "block-grass-large-tall", string tree = "tree")
        {
            if (!Props.Has(model)) return;
            var parent = new GameObject("Horizon").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(11);
            for (int i = 0; i < count; i++)
            {
                float a = (i + (float)rng.NextDouble() * 0.6f) / count * 360f;
                float r = minR + (float)rng.NextDouble() * (maxR - minR);
                var c = Polar(a, r);
                int tiles = 2 + rng.Next(4);
                float s = 5f + (float)rng.NextDouble() * 4f;
                for (int t = 0; t < tiles; t++)
                {
                    var off = new Vector3(((float)rng.NextDouble() - 0.5f) * s * 4f, 0f, ((float)rng.NextDouble() - 0.5f) * s * 4f);
                    float height = (float)rng.NextDouble() * s * 1.6f;
                    Props.Spawn(model, parent, c + off + Vector3.down * (s * 2f - height), new Vector3(s, s, s), (float)rng.NextDouble() * 360f, Props.Col.None, false);
                    if (Props.Has(tree) && rng.NextDouble() < 0.8)
                        Props.Spawn(tree, parent, c + off + Vector3.up * (height + s * 0.0f), s * 0.9f, (float)rng.NextDouble() * 360f, Props.Col.None, false);
                }
            }
        }

        static void BuildDiscIslands()
        {
            // Main island: rock body, grass lid, sandy beach ring.
            Disc("IslandRock", new Vector3(0f, -0.25f, 0f), 32f, 8f, rock);
            Disc("IslandGrass", Vector3.zero, 32.1f, 0.4f, grass);
            Disc("Beach", new Vector3(0f, -0.45f, 0f), 35f, 3f, sand);

            // East island (end of the sky course)
            Disc("EastRock", new Vector3(70f, 1.75f, 0f), 9f, 8f, rock);
            Disc("EastGrass", new Vector3(70f, 2f, 0f), 9.1f, 0.4f, Art.Textured("grass", new Vector2(4f, 4f)));
            Disc("EastBeach", new Vector3(70f, -0.45f, 0f), 11f, 3f, Art.Textured("sand", new Vector2(6f, 6f)));
        }

        // ---------- star 1: spiral tower ----------

        static void BuildTower()
        {
            var c = TowerCenter;
            const int steps = 42;
            const float rise = 0.5f, radius = 4.4f, spacing = 22f;
            float topY = rise * (steps + 1);

            // Core stops just under the top slab: two surfaces at the same height flicker (z-fighting) on phones.
            Disc("TowerCore", c + Vector3.up * (topY - 0.06f), 2.6f, topY - 0.06f, brick);
            Disc("TowerTop", c + Vector3.up * topY, 3.6f, 0.5f, stone);

            for (int i = 0; i < steps; i++)
            {
                if (i == 10 || i == 21 || i == 32) continue; // gaps: jump across
                float a = -90f + i * spacing;
                float rad = a * Mathf.Deg2Rad;
                float stepTop = rise * (i + 1);
                var pos = c + new Vector3(Mathf.Cos(rad) * radius, stepTop - 0.25f, Mathf.Sin(rad) * radius);
                KBox("platform", $"Step{i}", pos, new Vector3(2.4f, 0.5f, 1.7f), i % 2 == 0 ? plank : checker, null, -a);
                if (i % 4 == 2) Coin.Build(root, pos + Vector3.up * 1.05f);
            }

            // Battlements and flags on top
            for (int i = 0; i < 8; i++)
            {
                if (i == 2) continue; // opening where the stairs arrive (last step is at ~92 degrees)
                float a = i * 45f * Mathf.Deg2Rad;
                KBox("brick", "Merlon", c + new Vector3(Mathf.Cos(a) * 3.2f, topY + 0.4f, Mathf.Sin(a) * 3.2f), new Vector3(0.8f, 0.8f, 0.8f), stone);
            }
            if (Props.Has("flag"))
            {
                Props.Spawn("flag", root, c + new Vector3(0f, topY, 2.2f), 5f, 90f);
            }
            else
            {
                Art.Prim(PrimitiveType.Cylinder, "FlagPole", root, c + new Vector3(0f, topY + 3.5f, 2f), new Vector3(0.1f, 3.5f, 0.1f),
                         Art.Solid(new Color(0.9f, 0.9f, 0.95f), 0.6f, 0.6f), Art.Col.None);
                Box("Flag", c + new Vector3(0.7f, topY + 6.2f, 2f), new Vector3(1.4f, 0.8f, 0.06f), flagRed);
            }

            PowerStar.Build(root, c + Vector3.up * (topY + 1.5f), "פסגת המגדל");
            Coins(c + new Vector3(0f, 0.8f, -6.5f), c + new Vector3(-1.5f, 0.8f, -7f), c + new Vector3(1.5f, 0.8f, -7f));

            // Spring up to a high ledge with a red coin
            SpringPad.Build(root, new Vector3(10f, 0f, 7.5f), 21f);
            KBox("block-grass-low-large", "SpringLedge", new Vector3(10f, 5.7f, 11.5f), new Vector3(4f, 0.6f, 4f), checker);
            Coin.Build(root, new Vector3(10f, 6.8f, 11.5f), true);
            Coins(new Vector3(10f, 2.5f, 7.5f), new Vector3(10f, 4f, 7.5f));
        }

        // ---------- star 2: sky course to the east island ----------

        static void BuildSkyCourse()
        {
            // P1: stone pillar rising from the sea
            KBox("block-grass-large-tall", "Pillar1", new Vector3(38f, -1.5f, 0f), new Vector3(3f, 3.6f, 3f), stone);
            Coins(new Vector3(38f, 1.1f, 0f));

            // P2: platform sliding north-south
            var p2 = Mover.Create(root, "Slider", new Vector3(43f, 0.8f, -5f), new Vector3(43f, 0.8f, 5f), 5f);
            KBox("block-moving-large", "SliderTop", Vector3.zero, new Vector3(3f, 0.5f, 3f), checker, p2.transform);
            Coins(new Vector3(43f, 1.85f, -3f), new Vector3(43f, 1.85f, 0f), new Vector3(43f, 1.85f, 3f));
            Coin.Build(root, new Vector3(43f, 2.3f, 5f), true);

            // P3: spinning cross
            Art.Prim(PrimitiveType.Cylinder, "SpinnerPost", root, new Vector3(51f, -1.4f, 0f), new Vector3(1.4f, 2.6f, 1.4f), stone, Art.Col.Mesh);
            var spinner = Mover.Create(root, "Spinner", new Vector3(51f, 1.2f, 0f), new Vector3(51f, 1.2f, 0f), 1f, 35f);
            Art.Prim(PrimitiveType.Cylinder, "Hub", spinner.transform, Vector3.zero, new Vector3(2.6f, 0.3f, 2.6f), plank, Art.Col.Mesh);
            KBox("platform", "PlankA", new Vector3(0f, 0.1f, 0f), new Vector3(9f, 0.4f, 1.6f), checker, spinner.transform);
            KBox("platform", "PlankB", new Vector3(0f, 0.1f, 0f), new Vector3(1.6f, 0.4f, 9f), checker, spinner.transform);
            Coin.Build(root, new Vector3(51f, 2.3f, 0f), true);

            // P4: elevator
            var p4 = Mover.Create(root, "Elevator", new Vector3(59f, -0.2f, 0f), new Vector3(59f, 3.2f, 0f), 4f);
            KBox("block-moving-large", "ElevatorTop", Vector3.zero, new Vector3(3f, 0.5f, 3f), plank, p4.transform);
            Coins(new Vector3(59f, 4.3f, 0f));

            // East island
            Checkpoint.Build(root, new Vector3(63.5f, 2f, -2.5f), 90f);
            Grumble.Build(root, new Vector3(70f, 2f, 4f));
            CoinRing(new Vector3(70f, 2.8f, 0f), 3f, 6);
            KBox("block-grass", "Pedestal", new Vector3(74f, 2.5f, 0f), new Vector3(2f, 1f, 2f), stone);
            PowerStar.Build(root, new Vector3(74f, 4.4f, 0f), "גשר השמיים");
            Tree(new Vector3(66f, 2f, 6f), 1f);
            Tree(new Vector3(75f, 2f, -5f), 1.1f);
            PineTree(new Vector3(72f, 2f, 6.5f), 0.9f);
        }

        // ---------- star 3: meadow, Grumbles and red coins ----------

        static void BuildMeadow(LevelInfo info)
        {
            var m = MeadowCenter;
            // Fence ring with an opening facing east
            const int segs = 22;
            const float fr = 11f;
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * 360f / segs, a1 = (i + 1) * 360f / segs;
                float mid = (a0 + a1) * 0.5f;
                if (Mathf.Abs(Mathf.DeltaAngle(mid, 0f)) < 25f) continue;
                var p0 = m + Polar(a0, fr);
                var p1 = m + Polar(a1, fr);
                Vector3 mp = (p0 + p1) * 0.5f;
                float yaw = -Mathf.Atan2(p1.z - p0.z, p1.x - p0.x) * Mathf.Rad2Deg;
                if (Props.Has("fence-straight"))
                {
                    // The Kenney fence sits 0.42 units off its pivot along +z; shift it back onto the ring.
                    var rot = Quaternion.Euler(0f, yaw, 0f);
                    Props.Spawn("fence-straight", root, mp - rot * new Vector3(0f, 0f, 0.42f * 2.6f),
                                new Vector3((p1 - p0).magnitude, 2.6f, 2.6f), yaw, Props.Col.Box);
                    continue;
                }
                Box("Post", p0 + Vector3.up * 0.55f, new Vector3(0.25f, 1.1f, 0.25f), plank);
                Box("Rail", mp + Vector3.up * 0.75f, new Vector3((p1 - p0).magnitude, 0.15f, 0.1f), plank, null, yaw);
                Box("Rail", mp + Vector3.up * 0.4f, new Vector3((p1 - p0).magnitude, 0.15f, 0.1f), plank, null, yaw);
            }

            Grumble.Build(root, m + new Vector3(4f, 0f, -4f));
            Grumble.Build(root, m + new Vector3(-4f, 0f, 4f));
            Grumble.Build(root, m + new Vector3(-2f, 0f, -8f));
            Grumble.Build(root, m + new Vector3(2f, 0f, 6f));
            CoinRing(m + Vector3.up * 0.8f, 6.5f, 8);

            // Rock with a red coin
            if (Props.Has("rocks")) Props.Spawn("rocks", root, m + new Vector3(0f, 0f, 7f), 3.5f, 20f, Props.Col.Box);
            else Art.Prim(PrimitiveType.Sphere, "MeadowRock", root, m + new Vector3(0f, 0.2f, 7f), new Vector3(2.5f, 2f, 2.5f), stone, Art.Col.Mesh);
            Coin.Build(root, m + new Vector3(0f, 2.2f, 7f), true);

            info.RedCoinStar = PowerStar.Build(root, m + new Vector3(0f, 1.8f, -1f), "אוצר האחו", true);
        }

        // ---------- south side: blocks, mound, pillars, more red coins ----------

        static void BuildSouthSide()
        {
            // Welcome coins
            CoinRing(new Vector3(0f, 0.8f, -13f), 3f, 8);
            CoinLine(new Vector3(0f, 0.8f, -7f), new Vector3(0f, 0.8f, 5f), 7);

            // Question block row
            for (int i = 0; i < 5; i++)
            {
                var p = new Vector3(6f + i, 2.9f, -14f);
                if (i % 2 == 0) QuestionBlock.Build(root, p);
                else KBox("brick", "BrickBlock", p, Vector3.one, brickBlock);
            }
            KBox("block-grass", "BlockStep", new Vector3(4f, 0.6f, -16f), new Vector3(1.5f, 1.2f, 1.5f), stone);
            Coin.Build(root, new Vector3(8f, 4.2f, -14f), true);

            // Grassy mound
            Art.Prim(PrimitiveType.Sphere, "Mound", root, new Vector3(16f, -0.6f, -10f), new Vector3(12f, 3.4f, 12f), Props.Has("tree") ? Art.Solid(new Color(90f / 255f, 196f / 255f, 135f / 255f), 0.05f) : grass, Art.Col.Mesh);
            Coins(new Vector3(16f, 1.9f, -10f), new Vector3(14f, 1.6f, -10f), new Vector3(18f, 1.6f, -10f));
            Grumble.Build(root, new Vector3(7f, 0f, -3f));

            // Stepping pillars near the tower
            KBox("block-grass-large-tall", "StepPillar1", new Vector3(-5f, 0.6f, 24f), new Vector3(1.8f, 1.2f, 1.8f), stone);
            KBox("block-grass-large-tall", "StepPillar2", new Vector3(-8f, 1.2f, 25.5f), new Vector3(1.8f, 2.4f, 1.8f), stone);
            KBox("block-grass-large-tall", "StepPillar3", new Vector3(-11f, 1.8f, 26f), new Vector3(1.8f, 3.6f, 1.8f), stone);
            Coin.Build(root, new Vector3(-11f, 4.4f, 26f), true);

            // Red coins hidden around the island
            Coin.Build(root, new Vector3(22f, 1f, -21f), true);
            Coin.Build(root, new Vector3(-27f, 1f, -10f), true);

            // Beach coins
            CoinLine(new Vector3(-10f, 0.8f, -30f), new Vector3(10f, 0.8f, -30f), 6);
            if (Props.Has("sign"))
            {
                Props.Spawn("sign", root, new Vector3(3.5f, 0f, -19f), 2.6f, 200f, Props.Col.Box);
                Props.Spawn("barrel", root, new Vector3(-4f, 0f, -18f), 2.2f, 0f, Props.Col.Box);
                Props.Spawn("crate", root, new Vector3(-5.3f, 0f, -18.6f), 2.2f, 25f, Props.Col.Box);
                Props.Spawn("chest", root, MeadowCenter + new Vector3(-6f, 0f, 0f), 2.2f, 90f, Props.Col.Box);
            }
        }

        // ---------- decoration ----------

        static void BuildDecor()
        {
            var rng = new System.Random(7);
            var zones = new List<Vector4> // x, z, radius
            {
                new Vector4(0f, -22f, 5f), new Vector4(0f, -13f, 5f), new Vector4(0f, -1f, 3f), new Vector4(0f, 14f, 8f),
                new Vector4(10f, 9f, 5f), new Vector4(8f, -14f, 5f), new Vector4(-16f, 0f, 12.5f), new Vector4(-8f, 25f, 5f),
                new Vector4(31f, 0f, 6f), new Vector4(16f, -10f, 7f), new Vector4(22f, -21f, 3f), new Vector4(-27f, -10f, 3f),
                new Vector4(12f, -3f, 3f),
            };
            var placed = new List<Vector3>();
            for (int tries = 0; tries < 400 && placed.Count < 26; tries++)
            {
                float a = (float)rng.NextDouble() * 360f;
                float r = 8f + (float)rng.NextDouble() * 21f;
                var p = Polar(a, r);
                if (Blocked(p, zones, 0f)) continue;
                bool near = false;
                foreach (var q in placed) if ((q - p).sqrMagnitude < 16f) { near = true; break; }
                if (near) continue;
                placed.Add(p);
                if (placed.Count % 3 == 0) PineTree(p, 0.8f + (float)rng.NextDouble() * 0.5f);
                else Tree(p, 0.8f + (float)rng.NextDouble() * 0.5f);
            }

            Color[] petals = { new Color(1f, 0.35f, 0.4f), new Color(1f, 0.9f, 0.3f), Color.white, new Color(0.85f, 0.5f, 1f), new Color(1f, 0.6f, 0.2f) };
            var center = Art.Solid(new Color(1f, 0.85f, 0.2f));
            for (int i = 0; i < 170; i++)
            {
                var p = Polar((float)rng.NextDouble() * 360f, 4f + (float)rng.NextDouble() * 27f);
                if (Blocked(p, zones, -2f) && i % 3 != 0) continue;
                var col = petals[rng.Next(petals.Length)];
                if (Props.Has("flowers"))
                {
                    string[] kinds = { "flowers", "flowers-tall", "grass", "grass", "mushrooms" };
                    Props.Spawn(kinds[rng.Next(kinds.Length)], root, p, 1.8f + (float)rng.NextDouble() * 0.6f,
                                (float)rng.NextDouble() * 360f, Props.Col.None, false);
                    continue;
                }
                Art.Prim(PrimitiveType.Sphere, "Flower", root, p + Vector3.up * 0.12f, new Vector3(0.32f, 0.1f, 0.32f), Art.Solid(col, 0.2f), Art.Col.None, null, false);
                Art.Prim(PrimitiveType.Sphere, "FlowerHeart", root, p + Vector3.up * 0.17f, Vector3.one * 0.12f, center, Art.Col.None, null, false);
            }

            for (int i = 0; i < 10; i++)
            {
                var p = Polar(i * 36f + 10f, 30.5f);
                if (Blocked(p, zones, 0f)) continue;
                if (Props.Has("rocks")) Props.Spawn(i % 2 == 0 ? "rocks" : "tree-pine-small", root, p, 2.6f, i * 47f, Props.Col.Box);
                else Art.Prim(PrimitiveType.Sphere, "Bush", root, p, new Vector3(2.4f, 1.6f, 2.4f), leaves, Art.Col.None);
            }

            var cloudMat = Art.Solid(new Color(1f, 1f, 1f), 0f);
            for (int i = 0; i < (Shader.Find("Maya/Sky") != null ? 0 : 14); i++)
            {
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(root, false);
                cloud.localPosition = new Vector3(-250f + (float)rng.NextDouble() * 500f, 32f + (float)rng.NextDouble() * 18f,
                                                  -150f + (float)rng.NextDouble() * 300f);
                cloud.gameObject.AddComponent<Cloud>().Speed = 1f + (float)rng.NextDouble() * 1.5f;
                int puffs = 3 + rng.Next(3);
                for (int k = 0; k < puffs; k++)
                    Art.Prim(PrimitiveType.Sphere, "Puff", cloud, new Vector3(k * 3.2f - puffs * 1.6f, (float)rng.NextDouble() * 1.2f, (float)rng.NextDouble() * 2f),
                             Vector3.one * (4f + (float)rng.NextDouble() * 3f), cloudMat, Art.Col.None, null, false);
            }

            // Pebble path from the start toward the tower
            var path = Art.Solid(new Color(0.85f, 0.8f, 0.7f), 0.1f);
            for (int i = 0; i < 14; i++)
            {
                float z = -19f + i * 2f;
                var pp = new Vector3((i % 2 == 0 ? -0.6f : 0.6f) + 1.6f, 0.02f, z);
                if (Props.Has("stones")) Props.Spawn("stones", root, pp, 2f, i * 70f, Props.Col.None, false);
                else Art.Prim(PrimitiveType.Cylinder, "Pebble", root, pp, new Vector3(1.1f, 0.02f, 0.9f), path, Art.Col.None, null, false);
            }
        }

        static bool Blocked(Vector3 p, List<Vector4> zones, float pad)
        {
            foreach (var z in zones)
            {
                float dx = p.x - z.x, dz = p.z - z.y, r = z.z + pad;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        internal static void Tree(Vector3 p, float s)
        {
            if (KenneyTree("tree", p, 2.5f * s)) return;
            var t = new GameObject("Tree").transform;
            t.SetParent(root, false);
            t.localPosition = p;
            Art.Prim(PrimitiveType.Cylinder, "Trunk", t, Vector3.up * 1.2f * s, new Vector3(0.5f, 1.2f, 0.5f) * s, bark);
            Art.Prim(PrimitiveType.Sphere, "Leaves", t, Vector3.up * 3.1f * s, Vector3.one * 2.8f * s, leaves, Art.Col.None);
            Art.Prim(PrimitiveType.Sphere, "Leaves", t, new Vector3(0.8f, 2.6f, 0.3f) * s, Vector3.one * 1.9f * s, leaves, Art.Col.None);
            Art.Prim(PrimitiveType.Sphere, "Leaves", t, new Vector3(-0.7f, 2.7f, -0.4f) * s, Vector3.one * 2f * s, leaves, Art.Col.None);
        }

        internal static bool KenneyTree(string model, Vector3 p, float scale)
        {
            if (!Props.Has(model)) return false;
            var go = Props.Spawn(model, root, p, scale, (p.x * 31f + p.z * 17f) % 360f);
            go.AddComponent<Sway>();
            var trunk = go.AddComponent<BoxCollider>(); // trunk only, so the crown does not block jumps
            trunk.center = new Vector3(0f, 0.4f, 0f);
            trunk.size = new Vector3(0.22f, 0.8f, 0.22f);
            // Camera-only blocker for the crown (trigger on TransparentFX, which only the camera probes).
            var crown = new GameObject("CameraBlocker") { layer = 1 };
            crown.transform.SetParent(go.transform, false);
            var sphere = crown.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.center = new Vector3(0f, model == "tree" ? 1.3f : 1.1f, 0f);
            sphere.radius = model == "tree" ? 0.6f : 0.5f;
            return true;
        }

        internal static void PineTree(Vector3 p, float s)
        {
            if (KenneyTree("tree-pine", p, 2.6f * s)) return;
            var t = new GameObject("Pine").transform;
            t.SetParent(root, false);
            t.localPosition = p;
            Art.Prim(PrimitiveType.Cylinder, "Trunk", t, Vector3.up * 0.8f * s, new Vector3(0.45f, 0.8f, 0.45f) * s, bark);
            var needles = Art.Solid(new Color(0.15f, 0.45f, 0.25f), 0.1f);
            for (int i = 0; i < 3; i++)
                Art.MeshObject("Cone", Art.ConeMesh(), t, Vector3.up * (1.4f + i * 1.1f) * s, new Vector3(3.2f - i * 0.8f, 2.2f, 3.2f - i * 0.8f) * s, needles);
        }

        internal static void PalmTree(Vector3 p)
        {
            var t = new GameObject("Palm").transform;
            t.SetParent(root, false);
            t.localPosition = p;
            for (int i = 0; i < 5; i++)
                Art.Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(i * 0.12f, 0.4f + i * 0.8f, 0f), new Vector3(0.4f - i * 0.03f, 0.42f, 0.4f - i * 0.03f), bark);
            for (int i = 0; i < 6; i++)
                Art.Prim(PrimitiveType.Cube, "Frond", t, new Vector3(0.6f, 4.1f, 0f) + Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(1.1f, 0f, 0f),
                         new Vector3(2.4f, 0.08f, 0.6f), leaves, Art.Col.None, Quaternion.Euler(0f, i * 60f, -18f));
            var nut = Art.Solid(new Color(0.4f, 0.26f, 0.12f), 0.3f);
            for (int i = 0; i < 3; i++)
                Art.Prim(PrimitiveType.Sphere, "Coconut", t, new Vector3(0.6f, 3.8f, 0f) + Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0.3f, 0f, 0f),
                         Vector3.one * 0.32f, nut, Art.Col.None);
        }

        /// <summary>Same point, dropped onto the highest collider below <paramref name="fromHeight"/>.</summary>
        internal static Vector3 Snap(Vector3 p, float fromHeight = 40f)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(p.x, fromHeight, p.z), Vector3.down, out var hit, fromHeight + 60f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                ? new Vector3(p.x, hit.point.y, p.z)
                : p;
        }

        internal static Vector3 Polar(float degrees, float radius)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }
    }
}
