using UnityEngine;

namespace MayaAdventure
{
    public enum PowerKind { None, Wing, Metal }

    /// <summary>
    /// Platformer movement for Maya: camera-relative running, triple jump, ground pound,
    /// coyote time, jump buffering, springs, conveyor belts, riding moving platforms and swimming,
    /// plus Mario-style extras: punch-punch-kick combo, wall jumps, the wing and metal power-ups,
    /// cannon launches and warp pipes.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MayaController : MonoBehaviour
    {
        [Header("Movement")]
        public float runSpeed = 8.5f;
        public float walkSpeed = 4f;
        public float groundAccel = 45f;
        public float groundDecel = 35f;
        public float airAccel = 16f;
        public float turnSpeed = 900f;

        [Header("Jumping")]
        public float gravity = 32f;
        public float maxFallSpeed = 32f;
        public float[] jumpSpeeds = { 10.5f, 12.8f, 16f };
        public float chainWindow = 0.28f;
        public float coyoteTime = 0.12f;
        public float jumpBuffer = 0.14f;
        public float poundSpeed = 30f;

        public bool InputEnabled { get; set; }
        public bool Grounded { get; private set; }
        public Vector3 Velocity => velocity;
        public bool Invulnerable => Time.time < invulnerableUntil;
        public bool Swimming { get; private set; }
        public bool HeadUnderwater => Swimming && transform.position.y + 1.45f < WaterY;

        [Header("Swimming")]
        public float swimSpeed = 4.2f;
        public float swimAccel = 9f;
        const float FloatDepth = 1.2f;   // feet this far under the surface when floating with the head out
        const float BoundsRadius = 170f;

        static float WaterY => GameManager.I != null ? GameManager.I.WaterY : float.NegativeInfinity;

        public PowerKind Power { get; private set; }
        public float PowerTimeLeft => Mathf.Max(0f, powerUntil - Time.time);
        public float PowerDuration { get; private set; } = 1f;
        public bool Hidden { get; private set; }
        public bool Punching => punchTimer > 0f;
        float powerUntil;
        int punchCombo;
        float punchTimer, punchLength, lastPunchEnd = -10f;
        readonly System.Collections.Generic.HashSet<object> punchHits = new System.Collections.Generic.HashSet<object>();
        Vector3 wallNormal;
        float wallTime = -10f, airLock;
        bool cannonFlight;
        Conveyor conveyor;
        float bubbleTimer;

        CharacterController cc;
        MayaModel model;
        Vector3 velocity;
        int jumpChain;
        float lastLandTime = -10f, lastGroundedTime = -10f, jumpPressedTime = -10f;
        bool jumpCutAvailable, jumpedSinceGrounded;
        int poundState; // 0 = none, 1 = hovering, 2 = dropping
        float poundTimer, stunTimer, invulnerableUntil;

        Transform ground, hitGround;
        Vector3 groundLocalPos;
        Quaternion groundLastRot;
        SpringPad hitSpring;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            model = GetComponent<MayaModel>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Hidden) return; // inside a cannon or a warp pipe
            Physics.SyncTransforms(); // platforms moved this frame
            FollowPlatform();
            if (Power != PowerKind.None && Time.time > powerUntil) EndPower();
            if (Grounded && conveyor != null) cc.Move(conveyor.Velocity * dt);

            Vector3 wish = ScriptedInput.Active ? Vector3.ClampMagnitude(ScriptedInput.WorldMove, 1f)
                         : CameraRelative(InputEnabled ? Controls.Move : Vector2.zero);

            // Water: below chest depth Maya swims (with the metal power-up she sinks and walks instead).
            float waterY = WaterY;
            bool metalUnderwater = Power == PowerKind.Metal && transform.position.y + 0.95f < waterY;
            if (Power == PowerKind.Metal && Swimming) Swimming = false;
            if (!Swimming && Power != PowerKind.Metal && transform.position.y + 0.95f < waterY && velocity.y < 1f) EnterWater();
            if (Swimming)
            {
                SwimUpdate(dt, wish, waterY);
                return;
            }

