using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Loads Maya.glb (mesh + Walk/Idle/Jump clips) from StreamingAssets with glTFast and drives
    /// its legacy animations plus procedural lean, squash and flips. Shows a capsule until loaded.
    /// </summary>
    public class MayaModel : MonoBehaviour
    {
        public const string FileName = "Maya.glb";
        /// <summary>Rendering layer only Maya is on, so the camera fill light touches nobody else.</summary>
        public const uint FillLayer = 1u << 1;
        const float PivotHeight = 0.85f;

        Transform squashRoot, pivot, visual;
        GameObject placeholder;
        Renderer[] renderers = Array.Empty<Renderer>();
        Animation anim;
        GltfImport gltf;
        string currentClip;
        float flipTime = -1f, flipDuration = 1f, squash, lean, crouchAmount;
        float punchT = -1f, punchLen = 0.25f;
        int punchKind;
        Transform wings, wingL, wingR;
        // Skeleton bones used to pose punches and kicks on top of the animation clips.
        Transform spine, upperSpine, armR, foreR, handR, armL, foreL, handL, thighR, shinR, footR;
        Transform[] posed = Array.Empty<Transform>();
        Quaternion[] posedBase = Array.Empty<Quaternion>();
        bool posing, sparked;
        PowerKind power;
        FlipAxis flipAxis;
        bool visible = true;

        void Awake()
        {
            squashRoot = new GameObject("Squash").transform;
            squashRoot.SetParent(transform, false);
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(squashRoot, false);
            pivot.localPosition = Vector3.up * PivotHeight;
            visual = new GameObject("Visual").transform;
            visual.SetParent(pivot, false);
            visual.localPosition = new Vector3(0f, -PivotHeight - 0.04f, 0f);

            placeholder = Art.Prim(PrimitiveType.Capsule, "Placeholder", pivot, Vector3.zero,
                                   new Vector3(0.6f, 0.85f, 0.6f), Art.Solid(new Color(1f, 0.55f, 0.75f)), Art.Col.None);
            renderers = placeholder.GetComponentsInChildren<Renderer>();
        }

        async void Start()
        {
            try
            {
                await Load();
            }
            catch (Exception e)
            {
                // Play mode stopped while the model was still loading: nothing to report.
                if (this == null || !Application.isPlaying) return;
                Debug.LogException(e);
            }
        }

        async Task Load()
        {
            var prefab = Resources.Load<GameObject>("Characters/Maya");
            if (prefab != null) Instantiate(prefab, visual, false);
            else if (!await LoadRuntime()) return;
            if (this == null) return;
            SetupModel();
        }

        /// <summary>Old path: parse StreamingAssets/Maya.glb at runtime with glTFast.</summary>
        async Task<bool> LoadRuntime()
        {
            byte[] data = await StreamingFile.ReadAllBytes(FileName);
            if (data == null)
            {
                Debug.LogWarning($"[Maya] Model not found: {StreamingFile.PathOf(FileName)}");
                return false;
            }
            if (this == null) return false;

            gltf = new GltfImport();
            var settings = new ImportSettings { AnimationMethod = AnimationMethod.Legacy };
            if (!await gltf.Load(data, StreamingFile.UriOf(FileName), settings))
            {
                Debug.LogError("[Maya] Could not parse Maya.glb");
                return false;
            }
            if (this == null) return false;
            if (!await gltf.InstantiateMainSceneAsync(visual))
            {
                Debug.LogError("[Maya] Could not instantiate Maya.glb");
                return false;
            }
            return this != null;
        }

        void SetupModel()
        {
            Destroy(placeholder);
            placeholder = null;

            anim = visual.GetComponentInChildren<Animation>(true);
            var clips = gltf != null ? gltf.GetAnimationClips() : null;
            if (anim == null && visual.childCount > 0) anim = visual.GetChild(0).gameObject.AddComponent<Animation>();
            if (anim != null)
            {
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                anim.playAutomatically = false;
                if (clips != null)
                {
                    foreach (var clip in clips)
                    {
                        if (clip == null || anim.GetClip(clip.name) != null) continue;
                        clip.legacy = true;
                        anim.AddClip(clip, clip.name);
                    }
                }
                foreach (var loop in new[] { "Idle", "Walk", "Run", "Sprint", "Swim", "SwimIdle", "Dance", "Crouch" })
                    SetWrap(loop, WrapMode.Loop);
                foreach (var once in new[] { "JumpStart", "JumpLand", "PunchJab", "PunchCross", "Hit", "Roll", "Death" })
                    SetWrap(once, WrapMode.ClampForever);
                // Maya's original model has a single held jump pose; the retargeted one loops a falling cycle.
                SetWrap("Jump", anim["JumpStart"] != null ? WrapMode.Loop : WrapMode.ClampForever);
                hasPunchClips = anim["PunchJab"] != null && anim["PunchCross"] != null;
                anim.Stop();
                currentClip = null;
            }

            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;
            FindBones();
            // Same layer as the player root, so the camera probe ignores her and the fill light finds her.
            foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.enabled = visible;
                r.renderingLayerMask = 1u | FillLayer;
            }
            ApplyCharacterShader();
            Outfits.Changed -= ApplyOutfits;
            Outfits.Changed += ApplyOutfits;
            ApplyOutfits();
            SetPower(power);
        }

        void FindBones()
        {
            Transform Bone(string n)
            {
                foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                    if (t.name == n || t.name.EndsWith(":" + n) || t.name.EndsWith("_" + n)) return t;
                return null;
            }
            spine = Bone("Spine1");
            upperSpine = Bone("Spine2");
            armR = Bone("RightArm"); foreR = Bone("RightForeArm"); handR = Bone("RightHand");
            armL = Bone("LeftArm"); foreL = Bone("LeftForeArm"); handL = Bone("LeftHand");
            thighR = Bone("RightUpLeg"); shinR = Bone("RightLeg"); footR = Bone("RightFoot");
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var t in new[] { spine, armR, foreR, armL, foreL, thighR, shinR })
                if (t != null) list.Add(t);
            posed = list.ToArray();
            posedBase = new Quaternion[posed.Length];
        }

        void Update()
        {
            // Undo last frame's punch pose before the clips run, in case a clip doesn't key one of these bones.
            if (!posing) return;
            for (int i = 0; i < posed.Length; i++) posed[i].localRotation = posedBase[i];
            posing = false;
        }

        void LateUpdate()
        {
            if (punchT < 0f || armR == null || foreR == null || handR == null) return;
            if (punchKind < 2 && hasPunchClips)
            {
                // The punch clips do the arms; only add the little spark at the fist.
                float kk = punchT / punchLen;
                if (!sparked && kk > 0.35f)
                {
                    sparked = true;
                    var fist = punchKind == 0 ? handR : handL;
                    if (fist != null) Debris.Burst(fist.position + pivot.forward * 0.15f, new Color(1f, 0.95f, 0.7f), 4, 1.6f);
                }
                return;
            }
            float k = Mathf.Clamp01(punchT / punchLen);
            // Snap out fast, hold, pull back.
            float w = k < 0.3f ? Mathf.SmoothStep(0f, 1f, k / 0.3f) : k < 0.6f ? 1f : Mathf.SmoothStep(1f, 0f, (k - 0.6f) / 0.4f);
            if (w <= 0.001f) return;
            for (int i = 0; i < posed.Length; i++) posedBase[i] = posed[i].localRotation;
            posing = true;

            var fwd = pivot.forward;
            var up = pivot.up;
            var side = pivot.right;
            if (punchKind == 2 && thighR != null && shinR != null && footR != null)
            {
                // Kick: the right leg swings up and straight, arms out for balance.
                var kickDir = (fwd * 0.9f + up * 0.35f).normalized;
                Aim(thighR, shinR, kickDir, w);
                Aim(shinR, footR, kickDir, w);
                if (foreL != null && handL != null) Aim(armL, foreL, (-side + up * 0.1f).normalized, w * 0.8f);
                Aim(armR, foreR, (side + up * 0.1f).normalized, w * 0.8f);
            }
            else
            {
                bool right = punchKind == 0;
                var arm = right ? armR : armL;
                var fore = right ? foreR : foreL;
                var hand = right ? handR : handL;
                if (arm == null || fore == null || hand == null) return;
                // Shoulders twist into the punch, the punching arm shoots straight forward, the other one guards.
                if (spine != null) spine.rotation = Quaternion.AngleAxis((right ? -28f : 28f) * w, up) * spine.rotation;
                var punchDir = (fwd + up * 0.08f + side * (right ? -0.12f : 0.12f)).normalized;
                Aim(arm, fore, punchDir, w);
                Aim(fore, hand, punchDir, w);
                var otherArm = right ? armL : armR;
                var otherFore = right ? foreL : foreR;
                var otherHand = right ? handL : handR;
                if (otherArm != null && otherFore != null && otherHand != null)
                {
                    Aim(otherArm, otherFore, (fwd * 0.35f - up * 0.6f + side * (right ? -0.3f : 0.3f)).normalized, w * 0.7f);
                    Aim(otherFore, otherHand, (fwd + up * 0.9f).normalized, w * 0.7f);
                }
                if (!sparked && k > 0.25f)
                {
                    sparked = true;
                    Debris.Burst(hand.position + fwd * 0.15f, new Color(1f, 0.95f, 0.7f), 4, 1.6f);
                }
            }
        }

        /// <summary>Rotates <paramref name="bone"/> so the direction to <paramref name="child"/> turns toward <paramref name="dir"/>.</summary>
        static void Aim(Transform bone, Transform child, Vector3 dir, float weight)
        {
            var cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-6f) return;
            var target = Quaternion.FromToRotation(cur.normalized, dir) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, weight);
        }

        void SetWrap(string clipName, WrapMode mode)
        {
            var state = anim[clipName];
            if (state != null) state.wrapMode = mode;
        }

        public enum FlipAxis { Forward, Backward, Side }

        /// <summary>
        /// Drives the clip choice plus procedural pose: <paramref name="pitch"/> tilts the body forward
        /// (dives, long jumps), <paramref name="crouch"/> squashes it (0..1).
        /// </summary>
        public void SetMotion(bool grounded, float horizontalSpeed, float verticalSpeed, float dt,
                              float pitch = 0f, float crouch = 0f)
        {
            airTime = grounded ? 0f : airTime + dt;
            UpdateFace(grounded, verticalSpeed);
            lastSpeed = horizontalSpeed + (grounded ? 0f : Mathf.Abs(verticalSpeed) * 0.4f);
            AnimateCape(dt);
            // Little dust puffs from the feet while running.
            if (grounded && !Swimming && horizontalSpeed > 6f)
            {
                dustTimer -= dt;
                if (dustTimer <= 0f)
                {
                    dustTimer = 0.18f;
                    Debris.Dust(transform.position - transform.forward * 0.2f, 1, 0.6f);
                }
            }
            landTime += dt;
            string want = PickClip(grounded, horizontalSpeed, verticalSpeed);
            if (Swimming && anim != null && anim["Swim"] != null) pitch *= 0.15f; // the swim clips already lie flat
            if (anim != null && anim[want] != null)
            {
                if (want != currentClip)
                {
                    var state = anim[want];
                    if (state.wrapMode == WrapMode.ClampForever) state.time = 0f;
                    if (want == "JumpStart") state.time = state.length * 0.3f; // skip the wind-up: Maya is already airborne
                    if (want == "Jump" && state.wrapMode == WrapMode.ClampForever) state.time = 0f;
                    anim.CrossFade(want, want == "JumpStart" || want == oneShot ? 0.06f : 0.15f);
                    currentClip = want;
                }
                var s = anim[want];
                switch (want)
                {
                    case "Walk": s.speed = Mathf.Clamp(horizontalSpeed / (anim["Run"] != null ? 1.5f : 1.6f), 0.7f, 3.2f); break;
                    case "Run": s.speed = Mathf.Clamp(horizontalSpeed / 4.5f, 0.8f, 1.6f); break;
                    case "Sprint": s.speed = Mathf.Clamp(horizontalSpeed / 7.5f, 0.8f, 1.5f); break;
                    case "Swim": s.speed = Mathf.Clamp(0.7f + horizontalSpeed * 0.12f, 0.7f, 1.4f); break;
                    case "JumpLand": s.speed = 1.8f; break;
                }
            }

            float targetLean = grounded ? Mathf.Clamp(horizontalSpeed * 1.2f, 0f, 10f) : 0f;
            lean = Mathf.Lerp(lean, targetLean + pitch, 1f - Mathf.Exp(-12f * dt));
            crouchAmount = Mathf.Lerp(crouchAmount, crouch, 1f - Mathf.Exp(-15f * dt));

            squash = Mathf.MoveTowards(squash, 0f, dt * 4f);
            float stretch = grounded ? 0f : Mathf.Clamp(verticalSpeed * 0.006f, -0.06f, 0.08f);
            float sy = 1f - squash * 0.22f - crouchAmount * 0.3f + stretch;
            float sxz = 1f / Mathf.Sqrt(Mathf.Max(0.5f, sy));
            squashRoot.localScale = new Vector3(sxz, sy, sxz);

            var flip = Quaternion.identity;
            if (flipTime >= 0f)
            {
                flipTime += dt;
                float k = flipTime / flipDuration;
                if (k >= 1f) flipTime = -1f;
                else
                {
                    float a = Mathf.SmoothStep(0f, 360f, k);
                    flip = flipAxis == FlipAxis.Forward ? Quaternion.Euler(a, 0f, 0f)
                         : flipAxis == FlipAxis.Backward ? Quaternion.Euler(-a, 0f, 0f)
                         : Quaternion.Euler(0f, 0f, a);
                }
            }
            var punch = Quaternion.identity;
            if (punchT >= 0f)
            {
                punchT += dt;
                float k = punchT / punchLen;
                if (k >= 1f) punchT = -1f;
                else
                {
                    float s = Mathf.Sin(k * Mathf.PI);
                    punch = punchKind == 0 ? Quaternion.Euler(8f * s, -10f * s, 0f)
                          : punchKind == 1 ? Quaternion.Euler(8f * s, 10f * s, 0f)
                          : Quaternion.Euler(-10f * s, 0f, 0f); // kick: lean back while the leg swings up
                }
            }
            pivot.localRotation = punch * flip * Quaternion.Euler(lean, 0f, 0f);

            if (wings != null && wings.gameObject.activeSelf)
            {
                // Folded and breathing on the ground, beating in the air, sweeping slowly under water.
                float rate = Swimming ? 4f : grounded ? 3f : 13f;
                float amp = Swimming ? 18f : grounded ? 8f : 34f;
                float flap = Mathf.Sin(Time.time * rate) * amp;
                float fold = grounded && !Swimming ? 30f : 5f;
                wingL.localRotation = Quaternion.Euler(0f, -fold - 15f + flap * 0.4f, 10f + flap);
                wingR.localRotation = Quaternion.Euler(0f, fold + 15f - flap * 0.4f, -10f - flap);
            }
        }

        /// <summary>Set by the controller every frame: Maya is swimming (uses the swim clips).</summary>
        public bool Swimming { get; set; }

        string oneShot;
        float oneShotUntil, airTime, landTime = 10f, dustTimer;
        bool hasPunchClips, hardLanding;

        string PickClip(bool grounded, float hSpeed, float vSpeed)
        {
            if (oneShot != null)
            {
                if (Time.time < oneShotUntil && anim != null && anim[oneShot] != null) return oneShot;
                oneShot = null;
            }
            if (Swimming) return Has("Swim") ? (hSpeed > 0.6f ? "Swim" : "SwimIdle") : (hSpeed > 0.4f ? "Walk" : "Idle");
            if (!grounded) return Has("JumpStart") && vSpeed > 1.5f && airTime < 0.45f ? "JumpStart" : "Jump";
            if (hardLanding && landTime < 0.2f && hSpeed < 3f && Has("JumpLand")) return "JumpLand";
            if (hSpeed < 0.4f) return "Idle";
            if (!Has("Run")) return "Walk";
            return hSpeed < 2.6f ? "Walk" : hSpeed < 7.2f ? "Run" : Has("Sprint") ? "Sprint" : "Run";
        }

        bool Has(string clip) => anim != null && anim[clip] != null;

        /// <summary>Plays a clip once over the motion clips (hurt, celebrate, punches).</summary>
        void PlayOnce(string clip, float seconds, float speed = 1f)
        {
            if (!Has(clip)) return;
            oneShot = clip;
            oneShotUntil = Time.time + seconds;
            var s = anim[clip];
            s.speed = speed;
            s.time = 0f;
            currentClip = null; // force the cross-fade even if the same clip played before
        }

        public void Hurt() => PlayOnce("Hit", 0.4f, 1.2f);

        public void Celebrate(float seconds) => PlayOnce("Dance", seconds);

        public void Flip(float duration, FlipAxis axis = FlipAxis.Forward)
        {
            flipTime = 0f;
            flipDuration = Mathf.Max(0.05f, duration);
            flipAxis = axis;
        }

        public void StopFlip() => flipTime = -1f;

        /// <summary>Procedural punch (0, 1) or spin kick (2).</summary>
        public void Punch(int kind, float length)
        {
            punchKind = kind;
            punchLen = Mathf.Max(0.05f, length);
            punchT = 0f;
            sparked = false;
            if (kind < 2 && hasPunchClips)
            {
                string clip = kind == 0 ? "PunchJab" : "PunchCross";
                // Squeeze the clip so its hit lands inside the punch window.
                PlayOnce(clip, length * 1.7f, anim[clip].length / (length * 1.9f));
            }
        }

        /// <summary>Wings on her back for the wing power, a polished metal look for the metal power.</summary>
        public void SetPower(PowerKind kind)
        {
            power = kind;
            if (kind == PowerKind.Wing && wings == null) BuildWings();
            if (wings != null)
            {
                AttachWings();
                wings.gameObject.SetActive(kind == PowerKind.Wing && visible);
            }

            var block = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null || (wings != null && r.transform.IsChildOf(wings))) continue;
                if (kind == PowerKind.Metal)
                {
                    r.GetPropertyBlock(block);
                    block.SetColor("baseColorFactor", new Color(0.82f, 0.85f, 0.92f, 1f));
                    block.SetFloat("metallicFactor", 1f);
                    block.SetFloat("roughnessFactor", 0.15f);
                    r.SetPropertyBlock(block);
                }
                else
                {
                    Brighten(r, block);
                }
            }
            faceDirty = true; // the property blocks were just rebuilt
        }

        /// <summary>Swaps the imported glTF materials for Maya/Character (soft portrait lighting), keeping texture and tint.</summary>
        void ApplyCharacterShader()
        {
            var shader = Shader.Find("Maya/Character");
            if (shader == null) return;
            foreach (var r in renderers)
            {
                if (r == null || (wings != null && r.transform.IsChildOf(wings))) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == shader) continue;
                    var c = new Material(shader) { name = m.name + " (Maya)" };
                    characterMats.Add(c);
                    if (m.HasProperty("baseColorTexture")) c.SetTexture("baseColorTexture", m.GetTexture("baseColorTexture"));
                    if (m.HasProperty("baseColorFactor")) c.SetColor("baseColorFactor", m.GetColor("baseColorFactor"));
                    var main = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
                    var faces = Resources.Load<Texture2D>("Characters/face_expressions");
                    if (main != null && main.name.Contains("texture_0") && faces != null)
                    {
                        // Where the expression atlas sits on her texture (generated with it by Tools~/make_faces.py).
                        c.SetTexture("_FaceTex", faces);
                        var rects = new Vector4[16];
                        int count = 0;
                        for (int k = 0; k + 1 < FaceAtlasData.Rects.Length && count < 8; k += 2)
                        {
                            if (FaceAtlasData.Rects[k] == Vector4.zero) continue;
                            rects[count * 2] = FaceAtlasData.Rects[k];
                            rects[count * 2 + 1] = FaceAtlasData.Rects[k + 1];
                            count++;
                        }
                        c.SetVectorArray("_FaceRects", rects);
                        c.SetInt("_FaceRectCount", count);
                        c.SetFloat("_FaceTiles", FaceAtlasData.Tiles);
                        if (!faceRenderers.Contains(r)) faceRenderers.Add(r);
                    }
                    if (m.HasProperty("normalTexture") && m.GetTexture("normalTexture") != null)
                    {
                        c.SetTexture("normalTexture", m.GetTexture("normalTexture"));
                        if (m.HasProperty("normalTexture_scale")) c.SetFloat("normalTexture_scale", m.GetFloat("normalTexture_scale"));
                    }
                    mats[i] = c;
                }
                r.sharedMaterials = mats;
            }
        }

        // ---------- wardrobe ----------

        readonly System.Collections.Generic.List<Material> characterMats = new System.Collections.Generic.List<Material>();
        GameObject crown, cape;
        Mesh capeMesh;
        Vector3[] capeBase, capeVerts;
        float lastSpeed, capeLift;
        const int CapeCols = 6, CapeRows = 10;

        void OnDestroy() => Outfits.Changed -= ApplyOutfits;

        /// <summary>Shows what Maya wears from her wardrobe: crown, cape, jacket colour.</summary>
        void ApplyOutfits()
        {
            if (this == null || visual == null) return;
            var tint = Outfits.JacketTint;
            foreach (var m in characterMats) if (m != null) m.SetColor("_JacketTint", tint);
            if (Outfits.Wears("crown") && crown == null) crown = BuildCrown();
            if (crown != null) crown.SetActive(Outfits.Wears("crown"));
            if (Outfits.Wears("cape") && cape == null) cape = BuildCape();
            if (cape != null) cape.SetActive(Outfits.Wears("cape"));
        }

        Transform HeadBone()
        {
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                if (t.name == "Head" || t.name.EndsWith(":Head") || t.name.EndsWith("_Head")) return t;
            return null;
        }

        GameObject BuildCrown()
        {
            var head = HeadBone();
            if (head == null) return null;
            // Sit it on top of her curls: the highest point of the model, above the head bone.
            float top = float.MinValue;
            foreach (var r in renderers) if (r != null && !(wings != null && r.transform.IsChildOf(wings))) top = Mathf.Max(top, r.bounds.max.y);
            var go = new GameObject("Crown");
            go.transform.position = new Vector3(head.position.x, top - 0.2f, head.position.z) - pivot.forward * 0.02f;
            go.transform.rotation = pivot.rotation * Quaternion.Euler(-8f, 0f, 0f);
            var gold = Art.Glow(new Color(1f, 0.8f, 0.2f), 0.45f, 0.9f, 1f);
            var gem = Art.Glow(new Color(1f, 0.3f, 0.55f), 0.8f, 0.9f, 0f);
            Art.MeshObject("Band", Art.LatheMesh("MayaCrown", new[]
            {
                new Vector2(0.085f, 0f), new Vector2(0.095f, 0f), new Vector2(0.1f, 0.03f), new Vector2(0.105f, 0.05f), new Vector2(0.09f, 0.05f), new Vector2(0.088f, 0.01f),
            }), go.transform, Vector3.zero, Vector3.one, gold);
            for (int i = 0; i < 5; i++)
            {
                var rot = Quaternion.Euler(0f, i * 72f, 0f);
                Art.MeshObject("Point", Art.ConeMesh(), go.transform, rot * new Vector3(0f, 0.04f, 0.1f), new Vector3(0.045f, 0.075f, 0.045f), gold);
                Art.Prim(PrimitiveType.Sphere, "Gem", go.transform, rot * new Vector3(0f, 0.025f, 0.104f), Vector3.one * 0.022f, gem, Art.Col.None);
            }
            go.transform.SetParent(head, true);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.renderingLayerMask = 1u | FillLayer;
            return go;
        }

        GameObject BuildCape()
        {
            var back = upperSpine != null ? upperSpine : spine;
            if (back == null) return null;
            var go = new GameObject("Cape");
            // Hangs from the shoulders, down her back.
            go.transform.position = back.position + pivot.up * 0.14f - pivot.forward * 0.13f;
            go.transform.rotation = pivot.rotation;
            capeMesh = new Mesh { name = "Cape" };
            capeMesh.MarkDynamic();
            capeBase = new Vector3[(CapeCols + 1) * (CapeRows + 1)];
            var uv = new Vector2[capeBase.Length];
            var tris = new System.Collections.Generic.List<int>();
            for (int r = 0; r <= CapeRows; r++)
                for (int c = 0; c <= CapeCols; c++)
                {
                    float u = c / (float)CapeCols, v = r / (float)CapeRows;
                    float width = Mathf.Lerp(0.3f, 0.52f, v); // narrow at the shoulders, wide at the hem
                    capeBase[r * (CapeCols + 1) + c] = new Vector3((u - 0.5f) * width, -v * 0.78f, -0.02f * Mathf.Sin(u * Mathf.PI));
                    uv[r * (CapeCols + 1) + c] = new Vector2(u, v);
                    if (r < CapeRows && c < CapeCols)
                    {
                        int a = r * (CapeCols + 1) + c, b = a + CapeCols + 1;
                        tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                    }
                }
            capeVerts = (Vector3[])capeBase.Clone();
            capeMesh.vertices = capeVerts;
            capeMesh.uv = uv;
            capeMesh.SetTriangles(tris, 0);
            capeMesh.RecalculateNormals();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(lit) { name = "Cape" };
            mat.SetColor("_BaseColor", new Color(0.85f, 0.12f, 0.25f));
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Cull", 0f); // both sides
            go.AddComponent<MeshFilter>().sharedMesh = capeMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.renderingLayerMask = 1u | FillLayer;
            go.transform.SetParent(back, true);
            return go;
        }

        /// <summary>The cape lifts behind her as she runs and flutters, curling more towards the hem.</summary>
        void AnimateCape(float dt)
        {
            if (cape == null || !cape.activeSelf || capeMesh == null) return;
            capeLift = Mathf.Lerp(capeLift, Mathf.Clamp01(lastSpeed / 9f), 1f - Mathf.Exp(-4f * dt));
            float t = Time.time;
            for (int r = 0; r <= CapeRows; r++)
            {
                float v = r / (float)CapeRows;
                float angle = (6f + capeLift * 38f) * v + Mathf.Sin(t * (5f + capeLift * 6f) - v * 4f) * (2f + capeLift * 7f) * v;
                var rot = Quaternion.Euler(angle, 0f, 0f); // swings back, away from her
                for (int c = 0; c <= CapeCols; c++)
                {
                    int i = r * (CapeCols + 1) + c;
                    var b = capeBase[i];
                    capeVerts[i] = rot * new Vector3(b.x, b.y, b.z) + new Vector3(0f, 0f, -Mathf.Sin(t * 7f + c) * 0.01f * capeLift * v);
                }
            }
            capeMesh.vertices = capeVerts;
            capeMesh.RecalculateNormals();
            capeMesh.RecalculateBounds();
        }

        // ---------- face ----------

        // Her own face, blinking every few seconds (the only expression: the others looked odd on this model).
        public enum Face { Normal, Blink }

        readonly System.Collections.Generic.List<Renderer> faceRenderers = new System.Collections.Generic.List<Renderer>();
        MaterialPropertyBlock faceBlock; // created on first use: Unity forbids it in field initialisers
        Face face, held;
        float heldUntil, nextBlink = 2f, blinkUntil;
        bool faceDirty = true;

        /// <summary>Holds a face for a while (used by the smoke test to look at the blink).</summary>
        public void Express(Face f, float seconds)
        {
            held = f;
            heldUntil = Time.time + seconds;
        }

        void UpdateFace(bool grounded, float verticalSpeed)
        {
            var f = Face.Normal;
            if (Time.time < heldUntil) f = held;
            else
            {
                if (Time.time > nextBlink)
                {
                    blinkUntil = Time.time + 0.13f;
                    nextBlink = Time.time + UnityEngine.Random.Range(2f, 5f);
                }
                if (Time.time < blinkUntil) f = Face.Blink;
            }
            if (f == face && !faceDirty) return;
            face = f;
            faceDirty = false;
            if (faceBlock == null) faceBlock = new MaterialPropertyBlock();
            foreach (var r in faceRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(faceBlock);
                faceBlock.SetFloat("_FaceIndex", (int)f);
                r.SetPropertyBlock(faceBlock);
            }
        }

        /// <summary>Lifts Maya's albedo a little so she reads clearly against the bright levels.</summary>
        static void Brighten(Renderer r, MaterialPropertyBlock block)
        {
            r.SetPropertyBlock(null);
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty("baseColorFactor")) return;
            var c = m.GetColor("baseColorFactor");
            block.Clear();
            block.SetColor("baseColorFactor", new Color(c.r * 1.04f, c.g * 1.06f, c.b * 1.1f, c.a)); // neutral: warm skin goes orange under ACES
            r.SetPropertyBlock(block);
        }

        // ---------- wings ----------

        /// <summary>Feathered angel wings: a row of long flight feathers fanned out under a layer of soft coverts.</summary>
        void BuildWings()
        {
            wings = new GameObject("Wings").transform;
            wings.SetParent(pivot, false);
            wings.localPosition = new Vector3(0f, 0.42f, -0.22f);
            var feather = Art.Glow(new Color(1f, 0.99f, 0.96f), 0.35f, 0.55f, 0f);
            var tip = Art.Glow(new Color(0.86f, 0.92f, 1f), 0.45f, 0.6f, 0f);
            var down = Art.Glow(new Color(1f, 0.96f, 0.9f), 0.3f, 0.4f, 0f);
            wingL = Wing("WingL", -1f, feather, tip, down);
            wingR = Wing("WingR", 1f, feather, tip, down);
        }

        Transform Wing(string name, float side, Material feather, Material tip, Material down)
        {
            var root = new GameObject(name).transform;
            root.SetParent(wings, false);
            root.localPosition = new Vector3(0.07f * side, 0f, 0f);
            // Arm of the wing: a gentle arc out and up from the shoulder blade.
            var arm = new GameObject("Arm").transform;
            arm.SetParent(root, false);
            arm.localRotation = Quaternion.Euler(0f, 0f, 32f * side); // rises up and out from the shoulder
            // Long flight feathers, fanned from almost horizontal to pointing down-back.
            const int primaries = 7;
            for (int i = 0; i < primaries; i++)
            {
                float t = i / (primaries - 1f);
                float along = 0.05f + t * 0.52f;                 // where it grows from along the arm
                float length = Mathf.Lerp(0.3f, 0.66f, t);        // longest at the wing tip
                float angle = Mathf.Lerp(12f, 78f, t);             // fan: inner ones hang down, outer ones reach out
                var f = new GameObject("Feather").transform;
                f.SetParent(arm, false);
                f.localPosition = new Vector3(along * side, 0.02f * t, -0.01f * i);
                f.localRotation = Quaternion.Euler(0f, 6f * side * t, angle * side);
                Art.Prim(PrimitiveType.Sphere, "Vane", f, new Vector3(0f, -length * 0.5f, 0f), new Vector3(0.14f, length, 0.025f),
                         t > 0.7f ? tip : feather, Art.Col.None, null, false);
            }
            // Soft coverts over the feather roots.
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                var c = new GameObject("Covert").transform;
                c.SetParent(arm, false);
                c.localPosition = new Vector3((0.05f + t * 0.42f) * side, -0.02f, 0.012f);
                c.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(20f, 60f, t) * side);
                Art.Prim(PrimitiveType.Sphere, "Down", c, new Vector3(0f, -0.09f, 0f), new Vector3(0.13f, 0.22f, 0.04f), down, Art.Col.None, null, false);
            }
            // Leading edge.
            Art.Prim(PrimitiveType.Capsule, "Edge", arm, new Vector3(0.3f * side, 0.02f, 0.01f), new Vector3(0.07f, 0.3f, 0.07f), down, Art.Col.None,
                     Quaternion.Euler(0f, 0f, 90f), false);
            return root;
        }

        /// <summary>Hang the wings on the upper-spine bone so they stay on her back in every pose (swimming included).</summary>
        void AttachWings()
        {
            var bone = upperSpine != null ? upperSpine : spine;
            if (wings == null || bone == null || wings.parent == bone) return;
            wings.SetParent(pivot, false);
            wings.localPosition = new Vector3(0f, 0.42f, -0.2f);
            wings.localRotation = Quaternion.identity;
            // Snap onto the back just behind the bone, keeping the world pose, then follow the bone from now on.
            var back = bone.position - pivot.forward * 0.16f + pivot.up * 0.06f;
            wings.SetPositionAndRotation(back, pivot.rotation);
            wings.SetParent(bone, true);
        }

        public void Land(float impact)
        {
            squash = Mathf.Max(squash, 0.3f + impact * 0.7f);
            hardLanding = impact > 0.3f;
            landTime = 0f;
            if (!Swimming) Debris.Dust(transform.position, 3 + Mathf.RoundToInt(impact * 6f), 0.7f + impact * 0.6f);
        }

        public void SetVisible(bool value)
        {
            if (value == visible) return;
            visible = value;
            foreach (var r in renderers)
                if (r != null) r.enabled = value;
            if (wings != null) wings.gameObject.SetActive(value && power == PowerKind.Wing);
        }
    }
}
