using System.Collections.Generic;
using UnityEngine;
using static MayaAdventure.LevelBuilder;

namespace MayaAdventure
{
    /// <summary>
    /// Level 5 "Twilight Woods": glowing giant mushrooms spiral up to a great tree (star 1),
    /// a hedge maze hides star 2 at its heart, and 8 red coins reveal star 3.
    /// </summary>
    public static class DuskLevel
    {
        static readonly Color Lawn = new Color(0.5f, 0.74f, 0.56f);
        static readonly Vector3 MazeCenter = new Vector3(-13f, 0f, 2f);
        static readonly Vector3 Grove = new Vector3(12f, 0f, 8f);
        static int lights;

        public static LevelInfo Build()
        {
            Begin("Level 5 - Twilight Woods");
            cover = Nature.Cover.Dusk;
            lights = 0;
            var info = new LevelInfo
            {
                Name = Levels.Names[4],
                Theme = LevelTheme.Dusk(),
                Root = root,
                Start = new Vector3(0f, 0f, -22f),
                StartYaw = 0f,
                ReturnSpawn = new Vector3(5f, 0f, -24f),
                ReturnYaw = -90f,
                WaterY = -1f,
                KillY = -30f,
                TotalRedCoins = 8,
                RedCoinHint = "כוכב הגחליליות הופיע ליד הכניסה!",
                Ambience = AmbienceKind.Fireflies,
            };

            TileIsland("WoodsIsland", Vector3.zero, 30f, 0f, "block-grass-large-tall", Lawn, -10f);
            Water(-1f, -10f, info.Theme);
            Portal.Build(root, new Vector3(8f, 0f, -24f), 90f, 3, 0);

            BuildMushroomClimb();
            BuildMaze();
            BuildRedCoins(info);
            BuildDecor();
            Zones.Dusk(Lawn);
            DistantIslands(Lawn, 9, 150f, 230f, "block-grass-large-tall", "tree-pine");
            return info;
        }

        // ---------- star 1: giant mushrooms up to the great tree ----------

        static void GiantMushroom(Vector3 basePos, float height, float capRadius, Color cap)
        {
            var t = new GameObject("GiantMushroom").transform;
            t.SetParent(root, false);
            t.localPosition = basePos;
            var stem = Art.Solid(new Color(0.95f, 0.9f, 0.82f), 0.2f);
            Art.Prim(PrimitiveType.Cylinder, "Stem", t, Vector3.up * (height * 0.5f), new Vector3(capRadius * 0.45f, height * 0.5f, capRadius * 0.45f), stem, Art.Col.Mesh);
            // Cap: a flattened sphere you can stand on (flat cylinder collider on top).
            Art.Prim(PrimitiveType.Sphere, "Cap", t, Vector3.up * height, new Vector3(capRadius * 2f, capRadius * 0.8f, capRadius * 2f),
                     Art.Glow(cap, 0.35f, 0.35f, 0f), Art.Col.None);
            Art.Prim(PrimitiveType.Cylinder, "CapTop", t, Vector3.up * (height + capRadius * 0.3f), new Vector3(capRadius * 1.8f, 0.05f, capRadius * 1.8f),
                     Art.Glow(cap, 0.35f, 0.35f, 0f), Art.Col.Mesh, null, false);
            var spot = Art.Glow(new Color(1f, 0.95f, 0.75f), 2.5f, 0.2f, 0f);
            for (int i = 0; i < 5; i++)
            {
                var d = Polar(i * 72f + basePos.x * 10f, capRadius * 0.55f);
                Art.Prim(PrimitiveType.Sphere, "Spot", t, Vector3.up * (height + capRadius * 0.3f) + d, new Vector3(0.35f, 0.12f, 0.35f) * capRadius * 0.6f, spot, Art.Col.None, null, false);
            }
            if (lights < 7)
            {
                lights++;
                var l = new GameObject("Glow").AddComponent<Light>();
                l.transform.SetParent(t, false);
                l.transform.localPosition = Vector3.up * (height - 0.6f);
                l.type = LightType.Point;
                l.color = cap;
                l.range = capRadius * 4f;
                l.intensity = 1.5f;
            }
        }

