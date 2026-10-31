using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Hurts Maya on contact: spinning saws (optionally on a track) and spike traps.</summary>
    public class Hazard : MonoBehaviour
    {
        float radius = 1f;
        Vector3 centerOffset;
        Transform spin;
        float spinSpeed;

        /// <summary>Spinning saw blade that travels between two points.</summary>
        public static Hazard Saw(Transform parent, Vector3 from, Vector3 to, float period, float yaw = 0f)
        {
            var mover = Mover.Create(parent, "Saw", from, to, period);
            mover.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var h = mover.gameObject.AddComponent<Hazard>();
            h.spin = new GameObject("Blade").transform;
            h.spin.SetParent(mover.transform, false);
            if (Props.Has("saw")) Props.Spawn("saw", h.spin, Vector3.zero, 2.4f);
            else Art.Prim(PrimitiveType.Cylinder, "Blade", h.spin, Vector3.zero, new Vector3(1.9f, 0.05f, 1.9f),
                          Art.Solid(new Color(0.7f, 0.7f, 0.75f), 0.8f, 0.9f), Art.Col.None, Quaternion.Euler(90f, 0f, 0f));
            h.spinSpeed = 540f;
            h.radius = 0.95f;
            return h;
        }

        /// <summary>Spike trap lying on the floor at <paramref name="position"/>.</summary>
        public static Hazard Spikes(Transform parent, Vector3 position, float scale = 2.6f)
        {
            var go = new GameObject("Spikes");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            if (Props.Has("trap-spikes-large")) Props.Spawn("trap-spikes-large", go.transform, Vector3.zero, scale);
            else Art.Prim(PrimitiveType.Cube, "Spikes", go.transform, new Vector3(0f, 0.15f, 0f), new Vector3(1.8f, 0.3f, 1.8f),
                          Art.Solid(new Color(0.6f, 0.6f, 0.65f), 0.7f, 0.8f), Art.Col.None);
            var h = go.AddComponent<Hazard>();
            h.radius = 0.38f * scale;
            h.centerOffset = new Vector3(0f, 0.2f, 0f);
            return h;
        }

        void Update()
        {
            if (spin != null) spin.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
            var gm = GameManager.I;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing || gm.Player.Invulnerable) return;
            Vector3 c = transform.position + centerOffset;
            Vector3 p = gm.Player.transform.position;
            // Distance from the hazard centre to Maya's body (a vertical segment from her feet to her head).
            float y = Mathf.Clamp(c.y, p.y + 0.3f, p.y + 1.4f);
            if ((new Vector3(p.x, y, p.z) - c).sqrMagnitude < (radius + 0.3f) * (radius + 0.3f))
                gm.Damage(1, c);
        }
    }
}
