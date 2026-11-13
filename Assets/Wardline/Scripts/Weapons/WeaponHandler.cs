using System;
using UnityEngine;

namespace Wardline
{
    [Serializable]
    public class WeaponInstance
    {
        public WeaponDef def;
        public int mag, reserve;
        public WeaponInstance(WeaponDef d) { def = d; mag = d.magazine; reserve = d.reserve; }
    }

    /// <summary>
    /// Inventory and firing logic shared by the player and bots: slots, switching, reloading, recoil, spread,
    /// scopes, knife, grenades and the charge. Controllers only feed intent (trigger, aim direction).
    /// </summary>
    public class WeaponHandler : MonoBehaviour
    {
        public Actor actor;
        public WeaponInstance primary, secondary;
        public readonly WeaponInstance knife = new WeaponInstance(WeaponDatabase.Knife);
        public int heCount, flashCount, smokeCount;
        public bool hasCharge;

        public WeaponSlot slot = WeaponSlot.Secondary;
        WeaponSlot previousSlot = WeaponSlot.Knife;
        public GrenadeType selectedGrenade = GrenadeType.HE;

        public float deployUntil, nextFireTime, reloadUntil;
        public bool reloading;
        public int zoomLevel;
        int zoomRestore;
        public float recoilShots;
        public Vector2 punch;                       // x = pitch up (deg), y = yaw (deg)
        float lastShot = -10f;
        bool grenadePrimed;
        public float grenadePrimeTime;
        public bool unlimitedAmmo;

        public event Action<WeaponDef> OnFired, OnSwitched, OnReloadStart, OnReloadEnd;
        public event Action<WeaponDef> OnMelee;

        // Visual hooks: player view model and bots' third-person muzzle supply these
        public Func<Vector3> muzzleProvider;
        public int shotsFired;

        public WeaponInstance Current => slot == WeaponSlot.Primary ? primary : slot == WeaponSlot.Secondary ? secondary : slot == WeaponSlot.Knife ? knife : null;

        public WeaponDef CurrentDef
        {
            get
            {
                switch (slot)
                {
                    case WeaponSlot.Primary: return primary?.def;
                    case WeaponSlot.Secondary: return secondary?.def;
                    case WeaponSlot.Knife: return knife.def;
                    case WeaponSlot.Grenade: return GrenadeDef(selectedGrenade);
                    case WeaponSlot.Bomb: return hasCharge ? WeaponDatabase.Charge : null;
                }
                return null;
            }
        }

        public bool IsDeploying => Time.time < deployUntil;
        public bool CanFireNow => Time.time >= nextFireTime && !IsDeploying && !reloading && !actor.InFreeze;
        public int GrenadeCount(GrenadeType t) => t == GrenadeType.HE ? heCount : t == GrenadeType.Flash ? flashCount : t == GrenadeType.Smoke ? smokeCount : 0;
        public bool HasAnyGrenade => heCount + flashCount + smokeCount > 0;
        public static WeaponDef GrenadeDef(GrenadeType t) => t == GrenadeType.HE ? WeaponDatabase.Get("he") : t == GrenadeType.Flash ? WeaponDatabase.Get("flash") : t == GrenadeType.Smoke ? WeaponDatabase.Get("smoke") : null;

        public void ResetToDefaults()
        {
            primary = null;
            secondary = new WeaponInstance(WeaponDatabase.DefaultPistol(actor.team));
            heCount = flashCount = smokeCount = 0;
            hasCharge = false;
            Equip(WeaponSlot.Secondary, true);
        }

        public void RefillForNewRound()
        {
            hasCharge = false;
            if (secondary == null) secondary = new WeaponInstance(WeaponDatabase.DefaultPistol(actor.team));
            Equip(primary != null ? WeaponSlot.Primary : WeaponSlot.Secondary, true);
        }

