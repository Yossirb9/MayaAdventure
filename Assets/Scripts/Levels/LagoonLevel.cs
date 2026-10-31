using UnityEngine;
using static MayaAdventure.LevelBuilder;

namespace MayaAdventure
{
    /// <summary>
    /// Level 3 "Coral Lagoon": a swimming level. Star 1 rests on a sunken treasure on the sea floor,
    /// star 2 hides in an underwater coral cave, star 3 tops a tall island reached by springs and lifts.
    /// </summary>
    public static class LagoonLevel
    {
        static readonly Color Sand = new Color(0.98f, 0.9f, 0.66f);
        const float SeaLevel = 0f;
        const float Floor = -12f;

        public static LevelInfo Build()
        {
            Begin("Level 3 - Coral Lagoon");
            cover = Nature.Cover.Beach;
            var info = new LevelInfo
            {
                Name = Levels.Names[2],
                Theme = LevelTheme.Lagoon(),
                Root = root,
                Start = new Vector3(0f, 1f, -24f),
                StartYaw = 0f,
                ReturnSpawn = new Vector3(-5f, 1f, -26f),
                ReturnYaw = 90f,
                WaterY = SeaLevel,
                KillY = -30f,
                Ambience = AmbienceKind.Motes,
            };

            TileIsland("StartIsland", new Vector3(0f, 0f, -24f), 10f, 1f, "block-grass-large-tall", Sand, Floor);
            Water(SeaLevel, Floor, info.Theme);
            Portal.Build(root, new Vector3(7f, 1f, -26f), 90f, 1, 0);
            Portal.Build(root, new Vector3(-8f, 1f, -26f), 90f, 3, Levels.Required[3]);

            PalmTree(new Vector3(4f, 1f, -19f));
            PalmTree(new Vector3(-5f, 1f, -18f));
            CoinLine(new Vector3(0f, 1.8f, -20f), new Vector3(0f, 1.8f, -16f), 3);

            BuildSunkenTreasure();
            BuildCoralCave();
            BuildTallIsland();
            BuildReef();
            Zones.Lagoon(Sand, SeaLevel, Floor);
            DistantIslands(Sand, 10, 150f, 240f);
            return info;
        }

        // ---------- star 1: sunken treasure ----------

        static void BuildSunkenTreasure()
        {
            var c = new Vector3(18f, Floor, -6f);
            if (Props.Has("chest"))
            {
                Props.Spawn("chest", root, c, 3f, -30f, Props.Col.Box);
                Props.Spawn("barrel", root, c + new Vector3(2.5f, 0f, 1f), 2.4f, 0f, Props.Col.Box);
                Props.Spawn("crate", root, c + new Vector3(-2.2f, 0f, 1.5f), 2.4f, 20f, Props.Col.Box);
                Props.Spawn("crate", root, c + new Vector3(-2.4f, 1.2f, 1.6f), 2.2f, 50f, Props.Col.Box);
            }
            PowerStar.Build(root, c + Vector3.up * 2.6f, "האוצר השקוע");
            // A trail of coins spiralling down to it (coins also restore breath, like in SM64).
            for (int i = 0; i < 10; i++)
                Coin.Build(root, Vector3.Lerp(new Vector3(8f, SeaLevel - 0.8f, -16f), c + Vector3.up * 1.5f, i / 9f) + Polar(i * 60f, 1.2f));
        }

        // ---------- star 2: coral cave ----------

