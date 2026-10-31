using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Scrolls the water texture and bobs the surface a little.</summary>
    public class WaterSurface : MonoBehaviour
    {
        Material mat;
        float baseY;

        void Start()
        {
            mat = GetComponent<Renderer>().sharedMaterial;
            baseY = transform.localPosition.y;
        }

        void Update()
        {
            float t = Time.time;
            if (mat != null) mat.mainTextureOffset = new Vector2(t * 0.004f, t * 0.0025f);
            var p = transform.localPosition;
            p.y = baseY + Mathf.Sin(t * 0.8f) * 0.06f;
            transform.localPosition = p;
        }
    }
}
