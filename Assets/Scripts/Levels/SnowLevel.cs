using UnityEngine;
using static MayaAdventure.LevelBuilder;

namespace MayaAdventure
{
    /// <summary>
    /// Level 2 "Snowy Peaks": a stepped snow mountain (star 1), a frozen lake crossed on
    /// moving ice blocks to a tall ice island (star 2), and a snowball arena (star 3, defeat everyone).
    /// </summary>
    public static class SnowLevel
    {
        static readonly Color Snow = new Color(0.93f, 0.96f, 1f);
        static readonly Vector3 Peak = new Vector3(0f, 0f, 8f);
        static readonly Vector3 Arena = new Vector3(-18f, 0f, -4f);

        public static LevelInfo Build()
        {
            Begin("Level 2 - Snowy Peaks");
            cover = Nature.Cover.Snow;
            var info = new LevelInfo
            {
                Name = Levels.Names[1],
                Theme = LevelTheme.Snow(),
                Root = root,
                Start = new Vector3(0f, 0f, -22f),
                StartYaw = 0f,
                ReturnSpawn = new Vector3(-5f, 0f, -24f),
                ReturnYaw = 90f,
                WaterY = -1f,
                KillY = -30f,
                Ambience = AmbienceKind.Snow,
            };

            const string tile = "block-snow-large-tall";
            TileIsland("SnowIsland", Vector3.zero, 30f, 0f, tile, Snow, -10f);
            Water(-1f, -10f, info.Theme);

            Portal.Build(root, new Vector3(8f, 0f, -24f), 90f, 0, 0);
            Portal.Build(root, new Vector3(-8f, 0f, -24f), 90f, 2, Levels.Required[2]);

            BuildMountain();
            BuildLake();
            BuildArena();
            BuildDecor();
            Zones.Snow(Snow);
            DistantIslands(Snow, 8, 150f, 230f, tile, "tree-pine-snow");
            return info;
        }

        // ---------- star 1: stepped mountain ----------

        static void BuildMountain()
        {
            float[] radii = { 15f, 11.5f, 8f, 5f, 2.5f };
            for (int k = 0; k < radii.Length; k++)
            {
                float top = 1.4f * (k + 1);
                TileIsland($"Tier{k + 1}", Peak, radii[k], top, "block-snow-large-tall", Snow, float.NaN);
                // a ring of coins on every tier
                int count = 6 + k;
                for (int i = 0; i < count; i++)
                {
                    var p = Snap(Peak + Polar(i * 360f / count + k * 17f, radii[k] - 1.2f));
                    if (k < radii.Length - 1) Coin.Build(root, p + Vector3.up * 0.8f);
                }
                if (k < 3)
                {
                    for (int t = 0; t < 3; t++)
                        KenneyTree(t % 2 == 0 ? "tree-pine-snow" : "tree-snow",
                                   Snap(Peak + Polar(40f + t * 120f + k * 50f, radii[k] - 2.2f)), 2.3f);
                }
            }
            Grumble.Build(root, Snap(Peak + Polar(300f, 9.7f)));
            Grumble.Build(root, Snap(Peak + Polar(120f, 6.5f)));
            var summit = Snap(Peak);
            PowerStar.Build(root, summit + Vector3.up * 1.6f, "פסגת השלג");
            if (Props.Has("flag")) Props.Spawn("flag", root, Snap(Peak + new Vector3(1.2f, 0f, 0.8f)), 3.2f, 30f);
        }

        // ---------- star 2: frozen lake crossing ----------

        static void BuildLake()
        {
            KBox("block-snow-low-large", "Ice1", new Vector3(36f, 0.05f, 0f), new Vector3(3.5f, 0.5f, 3.5f), stone);
            Coin.Build(root, new Vector3(36f, 1.1f, 0f));

            var slider = Mover.Create(root, "IceSlider", new Vector3(41.5f, 1.2f, -4f), new Vector3(41.5f, 1.2f, 4f), 4.5f);
            KBox("block-moving-blue", "IceSliderTop", Vector3.zero, new Vector3(3f, 0.5f, 3f), checker, slider.transform);
            CoinLine(new Vector3(41.5f, 2.2f, -3f), new Vector3(41.5f, 2.2f, 3f), 3);

            var lift = Mover.Create(root, "IceLift", new Vector3(47f, 1.5f, 0f), new Vector3(47f, 5.2f, 0f), 4f, 0f, 1f);
            KBox("block-moving-blue", "IceLiftTop", Vector3.zero, new Vector3(3f, 0.5f, 3f), checker, lift.transform);

            TileIsland("IceIsland", new Vector3(54f, 0f, 0f), 5f, 5.5f, "block-snow-large-tall", Snow, -10f);
            Checkpoint.Build(root, new Vector3(52f, 5.5f, -2f), -90f);
            PowerStar.Build(root, new Vector3(55f, 7.1f, 0f), "האגם הקפוא");
            KenneyTree("tree-pine-snow", new Vector3(55.5f, 5.5f, 3f), 2.4f);
        }

