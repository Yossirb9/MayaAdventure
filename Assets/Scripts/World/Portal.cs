using UnityEngine;

namespace MayaAdventure
{
    /// <summary>A glowing gate to another level; opens once Maya has enough stars.</summary>
    public class Portal : MonoBehaviour
    {
        int target, required;
        Transform swirl;
        Renderer swirlRenderer;
        bool lastUnlocked;
        float cooldown = 1.5f;

        public static Portal Build(Transform parent, Vector3 position, float yaw, int target, int required)
        {
            var go = new GameObject($"Portal to {target}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var p = go.AddComponent<Portal>();
            p.target = target;
            p.required = required;

            if (Props.Has("door-large-open"))
                Props.Spawn("door-large-open", go.transform, Vector3.zero, new Vector3(3.4f, 3.6f, 3.4f));
            else
                Art.Prim(PrimitiveType.Cube, "Frame", go.transform, new Vector3(0f, 1.8f, 0f), new Vector3(3.2f, 3.6f, 0.4f),
                         Art.Solid(new Color(0.5f, 0.45f, 0.6f)), Art.Col.None);

            p.swirl = new GameObject("Swirl").transform;
            p.swirl.SetParent(go.transform, false);
            p.swirl.localPosition = new Vector3(0f, 1.55f, 0f);
            var disc = Art.Prim(PrimitiveType.Cylinder, "Disc", p.swirl, Vector3.zero, new Vector3(2.3f, 0.02f, 2.7f),
                                Art.Glow(new Color(0.55f, 0.35f, 1f), 2.2f, 0.2f, 0f), Art.Col.None, Quaternion.Euler(90f, 0f, 0f), false);
            p.swirlRenderer = disc.GetComponent<Renderer>();
            var light = p.swirl.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.6f;
            light.color = new Color(0.65f, 0.45f, 1f);
            p.lastUnlocked = !p.Unlocked; // force first colour update
            return p;
        }

        bool Unlocked => GameManager.I != null && GameManager.I.Stars >= required;

        void Update()
        {
            swirl.localRotation = Quaternion.Euler(0f, 0f, Time.time * 90f);
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.04f;
            swirl.localScale = new Vector3(pulse, pulse, 1f);

            bool unlocked = Unlocked;
            if (unlocked != lastUnlocked)
            {
                lastUnlocked = unlocked;
                var c = unlocked ? new Color(0.55f, 0.35f, 1f) : new Color(0.45f, 0.45f, 0.5f);
                swirlRenderer.sharedMaterial = Art.Glow(c, unlocked ? 2.2f : 0.4f, 0.2f, 0f);
                var light = swirl.GetComponent<Light>();
                light.color = c;
                light.intensity = unlocked ? 1.6f : 0.3f;
            }

            var gm = GameManager.I;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            if (cooldown > 0f) cooldown -= Time.deltaTime;
            Vector3 d = gm.Player.transform.position - transform.position;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            if (horizontal < 8f)
            {
                string name = Levels.Names[target];
                gm.Hud.Hint(unlocked ? $"שער אל: {name}"
                                     : $"השער אל {name} ייפתח עם {required} כוכבים (יש לך {gm.Stars})");
            }
            if (horizontal < 1.5f && Mathf.Abs(d.y) < 2.5f && cooldown <= 0f)
            {
                cooldown = 2f;
                if (unlocked) gm.EnterLevel(target);
                else gm.Sfx.Play(gm.Sfx.Block);
            }
        }
    }
}
