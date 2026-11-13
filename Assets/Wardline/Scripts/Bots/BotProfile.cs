using UnityEngine;

namespace Wardline
{
    /// <summary>
    /// Difficulty changes *behaviour*, never health or damage: reaction time, aim speed, aim error, recoil
    /// control, perception, hearing, cover use, grenades, teamwork, burst discipline and mistakes.
    /// Every value respects the same rules as the player (line of sight, smoke, flashbangs, spread).
    /// </summary>
    public class BotProfile
    {
        public BotDifficulty difficulty;
        public float reactionMin, reactionMax;      // seconds from first sight to first shot
        public float aimSpeed;                      // deg/s turn rate while tracking
        public float errorStart, errorSettled;      // metres of aim offset at the target
        public float settleTime;                    // time to converge from start to settled error
        public float headChance;                    // probability to aim at the head instead of the chest
        public float recoilControl;                 // 0..1 share of recoil compensated
        public float fov;                           // vision cone (degrees)
        public float perceptionInterval;            // seconds between sight checks
        public float hearingMult;                   // 0 = ignores footsteps
        public bool hearsFootsteps;
        public float coverUse;                      // chance to seek cover when reloading/hurt
        public float grenadeUse;                    // chance to use a grenade when the situation calls for it
        public float flankChance;
        public float strafeSkill;                   // 0 stands still, 1 constantly strafes and stops to shoot
        public float burstDiscipline;               // 0 sprays, 1 taps at range
        public float teamwork;                      // waits for team, rotates on information
        public float mistakeChance;                 // per-decision chance of a poor choice
        public bool predictsMovement;               // aims at where a lost target is likely to be
        public bool turnsFromFlash;
        public bool checksBeforeDefuse;

        public float Reaction() => Random.Range(reactionMin, reactionMax);

        public static BotProfile For(BotDifficulty d)
        {
            switch (d)
            {
                case BotDifficulty.Easy:
                    return new BotProfile { difficulty = d, reactionMin = 0.6f, reactionMax = 1.0f, aimSpeed = 85f, errorStart = 1.1f, errorSettled = 0.5f, settleTime = 1.4f,
                        headChance = 0.05f, recoilControl = 0.1f, fov = 100f, perceptionInterval = 0.35f, hearingMult = 0.6f, hearsFootsteps = false, coverUse = 0.05f,
                        grenadeUse = 0f, flankChance = 0f, strafeSkill = 0.05f, burstDiscipline = 0f, teamwork = 0f, mistakeChance = 0.25f };
                case BotDifficulty.Normal:
                    return new BotProfile { difficulty = d, reactionMin = 0.38f, reactionMax = 0.6f, aimSpeed = 160f, errorStart = 0.7f, errorSettled = 0.26f, settleTime = 0.9f,
                        headChance = 0.2f, recoilControl = 0.45f, fov = 115f, perceptionInterval = 0.22f, hearingMult = 0.8f, hearsFootsteps = true, coverUse = 0.3f,
                        grenadeUse = 0.2f, flankChance = 0.1f, strafeSkill = 0.4f, burstDiscipline = 0.4f, teamwork = 0.4f, mistakeChance = 0.1f };
                case BotDifficulty.Hard:
                    return new BotProfile { difficulty = d, reactionMin = 0.24f, reactionMax = 0.36f, aimSpeed = 250f, errorStart = 0.45f, errorSettled = 0.13f, settleTime = 0.55f,
                        headChance = 0.45f, recoilControl = 0.7f, fov = 125f, perceptionInterval = 0.15f, hearingMult = 1f, hearsFootsteps = true, coverUse = 0.7f,
                        grenadeUse = 0.55f, flankChance = 0.3f, strafeSkill = 0.75f, burstDiscipline = 0.75f, teamwork = 0.75f, mistakeChance = 0.03f, turnsFromFlash = true };
                default:
                    return new BotProfile { difficulty = d, reactionMin = 0.17f, reactionMax = 0.26f, aimSpeed = 340f, errorStart = 0.32f, errorSettled = 0.07f, settleTime = 0.4f,
                        headChance = 0.7f, recoilControl = 0.9f, fov = 130f, perceptionInterval = 0.1f, hearingMult = 1f, hearsFootsteps = true, coverUse = 0.9f,
                        grenadeUse = 0.85f, flankChance = 0.4f, strafeSkill = 0.95f, burstDiscipline = 0.95f, teamwork = 1f, mistakeChance = 0f,
                        predictsMovement = true, turnsFromFlash = true, checksBeforeDefuse = true };
            }
        }
    }