        /// <summary>Gives a weapon or grenade. Returns the replaced weapon (to be dropped), if any.</summary>
        public WeaponInstance Give(WeaponDef def, bool autoEquip = true)
        {
            if (def == null || def.category == WeaponCategory.Equipment) return null;
            WeaponInstance dropped = null;
            switch (def.slot)
            {
                case WeaponSlot.Primary: dropped = primary; primary = new WeaponInstance(def); if (autoEquip) Equip(WeaponSlot.Primary); break;
                case WeaponSlot.Secondary: dropped = secondary; secondary = new WeaponInstance(def); if (autoEquip && primary == null) Equip(WeaponSlot.Secondary); break;
                case WeaponSlot.Grenade:
                    if (def.grenade == GrenadeType.HE) heCount = Mathf.Min(def.maxCarry, heCount + 1);
                    if (def.grenade == GrenadeType.Flash) flashCount = Mathf.Min(def.maxCarry, flashCount + 1);
                    if (def.grenade == GrenadeType.Smoke) smokeCount = Mathf.Min(def.maxCarry, smokeCount + 1);
                    break;
                case WeaponSlot.Bomb: hasCharge = true; break;
            }
            return dropped;
        }

        public void GiveInstance(WeaponInstance inst)
        {
            if (inst.def.slot == WeaponSlot.Primary) { primary = inst; Equip(WeaponSlot.Primary); }
            else if (inst.def.slot == WeaponSlot.Secondary) { secondary = inst; if (primary == null || slot == WeaponSlot.Secondary) Equip(WeaponSlot.Secondary); }
        }

        public bool CanCarryGrenade(WeaponDef def) => GrenadeCount(def.grenade) < def.maxCarry;

        public bool HasSlot(WeaponSlot s) => s == WeaponSlot.Primary ? primary != null : s == WeaponSlot.Secondary ? secondary != null : s == WeaponSlot.Knife || (s == WeaponSlot.Grenade ? HasAnyGrenade : hasCharge);

        public void Equip(WeaponSlot s, bool instant = false)
        {
            if (!HasSlot(s)) return;
            if (s == WeaponSlot.Grenade && GrenadeCount(selectedGrenade) == 0) selectedGrenade = FirstGrenade();
            if (s != slot) previousSlot = slot;
            slot = s;
            reloading = false;
            zoomLevel = 0; zoomRestore = 0;
            grenadePrimed = false;
            var def = CurrentDef;
            deployUntil = Time.time + (instant ? 0f : def != null ? def.deployTime : 0.5f);
            if (actor.model) actor.model.ShowWeapon(def);
            if (!instant) AudioManager.Play3D("deploy", actor.EyePosition, 0.4f, 10f);
            OnSwitched?.Invoke(def);
        }

        GrenadeType FirstGrenade() => heCount > 0 ? GrenadeType.HE : flashCount > 0 ? GrenadeType.Flash : smokeCount > 0 ? GrenadeType.Smoke : GrenadeType.HE;

        /// <summary>Cycle primary → secondary → knife → grenades → charge.</summary>
        public void CycleNext()
        {
            if (slot == WeaponSlot.Grenade)
            {
                // step through grenade types before leaving the slot
                var order = new[] { GrenadeType.HE, GrenadeType.Flash, GrenadeType.Smoke };
                int idx = Array.IndexOf(order, selectedGrenade);
                for (int i = idx + 1; i < order.Length; i++)
                    if (GrenadeCount(order[i]) > 0) { selectedGrenade = order[i]; Equip(WeaponSlot.Grenade); return; }
            }
            for (int i = 1; i <= 5; i++)
            {
                var s = (WeaponSlot)(((int)slot + i) % 5);
                if (HasSlot(s)) { if (s == WeaponSlot.Grenade) selectedGrenade = FirstGrenade(); Equip(s); return; }
            }
        }

        public void SwapToPrevious() { if (HasSlot(previousSlot)) Equip(previousSlot); }

