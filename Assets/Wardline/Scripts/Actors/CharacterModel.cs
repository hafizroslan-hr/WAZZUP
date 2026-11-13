using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    /// <summary>
    /// Procedural low-poly humanoid built from primitives, with per-zone hitboxes, procedural walk/crouch
    /// animation, a held third-person weapon and a physics-free death fall.
    /// </summary>
    public class CharacterModel : MonoBehaviour
    {
        public Transform visualRoot, hips, torso, head, legL, legR, armL, armR, weaponHolder, muzzle;
        readonly List<Collider> hitboxes = new List<Collider>();
        readonly List<Renderer> teamRenderers = new List<Renderer>();
        readonly List<Renderer> accentRenderers = new List<Renderer>();
        Renderer helmetRenderer, maskRenderer, bandRenderer;
        GameObject weaponVisual;
        WeaponDef shownWeapon;

        float phase, crouchBlend, deathT = -1f;
        Vector3 deathAxis;
        Actor actor;

        public static CharacterModel Build(Actor actor, Team team)
        {
            var root = new GameObject("Model");
            root.transform.SetParent(actor.transform, false);
            var m = root.AddComponent<CharacterModel>();
            m.actor = actor;
            m.Construct();
            m.ApplyTeam(team);
            return m;
        }

        void Construct()
        {
            var skin = MaterialLib.Lit(new Color(0.75f, 0.58f, 0.45f));
            var dark = MaterialLib.Lit(new Color(0.08f, 0.08f, 0.09f));
            var boots = MaterialLib.Lit(new Color(0.12f, 0.1f, 0.08f));

            visualRoot = new GameObject("Visual").transform; visualRoot.SetParent(transform, false);
            hips = new GameObject("Hips").transform; hips.SetParent(visualRoot, false); hips.localPosition = new Vector3(0, 0.95f, 0);
            torso = new GameObject("Torso").transform; torso.SetParent(hips, false);

            // Torso: chest + stomach hitboxes
            var chest = MeshUtil.Visual(PrimitiveType.Cube, torso, new Vector3(0, 0.45f, 0), new Vector3(0.46f, 0.36f, 0.26f), skin);
            teamRenderers.Add(chest.GetComponent<Renderer>());
            AddHitbox(chest, HitZone.Chest, false);
            var stomach = MeshUtil.Visual(PrimitiveType.Cube, torso, new Vector3(0, 0.12f, 0), new Vector3(0.4f, 0.3f, 0.24f), skin);
            accentRenderers.Add(stomach.GetComponent<Renderer>());
            AddHitbox(stomach, HitZone.Stomach, false);
            var vest = MeshUtil.Visual(PrimitiveType.Cube, torso, new Vector3(0, 0.38f, 0.02f), new Vector3(0.5f, 0.3f, 0.3f), skin);
            accentRenderers.Add(vest.GetComponent<Renderer>());
            var band = MeshUtil.Visual(PrimitiveType.Cube, torso, new Vector3(-0.29f, 0.48f, 0), new Vector3(0.05f, 0.08f, 0.2f), skin);
            bandRenderer = band.GetComponent<Renderer>();

            // Head
            head = new GameObject("Head").transform; head.SetParent(torso, false); head.localPosition = new Vector3(0, 0.78f, 0);
            var headGo = MeshUtil.Visual(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.25f, 0.28f, 0.27f), skin);
            AddHitbox(headGo, HitZone.Head, true);
            var mask = MeshUtil.Visual(PrimitiveType.Sphere, head, new Vector3(0, -0.02f, 0.01f), new Vector3(0.262f, 0.25f, 0.27f), dark);
            maskRenderer = mask.GetComponent<Renderer>();
            MeshUtil.Visual(PrimitiveType.Cube, head, new Vector3(0, 0.03f, 0.12f), new Vector3(0.17f, 0.045f, 0.05f), MaterialLib.Lit(new Color(0.05f, 0.05f, 0.05f), null, 0.8f));
            var helmet = MeshUtil.Visual(PrimitiveType.Sphere, head, new Vector3(0, 0.07f, -0.01f), new Vector3(0.3f, 0.22f, 0.31f), dark);
            helmetRenderer = helmet.GetComponent<Renderer>();

            // Legs pivot at hip
            legL = Limb("LegL", hips, new Vector3(-0.12f, 0f, 0), new Vector3(0.17f, 0.92f, 0.19f), HitZone.Leg, boots, true);
            legR = Limb("LegR", hips, new Vector3(0.12f, 0f, 0), new Vector3(0.17f, 0.92f, 0.19f), HitZone.Leg, boots, true);
            // Arms pivot at shoulder
            armL = Limb("ArmL", torso, new Vector3(-0.3f, 0.6f, 0), new Vector3(0.12f, 0.62f, 0.13f), HitZone.Arm, skin, false);
            armR = Limb("ArmR", torso, new Vector3(0.3f, 0.6f, 0), new Vector3(0.12f, 0.62f, 0.13f), HitZone.Arm, skin, false);

            weaponHolder = new GameObject("WeaponHolder").transform; weaponHolder.SetParent(torso, false);
            weaponHolder.localPosition = new Vector3(0.12f, 0.42f, 0.38f);
            muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(weaponHolder, false); muzzle.localPosition = new Vector3(0, 0.03f, 0.5f);

            // Arms hold the weapon forward
            armL.localRotation = Quaternion.Euler(-75, 18, 0);
            armR.localRotation = Quaternion.Euler(-80, -8, 0);
        }

        Transform Limb(string n, Transform parent, Vector3 pivot, Vector3 size, HitZone zone, Material endMat, bool isLeg)
        {
            var p = new GameObject(n).transform; p.SetParent(parent, false); p.localPosition = pivot;
            var seg = MeshUtil.Visual(PrimitiveType.Cube, p, new Vector3(0, -size.y * 0.5f, 0), size, MaterialLib.Lit(Color.gray));
            if (isLeg) accentRenderers.Add(seg.GetComponent<Renderer>()); else teamRenderers.Add(seg.GetComponent<Renderer>());
            AddHitbox(seg, zone, false);
            var end = MeshUtil.Visual(PrimitiveType.Cube, p, new Vector3(0, -size.y + (isLeg ? 0.05f : 0.04f), isLeg ? 0.04f : 0), new Vector3(size.x * 1.05f, isLeg ? 0.12f : 0.1f, size.z * (isLeg ? 1.4f : 1f)), endMat);
            return p;
        }

        void AddHitbox(GameObject go, HitZone zone, bool sphere)
        {
            Collider c = sphere ? (Collider)go.AddComponent<SphereCollider>() : go.AddComponent<BoxCollider>();
            c.isTrigger = true;
            go.layer = Layers.Hitbox;
            var hb = go.AddComponent<Hitbox>();
            hb.actor = actor;
            hb.zone = zone;
            hitboxes.Add(c);
        }

        public void ApplyTeam(Team t)
        {
            Color body, accent, helmetC, bandC;
            bool helmetOn;
            if (t == Team.Raiders)
            {
                body = new Color(0.52f, 0.43f, 0.31f); accent = new Color(0.3f, 0.29f, 0.2f); helmetC = new Color(0.1f, 0.1f, 0.1f);
                bandC = new Color(0.9f, 0.45f, 0.1f); helmetOn = false;
            }
            else
            {
                body = new Color(0.17f, 0.22f, 0.32f); accent = new Color(0.12f, 0.14f, 0.18f); helmetC = new Color(0.2f, 0.24f, 0.3f);
                bandC = new Color(0.25f, 0.55f, 1f); helmetOn = true;
            }
            var bodyM = MaterialLib.Lit(body); var accM = MaterialLib.Lit(accent);
            foreach (var r in teamRenderers) r.sharedMaterial = bodyM;
            foreach (var r in accentRenderers) r.sharedMaterial = accM;
            bandRenderer.sharedMaterial = MaterialLib.Lit(bandC);
            helmetRenderer.sharedMaterial = MaterialLib.Lit(helmetC, null, 0.4f);
            helmetRenderer.enabled = helmetOn;
            maskRenderer.enabled = !helmetOn;
        }

        public void ShowWeapon(WeaponDef def)
        {
            if (def == shownWeapon) return;
            shownWeapon = def;
            if (weaponVisual) Destroy(weaponVisual);
            if (def == null) return;
            weaponVisual = WeaponModels.BuildWorld(def, weaponHolder);
            weaponVisual.transform.localPosition = Vector3.zero;
            if (ownerHidden) foreach (var r in weaponVisual.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            muzzle.localPosition = new Vector3(0, 0.03f, def.modelLength * 0.5f + def.barrelLength + 0.05f);
        }

        public void SetHitboxesActive(bool on) { foreach (var c in hitboxes) if (c) c.enabled = on; }

        /// <summary>Hide the body from its owner's first-person camera while keeping hitboxes and shadows.</summary>
        bool ownerHidden;
        public void SetOwnerHidden(bool hidden)
        {
            ownerHidden = hidden;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = hidden ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        public void ResetPose()
        {
            deathT = -1f;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localPosition = Vector3.zero;
            crouchBlend = 0f;
        }

        public void PlayDeath(Vector3 hitDir)
        {
            deathT = 0f;
            Vector3 local = transform.InverseTransformDirection(hitDir.sqrMagnitude > 0.01f ? hitDir.Flat().normalized : -transform.forward);
            if (local.sqrMagnitude < 0.01f) local = Vector3.back;
            deathAxis = Vector3.Cross(Vector3.up, local).normalized;
            if (deathAxis.sqrMagnitude < 0.01f) deathAxis = Vector3.right;
        }

        void LateUpdate()
        {
            if (actor == null) return;
            float dt = Time.deltaTime;

            if (deathT >= 0f)
            {
                deathT += dt;
                float k = Mathf.Clamp01(deathT / 0.55f);
                float ease = k * k * (3f - 2f * k);
                visualRoot.localRotation = Quaternion.AngleAxis(88f * ease, deathAxis);
                visualRoot.localPosition = new Vector3(0, -0.12f * ease, 0);
                legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(-20, 0, 0), dt * 6f);
                legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(10, 0, 0), dt * 6f);
                return;
            }

            crouchBlend = Mathf.MoveTowards(crouchBlend, actor.crouching ? 1f : 0f, dt * 6f);
            Vector3 v = actor.velocity; v.y = 0;
            Vector3 localV = transform.InverseTransformDirection(v);
            float speed = v.magnitude;
            phase += dt * speed * 1.9f;
            float swing = Mathf.Clamp01(speed / 3f) * (actor.grounded ? 32f : 10f);
            float dirSign = localV.z < -0.2f ? -1f : 1f;
            float s = Mathf.Sin(phase) * swing * dirSign;

            hips.localPosition = new Vector3(0, Mathf.Lerp(0.95f, 0.58f, crouchBlend) + Mathf.Abs(Mathf.Cos(phase)) * 0.03f * Mathf.Clamp01(speed / 3f), 0);
            float crouchLeg = 75f * crouchBlend;
            legL.localRotation = Quaternion.Euler(-crouchLeg + s, 0, 0);
            legR.localRotation = Quaternion.Euler(-crouchLeg - s, 0, 0);
            torso.localRotation = Quaternion.Euler(crouchBlend * 18f, 0, 0);
            // Aim pitch drives the upper body so the head and weapon look where the actor aims.
            float aimPitch = Mathf.Clamp(actor.pitch, -60f, 60f);
            head.localRotation = Quaternion.Euler(aimPitch * 0.5f, 0, 0);
            weaponHolder.localRotation = Quaternion.Euler(aimPitch - crouchBlend * 18f, 0, 0);
            armL.localRotation = Quaternion.Euler(-75 + aimPitch - crouchBlend * 18f, 18, 0);
            armR.localRotation = Quaternion.Euler(-80 + aimPitch - crouchBlend * 18f, -8, 0);
        }
    }
}
