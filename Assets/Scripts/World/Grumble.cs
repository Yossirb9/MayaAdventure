using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Grumble: a Kenney character (purple blob placeholder until the model loads).
    /// Wanders around its home, chases Maya when she's close.
    /// Stomp, punch or ground-pound it to defeat it; touching it from the side hurts.
    /// </summary>
    public class Grumble : Enemy
    {
        protected float leash = 5f, speed = 1.6f, chaseSpeed = 3.4f, chaseRange = 7f;
        protected Vector3 home, target;
        protected Animation anim;
        bool chasing;
        static readonly string[] Models = { "character-oopi", "character-oozi", "character-oodi", "character-ooli" };

        public static Grumble Build(Transform parent, Vector3 position) => Build<Grumble>(parent, position, 1f, null);

        protected static T Build<T>(Transform parent, Vector3 position, float scale, string model) where T : Grumble
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var g = go.AddComponent<T>();
            g.home = position;
            g.target = position;
            g.bodyRadius *= scale;
            g.bodyHeight *= scale;

            g.body = new GameObject("Body").transform;
            g.body.SetParent(go.transform, false);
            g.body.localScale = Vector3.one * scale;
            var skin = Art.Solid(new Color(0.55f, 0.32f, 0.65f), 0.4f);
            var white = Art.Solid(Color.white, 0.6f);
            var black = Art.Solid(new Color(0.08f, 0.06f, 0.1f), 0.5f);
            var feet = Art.Solid(new Color(0.3f, 0.18f, 0.35f), 0.3f);
            Art.Prim(PrimitiveType.Sphere, "Blob", g.body, new Vector3(0f, 0.55f, 0f), new Vector3(1.1f, 1f, 1.1f), skin, Art.Col.None);
            for (int s = -1; s <= 1; s += 2)
            {
                Art.Prim(PrimitiveType.Sphere, "Eye", g.body, new Vector3(0.2f * s, 0.7f, 0.42f), new Vector3(0.26f, 0.32f, 0.18f), white, Art.Col.None);
                Art.Prim(PrimitiveType.Sphere, "Pupil", g.body, new Vector3(0.18f * s, 0.67f, 0.51f), Vector3.one * 0.12f, black, Art.Col.None);
                Art.Prim(PrimitiveType.Cube, "Brow", g.body, new Vector3(0.2f * s, 0.9f, 0.47f), new Vector3(0.3f, 0.07f, 0.07f), black,
                         Art.Col.None, Quaternion.Euler(0f, 0f, 20f * s));
                Art.Prim(PrimitiveType.Sphere, "Foot", g.body, new Vector3(0.25f * s, 0.1f, 0.1f), new Vector3(0.35f, 0.2f, 0.45f), feet, Art.Col.None);
            }
            g.LoadModel(model ?? Models[Mathf.Abs(Mathf.RoundToInt(position.x * 7f + position.z * 3f)) % Models.Length]);
            return g;
        }

        protected string walkClip = "walk";

        async void LoadModel(string model)
        {
            var holder = new GameObject("Model").transform;
            holder.SetParent(body, false);
            holder.localScale = Vector3.one * 1.35f;
            // Animated Quaternius enemies (horned blob / crab); the Kenney characters are the fallback.
            string kit = model == "character-oozi" || model == "character-ooli" ? "Platformer/Crab" : "Platformer/Enemy";
            var prefab = Nature.Load(kit);
            if (prefab != null)
            {
                holder.localScale = Vector3.one * 0.78f;
                var inst = Instantiate(prefab, holder, false);
                anim = inst.GetComponentInChildren<Animation>();
                if (anim == null) anim = inst.GetComponent<Animation>();
                walkClip = "Walk";
                if (anim != null)
                {
                    foreach (AnimationState s in anim) s.wrapMode = WrapMode.Loop;
                    if (anim["Death"] != null) anim["Death"].wrapMode = WrapMode.ClampForever;
                    if (anim["HitRecieve"] != null) anim["HitRecieve"].wrapMode = WrapMode.ClampForever;
                }
            }
            else anim = await RuntimeModels.Instantiate($"Kenney/{model}.glb", holder);
            if (this == null || holder == null || holder.childCount == 0) return;
            foreach (Transform child in body)
                if (child != holder) Destroy(child.gameObject);
            if (anim != null && anim[walkClip] != null) anim.Play(walkClip);
            OnModelLoaded(holder);
        }

        protected virtual void OnModelLoaded(Transform model) { }

        protected virtual void Update()
        {
            if (Dead) return;
            var gm = GameManager.I;
            float dt = Time.deltaTime;
            if (gm == null || gm.Player == null || dt <= 0f) return;

            Vector3 pp = gm.Player.transform.position;
            Vector3 pos = transform.localPosition;
            Vector3 toP = pp - pos;
            bool chase = gm.State == GameState.Playing && toP.y > -1.5f && toP.y < 2f + bodyHeight &&
                         new Vector2(toP.x, toP.z).sqrMagnitude < chaseRange * chaseRange &&
                         (pp - home).sqrMagnitude < (leash + chaseRange) * (leash + chaseRange);

            Vector3 dest = chase ? pp : target;
            dest.y = pos.y;
            Vector3 d = dest - pos;
            if (!chase && d.sqrMagnitude < 0.25f)
            {
                var c = Random.insideUnitCircle * Mathf.Min(3.5f, leash);
                target = home + new Vector3(c.x, 0f, c.y);
            }
            else if (d.sqrMagnitude > 1e-4f)
            {
                float step = Mathf.Min((chase ? chaseSpeed : speed) * dt, d.magnitude);
                pos += d.normalized * step;
                Vector3 off = pos - home;
                if (off.sqrMagnitude > leash * leash) pos = home + off.normalized * leash;
                transform.localPosition = pos;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * dt);
            }

            if (anim != null && chase != chasing && anim[walkClip] != null) anim[walkClip].speed = chase ? 1.8f : 1f;
            chasing = chase;
            if (anim == null)
            {
                float hop = Mathf.Abs(Mathf.Sin(Time.time * (chase ? 12f : 7f) + home.x));
                body.localPosition = Vector3.up * (hop * 0.08f);
            }

            CheckPlayerContact();
        }

        protected override void Die()
        {
            if (anim != null && anim["Death"] != null) anim.CrossFade("Death", 0.05f);
            else if (anim != null) anim.Stop();
            base.Die();
        }

        /// <summary>Kept for older callers.</summary>
        public void Squash() => Hit(HitKind.Pound, transform.position);
    }
}
