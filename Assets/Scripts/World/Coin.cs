using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Spinning coin, collected by touch. Red coins count toward the secret star.</summary>
    public class Coin : MonoBehaviour
    {
        public bool Red;
        public int Value = 1;

        /// <summary>Blue coin: worth five.</summary>
        public static Coin BuildBlue(Transform parent, Vector3 position)
        {
            var c = Build(parent, position);
            c.Value = 5;
            foreach (Transform t in c.spinner) Props.Tint(t.gameObject, new Color(0.4f, 0.75f, 1.6f));
            c.spinner.localScale = Vector3.one * 1.15f;
            return c;
        }
        Transform spinner;
        Vector3 home;
        string key;

        public static Coin Build(Transform parent, Vector3 position, bool red = false)
        {
            var go = new GameObject(red ? "RedCoin" : "Coin");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var coin = go.AddComponent<Coin>();
            coin.Red = red;
            coin.home = go.transform.position;
            coin.spinner = BuildVisual(go.transform, red);
            coin.spinner.localRotation = Quaternion.Euler(0f, position.x * 37f + position.z * 11f, 0f);
            return coin;
        }

        public static Transform BuildVisual(Transform parent, bool red)
        {
            var spin = new GameObject("Spin").transform;
            spin.SetParent(parent, false);
            if (Props.Has("coin-gold"))
            {
                // Kenney coin: 0.4 tall with its origin at the bottom; centre it on the pivot.
                var coin = Props.Spawn("coin-gold", spin, new Vector3(0f, -0.45f, 0f), 2.25f, 0f, Props.Col.None, false);
                Props.Tint(coin, red ? new Color(1.3f, 0.3f, 0.3f) : new Color(1.3f, 1.18f, 0.9f)); // a touch of glow
                return spin;
            }
            var face = red ? Art.Glow(new Color(0.95f, 0.15f, 0.15f), 0.6f) : Art.Glow(new Color(1f, 0.78f, 0.12f), 0.6f, 0.8f, 0.6f);
            var rim = red ? Art.Glow(new Color(0.7f, 0.05f, 0.05f), 0.4f) : Art.Glow(new Color(0.85f, 0.55f, 0.05f), 0.4f, 0.8f, 0.6f);
            var r = Quaternion.Euler(90f, 0f, 0f);
            Art.Prim(PrimitiveType.Cylinder, "Rim", spin, Vector3.zero, new Vector3(0.75f, 0.06f, 0.75f), rim, Art.Col.None, r, false);
            Art.Prim(PrimitiveType.Cylinder, "Face", spin, Vector3.zero, new Vector3(0.55f, 0.075f, 0.55f), face, Art.Col.None, r, false);
            return spin;
        }

        void Start()
        {
            // Already picked up earlier in this session: stays gone.
            key = GameManager.KeyFor(Red ? "red" : Value > 1 ? "blue" : "coin", home);
            if (GameManager.I != null && GameManager.I.Taken(key)) Destroy(gameObject);
        }

        void Update()
        {
            spinner.Rotate(0f, 200f * Time.deltaTime, 0f, Space.Self);
            var gm = GameManager.I;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            Vector3 d = gm.Player.transform.position + Vector3.up * 0.85f - transform.position;
            if (d.sqrMagnitude < 1.1f)
            {
                gm.Take(key);
                if (Value > 1) gm.CollectCoins(Value, transform.position);
                else gm.CollectCoin(Red, transform.position);
                Debris.Burst(transform.position, Red ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.85f, 0.3f), 8, 3f);
                Destroy(gameObject);
            }
        }
    }
}
