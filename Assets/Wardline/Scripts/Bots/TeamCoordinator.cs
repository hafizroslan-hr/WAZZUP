using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Wardline
{
    public struct BotObjective
    {
        public BotState label;
        public Vector3 position, look;
        public bool walk, crouch;
        public float tolerance;
        public Vector3? utilityTarget;
        public bool preferSmoke;
    }

    /// <summary>
    /// Team-level tactics. Raiders pick a site and a strategy (rush / default / split / fake), stage on lanes and
    /// execute together with utility. Wardens split across sites, hold angles, rotate on callouts and retake
    /// after a plant. Shared knowledge is limited to what teammates have seen or heard (radio callouts).
    /// </summary>
    public class TeamCoordinator
    {
        public enum Strategy { Rush, Default, Split, Fake }

        public readonly Team team;
        readonly BotDifficulty difficulty;
        readonly float teamwork;
        readonly MapRuntime map;
        public int targetSite;
        public Strategy strategy;
        public bool executing;
        float executeAt, roundStart, nextTick, lastRotation = -99f, plantSeenTime = -1f;

        class Assignment
        {
            public int site;
            public char lane;
            public Vector3 stage, hold, face;
            public bool faker, lurker, utilityDone;
        }

        readonly Dictionary<BotBrain, Assignment> assign = new Dictionary<BotBrain, Assignment>();
        struct Intel { public Vector3 pos; public float time; }
        readonly List<Intel> intel = new List<Intel>();
        static readonly Dictionary<Team, (Vector3 pos, float time)> lastCallout = new Dictionary<Team, (Vector3, float)>();

        public TeamCoordinator(Team team, BotDifficulty difficulty, MapRuntime map)
        {
            this.team = team; this.difficulty = difficulty; this.map = map;
            teamwork = BotProfile.For(difficulty).teamwork;
        }

        public char LaneForSite(int site) => site == 0 ? map.laneA : map.laneB;
        char laneMid => map.laneMid;
        char laneA => map.laneA;
        char laneB => map.laneB;

        public static bool RecentCallout(Team t, Vector3 pos)
        {
            if (lastCallout.TryGetValue(t, out var c) && Time.time - c.time < 5f && Vector3.Distance(c.pos, pos) < 12f) return true;
            lastCallout[t] = (pos, Time.time);
            return false;
        }

        public void Report(Vector3 pos, BotBrain from)
        {
            intel.Add(new Intel { pos = pos, time = Time.time });
            if (intel.Count > 40) intel.RemoveAt(0);
        }

        /// <summary>Player radio callouts also feed the team's intel.</summary>
        public void ReportFromPlayer(Vector3 pos) => Report(pos, null);

        int RecentIntelNear(Vector3 p, float radius, float window)
        {
            int n = 0;
            foreach (var i in intel) if (Time.time - i.time < window && Vector3.Distance(i.pos, p) < radius) n++;
            return n;
        }

        Vector3? LatestIntel(float window)
        {
            for (int i = intel.Count - 1; i >= 0; i--) if (Time.time - intel[i].time < window) return intel[i].pos;
            return null;
        }

        // ------------------------------------------------------------ Round setup
        public void NewRound(List<BotBrain> bots)
        {
            assign.Clear(); intel.Clear();
            executing = false; roundStart = Time.time; plantSeenTime = -1f; lastRotation = -99f;
            var mm = MatchManager.I;
            var mode = mm.Config.mode;
            var mine = bots.FindAll(b => b.actor.team == team);
            MathUtil.Shuffle(mine);
            if (mode != GameMode.Classic) { foreach (var b in mine) assign[b] = new Assignment(); return; }

            if (team == Team.Raiders)
            {
                targetSite = Random.value < 0.5f ? 0 : 1;
                float r = Random.value;
                strategy = difficulty == BotDifficulty.Easy ? Strategy.Rush
                    : difficulty == BotDifficulty.Normal ? (r < 0.35f ? Strategy.Rush : Strategy.Default)
                    : difficulty == BotDifficulty.Hard ? (r < 0.15f ? Strategy.Rush : r < 0.55f ? Strategy.Default : Strategy.Split)
                    : (r < 0.1f ? Strategy.Rush : r < 0.4f ? Strategy.Default : r < 0.75f ? Strategy.Split : Strategy.Fake);
                if (mine.Count < 3 && strategy == Strategy.Fake) strategy = Strategy.Split;
                executeAt = roundStart + (strategy == Strategy.Rush ? 0f : difficulty == BotDifficulty.Expert ? 28f : 35f);
                for (int i = 0; i < mine.Count; i++)
                {
                    var a = new Assignment { site = targetSite };
                    switch (strategy)
                    {
                        case Strategy.Rush: a.lane = difficulty == BotDifficulty.Easy ? RandomLane() : LaneForSite(targetSite); break;
                        case Strategy.Default: a.lane = i % 3 == 2 ? laneMid : LaneForSite(targetSite); break;
                        case Strategy.Split: a.lane = i % 2 == 0 ? LaneForSite(targetSite) : laneMid; break;
                        case Strategy.Fake:
                            if (i < 2) { a.faker = true; a.lane = LaneForSite(1 - targetSite); }
                            else a.lane = i % 2 == 0 ? LaneForSite(targetSite) : laneMid;
                            break;
                    }
                    if (difficulty >= BotDifficulty.Hard && i == mine.Count - 1 && mine.Count >= 4 && strategy != Strategy.Rush && Random.value < BotProfile.For(difficulty).flankChance)
                    { a.lurker = true; a.lane = a.lane == laneMid ? LaneForSite(1 - targetSite) : laneMid; }
                    a.stage = StagePoint(a.lane, a.faker ? 1 - targetSite : targetSite);
                    a.hold = RandomSiteSpot(targetSite);
                    assign[mine[i]] = a;
                }
            }
            else
            {
                // Wardens: split across both sites, optional mid lurker for info.
                int n = mine.Count;
                for (int i = 0; i < n; i++)
                {
                    var a = new Assignment();
                    if (difficulty == BotDifficulty.Easy) a.site = Random.value < 0.5f ? 0 : 1;
                    else a.site = i % 2;
                    if (n >= 5 && i == n - 1 && difficulty >= BotDifficulty.Normal) { a.lurker = true; a.hold = StagePoint(laneMid, a.site); }
                    else a.hold = GuardSpot(a.site, i);
                    a.face = ThreatFacing(a.hold, a.site);
                    assign[mine[i]] = a;
                }
            }
        }

        char RandomLane() { var l = new[] { laneA, laneB, laneMid }; return l[Random.Range(0, 3)]; }

        Vector3 StagePoint(char lane, int site)
        {
            var pts = map.LanePoints(lane);
            if (pts == null || pts.Count == 0) return map.SiteCenter(site);
            Vector3 best = pts[0]; float bd = float.MaxValue;
            foreach (var p in pts) { float d = Vector3.Distance(p, map.SiteCenter(site)); if (d < bd) { bd = d; best = p; } }
            return Jitter(best, 2f);
        }

        Vector3 RandomSiteSpot(int site)
        {
            var cells = map.SiteCells(site);
            return Jitter(cells[Random.Range(0, cells.Count)], 0.8f);
        }

        Vector3 GuardSpot(int site, int index)
        {
            // Holding positions: guard markers and platforms near the site, then site cells.
            var c = map.SiteCenter(site);
            var options = new List<Vector3>();
            foreach (var g in map.guardSpots) if (Vector3.Distance(g, c) < 24f) options.Add(g);
            foreach (var p in map.platforms) if (Vector3.Distance(p, c) < 22f) options.Add(p);
            foreach (var s in map.SiteCells(site)) if (Random.value < 0.25f) options.Add(s);
            if (options.Count == 0) return RandomSiteSpot(site);
            return Jitter(options[(index * 7 + Random.Range(0, options.Count)) % options.Count], 0.6f);
        }

        Vector3 ThreatFacing(Vector3 from, int site)
        {
            // Face the lane attackers use to reach this site.
            var pts = map.LanePoints(LaneForSite(site));
            Vector3 threat = pts != null && pts.Count > 0 ? pts[0] : map.raiderSpawnCenter;
            float bestD = float.MaxValue;
            if (pts != null) foreach (var p in pts) { float d = Vector3.Distance(p, from); if (d < bestD) { bestD = d; threat = p; } }
            var mid = map.LanePoints(laneMid);
            if (mid != null && Random.value < 0.35f) threat = mid[0];
            return threat + Vector3.up * 1.5f;
        }

        static Vector3 Jitter(Vector3 p, float r)
        {
            Vector3 j = p + new Vector3(Random.Range(-r, r), 0, Random.Range(-r, r));
            return NavMesh.SamplePosition(j, out var h, 2f, NavMesh.AllAreas) ? h.position : p;
        }

        // ------------------------------------------------------------ Per-tick team logic
        public void Tick(List<BotBrain> bots)
        {
            if (Time.time < nextTick) return;
            nextTick = Time.time + 0.5f;
            var mm = MatchManager.I;
            if (mm == null || mm.Config.mode != GameMode.Classic) return;

            if (team == Team.Raiders && !executing && !mm.ChargePlanted)
            {
                int alive = 0, staged = 0;
                foreach (var kv in assign)
                {
                    if (!kv.Key.actor.alive || kv.Value.faker || kv.Value.lurker) continue;
                    alive++;
                    if (Vector3.Distance(kv.Key.transform.position, kv.Value.stage) < 6f) staged++;
                }
                bool groupReady = alive > 0 && staged >= Mathf.CeilToInt(alive * 0.6f);
                if (strategy == Strategy.Rush || Time.time >= executeAt || (groupReady && Time.time - roundStart > 8f) || mm.RoundTimeLeft < 40f)
                {
                    executing = true;
                    if (mm.Player != null && mm.Player.team == team) GameEvents.RaiseRadio(FirstAlive(), "Go go go! Push " + map.SiteName(targetSite) + "!");
                }
            }

            if (team == Team.Wardens && !mm.ChargePlanted && teamwork > 0f && Time.time - lastRotation > 12f)
            {
                // Rotate on information: several callouts near one site pull help from the other.
                for (int site = 0; site < 2; site++)
                {
                    int threat = RecentIntelNear(map.SiteCenter(site), 22f, 6f);
                    int needed = difficulty == BotDifficulty.Expert ? 2 : difficulty == BotDifficulty.Hard ? 2 : 3;
                    if (threat < needed || Random.value > teamwork) continue;
                    int moved = 0, maxMove = difficulty == BotDifficulty.Expert ? 2 : 1;
                    foreach (var kv in assign)
                    {
                        if (!kv.Key.actor.alive || kv.Value.site == site || moved >= maxMove) continue;
                        kv.Value.site = site; kv.Value.hold = GuardSpot(site, moved + 3); kv.Value.face = ThreatFacing(kv.Value.hold, site);
                        moved++;
                    }
                    if (moved > 0)
                    {
                        lastRotation = Time.time;
                        if (mm.Player != null && mm.Player.team == team) GameEvents.RaiseRadio(FirstAlive(), "Rotating to " + map.SiteName(site) + "!");
                    }
                }
            }

            if (mm.ChargePlanted && plantSeenTime < 0f) plantSeenTime = Time.time;
        }

        Actor FirstAlive()
        {
            foreach (var kv in assign) if (kv.Key.actor.alive) return kv.Key.actor;
            return null;
        }

        // ------------------------------------------------------------ Per-bot objective
        public BotObjective GetObjective(BotBrain b)
        {
            var mm = MatchManager.I;
            var o = new BotObjective { label = BotState.Advance, tolerance = 1.2f, look = b.actor.EyePosition + b.transform.forward * 10f };
            if (!assign.TryGetValue(b, out var a)) { a = new Assignment { site = Random.Range(0, 2) }; a.hold = GuardSpot(a.site, 0); assign[b] = a; }

            if (mm.Config.mode != GameMode.Classic) return Hunt(b, o);

            float roundAge = Time.time - roundStart;
            if (team == Team.Raiders)
            {
                // Charge dropped: nearest living raider recovers it
                if (!mm.ChargePlanted && mm.DroppedCharge != null && IsNearestRaiderTo(b, mm.DroppedCharge.transform.position))
                {
                    o.label = BotState.RecoverCharge; o.position = mm.DroppedCharge.transform.position; o.tolerance = 0.5f;
                    o.look = o.position + Vector3.up; return o;
                }
                if (mm.ChargePlanted)
                {
                    // Guard the planted charge from cover around it.
                    if (a.hold == Vector3.zero || Vector3.Distance(a.hold, mm.ChargePosition) > 18f) a.hold = PostPlantSpot(mm.ChargePosition);
                    o.label = BotState.GuardSite; o.position = a.hold; o.tolerance = 1f; o.crouch = difficulty >= BotDifficulty.Hard;
                    o.look = mm.ChargePosition + (map.wardenSpawnCenter - mm.ChargePosition).normalized * 8f + Vector3.up * 1.4f;
                    return o;
                }

                int site = a.faker && Time.time < executeAt + 6f ? 1 - targetSite : targetSite;
                bool carrier = b.actor.weapons.hasCharge;
                if (!executing && !carrier || (!executing && carrier && strategy != Strategy.Rush))
                {
                    o.label = BotState.Advance; o.position = a.stage; o.tolerance = 2.5f;
                    o.walk = difficulty >= BotDifficulty.Hard && Vector3.Distance(b.transform.position, a.stage) < 18f && !a.faker;
                    o.look = map.SiteCenter(site) + Vector3.up * 1.4f;
                    return o;
                }
                if (carrier)
                {
                    // Plant once inside the site; wait for a moment of quiet unless time is short.
                    o.label = mm.Map.SiteAt(b.transform.position) == site && !b.targetVisible ? BotState.Plant : BotState.Advance;
                    o.position = PlantSpot(site); o.tolerance = 1f; o.look = o.position;
                    return o;
                }
                o.label = BotState.Advance; o.position = a.hold; o.tolerance = 1.5f;
                o.look = map.SiteCenter(site) + (map.wardenSpawnCenter - map.SiteCenter(site)).normalized * 6f + Vector3.up * 1.4f;
                if (!a.utilityDone && difficulty >= BotDifficulty.Hard && Vector3.Distance(b.transform.position, map.SiteCenter(site)) < 26f)
                {
                    a.utilityDone = true;
                    bool smoke = b.actor.weapons.smokeCount > 0;
                    o.preferSmoke = smoke;
                    // Smoke the defenders' rotation path, flash into the site
                    o.utilityTarget = smoke ? map.SiteCenter(site) + (map.wardenSpawnCenter - map.SiteCenter(site)).normalized * 7f : map.SiteCenter(site) + Vector3.up;
                }
                return o;
            }

            // ---------------- Wardens
            if (mm.ChargePlanted)
            {
                Vector3 c = mm.ChargePosition;
                bool groupFirst = difficulty >= BotDifficulty.Hard && Time.time - plantSeenTime < (difficulty == BotDifficulty.Expert ? 6f : 4f) && mm.ChargeTimeLeft > 18f;
                if (groupFirst)
                {
                    Vector3 stage = c + (map.wardenSpawnCenter - c).normalized * 14f;
                    o.label = BotState.Rotate; o.position = Jitter(stage, 2f); o.tolerance = 3f; o.look = c + Vector3.up * 1.4f; o.walk = true;
                    return o;
                }
                bool defuser = IsNearestWardenTo(b, c) || mm.ChargeTimeLeft < 14f;
                bool clear = !b.targetVisible && (!b.profile.checksBeforeDefuse || RecentIntelNear(c, 15f, 3f) == 0 || mm.ChargeTimeLeft < (b.actor.defuseKit ? 9f : 14f));
                if (defuser && clear)
                {
                    o.label = BotState.Defuse; o.position = c; o.tolerance = 0.8f; o.look = c;
                    return o;
                }
                o.label = BotState.Rotate; o.position = Jitter(c + Random.insideUnitSphere.Flat() * 4f, 1f); o.tolerance = 3f; o.look = c + Vector3.up * 1.4f;
                return o;
            }

            o.label = a.lurker ? BotState.Patrol : BotState.GuardSite;
            o.position = a.hold; o.tolerance = 1f;
            o.look = a.face == Vector3.zero ? ThreatFacing(a.hold, a.site) : a.face;
            o.crouch = difficulty >= BotDifficulty.Hard && Random.value < 0.5f;
            if (a.lurker && roundAge > 30f) { a.lurker = false; a.hold = GuardSpot(a.site, 1); }
            return o;
        }

        BotObjective Hunt(BotBrain b, BotObjective o)
        {
            // Deathmatch / practice: move toward fresh intel, otherwise sweep lanes.
            var known = LatestIntel(12f);
            if (!assign.TryGetValue(b, out var a)) { a = new Assignment(); assign[b] = a; }
            if (known.HasValue && Random.value < 0.5f + teamwork * 0.3f) { o.position = known.Value; o.walk = difficulty >= BotDifficulty.Hard && Vector3.Distance(known.Value, b.transform.position) < 15f; }
            else
            {
                if (a.hold == Vector3.zero || Vector3.Distance(a.hold, b.transform.position) < 3f) a.hold = Jitter(map.RandomFloor(), 1f);
                o.position = a.hold;
            }
            o.label = BotState.Patrol; o.tolerance = 2f;
            o.look = o.position + Vector3.up * 1.4f;
            return o;
        }

        Vector3 PlantSpot(int site)
        {
            var cells = map.SiteCells(site);
            Vector3 c = map.SiteCenter(site), best = cells[0];
            float bd = float.MaxValue;
            foreach (var p in cells) { float d = Vector3.Distance(p, c); if (d < bd) { bd = d; best = p; } }
            return best;
        }

        Vector3 PostPlantSpot(Vector3 charge)
        {
            var options = new List<Vector3>();
            foreach (var p in map.platforms) if (Vector3.Distance(p, charge) < 18f) options.Add(p);
            foreach (var g in map.guardSpots) if (Vector3.Distance(g, charge) < 18f) options.Add(g);
            foreach (var f in map.allFloor) if (Vector3.Distance(f, charge) > 6f && Vector3.Distance(f, charge) < 14f && Random.value < 0.1f) options.Add(f);
            if (options.Count == 0) return Jitter(charge, 4f);
            return Jitter(options[Random.Range(0, options.Count)], 0.5f);
        }

        bool IsNearestRaiderTo(BotBrain b, Vector3 p)
        {
            float mine = Vector3.Distance(b.transform.position, p);
            foreach (var kv in assign) if (kv.Key != b && kv.Key.actor.alive && Vector3.Distance(kv.Key.transform.position, p) < mine - 0.5f) return false;
            var player = MatchManager.I.Player;
            if (player != null && player.alive && player.team == team && Vector3.Distance(player.transform.position, p) < mine - 3f) return false;
            return true;
        }

        bool IsNearestWardenTo(BotBrain b, Vector3 p)
        {
            float mine = Vector3.Distance(b.transform.position, p) - (b.actor.defuseKit ? 5f : 0f);
            foreach (var kv in assign)
            {
                if (kv.Key == b || !kv.Key.actor.alive) continue;
                float d = Vector3.Distance(kv.Key.transform.position, p) - (kv.Key.actor.defuseKit ? 5f : 0f);
                if (d < mine - 0.5f) return false;
            }
            var player = MatchManager.I.Player;
            if (player != null && player.alive && player.team == team && Vector3.Distance(player.transform.position, p) < 4f) return false;
            return true;
        }
    }
}
