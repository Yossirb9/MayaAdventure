using UnityEngine;

namespace MayaAdventure
{
    /// <summary>A coin that pops out of a block or a defeated enemy (already credited).</summary>
    public class CoinPop : MonoBehaviour
    {
        float t;
        Vector3 start;
        Transform spin;

        public static void Spawn(Vector3 position)
        {
            var go = new GameObject("CoinPop");
            go.transform.position = position;
            var pop = go.AddComponent<CoinPop>();
            pop.start = position;
            pop.spin = Coin.BuildVisual(go.transform, false);
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / 0.55f;
            transform.position = start + Vector3.up * (Mathf.Sin(Mathf.Min(k, 1f) * Mathf.PI * 0.5f) * 1.6f);
            spin.Rotate(0f, 900f * Time.deltaTime, 0f, Space.Self);
            if (k >= 1f)
            {
                Debris.Burst(transform.position, new Color(1f, 0.85f, 0.3f), 8, 3f);
                Destroy(gameObject);
            }
        }
    }
}