        static void BuildCoralCave()
        {
            var c = new Vector3(-22f, Floor, 6f);
            var rockMat = Art.Textured("rock", new Vector2(2f, 2f), 0.05f);
            // Cave: two walls and a roof of rock, open at both ends along z.
            Box("CaveWallL", c + new Vector3(-3.5f, 3f, 0f), new Vector3(2f, 6f, 14f), rockMat);
            Box("CaveWallR", c + new Vector3(3.5f, 3f, 0f), new Vector3(2f, 6f, 14f), rockMat);
            Box("CaveRoof", c + new Vector3(0f, 6.5f, 0f), new Vector3(9f, 1.2f, 14f), rockMat);
            Box("CaveBack", c + new Vector3(0f, 3f, 7.5f), new Vector3(9f, 7f, 1.5f), rockMat);
            PowerStar.Build(root, c + new Vector3(0f, 2f, 4.5f), "מערת האלמוגים");
            CoinLine(c + new Vector3(0f, 2f, -12f), c + new Vector3(0f, 2f, 2f), 7);
            if (Props.Has("jewel"))
                for (int i = 0; i < 6; i++)
                {
                    var j = Props.Spawn("jewel", root, c + new Vector3(i % 2 == 0 ? -2.3f : 2.3f, 0f, -5f + i * 2f), 2.5f, i * 40f);
                    Props.Tint(j, i % 3 == 0 ? new Color(1f, 0.5f, 1.4f) : new Color(0.5f, 1.4f, 1.6f));
                }
        }

        // ---------- star 3: tall island ----------

        static void BuildTallIsland()
        {
            // Stepping islands -> spring -> lift -> summit
            TileIsland("Step1", new Vector3(-6f, 0f, -6f), 4f, 1.2f, "block-grass-large-tall", Sand, Floor);
            TileIsland("Step2", new Vector3(4f, 0f, 6f), 4f, 2.2f, "block-grass-large-tall", Sand, Floor);
            CoinRing(new Vector3(-6f, 2f, -6f), 1.6f, 5);
            SpringPad.Build(root, new Vector3(4f, 2.2f, 7f), 20f);
            KBox("block-grass-low-large", "Ledge", new Vector3(4f, 7.7f, 13f), new Vector3(4f, 0.6f, 4f), checker);
            Coin.Build(root, new Vector3(4f, 8.8f, 13f));

            var lift = Mover.Create(root, "Lift", new Vector3(4f, 7.8f, 19f), new Vector3(4f, 12.5f, 19f), 4.5f);
            KBox("block-moving-large", "LiftTop", Vector3.zero, new Vector3(3f, 0.5f, 3f), plank, lift.transform);

            TileIsland("Summit", new Vector3(4f, 0f, 27f), 5f, 12.5f, "block-grass-large-tall", Sand, Floor);
            Checkpoint.Build(root, new Vector3(2f, 12.5f, 25f), 180f);
            PalmTree(new Vector3(6f, 12.5f, 29f));
            PowerStar.Build(root, new Vector3(4f, 14.1f, 28f), "פסגת הלגונה");
            Grumble.Build(root, new Vector3(5f, 2.2f, 4.5f));
        }

        // ---------- reef decoration ----------

        static void BuildReef()
        {
            var rng = new System.Random(3);
            Color[] coral = { new Color(1.4f, 0.45f, 0.6f), new Color(1.5f, 0.7f, 0.3f), new Color(0.7f, 0.5f, 1.5f), new Color(0.4f, 1.3f, 1.2f) };
            string[] kinds = { "flowers-tall", "mushrooms", "plant", "grass", "rocks" };
            for (int i = 0; i < 110; i++)
            {
                var p = Polar((float)rng.NextDouble() * 360f, 6f + (float)rng.NextDouble() * 55f);
                p.y = Floor;
                string kind = kinds[rng.Next(kinds.Length)];
                if (!Props.Has(kind)) continue;
                var go = Props.Spawn(kind, root, p, kind == "rocks" ? 3f + (float)rng.NextDouble() * 3f : 3.5f + (float)rng.NextDouble() * 4f,
                                     (float)rng.NextDouble() * 360f, Props.Col.None, false);
                if (kind != "rocks") Props.Tint(go, coral[rng.Next(coral.Length)]);
            }
            // Floating coins over the open water to tempt swimming.
            CoinRing(new Vector3(14f, SeaLevel + 0.6f, -18f), 3f, 6);
            CoinRing(new Vector3(-14f, SeaLevel - 3f, -10f), 2.5f, 6);
        }
    }
}
