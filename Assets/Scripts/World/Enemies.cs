using System.Collections;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Small helpers shared by the enemies in this file.</summary>
    static class EnemyArt
    {
        public static void Eyes(Transform parent, float y, float z, float spread, float size, bool angry = true)
        {
            var white = Art.Solid(Color.white, 0.6f);
            var black = Art.Solid(new Color(0.06f, 0.05f, 0.08f), 0.5f);
            for (int s = -1; s <= 1; s += 2)
            {
                Art.Prim(PrimitiveType.Sphere, "Eye", parent, new Vector3(spread * s, y, z), new Vector3(size, size * 1.25f, size * 0.6f), white, Art.Col.None);
                Art.Prim(PrimitiveType.Sphere, "Pupil", parent, new Vector3(spread * s * 0.9f, y - size * 0.1f, z + size * 0.28f), Vector3.one * size * 0.45f, black, Art.Col.None);
                if (angry)
                    Art.Prim(PrimitiveType.Cube, "Brow", parent, new Vector3(spread * s, y + size * 0.75f, z + size * 0.2f), new Vector3(size * 1.1f, size * 0.22f, size * 0.22f),
                             black, Art.Col.None, Quaternion.Euler(0f, 0f, 22f * s));
            }
        }

        public static bool PlayerNear(Vector3 p, float range, out Vector3 toPlayer)
        {
            toPlayer = Vector3.zero;
            var gm = GameManager.I;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing || gm.Player.Hidden) return false;
            toPlayer = gm.Player.transform.position - p;
            return toPlayer.sqrMagnitude < range * range;
        }

        public static void Flash(Renderer[] renderers, bool on)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!on) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(block);
                block.SetColor("baseColorFactor", new Color(1.8f, 0.6f, 0.5f));
                block.SetColor("_Color", new Color(1.8f, 0.6f, 0.5f));
                block.SetColor("_BaseColor", new Color(1.8f, 0.6f, 0.5f));
                r.SetPropertyBlock(block);
            }
        }
    }

    /// <summary>
    /// Walking bomb: waddles after Maya and lights its fuse when it gets close, then explodes.
    /// Stomp it or punch it before it goes off.
    /// </summary>
    public class Bomber : Enemy
    {
        Vector3 home, wanderTarget;
        float fuse = -1f;
        Renderer[] renderers;
        Transform spark;
        const float FuseTime = 1.6f, BlastRadius = 3.2f;

        public static Bomber Build(Transform parent, Vector3 position)
        {
            var go = new GameObject("Bomber");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var b = go.AddComponent<Bomber>();
            b.home = b.wanderTarget = position;
            b.bodyRadius = 0.65f;
            b.bodyHeight = 1.3f;
            b.body = new GameObject("Body").transform;
            b.body.SetParent(go.transform, false);
            if (Props.Has("bomb")) Props.Spawn("bomb", b.body, new Vector3(0f, 0.12f, 0f), 2.4f);
            else Art.Prim(PrimitiveType.Sphere, "Bomb", b.body, new Vector3(0f, 0.7f, 0f), Vector3.one * 1.2f, Art.Solid(new Color(0.12f, 0.12f, 0.16f), 0.6f), Art.Col.None);
            EnemyArt.Eyes(b.body, 0.75f, 0.52f, 0.2f, 0.22f, false);
            var feet = Art.Solid(new Color(1f, 0.65f, 0.15f), 0.3f);
            for (int s = -1; s <= 1; s += 2)
                Art.Prim(PrimitiveType.Sphere, "Foot", b.body, new Vector3(0.28f * s, 0.1f, 0.08f), new Vector3(0.34f, 0.2f, 0.46f), feet, Art.Col.None);
            b.spark = Art.Prim(PrimitiveType.Sphere, "Spark", b.body, new Vector3(0f, 1.5f, 0f), Vector3.one * 0.18f, Art.Glow(new Color(1f, 0.7f, 0.2f), 3f), Art.Col.None, null, false).transform;
            b.renderers = b.body.GetComponentsInChildren<Renderer>();
            return b;
        }

        void Update()
        {
            if (Dead) return;
            float dt = Time.deltaTime;
            Vector3 pos = transform.localPosition;
            bool near = EnemyArt.PlayerNear(transform.position, 9f, out var toP) && Mathf.Abs(toP.y) < 2.5f;
            spark.localScale = Vector3.one * (0.14f + Mathf.Abs(Mathf.Sin(Time.time * (fuse >= 0f ? 30f : 8f))) * 0.14f);

            if (fuse >= 0f)
            {
                fuse += dt;
                float k = fuse / FuseTime;
                EnemyArt.Flash(renderers, Mathf.Repeat(fuse * (4f + k * 12f), 1f) > 0.5f);
                body.localScale = Vector3.one * (1f + k * 0.25f);
                if (fuse >= FuseTime) Explode();
                CheckPlayerContact();
                return;
            }

            Vector3 dest = near ? transform.parent.InverseTransformPoint(transform.position + toP) : wanderTarget;
            dest.y = pos.y;
            Vector3 d = dest - pos;
            if (!near && d.sqrMagnitude < 0.2f)
            {
                var c = Random.insideUnitCircle * 3f;
                wanderTarget = home + new Vector3(c.x, 0f, c.y);
            }
            else if (d.sqrMagnitude > 1e-4f)
            {
                pos += d.normalized * Mathf.Min((near ? 2.8f : 1.2f) * dt, d.magnitude);
                Vector3 off = pos - home;
                if (off.sqrMagnitude > 64f) pos = home + off.normalized * 8f;
                transform.localPosition = pos;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * dt);
            }
            body.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * (near ? 11f : 6f))) * 0.08f);
            if (near && new Vector2(toP.x, toP.z).magnitude < 2.4f)
            {
                fuse = 0f;
                var gm = GameManager.I;
                if (gm != null) gm.Sfx.Play(gm.Sfx.Fuse);
            }
            CheckPlayerContact();
        }

        void Explode()
        {
            Dead = true;
            var gm = GameManager.I;
            Vector3 c = Center;
            Debris.Burst(c, new Color(1f, 0.6f, 0.2f), 24, 8f);
            Debris.Burst(c, new Color(0.3f, 0.3f, 0.3f), 14, 5f);
            if (gm != null)
            {
                gm.Sfx.Play(gm.Sfx.Boom);
                if (gm.Player != null && (gm.Player.transform.position + Vector3.up * 0.8f - c).sqrMagnitude < BlastRadius * BlastRadius)
                    gm.Damage(2, c);
            }
            foreach (var box in Punchable.All.ToArray())
                if (box is Crate && (box.PunchPoint - c).sqrMagnitude < BlastRadius * BlastRadius) box.OnPunched(c);
            Destroy(gameObject);
        }
    }

    /// <summary>Spiny: a spiked block that patrols back and forth. Stomping it hurts; punch it or ground-pound next to it.</summary>
    public class Spiny : Enemy
    {
        Vector3 a, b;
        float t, speed;

        public static Spiny Build(Transform parent, Vector3 from, Vector3 to, float speed = 2.2f)
        {
            var go = new GameObject("Spiny");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = from;
            var s = go.AddComponent<Spiny>();
            s.a = from;
            s.b = to;
            s.speed = speed;
            s.stompable = false;
            s.bodyRadius = 0.65f;
            s.bodyHeight = 1.2f;
            s.coinsOnDeath = 2;
            s.body = new GameObject("Body").transform;
            s.body.SetParent(go.transform, false);
            if (Props.Has("spike-block"))
            {
                var m = Props.Spawn("spike-block", s.body, Vector3.zero, 1.3f);
                Props.Tint(m, new Color(1.25f, 0.55f, 0.5f));
            }
            else Art.Prim(PrimitiveType.Cube, "Spikes", s.body, new Vector3(0f, 0.6f, 0f), Vector3.one * 1.1f, Art.Solid(new Color(0.8f, 0.25f, 0.2f), 0.4f), Art.Col.None);
            EnemyArt.Eyes(s.body, 0.72f, 0.6f, 0.2f, 0.2f);
            return s;
        }

        void Update()
        {
            if (Dead) return;
            float len = Mathf.Max(0.1f, Vector3.Distance(a, b));
            t += Time.deltaTime * speed / len;
            float k = Mathf.PingPong(t, 1f);
            var prev = transform.localPosition;
            transform.localPosition = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, k));
            var d = transform.localPosition - prev;
            if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(transform.parent.TransformDirection(d));
            body.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 6f) * 8f, 0f);
            CheckPlayerContact();
        }
    }

    /// <summary>Buzzer: a bee that hovers around its hive and swoops at Maya. One stomp or punch defeats it.</summary>
    public class Buzzer : Enemy
    {
        Vector3 home;
        Vector3 swoopTarget;
        float swoop = -1f, cooldown, phase;
        Transform wingL, wingR;

        public override Vector3 Origin => transform.position - Vector3.up * 0.45f;

        public static Buzzer Build(Transform parent, Vector3 position)
        {
            var go = new GameObject("Buzzer");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var z = go.AddComponent<Buzzer>();
            z.home = position;
            z.phase = Random.value * 10f;
            z.bodyRadius = 0.55f;
            z.bodyHeight = 0.9f;
            z.body = new GameObject("Body").transform;
            z.body.SetParent(go.transform, false);
            var yellow = Art.Solid(new Color(1f, 0.8f, 0.15f), 0.5f);
            var black = Art.Solid(new Color(0.08f, 0.07f, 0.06f), 0.4f);
            Art.Prim(PrimitiveType.Sphere, "Thorax", z.body, Vector3.zero, new Vector3(0.75f, 0.7f, 0.8f), yellow, Art.Col.None);
            Art.Prim(PrimitiveType.Sphere, "Abdomen", z.body, new Vector3(0f, -0.05f, -0.55f), new Vector3(0.7f, 0.65f, 0.85f), yellow, Art.Col.None);
            for (int i = 0; i < 2; i++)
                Art.Prim(PrimitiveType.Cylinder, "Stripe", z.body, new Vector3(0f, -0.05f, -0.42f - i * 0.28f), new Vector3(0.72f, 0.05f, 0.67f), black,
                         Art.Col.None, Quaternion.Euler(90f, 0f, 0f));
            Art.Prim(PrimitiveType.Cylinder, "Stinger", z.body, new Vector3(0f, -0.1f, -1.02f), new Vector3(0.08f, 0.15f, 0.08f), black, Art.Col.None, Quaternion.Euler(90f, 0f, 0f));
            EnemyArt.Eyes(z.body, 0.08f, 0.32f, 0.17f, 0.2f);
            var bee = Nature.Load("Platformer/Bee");
            if (bee != null)
            {
                // Animated Quaternius bee replaces the primitive body.
                foreach (Transform child in z.body) Destroy(child.gameObject);
                var model = Instantiate(bee, z.body, false);
                model.transform.localScale = Vector3.one * 0.6f;
                model.transform.localPosition = new Vector3(0f, -0.55f, 0f);
                var a = model.GetComponentInChildren<Animation>();
                if (a != null && a["Flying"] != null)
                {
                    a["Flying"].wrapMode = WrapMode.Loop;
                    a.Play("Flying");
                }
                z.wingL = new GameObject("WingL").transform;
                z.wingL.SetParent(z.body, false);
                z.wingR = new GameObject("WingR").transform;
                z.wingR.SetParent(z.body, false);
                return z;
            }
            var wing = Art.Glow(new Color(0.85f, 0.95f, 1f), 0.4f, 0.8f, 0f);
            z.wingL = new GameObject("WingL").transform;
            z.wingL.SetParent(z.body, false);
            z.wingL.localPosition = new Vector3(-0.2f, 0.35f, -0.1f);
            Art.Prim(PrimitiveType.Sphere, "Wing", z.wingL, new Vector3(-0.3f, 0.05f, 0f), new Vector3(0.6f, 0.05f, 0.35f), wing, Art.Col.None, null, false);
            z.wingR = new GameObject("WingR").transform;
            z.wingR.SetParent(z.body, false);
            z.wingR.localPosition = new Vector3(0.2f, 0.35f, -0.1f);
            Art.Prim(PrimitiveType.Sphere, "Wing", z.wingR, new Vector3(0.3f, 0.05f, 0f), new Vector3(0.6f, 0.05f, 0.35f), wing, Art.Col.None, null, false);
            return z;
        }

        void Update()
        {
            if (Dead) return;
            float dt = Time.deltaTime;
            float flap = Mathf.Sin(Time.time * 55f) * 35f;
            wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
            wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
            var world = transform.parent;
            Vector3 pos = transform.localPosition;
            cooldown -= dt;

            if (swoop >= 0f)
            {
                swoop += dt;
                Vector3 d = swoopTarget - pos;
                if (d.magnitude < 0.4f || swoop > 2.2f) { swoop = -1f; cooldown = 2f; }
                else
                {
                    pos += d.normalized * 6.5f * dt;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(world.TransformDirection(d)), 540f * dt);
                }
            }
            else
            {
                float ang = Time.time * 1.1f + phase;
                Vector3 orbit = home + new Vector3(Mathf.Cos(ang) * 2.5f, Mathf.Sin(ang * 2.3f) * 0.4f, Mathf.Sin(ang) * 2.5f);
                Vector3 d = orbit - pos;
                pos += Vector3.ClampMagnitude(d, 4f * dt);
                if (d.sqrMagnitude > 1e-4f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(world.TransformDirection(new Vector3(d.x, 0f, d.z) + Vector3.forward * 1e-3f)), 270f * dt);
                if (cooldown <= 0f && EnemyArt.PlayerNear(transform.position, 8f, out var toP) && (GameManager.I.Player.transform.position - world.TransformPoint(home)).sqrMagnitude < 144f)
                {
                    swoop = 0f;
                    swoopTarget = world.InverseTransformPoint(GameManager.I.Player.transform.position + Vector3.up * 0.9f);
                    GameManager.I.Sfx.Play(GameManager.I.Sfx.Swoosh, 0.5f);
                }
            }
            transform.localPosition = pos;
            CheckPlayerContact();
        }
    }

    /// <summary>Snapper: a biting plant living in a green pipe. Stomping it hurts; punch it or pound next to it.</summary>
    public class Snapper : Enemy
    {
        Transform head, jaw;
        float bite = -1f, cooldown;
        Vector3 rest;

        public override Vector3 Origin => head != null ? head.position - Vector3.up * 1f : transform.position;

        public static Snapper Build(Transform parent, Vector3 position, float yaw = 0f)
        {
            var go = new GameObject("Snapper");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var p = go.AddComponent<Snapper>();
            p.stompable = false;
            p.bodyRadius = 0.6f;
            p.bodyHeight = 1.4f;
            p.coinsOnDeath = 2;
            // The pipe stays behind when the plant is defeated.
            var pipe = Props.Has("pipe") ? Props.Spawn("pipe", parent, position, new Vector3(2f, 2.4f, 2f), yaw, Props.Col.Box)
                                         : Art.Prim(PrimitiveType.Cylinder, "Pipe", parent, position + Vector3.up * 0.67f, new Vector3(2f, 0.67f, 2f), Art.Solid(new Color(0.2f, 0.7f, 0.3f), 0.5f));
            pipe.name = "SnapperPipe";
            p.body = new GameObject("Body").transform;
            p.body.SetParent(go.transform, false);
            p.body.localPosition = Vector3.up * 1.34f;
            var stem = Art.Solid(new Color(0.2f, 0.6f, 0.25f), 0.3f);
            Art.Prim(PrimitiveType.Cylinder, "Stem", p.body, new Vector3(0f, 0.25f, 0f), new Vector3(0.22f, 0.25f, 0.22f), stem, Art.Col.None);
            for (int s = -1; s <= 1; s += 2)
                Art.Prim(PrimitiveType.Sphere, "Leaf", p.body, new Vector3(0.35f * s, 0.12f, 0f), new Vector3(0.6f, 0.08f, 0.3f), stem, Art.Col.None, Quaternion.Euler(0f, 0f, -20f * s));
            p.head = new GameObject("Head").transform;
            p.head.SetParent(p.body, false);
            p.head.localPosition = new Vector3(0f, 0.6f, 0f);
            p.rest = p.head.localPosition;
            var red = Art.Solid(new Color(0.9f, 0.15f, 0.15f), 0.5f);
            var spot = Art.Solid(Color.white, 0.5f);
            var mouth = Art.Solid(new Color(0.95f, 0.85f, 0.8f), 0.3f);
            Art.Prim(PrimitiveType.Sphere, "Top", p.head, new Vector3(0f, 0.12f, 0f), new Vector3(1f, 0.55f, 1f), red, Art.Col.None);
            for (int i = 0; i < 5; i++)
            {
                var r = Quaternion.Euler(-30f, i * 72f, 0f);
                Art.Prim(PrimitiveType.Sphere, "Spot", p.head, new Vector3(0f, 0.12f, 0f) + r * new Vector3(0f, 0f, 0.42f), new Vector3(0.18f, 0.18f, 0.06f), spot, Art.Col.None, r);
            }
            p.jaw = new GameObject("Jaw").transform;
            p.jaw.SetParent(p.head, false);
            Art.Prim(PrimitiveType.Sphere, "Lower", p.jaw, new Vector3(0f, -0.12f, 0f), new Vector3(0.95f, 0.4f, 0.95f), red, Art.Col.None);
            for (int i = 0; i < 6; i++)
            {
                var r = Quaternion.Euler(0f, -75f + i * 30f, 0f);
                Art.Prim(PrimitiveType.Cube, "Tooth", p.head, new Vector3(0f, -0.02f, 0f) + r * new Vector3(0f, 0f, 0.4f), new Vector3(0.1f, 0.12f, 0.06f), mouth, Art.Col.None, r * Quaternion.Euler(0f, 0f, 45f));
            }
            return p;
        }

        void Update()
        {
            if (Dead) return;
            float dt = Time.deltaTime;
            cooldown -= dt;
            bool near = EnemyArt.PlayerNear(head.position, 4.2f, out var toP);
            if (near)
            {
                var flat = new Vector3(toP.x, 0f, toP.z);
                if (flat.sqrMagnitude > 0.01f)
                    body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(flat), 300f * dt);
                if (bite < 0f && cooldown <= 0f) bite = 0f;
            }
            float open;
            if (bite >= 0f)
            {
                bite += dt;
                float k = bite / 0.7f;
                float lunge = k < 0.4f ? -0.3f * (k / 0.4f) : k < 0.6f ? Mathf.Lerp(-0.3f, 1.1f, (k - 0.4f) / 0.2f) : Mathf.Lerp(1.1f, 0f, (k - 0.6f) / 0.4f);
                head.localPosition = rest + Vector3.forward * lunge;
                open = k < 0.5f ? 40f : 0f;
                if (k >= 1f) { bite = -1f; cooldown = 0.6f; }
            }
            else
            {
                head.localPosition = rest + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.1f;
                open = 15f + Mathf.Sin(Time.time * 5f) * 12f;
            }
            jaw.localRotation = Quaternion.Euler(open, 0f, 0f);
            CheckPlayerContact();
        }
    }

    /// <summary>
    /// Stone crusher (a thwomp): hovers, drops when Maya walks underneath, rests, then rises again.
    /// Maya can ride on top of it. It can't be defeated.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Crusher : MonoBehaviour
    {
        Vector3 top, bottom;
        float state, timer; // 0 = waiting up, 1 = falling, 2 = resting down, 3 = rising
        float speed;
        const float Half = 1.3f;

        public static Crusher Build(Transform parent, Vector3 floor, float height = 5f)
        {
            var go = new GameObject("Crusher");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Crusher>();
            c.bottom = floor;
            c.top = floor + Vector3.up * height;
            go.transform.localPosition = c.top;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var stoneMat = Art.Textured("rock", new Vector2(1f, 1f), 0.15f);
            Art.Prim(PrimitiveType.Cube, "Block", go.transform, new Vector3(0f, 1f, 0f), new Vector3(Half * 2f, 2f, Half * 2f), stoneMat);
            var dark = Art.Solid(new Color(0.25f, 0.27f, 0.32f), 0.3f);
            for (int i = 0; i < 4; i++)
            {
                var r = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                Art.Prim(PrimitiveType.Cube, "Spike", go.transform, r * new Vector3(0f, 0.15f, Half * 1.35f), new Vector3(0.4f, 0.4f, 0.4f), dark, Art.Col.None, r * Quaternion.Euler(45f, 0f, 45f));
            }
            for (int i = 0; i < 4; i++)
            {
                var face = new GameObject("Face").transform;
                face.SetParent(go.transform, false);
                face.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                face.localPosition = face.localRotation * new Vector3(0f, 0f, Half + 0.01f);
                EnemyArt.Eyes(face, 1.25f, 0f, 0.45f, 0.38f);
                Art.Prim(PrimitiveType.Cube, "Mouth", face, new Vector3(0f, 0.55f, 0.02f), new Vector3(1.1f, 0.18f, 0.06f), Art.Solid(new Color(0.08f, 0.06f, 0.06f), 0.3f), Art.Col.None);
            }
            return c;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var gm = GameManager.I;
            Vector3 pos = transform.localPosition;
            switch ((int)state)
            {
                case 0:
                    if (EnemyArt.PlayerNear(transform.position, 12f, out var toP) && Mathf.Abs(toP.x) < Half + 0.6f && Mathf.Abs(toP.z) < Half + 0.6f && toP.y < 0f)
                    {
                        state = 1;
                        speed = 0f;
                    }
                    break;
                case 1:
                    speed += 60f * dt;
                    pos = Vector3.MoveTowards(pos, bottom, speed * dt);
                    HurtUnderneath();
                    if ((pos - bottom).sqrMagnitude < 1e-4f)
                    {
                        state = 2;
                        timer = 1.4f;
                        if (gm != null)
                        {
                            gm.Sfx.Play(gm.Sfx.Boom, 0.7f);
                            Debris.Burst(transform.parent.TransformPoint(bottom), new Color(0.7f, 0.7f, 0.7f), 16, 5f);
                        }
                    }
                    break;
                case 2:
                    timer -= dt;
                    if (timer <= 0f) state = 3;
                    break;
                case 3:
                    pos = Vector3.MoveTowards(pos, top, 2.2f * dt);
                    if ((pos - top).sqrMagnitude < 1e-4f) { state = 0; }
                    break;
            }
            transform.localPosition = pos;
        }

        void HurtUnderneath()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null) return;
            Vector3 rel = gm.Player.transform.position - transform.position;
            if (Mathf.Abs(rel.x) < Half + 0.3f && Mathf.Abs(rel.z) < Half + 0.3f && rel.y < 0.2f && rel.y > -2.2f)
                gm.Damage(2, gm.Player.transform.position + Vector3.up * 3f + new Vector3(rel.x, 0f, rel.z).normalized * -1f);
        }
    }

    /// <summary>
    /// Chomper: a giant iron ball on a chain, tied to a wooden stake. It lunges at Maya and can't be
    /// hurt, but ground-pounding the stake three times sets it free and reveals a star.
    /// </summary>
    public class Chomper : MonoBehaviour
    {
        Transform ball, mouthTop, mouthBottom;
        Transform[] links;
        Vector3 post, rest;
        float lunge = -1f, cooldown, hop;
        Vector3 lungeFrom, lungeTo;
        bool free;
        const float Chain = 6.5f, BallRadius = 1.1f;

        public static Chomper Build(Transform parent, Vector3 stake, PowerStar star)
        {
            var go = new GameObject("Chomper");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = stake;
            var c = go.AddComponent<Chomper>();
            c.post = Vector3.zero;
            c.rest = new Vector3(2.5f, 0f, 2.5f);
            c.ball = new GameObject("Ball").transform;
            c.ball.SetParent(go.transform, false);
            c.ball.localPosition = c.rest;
            var iron = Art.Solid(new Color(0.1f, 0.1f, 0.13f), 0.85f, 0.6f);
            var white = Art.Solid(Color.white, 0.5f);
            Art.Prim(PrimitiveType.Sphere, "Iron", c.ball, new Vector3(0f, BallRadius, 0f), Vector3.one * BallRadius * 2f, iron, Art.Col.None);
            var faceRoot = new GameObject("Face").transform;
            faceRoot.SetParent(c.ball, false);
            faceRoot.localPosition = new Vector3(0f, BallRadius, 0f);
            EnemyArt.Eyes(faceRoot, 0.4f, 0.85f, 0.38f, 0.36f);
            c.mouthTop = new GameObject("MouthTop").transform;
            c.mouthTop.SetParent(faceRoot, false);
            c.mouthBottom = new GameObject("MouthBottom").transform;
            c.mouthBottom.SetParent(faceRoot, false);
            var red = Art.Solid(new Color(0.7f, 0.1f, 0.12f), 0.3f);
            Art.Prim(PrimitiveType.Sphere, "Mouth", faceRoot, new Vector3(0f, -0.25f, 0.82f), new Vector3(1.1f, 0.5f, 0.5f), red, Art.Col.None);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.4f + i * 0.2f;
                Art.Prim(PrimitiveType.Cube, "Tooth", c.mouthTop, new Vector3(x, -0.05f, 1f), new Vector3(0.13f, 0.13f, 0.06f), white, Art.Col.None, Quaternion.Euler(0f, 0f, 45f));
                Art.Prim(PrimitiveType.Cube, "Tooth", c.mouthBottom, new Vector3(x + 0.1f, -0.45f, 0.95f), new Vector3(0.13f, 0.13f, 0.06f), white, Art.Col.None, Quaternion.Euler(0f, 0f, 45f));
            }
            var linkMat = Art.Solid(new Color(0.35f, 0.35f, 0.4f), 0.8f, 0.8f);
            c.links = new Transform[6];
            for (int i = 0; i < c.links.Length; i++)
                c.links[i] = Art.Prim(PrimitiveType.Sphere, "Link", go.transform, Vector3.zero, new Vector3(0.38f, 0.38f, 0.22f), linkMat, Art.Col.None).transform;
            var stakeGo = new GameObject("Stake");
            stakeGo.transform.SetParent(parent, false);
            stakeGo.transform.localPosition = stake;
            var s = stakeGo.AddComponent<ChomperStake>();
            s.Init(c, star);
            return c;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (free)
            {
                hop += dt;
                ball.localPosition += new Vector3(9f, Mathf.Lerp(14f, -14f, hop / 2f), 4f) * dt;
                ball.Rotate(400f * dt, 0f, 0f, Space.Self);
                if (hop > 2.5f) Destroy(gameObject);
                return;
            }
            cooldown -= dt;
            float chew = Mathf.Abs(Mathf.Sin(Time.time * (lunge >= 0f ? 18f : 6f)));
            mouthTop.localPosition = Vector3.up * chew * 0.12f;
            mouthBottom.localPosition = Vector3.down * chew * 0.12f;

            var gm = GameManager.I;
            bool near = EnemyArt.PlayerNear(transform.position, Chain + 4f, out var toP) && Mathf.Abs(toP.y) < 3f;
            if (lunge >= 0f)
            {
                lunge += dt;
                float k = lunge / 1.3f;
                Vector3 p = k < 0.15f ? Vector3.Lerp(lungeFrom, lungeFrom - (lungeTo - lungeFrom).normalized * 0.8f, k / 0.15f)
                          : k < 0.4f ? Vector3.Lerp(lungeFrom, lungeTo, (k - 0.15f) / 0.25f)
                          : Vector3.Lerp(lungeTo, rest, (k - 0.4f) / 0.6f);
                p.y = k > 0.15f && k < 0.4f ? Mathf.Sin((k - 0.15f) / 0.25f * Mathf.PI) * 1.2f : 0f;
                ball.localPosition = p;
                if (k >= 1f) { lunge = -1f; cooldown = 1f; }
            }
            else
            {
                hop += dt;
                ball.localPosition = rest + Vector3.up * Mathf.Abs(Mathf.Sin(hop * 4f)) * 0.35f;
                if (near && cooldown <= 0f)
                {
                    lunge = 0f;
                    lungeFrom = ball.localPosition;
                    lungeFrom.y = 0f;
                    var flat = transform.InverseTransformDirection(new Vector3(toP.x, 0f, toP.z));
                    lungeTo = Vector3.ClampMagnitude(flat, Chain);
                    rest = Vector3.ClampMagnitude(flat, 3f);
                    if (gm != null) gm.Sfx.Play(gm.Sfx.Swoosh, 0.9f);
                }
            }
            var look = near ? new Vector3(toP.x, 0f, toP.z) : transform.TransformDirection(ball.localPosition);
            if (look.sqrMagnitude > 0.01f) ball.rotation = Quaternion.RotateTowards(ball.rotation, Quaternion.LookRotation(look), 400f * dt);
            UpdateLinks();

            if (gm != null && gm.Player != null && gm.State == GameState.Playing &&
                (gm.Player.transform.position + Vector3.up * 0.8f - (ball.position + Vector3.up * BallRadius)).sqrMagnitude < (BallRadius + 0.45f) * (BallRadius + 0.45f))
                gm.Damage(lunge >= 0f ? 2 : 1, ball.position + Vector3.up * BallRadius);
        }

        void UpdateLinks()
        {
            Vector3 a = post + Vector3.up * 0.7f;
            Vector3 b = ball.localPosition + Vector3.up * BallRadius * 0.6f;
            for (int i = 0; i < links.Length; i++)
            {
                float k = (i + 1f) / (links.Length + 1f);
                var p = Vector3.Lerp(a, b, k);
                p.y -= Mathf.Sin(k * Mathf.PI) * Mathf.Max(0f, 1.2f - (b - a).magnitude * 0.12f);
                links[i].localPosition = p;
                links[i].localRotation = Quaternion.LookRotation((b - a).sqrMagnitude > 0.01f ? b - a : Vector3.forward) * Quaternion.Euler(0f, 0f, i * 90f);
            }
        }

        public void Free()
        {
            free = true;
            hop = 0f;
            foreach (var l in links) l.SetParent(ball, true);
        }
    }

    /// <summary>The stake a <see cref="Chomper"/> is tied to: three ground pounds drive it in.</summary>
    public class ChomperStake : PoundTarget
    {
        Chomper chomper;
        PowerStar star;
        Transform pole;
        int pounds;

        public void Init(Chomper c, PowerStar s)
        {
            chomper = c;
            star = s;
            pole = Art.Prim(PrimitiveType.Cylinder, "Pole", transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.55f, 0.6f), Art.Textured("plank", new Vector2(1f, 2f))).transform;
            Art.Prim(PrimitiveType.Cylinder, "Cap", pole, new Vector3(0f, 1f, 0f), new Vector3(1.15f, 0.08f, 1.15f), Art.Solid(new Color(0.55f, 0.4f, 0.25f), 0.2f), Art.Col.None);
        }

        public override float Radius => 1.5f;

        public override void OnPound()
        {
            if (pounds >= 3) return;
            pounds++;
            pole.localPosition -= Vector3.up * 0.33f;
            var gm = GameManager.I;
            if (pounds < 3)
            {
                gm?.Hud.Show(pounds == 1 ? "היתד זזה! עוד שתי הלמות" : "עוד הלמות אחת!", 1.5f);
                return;
            }
            Destroy(pole.GetComponent<Collider>());
            chomper.Free();
            if (star != null) star.Reveal();
            if (gm != null)
            {
                gm.Sfx.Play(gm.Sfx.Reveal);
                gm.Hud.Show("כלב השרשרת השתחרר! כוכב הופיע", 3f);
            }
        }
    }

    /// <summary>
    /// King Grumble: a big boss Grumble wearing a crown. Three stomps (or pounds on his head) win the
    /// fight; punches only push him back. Every few seconds he jumps and sends out a shockwave, so jump over it.
    /// </summary>
    public class KingGrumble : Grumble
    {
        int hp = 3, maxHp = 3;
        float invulnerable, dazed, waveTimer = 2.5f, wave = -1f;

        /// <summary>The king Maya is fighting right now (the HUD shows his health bar).</summary>
        public static KingGrumble Engaged { get; private set; }
        public int Hp => hp;
        public int MaxHp => maxHp;
        /// <summary>The shockwave ring's radius, or -1 when there is none.</summary>
        public float WaveRadius => wave < 0f ? -1f : wave * 9f;
        public Vector3 WaveCenter => waveCenter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEngaged() => Engaged = null;

        /// <summary>Jump up and send out a shockwave now (used by the trailer).</summary>
        public void SlamNow()
        {
            if (!Dead && wave < 0f && dazed <= 0f) StartCoroutine(JumpSlam());
        }
        Transform ring;
        Mesh ringMesh;
        readonly Vector3[] ringVerts = new Vector3[2 * 65];
        int[] ringTris;
        Renderer[] renderers;
        PowerStar star;
        Vector3 waveCenter;

        public static KingGrumble Build(Transform parent, Vector3 position, PowerStar star, int hp = 3)
        {
            var k = Build<KingGrumble>(parent, position, 2.4f, "character-oobi");
            k.star = star;
            k.hp = k.maxHp = hp;
            k.leash = 9f;
            k.chaseRange = 14f;
            k.chaseSpeed = 2.6f;
            k.speed = 1.2f;
            k.coinsOnDeath = 5;
            k.AddCrown();
            // The shockwave is a thin glowing band that races outward (rebuilt each frame at its current radius).
            k.ringMesh = new Mesh { name = "Shockwave" };
            k.ringMesh.MarkDynamic();
            var ringGo = new GameObject("Shockwave");
            ringGo.transform.SetParent(parent, false);
            ringGo.AddComponent<MeshFilter>().sharedMesh = k.ringMesh;
            var mr = ringGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Art.Glow(new Color(1f, 0.45f, 0.2f), 0.9f, 0.3f, 0f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            k.ring = ringGo.transform;
            k.ring.gameObject.SetActive(false);
            return k;
        }

        void AddCrown()
        {
            var crown = new GameObject("Crown").transform;
            crown.SetParent(body, false);
            crown.localPosition = new Vector3(0f, 1.18f, 0f);
            var gold = Art.Glow(new Color(1f, 0.8f, 0.2f), 0.5f, 0.9f, 1f);
            Art.Prim(PrimitiveType.Cylinder, "Band", crown, Vector3.zero, new Vector3(0.55f, 0.08f, 0.55f), gold, Art.Col.None);
            for (int i = 0; i < 5; i++)
            {
                var r = Quaternion.Euler(0f, i * 72f, 0f);
                Art.MeshObject("Point", Art.ConeMesh(), crown, r * new Vector3(0f, 0.08f, 0.24f), new Vector3(0.12f, 0.22f, 0.12f), gold);
            }
        }

        protected override void OnModelLoaded(Transform model)
        {
            if (walkClip == "Walk") Props.Tint(model.gameObject, new Color(1.25f, 0.7f, 1.35f));
            AddCrown();
            renderers = body.GetComponentsInChildren<Renderer>();
        }

        public override bool Hit(HitKind kind, Vector3 from)
        {
            if (Dead) return false;
            var gm = GameManager.I;
            if (kind == HitKind.Punch)
            {
                var push = transform.position - from;
                push.y = 0f;
                transform.position += push.normalized * 1.2f;
                dazed = 0.5f;
                return false;
            }
            if (invulnerable > 0f) return kind == HitKind.Stomp; // still bounce Maya off his head
            hp--;
            invulnerable = 1.4f;
            // Each hit makes him angrier: faster, and the next shockwave comes sooner.
            chaseSpeed += 0.6f;
            waveTimer = Mathf.Min(waveTimer, 1.6f);
            dazed = 1.4f;
            if (gm != null)
            {
                gm.Sfx.Play(gm.Sfx.Stomp);
                Debris.Burst(Center + Vector3.up, new Color(1f, 0.85f, 0.3f), 14, 5f);
            }
            if (hp > 0)
            {
                gm?.Hud.Show(hp == 1 ? "עוד פעם אחת!" : $"המלך כועס! עוד {hp}", 1.5f);
                return true;
            }
            Die();
            return true;
        }

        protected override void Update()
        {
            bool near = !Dead && EnemyArt.PlayerNear(transform.position, 20f, out _);
            if (near) Engaged = this;
            else if (Engaged == this) Engaged = null;
            if (Dead) return;
            float dt = Time.deltaTime;
            if (invulnerable > 0f)
            {
                invulnerable -= dt;
                if (renderers != null) EnemyArt.Flash(renderers, invulnerable > 0f && Mathf.Repeat(invulnerable * 8f, 1f) > 0.5f);
            }
            UpdateWave(dt);
            if (dazed > 0f)
            {
                dazed -= dt;
                body.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 25f) * 10f, 0f);
                if (invulnerable <= 0f) CheckPlayerContact();
                return;
            }
            body.localRotation = Quaternion.identity;
            waveTimer -= dt;
            if (waveTimer <= 0f && wave < 0f && EnemyArt.PlayerNear(transform.position, 16f, out _)) StartCoroutine(JumpSlam());
            base.Update();
        }

        IEnumerator JumpSlam()
        {
            waveTimer = 3.2f + Random.value * 1.6f; // more aggressive than before
            dazed = 1.1f;
            var start = body.localPosition;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                if (Dead) yield break;
                body.localPosition = start + Vector3.up * Mathf.Sin(t / 0.9f * Mathf.PI) * 2.2f;
                yield return null;
            }
            body.localPosition = start;
            var gm = GameManager.I;
            if (gm != null) gm.Sfx.Play(gm.Sfx.Boom, 0.8f);
            Debris.Burst(transform.position, new Color(0.75f, 0.65f, 0.45f), 18, 6f);
            wave = 0f;
            waveCenter = transform.position;
            ring.position = waveCenter + Vector3.up * 0.05f;
            ring.gameObject.SetActive(true);
        }

        void UpdateWave(float dt)
        {
            if (wave < 0f) return;
            wave += dt;
            float r = wave * 9f;
            ring.localScale = Vector3.one;
            BuildRing(r);
            var gm = GameManager.I;
            if (gm != null && gm.Player != null && gm.Player.Grounded)
            {
                var d = gm.Player.transform.position - waveCenter;
                float flat = new Vector2(d.x, d.z).magnitude;
                if (Mathf.Abs(flat - r) < 0.7f && Mathf.Abs(d.y) < 0.8f) gm.Damage(1, waveCenter);
            }
            if (r > 11f)
            {
                wave = -1f;
                ring.gameObject.SetActive(false);
            }
        }

        void BuildRing(float r)
        {
            const int seg = 64;
            float inner = Mathf.Max(0f, r - 0.55f), wobble = Mathf.Sin(Time.time * 30f) * 0.04f;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ringVerts[i * 2] = d * inner + Vector3.up * 0.06f;
                ringVerts[i * 2 + 1] = d * r + Vector3.up * (0.22f + wobble);
            }
            if (ringTris == null)
            {
                ringTris = new int[seg * 12];
                for (int i = 0; i < seg; i++)
                {
                    int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3, t = i * 12;
                    // both faces, so it reads from any angle
                    ringTris[t] = a0; ringTris[t + 1] = a1; ringTris[t + 2] = b0;
                    ringTris[t + 3] = b0; ringTris[t + 4] = a1; ringTris[t + 5] = b1;
                    ringTris[t + 6] = a0; ringTris[t + 7] = b0; ringTris[t + 8] = a1;
                    ringTris[t + 9] = b0; ringTris[t + 10] = b1; ringTris[t + 11] = a1;
                }
                ringMesh.vertices = ringVerts;
                ringMesh.triangles = ringTris;
            }
            ringMesh.vertices = ringVerts;
            ringMesh.RecalculateNormals();
            ringMesh.RecalculateBounds();
        }

        protected override void OnDefeated()
        {
            if (ring != null) Destroy(ring.gameObject);
            var gm = GameManager.I;
            if (star != null) star.Reveal();
            if (gm != null)
            {
                gm.Sfx.Play(gm.Sfx.Reveal);
                gm.Hud.Show("ניצחת את המלך! כוכב הופיע", 3f);
            }
        }
    }
}
