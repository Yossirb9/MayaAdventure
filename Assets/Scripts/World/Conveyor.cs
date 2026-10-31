using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Conveyor belt: carries Maya along its local +Z while she stands on it.</summary>
    public class Conveyor : MonoBehaviour
    {
        public float Speed = 3f;
        public Vector3 Velocity => transform.forward * Speed;

        public static Conveyor Build(Transform parent, Vector3 center, Vector3 size, float yaw, float speed)
        {
            var go = LevelBuilder.KBox("conveyor-belt", "Conveyor", center, size, LevelBuilder.plank, parent, yaw);
            var c = go.AddComponent<Conveyor>();
            c.Speed = speed;
            return c;
        }
    }
}
