using System.Collections.Generic;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Reveals a hidden star once every enemy in a group has been defeated.</summary>
    public class RevealWhenCleared : MonoBehaviour
    {
        readonly List<Enemy> enemies = new List<Enemy>();
        PowerStar star;
        string message;
        int lastLeft = -1;

        public static void Watch(Transform parent, PowerStar star, string message, params Enemy[] group)
        {
            var go = new GameObject("StarOnClear");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RevealWhenCleared>();
            r.star = star;
            r.message = message;
            r.enemies.AddRange(group);
        }

        void Update()
        {
            int left = 0;
            foreach (var e in enemies)
                if (e != null && !e.Dead) left++;
            var gm = GameManager.I;
            if (gm == null) return;
            if (left != lastLeft && lastLeft > 0 && left > 0) gm.Hud.Show($"נשארו {left} אויבים", 1.5f);
            lastLeft = left;
            if (left > 0) return;
            if (star != null)
            {
                star.Reveal();
                gm.Sfx.Play(gm.Sfx.Reveal);
                gm.Hud.Show(message, 3.5f);
            }
            Destroy(gameObject);
        }
    }
}
