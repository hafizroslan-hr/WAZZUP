using UnityEngine;

namespace Wardline
{
    /// <summary>
    /// First-person weapon presentation: procedural model + arms, with deploy, fire kick, reload, melee,
    /// throw, bob and sway animations. Rendered by a dedicated overlay camera so it never clips into walls.
    /// </summary>
    public class ViewModel : MonoBehaviour
    {
        Actor actor;
        Transform holder;           // animated pivot
        WeaponModels.Parts parts;
        WeaponDef shown;
        Transform armL, armR;
        Vector3 magRest;

        float kick, kickRot, meleeT = -1f, throwT = -1f, bobPhase, deployStart;
        Vector2 sway;
        public Vector3 MuzzleWorld => parts != null && parts.muzzle ? parts.muzzle.position : transform.position + transform.forward * 0.5f;

        public static ViewModel Create(Actor actor, Transform cameraTransform)
        {
            var go = new GameObject("ViewModel");
            go.transform.SetParent(cameraTransform, false);
            var vm = go.AddComponent<ViewModel>();
            vm.actor = actor;
            vm.holder = new GameObject("Holder").transform;
            vm.holder.SetParent(go.transform, false);
            actor.weapons.OnFired += vm.HandleFire;
            actor.weapons.OnSwitched += vm.HandleSwitch;
            actor.weapons.OnMelee += vm.HandleMelee;
            return vm;
        }

        void OnDestroy()
        {
            if (actor && actor.weapons)
            {
                actor.weapons.OnFired -= HandleFire;
                actor.weapons.OnSwitched -= HandleSwitch;
                actor.weapons.OnMelee -= HandleMelee;
            }
        }

        void HandleSwitch(WeaponDef def) { Rebuild(def); deployStart = Time.time; }

        void HandleFire(WeaponDef def)
        {
            kick = def.category == WeaponCategory.Sniper || def.category == WeaponCategory.Shotgun ? 1f : 0.55f;
            kickRot = Mathf.Min(1f, def.recoilUp / 3f + 0.3f);
            if (def.IsGun && parts?.ejection != null && def.category != WeaponCategory.Shotgun && def.fireMode != FireMode.Bolt && GameApp.Settings.effectsQuality > 0)
                Effects.ShellCasing(parts.ejection.position, transform.right, Layers.ViewModel);
            if (GameApp.Settings.haptics) Haptics.Pulse(def.category == WeaponCategory.Sniper || def.category == WeaponCategory.Shotgun ? 1 : 0, 0.06f);
        }

        void HandleMelee(WeaponDef def)
        {
            if (def.category == WeaponCategory.Grenade) throwT = 0f; else meleeT = 0f;
        }

