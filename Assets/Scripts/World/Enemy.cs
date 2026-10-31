using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MayaAdventure
{
    public enum HitKind { Stomp, Punch, Pound, Metal }

    /// <summary>
    /// Shared enemy behaviour: registry, contact with Maya (stomp / hurt), reactions to punches,
    /// ground pounds and the metal power-up, and a squash-and-pop death that drops a coin.
    /// </summary>
    public abstract class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public bool Dead { get; protected set; }
        /// <summary>Horizontal radius and height of the body, measured from <see cref="Origin"/>.</summary>
        protected float bodyRadius = 0.55f, bodyHeight = 1.1f;
        protected bool stompable = true, punchable = true, poundable = true;
        protected int coinsOnDeath = 1;
        protected Transform body; // the part that squashes when defeated

        /// <summary>Point at the enemy's feet (override for flying enemies).</summary>
        public virtual Vector3 Origin => transform.position;
        public Vector3 Center => Origin + Vector3.up * (bodyHeight * 0.5f);
        public float Radius => bodyRadius;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>Returns true if the hit defeated (or affected) the enemy.</summary>
        public virtual bool Hit(HitKind kind, Vector3 from)
        {
            if (Dead) return false;
            bool allowed = kind == HitKind.Metal ||
                           (kind == HitKind.Stomp && stompable) ||
                           (kind == HitKind.Punch && punchable) ||
                           (kind == HitKind.Pound && poundable);
            if (!allowed) return false;
            Die();
            return true;
        }

        protected virtual void Die()
        {
            if (Dead) return;
            Dead = true;
            StartCoroutine(DieRoutine());
        }

        IEnumerator DieRoutine()
        {
            var gm = GameManager.I;
            if (gm != null) gm.Sfx.Play(gm.Sfx.Stomp);
            var t = body != null ? body : transform;
            var s0 = t.localScale;
            for (float time = 0f; time < 0.12f; time += Time.deltaTime)
            {
                float k = time / 0.12f;
                t.localScale = new Vector3(s0.x * (1f + k * 0.5f), s0.y * (1f - k * 0.8f), s0.z * (1f + k * 0.5f));
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            Debris.Burst(Center, new Color(0.7f, 0.45f, 0.85f), 12, 4f);
            if (gm != null)
                for (int i = 0; i < coinsOnDeath; i++)
                {
                    CoinPop.Spawn(Center + Vector3.up * 0.3f + Random.insideUnitSphere * 0.4f);
                    gm.CollectCoin(false, Center);
                }
            OnDefeated();
            Destroy(gameObject);
        }

        protected virtual void OnDefeated() { }

        /// <summary>
        /// Call from Update: stomping from above defeats stompable enemies (and bounces Maya),
        /// anything else hurts her. With the metal power-up she defeats whatever she touches.
        /// </summary>
        protected void CheckPlayerContact(bool harmful = true)
        {
            var gm = GameManager.I;
            if (Dead || gm == null || gm.Player == null || gm.State != GameState.Playing) return;
            var p = gm.Player;
            Vector3 rel = p.transform.position - Origin;
            if (new Vector2(rel.x, rel.z).magnitude > bodyRadius + 0.35f || rel.y < -1.6f || rel.y > bodyHeight + 0.15f) return;

            if (p.Power == PowerKind.Metal)
            {
                Hit(HitKind.Metal, p.transform.position);
                return;
            }
            if (p.Velocity.y < -0.5f && rel.y > bodyHeight * 0.45f)
            {
                if (Hit(HitKind.Stomp, p.transform.position)) p.Bounce(Controls.JumpHeld ? 14f : 10f);
                else
                {
                    p.Bounce(8f);
                    if (harmful) gm.Damage(1, Center); // spiky: stomping hurts
                }
                return;
            }
            if (harmful && !p.Invulnerable) gm.Damage(1, Center);
        }
    }
}