        static void BuildMushroomClimb()
        {
            Color[] caps = { new Color(1f, 0.35f, 0.5f), new Color(0.6f, 0.4f, 1f), new Color(0.3f, 0.8f, 1f), new Color(1f, 0.6f, 0.25f) };
            const int steps = 9;
            for (int i = 0; i < steps; i++)
            {
                float a = -100f + i * 48f;
                float h = 1.0f + i * 1.35f;
                var p = Grove + Polar(a, 7.5f);
                GiantMushroom(p, h, 1.6f, caps[i % caps.Length]);
                if (i % 2 == 1) Coin.Build(root, p + Vector3.up * (h + 1.4f));
            }
            // The great tree at the centre of the spiral, with a platform in its crown.
            float top = 1.0f + steps * 1.35f + 0.5f;
            if (Props.Has("tree")) Props.Spawn("tree", root, Grove, new Vector3(6f, (top - 1f) / 1.93f, 6f), 20f); // crown ends just under the deck
            Art.Prim(PrimitiveType.Cylinder, "Trunk", root, Grove + Vector3.up * (top * 0.5f), new Vector3(2.2f, top * 0.5f, 2.2f), bark);
            KBox("block-grass-low-large", "CrownDeck", Grove + Vector3.up * (top + 0.25f), new Vector3(5f, 0.5f, 5f), checker);
            PowerStar.Build(root, Grove + Vector3.up * (top + 2f), "צמרת העץ הגדול");
        }

        // ---------- star 2: hedge maze ----------