            bool walk = InputEnabled && Controls.WalkHeld;
            if (InputEnabled && Controls.JumpDown) jumpPressedTime = Time.time;
            // One button: on the ground it punches (on a warp pipe it enters the pipe), in the air it ground-pounds.
            bool punch = Controls.PunchDown || Controls.PoundDown && Grounded && !WarpPipe.PlayerOnPipe;
            if (InputEnabled && punch && punchTimer <= 0f && stunTimer <= 0f && poundState == 0) StartPunch(false);
            if (cannonFlight && Grounded)
            {
                // Plant the feet after a cannon shot instead of skidding off the target island.
                velocity.x = velocity.z = 0f;
                cannonFlight = false;
            }
            if (airLock > 0f && Grounded) airLock = 0f;
            if (airLock > 0f) airLock -= dt;

            // Horizontal motion
            var h = new Vector3(velocity.x, 0f, velocity.z);
            if (stunTimer > 0f)
            {
                stunTimer -= dt;
                h = Vector3.MoveTowards(h, Vector3.zero, 4f * dt);
            }
            else if (poundState != 0)
            {
                h = Vector3.zero;
            }
            else if (punchTimer > 0f)
            {
                punchTimer -= dt;
                if (punchTimer <= 0f) lastPunchEnd = Time.time;
                if (Grounded) h = Vector3.MoveTowards(h, Vector3.zero, 18f * dt); // lunge, then plant the feet
                DoPunchHits();
            }
            else if (airLock > 0f && !Grounded)
            {
                // just launched (cannon / wall jump): keep the arc
            }
            else
            {
                float top = (walk ? walkSpeed : runSpeed) * wish.magnitude * (metalUnderwater ? 0.6f : 1f);
                Vector3 target = wish.sqrMagnitude > 1e-4f ? wish.normalized * top : Vector3.zero;
                float rate = Grounded ? (target.sqrMagnitude > h.sqrMagnitude ? groundAccel : groundDecel) : airAccel;
                h = Vector3.MoveTowards(h, target, rate * dt);
                if (wish.sqrMagnitude > 1e-3f)
                {
                    var face = Quaternion.LookRotation(wish);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * (Grounded ? 1f : 0.45f) * dt);
                }
            }

            // Jumping
            bool canJump = (Grounded || Time.time - lastGroundedTime <= coyoteTime) && !jumpedSinceGrounded &&
                           poundState == 0 && stunTimer <= 0f;
            bool takeoff = canJump && Time.time - jumpPressedTime <= jumpBuffer;
            if (takeoff) Jump(h.magnitude);
            else if (!Grounded && poundState == 0 && Time.time - jumpPressedTime <= jumpBuffer && Time.time - wallTime < 0.25f)
            {
                h = WallJump();
            }

            // Releasing the button early cuts the jump short (never on the take-off frame itself,
            // so even a very quick tap still gives a small hop).
            if (!takeoff && jumpCutAvailable && velocity.y > 0f && !(InputEnabled && Controls.JumpHeld))
            {
                velocity.y *= 0.5f;
                jumpCutAvailable = false;
            }
            if (velocity.y <= 0f) jumpCutAvailable = false;

            // Ground pound
            if (InputEnabled && Controls.PoundDown && !Grounded && poundState == 0 && stunTimer <= 0f)
            {
                poundState = 1;
                poundTimer = 0.28f;
                h = Vector3.zero;
                jumpCutAvailable = false;
                model.Flip(0.28f);
            }

            // Vertical motion
            if (poundState == 1)
            {
                poundTimer -= dt;
                velocity.y = 0f;
                if (poundTimer <= 0f) poundState = 2;
            }
            if (poundState == 2)
            {
                velocity.y = -poundSpeed;
            }
            else if (poundState == 0 && !takeoff)
            {
                float g = metalUnderwater ? gravity * 0.45f : gravity;
                float maxFall = metalUnderwater ? 6f : maxFallSpeed;
                velocity.y = Mathf.Max(velocity.y - g * dt, -maxFall);
                // Wing power: hold jump while falling to glide.
                if (Power == PowerKind.Wing && !Grounded && velocity.y < -2.5f && InputEnabled && Controls.JumpHeld) velocity.y = -2.5f;
                if (Grounded && velocity.y < 0f) velocity.y = -3f; // stick to slopes and descending platforms
            }

            velocity.x = h.x;
            velocity.z = h.z;
            float fallSpeed = -velocity.y;

            hitGround = null;
            hitSpring = null;
            CollisionFlags flags = cc.Move(velocity * dt);
            KeepInBounds();
            bool wasGrounded = Grounded;
            Grounded = (flags & CollisionFlags.Below) != 0 || cc.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

            if (hitSpring != null)
            {
                Launch(hitSpring);
            }
            else if (Grounded)
            {
                if (!wasGrounded) OnLanded(fallSpeed);
                lastGroundedTime = Time.time;
                jumpedSinceGrounded = false;
                AttachGround(hitGround);
            }
            else
            {
                ground = null;
            }

            bool gliding = Power == PowerKind.Wing && !Grounded && velocity.y <= -2.4f && InputEnabled && Controls.JumpHeld;
            model.Swimming = false;
            model.SetMotion(Grounded, new Vector2(velocity.x, velocity.z).magnitude, velocity.y, dt, gliding ? 35f : 0f);
            model.SetVisible(!Invulnerable || Mathf.Repeat(Time.time * 10f, 1f) > 0.35f);
        }

        // ---------- punch combo ----------

        void StartPunch(bool swimming)
        {
            punchCombo = Time.time - lastPunchEnd < 0.4f ? (punchCombo + 1) % 3 : 0;
            punchLength = punchCombo == 2 ? 0.42f : 0.3f;
            punchTimer = punchLength;
            punchHits.Clear();
            var forward = transform.forward;
            if (swimming)
            {
                velocity += forward * 2.5f; // a short push through the water
            }
            else if (Grounded)
            {
                // A little lunge forward, like SM64's punches.
                float lunge = punchCombo == 2 ? 5.5f : 4f;
                var h = new Vector3(velocity.x, 0f, velocity.z);
                h = forward * Mathf.Max(Vector3.Dot(h, forward), lunge);
                velocity.x = h.x;
                velocity.z = h.z;
            }
            else if (velocity.y < 2f)
            {
                velocity.y = 2f; // jump kick: a tiny hang in the air
            }
            model.Punch(punchCombo, punchLength);
            var gm = GameManager.I;
            if (gm != null) gm.Sfx.Play(gm.Sfx.Swoosh, 0.7f);
        }

        void DoPunchHits()
        {
            float t = punchLength - punchTimer;
            if (t < 0.04f || t > punchLength * 0.8f) return;
            float reach = punchCombo == 2 ? 1.0f : 0.8f;
            float radius = punchCombo == 2 ? 1.05f : 0.85f;
            Vector3 c = transform.position + Vector3.up * 0.9f + transform.forward * reach;
            var gm = GameManager.I;
            foreach (var e in Enemy.All.ToArray())
            {
                if (e == null || e.Dead || punchHits.Contains(e)) continue;
                float r = radius + e.Radius;
                if ((e.Center - c).sqrMagnitude > r * r) continue;
                punchHits.Add(e);
                if (e.Hit(HitKind.Punch, transform.position))
                    Debris.Burst(e.Center, new Color(1f, 0.95f, 0.7f), 8, 4f);
                else if (gm != null)
                    gm.Sfx.Play(gm.Sfx.Block, 0.5f); // it shrugged the punch off
            }
            foreach (var target in Punchable.All.ToArray())
            {
                if (target == null || punchHits.Contains(target)) continue;
                float r = radius + target.Radius;
                if ((target.PunchPoint - c).sqrMagnitude > r * r) continue;
                punchHits.Add(target);
                target.OnPunched(transform.position);
            }
        }

        // ---------- wall jump ----------

        Vector3 WallJump()
        {
            var away = wallNormal * 7.5f;
            velocity = away + Vector3.up * (jumpSpeeds[1] * (Power == PowerKind.Wing ? 1.3f : 1f));
            transform.rotation = Quaternion.LookRotation(wallNormal);
            Grounded = false;
            jumpedSinceGrounded = true;
            jumpCutAvailable = false;
            jumpPressedTime = -10f;
            wallTime = -10f;
            airLock = 0.22f;
            model.Flip(0.45f);
            GameManager.I?.Sfx.Jump(1);
            Debris.Burst(transform.position + Vector3.up * 0.9f - wallNormal * 0.3f, new Color(0.9f, 0.9f, 0.9f), 6, 2f);
            return new Vector3(velocity.x, 0f, velocity.z);
        }

        // ---------- power-ups ----------

        public void GivePower(PowerKind kind, float seconds)
        {
            if (Power != PowerKind.None && Power != kind) EndPower();
            Power = kind;
            PowerDuration = seconds;
            powerUntil = Time.time + seconds;
            model.SetPower(kind);
            if (kind == PowerKind.Metal) Swimming = false;
        }

        void EndPower()
        {
            Power = PowerKind.None;
            model.SetPower(PowerKind.None);
            GameManager.I?.Hud.Show("הכוח נגמר", 1.2f);
        }

        // ---------- cannons and warp pipes ----------

        /// <summary>Hide Maya and freeze her (inside a cannon or while warping).</summary>
        public void SetHidden(bool hidden)
        {
            Hidden = hidden;
            velocity = Vector3.zero;
            ground = null;
            model.SetVisible(!hidden);
        }

        /// <summary>Fire Maya out of a cannon: no air steering until she lands.</summary>
        public void CannonLaunch(Vector3 from, Vector3 launchVelocity, float flightTime)
        {
            Teleport(from, Quaternion.LookRotation(new Vector3(launchVelocity.x, 0f, launchVelocity.z)).eulerAngles.y, false);
            Hidden = false;
            model.SetVisible(true);
            velocity = launchVelocity;
            Grounded = false;
            jumpedSinceGrounded = true;
            jumpCutAvailable = false;
            airLock = flightTime;
            cannonFlight = true;
            model.Flip(0.6f);
        }

        void Jump(float horizontalSpeed)
        {
            bool chained = Time.time - lastLandTime <= chainWindow && horizontalSpeed > 4f && jumpChain < 2;
            jumpChain = chained ? jumpChain + 1 : 0;
            velocity.y = jumpSpeeds[jumpChain] * (Power == PowerKind.Wing ? (jumpChain == 2 ? 1.6f : 1.25f) : 1f);
            if (Power == PowerKind.Metal && transform.position.y + 0.95f < WaterY) velocity.y *= 0.75f;
            Grounded = false;
            jumpedSinceGrounded = true;
            jumpCutAvailable = jumpChain < 2;
            jumpPressedTime = -10f;
            ground = null;
            if (jumpChain == 2)
            {
                model.Flip(0.75f);
                GameManager.I?.Sfx.Say(GameManager.I.Sfx.SayWow);
            }
            GameManager.I?.Sfx.Jump(jumpChain);
        }

        void Launch(SpringPad pad)
        {
            velocity.y = pad.Power * (poundState == 2 ? 1.2f : 1f);
            poundState = 0;
            Grounded = false;
            jumpedSinceGrounded = true;
            jumpCutAvailable = false;
            jumpChain = 0;
            ground = null;
            pad.Trigger();
            model.Flip(0.6f);
        }

        void OnLanded(float fallSpeed)
        {
            lastLandTime = Time.time;
            if (poundState == 2)
            {
                poundState = 0;
                stunTimer = 0.18f;
                model.Land(1f);
                GameManager.I?.PoundShock(transform.position);
            }
            else
            {
                model.Land(Mathf.Clamp01(fallSpeed / 25f));
            }
        }

        // ---------- swimming ----------

        void EnterWater()
        {
            Swimming = true;
            bool hard = velocity.y < -6f;
            velocity.y *= 0.25f;
            velocity.x *= 0.5f;
            velocity.z *= 0.5f;
            poundState = 0;
            stunTimer = 0f;
            ground = null;
            conveyor = null;
            model.StopFlip();
            var gm = GameManager.I;
            if (gm != null)
            {
                gm.Sfx.Play(gm.Sfx.Splash, hard ? 1f : 0.5f);
                Debris.Burst(new Vector3(transform.position.x, WaterY + 0.1f, transform.position.z), new Color(0.75f, 0.92f, 1f), hard ? 18 : 8, hard ? 6f : 3f);
            }
        }

        void SwimUpdate(float dt, Vector3 wish, float waterY)
        {
            // The pound button punches under water too (holding it keeps diving).
            if (InputEnabled && (Controls.PunchDown || Controls.PoundDown) && punchTimer <= 0f && stunTimer <= 0f) StartPunch(true);
            if (punchTimer > 0f)
            {
                punchTimer -= dt;
                if (punchTimer <= 0f) lastPunchEnd = Time.time;
                DoPunchHits();
            }

            // Horizontal: slower, floaty steering.
            var h = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 target = wish.sqrMagnitude > 1e-4f ? wish.normalized * swimSpeed * wish.magnitude : Vector3.zero;
            h = Vector3.MoveTowards(h, target, swimAccel * dt);
            if (wish.sqrMagnitude > 1e-3f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(wish), turnSpeed * 0.5f * dt);

            // Vertical: float up to the surface; A swims up / leaps out, Ctrl/E dives.
            float floatY = waterY - FloatDepth;
            bool atSurface = transform.position.y > floatY - 0.35f;
            bool jumpDown = InputEnabled && Controls.JumpDown;
            if (jumpDown && atSurface)
            {
                // Leap out of the water (enough to climb onto a low shore).
                velocity.y = 13.5f;
                Swimming = false;
                Grounded = false;
                jumpedSinceGrounded = true;
                jumpCutAvailable = false;
                velocity.x = h.x;
                velocity.z = h.z;
                GameManager.I?.Sfx.Jump(0);
                Debris.Burst(new Vector3(transform.position.x, waterY + 0.1f, transform.position.z), new Color(0.75f, 0.92f, 1f), 10, 4f);
                cc.Move(velocity * dt);
                return;
            }
            if (jumpDown) velocity.y = Mathf.Max(velocity.y, 5f);                 // stroke upward
            else if (InputEnabled && Controls.PoundHeld) velocity.y = Mathf.MoveTowards(velocity.y, -4.5f, 14f * dt);
            else if (InputEnabled && Controls.JumpHeld) velocity.y = Mathf.MoveTowards(velocity.y, 3.5f, 10f * dt);
            else velocity.y = Mathf.MoveTowards(velocity.y, Mathf.Clamp((floatY - transform.position.y) * 2.5f, -2f, 2.2f), 7f * dt);
            if (atSurface && velocity.y > 0f && transform.position.y > floatY) velocity.y = Mathf.Min(velocity.y, (floatY - transform.position.y + 0.05f) / dt);

            velocity.x = h.x;
            velocity.z = h.z;
            hitGround = null;
            hitSpring = null;
            CollisionFlags flags = cc.Move(velocity * dt);
            KeepInBounds();
            Grounded = (flags & CollisionFlags.Below) != 0;

            // Walked up a beach until the chest is out of the water: back to normal movement.
            if (Grounded && transform.position.y + 0.85f > waterY)
            {
                Swimming = false;
                lastGroundedTime = Time.time;
                jumpedSinceGrounded = false;
            }

            if (HeadUnderwater)
            {
                bubbleTimer -= dt;
                if (bubbleTimer <= 0f)
                {
                    bubbleTimer = 0.7f;
                    Bubble.Spawn(transform.position + Vector3.up * 1.5f + transform.forward * 0.2f, waterY);
                }
            }

            float speed = h.magnitude;
            float pitch = speed > 0.5f ? (atSurface ? 25f : 70f) : (atSurface ? 0f : 15f);
            if (velocity.y < -1.5f) pitch = 80f;
            model.Swimming = true;
            model.SetMotion(true, speed, 0f, dt, pitch);
            model.SetVisible(!Invulnerable || Mathf.Repeat(Time.time * 10f, 1f) > 0.35f);
        }

        void KeepInBounds()
        {
            var p = transform.position;
            float d = new Vector2(p.x, p.z).magnitude;
            if (d <= BoundsRadius) return;
            var back = -new Vector3(p.x, 0f, p.z) / d * (d - BoundsRadius);
            cc.Move(back);
            velocity.x = 0f;
            velocity.z = 0f;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) < 0.3f && !Grounded)
            {
                wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                wallTime = Time.time;
            }
            if (hit.normal.y > 0.6f)
            {
                hitGround = hit.collider.transform;
                conveyor = hit.collider.GetComponentInParent<Conveyor>();
                var pad = hit.collider.GetComponentInParent<SpringPad>();
                if (pad != null) hitSpring = pad;
            }
            else if (hit.normal.y < -0.6f && velocity.y > 0f)
            {
                var block = hit.collider.GetComponentInParent<Punchable>();
                if (block != null) block.OnPunched(transform.position);
            }
        }

        // ---------- moving platforms ----------

        void AttachGround(Transform t)
        {
            if (t != null && t.GetComponentInParent<Mover>() != null)
            {
                ground = t;
                groundLocalPos = t.InverseTransformPoint(transform.position);
                groundLastRot = t.rotation;
            }
            else
            {
                ground = null;
            }
        }

        void FollowPlatform()
        {
            if (ground == null) return;
            Vector3 delta = ground.TransformPoint(groundLocalPos) - transform.position;
            if (delta.sqrMagnitude > 1e-8f) cc.Move(delta);
            float yaw = (ground.rotation * Quaternion.Inverse(groundLastRot)).eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(0f, yaw)) > 0.001f)
                transform.rotation = Quaternion.Euler(0f, yaw, 0f) * transform.rotation;
            groundLastRot = ground.rotation;
        }

        // ---------- external ----------

        public void Knockback(Vector3 source)
        {
            if (Swimming)
            {
                invulnerableUntil = Time.time + 1.8f;
                return;
            }
            Vector3 away = transform.position - source;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-3f) away = -transform.forward;
            away.Normalize();
            velocity = away * 7f + Vector3.up * 7.5f;
            Grounded = false;
            jumpedSinceGrounded = true;
            jumpCutAvailable = false;
            poundState = 0;
            stunTimer = 0.55f;
            invulnerableUntil = Time.time + 1.8f;
            ground = null;
            model.Hurt();
        }

        /// <summary>Title screen: a random little show when the player taps Maya.</summary>
        public void TitleReact()
        {
            var gm = GameManager.I;
            switch (Random.Range(0, 4))
            {
                case 0: // jump with a back flip
                    Bounce(10.5f);
                    model.Flip(0.75f, MayaModel.FlipAxis.Backward);
                    gm?.Sfx.Jump(2);
                    break;
                case 1:
                    model.Celebrate(2.2f);
                    gm?.Sfx.Play(gm.Sfx.Star, 0.5f);
                    break;
                case 2: // spin kick on the spot
                    punchCombo = 1;
                    lastPunchEnd = Time.time;
                    StartPunch(false);
                    velocity.x = velocity.z = 0f;
                    break;
                default: // hop and twirl
                    Bounce(8f);
                    model.Flip(0.6f, MayaModel.FlipAxis.Side);
                    gm?.Sfx.Jump(1);
                    break;
            }
            Debris.Burst(transform.position + Vector3.up * 1.2f, new Color(1f, 0.85f, 0.4f), 14, 3f);
        }

        /// <summary>A little victory dance (title screen, trailer).</summary>
        public void Celebrate() => model.Celebrate(1.6f);

        /// <summary>A quick happy face without stopping her (after a star).</summary>
        public void SmileBriefly() => model.Express(MayaModel.Face.Happy, 1.2f);

        public void Bounce(float speed)
        {
            velocity.y = speed;
            Grounded = false;
            jumpedSinceGrounded = true;
            jumpCutAvailable = false;
            poundState = 0;
            ground = null;
        }

        /// <param name="protect">A second of invulnerability (with blinking) - for respawns after a fall or a lost life,
        /// not for placing her at a level start, on the title screen, out of a cannon or a pipe.</param>
        public void Teleport(Vector3 position, float yaw, bool protect = true)
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            cc.enabled = false;
            transform.SetPositionAndRotation(position + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = true;
            velocity = Vector3.zero;
            ground = null;
            conveyor = null;
            poundState = 0;
            stunTimer = 0f;
            Grounded = false;
            Swimming = false;
            airLock = 0f;
            punchTimer = 0f;
            invulnerableUntil = protect ? Time.time + 1f : 0f;
        }

        static Vector3 CameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 1e-4f) return Vector3.zero;
            var cam = Camera.main;
            Vector3 f = cam != null ? cam.transform.forward : Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize();
            var r = new Vector3(f.z, 0f, -f.x);
            return f * input.y + r * input.x;
        }
    }
}