        // ---------- star 3: snowball arena ----------

        static void BuildArena()
        {
            const int segs = 20;
            const float r = 9f;
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * 360f / segs, a1 = (i + 1) * 360f / segs;
                if (Mathf.Abs(Mathf.DeltaAngle((a0 + a1) * 0.5f, 0f)) < 20f) continue; // entrance toward the start
                var p0 = Arena + Polar(a0, r);
                var p1 = Arena + Polar(a1, r);
                var mid = (p0 + p1) * 0.5f;
                float yaw = -Mathf.Atan2(p1.z - p0.z, p1.x - p0.x) * Mathf.Rad2Deg;
                if (Props.Has("fence-straight"))
                {
                    var rot = Quaternion.Euler(0f, yaw, 0f);
                    Props.Spawn("fence-straight", root, mid - rot * new Vector3(0f, 0f, 0.42f * 2.6f),
                                new Vector3((p1 - p0).magnitude, 2.6f, 2.6f), yaw, Props.Col.Box);
                }
                else
                {
                    Box("Rail", mid + Vector3.up * 0.6f, new Vector3((p1 - p0).magnitude, 1.2f, 0.2f), plank, null, yaw);
                }
            }
            // The giant Snowman guards the arena's star.
            var star = PowerStar.Build(root, Arena + Vector3.up * 1.8f, "קרב כדורי השלג", true);
            GiantBoss.Build(root, Arena, GiantBoss.Kind.Snowman, star);
            CoinRing(Arena + Vector3.up * 0.8f, 6.5f, 10);
        }

        // ---------- decoration ----------

        static void BuildDecor()
        {
            var rng = new System.Random(5);
            for (int i = 0; i < 40; i++)
            {
                var p = Polar((float)rng.NextDouble() * 360f, 18f + (float)rng.NextDouble() * 10f);
                if ((p - Arena).sqrMagnitude < 12f * 12f || p.z < -17f) continue;
                if (i % 3 == 0) KenneyTree("tree-pine-snow", p, 2.2f + (float)rng.NextDouble() * 0.8f);
                else if (i % 3 == 1) KenneyTree("tree-snow", p, 2.2f + (float)rng.NextDouble() * 0.6f);
                else if (Props.Has("rocks")) Props.Spawn("rocks", root, p, 2.5f, (float)rng.NextDouble() * 360f, Props.Col.Box);
            }
            Snowman(new Vector3(4f, 0f, -16f), 200f);
            Snowman(new Vector3(-12f, 0f, -14f), 140f);
            Snowman(new Vector3(20f, 0f, 14f), 250f);
            CoinLine(new Vector3(0f, 0.8f, -18f), new Vector3(0f, 0.8f, -9f), 5);
        }

        static void Snowman(Vector3 p, float yaw)
        {
            var t = new GameObject("Snowman").transform;
            t.SetParent(root, false);
            t.localPosition = p;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var white = Art.Solid(new Color(0.97f, 0.98f, 1f), 0.25f);
            var coal = Art.Solid(new Color(0.08f, 0.08f, 0.1f), 0.4f);
            Art.Prim(PrimitiveType.Sphere, "Base", t, new Vector3(0f, 0.6f, 0f), Vector3.one * 1.3f, white);
            Art.Prim(PrimitiveType.Sphere, "Body", t, new Vector3(0f, 1.45f, 0f), Vector3.one * 0.95f, white, Art.Col.None);
            Art.Prim(PrimitiveType.Sphere, "Head", t, new Vector3(0f, 2.1f, 0f), Vector3.one * 0.65f, white, Art.Col.None);
            Art.Prim(PrimitiveType.Sphere, "EyeL", t, new Vector3(-0.11f, 2.18f, 0.29f), Vector3.one * 0.08f, coal, Art.Col.None);
            Art.Prim(PrimitiveType.Sphere, "EyeR", t, new Vector3(0.11f, 2.18f, 0.29f), Vector3.one * 0.08f, coal, Art.Col.None);
            Art.MeshObject("Nose", Art.ConeMesh(), t, new Vector3(0f, 2.08f, 0.3f), new Vector3(0.12f, 0.35f, 0.12f),
                           Art.Solid(new Color(1f, 0.5f, 0.1f), 0.3f), Art.Col.None, Quaternion.Euler(90f, 0f, 0f));
            Art.Prim(PrimitiveType.Cylinder, "Hat", t, new Vector3(0f, 2.55f, 0f), new Vector3(0.45f, 0.2f, 0.45f), coal, Art.Col.None);
            Art.Prim(PrimitiveType.Cylinder, "Brim", t, new Vector3(0f, 2.38f, 0f), new Vector3(0.7f, 0.02f, 0.7f), coal, Art.Col.None);
        }
    }
}