    /// <summary>Round-start purchasing for bots, based on money, team economy, side and difficulty.</summary>
    public static class BotBuy
    {
        public static void Buy(Actor a, BotProfile p, float teamAvgMoney, bool pistolRound, GameMode mode)
        {
            var w = a.weapons;
            bool smart = p.difficulty >= BotDifficulty.Normal;
            // Team economy: save together when the team can't afford a full buy (Normal and above).
            bool eco = smart && !pistolRound && teamAvgMoney < 3000 && a.money < 3700 && mode == GameMode.Classic;
            if (p.difficulty == BotDifficulty.Easy && Random.value < 0.3f) eco = false;   // easy bots buy erratically

            if (pistolRound)
            {
                if (smart && a.money >= 650 && Random.value < 0.6f) TryBuy(a, "kevlar");
                else if (a.money >= 600 && Random.value < 0.5f) TryBuy(a, Random.value < 0.5f ? "p9" : "viper50");
                if (p.difficulty >= BotDifficulty.Hard) TryBuy(a, Random.value < 0.5f ? "flash" : "he");
                if (a.team == Team.Wardens && p.difficulty >= BotDifficulty.Hard) TryBuy(a, "kit");
                return;
            }

            if (eco)
            {
                if (a.money > 1200 && Random.value < 0.4f) TryBuy(a, "viper50");
                return;
            }

            if (w.primary == null)
            {
                string pick = PickPrimary(a, p);
                if (pick != null) TryBuy(a, pick);
            }

            if (a.armor < 50 || !a.helmet) { if (!TryBuy(a, "kevlarhelm")) TryBuy(a, "kevlar"); }
            if (a.team == Team.Wardens && !a.defuseKit && (p.difficulty >= BotDifficulty.Hard || Random.value < 0.5f)) TryBuy(a, "kit");
            if (p.grenadeUse > 0f)
            {
                if (Random.value < p.grenadeUse) TryBuy(a, "flash");
                if (Random.value < p.grenadeUse) TryBuy(a, "he");
                if (Random.value < p.grenadeUse * 0.8f) TryBuy(a, "smoke");
                if (p.difficulty == BotDifficulty.Expert && Random.value < 0.6f) TryBuy(a, "flash");
            }
            if (w.primary == null && a.money > 700 && Random.value < 0.6f) TryBuy(a, "viper50");
        }

        static string PickPrimary(Actor a, BotProfile p)
        {
            int m = a.money - 650;   // leave room for armour
            bool raider = a.team == Team.Raiders;
            bool sniperSlotFree = !TeammateHasSniper(a);
            if (m >= 4750 && sniperSlotFree && p.difficulty >= BotDifficulty.Hard && Random.value < 0.3f) return "longbow";
            if (m >= (raider ? 2500 : 3100))
            {
                if (Random.value < 0.08f && m >= 3500) return raider ? "sg5" : "aux";
                if (Random.value < 0.06f && m >= 5750 && p.difficulty <= BotDifficulty.Normal) return "anvil";
                return raider ? "ar47" : "m7";
            }
            if (m >= (raider ? 2000 : 2250)) return raider ? "gale" : "fennec";
            if (m >= 2750 && Random.value < 0.25f) return "swift";
            if (m >= 1700 && Random.value < 0.25f) return "breacher";
            if (m >= 1500) return Random.value < 0.6f ? "mpx5" : (raider ? "hornet" : "wasp");
            if (m >= 1250) return raider ? "hornet" : "wasp";
            return null;
        }

        static bool TeammateHasSniper(Actor a)
        {
            foreach (var o in Actor.All)
                if (o != a && o.team == a.team && o.weapons.primary != null && o.weapons.primary.def.category == WeaponCategory.Sniper) return true;
            return false;
        }

        public static bool TryBuy(Actor a, string id)
        {
            var def = WeaponDatabase.Get(id);
            if (def == null || !WeaponDatabase.CanBuy(def, a.team) || a.money < def.price) return false;
            if (def.category == WeaponCategory.Grenade && !a.weapons.CanCarryGrenade(def)) return false;
            if (id == "kit" && a.defuseKit) return false;
            if (id == "kevlar" && a.armor >= 100) return false;
            if (id == "kevlarhelm" && a.armor >= 100 && a.helmet) return false;
            a.money -= def.price;
            switch (id)
            {
                case "kevlar": a.armor = 100; break;
                case "kevlarhelm": a.armor = 100; a.helmet = true; break;
                case "kit": a.defuseKit = true; break;
                default:
                    var dropped = a.weapons.Give(def);
                    if (dropped != null) Pickup.Drop(dropped, a.transform.position + a.transform.right * 0.5f, a);
                    break;
            }
            return true;
        }
    }
}
