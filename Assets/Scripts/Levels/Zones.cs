using UnityEngine;
using static MayaAdventure.LevelBuilder;

namespace MayaAdventure
{
    /// <summary>
    /// The second half of every world (stars 4 and 5) plus extra enemies, crates and pickups.
    /// Each level builder calls its expansion after its own stars, so the star ids stay stable.
    /// </summary>
    public static class Zones
    {
        static bool Touch => TouchControls.Active;
        static string PunchKey => Touch ? "כפתור מכה / הלמות" : "Ctrl / E או קליק";
        static string PoundKey => Touch ? "כפתור הלמות" : "Ctrl / E";

        // ---------- shared pieces ----------

        internal static void Bridge(Vector3 from, Vector3 to, float width, bool coins = true)
        {
            var d = to - from;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            KBox("platform", "Bridge", (from + to) * 0.5f - Vector3.up * 0.2f, new Vector3(width, 0.4f, d.magnitude), plank, null, yaw);
            var side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (width * 0.5f - 0.15f);
            foreach (var end in new[] { from, to })
                for (int s = -1; s <= 1; s += 2)
                    Art.Prim(PrimitiveType.Cylinder, "Post", root, end + side * s + Vector3.up * 0.5f, new Vector3(0.25f, 0.6f, 0.25f), bark);
            if (coins) CoinLine(from + d.normalized * 1.5f + Vector3.up * 0.8f, to - d.normalized * 1.5f + Vector3.up * 0.8f, Mathf.Clamp(Mathf.RoundToInt(d.magnitude / 2.5f), 2, 8));
        }

        /// <summary>A floating island of tiles (top at <paramref name="top"/>) with a rocky cone underneath.</summary>
        internal static void SkyIsland(string name, Vector3 center, float radius, float top, string tile, Color lawn)
        {
            TileIsland(name, new Vector3(center.x, 0f, center.z), radius, top, tile, lawn, float.NaN, true);
            if (Nature.Load("Nature/Grass_Common_Short") != null) return; // the island mesh has its own rocky underside
            Art.MeshObject(name + "Underside", Art.ConeMesh(), root, new Vector3(center.x, top - 4f, center.z), new Vector3(radius * 2.3f, radius * 1.6f, radius * 2.3f),
                           Art.Textured("rock", new Vector2(3f, 1.5f), 0.05f), Art.Col.None, Quaternion.Euler(180f, 0f, 0f));
        }

        static Vector3 Ground(Vector3 p) => Snap(p, p.y + 6f);

        /// <summary>Crates at ground level: coin crates with one surprise among them.</summary>
        static void CrateStack(Vector3 at, float yaw, CrateContents special)
        {
            var r = Quaternion.Euler(0f, yaw, 0f);
            Crate.Build(root, Ground(at), CrateContents.Coins, 3, yaw);
            Crate.Build(root, Ground(at + r * new Vector3(1.4f, 0f, 0f)), special, 1, yaw + 10f);
            Crate.Build(root, Ground(at + r * new Vector3(0.7f, 0f, 1.3f)), CrateContents.Coins, 2, yaw - 15f);
        }

