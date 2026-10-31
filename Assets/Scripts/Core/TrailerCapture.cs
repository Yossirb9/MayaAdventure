#if UNITY_EDITOR
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Captures the frame after every other script's LateUpdate.</summary>
    [DefaultExecutionOrder(32000)]
    public class TrailerCapture : MonoBehaviour
    {
        public TrailerDirector Director;

        void LateUpdate()
        {
            if (Director != null && !Director.Done) Director.Capture();
        }
    }
}
#endif
