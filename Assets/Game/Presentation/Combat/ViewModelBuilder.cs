using System.Collections.Generic;
using Adventure3D.Gameplay;
using UnityEngine;

namespace Adventure3D.Presentation
{
    /// <summary>Parts of a built first-person weapon the animation needs.</summary>
    public sealed class ViewModelRig
    {
        public GameObject Root;
        public Transform Muzzle;
        public Transform Magazine;
        public Vector3 MagazineRest;
        public Transform LeftHand;
        public Vector3 LeftHandRest;
        public Transform Bolt;
        public Vector3 BoltRest;
        /// <summary>Hip-fire position of the weapon in camera space.</summary>
        public Vector3 HipPosition;
        /// <summary>Aim-down-sights position (sight on the screen centre).</summary>
        public Vector3 AimPosition;
        public WeaponClass Class;
        public Animation Anim;
        public bool IsGlb;
    }

    /// <summary>
    /// Builds first-person arms (tactical sleeves with an orange stripe, gloves) and a weapon from
    /// Unity primitives, matching the operative reference. This is the placeholder for a real rigged
    /// FBX: put a prefab at Resources/Adventure3D/ViewModels/&lt;weaponId&gt; with child transforms named
    /// "Muzzle", "Magazine" and "LeftHand", and it is used instead of the generated model.
    /// </summary>
    public static class ViewModelBuilder
    {
        public const int ViewModelLayer = 31;
        /// <summary>Overall size of the generated first-person model.</summary>
        const float Scale = 0.85f;

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        static Material Mat(string key, Color color, float metallic, float smoothness)
        {
            if (Mats.TryGetValue(key, out var m) && m != null) return m;
            var shader = Shader.Find("Standard");
            m = new Material(shader) { name = "VM_" + key, color = color };
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smoothness);
            Mats[key] = m;
            return m;
        }

        static Material Gunmetal => Mat("gunmetal", new Color(0.11f, 0.115f, 0.12f), 0.75f, 0.5f);
        static Material Blackened => Mat("black", new Color(0.06f, 0.06f, 0.065f), 0.4f, 0.35f);
        static Material Polymer => Mat("polymer", new Color(0.14f, 0.135f, 0.12f), 0f, 0.25f);
        static Material Wood => Mat("wood", new Color(0.36f, 0.19f, 0.09f), 0f, 0.45f);
        static Material Tan => Mat("tan", new Color(0.42f, 0.36f, 0.26f), 0f, 0.2f);
        static Material Sleeve => Mat("sleeve", new Color(0.17f, 0.165f, 0.165f), 0f, 0.12f);
        static Material Stripe => Mat("stripe", new Color(0.9f, 0.42f, 0.08f), 0f, 0.3f);
        static Material Glove => Mat("glove", new Color(0.1f, 0.1f, 0.09f), 0f, 0.3f);
        static Material GlovePad => Mat("glovepad", new Color(0.24f, 0.22f, 0.17f), 0f, 0.2f);
        static Material Steel => Mat("steel", new Color(0.62f, 0.64f, 0.66f), 0.9f, 0.75f);
        static Material Lens => Mat("lens", new Color(0.05f, 0.12f, 0.16f), 0.2f, 0.95f);

        public static string GlbId(string weaponId) => weaponId switch
        {
            "usp45" => "usp",
            "elites" => "elite",
            "smoke" => "smokegrenade",
            _ => weaponId,
        };

