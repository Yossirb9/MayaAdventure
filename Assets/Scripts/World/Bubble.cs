using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Air bubble that wobbles up to the water surface and pops.</summary>
    public class Bubble : MonoBehaviour
    {
        float surface, phase, speed;

        public static void Spawn(Vector3 position, float waterSurface)
        {
            var go = Art.Prim(PrimitiveType.Sphere, "Bubble", null, position, Vector3.one * Random.Range(0.08f, 0.16f),
                              Art.Glow(new Color(0.8f, 0.95f, 1f), 0.4f, 0.95f, 0f), Art.Col.None, null, false);
            var b = go.AddComponent<Bubble>();
            b.surface = waterSurface;
            b.phase = Random.value * 10f;
            b.speed = Random.Range(1.2f, 1.8f);
        }

        void Update()
        {
            var p = transform.position;
            p.y += speed * Time.deltaTime;
            p.x += Mathf.Sin(Time.time * 6f + phase) * 0.004f;
            transform.position = p;
            if (p.y > surface) Destroy(gameObject);
        }
    }
}
