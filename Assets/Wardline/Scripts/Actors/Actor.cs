using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    public struct DamageInfo
    {
        public float amount;
        public Actor attacker;
        public WeaponDef weapon;
        public HitZone zone;
        public Vector3 point, direction, sourcePosition;
        public bool explosive;
    }

    public class Hitbox : MonoBehaviour
    {
        public Actor actor;
        public HitZone zone;
    }

    /// <summary>
    /// Shared state for every combatant (player or bot): team, health, armour, money, inventory, movement
    /// state used by the accuracy model, and scoreboard counters.
    /// </summary>
    public class Actor : MonoBehaviour
    {
        public static readonly List<Actor> All = new List<Actor>();

        public const float RunSpeed = 5.6f;
        public const float WalkFactor = 0.52f;
        public const float CrouchFactor = 0.36f;
        public const float StandEye = 1.62f, CrouchEye = 1.12f;

        public string displayName;
        public Team team;
        public bool isPlayer;
        public float health = 100f, armor;
        public bool helmet, defuseKit;
        public int money;
        public bool alive = true;

        // Scoreboard
        public int kills, deaths, headshots, score, roundKills, killStreak, bestStreak;

        public WeaponHandler weapons;
        public CharacterModel model;
        public BotBrain bot;
        public PlayerController player;

        // Movement state (written by the controller every frame, read by the weapon accuracy model)
        public Vector3 velocity;
        public bool crouching, grounded = true, walking;
        public Vector3 aimDirection = Vector3.forward;
        public float yaw, pitch;

        public float blindUntil, blindDuration;
        public float spawnProtectedUntil;
        public float lastHurtTime = -10f;
        public Actor lastAttacker;
        public Vector3 lastHurtFrom;
        public float lastFireTime = -10f;
        public float frozenUntil;                // freeze time: may look and buy but not move or fire
        public bool InFreeze => Time.time < frozenUntil;

        public float EyeHeight => crouching ? CrouchEye : StandEye;
        public Vector3 EyePosition => transform.position + Vector3.up * EyeHeight;
        public Vector3 ChestPosition => transform.position + Vector3.up * (crouching ? 0.85f : 1.3f);
        public Vector3 HeadPosition => model && model.head ? model.head.position : EyePosition;
        public float HorizontalSpeed => new Vector2(velocity.x, velocity.z).magnitude;
        public float MaxSpeed => RunSpeed * (weapons != null && weapons.CurrentDef != null ? weapons.CurrentDef.moveSpeed : 1f) * (weapons != null && weapons.zoomLevel > 0 ? 0.75f : 1f);
        public float BlindAmount => Time.time >= blindUntil ? 0f : Mathf.Clamp01((blindUntil - Time.time) / Mathf.Max(0.5f, blindDuration * 0.6f));
        public bool IsBlind => BlindAmount > 0.35f;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static int CountAlive(Team t)
        {
            int n = 0;
            foreach (var a in All) if (a.alive && a.team == t) n++;
            return n;
        }

        public void SetTeam(Team t)
        {
            team = t;
            if (model) model.ApplyTeam(t);
        }

        /// <summary>Prepare for a new round / respawn. Survivors keep their equipment.</summary>
        public void Respawn(Vector3 pos, float facingYaw, bool keepInventory)
        {
            alive = true;
            health = 100f;
            blindUntil = 0f;
            velocity = Vector3.zero;
            crouching = false;
            lastAttacker = null;
            roundKills = 0;
            if (!keepInventory)
            {
                armor = 0f; helmet = false; defuseKit = false;
                weapons.ResetToDefaults();
            }
            else weapons.RefillForNewRound();

            yaw = facingYaw; pitch = 0f;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, facingYaw, 0));
            aimDirection = transform.forward;
            if (model) { model.ResetPose(); model.SetHitboxesActive(true); }
            if (player) player.OnRespawn(pos, facingYaw);
            if (bot) bot.OnRespawn(pos);
            gameObject.SetActive(true);
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (!alive) return;
            if (Time.time < spawnProtectedUntil) return;

            float dmg = info.amount;
            if (!info.explosive)
            {
                float head = info.weapon != null ? info.weapon.headMultiplier : 4f;
                switch (info.zone)
                {
                    case HitZone.Head: dmg *= head; break;
                    case HitZone.Stomach: dmg *= 1.25f; break;
                    case HitZone.Leg: dmg *= 0.75f; break;
                }
            }

            bool armored = armor > 0f && info.zone != HitZone.Leg && (info.zone != HitZone.Head || helmet);
            if (armored)
            {
                float pen = info.explosive ? 0.5f : info.weapon != null ? info.weapon.armorPenetration : 0.5f;
                float healthDmg = dmg * pen;
                float armorDmg = (dmg - healthDmg) * 0.5f;
                if (armorDmg > armor) { healthDmg += (armorDmg - armor) * 2f; armorDmg = armor; }
                armor = Mathf.Max(0f, armor - armorDmg);
                dmg = healthDmg;
                if (info.zone == HitZone.Head && helmet) AudioManager.Play3D("armor_hit", info.point, 0.8f, 25f);
            }

            dmg = Mathf.Max(1f, Mathf.Round(dmg));
            health -= dmg;
            lastHurtTime = Time.time;
            lastHurtFrom = info.sourcePosition;
            if (info.attacker != null && info.attacker != this) lastAttacker = info.attacker;

            GameEvents.RaiseDamaged(info.attacker, this, dmg, info.sourcePosition);
            if (bot) bot.OnDamaged(info);

            if (health <= 0f)
            {
                health = 0f;
                Die(info.attacker, info.weapon, info.zone == HitZone.Head && !info.explosive, info.direction);
            }
            else if (isPlayer) AudioManager.Play2D("hurt", 0.6f);
        }

        public void Die(Actor killer, WeaponDef weapon, bool headshot, Vector3 hitDir)
        {
            if (!alive) return;
            alive = false;
            deaths++;
            killStreak = 0;
            AudioManager.Play3D("death", ChestPosition, 0.9f, 30f);
            if (headshot) AudioManager.Play3D("headshot", HeadPosition, 1f, 25f);
            if (model) { model.SetHitboxesActive(false); model.PlayDeath(hitDir); }
            weapons.DropOnDeath();
            if (killer != null && killer != this)
            {
                if (killer.team != team || MatchManager.I == null || MatchManager.I.Config.mode == GameMode.Practice)
                {
                    killer.kills++; killer.roundKills++; killer.killStreak++;
                    killer.bestStreak = Mathf.Max(killer.bestStreak, killer.killStreak);
                    if (headshot) killer.headshots++;
                }
            }
            GameEvents.RaiseKill(killer, this, weapon, headshot);
        }

        public void Blind(float duration)
        {
            float until = Time.time + duration;
            if (until > blindUntil) { blindUntil = until; blindDuration = duration; }
        }
    }
}
