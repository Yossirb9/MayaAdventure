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

        public override void OnPunched(Vector3 from)
        {
            if (opened) return;
            opened = true;
            var gm = GameManager.I;
            var anim = GetComponentInChildren<Animation>();
            if (anim != null && anim["Chest_Open"] != null)
            {
                anim["Chest_Open"].wrapMode = WrapMode.ClampForever;
                anim.Play("Chest_Open");
            }
            else
            {
                foreach (var t in GetComponentsInChildren<Transform>())
                    if (t.name.Contains("Top") || t.name.Contains("Lid")) t.localRotation *= Quaternion.Euler(-100f, 0f, 0f);
            }
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

        public static OneUp Build(Transform parent, Vector3 position)
        {
            var go = new GameObject("OneUp");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var stem = Art.Solid(new Color(1f, 0.95f, 0.85f), 0.3f);
            var cap = Art.Glow(new Color(0.25f, 0.85f, 0.3f), 0.3f, 0.5f, 0f);
            var spot = Art.Solid(Color.white, 0.4f);
            Art.Prim(PrimitiveType.Cylinder, "Stem", go.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.45f, 0.3f, 0.45f), stem, Art.Col.None);
            Art.Prim(PrimitiveType.Sphere, "Cap", go.transform, new Vector3(0f, 0.65f, 0f), new Vector3(0.95f, 0.6f, 0.95f), cap, Art.Col.None);
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(-35f, i * 90f, 0f);
                Art.Prim(PrimitiveType.Sphere, "Spot", go.transform, new Vector3(0f, 0.65f, 0f) + rot * new Vector3(0f, 0f, 0.44f),
                         new Vector3(0.22f, 0.22f, 0.08f), spot, Art.Col.None, rot);
            }
            var o = go.AddComponent<OneUp>();
            o.dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
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
            var capMat = kind == PowerKind.Wing ? Art.Glow(new Color(1f, 0.25f, 0.3f), 0.5f, 0.5f, 0f)
                                                : Art.Glow(new Color(0.8f, 0.85f, 0.95f), 0.3f, 0.95f, 1f);
            Art.Prim(PrimitiveType.Sphere, "Dome", c.spin, new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.55f, 0.8f), capMat, Art.Col.None);
            Art.Prim(PrimitiveType.Cylinder, "Brim", c.spin, new Vector3(0f, 0f, 0.2f), new Vector3(0.95f, 0.03f, 0.75f), capMat, Art.Col.None);
            if (kind == PowerKind.Wing)
            {
                var feather = Art.Glow(Color.white, 0.6f, 0.4f, 0f);
                Art.Prim(PrimitiveType.Sphere, "WingL", c.spin, new Vector3(-0.5f, 0.3f, -0.05f), new Vector3(0.55f, 0.3f, 0.06f), feather, Art.Col.None, Quaternion.Euler(0f, 0f, 25f));
                Art.Prim(PrimitiveType.Sphere, "WingR", c.spin, new Vector3(0.5f, 0.3f, -0.05f), new Vector3(0.55f, 0.3f, 0.06f), feather, Art.Col.None, Quaternion.Euler(0f, 0f, -25f));
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
