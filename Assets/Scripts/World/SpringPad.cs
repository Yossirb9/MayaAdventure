using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Trampoline: Maya is launched when she lands on it (MayaController detects the contact).</summary>
    public class SpringPad : MonoBehaviour
    {
        public float Power = 21f;
        Transform top;
        float squash;
        bool kenney;

        public static SpringPad Build(Transform parent, Vector3 position, float power)
        {
            var go = new GameObject("SpringPad");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var pad = go.AddComponent<SpringPad>();
            pad.Power = power;
            if (Props.Has("spring"))
            {
                // Kenney spring: 0.75 x 0.47 -> about 1.9 m wide, 0.6 m tall. Collider stays put; only the model squashes.
                pad.top = new GameObject("Squash").transform;
                pad.top.SetParent(go.transform, false);
                Props.Spawn("spring", pad.top, Vector3.zero, new Vector3(2.5f, 1.3f, 2.5f));
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.3f, 0f);
                box.size = new Vector3(1.8f, 0.6f, 1.8f);
                pad.kenney = true;
                return pad;
            }
            Art.Prim(PrimitiveType.Cylinder, "Base", go.transform, new Vector3(0f, 0.12f, 0f), new Vector3(1.9f, 0.12f, 1.9f),
                     Art.Solid(new Color(0.35f, 0.35f, 0.4f), 0.5f, 0.6f), Art.Col.Mesh);
            pad.top = Art.Prim(PrimitiveType.Cylinder, "Top", go.transform, new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.1f, 1.6f),
                               Art.Solid(new Color(1f, 0.25f, 0.3f), 0.6f), Art.Col.Mesh).transform;
            return pad;
        }

        public void Trigger()
        {
            squash = 1f;
            var gm = GameManager.I;
            if (gm != null) gm.Sfx.Play(gm.Sfx.Spring);
        }

        void Update()
        {
            squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 4f);
            float wobble = Mathf.Sin(squash * 20f) * squash;
            if (kenney) top.localScale = new Vector3(1f + 0.15f * wobble, 1f - 0.35f * wobble, 1f + 0.15f * wobble);
            else top.localPosition = new Vector3(0f, 0.35f - 0.12f * wobble, 0f);
        }
    }
}