        /// <summary>Boss island reached by a bridge: King Grumble guards a hidden star.</summary>
        static void BossIsland(Vector3 center, float radius, float top, string tile, Color lawn, float baseBottom, Vector3 bridgeFrom, string starName, int hp)
        {
            TileIsland("BossIsland", new Vector3(center.x, 0f, center.z), radius, top, tile, lawn, baseBottom);
            var c = new Vector3(center.x, top, center.z);
            var dir = (c - bridgeFrom);
            dir.y = 0f;
            dir.Normalize();
            var bridgeTo = c - dir * (radius - 0.8f);
            bridgeTo.y = top;
            Bridge(bridgeFrom, bridgeTo, 3.5f);
            Sign.Build(root, bridgeFrom + Quaternion.Euler(0f, 90f, 0f) * dir * 2.4f, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg,
                       "המלך גרמבל! קפצי לו על הראש וקפצי מעל גלי ההדף");
            Checkpoint.Build(root, bridgeTo + dir * 1.5f + Quaternion.Euler(0f, 90f, 0f) * dir * 2f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            var star = PowerStar.Build(root, c + Vector3.up * 2f, starName, true);
            KingGrumble.Build(root, c + dir * 2f, star, hp);
            Heart.Build(root, bridgeTo + dir * 1.2f + Vector3.up * 1f - Quaternion.Euler(0f, 90f, 0f) * dir * 2f);
            for (int i = 0; i < 6; i++)
            {
                var p = c + Polar(i * 60f + 30f, radius - 2.2f);
                if (Vector3.Dot((p - c).normalized, -dir) > 0.8f) continue; // keep the entrance clear
                if (Props.Has("rocks")) Props.Spawn("rocks", root, p, 2.4f, i * 50f, Props.Col.Box);
            }
        }

        /// <summary>Purple switch at the foot of a spiral of temporary blocks leading to a star.</summary>
        static void SwitchSpiral(Vector3 center, float baseTop, int steps, float rise, float seconds, string starName)
        {
            var sw = PurpleSwitch.Build(root, new Vector3(center.x + 2.5f, baseTop, center.z - 4.5f), seconds);
            const float r = 5.5f;
            for (int i = 0; i < steps; i++)
            {
                float a = -90f + i * 300f / (steps - 1);
                var p = center + Polar(a, r);
                p.y = baseTop + 1.2f + i * rise;
                sw.Block(p, new Vector3(2.2f, 0.6f, 2.2f));
            }
            float topY = baseTop + 1.2f + steps * rise;
            KBox("block-grass-low-large", "StarPerch", new Vector3(center.x, topY - 0.3f, center.z), new Vector3(4f, 0.6f, 4f), checker);
            PowerStar.Build(root, new Vector3(center.x, topY + 1.6f, center.z), starName);
            Coin.Build(root, new Vector3(center.x + 1.2f, topY + 0.8f, center.z));
            Sign.Build(root, new Vector3(center.x - 1.5f, baseTop, center.z - 6.5f), 0f, "דרכי על המתג הסגול ורוצי במדרגות לפני שהן נעלמות!");
        }

        /// <summary>Cannon that fires Maya to a floating island; a warp pipe on the island brings her back.</summary>
        static Vector3 CannonTo(Vector3 cannon, Vector3 islandCenter, float radius, float top, string tile, Color lawn, Vector3 pipeHome, float flight = 2f)
        {
            SkyIsland("CannonIsland", islandCenter, radius, top, tile, lawn);
            var land = new Vector3(islandCenter.x, top, islandCenter.z);
            var back = cannon - land;
            back.y = 0f;
            var landing = land + back.normalized * (radius * 0.35f);
            Cannon.Build(root, cannon, landing, flight);
            Sign.Build(root, cannon + Quaternion.LookRotation(landing - cannon) * new Vector3(2.4f, 0f, -1.5f), Mathf.Atan2((landing - cannon).x, (landing - cannon).z) * Mathf.Rad2Deg + 180f,
                       "תותח! היכנסי לתוכו כדי לעוף");
            WarpPipe.Pair(root, land - back.normalized * (radius * 0.45f) + Quaternion.Euler(0f, 90f, 0f) * back.normalized * 2.5f, pipeHome);
            return land;
        }

        // ---------- level 1: Sunny Island ----------

        public static void Sunny()
        {
            var lawn = KenneyGrass;
            // Star 4: King Grumble on his own island, south-west over a bridge.
            BossIsland(new Vector3(-52f, 0f, -20f), 11f, 0f, "block-grass-large-tall", lawn, -12f, new Vector3(-28.6f, 0f, -11f), "המלך גרמבל", 3);
            // Star 5: purple switch spiral on the north island.
            TileIsland("SwitchIsland", new Vector3(0f, 0f, 50f), 9f, 0f, "block-grass-large-tall", lawn, -12f);
            Bridge(new Vector3(0f, 0f, 30.5f), new Vector3(0f, 0f, 42f), 3.5f);
            SwitchSpiral(new Vector3(0f, 0f, 50f), 0f, 10, 1.1f, 11f, "מדרגות המתג הסגול");
            PowerBox.Build(root, new Vector3(-5f, 1.1f, 45f), PowerKind.Wing);
            Bomber.Build(root, new Vector3(5f, 0f, 53f));
            Bomber.Build(root, new Vector3(-5f, 0f, 54f));

            // Main island extras.
            Sign.Build(root, new Vector3(-2f, 0f, -20f), 150f, $"חדש! {PunchKey} - מכה. שברי ארגזים ותני מכות לאויבים");
            CrateStack(new Vector3(6f, 0f, -16f), 15f, CrateContents.Heart);
            CrateStack(new Vector3(-12f, 0f, -14f), -30f, CrateContents.OneUp);
            Crate.Build(root, Ground(new Vector3(18f, 0f, -14f)), CrateContents.BlueCoin);
            Bomber.Build(root, new Vector3(20f, 0f, -2f));
            Buzzer.Build(root, new Vector3(22f, 3.5f, -18f));
            Buzzer.Build(root, new Vector3(-22f, 3f, 14f));
            Spiny.Build(root, new Vector3(-31.4f, 0f, -12.1f), new Vector3(-36.1f, 0f, -13.9f), 1.6f);
            WarpPipe.Pair(root, new Vector3(-18f, 0f, -22f), new Vector3(66f, 2f, -4.5f));
            Heart.Build(root, new Vector3(-2f, 1f, 38f));
        }

        // ---------- level 2: Snowy Peaks ----------

        public static void Snow(Color snow)
        {
            const string tile = "block-snow-large-tall";
            // Star 4: free the chomper on the south-east island.
            TileIsland("ChompIsland", new Vector3(40f, 0f, -34f), 10f, 0f, tile, snow, -10f);
            Bridge(new Vector3(21.5f, 0f, -18.3f), new Vector3(33f, 0f, -28f), 3.5f);
            var star = PowerStar.Build(root, new Vector3(43f, 1.8f, -38f), "כלב השרשרת", true);
            Chomper.Build(root, new Vector3(40f, 0f, -33f), star);
            Sign.Build(root, new Vector3(35.5f, 0f, -27.5f), 230f, $"הלמי 3 פעמים על היתד ({PoundKey} באוויר) כדי לשחרר את כלב השרשרת");
            Heart.Build(root, new Vector3(33.5f, 1f, -31f));

            // Star 5: a cannon on the north shore fires Maya to a floating ice island.
            var land = CannonTo(new Vector3(14f, 0f, 22f), new Vector3(22f, 0f, 60f), 7f, 13f, tile, snow, new Vector3(-14f, 0f, -18f), 2f);
            PowerStar.Build(root, land + new Vector3(1.5f, 1.8f, -1.5f), "אי הקרח המעופף");
            Spiny.Build(root, land + new Vector3(-3f, 0f, 1f), land + new Vector3(3f, 0f, 1f), 2f);
            PowerBox.Build(root, land + new Vector3(3.5f, 1.1f, 3f), PowerKind.Wing);
            KenneyTree("tree-pine-snow", land + new Vector3(3f, 0f, -3.5f), 2.2f);
            CoinRing(land + Vector3.up * 0.8f, 4.5f, 8);

            // Main island extras.
            Spiny.Build(root, new Vector3(10f, 0f, -14f), new Vector3(18f, 0f, -12f), 2.2f);
            Spiny.Build(root, new Vector3(-14f, 0f, -20f), new Vector3(-20f, 0f, -15f), 1.8f);
            Buzzer.Build(root, new Vector3(30f, 4f, 6f));
            Buzzer.Build(root, new Vector3(-8f, 6f, 22f));
            Bomber.Build(root, new Vector3(20f, 0f, 4f));
            CrateStack(new Vector3(7f, 0f, -19f), 0f, CrateContents.Heart);
            CrateStack(new Vector3(-24f, 0f, 10f), 40f, CrateContents.OneUp);
            Crate.Build(root, Ground(new Vector3(24f, 0f, -6f)), CrateContents.BlueCoin);
        }

        // ---------- level 3: Coral Lagoon ----------

        public static void Lagoon(Color sand, float sea, float floor)
        {
            const string tile = "block-grass-large-tall";
            // Star 4: a cage on the sea floor; only the metal cap sinks deep enough to pound its switch.
            PowerBox.Build(root, new Vector3(5f, sea + 2.1f, -31f), PowerKind.Metal);
            Sign.Build(root, new Vector3(3f, sea + 1f, -32.5f), 180f, "כובע המתכת שוקע! בקרקעית יש כלוב עם מתג - הלמי עליו");
            var cage = new Vector3(14f, floor, -46f);
            var cageRoot = new GameObject("Cage").transform;
            cageRoot.SetParent(root, false);
            var bars = Art.Solid(new Color(0.55f, 0.55f, 0.6f), 0.8f, 0.9f);
            for (int i = 0; i < 12; i++)
                Art.Prim(PrimitiveType.Cylinder, "Bar", cageRoot, cage + Polar(i * 30f, 2.2f) + Vector3.up * 1.6f, new Vector3(0.22f, 1.6f, 0.22f), bars);
            Art.Prim(PrimitiveType.Cylinder, "Roof", cageRoot, cage + Vector3.up * 3.3f, new Vector3(5f, 0.15f, 5f), bars);
            PowerStar.Build(root, cage + Vector3.up * 1.6f, "הכלוב השקוע");
            PoundSwitch.Build(root, cage + new Vector3(-4.5f, 0f, 1.5f), () =>
            {
                Object.Destroy(cageRoot.gameObject);
                GameManager.I?.Hud.Show("הכלוב נפתח!", 2f);
                GameManager.I?.Sfx.Play(GameManager.I.Sfx.Reveal);
            });
            CoinLine(new Vector3(8f, sea - 3f, -36f), cage + new Vector3(-3f, 1.5f, 3f), 6);

            // Star 5: a cannon to a hidden island where snappers guard the star.
            var land = CannonTo(new Vector3(-4f, sea + 1f, -28f), new Vector3(-46f, 0f, -46f), 9f, 6f, tile, sand, new Vector3(-6f, sea + 1f, -21f), 2f);
            // The giant Octopus has taken over the hidden island.
            var star = PowerStar.Build(root, land + Vector3.up * 1.8f, "התמנון הענק", true);
            GiantBoss.Build(root, land, GiantBoss.Kind.Octopus, star);
            Sign.Build(root, land + new Vector3(-2f, 0f, 5.5f), 180f, $"צמחים טורפים! אי אפשר לקפוץ עליהם - {PunchKey} או הלמות לידם");
            PalmTree(land + new Vector3(6f, 0f, 0f));
            Heart.Build(root, land + new Vector3(1.5f, 1f, 5f));

            // Extras around the start island.
            CrateStack(new Vector3(3f, sea + 1f, -27f), 20f, CrateContents.Heart);
            Crate.Build(root, new Vector3(6.5f, sea + 1f, -21f), CrateContents.OneUp);
            Buzzer.Build(root, new Vector3(4f, 15f, 30f));
            Spiny.Build(root, new Vector3(2f, 2.2f, 4f), new Vector3(6f, 2.2f, 8f), 1.4f);
        }

        // ---------- level 4: Sky Fortress ----------

        public static void Fortress(System.Action<string, Vector3, Vector3> deck, Color stoneLawn)
        {
            // Star 4: the crusher corridor east of the start deck.
            Bridge(new Vector3(8f, 0f, -20f), new Vector3(41f, 0f, -20f), 3.2f, false);
            for (int i = 0; i < 4; i++)
                Crusher.Build(root, new Vector3(14f + i * 7f, 0f, -20f), 5.5f);
            CoinLine(new Vector3(11f, 0.8f, -20f), new Vector3(38f, 0.8f, -20f), 9);
            Spiny.Build(root, new Vector3(17.5f, 0f, -20f), new Vector3(24.5f, 0f, -20f), 2f);
            deck("CrusherKeep", new Vector3(46f, 0f, -21f), new Vector3(10f, 1.5f, 10f));
            PowerStar.Build(root, new Vector3(48f, 1.6f, -21f), "מסדרון המוחצים");
            Sign.Build(root, new Vector3(6.5f, 0f, -17.5f), 90f, "זהירות, מוחצי אבן! רוצי מתחתם כשהם עולים - או רכבי עליהם");
            Heart.Build(root, new Vector3(44f, 1f, -24f));

            // Star 5: a cannon on the hub fires Maya to the bee hive; defeat every bee.
            var land = CannonTo(new Vector3(-3.5f, 0f, -4f), new Vector3(-36f, 0f, -40f), 8f, 5f, "block-grass-large-tall", stoneLawn, new Vector3(3.5f, 0f, -16.5f), 2f);
            var star = PowerStar.Build(root, land + Vector3.up * 2f, "כוורת הדבורים", true);
            RevealWhenCleared.Watch(root, star, "כל הדבורים הובסו - כוכב הופיע!",
                Buzzer.Build(root, land + new Vector3(3f, 2.2f, 2f)),
                Buzzer.Build(root, land + new Vector3(-3f, 2.6f, 1f)),
                Buzzer.Build(root, land + new Vector3(0f, 3f, -3f)),
                Buzzer.Build(root, land + new Vector3(2f, 3.4f, -1f)),
                Buzzer.Build(root, land + new Vector3(-2f, 2f, 3f)));
            var hive = Art.Solid(new Color(0.95f, 0.7f, 0.25f), 0.3f);
            for (int i = 0; i < 4; i++)
                Art.Prim(PrimitiveType.Sphere, "Hive", root, land + new Vector3(-4.5f, 1.4f - i * 0.25f + i * 0.9f, -4f), new Vector3(2.4f - i * 0.4f, 0.9f, 2.4f - i * 0.4f), hive);
            CrateStack(land + new Vector3(4f, 0f, -4.5f), 30f, CrateContents.Heart);

            // Extras.
            CrateStack(new Vector3(-5f, 0f, -17f), 0f, CrateContents.OneUp);
            Bomber.Build(root, new Vector3(46f, 0f, 2f));
            Bomber.Build(root, new Vector3(-30f, 1.5f, -3f));
            Spiny.Build(root, new Vector3(-3f, 0f, 3f), new Vector3(3f, 0f, 3f), 1.6f);
            Crate.Build(root, new Vector3(2f, 0f, -25f), CrateContents.BlueCoin);
        }

        // ---------- level 5: Twilight Woods ----------

        public static void Dusk(Color lawn)
        {
            const string tile = "block-grass-large-tall";
            // Star 4: the snapper garden on the east island.
            TileIsland("SnapperIsland", new Vector3(46f, 0f, -12f), 10f, 0f, tile, lawn, -10f);
            Bridge(new Vector3(28.4f, 0f, -7.4f), new Vector3(37.5f, 0f, -9.8f), 3.5f);
            var star = PowerStar.Build(root, new Vector3(47f, 1.8f, -12f), "גן הצמחים הטורפים", true);
            RevealWhenCleared.Watch(root, star, "הגן נוקה - כוכב הופיע!",
                Snapper.Build(root, new Vector3(43f, 0f, -16f)),
                Snapper.Build(root, new Vector3(50f, 0f, -16f), 60f),
                Snapper.Build(root, new Vector3(51f, 0f, -8f), 120f),
                Snapper.Build(root, new Vector3(44f, 0f, -6f), 200f),
                Snapper.Build(root, new Vector3(48f, 0f, -5f), 260f));
            Sign.Build(root, new Vector3(36f, 0f, -12.5f), 250f, $"הצמחים הטורפים נושכים! {PunchKey} או הלמות לידם");
            Heart.Build(root, new Vector3(39f, 1f, -13f));

            // Star 5: the final King Grumble (four hits) south-west.
            BossIsland(new Vector3(-34f, 0f, -44f), 12f, 0f, tile, lawn, -10f, new Vector3(-17.5f, 0f, -23.2f), "המלך גרמבל הגדול", 4);
            Bomber.Build(root, new Vector3(-30f, 0f, -38f));
            Bomber.Build(root, new Vector3(-38f, 0f, -48f));

            // Extras.
            Bomber.Build(root, new Vector3(18f, 0f, -14f));
            Buzzer.Build(root, new Vector3(12f, 5f, 2f));
            Buzzer.Build(root, new Vector3(16f, 6f, 14f));
            Spiny.Build(root, new Vector3(-6f, 0f, -16f), new Vector3(-12f, 0f, -20f), 1.8f);
            CrateStack(new Vector3(6f, 0f, -20f), 10f, CrateContents.Heart);
            CrateStack(new Vector3(20f, 0f, -20f), -20f, CrateContents.OneUp);
            PowerBox.Build(root, new Vector3(-5f, 1.1f, -18f), PowerKind.Metal);
            Crate.Build(root, Ground(new Vector3(-22f, 0f, -22f)), CrateContents.BlueCoin);
        }
    }
}
