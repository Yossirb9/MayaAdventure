using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Purple switch: step on it (or ground-pound it) and its purple blocks appear for a few seconds,
    /// ticking faster as time runs out.
    /// </summary>
    public class PurpleSwitch : MonoBehaviour
    {
        readonly List<TempBlock> blocks = new List<TempBlock>();
        Transform cap;
        float seconds, left = -1f, tick;

        public static PurpleSwitch Build(Transform parent, Vector3 position, float seconds = 9f)
        {
            var go = new GameObject("PurpleSwitch");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var s = go.AddComponent<PurpleSwitch>();
            s.seconds = seconds;
            var purple = Art.Glow(new Color(0.65f, 0.3f, 1f), 0.6f, 0.6f, 0.2f);
            Art.Prim(PrimitiveType.Cylinder, "Base", go.transform, new Vector3(0f, 0.08f, 0f), new Vector3(1.9f, 0.08f, 1.9f), Art.Solid(new Color(0.85f, 0.85f, 0.9f), 0.5f));
            s.cap = Art.Prim(PrimitiveType.Cylinder, "Cap", go.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.3f, 0.15f, 1.3f), purple, Art.Col.None).transform;
            return s;
        }

        /// <summary>Adds a block (centre, size) that this switch makes solid.</summary>
        public TempBlock Block(Vector3 center, Vector3 size)
        {
            var b = TempBlock.Build(transform.parent, center, size);
            blocks.Add(b);
            return b;
        }

        /// <summary>A line of square steps from <paramref name="from"/> to <paramref name="to"/> (step centres).</summary>
        public void Stairs(Vector3 from, Vector3 to, int count, float size = 2f)
        {
            for (int i = 0; i < count; i++)
                Block(Vector3.Lerp(from, to, count == 1 ? 0f : i / (count - 1f)), new Vector3(size, 0.6f, size));
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null) return;
            float dt = Time.deltaTime;
            if (left >= 0f)
            {
                left -= dt;
                tick -= dt;
                if (tick <= 0f)
                {
                    gm.Sfx.Play(gm.Sfx.Tick, 0.6f);
                    tick = left < 3f ? 0.25f : 0.5f;
                }
                foreach (var b in blocks) b.Blink(left < 2f && Mathf.Repeat(left * 6f, 1f) > 0.5f);
                if (left < 0f)
                {
                    foreach (var b in blocks) b.Set(false);
                    cap.localPosition = new Vector3(0f, 0.3f, 0f);
                }
                return;
            }
            var rel = gm.Player.transform.position - transform.position;
            if (gm.State == GameState.Playing && new Vector2(rel.x, rel.z).magnitude < 1.15f && rel.y > -0.3f && rel.y < 0.9f && gm.Player.Velocity.y <= 0.5f)
            {
                left = seconds;
                tick = 0f;
                cap.localPosition = new Vector3(0f, 0.15f, 0f);
                foreach (var b in blocks) b.Set(true);
                gm.Sfx.Play(gm.Sfx.Block);
                gm.Hud.Show("מהר! הקוביות הסגולות ייעלמו בקרוב", 2f);
            }
        }
    }

    /// <summary>A purple block that only exists while its switch is pressed (a faint marker otherwise).</summary>
    public class TempBlock : MonoBehaviour
    {
        GameObject solid, marker;
        Renderer solidRenderer;

        public static TempBlock Build(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("TempBlock");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var t = go.AddComponent<TempBlock>();
            t.solid = Art.Prim(PrimitiveType.Cube, "Solid", go.transform, Vector3.zero, size, Art.Glow(new Color(0.6f, 0.3f, 1f), 0.5f, 0.6f, 0.1f));
            t.solidRenderer = t.solid.GetComponent<Renderer>();
            t.marker = Art.Prim(PrimitiveType.Cube, "Marker", go.transform, Vector3.zero, new Vector3(size.x, 0.05f, size.z),
                                Art.Glow(new Color(0.5f, 0.25f, 0.9f), 0.8f, 0.3f, 0f), Art.Col.None, null, false);
            t.Set(false);
            return t;
        }

        public void Set(bool on)
        {
            solid.SetActive(on);
            marker.SetActive(!on);
        }

        public void Blink(bool off)
        {
            if (solidRenderer != null) solidRenderer.enabled = !off;
        }
    }

    /// <summary>A green warp pipe: stand on top and press pound (Ctrl / E / the pound button) to travel to its partner.</summary>
    public class WarpPipe : MonoBehaviour
    {
        WarpPipe partner;
        float topY;
        static float lastWarp = -10f;
        static int onPipeFrame = -10;

        /// <summary>Maya stands on a pipe (this frame or the last), so the pound button enters it instead of punching.</summary>
        public static bool PlayerOnPipe => Time.frameCount - onPipeFrame <= 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lastWarp = -10f;
            onPipeFrame = -10;
        }

        public static void Pair(Transform parent, Vector3 a, Vector3 b)
        {
            var pa = Build(parent, a);
            var pb = Build(parent, b);
            pa.partner = pb;
            pb.partner = pa;
        }

        static WarpPipe Build(Transform parent, Vector3 position)
        {
            var go = new GameObject("WarpPipe");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var w = go.AddComponent<WarpPipe>();
            w.topY = 1.68f;
            if (Props.Has("pipe")) Props.Spawn("pipe", go.transform, Vector3.zero, new Vector3(2.4f, 3f, 2.4f), 0f, Props.Col.Box);
            else Art.Prim(PrimitiveType.Cylinder, "Pipe", go.transform, new Vector3(0f, 0.84f, 0f), new Vector3(2.4f, 0.84f, 2.4f), Art.Solid(new Color(0.2f, 0.7f, 0.3f), 0.5f));
            Art.Prim(PrimitiveType.Cylinder, "Hole", go.transform, new Vector3(0f, w.topY + 0.01f, 0f), new Vector3(1.6f, 0.005f, 1.6f), Art.Solid(Color.black, 0f), Art.Col.None, null, false);
            return w;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing || partner == null) return;
            var p = gm.Player;
            var rel = p.transform.position - transform.position;
            bool onTop = new Vector2(rel.x, rel.z).magnitude < 1f && Mathf.Abs(rel.y - topY) < 0.3f && p.Grounded;
            if (!onTop || p.Hidden) return;
            onPipeFrame = Time.frameCount;
            gm.Hud.Hint(TouchControls.Active ? "מכה / הלמות - להיכנס לצינור" : "Ctrl / E - להיכנס לצינור");
            if (Controls.PoundDown && Time.time - lastWarp > 1.2f) StartCoroutine(Warp(p));
        }

        IEnumerator Warp(MayaController p)
        {
            lastWarp = Time.time;
            var gm = GameManager.I;
            gm.Sfx.Play(gm.Sfx.Warp);
            p.SetHidden(true);
            gm.Hud.Fade(1f, 0.35f);
            yield return new WaitForSeconds(0.45f);
            var exit = partner.transform.position + Vector3.up * (partner.topY + 0.05f);
            p.Teleport(exit, p.transform.eulerAngles.y, false);
            gm.Cam.Snap();
            gm.Hud.Fade(0f, 0.35f);
            yield return new WaitForSeconds(0.2f);
            p.SetHidden(false);
            p.Bounce(9f);
            gm.Sfx.Play(gm.Sfx.Warp, 0.7f);
            lastWarp = Time.time;
        }
    }

    /// <summary>Cannon: walk into the hatch and Maya gets loaded, then fired in a high arc to <c>target</c>.</summary>
    public class Cannon : MonoBehaviour
    {
        Vector3 target;
        float flightTime;
        bool busy, armed = true;
        Transform barrel;

        public static Cannon Build(Transform parent, Vector3 position, Vector3 target, float flightTime = 2.2f)
        {
            var go = new GameObject("Cannon");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var c = go.AddComponent<Cannon>();
            c.target = target;
            c.flightTime = flightTime;
            var flat = target - position;
            flat.y = 0f;
            go.transform.localRotation = Quaternion.LookRotation(flat.sqrMagnitude > 0.01f ? flat : Vector3.forward);
            var iron = Art.Solid(new Color(0.16f, 0.16f, 0.2f), 0.8f, 0.6f);
            var gold = Art.Glow(new Color(1f, 0.75f, 0.2f), 0.3f, 0.8f, 1f);
            // Mesh collider: a cylinder primitive's capsule collider would bulge into a ball and lift Maya off the hatch.
            Art.Prim(PrimitiveType.Cylinder, "Base", go.transform, new Vector3(0f, 0.25f, 0f), new Vector3(2.6f, 0.25f, 2.6f), Art.Textured("brick", new Vector2(4f, 1f)), Art.Col.Mesh);
            c.barrel = new GameObject("Barrel").transform;
            c.barrel.SetParent(go.transform, false);
            c.barrel.localPosition = new Vector3(0f, 0.9f, 0f);
            c.barrel.localRotation = Quaternion.Euler(-50f, 0f, 0f);
            Art.Prim(PrimitiveType.Cylinder, "Tube", c.barrel, new Vector3(0f, 0f, 0.6f), new Vector3(1.1f, 0.9f, 1.1f), iron, Art.Col.None, Quaternion.Euler(90f, 0f, 0f));
            Art.Prim(PrimitiveType.Cylinder, "Rim", c.barrel, new Vector3(0f, 0f, 1.45f), new Vector3(1.3f, 0.08f, 1.3f), gold, Art.Col.None, Quaternion.Euler(90f, 0f, 0f));
            Art.Prim(PrimitiveType.Sphere, "Back", c.barrel, new Vector3(0f, 0f, -0.3f), Vector3.one * 1.1f, iron, Art.Col.None);
            Art.Prim(PrimitiveType.Cylinder, "Hatch", go.transform, new Vector3(0f, 0.51f, -0.9f), new Vector3(0.9f, 0.01f, 0.9f), Art.Glow(new Color(1f, 0.85f, 0.3f), 0.6f), Art.Col.None, null, false);
            return c;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (busy || gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            var p = gm.Player;
            var rel = p.transform.position - transform.position;
            bool inside = new Vector2(rel.x, rel.z).magnitude < 1.4f && rel.y > -0.2f && rel.y < 2f && !p.Hidden;
            if (!inside) { armed = true; return; }
            if (armed) StartCoroutine(Fire(p));
        }

        IEnumerator Fire(MayaController p)
        {
            busy = true;
            armed = false;
            var gm = GameManager.I;
            gm.Sfx.Play(gm.Sfx.Warp, 0.6f);
            p.SetHidden(true);
            p.Teleport(transform.position + Vector3.up * 0.5f, transform.eulerAngles.y, false);
            gm.Hud.Show("3... 2... 1...", 1.1f);
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                barrel.localScale = Vector3.one * (1f + Mathf.Sin(t * 30f) * 0.03f);
                yield return null;
            }
            barrel.localScale = Vector3.one;
            Vector3 muzzle = barrel.TransformPoint(new Vector3(0f, 0f, 1.7f));
            Vector3 dest = transform.parent.TransformPoint(target);
            float g = p.gravity;
            Vector3 v = (dest - muzzle) / flightTime + Vector3.up * (0.5f * g * flightTime);
            gm.Sfx.Play(gm.Sfx.Boom);
            Debris.Burst(muzzle, new Color(0.9f, 0.9f, 0.9f), 16, 5f);
            p.CannonLaunch(muzzle, v, flightTime);
            yield return new WaitForSeconds(1f);
            busy = false;
        }
    }

    /// <summary>A wooden sign: walk up to it to read the hint.</summary>
    public class Sign : MonoBehaviour
    {
        string text;

        public static Sign Build(Transform parent, Vector3 position, float yaw, string text)
        {
            var go = new GameObject("Sign");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (Props.Has("sign")) Props.Spawn("sign", go.transform, Vector3.zero, 2.6f, 0f, Props.Col.Box);
            else
            {
                Art.Prim(PrimitiveType.Cube, "Post", go.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.15f, 1.2f, 0.15f), Art.Textured("plank", Vector2.one));
                Art.Prim(PrimitiveType.Cube, "Board", go.transform, new Vector3(0f, 1.2f, 0f), new Vector3(1.2f, 0.7f, 0.1f), Art.Textured("plank", Vector2.one));
            }
            var s = go.AddComponent<Sign>();
            s.text = text;
            return s;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            if ((gm.Player.transform.position - transform.position).sqrMagnitude < 2.8f * 2.8f) gm.Hud.Hint(text);
        }
    }

    /// <summary>A floor button that only a ground pound can press; runs an action once.</summary>
    public class PoundSwitch : PoundTarget
    {
        System.Action action;
        Transform cap;
        bool pressed;

        public static PoundSwitch Build(Transform parent, Vector3 position, System.Action onPressed)
        {
            var go = new GameObject("PoundSwitch");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var s = go.AddComponent<PoundSwitch>();
            s.action = onPressed;
            Art.Prim(PrimitiveType.Cylinder, "Base", go.transform, new Vector3(0f, 0.1f, 0f), new Vector3(2.2f, 0.1f, 2.2f), Art.Solid(new Color(0.75f, 0.75f, 0.8f), 0.6f, 0.5f));
            s.cap = Art.Prim(PrimitiveType.Cylinder, "Cap", go.transform, new Vector3(0f, 0.32f, 0f), new Vector3(1.5f, 0.14f, 1.5f), Art.Glow(new Color(1f, 0.75f, 0.2f), 0.7f)).transform;
            return s;
        }

        public override float Radius => 1.8f;

        public override void OnPound()
        {
            if (pressed) return;
            pressed = true;
            cap.localPosition = new Vector3(0f, 0.18f, 0f);
            var gm = GameManager.I;
            if (gm != null) gm.Sfx.Play(gm.Sfx.Block);
            action?.Invoke();
        }
    }
}