        /// <summary>Select grenades, stepping through the available types on repeated presses.</summary>
        public void SelectGrenadeSlot()
        {
            if (!HasAnyGrenade) return;
            if (slot == WeaponSlot.Grenade) { CycleGrenadeType(); return; }
            selectedGrenade = FirstGrenade();
            Equip(WeaponSlot.Grenade);
        }

        void CycleGrenadeType()
        {
            var order = new[] { GrenadeType.HE, GrenadeType.Flash, GrenadeType.Smoke };
            int idx = Array.IndexOf(order, selectedGrenade);
            for (int i = 1; i <= 3; i++)
            {
                var g = order[(idx + i) % 3];
                if (GrenadeCount(g) > 0) { selectedGrenade = g; Equip(WeaponSlot.Grenade); return; }
            }
        }

        public void Reload()
        {
            var w = Current;
            if (w == null || !w.def.IsGun || reloading || IsDeploying) return;
            if (w.mag >= w.def.magazine || (w.reserve <= 0 && !unlimitedAmmo)) return;
            reloading = true;
            zoomLevel = 0;
            bool shells = w.def.category == WeaponCategory.Shotgun;
            reloadUntil = Time.time + (shells ? 0.55f : w.def.reloadTime);
            AudioManager.Play3D(shells ? "pump" : "mag_out", actor.EyePosition, 0.5f, 18f);
            Hearing.Emit(actor.transform.position, 10f, NoiseType.Reload, actor);
            OnReloadStart?.Invoke(w.def);
        }

        public void ToggleZoom()
        {
            var def = CurrentDef;
            if (def == null || !def.HasScope || reloading || IsDeploying) return;
            zoomLevel = (zoomLevel + 1) % (def.zoomFov.Length + 1);
            zoomRestore = 0;
            AudioManager.Play2D("scope", actor.isPlayer ? 0.5f : 0f);
        }

        public float ZoomFov(float normalFov)
        {
            var def = CurrentDef;
            if (zoomLevel <= 0 || def == null || !def.HasScope) return normalFov;
            return def.zoomFov[Mathf.Clamp(zoomLevel - 1, 0, def.zoomFov.Length - 1)];
        }

        /// <summary>Current cone half-angle in degrees for the equipped gun, given the actor's movement.</summary>
        public float CurrentSpread()
        {
            var def = CurrentDef;
            if (def == null || !def.IsGun) return 0f;
            float maxSpeed = Actor.RunSpeed * def.moveSpeed;
            float speed01 = Mathf.Clamp01(actor.HorizontalSpeed / maxSpeed);
            float s = actor.crouching ? def.spreadCrouch : def.spreadStand;
            // Walking (under ~45% speed) stays accurate, running is punished — the classic tactical feel.
            s += def.spreadMove * Mathf.Clamp01((speed01 - 0.42f) / 0.58f);
            if (!actor.grounded) s = Mathf.Max(s, def.spreadAir);
            s += Mathf.Min(recoilShots * def.spreadPerShot, def.spreadMax);
            if (def.unscopedInaccurate && zoomLevel == 0) s += 4.5f;
            s *= 1f + actor.BlindAmount;
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var w = Current;

            if (reloading && Time.time >= reloadUntil && w != null)
            {
                if (w.def.category == WeaponCategory.Shotgun)
                {
                    if (unlimitedAmmo || w.reserve > 0) { w.mag++; if (!unlimitedAmmo) w.reserve--; AudioManager.Play3D("mag_in", actor.EyePosition, 0.35f, 12f); }
                    if (w.mag < w.def.magazine && (w.reserve > 0 || unlimitedAmmo)) reloadUntil = Time.time + 0.5f;
                    else { reloading = false; AudioManager.Play3D("pump", actor.EyePosition, 0.5f, 12f); OnReloadEnd?.Invoke(w.def); }
                }
                else
                {
                    int need = w.def.magazine - w.mag;
                    int take = unlimitedAmmo ? need : Mathf.Min(need, w.reserve);
                    w.mag += take; if (!unlimitedAmmo) w.reserve -= take;
                    reloading = false;
                    AudioManager.Play3D("mag_in", actor.EyePosition, 0.5f, 15f);
                    OnReloadEnd?.Invoke(w.def);
                }
            }

            // Bolt-action rifles leave the scope after each shot and return once the bolt is cycled.
            if (zoomRestore > 0 && Time.time >= nextFireTime && !reloading) { zoomLevel = zoomRestore; zoomRestore = 0; }

            var def = CurrentDef;
            if (def != null && def.IsGun)
            {
                float since = Time.time - lastShot;
                if (since > def.FireInterval * 1.3f)
                {
                    recoilShots = Mathf.MoveTowards(recoilShots, 0f, dt * 9f);
                    punch = Vector2.MoveTowards(punch, Vector2.zero, def.recoilRecovery * dt * (1f + punch.magnitude * 0.25f));
                }
            }
            else
            {
                recoilShots = 0f;
                punch = Vector2.MoveTowards(punch, Vector2.zero, 12f * dt);
            }
        }

