using UnityEngine;

namespace MayaAdventure
{
    /// <summary>Orbit camera with mouse look, zoom, soft follow and wall avoidance.</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;
        public bool InputEnabled;
        public float distance = 7.5f, minDistance = 3f, maxDistance = 13f;
        public float minPitch = -5f, maxPitch = 65f;

        float yaw, pitch = 18f, currentDistance, idleLook;
        Vector3 focus;
        const int Mask = ~(1 << 2); // everything but Ignore Raycast (Maya); also sees the tree-crown blockers on layer 1

        /// <summary>Title screen: a fixed, gently drifting shot (set by <see cref="TitleShot"/>).</summary>
        public bool TitleMode { get; private set; }
        Vector3 titlePos, titleLook;

        public void TitleShot(Vector3 position, Vector3 lookAt)
        {
            TitleMode = true;
            // Skip everything closer than Maya, so grass and flowers in front of the lens never hide her.
            GetComponent<Camera>().nearClipPlane = 1.5f;
            titlePos = position;
            titleLook = lookAt;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        }

        void LateUpdate()
        {
            if (TitleMode)
            {
                float t = Time.unscaledTime;
                var p = titlePos + new Vector3(Mathf.Sin(t * 0.31f) * 0.12f, Mathf.Sin(t * 0.43f) * 0.06f, Mathf.Cos(t * 0.27f) * 0.1f);
                transform.SetPositionAndRotation(p, Quaternion.LookRotation(titleLook - p));
                return;
            }
            if (Target == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (InputEnabled)
            {
                Vector2 look = Controls.Look;
                yaw += look.x;
                pitch -= look.y;
                distance = Mathf.Clamp(distance - Controls.Zoom * 1.2f, minDistance, maxDistance);
                idleLook = look.sqrMagnitude > 0.01f ? 0f : idleLook + dt;

                // Gently swing behind Maya while she runs, if the player isn't steering the camera.
                var maya = Target.GetComponent<MayaController>();
                if (idleLook > 1.2f && maya != null && maya.Grounded &&
                    new Vector2(maya.Velocity.x, maya.Velocity.z).sqrMagnitude > 4f)
                {
                    float behind = Target.eulerAngles.y;
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, behind)) < 100f)
                        yaw = Mathf.MoveTowardsAngle(yaw, behind, 35f * dt);
                }
            }
            else
            {
                yaw += 8f * dt; // slow showcase orbit on menus
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            Vector3 desired = Target.position + Vector3.up * 1.4f;
            float kxz = 1f - Mathf.Exp(-12f * dt), ky = 1f - Mathf.Exp(-5f * dt);
            focus = new Vector3(Mathf.Lerp(focus.x, desired.x, kxz), Mathf.Lerp(focus.y, desired.y, ky),
                                Mathf.Lerp(focus.z, desired.z, kxz));

            var rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 dir = rot * Vector3.back;
            float d = distance;
            if (Physics.SphereCast(focus, 0.3f, dir, out var hit, distance, Mask, QueryTriggerInteraction.Collide))
                d = Mathf.Max(0.8f, hit.distance - 0.1f);
            currentDistance = d < currentDistance ? d : Mathf.Lerp(currentDistance, d, 1f - Mathf.Exp(-4f * dt));

            var camPos = focus + dir * currentDistance;
            // Never dip into the ground.
            if (Physics.Raycast(camPos + Vector3.up * 3f, Vector3.down, out var floor, 3.6f, Mask, QueryTriggerInteraction.Ignore))
                camPos.y = Mathf.Max(camPos.y, floor.point.y + 0.6f);
            transform.position = camPos;
            transform.rotation = Quaternion.LookRotation(focus - camPos, Vector3.up);
        }

        public void Snap()
        {
            if (TitleMode) GetComponent<Camera>().nearClipPlane = 0.1f;
            TitleMode = false;
            if (Target == null) return;
            focus = Target.position + Vector3.up * 1.4f;
            yaw = Target.eulerAngles.y;
            pitch = 18f;
            currentDistance = distance;
            transform.position = focus + Quaternion.Euler(pitch, yaw, 0f) * Vector3.back * currentDistance;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
