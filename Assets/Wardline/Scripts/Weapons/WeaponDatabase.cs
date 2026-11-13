using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    public enum WeaponCategory { Pistol, SMG, Shotgun, Rifle, Sniper, Heavy, Knife, Grenade, Equipment, Bomb }
    public enum WeaponSlot { Primary = 0, Secondary = 1, Knife = 2, Grenade = 3, Bomb = 4 }
    public enum FireMode { Semi, Auto, Bolt, Pump }
    public enum GrenadeType { None, HE, Flash, Smoke }

    /// <summary>
    /// Data-only weapon definition. All weapons are original designs with functional equivalents of the classic
    /// archetypes. Tuning lives here so player and bots share identical rules.
    /// </summary>
    public class WeaponDef
    {
        public string id, name, archetype, description;
        public WeaponCategory category;
        public WeaponSlot slot;
        public FireMode fireMode = FireMode.Auto;
        public Team team = Team.None;             // None = both teams can buy
        public int price;
        public float damage;                      // per bullet/pellet before multipliers
        public float rpm;
        public int magazine, reserve;
        public float reloadTime, deployTime = 0.8f;
        public float armorPenetration = 0.75f;    // share of damage that ignores armour
        public float rangeModifier = 0.9f;        // damage multiplier per 10 m
        public float effectiveRange = 40f;
        public int pellets = 1;
        public float headMultiplier = 4f;
        // Accuracy model (degrees of cone half-angle)
        public float spreadStand = 0.4f, spreadCrouch = 0.25f, spreadMove = 3f, spreadAir = 8f, spreadPerShot = 0.35f, spreadMax = 5f;
        public float recoilUp = 1.2f, recoilSide = 0.4f, recoilMaxUp = 9f, recoilRecovery = 9f;   // degrees
        public float moveSpeed = 1f;              // multiplier on base run speed
        public float[] zoomFov;                   // null = no scope; 1-2 zoom levels
        public bool scopeOverlay;                 // true sniper scope with black mask
        public bool silenced;
        public bool unscopedInaccurate;           // snipers are wildly inaccurate unscoped
        public float killReward = 300;
        public string sound = "shot_rifle";
        public GrenadeType grenade = GrenadeType.None;
        public int maxCarry = 1;
        // Procedural model
        public Color bodyColor = new Color(0.12f, 0.12f, 0.13f), accentColor = new Color(0.35f, 0.25f, 0.15f);
        public float modelLength = 0.7f, barrelLength = 0.3f;
        public bool hasStock = true, hasScope = false, drumMag = false, boxMag = false;

        public float FireInterval => 60f / Mathf.Max(1f, rpm);
        public bool IsGun => category <= WeaponCategory.Heavy;
        public bool HasScope => zoomFov != null && zoomFov.Length > 0;
    }

    public static class WeaponDatabase
    {
        public static readonly List<WeaponDef> All = new List<WeaponDef>();
        static readonly Dictionary<string, WeaponDef> byId = new Dictionary<string, WeaponDef>();

        public static WeaponDef Get(string id) => byId.TryGetValue(id, out var w) ? w : null;

        public static IEnumerable<WeaponDef> Category(WeaponCategory c) { foreach (var w in All) if (w.category == c) yield return w; }

        public static bool CanBuy(WeaponDef w, Team t) => w.price > 0 && (w.team == Team.None || w.team == t);

        public static WeaponDef DefaultPistol(Team t) => Get(t == Team.Raiders ? "k18" : "warden45");
        public static WeaponDef Knife => Get("knife");
        public static WeaponDef Charge => Get("charge");

        static WeaponDatabase()
        {
            // ---------------- PISTOLS ----------------
            Add(new WeaponDef { id = "k18", name = "K-18", archetype = "Polymer 9mm pistol", category = WeaponCategory.Pistol, slot = WeaponSlot.Secondary, fireMode = FireMode.Semi,
                team = Team.Raiders, price = 200, damage = 25, rpm = 400, magazine = 20, reserve = 120, reloadTime = 2.2f, armorPenetration = 0.47f, rangeModifier = 0.75f, effectiveRange = 20,
                spreadStand = 0.55f, spreadCrouch = 0.4f, spreadMove = 1.8f, spreadAir = 6f, spreadPerShot = 0.55f, spreadMax = 4f, recoilUp = 1.4f, recoilSide = 0.4f, recoilRecovery = 14f,
                sound = "shot_pistol", modelLength = 0.2f, barrelLength = 0.04f, hasStock = false, description = "Standard-issue Raider sidearm. Large magazine, weak against armour." });
            Add(new WeaponDef { id = "warden45", name = "Warden .45 S", archetype = "Suppressed .45 pistol", category = WeaponCategory.Pistol, slot = WeaponSlot.Secondary, fireMode = FireMode.Semi,
                team = Team.Wardens, price = 200, damage = 34, rpm = 352, magazine = 12, reserve = 100, reloadTime = 2.7f, armorPenetration = 0.5f, rangeModifier = 0.79f, effectiveRange = 25, silenced = true,
                spreadStand = 0.45f, spreadCrouch = 0.3f, spreadMove = 1.6f, spreadAir = 6f, spreadPerShot = 0.6f, spreadMax = 4f, recoilUp = 1.6f, recoilSide = 0.35f, recoilRecovery = 13f,
                sound = "shot_silenced", modelLength = 0.26f, barrelLength = 0.12f, hasStock = false, description = "Warden sidearm with an integrated suppressor. Quiet and precise." });
            Add(new WeaponDef { id = "p9", name = "P-9 Compact", archetype = "Compact double-action pistol", category = WeaponCategory.Pistol, slot = WeaponSlot.Secondary, fireMode = FireMode.Semi,
                price = 600, damage = 32, rpm = 400, magazine = 13, reserve = 52, reloadTime = 2.7f, armorPenetration = 0.62f, rangeModifier = 0.8f, effectiveRange = 25,
                spreadStand = 0.45f, spreadCrouch = 0.32f, spreadMove = 1.7f, spreadAir = 6f, spreadPerShot = 0.6f, spreadMax = 4f, recoilUp = 1.8f, recoilSide = 0.45f, recoilRecovery = 13f,
                sound = "shot_pistol", modelLength = 0.21f, barrelLength = 0.05f, hasStock = false, bodyColor = new Color(0.25f, 0.25f, 0.27f), description = "Better armour penetration than the starting pistols." });
            Add(new WeaponDef { id = "viper50", name = "Viper .50", archetype = "Heavy magnum pistol", category = WeaponCategory.Pistol, slot = WeaponSlot.Secondary, fireMode = FireMode.Semi,
                price = 650, damage = 54, rpm = 267, magazine = 7, reserve = 35, reloadTime = 2.2f, armorPenetration = 0.93f, rangeModifier = 0.81f, effectiveRange = 35,
                spreadStand = 0.5f, spreadCrouch = 0.35f, spreadMove = 2.4f, spreadAir = 8f, spreadPerShot = 1.4f, spreadMax = 5f, recoilUp = 3.4f, recoilSide = 0.8f, recoilRecovery = 10f, moveSpeed = 0.98f,
                sound = "shot_heavy_pistol", modelLength = 0.28f, barrelLength = 0.1f, hasStock = false, bodyColor = new Color(0.55f, 0.56f, 0.58f), description = "Hand cannon. One headshot kills through a helmet." });
            Add(new WeaponDef { id = "twin9", name = "Twin 9s", archetype = "Dual pistols", category = WeaponCategory.Pistol, slot = WeaponSlot.Secondary, fireMode = FireMode.Semi,
                team = Team.Raiders, price = 800, damage = 36, rpm = 500, magazine = 30, reserve = 120, reloadTime = 4.5f, armorPenetration = 0.57f, rangeModifier = 0.75f, effectiveRange = 22,
                spreadStand = 0.7f, spreadCrouch = 0.55f, spreadMove = 2.2f, spreadAir = 7f, spreadPerShot = 0.65f, spreadMax = 5f, recoilUp = 1.5f, recoilSide = 0.6f, recoilRecovery = 12f,
                sound = "shot_pistol", modelLength = 0.2f, barrelLength = 0.05f, hasStock = false, bodyColor = new Color(0.6f, 0.6f, 0.62f), description = "A pistol in each hand. Fast follow-up shots, long reload." });

            // ---------------- SMGs ----------------
            Add(new WeaponDef { id = "mpx5", name = "MPX-5", archetype = "9mm submachine gun", category = WeaponCategory.SMG, slot = WeaponSlot.Primary, price = 1500, damage = 26, rpm = 750,
                magazine = 30, reserve = 120, reloadTime = 2.6f, armorPenetration = 0.5f, rangeModifier = 0.84f, effectiveRange = 25, killReward = 600,
                spreadStand = 0.5f, spreadCrouch = 0.35f, spreadMove = 1.6f, spreadAir = 6f, spreadPerShot = 0.18f, spreadMax = 3.2f, recoilUp = 0.55f, recoilSide = 0.3f, recoilMaxUp = 6f, moveSpeed = 1f,
                sound = "shot_smg", modelLength = 0.5f, barrelLength = 0.1f, description = "Accurate, easy-to-control SMG. Good kill reward." });
            Add(new WeaponDef { id = "hornet", name = "Hornet", archetype = "Machine pistol", category = WeaponCategory.SMG, slot = WeaponSlot.Primary, team = Team.Raiders, price = 1400, damage = 29, rpm = 857,
                magazine = 30, reserve = 100, reloadTime = 3.1f, armorPenetration = 0.47f, rangeModifier = 0.82f, effectiveRange = 18, killReward = 600,
                spreadStand = 0.9f, spreadCrouch = 0.7f, spreadMove = 1.8f, spreadAir = 6f, spreadPerShot = 0.28f, spreadMax = 4.5f, recoilUp = 0.8f, recoilSide = 0.55f, recoilMaxUp = 8f,
                sound = "shot_smg", modelLength = 0.34f, barrelLength = 0.05f, hasStock = false, boxMag = true, description = "Blistering fire rate, poor accuracy. Up-close only." });
            Add(new WeaponDef { id = "wasp", name = "Wasp", archetype = "Suppressed machine pistol", category = WeaponCategory.SMG, slot = WeaponSlot.Primary, team = Team.Wardens, price = 1250, damage = 26, rpm = 857,
                magazine = 30, reserve = 120, reloadTime = 2.1f, armorPenetration = 0.48f, rangeModifier = 0.84f, effectiveRange = 20, killReward = 600, silenced = true,
                spreadStand = 0.7f, spreadCrouch = 0.5f, spreadMove = 1.6f, spreadAir = 6f, spreadPerShot = 0.22f, spreadMax = 4f, recoilUp = 0.65f, recoilSide = 0.4f, recoilMaxUp = 7f,
                sound = "shot_silenced", modelLength = 0.38f, barrelLength = 0.16f, hasStock = false, description = "Quiet close-quarters SMG for Wardens." });
            Add(new WeaponDef { id = "vectorp", name = "Vector-P", archetype = "Bullpup PDW", category = WeaponCategory.SMG, slot = WeaponSlot.Primary, price = 2350, damage = 26, rpm = 857,
                magazine = 50, reserve = 100, reloadTime = 3.3f, armorPenetration = 0.69f, rangeModifier = 0.84f, effectiveRange = 28, killReward = 300,
                spreadStand = 0.55f, spreadCrouch = 0.4f, spreadMove = 1.4f, spreadAir = 6f, spreadPerShot = 0.17f, spreadMax = 3.6f, recoilUp = 0.55f, recoilSide = 0.35f, recoilMaxUp = 6.5f, moveSpeed = 0.98f,
                sound = "shot_smg", modelLength = 0.5f, barrelLength = 0.06f, boxMag = true, bodyColor = new Color(0.2f, 0.22f, 0.2f), description = "50-round PDW that pierces armour well. Run and gun." });

            // ---------------- SHOTGUNS ----------------
            Add(new WeaponDef { id = "breacher", name = "Breacher 12", archetype = "Pump-action shotgun", category = WeaponCategory.Shotgun, slot = WeaponSlot.Primary, fireMode = FireMode.Pump, price = 1700,
                damage = 20, pellets = 9, rpm = 68, magazine = 8, reserve = 32, reloadTime = 3.6f, armorPenetration = 0.5f, rangeModifier = 0.6f, effectiveRange = 12, killReward = 900, headMultiplier = 2.5f,
                spreadStand = 3.6f, spreadCrouch = 3.4f, spreadMove = 4.2f, spreadAir = 6f, spreadPerShot = 0f, spreadMax = 4.2f, recoilUp = 4.5f, recoilSide = 1f, recoilRecovery = 9f, moveSpeed = 0.94f,
                sound = "shot_shotgun", modelLength = 0.75f, barrelLength = 0.3f, accentColor = new Color(0.3f, 0.2f, 0.12f), description = "Devastating at door distance. Reloads shell by shell." });
            Add(new WeaponDef { id = "auto12", name = "Auto-12", archetype = "Semi-automatic shotgun", category = WeaponCategory.Shotgun, slot = WeaponSlot.Primary, fireMode = FireMode.Semi, price = 3000,
                damage = 22, pellets = 6, rpm = 240, magazine = 7, reserve = 32, reloadTime = 3.2f, armorPenetration = 0.6f, rangeModifier = 0.6f, effectiveRange = 14, killReward = 900, headMultiplier = 2.5f,
                spreadStand = 3.2f, spreadCrouch = 3f, spreadMove = 4f, spreadAir = 6f, spreadPerShot = 0.2f, spreadMax = 4.5f, recoilUp = 3.2f, recoilSide = 1f, recoilRecovery = 9f, moveSpeed = 0.94f,
                sound = "shot_shotgun", modelLength = 0.8f, barrelLength = 0.25f, description = "Fast follow-up shotgun blasts." });

            // ---------------- RIFLES ----------------
            Add(new WeaponDef { id = "ar47", name = "AR-47 Kestrel", archetype = "7.62 assault rifle", category = WeaponCategory.Rifle, slot = WeaponSlot.Primary, team = Team.Raiders, price = 2500,
                damage = 36, rpm = 600, magazine = 30, reserve = 90, reloadTime = 2.5f, armorPenetration = 0.775f, rangeModifier = 0.98f, effectiveRange = 70,
                spreadStand = 0.22f, spreadCrouch = 0.15f, spreadMove = 4.2f, spreadAir = 9f, spreadPerShot = 0.34f, spreadMax = 4.2f, recoilUp = 1.25f, recoilSide = 0.55f, recoilMaxUp = 10f, moveSpeed = 0.92f,
                sound = "shot_rifle", modelLength = 0.9f, barrelLength = 0.25f, accentColor = new Color(0.45f, 0.28f, 0.14f), description = "Kills with a single headshot through a helmet. Hard recoil." });
            Add(new WeaponDef { id = "m7", name = "M-7 Lancer", archetype = "5.56 carbine", category = WeaponCategory.Rifle, slot = WeaponSlot.Primary, team = Team.Wardens, price = 3100,
                damage = 33, rpm = 666, magazine = 30, reserve = 90, reloadTime = 3.1f, armorPenetration = 0.7f, rangeModifier = 0.97f, effectiveRange = 65,
                spreadStand = 0.18f, spreadCrouch = 0.12f, spreadMove = 3.8f, spreadAir = 9f, spreadPerShot = 0.28f, spreadMax = 3.8f, recoilUp = 1.0f, recoilSide = 0.4f, recoilMaxUp = 8.5f, moveSpeed = 0.92f,
                sound = "shot_rifle2", modelLength = 0.85f, barrelLength = 0.25f, bodyColor = new Color(0.14f, 0.14f, 0.15f), accentColor = new Color(0.2f, 0.2f, 0.21f), description = "Controllable all-rounder. The Warden's workhorse." });
            Add(new WeaponDef { id = "gale", name = "Gale", archetype = "Budget assault rifle", category = WeaponCategory.Rifle, slot = WeaponSlot.Primary, team = Team.Raiders, price = 2000,
                damage = 30, rpm = 666, magazine = 35, reserve = 90, reloadTime = 3.0f, armorPenetration = 0.775f, rangeModifier = 0.98f, effectiveRange = 55,
                spreadStand = 0.3f, spreadCrouch = 0.22f, spreadMove = 4f, spreadAir = 9f, spreadPerShot = 0.33f, spreadMax = 4.4f, recoilUp = 1.1f, recoilSide = 0.55f, recoilMaxUp = 9f, moveSpeed = 0.95f,
                sound = "shot_rifle", modelLength = 0.85f, barrelLength = 0.2f, accentColor = new Color(0.25f, 0.25f, 0.2f), description = "Cheaper Raider rifle with a big magazine." });
            Add(new WeaponDef { id = "fennec", name = "Fennec", archetype = "Bullpup burst rifle", category = WeaponCategory.Rifle, slot = WeaponSlot.Primary, team = Team.Wardens, price = 2250,
                damage = 30, rpm = 666, magazine = 25, reserve = 90, reloadTime = 3.3f, armorPenetration = 0.7f, rangeModifier = 0.96f, effectiveRange = 55,
                spreadStand = 0.28f, spreadCrouch = 0.2f, spreadMove = 3.9f, spreadAir = 9f, spreadPerShot = 0.32f, spreadMax = 4.2f, recoilUp = 1.05f, recoilSide = 0.45f, recoilMaxUp = 8.5f, moveSpeed = 0.94f,
                sound = "shot_rifle2", modelLength = 0.72f, barrelLength = 0.15f, bodyColor = new Color(0.3f, 0.3f, 0.28f), description = "Affordable Warden bullpup rifle." });
            Add(new WeaponDef { id = "sg5", name = "SG-5 Optic", archetype = "Scoped assault rifle", category = WeaponCategory.Rifle, slot = WeaponSlot.Primary, team = Team.Raiders, price = 3500,
                damage = 33, rpm = 666, magazine = 30, reserve = 90, reloadTime = 3.0f, armorPenetration = 0.8f, rangeModifier = 0.955f, effectiveRange = 80, zoomFov = new[] { 45f },
                spreadStand = 0.2f, spreadCrouch = 0.13f, spreadMove = 4f, spreadAir = 9f, spreadPerShot = 0.3f, spreadMax = 4f, recoilUp = 1.05f, recoilSide = 0.45f, recoilMaxUp = 9f, moveSpeed = 0.9f,
                sound = "shot_rifle", modelLength = 0.9f, barrelLength = 0.2f, hasScope = true, bodyColor = new Color(0.25f, 0.24f, 0.2f), description = "Assault rifle with a 2x optic for long lanes." });
            Add(new WeaponDef { id = "aux", name = "AUX Optic", archetype = "Scoped bullpup rifle", category = WeaponCategory.Rifle, slot = WeaponSlot.Primary, team = Team.Wardens, price = 3500,
                damage = 32, rpm = 666, magazine = 30, reserve = 90, reloadTime = 3.3f, armorPenetration = 0.7f, rangeModifier = 0.96f, effectiveRange = 80, zoomFov = new[] { 45f },
                spreadStand = 0.18f, spreadCrouch = 0.12f, spreadMove = 3.8f, spreadAir = 9f, spreadPerShot = 0.28f, spreadMax = 3.8f, recoilUp = 0.95f, recoilSide = 0.4f, recoilMaxUp = 8.5f, moveSpeed = 0.9f,
                sound = "shot_rifle2", modelLength = 0.75f, barrelLength = 0.15f, hasScope = true, bodyColor = new Color(0.32f, 0.36f, 0.3f), description = "Warden bullpup with an integrated 2x optic." });

            // ---------------- SNIPERS ----------------
            Add(new WeaponDef { id = "longbow", name = "Longbow .338", archetype = "Bolt-action magnum sniper", category = WeaponCategory.Sniper, slot = WeaponSlot.Primary, fireMode = FireMode.Bolt, price = 4750,
                damage = 115, rpm = 41, magazine = 10, reserve = 30, reloadTime = 3.6f, armorPenetration = 0.97f, rangeModifier = 0.99f, effectiveRange = 200, zoomFov = new[] { 40f, 12f }, scopeOverlay = true, unscopedInaccurate = true, headMultiplier = 4f, killReward = 100,
                spreadStand = 0.05f, spreadCrouch = 0.03f, spreadMove = 6f, spreadAir = 12f, spreadPerShot = 0f, spreadMax = 6f, recoilUp = 4f, recoilSide = 0.5f, recoilRecovery = 7f, moveSpeed = 0.84f, deployTime = 1.2f,
                sound = "shot_sniper", modelLength = 1.15f, barrelLength = 0.45f, hasScope = true, bodyColor = new Color(0.2f, 0.27f, 0.2f), description = "One body shot kills. Slow, heavy and loud." });
            Add(new WeaponDef { id = "swift", name = "Swift", archetype = "Lightweight bolt-action rifle", category = WeaponCategory.Sniper, slot = WeaponSlot.Primary, fireMode = FireMode.Bolt, price = 2750,
                damage = 75, rpm = 48, magazine = 10, reserve = 90, reloadTime = 2.0f, armorPenetration = 0.85f, rangeModifier = 0.98f, effectiveRange = 150, zoomFov = new[] { 40f, 15f }, scopeOverlay = true, unscopedInaccurate = true, headMultiplier = 4f,
                spreadStand = 0.06f, spreadCrouch = 0.04f, spreadMove = 3.5f, spreadAir = 3f, spreadPerShot = 0f, spreadMax = 3.5f, recoilUp = 2.5f, recoilSide = 0.4f, recoilRecovery = 9f, moveSpeed = 1f,
                sound = "shot_sniper", modelLength = 1f, barrelLength = 0.4f, hasScope = true, bodyColor = new Color(0.18f, 0.18f, 0.2f), description = "Fast and accurate even mid-jump. Headshots kill." });
            Add(new WeaponDef { id = "marksman", name = "Marksman SR", archetype = "Semi-automatic sniper", category = WeaponCategory.Sniper, slot = WeaponSlot.Primary, fireMode = FireMode.Semi, price = 4200,
                damage = 70, rpm = 240, magazine = 20, reserve = 90, reloadTime = 3.8f, armorPenetration = 0.83f, rangeModifier = 0.98f, effectiveRange = 150, zoomFov = new[] { 40f, 15f }, scopeOverlay = true, unscopedInaccurate = true,
                spreadStand = 0.1f, spreadCrouch = 0.06f, spreadMove = 5f, spreadAir = 10f, spreadPerShot = 0.4f, spreadMax = 3f, recoilUp = 1.8f, recoilSide = 0.5f, recoilRecovery = 8f, moveSpeed = 0.84f, deployTime = 1.1f,
                sound = "shot_sniper", modelLength = 1.1f, barrelLength = 0.35f, hasScope = true, bodyColor = new Color(0.32f, 0.3f, 0.22f), description = "Semi-auto sniper. Expensive but punishing." });

            // ---------------- HEAVY ----------------
            Add(new WeaponDef { id = "anvil", name = "Anvil LMG", archetype = "Belt-fed machine gun", category = WeaponCategory.Heavy, slot = WeaponSlot.Primary, price = 5750,
                damage = 32, rpm = 750, magazine = 100, reserve = 200, reloadTime = 4.7f, armorPenetration = 0.8f, rangeModifier = 0.97f, effectiveRange = 55,
                spreadStand = 0.5f, spreadCrouch = 0.35f, spreadMove = 5f, spreadAir = 10f, spreadPerShot = 0.15f, spreadMax = 4.5f, recoilUp = 0.75f, recoilSide = 0.55f, recoilMaxUp = 7f, moveSpeed = 0.8f, deployTime = 1.3f,
                sound = "shot_lmg", modelLength = 1f, barrelLength = 0.3f, drumMag = true, description = "100-round suppressive fire. Slow to move and reload." });

            // ---------------- MELEE / UTILITY ----------------
            Add(new WeaponDef { id = "knife", name = "Combat Knife", archetype = "Fixed blade", category = WeaponCategory.Knife, slot = WeaponSlot.Knife, fireMode = FireMode.Semi,
                damage = 22, rpm = 150, magazine = 0, reserve = 0, armorPenetration = 0.85f, effectiveRange = 1.8f, killReward = 1500, headMultiplier = 1.5f, moveSpeed = 1.04f, deployTime = 0.5f,
                sound = "knife", modelLength = 0.28f, description = "Fire to slash, Aim to stab (65 damage, slow). Fastest movement." });
            Add(new WeaponDef { id = "he", name = "Frag Grenade", archetype = "HE grenade", category = WeaponCategory.Grenade, slot = WeaponSlot.Grenade, grenade = GrenadeType.HE, price = 300, damage = 98, maxCarry = 1, deployTime = 0.6f,
                description = "Explodes 1.6 s after landing. Up to 98 damage." });
            Add(new WeaponDef { id = "flash", name = "Flash Charge", archetype = "Flashbang", category = WeaponCategory.Grenade, slot = WeaponSlot.Grenade, grenade = GrenadeType.Flash, price = 200, maxCarry = 2, deployTime = 0.6f,
                description = "Blinds anyone facing the burst. Turn away to resist." });
            Add(new WeaponDef { id = "smoke", name = "Smoke Canister", archetype = "Smoke grenade", category = WeaponCategory.Grenade, slot = WeaponSlot.Grenade, grenade = GrenadeType.Smoke, price = 300, maxCarry = 1, deployTime = 0.6f,
                description = "Blocks vision for 16 seconds. Bots cannot see through it either." });
            Add(new WeaponDef { id = "kevlar", name = "Kevlar Vest", archetype = "Body armour", category = WeaponCategory.Equipment, price = 650, description = "100 armour. Protects chest, stomach and arms." });
            Add(new WeaponDef { id = "kevlarhelm", name = "Vest + Helmet", archetype = "Body armour with helmet", category = WeaponCategory.Equipment, price = 1000, description = "100 armour and a helmet that reduces headshot damage." });
            Add(new WeaponDef { id = "kit", name = "Defuse Kit", archetype = "Defusal tools", category = WeaponCategory.Equipment, team = Team.Wardens, price = 200, description = "Halves defuse time from 10 s to 5 s." });
            Add(new WeaponDef { id = "charge", name = "Breach Charge", archetype = "Bomb", category = WeaponCategory.Bomb, slot = WeaponSlot.Bomb, team = Team.Raiders, deployTime = 0.6f,
                description = "Plant at site A or B. Detonates after the charge timer." });
        }

        static void Add(WeaponDef w) { All.Add(w); byId[w.id] = w; }
    }
}