        /// <summary>Feed trigger state once per frame. Returns true if a shot/attack happened.</summary>
        public bool TriggerUpdate(bool held, bool pressed, bool released)
        {
            var def = CurrentDef;
            if (def == null) return false;

            if (slot == WeaponSlot.Grenade) return GrenadeTrigger(held, pressed, released);
            if (slot == WeaponSlot.Bomb) return false;   // planting is driven by the match/use logic
            if (!CanFireNow) return false;

            if (def.category == WeaponCategory.Knife) { if (held) { Melee(false); return true; } return false; }

            bool wantShot = def.fireMode == FireMode.Auto ? held : pressed || (held && actor.isPlayer && GameApp.Settings.autoFire && def.fireMode == FireMode.Semi);
            if (!wantShot) return false;

            var w = Current;
            if (w.mag <= 0)
            {
                if (pressed) AudioManager.Play3D("dry", actor.EyePosition, 0.5f, 8f);
                nextFireTime = Time.time + 0.25f;
                if (w.reserve > 0 || unlimitedAmmo) { if (!actor.isPlayer || GameApp.Settings.autoReload) Reload(); }
                return false;
            }

            Fire(w);
            return true;
        }

        void Fire(WeaponInstance w)
        {
            var def = w.def;
            nextFireTime = Time.time + def.FireInterval;
            lastShot = Time.time;
            actor.lastFireTime = Time.time;
            w.mag--;
            shotsFired++;

            Vector3 origin = actor.EyePosition;
            Vector3 aim = actor.aimDirection;
            // Recoil punch is applied to the aim direction (the player camera already includes it).
            if (!actor.isPlayer)
            {
                float leak = actor.bot != null ? actor.bot.RecoilLeak : 1f;
                var e = Quaternion.LookRotation(aim).eulerAngles;
                aim = Quaternion.Euler(e.x - punch.x * leak, e.y + punch.y * leak, 0f) * Vector3.forward;
            }
            float spread = CurrentSpread();
            Vector3 muzzle = muzzleProvider != null ? muzzleProvider() : origin + aim * 0.5f;
            bool tracer = actor.isPlayer ? UnityEngine.Random.value < 0.25f : UnityEngine.Random.value < 0.45f;
            for (int i = 0; i < def.pellets; i++)
            {
                Vector3 d = MathUtil.SpreadDirection(aim, spread);
                Ballistics.FireBullet(actor, origin, d, def, muzzle, tracer && (i == 0 || UnityEngine.Random.value < 0.3f));
            }

            // Recoil: a learnable vertical climb plus a side sway that alternates as the spray continues.
            float climb = def.recoilUp * (recoilShots < 3 ? 0.8f : 1f);
            punch.x = Mathf.Min(def.recoilMaxUp, punch.x + climb);
            float sway = recoilShots > 4 ? Mathf.Sin(recoilShots * 0.55f) : 0.15f;
            punch.y += (sway + UnityEngine.Random.Range(-0.6f, 0.6f)) * def.recoilSide;
            punch.y = Mathf.Clamp(punch.y, -def.recoilMaxUp * 0.6f, def.recoilMaxUp * 0.6f);
            recoilShots += 1f;

            if (def.fireMode == FireMode.Bolt && zoomLevel > 0) { zoomRestore = zoomLevel; zoomLevel = 0; }

            float volume = def.silenced ? 0.4f : 1f;
            AudioManager.Play3D(def.sound, origin, volume, def.silenced ? 25f : 95f, def.category == WeaponCategory.Sniper ? 0.95f : 1f);
            if (def.fireMode == FireMode.Bolt) AudioManager.Play3D("bolt", origin, 0.45f, 12f);
            if (def.fireMode == FireMode.Pump) AudioManager.Play3D("pump", origin, 0.45f, 12f);
            Hearing.Emit(origin, def.silenced ? 14f : 60f, NoiseType.Gunshot, actor);
            Effects.MuzzleFlash(muzzle, aim, def.silenced, actor.isPlayer ? Layers.ViewModel : 0);

            OnFired?.Invoke(def);

            if (w.mag == 0 && (w.reserve > 0 || unlimitedAmmo) && (!actor.isPlayer || GameApp.Settings.autoReload))
                Invoke(nameof(AutoReload), 0.25f);
            if (unlimitedAmmo && w.mag == 0) w.reserve = Mathf.Max(w.reserve, def.magazine);
        }

