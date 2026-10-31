using UnityEngine;
using static MayaAdventure.LevelBuilder;

namespace MayaAdventure
{
    /// <summary>
    /// Level 4 "Sky Fortress": platforms floating above a sea of clouds (falling costs power).
    /// Star 1 at the end of a saw-and-spike bridge, star 2 on top of a tower reached by conveyor
    /// belts, springs and lifts, star 3 appears after clearing the guards on the west keep.
    /// </summary>
    public static class SkyFortressLevel
    {
        static Material Stone => Art.Textured("brick", new Vector2(2f, 2f), 0.12f);

        public static LevelInfo Build()
        {
            Begin("Level 4 - Sky Fortress");
            var info = new LevelInfo
            {
                Name = Levels.Names[3],
                Theme = LevelTheme.Sky(),
                Root = root,
                Start = new Vector3(0f, 0f, -20f),
                StartYaw = 0f,
                ReturnSpawn = new Vector3(-3.5f, 0f, -23f),
                ReturnYaw = 90f,
                KillY = -18f,
                Ambience = AmbienceKind.Motes,
            };

            Deck("StartDeck", new Vector3(0f, 0f, -20f), new Vector3(16f, 1.5f, 12f));
            Portal.Build(root, new Vector3(5.5f, 0f, -23f), 90f, 2, 0);
            Portal.Build(root, new Vector3(-6.5f, 0f, -23f), 90f, 4, Levels.Required[4]);
            Deck("Hub", new Vector3(0f, 0f, 0f), new Vector3(14f, 1.5f, 14f));
            Bridge(new Vector3(0f, 0f, -14f), new Vector3(0f, 0f, -7f), 4f);
            CoinLine(new Vector3(0f, 0.8f, -12f), new Vector3(0f, 0.8f, -8f), 3);
            Decorate(new Vector3(0f, 0f, 0f));

            BuildSawBridge();
            BuildTower();
            BuildKeep();
            Zones.Fortress((n, c, s) => Deck(n, c, s), KenneyGrass);
            CloudSea();
            return info;
        }

        static GameObject Deck(string name, Vector3 topCenter, Vector3 size)
        {
            var deck = KBox("platform-fortified", name, topCenter - Vector3.up * (size.y * 0.5f), size, Stone);
            // A stone underside hanging below, so the platforms look like floating keeps.
            Art.MeshObject("Underside", Art.ConeMesh(), root, topCenter - Vector3.up * size.y, new Vector3(size.x * 1.25f, size.y * 3f, size.z * 1.25f),
                           Art.Textured("rock", new Vector2(2f, 1f), 0.05f), Art.Col.None, Quaternion.Euler(180f, 0f, 0f));
            return deck;
        }

        static void Bridge(Vector3 from, Vector3 to, float width)
        {
            var d = to - from;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            KBox("platform", "Bridge", (from + to) * 0.5f - Vector3.up * 0.2f, new Vector3(width, 0.4f, d.magnitude), plank, null, yaw);
        }

        static void Decorate(Vector3 c)
        {
            if (!Props.Has("barrel")) return;
            Props.Spawn("barrel", root, c + new Vector3(5f, 0f, 5f), 2.2f, 0f, Props.Col.Box);
            Props.Spawn("crate", root, c + new Vector3(-5f, 0f, 5f), 2.2f, 15f, Props.Col.Box);
            Props.Spawn("flag", root, c + new Vector3(6f, 0f, -6f), 4f, 0f);
            Props.Spawn("flag", root, c + new Vector3(-6f, 0f, -6f), 4f, 180f);
        }

        // ---------- star 1: saw bridge (east) ----------

        static void BuildSawBridge()
        {
            Bridge(new Vector3(7f, 0f, 0f), new Vector3(40f, 0f, 0f), 4.5f);
            Hazard.Saw(root, new Vector3(14f, 0.9f, -1.8f), new Vector3(14f, 0.9f, 1.8f), 2.4f, 90f);
            Hazard.Saw(root, new Vector3(21f, 0.9f, 1.8f), new Vector3(21f, 0.9f, -1.8f), 2f, 90f);
            Hazard.Spikes(root, new Vector3(26f, 0f, -1.1f));
            Hazard.Spikes(root, new Vector3(26f, 0f, 1.1f));
            Hazard.Saw(root, new Vector3(31f, 0.9f, -1.8f), new Vector3(31f, 0.9f, 1.8f), 1.6f, 90f);
            Hazard.Spikes(root, new Vector3(35.5f, 0f, 0f));
            CoinLine(new Vector3(9f, 0.8f, 0f), new Vector3(38f, 0.8f, 0f), 10);
            Deck("EastKeep", new Vector3(46f, 0f, 0f), new Vector3(10f, 1.5f, 10f));
            Checkpoint.Build(root, new Vector3(43f, 0f, -3f), 90f);
            PowerStar.Build(root, new Vector3(48f, 1.6f, 0f), "גשר המסורים");
        }

