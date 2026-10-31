using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Moves between two points and/or spins. Runs before Maya so she can ride it.</summary>
    [DefaultExecutionOrder(-100)]
    public class Mover : MonoBehaviour
    {
        public Vector3 From, To;
        public float Period = 4f, Spin, Phase;

        public static Mover Create(Transform parent, string name, Vector3 from, Vector3 to, float period, float spin = 0f, float phase = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = from;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var m = go.AddComponent<Mover>();
            m.From = from;
            m.To = to;
            m.Period = period;
            m.Spin = spin;
            m.Phase = phase;
            return m;
        }

        void Update()
        {
            if (From != To)
            {
                float k = 0.5f - 0.5f * Mathf.Cos((Time.time + Phase) * Mathf.PI * 2f / Period);
                transform.localPosition = Vector3.Lerp(From, To, k);
            }
            if (Spin != 0f) transform.Rotate(0f, Spin * Time.deltaTime, 0f, Space.World);
        }
    }
}
