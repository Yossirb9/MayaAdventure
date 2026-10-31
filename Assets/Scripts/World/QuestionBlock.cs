using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Question block: bump it from below for a coin.</summary>
    public class QuestionBlock : Punchable
    {
        bool used;
        Renderer rend;
        GameObject model;
        Vector3 basePos;
        float bumpTime = -1f;

        public static QuestionBlock Build(Transform parent, Vector3 center)
        {
            if (Props.Has("crate-item"))
            {
                // Collider on the root so the head-bump test finds this component.
                var root = new GameObject("QuestionBlock");
                root.transform.SetParent(parent, false);
                root.transform.localPosition = center;
                root.AddComponent<BoxCollider>().size = Vector3.one;
                var qb = root.AddComponent<QuestionBlock>();
                qb.model = Props.Spawn("crate-item", root.transform, new Vector3(0f, -0.5f, 0f), 2f);
                qb.basePos = center;
                return qb;
            }
            var go = Art.Prim(PrimitiveType.Cube, "QuestionBlock", parent, center, Vector3.one,
                              Art.Textured("question", Vector2.one, 0.3f));
            var block = go.AddComponent<QuestionBlock>();
            block.rend = go.GetComponent<Renderer>();
            block.basePos = center;
            return block;
        }

        public override void OnPunched(Vector3 from) => Hit();

        public void Hit()
        {
            if (used) return;
            used = true;
            if (model != null)
            {
                Destroy(model);
                model = Props.Spawn("crate", transform, new Vector3(0f, -0.5f, 0f), 2f);
            }
            else rend.sharedMaterial = Art.Textured("usedblock", Vector2.one, 0.1f);
            bumpTime = 0f;
            var gm = GameManager.I;
            if (gm == null) return;
            gm.Sfx.Play(gm.Sfx.Block);
            CoinPop.Spawn(transform.position + Vector3.up * 0.8f);
            gm.CollectCoin(false, transform.position);
        }

        void Update()
        {
            if (bumpTime < 0f) return;
            bumpTime += Time.deltaTime;
            float k = bumpTime / 0.2f;
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.3f);
            if (k >= 1f)
            {
                bumpTime = -1f;
                transform.localPosition = basePos;
            }
        }
    }
}