        static void BuildMaze()
        {
            const int n = 7;
            const float cell = 3.2f;
            var rng = new System.Random(9);
            var visited = new bool[n, n];
            // walls: east[i,j] between (i,j)-(i+1,j), north[i,j] between (i,j)-(i,j+1)
            var east = new bool[n, n];
            var north = new bool[n, n];
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++) { east[i, j] = true; north[i, j] = true; }

            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(n / 2, 0));
            visited[n / 2, 0] = true;
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                var options = new List<Vector2Int>();
                if (c.x > 0 && !visited[c.x - 1, c.y]) options.Add(new Vector2Int(-1, 0));
                if (c.x < n - 1 && !visited[c.x + 1, c.y]) options.Add(new Vector2Int(1, 0));
                if (c.y > 0 && !visited[c.x, c.y - 1]) options.Add(new Vector2Int(0, -1));
                if (c.y < n - 1 && !visited[c.x, c.y + 1]) options.Add(new Vector2Int(0, 1));
                if (options.Count == 0) { stack.Pop(); continue; }
                var d = options[rng.Next(options.Count)];
                var nb = c + d;
                if (d.x == 1) east[c.x, c.y] = false;
                if (d.x == -1) east[nb.x, nb.y] = false;
                if (d.y == 1) north[c.x, c.y] = false;
                if (d.y == -1) north[nb.x, nb.y] = false;
                visited[nb.x, nb.y] = true;
                stack.Push(nb);
            }

            var origin = MazeCenter - new Vector3(n * cell * 0.5f, 0f, n * cell * 0.5f);
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                var c0 = origin + new Vector3(i * cell, 0f, j * cell);
                if (i < n - 1 && east[i, j]) Hedge(c0 + new Vector3(cell, 0f, cell * 0.5f), cell, 90f);
                if (j < n - 1 && north[i, j]) Hedge(c0 + new Vector3(cell * 0.5f, 0f, cell), cell, 0f);
            }
            // Outer walls with an entrance on the south side (toward the start).
            for (int i = 0; i < n; i++)
            {
                if (i != n / 2) Hedge(origin + new Vector3(i * cell + cell * 0.5f, 0f, 0f), cell, 0f);
                Hedge(origin + new Vector3(i * cell + cell * 0.5f, 0f, n * cell), cell, 0f);
                Hedge(origin + new Vector3(0f, 0f, i * cell + cell * 0.5f), cell, 90f);
                Hedge(origin + new Vector3(n * cell, 0f, i * cell + cell * 0.5f), cell, 90f);
            }
            PowerStar.Build(root, MazeCenter + Vector3.up * 1.6f, "לב המבוך");
            Lantern(MazeCenter + new Vector3(1f, 0f, -1f));
            Grumble.Build(root, origin + new Vector3(cell * 1.5f, 0f, cell * 4.5f));
            Grumble.Build(root, origin + new Vector3(cell * 5.5f, 0f, cell * 2.5f));
        }

        static void Hedge(Vector3 center, float length, float yaw)
        {
            Box("Hedge", center + Vector3.up * 1.1f, new Vector3(length + 0.6f, 2.2f, 0.7f), Art.Lawn(new Color(0.22f, 0.45f, 0.3f), 1f), null, yaw);
        }

        // ---------- star 3: red coins ----------

        static void BuildRedCoins(LevelInfo info)
        {
            Vector3[] spots =
            {
                new Vector3(22f, 1f, -14f), new Vector3(-24f, 1f, -11f), new Vector3(-20f, 1f, 16f), new Vector3(0f, 1f, 26f),
                Grove + new Vector3(0f, 1f, -2.5f), new Vector3(20f, 1f, 17f), new Vector3(-6f, 1f, -10f), new Vector3(0f, -6f, -34f),
            };
            foreach (var p in spots) Coin.Build(root, p, true);
            info.RedCoinStar = PowerStar.Build(root, new Vector3(0f, 1.8f, -14f), "אוצר הגחליליות", true);
        }

        // ---------- decoration ----------

        static void Lantern(Vector3 p)
        {
            Art.Prim(PrimitiveType.Cylinder, "Post", root, p + Vector3.up * 1.1f, new Vector3(0.14f, 1.1f, 0.14f), Art.Solid(new Color(0.2f, 0.16f, 0.14f), 0.4f, 0.6f));
            Art.Prim(PrimitiveType.Sphere, "Lamp", root, p + Vector3.up * 2.35f, Vector3.one * 0.45f, Art.Glow(new Color(1f, 0.75f, 0.35f), 4f, 0.2f, 0f), Art.Col.None, null, false);
            if (lights < 12)
            {
                lights++;
                var l = new GameObject("LanternLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.localPosition = p + Vector3.up * 2.35f;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.7f, 0.35f);
                l.range = 8f;
                l.intensity = 1.6f;
            }
        }

        static void BuildDecor()
        {
            Lantern(new Vector3(3f, 0f, -18f));
            Lantern(new Vector3(-3f, 0f, -12f));
            Lantern(new Vector3(-5f, 0f, -6f));
            CoinLine(new Vector3(0f, 0.8f, -18f), new Vector3(0f, 0.8f, -8f), 5);
            var rng = new System.Random(8);
            for (int i = 0; i < 45; i++)
            {
                var p = Polar((float)rng.NextDouble() * 360f, 10f + (float)rng.NextDouble() * 18f);
                if ((p - MazeCenter).sqrMagnitude < 15f * 15f || (p - Grove).sqrMagnitude < 11f * 11f || p.z < -16f) continue;
                if (i % 2 == 0) KenneyTree("tree-pine", p, 2.4f + (float)rng.NextDouble());
                else KenneyTree("tree", p, 2.2f + (float)rng.NextDouble() * 0.8f);
            }
            for (int i = 0; i < 120; i++)
            {
                var p = Polar((float)rng.NextDouble() * 360f, 3f + (float)rng.NextDouble() * 26f);
                if ((p - MazeCenter).sqrMagnitude < 13f * 13f) continue;
                string kind = i % 3 == 0 ? "mushrooms" : i % 3 == 1 ? "flowers" : "grass";
                if (!Props.Has(kind)) continue;
                var go = Props.Spawn(kind, root, p, 2f + (float)rng.NextDouble(), (float)rng.NextDouble() * 360f, Props.Col.None, false);
                if (kind == "mushrooms") Props.Tint(go, new Color(1.15f, 0.8f, 1.25f));
            }
            Grumble.Build(root, new Vector3(18f, 0f, -8f));
            Grumble.Build(root, new Vector3(-6f, 0f, 20f));
        }
    }
}
