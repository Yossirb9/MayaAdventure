using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Slowly carries a decorative cloud across the sky, wrapping round when it drifts too far.</summary>
    public class CloudDrift : MonoBehaviour
    {
        public float Speed = 1f;
        const float Range = 140f;

        void Update()
        {
            var p = transform.localPosition;
            p.x += Speed * Time.deltaTime;
            if (p.x > Range) p.x -= Range * 2f;
            transform.localPosition = p;
        }
    }
}
