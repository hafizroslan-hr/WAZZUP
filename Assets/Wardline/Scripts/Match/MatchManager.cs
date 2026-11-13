using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Wardline
{
    public enum MatchPhase { Freeze, Live, RoundOver, HalfTime, MatchOver }

    public class KillFeedEntry
    {
        public string killer, victim, weapon;
        public Team killerTeam, victimTeam;
        public bool headshot, involvesPlayer;
        public float time;
    }

    /// <summary>The planted charge: blinking light, accelerating beeps, explosion.</summary>
    public class ChargeObject : MonoBehaviour
    {
        Light blink;
        Renderer led;
        float nextBeep;

        public static ChargeObject Create(Vector3 pos, float yaw)
        {
            var go = new GameObject("PlantedCharge");
            go.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.Euler(0, yaw, 0));
            WeaponModels.Build(WeaponDatabase.Charge, go.transform, 1.6f);
            var c = go.AddComponent<ChargeObject>();
            var lg = new GameObject("Blink"); lg.transform.SetParent(go.transform, false); lg.transform.localPosition = Vector3.up * 0.3f;
            c.blink = lg.AddComponent<Light>(); c.blink.type = LightType.Point; c.blink.color = new Color(1f, 0.2f, 0.1f); c.blink.range = 4f; c.blink.intensity = 0f; c.blink.shadows = LightShadows.None;
            c.led = MeshUtil.Visual(PrimitiveType.Sphere, go.transform, new Vector3(0.05f, 0.12f, 0.03f), Vector3.one * 0.04f, MaterialLib.Unlit(new Color(1f, 0.15f, 0.1f))).GetComponent<Renderer>();
            return c;
        }

        public void Tick(float timeLeft, float total)
        {
            float k = 1f - Mathf.Clamp01(timeLeft / total);
            float interval = Mathf.Lerp(1.1f, 0.12f, k * k);
            if (Time.time >= nextBeep)
            {
                nextBeep = Time.time + interval;
                AudioManager.Play3D("beep", transform.position, 0.8f, 45f);
                Hearing.Emit(transform.position, 25f, NoiseType.Bomb, null);
                blink.intensity = 3f;
                led.enabled = true;
            }
            blink.intensity = Mathf.MoveTowards(blink.intensity, 0f, Time.deltaTime * 20f);
            led.enabled = blink.intensity > 0.5f;
        }
    }

    /// <summary>
    /// Rules engine for all modes: spawning, freeze/buy time, round timer, charge plant/defuse/detonation,
    /// win conditions, economy, half-time side switch, team deathmatch respawns, practice range and stats.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager I { get; private set; }

        public MatchConfig Config { get; private set; }
        public MapRuntime Map { get; private set; }
        public Actor Player { get; private set; }
        public MatchPhase Phase { get; private set; }
        public Team LastWinner { get; private set; }
        public string LastRoundReason { get; private set; }

        public int raiderScore, wardenScore, roundNumber;
        public readonly List<KillFeedEntry> killFeed = new List<KillFeedEntry>();
        readonly List<BotBrain> bots = new List<BotBrain>();
        TeamCoordinator raiderCoord, wardenCoord;
        readonly Dictionary<Team, int> lossStreak = new Dictionary<Team, int> { { Team.Raiders, 0 }, { Team.Wardens, 0 } };

        float phaseEnd, roundEndTime, buyEnd, matchStart;
        // Charge state
        public bool ChargePlanted { get; private set; }
        public Vector3 ChargePosition { get; private set; }
        public int ChargeSite { get; private set; } = -1;
        public Pickup DroppedCharge { get; private set; }
        ChargeObject chargeObj;
        float chargeExplodeAt;
        Actor planter;
        public Actor PlantingActor { get; private set; }
        public Actor DefusingActor { get; private set; }
        public float PlantProgress { get; private set; }
        public float DefuseProgress { get; private set; }
        float lastPlantKey;
        bool raidersPlantedThisRound;

        // Mode-specific
        readonly Dictionary<Actor, float> respawnAt = new Dictionary<Actor, float>();
        readonly Dictionary<Actor, float> spawnTime = new Dictionary<Actor, float>();
        public int practiceKills, practiceHeadshots, practiceShots;
        readonly List<string> playerLoadout = new List<string>();
        int playerShotsAtStart;
        string favouriteThisMatch;

        public float RoundTimeLeft => Mathf.Max(0f, roundEndTime - Time.time);
        public float ChargeTimeLeft => ChargePlanted ? Mathf.Max(0f, chargeExplodeAt - Time.time) : 0f;
        public float PhaseTimeLeft => Mathf.Max(0f, phaseEnd - Time.time);
        public float MatchTimeLeft => Config.mode == GameMode.TeamDeathmatch ? Mathf.Max(0f, matchStart + Config.tdmMinutes * 60f - Time.time) : 0f;
        public int HalfRounds => Mathf.Max(1, Config.maxRounds / 2);
        public int RoundsToWin => Config.maxRounds / 2 + 1;
        public bool IsPlantingOrDefusing(Actor a) => a == PlantingActor || a == DefusingActor;

        // ------------------------------------------------------------ Setup
        public static MatchManager Begin(MatchConfig cfg)
        {
            var go = new GameObject("Match");
            I = go.AddComponent<MatchManager>();
            I.Setup(cfg);
            return I;
        }

        void Setup(MatchConfig cfg)
        {
            Config = cfg.Clone();
            Hearing.Clear();
            Effects.ClearAll();
            var defs = MapLibrary.All;
            Map = MapBuilder.Build(defs[Mathf.Clamp(Config.mapIndex, 0, defs.Count - 1)]);
            AudioManager.PlayMusic(null);
            AudioManager.PlayAmbient(Map.def.Theme.ambience);

            // Teams
            Team playerTeam = Config.teamChoice == 0 ? Team.Raiders : Config.teamChoice == 1 ? Team.Wardens : (Random.value < 0.5f ? Team.Raiders : Team.Wardens);
            if (Config.mode == GameMode.Practice) playerTeam = Team.Raiders;
            int total = Mathf.Clamp(Config.botCount, 1, 15);
            int mates, foes;
            if (Config.mode == GameMode.Practice) { mates = 0; foes = Mathf.Clamp(Config.botCount, 1, 10); }
            else if (Config.autoBalance) { int players = total + 1; mates = players / 2 - 1; foes = players - players / 2; if (Random.value < 0.5f && players % 2 == 1) { mates++; foes--; } mates = Mathf.Max(0, mates); foes = Mathf.Max(1, foes); }
            else { mates = Mathf.Clamp(Config.teammateBots, 0, total - 1); foes = Mathf.Max(1, total - mates); }

            Player = CreateActor("You", playerTeam, true);
            var names = new List<string>(BotNames);
            MathUtil.Shuffle(names);
            int ni = 0;
            var profile = BotProfile.For(Config.difficulty);
            for (int i = 0; i < mates; i++) CreateBot(names[ni++ % names.Count], playerTeam, profile);
            for (int i = 0; i < foes; i++) CreateBot(names[ni++ % names.Count], playerTeam.Opposite(), profile);

            raiderCoord = new TeamCoordinator(Team.Raiders, Config.difficulty, Map);
            wardenCoord = new TeamCoordinator(Team.Wardens, Config.difficulty, Map);
            foreach (var b in bots) b.coord = b.actor.team == Team.Raiders ? raiderCoord : wardenCoord;

            foreach (var a in Actor.All) a.money = Config.mode == GameMode.Classic ? Config.startMoney : 16000;
            if (Config.mode == GameMode.Practice)
            {
                Player.weapons.unlimitedAmmo = Config.unlimitedAmmo;
                foreach (var b in bots) { b.practice = true; b.practiceMode = Config.practiceBots; }
            }

            GameEvents.Kill += OnKill;
            matchStart = Time.time;
            playerShotsAtStart = 0;
            PlayerHitFeedback.shotsHit = 0;
            roundNumber = 0;
            StartRound(true);
        }

        static readonly string[] BotNames = { "Hawk", "Viper", "Nomad", "Ghost", "Rook", "Atlas", "Kestrel", "Brick", "Echo", "Jinx", "Flint", "Mako", "Onyx", "Quill", "Raze", "Sable", "Talon", "Vex", "Wren", "Zulu", "Boone", "Crane", "Dune", "Fable", "Grit", "Halo", "Ivo", "Kilo" };

        Actor CreateActor(string name, Team team, bool isPlayer)
        {
            var go = new GameObject(isPlayer ? "Player" : "Bot_" + name);
            go.layer = Layers.Actor;
            var a = go.AddComponent<Actor>();
            a.displayName = name; a.team = team; a.isPlayer = isPlayer;
            a.weapons = go.AddComponent<WeaponHandler>();
            a.weapons.actor = a;
            a.model = CharacterModel.Build(a, team);
            a.weapons.ResetToDefaults();
            if (isPlayer)
            {
                PlayerController.Create(a);
                var obs = go.AddComponent<NavMeshObstacle>();
                obs.shape = NavMeshObstacleShape.Capsule; obs.radius = 0.35f; obs.height = 1.8f; obs.center = new Vector3(0, 0.9f, 0); obs.carving = false;
            }
            return a;
        }

        void CreateBot(string name, Team team, BotProfile profile)
        {
            var a = CreateActor(name, team, false);
            bots.Add(BotBrain.Attach(a, profile));
        }

        void OnDestroy()
        {
            GameEvents.Kill -= OnKill;
            if (I == this) I = null;
        }

        public void Teardown()
        {
            GameEvents.Kill -= OnKill;
            Pickup.ClearAll();
            foreach (var g in Grenade.Live.ToArray()) if (g) Destroy(g.gameObject);
            foreach (var s in SmokeCloud.Active.ToArray()) if (s) Destroy(s.gameObject);
            if (chargeObj) Destroy(chargeObj.gameObject);
            foreach (var a in Actor.All.ToArray()) if (a) Destroy(a.gameObject);
            Actor.All.Clear();
            Map?.Destroy();
            Effects.ClearAll();
            Hearing.Clear();
            AudioManager.PlayAmbient(null);
            Destroy(gameObject);
            I = null;
        }

        // ------------------------------------------------------------ Rounds
        void StartRound(bool resetInventory)
        {
            roundNumber++;
            Pickup.ClearAll();
            foreach (var g in Grenade.Live.ToArray()) if (g) Destroy(g.gameObject);
            foreach (var s in SmokeCloud.Active.ToArray()) if (s) Destroy(s.gameObject);
            if (chargeObj) Destroy(chargeObj.gameObject);
            ChargePlanted = false; ChargeSite = -1; DroppedCharge = null; planter = null; raidersPlantedThisRound = false;
            PlantingActor = DefusingActor = null; PlantProgress = DefuseProgress = 0f;
            Hearing.Clear();
            killFeed.RemoveAll(k => Time.time - k.time > 2f);

            var raiders = new List<Actor>(); var wardens = new List<Actor>();
            foreach (var a in Actor.All) (a.team == Team.Raiders ? raiders : wardens).Add(a);
            SpawnTeam(raiders, Map.raiderSpawns, Map.raiderFacing, resetInventory);
            SpawnTeam(wardens, Map.wardenSpawns, Map.wardenFacing, resetInventory);

            if (Config.mode == GameMode.Classic)
            {
                // Give the charge to a random raider (bots or player)
                if (raiders.Count > 0) raiders[Random.Range(0, raiders.Count)].weapons.Give(WeaponDatabase.Charge, false);
                float freeze = Config.freezeTime;
                foreach (var a in Actor.All) a.frozenUntil = Time.time + freeze;
                Phase = MatchPhase.Freeze;
                phaseEnd = Time.time + freeze;
                roundEndTime = phaseEnd + Config.roundTimeMinutes * 60f;
                buyEnd = Time.time + freeze + Config.buyTime;
                BotsBuy(roundNumber == 1 || roundNumber == HalfRounds + 1);
                raiderCoord.NewRound(bots); wardenCoord.NewRound(bots);
                GameEvents.RaiseAnnounce(roundNumber == Config.maxRounds ? "Final round" : roundNumber == HalfRounds ? "Last round of the half" : $"Round {roundNumber}", Color.white);
            }
            else
            {
                Phase = MatchPhase.Live;
                foreach (var a in Actor.All) { a.frozenUntil = 0f; spawnTime[a] = Time.time; a.spawnProtectedUntil = Time.time + 2f; }
                if (Config.mode == GameMode.TeamDeathmatch) foreach (var b in bots) GiveRandomLoadout(b.actor);
                if (Config.mode == GameMode.Practice) foreach (var b in bots) PlaceForPractice(b.actor);
                raiderCoord.NewRound(bots); wardenCoord.NewRound(bots);
                GameEvents.RaiseAnnounce(Config.mode == GameMode.TeamDeathmatch ? $"Team Deathmatch — first to {Config.tdmKillLimit}" : "Practice range — open the buy menu anytime", Color.white);
                AudioManager.Play2D("round_start", 0.7f);
            }
        }

        void SpawnTeam(List<Actor> list, List<Vector3> spawns, float facing, bool resetInventory)
        {
            var pts = new List<Vector3>(spawns);
            MathUtil.Shuffle(pts);
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 p = pts[i % pts.Count];
                if (i >= pts.Count) p += new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                bool keep = !resetInventory && list[i].alive;
                list[i].Respawn(p, facing + Random.Range(-15f, 15f), keep);
            }
        }

        void BotsBuy(bool pistolRound)
        {
            foreach (Team t in new[] { Team.Raiders, Team.Wardens })
            {
                float sum = 0; int n = 0;
                foreach (var a in Actor.All) if (a.team == t) { sum += a.money; n++; }
                float avg = n > 0 ? sum / n : 0;
                foreach (var b in bots) if (b.actor.team == t) BotBuy.Buy(b.actor, b.profile, avg, pistolRound, Config.mode);
            }
        }

        void GiveRandomLoadout(Actor a)
        {
            string[] rifles = a.team == Team.Raiders ? new[] { "ar47", "ar47", "gale", "sg5", "mpx5", "breacher", "longbow", "vectorp" } : new[] { "m7", "m7", "fennec", "aux", "mpx5", "breacher", "longbow", "vectorp" };
            if (a.bot != null && a.bot.profile.difficulty <= BotDifficulty.Normal && Random.value < 0.3f) rifles = new[] { "mpx5", "hornet", "wasp", "breacher" };
            var def = WeaponDatabase.Get(rifles[Random.Range(0, rifles.Length)]);
            if (!WeaponDatabase.CanBuy(def, a.team)) def = WeaponDatabase.Get("mpx5");
            a.weapons.Give(def);
            a.armor = 100; a.helmet = true;
            if (a.bot != null && a.bot.profile.grenadeUse > 0.3f) a.weapons.Give(WeaponDatabase.Get("he"), false);
        }

        void PlaceForPractice(Actor a)
        {
            // Practice targets appear at varied distances in front of the player.
            for (int tries = 0; tries < 20; tries++)
            {
                Vector3 p = Map.RandomFloor();
                float d = Vector3.Distance(p, Player.transform.position);
                if (d < 10f || d > 45f) continue;
                if (NavMesh.SamplePosition(p, out var h, 2f, NavMesh.AllAreas))
                {
                    a.Respawn(h.position, Quaternion.LookRotation((Player.transform.position - h.position).Flat()).eulerAngles.y, true);
                    a.bot.OnRespawn(h.position);
                    a.spawnProtectedUntil = 0f;
                    if (a.weapons.primary == null) a.weapons.Give(WeaponDatabase.Get(a.team == Team.Raiders ? "ar47" : "m7"));
                    return;
                }
            }
        }

        // ------------------------------------------------------------ Update
        void Update()
        {
            float now = Time.time;
            Hearing.Prune();
            raiderCoord?.Tick(bots);
            wardenCoord?.Tick(bots);

            switch (Config.mode)
            {
                case GameMode.Classic: UpdateClassic(now); break;
                case GameMode.TeamDeathmatch: UpdateDeathmatch(now); break;
                case GameMode.Practice: UpdatePractice(now); break;
            }
        }

        void UpdateClassic(float now)
        {
            switch (Phase)
            {
                case MatchPhase.Freeze:
                    if (now >= phaseEnd)
                    {
                        Phase = MatchPhase.Live;
                        AudioManager.Play2D("round_start", 0.7f);
                        GameEvents.RaiseAnnounce("Go!", new Color(0.6f, 1f, 0.6f));
                    }
                    break;
                case MatchPhase.Live:
                    if (ChargePlanted)
                    {
                        chargeObj.Tick(ChargeTimeLeft, Config.bombTimer);
                        if (now >= chargeExplodeAt) Detonate();
                    }
                    else if (now >= roundEndTime) EndRound(Team.Wardens, "Time ran out — the charge was not planted");
                    if (PlantingActor != null && (!PlantingActor.alive || lastPlantKey < now - 0.25f)) { PlantingActor = null; PlantProgress = 0f; }
                    if (DefusingActor != null && (!DefusingActor.alive || lastDefuseTouch < now - 0.25f)) { DefusingActor = null; DefuseProgress = 0f; }
                    break;
                case MatchPhase.RoundOver:
                    if (ChargePlanted && chargeObj) { chargeObj.Tick(ChargeTimeLeft, Config.bombTimer); if (now >= chargeExplodeAt) Detonate(); }
                    if (now >= phaseEnd) NextRound();
                    break;
                case MatchPhase.HalfTime:
                    if (now >= phaseEnd) { SwapSides(); StartRound(true); }
                    break;
            }
        }

        float lastDefuseTouch;

        void NextRound()
        {
            int played = raiderScore + wardenScore;
            if (raiderScore >= RoundsToWin || wardenScore >= RoundsToWin || played >= Config.maxRounds) { EndMatch(); return; }
            if (played == HalfRounds)
            {
                Phase = MatchPhase.HalfTime;
                phaseEnd = Time.time + 5f;
                GameEvents.RaiseAnnounce("Half-time — switching sides", new Color(1f, 0.85f, 0.4f));
                return;
            }
            StartRound(false);
        }

        void SwapSides()
        {
            foreach (var a in Actor.All)
            {
                a.SetTeam(a.team.Opposite());
                a.money = Config.startMoney;
            }
            foreach (var b in bots) b.coord = b.actor.team == Team.Raiders ? raiderCoord : wardenCoord;
            (raiderScore, wardenScore) = (wardenScore, raiderScore);
            lossStreak[Team.Raiders] = 0; lossStreak[Team.Wardens] = 0;
        }

        void EndRound(Team winner, string reason)
        {
            if (Phase != MatchPhase.Live) return;
            Phase = MatchPhase.RoundOver;
            phaseEnd = Time.time + 5f;
            LastWinner = winner; LastRoundReason = reason;
            if (winner == Team.Raiders) raiderScore++; else wardenScore++;
            PlantingActor = DefusingActor = null;

            var loser = winner.Opposite();
            lossStreak[winner] = 0;
            lossStreak[loser] = Mathf.Min(lossStreak[loser] + 1, 5);
            int winReward = reason.Contains("detonated") ? 3500 : reason.Contains("defused") ? 3500 : 3250;
            int lossReward = 1400 + 500 * (lossStreak[loser] - 1);
            foreach (var a in Actor.All)
            {
                if (a.team == winner) Earn(a, winReward);
                else
                {
                    // Raiders who survive a lost timeout without planting get no loss bonus.
                    bool noBonus = loser == Team.Raiders && reason.StartsWith("Time") && a.alive;
                    if (!noBonus) Earn(a, lossReward + (loser == Team.Raiders && raidersPlantedThisRound ? 800 : 0));
                }
            }

            bool playerWon = Player.team == winner;
            AudioManager.Play2D(playerWon ? "round_win" : "round_lose", 0.8f);
            GameEvents.RaiseAnnounce($"{winner.Label()} win the round", winner.Tint());
            var stats = GameApp.Stats;
            stats.roundsPlayed++;
            if (playerWon) stats.roundsWon++;
        }

        void Earn(Actor a, int amount) => a.money = Mathf.Clamp(a.money + amount, 0, 16000);

        // ------------------------------------------------------------ Charge
        public bool CanBuy(Actor a)
        {
            if (!a.alive) return false;
            switch (Config.mode)
            {
                case GameMode.Practice: return true;
                case GameMode.TeamDeathmatch: return spawnTime.TryGetValue(a, out var st) && Time.time - st < 15f && Map.InBuyZone(a.team, a.transform.position);
                default: return (Phase == MatchPhase.Freeze || (Phase == MatchPhase.Live && Time.time < buyEnd)) && Map.InBuyZone(a.team, a.transform.position);
            }
        }

        public bool CanUse(Actor a)
        {
            if (!a.alive || Config.mode != GameMode.Classic && Pickup.FindSwappable(a) == null) return false;
            if (Pickup.FindSwappable(a) != null) return true;
            if (Config.mode != GameMode.Classic || Phase != MatchPhase.Live) return false;
            if (a.team == Team.Raiders && a.weapons.hasCharge && !ChargePlanted && Map.SiteAt(a.transform.position) >= 0) return true;
            if (a.team == Team.Wardens && ChargePlanted && Vector3.Distance(a.transform.position, ChargePosition) < 1.8f) return true;
            return false;
        }

        /// <summary>Hold-to-plant / hold-to-defuse and tap-to-swap-weapon. Called every frame by player and bots.</summary>
        public void InteractUpdate(Actor a, bool holding, bool pressed)
        {
            if (!a.alive) return;
            if (pressed && a.isPlayer)
            {
                var p = Pickup.FindSwappable(a);
                bool objectiveAvailable = Config.mode == GameMode.Classic && ((a.team == Team.Raiders && a.weapons.hasCharge && Map.SiteAt(a.transform.position) >= 0) || (a.team == Team.Wardens && ChargePlanted && Vector3.Distance(a.transform.position, ChargePosition) < 1.8f));
                if (p != null && !objectiveAvailable) { p.SwapInto(a); return; }
            }
            if (Config.mode != GameMode.Classic || Phase != MatchPhase.Live) return;
            float now = Time.time;

            // Planting
            if (a.team == Team.Raiders && a.weapons.hasCharge && !ChargePlanted && holding && a.grounded && Map.SiteAt(a.transform.position) >= 0)
            {
                if (PlantingActor != null && PlantingActor != a) return;
                if (PlantingActor == null) { PlantingActor = a; PlantProgress = 0f; if (a.weapons.slot != WeaponSlot.Bomb) a.weapons.Equip(WeaponSlot.Bomb, true); }
                lastPlantKey = now;
                float before = PlantProgress;
                PlantProgress += Time.deltaTime / 3f;
                if (Mathf.FloorToInt(before * 7f) != Mathf.FloorToInt(PlantProgress * 7f)) AudioManager.Play3D("plant_key", a.transform.position, 0.6f, 20f);
                if (PlantProgress >= 1f) Plant(a);
                return;
            }
            if (PlantingActor == a && !holding) { PlantingActor = null; PlantProgress = 0f; }

            // Defusing
            if (a.team == Team.Wardens && ChargePlanted && holding && Vector3.Distance(a.transform.position, ChargePosition) < 1.8f)
            {
                if (DefusingActor != null && DefusingActor != a) return;
                if (DefusingActor == null)
                {
                    DefusingActor = a; DefuseProgress = 0f;
                    Hearing.Emit(ChargePosition, 18f, NoiseType.Bomb, a);
                    if (Player.team == Team.Raiders || a.isPlayer) GameEvents.RaiseAnnounce(a.isPlayer ? "Defusing..." : "The charge is being defused!", new Color(0.5f, 0.8f, 1f));
                }
                lastDefuseTouch = now;
                float before = DefuseProgress;
                DefuseProgress += Time.deltaTime / (a.defuseKit ? 5f : 10f);
                if (Mathf.FloorToInt(before * 10f) != Mathf.FloorToInt(DefuseProgress * 10f)) AudioManager.Play3D("defuse_tick", ChargePosition, 0.7f, 20f);
                if (DefuseProgress >= 1f) Defuse(a);
                return;
            }
            if (DefusingActor == a && !holding) { DefusingActor = null; DefuseProgress = 0f; }
        }

        void Plant(Actor a)
        {
            ChargePlanted = true;
            raidersPlantedThisRound = true;
            PlantingActor = null; PlantProgress = 0f;
            planter = a;
            Vector3 pos = a.transform.position;
            if (Physics.Raycast(pos + Vector3.up, Vector3.down, out var hit, 3f, Layers.WorldMask)) pos = hit.point;
            ChargePosition = pos;
            ChargeSite = Map.SiteAt(pos);
            a.weapons.hasCharge = false;
            a.weapons.Equip(a.weapons.HasSlot(WeaponSlot.Primary) ? WeaponSlot.Primary : WeaponSlot.Secondary);
            chargeObj = ChargeObject.Create(pos, a.yaw);
            chargeExplodeAt = Time.time + Config.bombTimer;
            Earn(a, 300);
            AudioManager.Play2D("armed", 0.8f);
            Hearing.Emit(pos, 40f, NoiseType.Bomb, a);
            GameEvents.RaiseAnnounce($"The charge has been planted at site {Map.SiteName(ChargeSite)}", new Color(1f, 0.45f, 0.3f));
            if (a.isPlayer) GameApp.Stats.plants++;
        }

        void Defuse(Actor a)
        {
            DefusingActor = null;
            ChargePlanted = false;
            Earn(a, 300);
            AudioManager.Play2D("defused", 0.9f);
            if (a.isPlayer) GameApp.Stats.defuses++;
            if (chargeObj) Destroy(chargeObj.gameObject);
            EndRound(Team.Wardens, "The charge was defused");
        }

        void Detonate()
        {
            ChargePlanted = false;
            Vector3 p = ChargePosition;
            if (chargeObj) Destroy(chargeObj.gameObject);
            Effects.Explosion(p + Vector3.up * 0.5f, 3.2f);
            AudioManager.Play3D("big_explosion", p, 1f, 200f);
            CameraShake.Add(p, 1.5f, 50f);
            Ballistics.Explosion(p, 24f, 400f, planter != null && planter.alive ? planter : null, WeaponDatabase.Charge);
            if (Phase == MatchPhase.Live) EndRound(Team.Raiders, "The charge detonated");
        }

        public void OnChargeDropped(Pickup p)
        {
            DroppedCharge = p;
            if (Player != null && Player.team == Team.Raiders && Phase == MatchPhase.Live) GameEvents.RaiseAnnounce("The charge has been dropped", new Color(1f, 0.7f, 0.3f));
        }

        // ------------------------------------------------------------ Kills
        void OnKill(Actor killer, Actor victim, WeaponDef weapon, bool headshot)
        {
            killFeed.Add(new KillFeedEntry
            {
                killer = killer != null ? killer.displayName : "", victim = victim.displayName,
                weapon = weapon != null ? weapon.name : "World", killerTeam = killer != null ? killer.team : Team.None, victimTeam = victim.team,
                headshot = headshot, involvesPlayer = killer == Player || victim == Player, time = Time.time
            });
            if (killFeed.Count > 6) killFeed.RemoveAt(0);

            if (killer != null && killer != victim)
            {
                if (killer.team == victim.team && Config.mode != GameMode.Practice) { Earn(killer, -300); killer.score -= 1; }
                else
                {
                    if (Config.mode == GameMode.Classic) Earn(killer, (int)(weapon != null ? weapon.killReward : 300));
                    killer.score += 2;
                    if (killer == Player && weapon != null && Config.mode != GameMode.Practice) GameApp.Stats.AddWeaponKill(weapon.name);
                }
            }
            bool counts = Config.mode != GameMode.Practice;   // practice never affects career statistics
            if (victim == Player && counts) GameApp.Stats.deaths++;
            if (killer == Player && victim.team != Player.team)
            {
                if (counts)
                {
                    var s = GameApp.Stats; s.kills++; if (headshot) s.headshots++;
                    s.bestKillStreak = Mathf.Max(s.bestKillStreak, Player.killStreak);
                }
                else { practiceKills++; if (headshot) practiceHeadshots++; }
            }

            switch (Config.mode)
            {
                case GameMode.Classic:
                    if (Phase != MatchPhase.Live) break;
                    int r = Actor.CountAlive(Team.Raiders), w = Actor.CountAlive(Team.Wardens);
                    if (w == 0) EndRound(Team.Raiders, "All Wardens eliminated");
                    else if (r == 0 && !ChargePlanted) EndRound(Team.Wardens, "All Raiders eliminated");
                    break;
                case GameMode.TeamDeathmatch:
                    if (killer != null && killer.team != victim.team)
                    {
                        if (killer.team == Team.Raiders) raiderScore++; else wardenScore++;
                    }
                    respawnAt[victim] = Time.time + 3f;
                    if (raiderScore >= Config.tdmKillLimit || wardenScore >= Config.tdmKillLimit) EndMatch();
                    break;
                case GameMode.Practice:
                    respawnAt[victim] = Time.time + (victim == Player ? 2f : 1.2f);
                    break;
            }
        }

        // ------------------------------------------------------------ Deathmatch & practice
        void UpdateDeathmatch(float now)
        {
            if (Phase == MatchPhase.MatchOver) return;
            if (MatchTimeLeft <= 0f) { EndMatch(); return; }
            RespawnDue(now, false);
        }

        void UpdatePractice(float now) => RespawnDue(now, true);

        void RespawnDue(float now, bool practice)
        {
            foreach (var kv in new List<KeyValuePair<Actor, float>>(respawnAt))
            {
                if (now < kv.Value || kv.Key == null) continue;
                respawnAt.Remove(kv.Key);
                var a = kv.Key;
                if (practice && !a.isPlayer) { PlaceForPractice(a); continue; }
                Vector3 p = SafeSpawn(a.team);
                a.Respawn(p, a.team == Team.Raiders ? Map.raiderFacing : Map.wardenFacing, false);
                a.spawnProtectedUntil = now + 2f;
                spawnTime[a] = now;
                if (a.isPlayer) RebuyLoadout();
                else if (!practice) GiveRandomLoadout(a);
                if (practice) a.weapons.unlimitedAmmo = Config.unlimitedAmmo;
            }
        }

        Vector3 SafeSpawn(Team t)
        {
            var list = t == Team.Raiders ? Map.raiderSpawns : Map.wardenSpawns;
            Vector3 best = list[Random.Range(0, list.Count)]; float bestScore = -1;
            foreach (var p in list)
            {
                float nearest = 999f;
                foreach (var a in Actor.All) if (a.alive && a.team != t) nearest = Mathf.Min(nearest, Vector3.Distance(a.transform.position, p));
                float score = nearest + Random.value * 4f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------ Buying (player)
        public bool PlayerBuy(WeaponDef def)
        {
            var a = Player;
            if (!CanBuy(a)) { AudioManager.Play2D("deny"); return false; }
            int moneyBefore = a.money;
            if (Config.mode != GameMode.Classic) a.money = 16000;
            bool ok = BotBuy.TryBuy(a, def.id);
            if (Config.mode != GameMode.Classic) a.money = 16000;
            AudioManager.Play2D(ok ? "buy" : "deny");
            if (ok && def.IsGun)
            {
                playerLoadout.RemoveAll(id => WeaponDatabase.Get(id).slot == def.slot);
                playerLoadout.Add(def.id);
            }
            return ok;
        }

        void RebuyLoadout()
        {
            foreach (var id in playerLoadout) { var d = WeaponDatabase.Get(id); if (d != null) Player.weapons.Give(d); }
            Player.armor = 100; Player.helmet = true;
        }

        // ------------------------------------------------------------ Radio
        public static readonly string[] RadioCommands = { "Go go go!", "Follow me", "Hold this position", "Regroup on me", "Need backup", "Enemy spotted", "Fall back", "Affirmative", "Negative", "Sector clear" };

        public void PlayerRadio(int index)
        {
            if (Player == null || !Player.alive) return;
            string msg = RadioCommands[index];
            GameEvents.RaiseRadio(Player, msg);
            AudioManager.Play2D("radio", 0.6f);
            var coord = Player.team == Team.Raiders ? raiderCoord : wardenCoord;
            bool anyAck = false;
            foreach (var b in bots)
            {
                if (b.actor.team != Player.team || !b.actor.alive) continue;
                float d = Vector3.Distance(b.transform.position, Player.transform.position);
                switch (index)
                {
                    case 0: b.order = BotOrder.None; if (Player.team == Team.Raiders) coord.executing = true; anyAck = true; break;
                    case 1: if (d < 25f) { b.order = BotOrder.FollowLeader; b.orderLeader = Player; anyAck = true; } break;
                    case 2: if (d < 25f) { b.order = BotOrder.Hold; b.holdPosition = b.transform.position; anyAck = true; } break;
                    case 3: b.order = BotOrder.FollowLeader; b.orderLeader = Player; anyAck = true; break;
                    case 6: b.order = BotOrder.None; anyAck = true; break;
                }
            }
            if (index == 4)
            {
                // Two nearest teammates come to help
                var mates = bots.FindAll(b => b.actor.team == Player.team && b.actor.alive);
                mates.Sort((x, y) => Vector3.Distance(x.transform.position, Player.transform.position).CompareTo(Vector3.Distance(y.transform.position, Player.transform.position)));
                for (int i = 0; i < Mathf.Min(2, mates.Count); i++) { mates[i].order = BotOrder.FollowLeader; mates[i].orderLeader = Player; anyAck = true; }
            }
            if (index == 5)
            {
                // Share what the player is actually looking at
                if (Physics.Raycast(Player.EyePosition, Player.aimDirection, out var hit, 80f, Layers.BulletMask, QueryTriggerInteraction.Collide))
                {
                    coord.ReportFromPlayer(hit.point);
                    anyAck = true;
                }
            }
            if (anyAck && index <= 6)
            {
                foreach (var b in bots)
                    if (b.actor.team == Player.team && b.actor.alive) { StartCoroutine(Ack(b.actor)); break; }
            }
        }

        System.Collections.IEnumerator Ack(Actor a)
        {
            yield return new WaitForSeconds(0.7f);
            if (a != null && a.alive) GameEvents.RaiseRadio(a, Random.value < 0.5f ? "Roger that." : "Affirmative.");
        }

        public Vector3 ThreatDirection(Team team, Vector3 from)
        {
            Vector3 target = team == Team.Raiders ? Map.wardenSpawnCenter : Map.raiderSpawnCenter;
            Vector3 d = (target - from).Flat();
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
        }

        // ------------------------------------------------------------ End of match & stats
        public void EndMatch(bool abandoned = false)
        {
            if (Phase == MatchPhase.MatchOver) return;
            Phase = MatchPhase.MatchOver;
            foreach (var a in Actor.All) a.frozenUntil = float.MaxValue;
            AudioManager.Play2D("match_end", 0.8f);

            if (Config.mode == GameMode.Practice) { Persistence.SaveStats(GameApp.Stats); return; }
            var s = GameApp.Stats;
            int mine = Player.team == Team.Raiders ? raiderScore : wardenScore;
            int theirs = Player.team == Team.Raiders ? wardenScore : raiderScore;
            if (!abandoned)
            {
                s.matchesPlayed++;
                var mr = s.Map(Map.def.name);
                mr.played++;
                if (mine > theirs) { s.matchesWon++; mr.won++; }
                else if (mine < theirs) s.matchesLost++;
                else s.matchesDrawn++;
                mr.kills += Player.kills; mr.deaths += Player.deaths;
            }
            s.shotsFired += Player.weapons.shotsFired - playerShotsAtStart;
            s.shotsHit += PlayerHitFeedback.shotsHit;
            playerShotsAtStart = Player.weapons.shotsFired;
            PlayerHitFeedback.shotsHit = 0;
            Persistence.SaveStats(s);
            GameEvents.RaiseAnnounce(mine > theirs ? "Victory" : mine < theirs ? "Defeat" : "Draw", mine >= theirs ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.5f, 0.4f));
        }

        public string ResultText()
        {
            int mine = Player.team == Team.Raiders ? raiderScore : wardenScore;
            int theirs = Player.team == Team.Raiders ? wardenScore : raiderScore;
            return mine > theirs ? "VICTORY" : mine < theirs ? "DEFEAT" : "DRAW";
        }
    }
}