        public static ViewModelRig Build(WeaponDefinition w, Transform parent)
        {
            string glbId = GlbId(w.Id);
            var custom = Resources.Load<GameObject>("Adventure3D/Weapons/ViewModels/v_" + glbId)
                      ?? Resources.Load<GameObject>("Adventure3D/ViewModels/" + w.Id);
            ViewModelRig rig;
            if (custom != null) rig = FromPrefab(custom, parent, w);
            else
            {
                rig = new ViewModelRig { Class = w.Class };
                rig.Root = new GameObject("ViewModel_" + w.Id);
                rig.Root.transform.SetParent(parent, false);
                switch (w.Class)
                {
                    case WeaponClass.Knife: BuildKnife(rig); break;
                    case WeaponClass.Pistol: BuildPistol(rig, w.Id); break;
                    default: BuildLongGun(rig, w); break;
                }
            }
            SetLayer(rig.Root.transform, ViewModelLayer);
            foreach (var r in rig.Root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            if (rig.Magazine != null) rig.MagazineRest = rig.Magazine.localPosition;
            if (rig.LeftHand != null) rig.LeftHandRest = rig.LeftHand.localPosition;
            if (rig.Bolt != null) rig.BoltRest = rig.Bolt.localPosition;
            return rig;
        }

        static ViewModelRig FromPrefab(GameObject prefab, Transform parent, WeaponDefinition w)
        {
            var go = Object.Instantiate(prefab, parent, false);
            go.name = "ViewModel_" + w.Id;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);

            var rig = new ViewModelRig
            {
                Root = go,
                Class = w.Class,
                IsGlb = true,
                Anim = go.GetComponentInChildren<Animation>(true),
            };

            rig.Muzzle = Find(go.transform, "Muzzle");
            if (rig.Muzzle == null)
            {
                Bounds b = default;
                bool has = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                }
                var muzzleGo = new GameObject("Muzzle");
                muzzleGo.transform.SetParent(go.transform, false);
                float mz = has ? Mathf.Clamp(go.transform.InverseTransformPoint(b.max).z, 0.35f, 1.0f) : 0.7f;
                muzzleGo.transform.localPosition = new Vector3(0.03f, 0.03f, mz);
                rig.Muzzle = muzzleGo.transform;
            }

            rig.Magazine = Find(go.transform, "Magazine");
            rig.LeftHand = Find(go.transform, "LeftHand");
            rig.HipPosition = new Vector3(0f, -0.02f, -0.02f);
            rig.AimPosition = w.Class == WeaponClass.Pistol
                ? new Vector3(-0.06f, 0.02f, 0.04f)
                : new Vector3(-0.07f, 0.015f, 0.04f);

