using System.Collections.Generic;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Anything Maya can punch that isn't an enemy (crates, boxes, blocks).</summary>
    public abstract class Punchable : MonoBehaviour
    {
        public static readonly List<Punchable> All = new List<Punchable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public virtual Vector3 PunchPoint => transform.position;
        public virtual float Radius => 0.6f;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public abstract void OnPunched(Vector3 from);
    }

    /// <summary>Something that reacts to a nearby ground pound (stakes, switches).</summary>
    public abstract class PoundTarget : MonoBehaviour
    {
        public static readonly List<PoundTarget> All = new List<PoundTarget>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public virtual float Radius => 1.6f;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public abstract void OnPound();
    }
}