        // ---------- star 2: tower (north) ----------

        static void BuildTower()
        {
            Bridge(new Vector3(0f, 0f, 7f), new Vector3(0f, 0f, 12f), 4f);
            // Conveyor belts running back toward the hub: walk against them.
            Conveyor.Build(root, new Vector3(0f, -0.17f, 16f), new Vector3(4f, 0.35f, 8f), 180f, 3.5f);
            Conveyor.Build(root, new Vector3(0f, -0.17f, 24f), new Vector3(4f, 0.35f, 8f), 180f, 5f);
            CoinLine(new Vector3(0f, 0.9f, 13f), new Vector3(0f, 0.9f, 27f), 6);
            Deck("TowerBase", new Vector3(0f, 0f, 34f), new Vector3(10f, 1.5f, 10f));
            SpringPad.Build(root, new Vector3(0f, 0f, 33f), 21f);

            Deck("TowerMid", new Vector3(0f, 7f, 41f), new Vector3(7f, 1.2f, 7f));
            Coin.Build(root, new Vector3(0f, 8f, 41f));
            var spinner = Mover.Create(root, "TowerSpinner", new Vector3(0f, 7.2f, 50f), new Vector3(0f, 7.2f, 50f), 1f, 40f);
            KBox("platform", "SpinA", new Vector3(0f, -0.2f, 0f), new Vector3(10f, 0.4f, 1.8f), checker, spinner.transform);
            KBox("platform", "SpinB", new Vector3(0f, -0.2f, 0f), new Vector3(1.8f, 0.4f, 10f), checker, spinner.transform);
            var lift = Mover.Create(root, "TowerLift", new Vector3(0f, 7.3f, 58f), new Vector3(0f, 14f, 58f), 4.5f);
            KBox("block-moving-large", "TowerLiftTop", Vector3.zero, new Vector3(3f, 0.5f, 3f), plank, lift.transform);
            Deck("TowerTop", new Vector3(0f, 14f, 65f), new Vector3(8f, 1.2f, 8f));
            if (Props.Has("flag")) Props.Spawn("flag", root, new Vector3(2.5f, 14f, 67f), 5f, 0f);
            PowerStar.Build(root, new Vector3(0f, 15.6f, 66f), "פסגת המבצר");
        }

        // ---------- star 3: the west keep ----------

        static void BuildKeep()
        {
            var a = Mover.Create(root, "WestSlider1", new Vector3(-10f, -0.25f, 0f), new Vector3(-17f, -0.25f, 0f), 4f);
            KBox("block-moving-large", "WestSlider1Top", Vector3.zero, new Vector3(3f, 0.5f, 3f), plank, a.transform);
            var b = Mover.Create(root, "WestSlider2", new Vector3(-21f, 1f, -4f), new Vector3(-21f, 1f, 4f), 3.5f, 0f, 1.5f);
            KBox("block-moving-large", "WestSlider2Top", Vector3.zero, new Vector3(3f, 0.5f, 3f), plank, b.transform);
            Deck("WestKeep", new Vector3(-32f, 1.5f, 0f), new Vector3(14f, 1.5f, 14f));
            Hazard.Spikes(root, new Vector3(-32f, 1.5f, 5f));
            Hazard.Spikes(root, new Vector3(-27f, 1.5f, -5f));
            var star = PowerStar.Build(root, new Vector3(-32f, 3.2f, 0f), "שומרי המבצר", true);
            RevealWhenCleared.Watch(root, star, "השומרים הובסו - כוכב הופיע!",
                Grumble.Build(root, new Vector3(-29f, 1.5f, 2f)),
                Grumble.Build(root, new Vector3(-35f, 1.5f, -2f)),
                Grumble.Build(root, new Vector3(-33f, 1.5f, 4f)));
            CoinRing(new Vector3(-32f, 2.3f, 0f), 4f, 8);
        }

        // ---------- clouds below ----------

        static void CloudSea()
        {
            var mat = Art.Solid(new Color(1f, 0.98f, 1f), 0f);
            var rng = new System.Random(4);
            var parent = new GameObject("CloudSea").transform;
            parent.SetParent(root, false);
            for (int i = 0; i < 70; i++)
            {
                var p = Polar((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 140f);
                p.y = -22f - (float)rng.NextDouble() * 6f;
                float s = 14f + (float)rng.NextDouble() * 22f;
                Art.Prim(PrimitiveType.Sphere, "Cloud", parent, p, new Vector3(s, s * 0.35f, s * 0.8f), mat, Art.Col.None, null, false);
            }
        }
    }
}
