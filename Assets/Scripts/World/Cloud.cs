using UnityEngine;

namespace MayaAdventure
{
    public class Cloud : MonoBehaviour
    {
        public float Speed = 1.5f;

        void Update()
        {
            var p = transform.localPosition;
            p.x += Speed * Time.deltaTime;
            if (p.x > 260f) p.x = -260f;
            transform.localPosition = p;
        }
    }
}