        void AutoReload() { if (Current != null && Current.mag == 0) Reload(); }

        public void Stab() { if (slot == WeaponSlot.Knife && CanFireNow) Melee(true); }

        void Melee(bool stab)
        {
            var def = knife.def;
            nextFireTime = Time.time + (stab ? 1.1f : 0.4f);
            lastShot = Time.time;
            AudioManager.Play3D("knife", actor.EyePosition, 0.6f, 12f);
            Hearing.Emit(actor.transform.position, 8f, NoiseType.Knife, actor);
            OnMelee?.Invoke(def);
            Vector3 origin = actor.EyePosition, dir = actor.aimDirection;
            if (Ballistics.Trace(actor, origin, dir, stab ? 1.6f : 1.9f, out var hit, out var hb) && hb != null)
            {
                var victim = hb.actor;
                bool same = victim.team == actor.team && MatchManager.I != null && MatchManager.I.Config.mode != GameMode.Practice;
                if (!same || Ballistics.FriendlyFire)
                {
                    float dmg = stab ? 65f : def.damage;
                    bool backstab = Vector3.Dot(victim.transform.forward, dir.Flat().normalized) > 0.5f;
                    if (backstab) dmg *= stab ? 3f : 2f;
                    victim.ApplyDamage(new DamageInfo { amount = dmg, attacker = actor, weapon = def, zone = hb.zone == HitZone.Head ? HitZone.Head : HitZone.Chest, point = hit.point, direction = dir, sourcePosition = origin });
                    if (actor.isPlayer) PlayerHitFeedback.Register(hb.zone == HitZone.Head, !victim.alive);
                }
                Effects.Blood(hit.point, dir, false);
                AudioManager.Play3D("knife_hit", hit.point, 0.8f, 15f);
            }
            else if (Ballistics.Trace(actor, origin, dir, 1.9f, out var wh, out _))
            {
                Effects.Impact(wh.point, wh.normal, Color.gray);
                AudioManager.Play3D("impact", wh.point, 0.6f, 12f);
            }
        }

        bool GrenadeTrigger(bool held, bool pressed, bool released)
        {
            if (IsDeploying || actor.InFreeze) return false;
            if (GrenadeCount(selectedGrenade) <= 0) { CycleNext(); return false; }
            if (pressed && !grenadePrimed)
            {
                grenadePrimed = true; grenadePrimeTime = Time.time;
                AudioManager.Play3D("pin", actor.EyePosition, 0.5f, 8f);
            }
            if (grenadePrimed && (released || !held) && Time.time - grenadePrimeTime > 0.15f)
            {
                grenadePrimed = false;
                ThrowGrenade(selectedGrenade, actor.aimDirection, 1f);
                return true;
            }
            return false;
        }

