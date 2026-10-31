using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Glowing, spinning star: the main collectible.</summary>
    public class PowerStar : MonoBehaviour
    {
        public string Title;
        /// <summary>Stable id "level-index", used for saved progress.</summary>
        public string Id;
        public bool AlreadyCollected;
        static int levelIndex, counter;

        public static void BeginLevel(int index)
        {
            levelIndex = index;
            counter = 0;
        }
        Transform visual;
        Vector3 basePos;
        bool collected;

        public static PowerStar Build(Transform parent, Vector3 position, string title, bool hidden = false)
        {
            var go = new GameObject("PowerStar");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var star = go.AddComponent<PowerStar>();
            star.Title = title;
            star.Id = $"{levelIndex}-{counter++}";
            star.AlreadyCollected = GameManager.I != null && GameManager.I.HasStar(star.Id);
            star.basePos = position;
            star.visual = new GameObject("Spin").transform;
            star.visual.SetParent(go.transform, false);
            if (Props.Has("star"))
            {
                var model = Props.Spawn("star", star.visual, new Vector3(0f, -0.65f, 0f), 3.6f, 0f, Props.Col.None);
                // Already collected stars come back as a pale blue "ghost" (like SM64).
                Props.Tint(model, star.AlreadyCollected ? new Color(0.55f, 0.75f, 1.6f) : new Color(1.35f, 1.25f, 1.05f));
            }
            else
                Art.MeshObject("Star", Art.StarMesh(), star.visual, Vector3.zero, Vector3.one * 1.3f,
                               Art.Glow(new Color(1f, 0.85f, 0.15f), 1.4f, 0.8f, 0.5f));
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = star.AlreadyCollected ? new Color(0.5f, 0.7f, 1f) : new Color(1f, 0.9f, 0.4f);
            light.range = 7f;
            light.intensity = 2f;
            if (hidden) go.SetActive(false);
            return star;
        }

        public void Reveal()
        {
            gameObject.SetActive(true);
            Debris.Burst(transform.position, new Color(1f, 0.9f, 0.3f), 24, 6f);
        }

        void Update()
        {
            visual.Rotate(0f, 120f * Time.deltaTime, 0f, Space.Self);
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.2f);
            var gm = GameManager.I;
            if (collected || gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            Vector3 d = gm.Player.transform.position + Vector3.up * 0.85f - transform.position;
            if (d.sqrMagnitude < 1.5f * 1.5f)
            {
                collected = true;
                Debris.Burst(transform.position, new Color(1f, 0.9f, 0.3f), 30, 7f);
                gm.CollectStar(this);
                Destroy(gameObject);
            }
        }
    }
}
