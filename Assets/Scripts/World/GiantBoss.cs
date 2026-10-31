using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// A world boss guarding a hidden star: the giant Snowman (Snowy Peaks) or the giant Octopus (Coral Lagoon).
    /// It shuffles after Maya inside its arena and lobs snowballs / ink balls at her. Three stomps (or ground
    /// pounds) on its head win the star; a punch only pushes it back. It gets faster and throws more after each hit.
    /// </summary>
    public class GiantBoss : Enemy
    {
        public enum Kind { Snowman, Octopus }

        Kind kind;
        PowerStar star;
        Vector3 home;
        int hp = 3, maxHp = 3;
        float invulnerable, dazed, throwTimer = 2.5f, speed = 1.4f, leash = 6f, t;
        Renderer[] renderers;
        Transform[] tentacles;

        public override string BossName => kind == Kind.Snowman ? "איש השלג הענק" : "התמנון הענק";
        public override int BossHp => hp;
        public override int BossMaxHp => maxHp;

        public static GiantBoss Build(Transform parent, Vector3 position, Kind kind, PowerStar star)
        {
            var go = new GameObject(kind == Kind.Snowman ? "SnowmanBoss" : "OctopusBoss");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var b = go.AddComponent<GiantBoss>();
            b.kind = kind;
            b.star = star;
            b.home = position;
            b.coinsOnDeath = 8;
            b.body = new GameObject("Body").transform;
            b.body.SetParent(go.transform, false);
            if (kind == Kind.Snowman)
            {
                b.bodyRadius = 1.3f;
                b.bodyHeight = 2.7f;
                b.BuildSnowman();
            }
            else
            {
                b.bodyRadius = 1.5f;
                b.bodyHeight = 2.3f;
                b.BuildOctopus();
            }
            b.renderers = b.body.GetComponentsInChildren<Renderer>();
            return b;
        }

        // ---------- looks ----------

        void BuildSnowman()
        {
            var snow = Art.Solid(new Color(0.95f, 0.97f, 1f), 0.25f);
            var coal = Art.Solid(new Color(0.08f, 0.08f, 0.1f), 0.5f);
            var carrot = Art.Glow(new Color(1f, 0.5f, 0.1f), 0.2f, 0.4f, 0f);
            var wood = Art.Solid(new Color(0.4f, 0.26f, 0.14f), 0.2f);
            var scarf = Art.Glow(new Color(0.9f, 0.15f, 0.25f), 0.2f, 0.3f, 0f);
            Art.Prim(PrimitiveType.Sphere, "Base", body, new Vector3(0f, 0.75f, 0f), new Vector3(1.9f, 1.6f, 1.9f), snow, Art.Col.None);
            Art.Prim(PrimitiveType.Sphere, "Middle", body, new Vector3(0f, 1.85f, 0f), new Vector3(1.4f, 1.25f, 1.4f), snow, Art.Col.None);
            Art.Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 2.75f, 0f), Vector3.one * 1f, snow, Art.Col.None);
            for (int s = -1; s <= 1; s += 2)
            {
                Art.Prim(PrimitiveType.Sphere, "Eye", body, new Vector3(0.2f * s, 2.88f, 0.43f), Vector3.one * 0.14f, coal, Art.Col.None);
                Art.Prim(PrimitiveType.Cube, "Brow", body, new Vector3(0.21f * s, 3.06f, 0.42f), new Vector3(0.26f, 0.06f, 0.06f), coal,
                         Art.Col.None, Quaternion.Euler(0f, 0f, 22f * s));
                // Stick arms, raised angrily.
                Art.Prim(PrimitiveType.Cylinder, "Arm", body, new Vector3(0.95f * s, 2.15f, 0f), new Vector3(0.08f, 0.55f, 0.08f), wood, Art.Col.None,
                         Quaternion.Euler(0f, 0f, -55f * s));
            }
            Art.MeshObject("Nose", Art.ConeMesh(), body, new Vector3(0f, 2.75f, 0.45f), new Vector3(0.16f, 0.5f, 0.16f), carrot, Art.Col.None,
                           Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 5; i++)
                Art.Prim(PrimitiveType.Sphere, "Mouth", body, new Vector3((i - 2) * 0.09f, 2.55f - Mathf.Abs(i - 2) * -0.03f, 0.46f - Mathf.Abs(i - 2) * 0.02f),
                         Vector3.one * 0.07f, coal, Art.Col.None);
            for (int i = 0; i < 3; i++)
                Art.Prim(PrimitiveType.Sphere, "Button", body, new Vector3(0f, 1.6f + i * 0.3f, 0.66f - Mathf.Abs(i - 1) * 0.04f), Vector3.one * 0.12f, coal, Art.Col.None);
            Art.MeshObject("Scarf", Art.LatheMesh("BossScarf", new[] { new Vector2(0.42f, 0f), new Vector2(0.52f, 0.06f), new Vector2(0.53f, 0.16f), new Vector2(0.42f, 0.22f) }),
                           body, new Vector3(0f, 2.22f, 0f), Vector3.one, scarf);
            Art.Prim(PrimitiveType.Cube, "ScarfEnd", body, new Vector3(0.3f, 1.95f, 0.42f), new Vector3(0.18f, 0.45f, 0.05f), scarf, Art.Col.None, Quaternion.Euler(0f, 0f, 12f));
            // Top hat.
            Art.MeshObject("Hat", Art.LatheMesh("BossHat", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.55f, 0f), new Vector2(0.55f, 0.05f), new Vector2(0.33f, 0.05f), new Vector2(0.33f, 0.6f), new Vector2(0f, 0.6f),
            }), body, new Vector3(0f, 3.15f, 0f), Vector3.one, coal, Art.Col.None, Quaternion.Euler(-8f, 0f, 6f));
        }

        void BuildOctopus()
        {
            var skin = Art.Glow(new Color(0.62f, 0.3f, 0.85f), 0.25f, 0.6f, 0f);
            var spots = Art.Glow(new Color(0.85f, 0.6f, 1f), 0.2f, 0.6f, 0f);
            var white = Art.Solid(Color.white, 0.7f);
            var black = Art.Solid(new Color(0.06f, 0.05f, 0.08f), 0.6f);
            Art.MeshObject("Head", Art.LatheMesh("OctoHead", new[]
            {
                new Vector2(0f, 0.3f), new Vector2(0.9f, 0.32f), new Vector2(1.25f, 0.6f), new Vector2(1.35f, 1.1f), new Vector2(1.25f, 1.7f),
                new Vector2(0.95f, 2.15f), new Vector2(0.5f, 2.4f), new Vector2(0f, 2.48f),
            }), body, Vector3.zero, Vector3.one, skin);
            for (int i = 0; i < 7; i++)
            {
                var dir = Quaternion.Euler(0f, 40f + i * 47f, 0f) * Quaternion.Euler(-(30f + (i % 3) * 18f), 0f, 0f) * Vector3.forward;
                if (dir.z > 0.55f && dir.y < 0.6f) continue; // keep the face clear
                Art.Prim(PrimitiveType.Sphere, "Spot", body, new Vector3(0f, 1.3f, 0f) + Vector3.Scale(dir, new Vector3(1.3f, 1.1f, 1.3f)),
                         new Vector3(0.32f, 0.32f, 0.06f), spots, Art.Col.None, Quaternion.LookRotation(dir));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Art.Prim(PrimitiveType.Sphere, "Eye", body, new Vector3(0.42f * s, 1.25f, 1.12f), new Vector3(0.5f, 0.6f, 0.3f), white, Art.Col.None);
                Art.Prim(PrimitiveType.Sphere, "Pupil", body, new Vector3(0.38f * s, 1.2f, 1.27f), Vector3.one * 0.22f, black, Art.Col.None);
                Art.Prim(PrimitiveType.Cube, "Brow", body, new Vector3(0.42f * s, 1.62f, 1.15f), new Vector3(0.5f, 0.1f, 0.1f), black,
                         Art.Col.None, Quaternion.Euler(0f, 0f, 20f * s));
            }
            Art.Prim(PrimitiveType.Sphere, "Mouth", body, new Vector3(0f, 0.75f, 1.2f), new Vector3(0.35f, 0.2f, 0.15f), black, Art.Col.None);
            // Eight curling tentacles spread on the ground.
            tentacles = new Transform[8];
            for (int i = 0; i < 8; i++)
            {
                var root = new GameObject("Tentacle").transform;
                root.SetParent(body, false);
                root.localPosition = new Vector3(0f, 0.3f, 0f);
                root.localRotation = Quaternion.Euler(0f, i * 45f + 22.5f, 0f);
                var prev = root;
                for (int k = 0; k < 6; k++)
                {
                    var seg = new GameObject("Seg").transform;
                    seg.SetParent(prev, false);
                    seg.localPosition = k == 0 ? new Vector3(0f, 0f, 0.95f) : new Vector3(0f, 0f, 0.42f);
                    float r = 0.42f * (1f - k * 0.13f);
                    Art.Prim(PrimitiveType.Sphere, "Ball", seg, Vector3.zero, new Vector3(r, r * 0.8f, r * 1.3f), skin, Art.Col.None);
                    prev = seg;
                }
                tentacles[i] = root;
            }
        }

        // ---------- behaviour ----------

        void Update()
        {
            t += Time.deltaTime;
            var toPlayer = Vector3.zero;
            bool near = !Dead && EnemyArt.PlayerNear(transform.position, 18f, out toPlayer);
            if (near) Boss = this;
            else if (Boss == this) Boss = null;
            if (Dead) return;
            float dt = Time.deltaTime;
            AnimateIdle(dt);
            if (invulnerable > 0f)
            {
                invulnerable -= dt;
                EnemyArt.Flash(renderers, invulnerable > 0f && Mathf.Repeat(invulnerable * 8f, 1f) > 0.5f);
            }
            if (dazed > 0f)
            {
                dazed -= dt;
                body.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 25f) * 8f, 0f);
                if (invulnerable <= 0f) CheckPlayerContact();
                return;
            }
            body.localRotation = Quaternion.identity;
            if (near)
            {
                toPlayer.y = 0f;
                var face = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : transform.rotation;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, 120f * dt);
                // Shuffle after her, but never leave the arena.
                var next = transform.position + toPlayer.normalized * speed * dt;
                var off = next - home;
                off.y = 0f;
                if (toPlayer.magnitude > 2.2f && off.magnitude < leash) transform.position = next;
                throwTimer -= dt;
                if (throwTimer <= 0f)
                {
                    int shots = maxHp - hp + 1; // angrier: more at once
                    for (int i = 0; i < shots; i++) Throw(i, shots);
                    throwTimer = Mathf.Lerp(3.2f, 1.8f, (maxHp - hp) / (float)maxHp) + Random.value * 0.6f;
                }
            }
            CheckPlayerContact();
        }

        void AnimateIdle(float dt)
        {
            if (kind == Kind.Snowman)
            {
                body.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 3f) * 0.025f, 1f);
                return;
            }
            body.localScale = new Vector3(1f + Mathf.Sin(t * 2.2f) * 0.03f, 1f - Mathf.Sin(t * 2.2f) * 0.03f, 1f + Mathf.Sin(t * 2.2f) * 0.03f);
            if (tentacles == null) return;
            for (int i = 0; i < tentacles.Length; i++)
            {
                var seg = tentacles[i];
                int k = 0;
                foreach (var s in seg.GetComponentsInChildren<Transform>())
                {
                    if (s.name != "Seg") continue;
                    s.localRotation = Quaternion.Euler(-6f + Mathf.Sin(t * 2.5f + i + k * 0.7f) * 9f, Mathf.Sin(t * 1.7f + i * 0.6f + k) * 14f, 0f);
                    k++;
                }
            }
        }

        void Throw(int index, int count)
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null) return;
            var from = transform.position + Vector3.up * (kind == Kind.Snowman ? 2.6f : 1.6f) + transform.forward * 1f;
            var target = gm.Player.transform.position + gm.Player.Velocity * 0.5f;
            if (count > 1) target += Quaternion.Euler(0f, (index - (count - 1) * 0.5f) * 25f, 0f) * (transform.forward * 2f) - transform.forward * 2f;
            Lob.Launch(transform.parent, from, target, 1.15f, kind == Kind.Snowman ? new Color(0.95f, 0.97f, 1f) : new Color(0.25f, 0.1f, 0.35f));
            gm.Sfx.Play(gm.Sfx.Swoosh, 0.5f);
        }

        public override bool Hit(HitKind kind, Vector3 from)
        {
            if (Dead) return false;
            var gm = GameManager.I;
            if (kind == HitKind.Punch)
            {
                var push = transform.position - from;
                push.y = 0f;
                transform.position += push.normalized * 0.8f;
                dazed = 0.4f;
                return false;
            }
            if (invulnerable > 0f) return kind == HitKind.Stomp;
            hp--;
            invulnerable = 1.4f;
            dazed = 1.2f;
            speed += 0.7f;
            throwTimer = Mathf.Min(throwTimer, 1.4f);
            ThirdPersonCamera.Shake(0.25f);
            if (gm != null)
            {
                gm.Sfx.Play(gm.Sfx.Stomp);
                Debris.Burst(Center + Vector3.up, kind == HitKind.Metal ? Color.white : new Color(1f, 0.9f, 0.4f), 16, 5f);
            }
            if (hp > 0)
            {
                gm?.Hud.Show(hp == 1 ? "עוד פעם אחת!" : $"{BossName} כועס! עוד {hp}", 1.5f);
                return true;
            }
            Die();
            return true;
        }

        protected override void OnDefeated()
        {
            if (Boss == this) Boss = null;
            var gm = GameManager.I;
            if (star != null) star.Reveal();
            ThirdPersonCamera.Shake(0.35f);
            if (gm != null)
            {
                gm.SlowMotion(0.3f, 1f);
                gm.Sfx.Say(gm.Sfx.SayYay);
                gm.Sfx.Play(gm.Sfx.Reveal);
                gm.Hud.Show($"ניצחת את {BossName}! כוכב הופיע", 3f);
            }
        }
    }

    /// <summary>A snowball / ink ball thrown in an arc: hurts Maya on contact, bursts on the ground.</summary>
    public class Lob : MonoBehaviour
    {
        Vector3 velocity;
        Color colour;
        float life = 5f;

        public static void Launch(Transform parent, Vector3 from, Vector3 target, float flightTime, Color colour)
        {
            var go = Art.Prim(PrimitiveType.Sphere, "Lob", parent, Vector3.zero, Vector3.one * 0.55f, Art.Solid(colour, 0.3f), Art.Col.None);
            go.transform.position = from;
            var l = go.AddComponent<Lob>();
            l.colour = colour;
            var d = target + Vector3.up * 0.8f - from;
            // v = d / t - g t / 2 (g pointing down)
            l.velocity = d / flightTime + Vector3.up * (0.5f * 20f * flightTime);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            velocity += Vector3.down * 20f * dt;
            var next = transform.position + velocity * dt;
            var gm = GameManager.I;
            if (gm != null && gm.Player != null && gm.State == GameState.Playing &&
                (gm.Player.transform.position + Vector3.up * 0.8f - next).sqrMagnitude < 0.8f * 0.8f)
            {
                if (!gm.Player.Invulnerable) gm.Damage(1, transform.position);
                Pop();
                return;
            }
            if (life <= 0f || Physics.Linecast(transform.position, next, ~(1 << 2), QueryTriggerInteraction.Ignore) ||
                (gm != null && next.y < gm.WaterY))
            {
                Pop();
                return;
            }
            transform.position = next;
        }

        void Pop()
        {
            Debris.Burst(transform.position, colour, 10, 3.5f);
            Destroy(gameObject);
        }
    }
}