            if (rig.Anim != null)
            {
                var draw = rig.Anim.GetClip("draw");
                if (draw != null)
                {
                    draw.wrapMode = WrapMode.ClampForever;
                    rig.Anim.Play("draw");
                }
                else
                {
                    var idle = rig.Anim.GetClip("idle1");
                    if (idle != null)
                    {
                        idle.wrapMode = WrapMode.Loop;
                        rig.Anim.Play("idle1");
                    }
                }
            }
            return rig;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }

        // ------------------------------------------------------------------ weapons

        static void BuildLongGun(ViewModelRig rig, WeaponDefinition w)
        {
            var g = rig.Root.transform;
            bool ak = w.Id == "ak47";
            bool sniper = w.Class == WeaponClass.Sniper;
            bool shotgun = w.Class == WeaponClass.Shotgun;
            bool smg = w.Class == WeaponClass.SMG;
            bool heavy = w.Class == WeaponClass.Heavy;
            float len = smg ? 0.72f : sniper ? 1.25f : shotgun ? 1.05f : heavy ? 1.1f : 0.95f;
            var furniture = ak ? Wood : sniper ? Tan : Polymer;

            float recvLen = smg ? 0.22f : heavy ? 0.36f : 0.32f;
            Box(g, "Receiver", new Vector3(0f, 0f, 0.05f), new Vector3(heavy ? 0.07f : 0.05f, heavy ? 0.09f : 0.075f, recvLen), Gunmetal);
            Box(g, "TopCover", new Vector3(0f, 0.042f, 0.06f), new Vector3(0.044f, 0.012f, recvLen * 0.85f), Blackened);
            rig.Bolt = Box(g, "ChargingHandle", new Vector3(0.033f, 0.02f, 0.08f), new Vector3(0.018f, 0.012f, 0.03f), Steel).transform;

            float handZ = 0.05f + recvLen * 0.5f + 0.1f;
            Box(g, "Handguard", new Vector3(0f, -0.004f, handZ), new Vector3(0.058f, 0.062f, 0.2f), furniture);
            if (!ak) for (int i = 0; i < 5; i++) Box(g, "Rail", new Vector3(0f, 0.034f, handZ - 0.08f + i * 0.04f), new Vector3(0.022f, 0.006f, 0.02f), Blackened);

            float barrelLen = len * 0.42f;
            Cyl(g, "Barrel", new Vector3(0f, 0.005f, handZ + 0.1f + barrelLen * 0.5f - 0.05f), 0.011f, barrelLen, Gunmetal);
            float muzzleZ = handZ + 0.1f + barrelLen - 0.05f;
            Cyl(g, "MuzzleDevice", new Vector3(0f, 0.005f, muzzleZ + 0.025f), shotgun ? 0.017f : 0.015f, 0.05f, Blackened);
            if (ak) Cyl(g, "GasTube", new Vector3(0f, 0.03f, handZ), 0.012f, 0.2f, Gunmetal);
            if (shotgun) Cyl(g, "Tube", new Vector3(0f, -0.022f, handZ + 0.12f), 0.013f, 0.36f, Gunmetal);
            rig.Muzzle = new GameObject("Muzzle").transform;
            rig.Muzzle.SetParent(g, false);
            rig.Muzzle.localPosition = new Vector3(0f, 0.005f, muzzleZ + 0.06f);

            // Sights
            if (sniper)
            {
                Cyl(g, "Scope", new Vector3(0f, 0.078f, 0.07f), 0.022f, 0.3f, Blackened);
                Cyl(g, "ScopeBell", new Vector3(0f, 0.078f, 0.24f), 0.03f, 0.06f, Blackened);
                Cyl(g, "ScopeLens", new Vector3(0f, 0.078f, 0.271f), 0.026f, 0.004f, Lens);
                Box(g, "ScopeMount", new Vector3(0f, 0.055f, 0.07f), new Vector3(0.02f, 0.025f, 0.12f), Gunmetal);
            }
            else if (ak)
            {
                Box(g, "FrontPost", new Vector3(0f, 0.045f, muzzleZ - 0.06f), new Vector3(0.008f, 0.035f, 0.01f), Gunmetal);
                Box(g, "RearSight", new Vector3(0f, 0.052f, 0.15f), new Vector3(0.03f, 0.012f, 0.04f), Gunmetal);
            }
            else
            {
                // Hollow reflex sight: a frame you look through, with a red dot.
                Box(g, "OpticBase", new Vector3(0f, 0.052f, 0.07f), new Vector3(0.034f, 0.008f, 0.06f), Blackened);
                Box(g, "OpticTop", new Vector3(0f, 0.094f, 0.08f), new Vector3(0.036f, 0.005f, 0.04f), Blackened);
                Box(g, "OpticLeft", new Vector3(-0.017f, 0.073f, 0.08f), new Vector3(0.004f, 0.046f, 0.04f), Blackened);
                Box(g, "OpticRight", new Vector3(0.017f, 0.073f, 0.08f), new Vector3(0.004f, 0.046f, 0.04f), Blackened);
                Box(g, "RedDot", new Vector3(0f, 0.074f, 0.1f), new Vector3(0.0025f, 0.0025f, 0.001f), Mat("reddot", new Color(1f, 0.05f, 0.05f), 0f, 1f));
            }

            // Magazine, grip, stock
            if (!shotgun)
            {
                var mag = new GameObject("Magazine").transform;
                mag.SetParent(g, false);
                mag.localPosition = new Vector3(0f, -0.06f, 0.14f);
                if (heavy) Box(mag, "Box", new Vector3(0.0f, -0.05f, 0f), new Vector3(0.09f, 0.1f, 0.12f), Polymer);
                else
                {
                    float magLen = smg ? 0.14f : 0.16f;
                    var a = Box(mag, "MagTop", new Vector3(0f, -0.04f, 0f), new Vector3(0.03f, 0.09f, 0.06f), ak ? Wood : Blackened);
                    var b = Box(mag, "MagBottom", new Vector3(0f, -0.04f - magLen * 0.55f, ak ? 0.03f : 0.012f), new Vector3(0.03f, magLen * 0.6f, 0.058f), ak ? Wood : Blackened);
                    if (ak) { a.transform.localRotation = Quaternion.Euler(12f, 0f, 0f); b.transform.localRotation = Quaternion.Euler(24f, 0f, 0f); }
                }
                rig.Magazine = mag;
            }
            else
            {
                var pump = new GameObject("Magazine").transform; // the pump plays the reload motion
                pump.SetParent(g, false);
                pump.localPosition = new Vector3(0f, -0.025f, handZ + 0.06f);
                Cyl(pump, "Pump", Vector3.zero, 0.024f, 0.14f, Polymer);
                rig.Magazine = pump;
            }
            var grip = Box(g, "Grip", new Vector3(0f, -0.075f, -0.04f), new Vector3(0.034f, 0.1f, 0.045f), Polymer);
            grip.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            Box(g, "TriggerGuard", new Vector3(0f, -0.05f, 0.01f), new Vector3(0.012f, 0.008f, 0.06f), Gunmetal);
            if (!smg)
            {
                Box(g, "Stock", new Vector3(0f, -0.02f, -0.24f), new Vector3(0.042f, 0.075f, 0.26f), furniture);
                Box(g, "ButtPad", new Vector3(0f, -0.02f, -0.37f), new Vector3(0.045f, 0.09f, 0.02f), Blackened);
            }
            else Box(g, "FoldedStock", new Vector3(0.03f, -0.01f, -0.12f), new Vector3(0.01f, 0.04f, 0.18f), Steel);
            if (heavy) Cyl(g, "CarryHandle", new Vector3(0f, 0.07f, 0.2f), 0.008f, 0.12f, Steel);

            // Arms: right hand on the grip, left hand under the handguard.
            RightArm(g, new Vector3(0.006f, -0.085f, -0.045f));
            rig.LeftHand = SupportArm(g, new Vector3(-0.036f, -0.012f, handZ + (shotgun ? 0.06f : 0.03f)));

            g.localScale = Vector3.one * Scale;
            rig.HipPosition = new Vector3(0.13f, -0.125f, 0.4f);
            float sightHeight = (sniper ? 0.078f : ak ? 0.05f : 0.074f) * Scale;
            rig.AimPosition = new Vector3(0f, -sightHeight, sniper ? 0.12f : 0.24f);
        }

        static void BuildPistol(ViewModelRig rig, string id)
        {
            var g = rig.Root.transform;
            bool deagle = id == "deagle";
            float s = deagle ? 1.2f : 1f;
            Box(g, "Slide", new Vector3(0f, 0.012f, 0.05f), new Vector3(0.03f, 0.033f, 0.19f) * s, id == "glock18" ? Blackened : Gunmetal);
            Box(g, "Frame", new Vector3(0f, -0.014f, 0.04f), new Vector3(0.028f, 0.02f, 0.15f) * s, Polymer);
            var grip = Box(g, "Grip", new Vector3(0f, -0.065f, -0.02f), new Vector3(0.03f, 0.095f, 0.048f) * s, Polymer);
            grip.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            Box(g, "FrontSight", new Vector3(0f, 0.033f * s, 0.13f * s), new Vector3(0.005f, 0.008f, 0.006f), Blackened);
            Box(g, "RearSight", new Vector3(0f, 0.033f * s, -0.035f), new Vector3(0.022f, 0.008f, 0.008f), Blackened);
            rig.Bolt = g.Find("Slide");
            rig.Muzzle = new GameObject("Muzzle").transform;
            rig.Muzzle.SetParent(g, false);
            rig.Muzzle.localPosition = new Vector3(0f, 0.012f, 0.16f * s);
            var mag = new GameObject("Magazine").transform;
            mag.SetParent(g, false);
            mag.localPosition = new Vector3(0f, -0.1f, -0.03f);
            Box(mag, "MagBase", Vector3.zero, new Vector3(0.026f, 0.012f, 0.042f), Blackened);
            rig.Magazine = mag;

            RightArm(g, new Vector3(0.004f, -0.07f, -0.025f));
            rig.LeftHand = LeftArm(g, new Vector3(-0.022f, -0.075f, -0.01f), pistolGrip: true);
            g.localScale = Vector3.one * Scale;
            rig.HipPosition = new Vector3(0.1f, -0.14f, 0.32f);
            rig.AimPosition = new Vector3(0f, -0.04f * s * Scale, 0.3f);
        }

        static void BuildKnife(ViewModelRig rig)
        {
            var g = rig.Root.transform;
            Box(g, "Handle", new Vector3(0f, -0.02f, 0f), new Vector3(0.026f, 0.03f, 0.11f), Polymer);
            Box(g, "Guard", new Vector3(0f, -0.02f, 0.058f), new Vector3(0.04f, 0.04f, 0.008f), Gunmetal);
            var blade = Box(g, "Blade", new Vector3(0f, -0.012f, 0.14f), new Vector3(0.004f, 0.034f, 0.16f), Steel);
            blade.transform.localRotation = Quaternion.Euler(-4f, 0f, 0f);
            rig.Muzzle = blade.transform;
            RightArm(g, new Vector3(0.004f, -0.025f, -0.01f));
            rig.HipPosition = new Vector3(0.16f, -0.15f, 0.3f);
            rig.AimPosition = rig.HipPosition;
        }

        // ------------------------------------------------------------------ arms

        static void RightArm(Transform g, Vector3 handPos)
        {
            var hand = Hand(g, "RightHand", handPos, true);
            Vector3 wrist = handPos + new Vector3(0.015f, -0.02f, -0.06f);
            Vector3 elbow = new Vector3(0.17f, -0.2f, -0.24f);
            Limb(g, "RightForearm", wrist, elbow, 0.036f, Sleeve);
            Cuff(g, wrist, elbow);
            Limb(g, "RightUpperArm", elbow, elbow + new Vector3(0.06f, -0.12f, -0.25f), 0.045f, Sleeve);
            Limb(g, "RightStripe", Vector3.Lerp(wrist, elbow, 0.55f), Vector3.Lerp(wrist, elbow, 0.7f), 0.0375f, Stripe);
            hand.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        }

        /// <summary>
        /// Left hand holding the handguard from the side: palm against the left face, fingers curled
        /// over the top, forearm running back-left out of the screen (visible like in modern shooters).
        /// </summary>
        static Transform SupportArm(Transform g, Vector3 palmPos)
        {
            var hand = new GameObject("LeftHand").transform;
            hand.SetParent(g, false);
            hand.localPosition = palmPos;
            Box(hand, "Palm", new Vector3(-0.006f, -0.01f, 0f), new Vector3(0.022f, 0.06f, 0.085f), Glove);
            Box(hand, "PalmPad", new Vector3(-0.018f, -0.012f, 0f), new Vector3(0.006f, 0.045f, 0.06f), GlovePad);
            for (int i = 0; i < 4; i++)
            {
                float z = 0.03f - i * 0.02f;
                Limb(hand, "Finger" + i, new Vector3(-0.004f, 0.022f, z), new Vector3(0.046f, 0.03f, z + 0.004f), 0.0085f, Glove);
                Limb(hand, "FingerTip" + i, new Vector3(0.046f, 0.03f, z + 0.004f), new Vector3(0.062f, 0.012f, z + 0.006f), 0.008f, Glove);
            }
            Limb(hand, "Thumb", new Vector3(-0.004f, -0.035f, 0.02f), new Vector3(0.03f, -0.03f, 0.05f), 0.009f, Glove);
            Vector3 wrist = new Vector3(-0.02f, -0.045f, -0.06f);
            Vector3 elbow = new Vector3(-0.17f, -0.2f, -0.2f);
            Limb(hand, "Cuff", wrist, Vector3.Lerp(wrist, elbow, 0.1f), 0.037f, GlovePad);
            Limb(hand, "LeftForearm", wrist, elbow, 0.036f, Sleeve);
            Limb(hand, "LeftStripe", Vector3.Lerp(wrist, elbow, 0.5f), Vector3.Lerp(wrist, elbow, 0.62f), 0.0375f, Stripe);
            Limb(hand, "LeftUpperArm", elbow, elbow + new Vector3(-0.06f, -0.12f, -0.3f), 0.045f, Sleeve);
            return hand;
        }

        static Transform LeftArm(Transform g, Vector3 handPos, bool pistolGrip = false)
        {
            var hand = Hand(g, "LeftHand", handPos, false);
            Vector3 wrist = handPos + new Vector3(-0.03f, -0.035f, -0.06f);
            Vector3 elbow = pistolGrip ? new Vector3(-0.16f, -0.24f, -0.22f) : new Vector3(-0.2f, -0.2f, 0.02f);
            // The forearm is parented to the hand so it follows reload motions.
            Limb(hand, "LeftForearm", hand.InverseTransformPoint(g.TransformPoint(wrist)), hand.InverseTransformPoint(g.TransformPoint(elbow)), 0.036f, Sleeve);
            Limb(hand, "LeftStripe", hand.InverseTransformPoint(g.TransformPoint(Vector3.Lerp(wrist, elbow, 0.55f))),
                hand.InverseTransformPoint(g.TransformPoint(Vector3.Lerp(wrist, elbow, 0.7f))), 0.0375f, Stripe);
            Limb(hand, "LeftUpperArm", hand.InverseTransformPoint(g.TransformPoint(elbow)), hand.InverseTransformPoint(g.TransformPoint(elbow + new Vector3(-0.05f, -0.15f, -0.3f))), 0.045f, Sleeve);
            hand.localRotation = pistolGrip ? Quaternion.Euler(0f, 25f, -10f) : Quaternion.Euler(0f, 0f, 20f);
            return hand;
        }

        static Transform Hand(Transform g, string name, Vector3 pos, bool right)
        {
            var hand = new GameObject(name).transform;
            hand.SetParent(g, false);
            hand.localPosition = pos;
            float side = right ? 1f : -1f;
            Box(hand, "Palm", new Vector3(0.012f * side, 0f, -0.01f), new Vector3(0.03f, 0.07f, 0.075f), Glove);
            Box(hand, "Knuckles", new Vector3(0.024f * side, 0.005f, 0.01f), new Vector3(0.012f, 0.06f, 0.05f), GlovePad);
            // Fingers wrap around the grip toward the other side.
            for (int i = 0; i < 4; i++)
            {
                float y = 0.025f - i * 0.017f;
                Limb(hand, "Finger" + i, new Vector3(0.02f * side, y, 0.02f), new Vector3(-0.022f * side, y - 0.004f, 0.025f), 0.0085f, Glove);
            }
            Limb(hand, "Thumb", new Vector3(-0.005f * side, 0.03f, -0.01f), new Vector3(-0.02f * side, 0.04f, 0.035f), 0.009f, Glove);
            return hand;
        }

        static void Cuff(Transform g, Vector3 wrist, Vector3 elbow)
        {
            Limb(g, "Cuff", wrist, Vector3.Lerp(wrist, elbow, 0.12f), 0.039f, GlovePad);
        }

        // ------------------------------------------------------------------ primitives

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Cyl(Transform parent, string name, Vector3 pos, float radius, float length, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Limb(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            Vector3 d = to - from;
            float len = d.magnitude;
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            go.transform.localScale = new Vector3(radius * 2f, Mathf.Max(radius, len * 0.5f + radius), radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }

        /// <summary>Third-person weapon for other players' bodies (GLB from ThirdPerson_p or World_w, with primitive fallback).</summary>
        public static GameObject BuildWorldWeapon(WeaponDefinition w, Transform parent)
        {
            var root = new GameObject("WorldWeapon_" + w.Id);
            root.transform.SetParent(parent, false);

            string glbId = GlbId(w.Id);
            var prefab = Resources.Load<GameObject>("Adventure3D/Weapons/ThirdPerson/p_" + glbId)
                      ?? Resources.Load<GameObject>("Adventure3D/Weapons/World/w_" + glbId);
            if (prefab != null)
            {
                var model = Object.Instantiate(prefab, root.transform, false);
                model.name = "Model";
                foreach (var col in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);
                Bounds b = default;
                bool has = false;
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                }
                if (has)
                {
                    Vector3 centerLocal = root.transform.InverseTransformPoint(b.center);
                    if (b.size.x > b.size.z * 1.3f)
                    {
                        var holder = new GameObject("Align").transform;
                        holder.SetParent(root.transform, false);
                        holder.localRotation = Quaternion.Euler(0f, 90f, 0f);
                        model.transform.SetParent(holder, false);
                    }
                    model.transform.localPosition = -centerLocal;
                }
                return root;
            }

            if (w.IsKnife) return root;
            bool longGun = w.Class != WeaponClass.Pistol;
            float len = longGun ? (w.Class == WeaponClass.Sniper ? 1.15f : 0.85f) : 0.2f;
            Box(root.transform, "Body", new Vector3(0f, 0f, len * 0.3f), new Vector3(0.05f, 0.07f, len * 0.6f), Gunmetal);
            Cyl(root.transform, "Barrel", new Vector3(0f, 0.01f, len * 0.75f), 0.012f, len * 0.4f, Gunmetal);
            if (longGun) Box(root.transform, "Stock", new Vector3(0f, -0.02f, -0.12f), new Vector3(0.04f, 0.07f, 0.24f), w.Id == "ak47" ? Wood : Polymer);
            Box(root.transform, "Mag", new Vector3(0f, -0.07f, len * 0.3f), new Vector3(0.03f, 0.1f, 0.05f), Blackened);
            return root;
        }
    }
}