        void Rebuild(WeaponDef def)
        {
            if (parts != null && parts.root) Destroy(parts.root);
            if (armL) Destroy(armL.gameObject);
            if (armR) Destroy(armR.gameObject);
            parts = null; shown = def;
            if (def == null) return;

            parts = WeaponModels.Build(def, holder, 1f);
            magRest = parts.magazine ? parts.magazine.localPosition : Vector3.zero;

            // Arms: team-coloured sleeves with gloves
            var sleeve = MaterialLib.Lit(actor.team == Team.Raiders ? new Color(0.45f, 0.37f, 0.26f) : new Color(0.16f, 0.2f, 0.3f));
            var glove = MaterialLib.Lit(new Color(0.1f, 0.1f, 0.1f));
            armR = Arm(holder, new Vector3(0.05f, -0.09f, -0.12f), Quaternion.Euler(15, -8, 0), sleeve, glove);
            bool twoHanded = def.category != WeaponCategory.Pistol && def.category != WeaponCategory.Knife && def.category != WeaponCategory.Grenade || def.id == "twin9";
            if (twoHanded)
            {
                Vector3 lh = parts.leftHand ? parts.leftHand.localPosition : new Vector3(-0.1f, -0.05f, 0.1f);
                armL = Arm(holder, lh + new Vector3(-0.07f, -0.06f, -0.2f), Quaternion.Euler(10, 22, 0), sleeve, glove);
            }
            Layers.SetLayerRecursive(holder.gameObject, Layers.ViewModel);
            foreach (var r in holder.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Transform Arm(Transform parent, Vector3 pos, Quaternion rot, Material sleeve, Material glove)
        {
            var a = new GameObject("Arm").transform; a.SetParent(parent, false); a.localPosition = pos; a.localRotation = rot;
            MeshUtil.Visual(PrimitiveType.Cube, a, new Vector3(0, 0, -0.12f), new Vector3(0.075f, 0.075f, 0.3f), sleeve);
            MeshUtil.Visual(PrimitiveType.Cube, a, new Vector3(0, 0, 0.06f), new Vector3(0.07f, 0.06f, 0.08f), glove);
            return a;
        }

        Vector3 BasePosition(WeaponDef def)
        {
            if (def == null) return new Vector3(0.18f, -0.2f, 0.35f);
            switch (def.category)
            {
                case WeaponCategory.Pistol: return def.id == "twin9" ? new Vector3(0f, -0.17f, 0.33f) : new Vector3(0.14f, -0.15f, 0.3f);
                case WeaponCategory.Knife: return new Vector3(0.17f, -0.17f, 0.3f);
                case WeaponCategory.Grenade: case WeaponCategory.Bomb: return new Vector3(0.15f, -0.17f, 0.3f);
                default: return new Vector3(0.14f, -0.15f, 0.22f + def.modelLength * 0.12f);
            }
        }

        public void Tick(Vector2 lookDelta, bool hidden)
        {
            var w = actor.weapons;
            if (w.CurrentDef != shown) Rebuild(w.CurrentDef);
            holder.gameObject.SetActive(!hidden && parts != null);
            if (parts == null) return;
            float dt = Time.deltaTime;
            var def = shown;

            // Bob & sway
            float speed = actor.HorizontalSpeed;
            if (actor.grounded) bobPhase += dt * speed * 1.7f;
            float bobAmt = Mathf.Clamp01(speed / Actor.RunSpeed) * 0.012f;
            Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * bobAmt, -Mathf.Abs(Mathf.Cos(bobPhase)) * bobAmt, 0);
            sway = Vector2.Lerp(sway, new Vector2(-lookDelta.x, lookDelta.y) * 0.004f, dt * 10f);
            sway = Vector2.ClampMagnitude(sway, 0.04f);

            kick = Mathf.MoveTowards(kick, 0f, dt * 7f);
            kickRot = Mathf.MoveTowards(kickRot, 0f, dt * 6f);

            Vector3 pos = BasePosition(def) + bob + new Vector3(sway.x, sway.y, 0) + new Vector3(0, 0, -0.05f * kick);
            Quaternion rot = Quaternion.Euler(-6f * kickRot, sway.x * 60f, 0);

            // Deploy: rise from below
            float dep = Mathf.Clamp01((Time.time - deployStart) / Mathf.Max(0.15f, def.deployTime));
            pos += Vector3.down * (1f - dep) * 0.22f;
            rot *= Quaternion.Euler((1f - dep) * 30f, 0, 0);

            // Reload: tilt and drop the magazine
            if (w.reloading && def.IsGun)
            {
                float total = def.category == WeaponCategory.Shotgun ? 0.5f : def.reloadTime;
                float k = 1f - Mathf.Clamp01((w.reloadUntil - Time.time) / total);
                float tilt = Mathf.Sin(Mathf.Clamp01(k * 1.2f) * Mathf.PI);
                rot *= Quaternion.Euler(18f * tilt, 0, 25f * tilt);
                pos += new Vector3(0, -0.04f, 0) * tilt;
                if (parts.magazine && def.category != WeaponCategory.Shotgun)
                {
                    float out01 = k < 0.5f ? Mathf.Clamp01(k * 4f) : Mathf.Clamp01((0.9f - k) * 4f);
                    parts.magazine.localPosition = magRest + new Vector3(0, -0.18f * out01, -0.04f * out01);
                }
            }
            else if (parts.magazine) parts.magazine.localPosition = magRest;

            // Knife swing
            if (meleeT >= 0f)
            {
                meleeT += dt * 3.2f;
                float s = Mathf.Sin(Mathf.Clamp01(meleeT) * Mathf.PI);
                rot *= Quaternion.Euler(20f * s, -60f * s, -20f * s);
                pos += new Vector3(-0.12f * s, 0.03f * s, 0.08f * s);
                if (meleeT > 1f) meleeT = -1f;
            }
            // Grenade throw
            if (throwT >= 0f)
            {
                throwT += dt * 3f;
                float s = Mathf.Sin(Mathf.Clamp01(throwT) * Mathf.PI);
                pos += new Vector3(0, 0.1f * s, 0.2f * s);
                rot *= Quaternion.Euler(-40f * s, 0, 0);
                if (throwT > 1f) throwT = -1f;
            }
            else if (w.GrenadePrimed)
            {
                pos += new Vector3(0.02f, 0.06f, -0.08f);
                rot *= Quaternion.Euler(25f, 0, 0);
            }
            // Sprinting lowers the weapon a little
            if (!actor.walking && speed > Actor.RunSpeed * 0.8f) rot *= Quaternion.Euler(4f, -6f, 0);

            holder.localPosition = pos;
            holder.localRotation = rot;
        }
    }
}