        public bool GrenadePrimed => grenadePrimed;

        /// <summary>Throws a grenade along a direction with the given strength (0.3 underhand .. 1 full).</summary>
        public void ThrowGrenade(GrenadeType type, Vector3 dir, float strength)
        {
            if (GrenadeCount(type) <= 0) return;
            if (type == GrenadeType.HE) heCount--; else if (type == GrenadeType.Flash) flashCount--; else smokeCount--;
            Vector3 v = dir.normalized * Mathf.Lerp(7f, 17f, Mathf.Clamp01(strength)) + Vector3.up * 2.2f + actor.velocity * 0.6f;
            ThrowWithVelocity(type, v);
        }

        /// <summary>Bots use this: throws along an exact launch velocity (consumes the grenade).</summary>
        public bool ThrowGrenadeAt(GrenadeType type, Vector3 velocity)
        {
            if (GrenadeCount(type) <= 0 || actor.InFreeze) return false;
            if (type == GrenadeType.HE) heCount--; else if (type == GrenadeType.Flash) flashCount--; else smokeCount--;
            ThrowWithVelocity(type, velocity);
            return true;
        }

        void ThrowWithVelocity(GrenadeType type, Vector3 velocity)
        {
            Vector3 start = actor.EyePosition + actor.aimDirection.Flat().normalized * 0.35f;
            Grenade.Spawn(type, start, velocity, actor);
            AudioManager.Play3D("throw", start, 0.6f, 15f);
            Hearing.Emit(start, 12f, NoiseType.Grenade, actor);
            OnMelee?.Invoke(GrenadeDef(type));
            nextFireTime = Time.time + 0.6f;
            if (!HasAnyGrenade) { if (HasSlot(WeaponSlot.Primary)) Equip(WeaponSlot.Primary); else Equip(WeaponSlot.Secondary); }
            else if (GrenadeCount(selectedGrenade) == 0) { selectedGrenade = FirstGrenade(); Equip(WeaponSlot.Grenade); }
        }

        /// <summary>On death the best gun and the charge fall to the floor for others to pick up.</summary>
        public void DropOnDeath()
        {
            Vector3 pos = actor.transform.position;
            var best = primary ?? secondary;
            if (best != null) Pickup.Drop(best, pos + actor.transform.right * 0.3f, actor);
            if (hasCharge) { hasCharge = false; Pickup.DropCharge(pos + actor.transform.forward * 0.3f); }
            primary = null;
            secondary = null;
            heCount = flashCount = smokeCount = 0;
            reloading = false;
            zoomLevel = 0;
        }

        public void DropCurrent()
        {
            var w = Current;
            if (w == null || slot == WeaponSlot.Knife) return;
            Vector3 pos = actor.transform.position + actor.aimDirection.Flat().normalized * 1.2f;
            if (slot == WeaponSlot.Primary) { primary = null; Pickup.Drop(w, pos, actor, 2f); }
            else if (slot == WeaponSlot.Secondary) { secondary = null; Pickup.Drop(w, pos, actor, 2f); }
            Equip(HasSlot(WeaponSlot.Primary) ? WeaponSlot.Primary : HasSlot(WeaponSlot.Secondary) ? WeaponSlot.Secondary : WeaponSlot.Knife);
        }

        public void DropChargeForward()
        {
            if (!hasCharge) return;
            hasCharge = false;
            Pickup.DropCharge(actor.transform.position + actor.aimDirection.Flat().normalized * 1.2f, 2f);
            if (slot == WeaponSlot.Bomb) Equip(HasSlot(WeaponSlot.Primary) ? WeaponSlot.Primary : WeaponSlot.Secondary);
        }
    }
}
