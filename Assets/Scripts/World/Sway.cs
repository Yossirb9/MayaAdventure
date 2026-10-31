using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Gentle wind sway for trees and plants.</summary>
    public class Sway : MonoBehaviour
    {
        Quaternion baseRot;
        float phase, amount;

        void Start()
        {
            baseRot = transform.localRotation;
            phase = transform.position.x * 0.37f + transform.position.z * 0.21f;
            amount = Random.Range(1.2f, 2.2f);
        }

        void Update()
        {
            float t = Time.time * 1.3f + phase;
            transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t) * amount, 0f, Mathf.Sin(t * 0.7f + 1.3f) * amount * 0.6f);
        }
    }
}
