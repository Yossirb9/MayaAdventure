using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Spinning heart: refills the power meter when touched (it stays, like the hearts in SM64).</summary>
    public class Heart : MonoBehaviour
    {
        Transform spin;
        float cooldown;

        public static Heart Build(Transform parent, Vector3 position)
        {
            var go = new GameObject("Heart");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var h = go.AddComponent<Heart>();
            h.spin = new GameObject("Spin").transform;
            h.spin.SetParent(go.transform, false);
            if (Props.Has("heart"))
            {
                var m = Props.Spawn("heart", h.spin, new Vector3(0f, -0.45f, 0f), 2.6f, 0f, Props.Col.None, false);
                Props.Tint(m, new Color(1.5f, 0.6f, 0.75f));
            }
            else Art.Prim(PrimitiveType.Sphere, "Heart", h.spin, Vector3.zero, Vector3.one * 0.8f, Art.Glow(new Color(1f, 0.3f, 0.45f), 1f), Art.Col.None);
            return h;
        }

        void Update()
        {
            spin.Rotate(0f, 160f * Time.deltaTime, 0f, Space.Self);
            spin.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.12f);
            if (cooldown > 0f)
            {
                cooldown -= Time.deltaTime;
                spin.Rotate(0f, 900f * Time.deltaTime * Mathf.Clamp01(cooldown), 0f, Space.Self); // spins fast after a touch
                return;
            }
            var gm = GameManager.I;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            if ((gm.Player.transform.position + Vector3.up * 0.85f - transform.position).sqrMagnitude < 1.5f * 1.5f)
            {
                // Always answer the touch: refill the power meter, sparkle and chime, even when it was already full.
                bool full = gm.Health >= GameManager.MaxHealth;
                while (gm.Health < GameManager.MaxHealth) gm.Heal();
                gm.Sfx.Play(gm.Sfx.PowerUp, 0.6f);
                gm.Hud.Show(full ? "הכוח כבר מלא" : "כוח מלא!", 1.2f);
                Debris.Burst(transform.position, new Color(1f, 0.5f, 0.65f), 14, 3.5f);
                cooldown = 2.5f;
            }
        }
    }

    /// <summary>Treasure chest: punch it (or ground-pound next to it) and it opens, spilling coins.</summary>
    public class TreasureChest : Punchable
    {
        bool opened;
        string key;

        void Start()
        {
            key = GameManager.KeyFor("chest", transform.position);
            if (GameManager.I != null && GameManager.I.Taken(key)) Open(false); // opened earlier this session
        }

        public override Vector3 PunchPoint => transform.position + Vector3.up * 0.6f;
        public override float Radius => 1.1f;

        void Update()
        {
            if (opened) return;
            var gm = GameManager.I;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            if ((gm.Player.transform.position - transform.position).sqrMagnitude < 3f * 3f)
                gm.Hud.Hint(TouchControls.Active ? "מכה - לפתוח את התיבה" : "F / קליק / Ctrl - לפתוח את התיבה");
        }

        public override void OnPunched(Vector3 from) => Open(true);

        void Open(bool reward)
        {
            if (opened) return;
            opened = true;
            var gm = GameManager.I;
            if (gm != null) gm.Take(key);
            var anim = GetComponentInChildren<Animation>();
            if (anim != null && anim["Chest_Open"] != null)
            {
                anim["Chest_Open"].wrapMode = WrapMode.ClampForever;
                anim.Play("Chest_Open");
                if (!reward) anim["Chest_Open"].normalizedTime = 1f;
            }
            else
            {
                foreach (var t in GetComponentsInChildren<Transform>())
                    if (t.name.Contains("Top") || t.name.Contains("Lid")) t.localRotation *= Quaternion.Euler(-100f, 0f, 0f);
            }
            if (!reward) return;
            var c = PunchPoint + Vector3.up * 0.4f;
            for (int i = 0; i < 8; i++) CoinPop.Spawn(c + Random.insideUnitSphere * 0.5f);
            Debris.Burst(c, new Color(1f, 0.85f, 0.3f), 22, 5f);
            if (gm != null)
            {
                gm.CollectCoins(8, c);
                gm.Sfx.Play(gm.Sfx.Reveal);
                gm.Hud.Show("אוצר! 8 מטבעות", 2f);
            }
        }
    }

    /// <summary>Green mushroom: an extra life. It slides away from Maya.</summary>
    public class OneUp : MonoBehaviour
    {
        Vector3 dir = Vector3.forward;
        float life = 14f;
        string key;

        public static OneUp Build(Transform parent, Vector3 position)
        {
            var go = new GameObject("OneUp");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var stem = Art.Solid(new Color(1f, 0.94f, 0.82f), 0.35f);
            var cap = Art.Glow(new Color(0.2f, 0.82f, 0.32f), 0.25f, 0.55f, 0f);
            var spot = Art.Solid(new Color(1f, 1f, 0.97f), 0.5f);
            var eye = Art.Solid(new Color(0.06f, 0.05f, 0.08f), 0.8f);
            var stemMesh = Art.LatheMesh("OneUpStem", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.26f, 0.06f), new Vector2(0.29f, 0.22f),
                new Vector2(0.28f, 0.4f), new Vector2(0.22f, 0.52f), new Vector2(0f, 0.54f),
            });
            Art.MeshObject("Stem", stemMesh, go.transform, Vector3.zero, Vector3.one, stem);
            Art.MeshObject("Cap", Art.LatheMesh("OneUpCap", Art.DomeProfile(0.55f, 0.42f, 0.1f, 12)), go.transform,
                           new Vector3(0f, 0.42f, 0f), Vector3.one, cap);
            // White spots sitting on the dome.
            for (int i = 0; i < 6; i++)
            {
                float yaw = i * 60f + (i % 2) * 20f, tilt = i % 2 == 0 ? 50f : 22f;
                var dir = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-tilt, 0f, 0f) * Vector3.forward;
                var p = new Vector3(0f, 0.52f, 0f) + Vector3.Scale(dir, new Vector3(0.55f, 0.42f, 0.55f));
                Art.Prim(PrimitiveType.Sphere, "Spot", go.transform, p, new Vector3(0.24f, 0.24f, 0.06f), spot, Art.Col.None,
                         Quaternion.LookRotation(dir));
            }
            Art.Prim(PrimitiveType.Sphere, "Spot", go.transform, new Vector3(0f, 0.94f, 0f), new Vector3(0.26f, 0.05f, 0.26f), spot, Art.Col.None);
            // A friendly face on the stem.
            for (int s = -1; s <= 1; s += 2)
                Art.Prim(PrimitiveType.Sphere, "Eye", go.transform, new Vector3(0.09f * s, 0.27f, 0.265f), new Vector3(0.07f, 0.13f, 0.05f), eye, Art.Col.None);
            var o = go.AddComponent<OneUp>();
            o.dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            o.key = GameManager.KeyFor("oneup", go.transform.position);
            if (GameManager.I != null && GameManager.I.Taken(o.key)) Object.Destroy(go);
            return o;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null) return;
            float dt = Time.deltaTime;
            life -= dt;
            Vector3 toP = gm.Player.transform.position - transform.position;
            toP.y = 0f;
            if (toP.sqrMagnitude < 36f && toP.sqrMagnitude > 0.01f) dir = Vector3.Lerp(dir, -toP.normalized, dt * 2f).normalized;
            var next = transform.position + dir * 2.2f * dt;
            // Stay on solid ground: turn around at edges.
            if (Physics.Raycast(next + Vector3.up + dir * 0.4f, Vector3.down, out var hit, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                next.y = hit.point.y;
                transform.position = next;
            }
            else dir = -dir;
            if (gm.State == GameState.Playing &&
                (gm.Player.transform.position + Vector3.up * 0.6f - (transform.position + Vector3.up * 0.5f)).sqrMagnitude < 1.2f * 1.2f)
            {
                gm.AddLife();
                gm.Take(key);
                Debris.Burst(transform.position + Vector3.up * 0.5f, new Color(0.4f, 1f, 0.45f), 12, 3f);
                Destroy(gameObject);
                return;
            }
            if (life <= 0f) Destroy(gameObject);
        }
    }

    /// <summary>A cap that pops out of a power box; touching it grants the power.</summary>
    public class PowerCap : MonoBehaviour
    {
        PowerKind kind;
        Transform spin;
        Vector3 basePos;

        public static float Duration(PowerKind k) => k == PowerKind.Wing ? 25f : 18f;

        public static PowerCap Build(Transform parent, Vector3 position, PowerKind kind)
        {
            var go = new GameObject(kind + "Cap");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var c = go.AddComponent<PowerCap>();
            c.kind = kind;
            c.basePos = position;
            c.spin = new GameObject("Spin").transform;
            c.spin.SetParent(go.transform, false);
            var capMat = kind == PowerKind.Wing ? Art.Glow(new Color(1f, 0.22f, 0.32f), 0.35f, 0.55f, 0f)
                                                : Art.Glow(new Color(0.82f, 0.87f, 0.96f), 0.25f, 0.95f, 1f);
            var band = kind == PowerKind.Wing ? Art.Solid(new Color(1f, 0.97f, 0.92f), 0.4f) : Art.Glow(new Color(0.55f, 0.6f, 0.7f), 0.2f, 0.9f, 1f);
            Art.MeshObject("Dome", Art.LatheMesh("CapDome", Art.DomeProfile(0.42f, 0.36f, 0.06f, 12)), c.spin, new Vector3(0f, -0.05f, 0f), Vector3.one, capMat);
            Art.MeshObject("Band", Art.LatheMesh("CapBand", new[] { new Vector2(0.43f, 0f), new Vector2(0.44f, 0.04f), new Vector2(0.43f, 0.09f) }),
                           c.spin, new Vector3(0f, -0.05f, 0f), Vector3.one, band);
            // Peak: a flattened half-disc sticking out at the front.
            Art.MeshObject("Peak", Art.LatheMesh("CapPeak", new[] { new Vector2(0f, 0.02f), new Vector2(0.38f, 0.015f), new Vector2(0.4f, 0f), new Vector2(0f, -0.005f) }),
                           c.spin, new Vector3(0f, -0.04f, 0.2f), new Vector3(1f, 1f, 0.75f), capMat);
            // Emblem on the front.
            Art.MeshObject("Emblem", Art.StarMesh(), c.spin, new Vector3(0f, 0.16f, 0.39f), Vector3.one * 0.2f, band, Art.Col.None, Quaternion.Euler(-20f, 0f, 0f));
            if (kind == PowerKind.Wing)
            {
                // Two little layered feather wings on the sides.
                var feather = Art.Glow(new Color(1f, 0.99f, 0.96f), 0.4f, 0.5f, 0f);
                for (int s = -1; s <= 1; s += 2)
                    for (int f = 0; f < 3; f++)
                    {
                        float len = 0.75f - f * 0.14f;
                        var rot = Quaternion.Euler(0f, 0f, s * (-18f + f * 30f));
                        Art.Prim(PrimitiveType.Sphere, "Feather", c.spin, new Vector3(s * (0.36f + len * 0.42f), 0.22f + f * 0.05f, -0.04f - f * 0.04f),
                                 new Vector3(len, 0.18f, 0.05f), feather, Art.Col.None, rot);
                    }
            }
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4f;
            l.intensity = 1.2f;
            l.color = kind == PowerKind.Wing ? new Color(1f, 0.5f, 0.5f) : new Color(0.8f, 0.85f, 1f);
            Debris.Burst(go.transform.position, new Color(1f, 0.95f, 0.6f), 16, 4f);
            return c;
        }

        void Update()
        {
            spin.Rotate(0f, 120f * Time.deltaTime, 0f, Space.Self);
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.15f);
            var gm = GameManager.I;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            if ((gm.Player.transform.position + Vector3.up * 0.85f - transform.position).sqrMagnitude < 1.5f * 1.5f)
            {
                gm.Player.GivePower(kind, Duration(kind));
                gm.Sfx.Play(gm.Sfx.PowerUp);
                gm.Hud.Show(kind == PowerKind.Wing ? "כובע כנפיים! החזיקי קפיצה כדי לרחף" : "כובע מתכת! בלתי מנוצחת, ושוקעת במים", 3f);
                Debris.Burst(transform.position, new Color(1f, 0.95f, 0.6f), 20, 5f);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Red (wing) or silver (metal) power box: punch it or bump it from below to release a cap.</summary>
    public class PowerBox : Punchable
    {
        PowerKind kind;
        bool used;

        public static PowerBox Build(Transform parent, Vector3 center, PowerKind kind)
        {
            var go = new GameObject(kind + "Box");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = Vector3.one * 1.2f;
            var b = go.AddComponent<PowerBox>();
            b.kind = kind;
            if (Props.Has("crate-item"))
            {
                var m = Props.Spawn("crate-item", go.transform, new Vector3(0f, -0.6f, 0f), 2.4f);
                Props.Tint(m, kind == PowerKind.Wing ? new Color(1.6f, 0.45f, 0.45f) : new Color(0.75f, 0.8f, 0.9f));
            }
            else Art.Prim(PrimitiveType.Cube, "Box", go.transform, Vector3.zero, Vector3.one * 1.2f,
                          Art.Glow(kind == PowerKind.Wing ? new Color(1f, 0.3f, 0.3f) : new Color(0.75f, 0.8f, 0.9f), 0.5f), Art.Col.None);
            return b;
        }

        public override float Radius => 0.8f;

        public override void OnPunched(Vector3 from)
        {
            if (used) return;
            used = true;
            var gm = GameManager.I;
            if (gm != null) gm.Sfx.Play(gm.Sfx.Block);
            PowerCap.Build(transform.parent, transform.localPosition + Vector3.up * 1.4f, kind);
            foreach (var r in GetComponentsInChildren<Renderer>()) Props.Tint(r.gameObject, new Color(0.45f, 0.4f, 0.4f));
        }
    }
}
