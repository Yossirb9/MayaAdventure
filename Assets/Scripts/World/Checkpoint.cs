using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Flag that becomes the respawn point once touched.</summary>
    public class Checkpoint : MonoBehaviour
    {
        Renderer flag;
        Transform flagT;
        bool active, kenney;
        float yaw;

        public static Checkpoint Build(Transform parent, Vector3 position, float respawnYaw)
        {
            var go = new GameObject("Checkpoint");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var cp = go.AddComponent<Checkpoint>();
            cp.yaw = respawnYaw;
            if (Props.Has("flag"))
            {
                cp.flagT = Props.Spawn("flag", go.transform, Vector3.zero, 3.2f, 90f).transform;
                Props.Tint(cp.flagT.gameObject, new Color(0.55f, 0.55f, 0.6f));
                cp.kenney = true;
                return cp;
            }
            Art.Prim(PrimitiveType.Cylinder, "Pole", go.transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.12f, 1.5f, 0.12f),
                     Art.Solid(new Color(0.85f, 0.85f, 0.9f), 0.6f, 0.7f));
            Art.Prim(PrimitiveType.Sphere, "Knob", go.transform, new Vector3(0f, 3.05f, 0f), Vector3.one * 0.25f,
                     Art.Glow(new Color(1f, 0.85f, 0.2f), 0.6f), Art.Col.None);
            cp.flagT = Art.Prim(PrimitiveType.Cube, "Flag", go.transform, new Vector3(0.55f, 2.6f, 0f), new Vector3(1f, 0.6f, 0.05f),
                                Art.Solid(new Color(0.6f, 0.6f, 0.6f)), Art.Col.None).transform;
            cp.flag = cp.flagT.GetComponent<Renderer>();
            return cp;
        }

        void Update()
        {
            flagT.localRotation = Quaternion.Euler(0f, (kenney ? 90f : 0f) + Mathf.Sin(Time.time * 3f) * 12f, 0f);
            var gm = GameManager.I;
            if (active || gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            Vector3 d = gm.Player.transform.position - transform.position;
            if (d.sqrMagnitude < 2.5f * 2.5f)
            {
                active = true;
                if (kenney) Props.Tint(flagT.gameObject, Color.white);
                else flag.sharedMaterial = Art.Glow(new Color(0.3f, 0.9f, 0.4f), 0.5f, 0.3f, 0f);
                gm.SetCheckpoint(transform.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 1.5f, yaw);
            }
        }
    }
}
