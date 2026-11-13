using UnityEngine;
using UnityEngine.AI;

namespace Wardline
{
    public enum BotState { Idle, Advance, Follow, Search, Investigate, Engage, TakeCover, Retreat, ThrowGrenade, GuardSite, Plant, Defuse, RecoverCharge, Rotate, HoldPosition, Patrol, Practice }
    public enum BotOrder { None, FollowLeader, Hold, Regroup }

    /// <summary>
    /// Finite-state bot AI. Perception (vision cone + line of sight + smoke + flash blindness), hearing,
    /// human-like reaction delays and aim settling, recoil control, burst discipline, strafing, cover,
    /// grenades, objectives (plant/defuse/recover/guard/rotate) and team orders. Bots never read hidden
    /// positions: everything they know comes from sight, sound, damage direction or teammate callouts.
    /// </summary>
    public class BotBrain : MonoBehaviour
    {
        public Actor actor;
        public NavMeshAgent agent;
        public BotProfile profile;
        public TeamCoordinator coord;
        public BotState state = BotState.Idle;
        public BotOrder order = BotOrder.None;
        public Actor orderLeader;
        public Vector3 holdPosition;
        public PracticeBotMode practiceMode = PracticeBotMode.Fighting;
        public bool practice;

        public float RecoilLeak => 1f - profile.recoilControl;

        // Perception & memory
        public Actor target;
        public bool targetVisible;
        float reactionReady, engageStart;
        public Vector3 lastKnown;
        Vector3 lastKnownVel;
        public float lastSeen = -99f;
        Vector3 heardPos;
        float heardTime = -99f, heardScan;
        float nextPerception, nextThink;
        float lastCallout = -99f;

        // Aim
        Vector3 noiseSeed;
        bool aimHead;
        float nextAimRoll;
        int burstStartShots;
        float burstPauseUntil, nextTap;

        // Movement
        Vector3 dest;
        bool hasDest;
        float nextRepath, stuckTimer, strafeUntil, coverUntil, retreatUntil;
        Vector3 strafePoint, coverPoint;
        float grenadeCooldown, lookAwayUntil, nextStep;
        bool wantWalk, wantCrouch;
        Vector3 lookPoint;
        bool hasLookPoint;
        float practiceNextMove;
        Vector3 practiceAnchor;

        public static BotBrain Attach(Actor actor, BotProfile profile)
        {
            var b = actor.gameObject.AddComponent<BotBrain>();
            b.actor = actor; actor.bot = b; b.profile = profile;
            b.agent = actor.gameObject.AddComponent<NavMeshAgent>();
            b.agent.radius = 0.35f; b.agent.height = 1.8f; b.agent.baseOffset = 0f;
            b.agent.acceleration = 40f; b.agent.angularSpeed = 720f; b.agent.stoppingDistance = 0.35f;
            b.agent.autoBraking = true; b.agent.updateRotation = false; b.agent.updateUpAxis = true;
            b.agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            b.agent.avoidancePriority = Random.Range(30, 70);
            b.agent.enabled = false;
            var col = actor.gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.8f; col.radius = 0.32f; col.center = new Vector3(0, 0.9f, 0);
            actor.gameObject.layer = Layers.Actor;
            b.noiseSeed = new Vector3(Random.value * 100f, Random.value * 100f, Random.value * 100f);
            actor.weapons.muzzleProvider = () => actor.model.muzzle.position;
            return b;
        }

        public void OnRespawn(Vector3 pos)
        {
            agent.enabled = true;
            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas)) agent.Warp(hit.position); else agent.Warp(pos);
            target = null; targetVisible = false; lastSeen = -99f; heardTime = -99f;
            state = BotState.Idle; hasDest = false; order = BotOrder.None; orderLeader = null;
            stuckTimer = 0f; grenadeCooldown = Time.time + 5f;
            practiceAnchor = pos;
            actor.weapons.zoomLevel = 0;
        }

        public void SetProfile(BotProfile p) { profile = p; }

        // ---------------------------------------------------------------- Update
        void Update()
        {
            if (!actor.alive)
            {
                if (agent.enabled) { agent.enabled = false; }
                actor.velocity = Vector3.zero;
                return;
            }
            if (!agent.enabled) return;
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out var h, 6f, NavMesh.AllAreas)) agent.Warp(h.position);
                return;
            }

            float now = Time.time;
            if (actor.InFreeze)
            {
                agent.isStopped = true;
                actor.velocity = Vector3.zero;
                LookIdle();
                return;
            }

            if (practice && practiceMode != PracticeBotMode.Fighting) { PracticeUpdate(); FinishFrame(); return; }

            if (now >= nextPerception) { Perceive(); nextPerception = now + profile.perceptionInterval * Random.Range(0.85f, 1.15f); }
            if (now >= nextThink) { Think(); nextThink = now + 0.2f; }
            Act();
            FinishFrame();
        }

        void FinishFrame()
        {
            float dt = Time.deltaTime;
            // Movement state for the accuracy model, animation and footsteps
            float baseSpeed = actor.MaxSpeed * (wantCrouch ? Actor.CrouchFactor : wantWalk ? Actor.WalkFactor : 1f);
            agent.speed = baseSpeed;
            actor.crouching = wantCrouch;
            actor.walking = wantWalk || wantCrouch;
            actor.velocity = agent.velocity;
            actor.grounded = true;
            transform.rotation = Quaternion.Euler(0, actor.yaw, 0);
            actor.aimDirection = Quaternion.Euler(actor.pitch, actor.yaw, 0) * Vector3.forward;

            float hs = actor.HorizontalSpeed;
            if (hs > Actor.RunSpeed * 0.6f && !actor.walking && Time.time > nextStep)
            {
                nextStep = Time.time + 2.3f / Mathf.Max(hs, 0.1f);
                AudioManager.Play3D("step" + Random.Range(1, 5), transform.position, 0.5f, 22f, 1f, SoundCategory.Footsteps);
                Hearing.Emit(transform.position, 22f, NoiseType.Footstep, actor);
            }

            // Stuck detection & recovery: re-path, then nudge, then warp along the path as a last resort.
            if (hasDest && !agent.isStopped && agent.hasPath && agent.remainingDistance > 1f && hs < 0.25f)
            {
                stuckTimer += dt;
                if (stuckTimer > 1.5f && stuckTimer - dt <= 1.5f) { agent.ResetPath(); agent.SetDestination(dest + Random.insideUnitSphere.Flat() * 1.5f); }
                if (stuckTimer > 6f)
                {
                    Vector3 corner = agent.steeringTarget;
                    if (NavMesh.SamplePosition(Vector3.MoveTowards(transform.position, corner, 1.5f), out var h, 2f, NavMesh.AllAreas)) agent.Warp(h.position);
                    agent.SetDestination(dest);
                    stuckTimer = 0f;
                }
            }
            else stuckTimer = Mathf.Max(0f, stuckTimer - dt);

            Pickup.AutoCollect(actor);
        }

        // ---------------------------------------------------------------- Perception
        bool CanSee(Actor other, out float angle)
        {
            Vector3 eye = actor.EyePosition;
            Vector3 to = other.ChestPosition - eye;
            angle = Vector3.Angle(actor.aimDirection, to);
            float d = to.magnitude;
            if (d > 95f) return false;
            bool inContact = d < 1.6f;
            if (angle > profile.fov * 0.5f && !inContact) return false;
            if (SmokeCloud.DensityAt(eye) > 0.6f && d > 2.5f) return false;
            return Clear(eye, other.HeadPosition) || Clear(eye, other.ChestPosition) || Clear(eye, other.transform.position + Vector3.up * 0.45f);
        }

        static bool Clear(Vector3 a, Vector3 b) => !Physics.Linecast(a, b, Layers.VisionMask, QueryTriggerInteraction.Collide);

        void Perceive()
        {
            float now = Time.time;
            if (actor.IsBlind) { targetVisible = false; return; }

            Actor best = null; float bestScore = float.MaxValue, bestAngle = 0f;
            foreach (var e in Actor.All)
            {
                if (!e.alive || e == actor || !IsEnemy(e)) continue;
                if (!CanSee(e, out float ang)) continue;
                float d = Vector3.Distance(e.transform.position, transform.position);
                float score = d + ang * 0.25f + (e == target ? -8f : 0f);
                if (score < bestScore) { bestScore = score; best = e; bestAngle = ang; }
            }

            if (best != null)
            {
                bool fresh = best != target || now - lastSeen > 1.2f;
                if (fresh)
                {
                    float r = profile.Reaction();
                    if (bestAngle > 35f) r *= 1.3f;                        // peripheral spot takes longer
                    if (actor.HorizontalSpeed > 3f) r *= 1.1f;
                    if (now - lastSeen < 3f && best == target) r *= 0.5f;   // re-acquiring a known target
                    reactionReady = now + r;
                    engageStart = now;
                    aimHead = Random.value < profile.headChance;
                    burstStartShots = actor.weapons.shotsFired;
                    if (coord != null) coord.Report(best.transform.position, this);
                    Callout(best.transform.position);
                }
                lastKnownVel = best.velocity;
                target = best; targetVisible = true;
                lastKnown = best.transform.position; lastSeen = now;
            }
            else targetVisible = false;

            // Hearing: only position and kind of the sound — never identity or continuous tracking.
            if (now - heardScan > 0.15f)
            {
                heardScan = now;
                float bestLoud = 0f;
                foreach (var ev in Hearing.Events)
                {
                    if (ev.source == actor || now - ev.time > 1f) continue;
                    if (ev.source != null && !IsEnemy(ev.source)) continue;
                    if (ev.type == NoiseType.Footstep || ev.type == NoiseType.Land || ev.type == NoiseType.Reload)
                        if (!profile.hearsFootsteps) continue;
                    float d = Vector3.Distance(ev.position, transform.position);
                    float radius = ev.radius * profile.hearingMult;
                    if (d > radius) continue;
                    float loud = 1f - d / radius + (ev.type == NoiseType.Gunshot ? 0.5f : 0f);
                    if (loud > bestLoud) { bestLoud = loud; heardPos = ev.position; heardTime = ev.time; }
                }
            }

            // See incoming enemy flashbangs and turn away (Hard/Expert).
            if (profile.turnsFromFlash && now > lookAwayUntil)
            {
                foreach (var g in Grenade.Live)
                {
                    if (g == null || g.type != GrenadeType.Flash || g.owner == null || !IsEnemy(g.owner)) continue;
                    if (Vector3.Distance(g.transform.position, actor.EyePosition) > 16f) continue;
                    if (!Clear(actor.EyePosition, g.transform.position)) continue;
                    if (Random.value < (profile.difficulty == BotDifficulty.Expert ? 0.85f : 0.5f)) lookAwayUntil = now + 1.4f;
                    break;
                }
            }
        }

        bool IsEnemy(Actor other) => other.team != actor.team;

        void Callout(Vector3 pos)
        {
            if (Time.time - lastCallout < 8f || MatchManager.I == null) return;
            var player = MatchManager.I.Player;
            if (player == null || player.team != actor.team || MatchManager.I.Config.mode == GameMode.Practice) return;
            if (TeamCoordinator.RecentCallout(actor.team, pos)) return;
            lastCallout = Time.time;
            GameEvents.RaiseRadio(actor, "Enemy spotted " + MatchManager.I.Map.AreaName(pos) + ".");
        }

        public void OnDamaged(DamageInfo info)
        {
            // Damage reveals the direction of the shooter, like the player's damage indicator.
            if (!actor.alive) return;
            if (!targetVisible)
            {
                heardPos = info.sourcePosition; heardTime = Time.time;
                if (profile.difficulty >= BotDifficulty.Normal) { lastKnown = info.sourcePosition; lastSeen = Mathf.Max(lastSeen, Time.time - 3f); }
            }
            if (profile.coverUse > 0f && actor.health < 40f && Random.value < profile.coverUse * 0.6f) retreatUntil = Time.time + 2.5f;
        }

        // ---------------------------------------------------------------- Decision making
        void Think()
        {
            float now = Time.time;
            var prev = state;

            if (targetVisible && target != null && target.alive)
            {
                if (now < coverUntil) state = BotState.TakeCover;
                else if (now < retreatUntil) state = BotState.Retreat;
                else
                {
                    state = BotState.Engage;
                    bool needCover = actor.weapons.reloading || actor.health < 35f;
                    if (needCover && Random.value < profile.coverUse * 0.5f && FindCover(target.EyePosition, out coverPoint))
                    {
                        coverUntil = now + Random.Range(1.6f, 3f);
                        state = BotState.TakeCover;
                    }
                }
            }
            else if (now < coverUntil) state = BotState.TakeCover;
            else if (target != null && target.alive && now - lastSeen < (profile.difficulty == BotDifficulty.Easy ? 3f : 7f) && !ObjectiveIsUrgent())
                state = BotState.Search;
            else if (now - heardTime < 4f && !ObjectiveIsUrgent() && (profile.difficulty != BotDifficulty.Easy || Random.value < 0.5f))
                state = BotState.Investigate;
            else if (order == BotOrder.FollowLeader && orderLeader != null && orderLeader.alive) state = BotState.Follow;
            else if (order == BotOrder.Hold) state = BotState.HoldPosition;
            else state = BotState.Advance;   // objective-driven (coordinator decides the specifics)

            if (target != null && !target.alive) { target = null; targetVisible = false; }

            // Reload during quiet moments
            var w = actor.weapons;
            if (!targetVisible && !w.reloading && w.Current != null && w.Current.def.IsGun && w.Current.mag < w.Current.def.magazine * 0.5f && now - lastSeen > 1.5f) w.Reload();

            // Weapon choice: guns for fights, knife only if nothing else has ammo
            if (state == BotState.Engage || state == BotState.Search) EnsureGun();

            // Utility grenades on known-but-hidden enemies
            if ((state == BotState.Search || state == BotState.Investigate) && now > grenadeCooldown && profile.grenadeUse > 0f)
            {
                Vector3 spot = state == BotState.Search ? lastKnown : heardPos;
                float d = Vector3.Distance(spot, transform.position);
                if (d > 7f && d < 26f && Random.value < profile.grenadeUse * 0.35f)
                {
                    if (w.heCount > 0 && ThrowAt(GrenadeType.HE, spot)) grenadeCooldown = now + 6f;
                    else if (w.flashCount > 0 && d < 18f && ThrowAt(GrenadeType.Flash, spot)) { grenadeCooldown = now + 6f; lookAwayUntil = now + 1.2f; }
                }
                else grenadeCooldown = now + 1.5f;
            }

            if (prev != state && state != BotState.Engage) hasDest = false;
        }

        bool ObjectiveIsUrgent()
        {
            var mm = MatchManager.I;
            if (mm == null || mm.Config.mode != GameMode.Classic) return false;
            if (actor.team == Team.Raiders && actor.weapons.hasCharge && mm.RoundTimeLeft < 25f) return true;
            if (actor.team == Team.Wardens && mm.ChargePlanted && mm.ChargeTimeLeft < (actor.defuseKit ? 12f : 17f)) return true;
            return false;
        }

        void EnsureGun()
        {
            var w = actor.weapons;
            if (w.slot == WeaponSlot.Primary && w.primary != null && (w.primary.mag > 0 || w.primary.reserve > 0)) return;
            if (w.primary != null && (w.primary.mag > 0 || w.primary.reserve > 0)) { w.Equip(WeaponSlot.Primary); return; }
            if (w.slot == WeaponSlot.Secondary && w.secondary != null && (w.secondary.mag > 0 || w.secondary.reserve > 0)) return;
            if (w.secondary != null && (w.secondary.mag > 0 || w.secondary.reserve > 0)) { w.Equip(WeaponSlot.Secondary); return; }
            if (w.slot != WeaponSlot.Knife) w.Equip(WeaponSlot.Knife);
        }

        // ---------------------------------------------------------------- Acting
        void Act()
        {
            float now = Time.time;
            wantCrouch = false;
            wantWalk = false;
            agent.isStopped = false;

            if (actor.IsBlind)
            {
                // Blinded: Easy bots freeze, better bots back off and spray where the enemy was.
                agent.isStopped = profile.difficulty == BotDifficulty.Easy;
                if (!agent.isStopped) MoveTo(transform.position - actor.aimDirection.Flat().normalized * 3f, false);
                if (profile.difficulty >= BotDifficulty.Hard && now - lastSeen < 2f && actor.weapons.Current != null && actor.weapons.Current.def.IsGun)
                {
                    TurnTowards(lastKnown + Vector3.up * 1.3f, profile.aimSpeed * 0.3f);
                    actor.weapons.TriggerUpdate(true, true, false);
                }
                return;
            }

            switch (state)
            {
                case BotState.Engage: EngageUpdate(); return;
                case BotState.TakeCover:
                    MoveTo(coverPoint, false);
                    if (target != null) TurnTowards(lastKnown + Vector3.up * 1.4f, profile.aimSpeed * 0.6f);
                    if (!actor.weapons.reloading && actor.weapons.Current != null && actor.weapons.Current.def.IsGun && actor.weapons.Current.mag < actor.weapons.Current.def.magazine) actor.weapons.Reload();
                    if (targetVisible && now > reactionReady) FireControl(Vector3.Distance(target.transform.position, transform.position));
                    return;
                case BotState.Retreat:
                {
                    Vector3 away = transform.position + (transform.position - lastKnown).Flat().normalized * 6f;
                    MoveTo(away, false);
                    if (targetVisible) { AimAtTarget(); if (now > reactionReady) FireControl(Vector3.Distance(target.transform.position, transform.position)); }
                    return;
                }
                case BotState.Search:
                {
                    Vector3 guess = lastKnown;
                    if (profile.predictsMovement && lastKnownVel.sqrMagnitude > 1f)
                    {
                        Vector3 pred = lastKnown + lastKnownVel.Flat() * Mathf.Clamp(now - lastSeen, 0f, 1.2f);
                        if (NavMesh.SamplePosition(pred, out var h, 2f, NavMesh.AllAreas)) guess = h.position;
                    }
                    float d = Vector3.Distance(guess, transform.position);
                    wantWalk = profile.difficulty >= BotDifficulty.Hard && d < 16f;
                    if (d > 1.5f) MoveTo(guess, wantWalk); else agent.isStopped = true;
                    TurnTowards(guess + Vector3.up * 1.45f, profile.aimSpeed * 0.7f);   // pre-aim where they were
                    if (d < 2f && now - lastSeen > 3f) { lastSeen = -99f; target = null; }
                    return;
                }
                case BotState.Investigate:
                {
                    float d = Vector3.Distance(heardPos, transform.position);
                    wantWalk = profile.difficulty >= BotDifficulty.Normal && d < 20f;
                    if (d > 2f) MoveTo(heardPos, wantWalk); else { agent.isStopped = true; heardTime = -99f; }
                    TurnTowards(heardPos + Vector3.up * 1.4f, profile.aimSpeed * 0.6f);
                    return;
                }
                case BotState.Follow:
                {
                    Vector3 spot = orderLeader.transform.position - orderLeader.aimDirection.Flat().normalized * 2.5f + orderLeader.transform.right * (actor.GetInstanceID() % 2 == 0 ? 1.5f : -1.5f);
                    if (Vector3.Distance(spot, transform.position) > 1.5f) MoveTo(spot, orderLeader.walking); else agent.isStopped = true;
                    LookAlongPathOr(orderLeader.EyePosition + orderLeader.aimDirection * 10f);
                    return;
                }
                case BotState.HoldPosition:
                {
                    if (Vector3.Distance(holdPosition, transform.position) > 1f) MoveTo(holdPosition, false); else agent.isStopped = true;
                    LookAlongPathOr(holdPosition + (MatchManager.I != null ? MatchManager.I.ThreatDirection(actor.team, holdPosition) : transform.forward) * 10f + Vector3.up * 1.4f);
                    wantCrouch = agent.isStopped && profile.difficulty >= BotDifficulty.Hard;
                    return;
                }
                default:
                    ObjectiveUpdate();
                    return;
            }
        }

        void ObjectiveUpdate()
        {
            if (coord == null) { agent.isStopped = true; LookIdle(); return; }
            var obj = coord.GetObjective(this);
            state = obj.label;
            wantWalk = obj.walk;
            wantCrouch = obj.crouch && agent.isStopped;
            var mm = MatchManager.I;

            if (obj.label == BotState.Plant || obj.label == BotState.Defuse)
            {
                float d = Vector3.Distance(obj.position.Flat(), transform.position.Flat());
                bool atSpot = obj.label == BotState.Plant ? mm.Map.SiteAt(transform.position) >= 0 && d < 3.5f : d < 1.2f;
                if (!atSpot) { MoveTo(obj.position, false); LookAlongPathOr(obj.look); mm.InteractUpdate(actor, false, false); }
                else
                {
                    agent.isStopped = true;
                    if (obj.label == BotState.Plant && actor.weapons.slot != WeaponSlot.Bomb) actor.weapons.Equip(WeaponSlot.Bomb, true);
                    TurnTowards(obj.position + Vector3.down * 0.5f, 120f);
                    mm.InteractUpdate(actor, true, false);
                }
                return;
            }

            if (mm != null) mm.InteractUpdate(actor, false, false);
            if (actor.weapons.slot == WeaponSlot.Bomb || actor.weapons.slot == WeaponSlot.Knife && (actor.weapons.primary != null || actor.weapons.secondary != null)) EnsureGun();

            // Execute utility (smokes/flashes) once when the coordinator calls the push.
            if (obj.utilityTarget.HasValue && Time.time > grenadeCooldown)
            {
                var w = actor.weapons;
                Vector3 ut = obj.utilityTarget.Value;
                if (obj.preferSmoke && w.smokeCount > 0 && ThrowAt(GrenadeType.Smoke, ut)) grenadeCooldown = Time.time + 3f;
                else if (w.flashCount > 0 && Vector3.Distance(ut, transform.position) < 24f && ThrowAt(GrenadeType.Flash, ut)) { grenadeCooldown = Time.time + 3f; lookAwayUntil = Time.time + 1.3f; }
                else grenadeCooldown = Time.time + 2f;
            }

            if (Vector3.Distance(obj.position.Flat(), transform.position.Flat()) > obj.tolerance) MoveTo(obj.position, obj.walk);
            else { agent.isStopped = true; hasDest = false; }
            LookAlongPathOr(obj.look);
        }

        void EngageUpdate()
        {
            float now = Time.time;
            float dist = Vector3.Distance(target.transform.position, transform.position);
            var def = actor.weapons.CurrentDef;
            AimAtTarget();

            // Scope in with snipers at range
            if (def != null && def.HasScope && dist > 9f && actor.weapons.zoomLevel == 0 && !actor.weapons.reloading && now > reactionReady - 0.15f) actor.weapons.ToggleZoom();
            if (def != null && def.HasScope && dist < 6f && actor.weapons.zoomLevel > 0 && def.category != WeaponCategory.Sniper) actor.weapons.ToggleZoom();

            bool firing = now > reactionReady && FireControl(dist);

            // Movement while fighting
            bool close = def != null && (def.category == WeaponCategory.Knife || def.category == WeaponCategory.Shotgun || def.category == WeaponCategory.SMG);
            bool sniper = def != null && def.category == WeaponCategory.Sniper;
            if (def != null && def.category == WeaponCategory.Knife) { MoveTo(target.transform.position, false); return; }

            if (firing && profile.strafeSkill > 0.6f && dist > 7f)
            {
                agent.isStopped = true;   // stop to shoot accurately
                wantCrouch = profile.difficulty >= BotDifficulty.Hard && dist > 22f && !sniper && (actor.GetInstanceID() & 1) == 0;
                return;
            }
            if (sniper) { agent.isStopped = firing || dist > 12f; return; }

            if (Random.value < profile.strafeSkill || now < strafeUntil)
            {
                if (now >= strafeUntil)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, (target.transform.position - transform.position).Flat().normalized) * (Random.value < 0.5f ? -1f : 1f);
                    Vector3 p = transform.position + side * Random.Range(1.5f, 3f) + (close ? (target.transform.position - transform.position).Flat().normalized * 1.5f : Vector3.zero);
                    if (NavMesh.SamplePosition(p, out var h, 1.5f, NavMesh.AllAreas)) strafePoint = h.position; else strafePoint = transform.position;
                    strafeUntil = now + Random.Range(0.5f, 1.1f);
                }
                MoveTo(strafePoint, false);
            }
            else if (close || dist > (def != null ? def.effectiveRange * 0.8f : 20f) || profile.difficulty == BotDifficulty.Easy && Random.value < 0.01f)
                MoveTo(target.transform.position, false);
            else agent.isStopped = true;
        }

        void AimAtTarget()
        {
            float now = Time.time;
            if (now > nextAimRoll) { aimHead = Random.value < profile.headChance; nextAimRoll = now + Random.Range(0.8f, 1.6f); }
            Vector3 point = aimHead ? target.HeadPosition : target.ChestPosition;
            float settle = Mathf.Clamp01((now - engageStart) / Mathf.Max(0.05f, profile.settleTime));
            float err = Mathf.Lerp(profile.errorStart, profile.errorSettled, settle);
            if (target.HorizontalSpeed > 2f) err *= 1.3f;
            if (actor.HorizontalSpeed > 2f) err *= 1.35f;
            if (now - actor.lastHurtTime < 0.4f) err *= 1.3f;
            Vector3 n = new Vector3(Mathf.PerlinNoise(noiseSeed.x, now * 0.9f) - 0.5f, Mathf.PerlinNoise(noiseSeed.y, now * 0.9f) - 0.5f, Mathf.PerlinNoise(noiseSeed.z, now * 0.9f) - 0.5f) * 2f;
            point += n * err;
            if (lookAwayUntil > now) { TurnTowards(transform.position - actor.aimDirection.Flat() * 5f, profile.aimSpeed); return; }
            TurnTowards(point, profile.aimSpeed);
        }

        /// <summary>Decides whether to pull the trigger this frame. Returns true while actively shooting.</summary>
        bool FireControl(float dist)
        {
            float now = Time.time;
            var w = actor.weapons;
            var def = w.CurrentDef;
            if (def == null || w.slot == WeaponSlot.Grenade || w.slot == WeaponSlot.Bomb) { EnsureGun(); return false; }
            if (!targetVisible || target == null) return false;

            Vector3 eye = actor.EyePosition;
            Vector3 tp = aimHead ? target.HeadPosition : target.ChestPosition;
            float angle = Vector3.Angle(actor.aimDirection, tp - eye);
            float tol = Mathf.Atan2(0.5f, Mathf.Max(dist, 0.5f)) * Mathf.Rad2Deg + 0.6f;
            if (def.category == WeaponCategory.Knife) return dist < 1.8f && w.TriggerUpdate(true, true, false);
            if (angle > tol) return false;
            if (def.category == WeaponCategory.Sniper && now - engageStart < profile.reactionMax + 0.2f) return false;

            // Don't shoot through teammates
            if (Ballistics.Trace(actor, eye, actor.aimDirection, dist, out _, out var hb) && hb != null && hb.actor.team == actor.team && !practice) return false;

            if (def.fireMode == FireMode.Auto && dist > 14f && profile.burstDiscipline > 0f)
            {
                int burstLen = Mathf.RoundToInt(Mathf.Lerp(14f, dist > 30f ? 2f : 4f, profile.burstDiscipline));
                if (now < burstPauseUntil) return false;
                if (w.shotsFired - burstStartShots >= burstLen)
                {
                    burstPauseUntil = now + Mathf.Lerp(0.15f, 0.38f, profile.burstDiscipline);
                    burstStartShots = w.shotsFired;
                    return false;
                }
            }
            if (def.fireMode != FireMode.Auto)
            {
                if (now < nextTap) return false;
                bool shot = w.TriggerUpdate(true, true, false);
                if (shot) nextTap = now + (1f - profile.burstDiscipline) * 0.35f + 0.06f;
                return true;
            }
            w.TriggerUpdate(true, true, false);
            return true;
        }

        // ---------------------------------------------------------------- Helpers
        public void MoveTo(Vector3 p, bool walk)
        {
            wantWalk = wantWalk || walk;
            agent.isStopped = false;
            if (!hasDest || (p - dest).sqrMagnitude > 1f || Time.time > nextRepath)
            {
                dest = p; hasDest = true;
                nextRepath = Time.time + 1.2f;
                if (NavMesh.SamplePosition(p, out var h, 3f, NavMesh.AllAreas)) agent.SetDestination(h.position);
                else agent.SetDestination(p);
            }
        }

        void TurnTowards(Vector3 point, float speed)
        {
            Vector3 to = point - actor.EyePosition;
            if (to.sqrMagnitude < 0.0001f) return;
            var q = Quaternion.LookRotation(to);
            float dy = q.eulerAngles.y, dp = q.eulerAngles.x;
            if (dp > 180f) dp -= 360f;
            float dt = Time.deltaTime;
            // Faster flicks for large corrections, finer tracking near the target.
            float diff = Mathf.Abs(Mathf.DeltaAngle(actor.yaw, dy)) + Mathf.Abs(dp - actor.pitch);
            float step = speed * dt * Mathf.Lerp(0.45f, 1.25f, Mathf.Clamp01(diff / 45f));
            actor.yaw = Mathf.MoveTowardsAngle(actor.yaw, dy, step);
            actor.pitch = Mathf.MoveTowards(actor.pitch, Mathf.Clamp(dp, -80f, 80f), step);
        }

        void LookAlongPathOr(Vector3 fallback)
        {
            if (Time.time < lookAwayUntil) { TurnTowards(transform.position - actor.aimDirection.Flat() * 5f + Vector3.up * 1.5f, 300f); return; }
            Vector3 v = agent.velocity.Flat();
            if (v.sqrMagnitude > 1f && !agent.isStopped && (agent.steeringTarget - transform.position).sqrMagnitude > 4f)
                TurnTowards(actor.EyePosition + v.normalized * 10f, 220f);
            else TurnTowards(fallback, 180f);
        }

        void LookIdle()
        {
            var mm = MatchManager.I;
            Vector3 dir = mm != null ? mm.ThreatDirection(actor.team, transform.position) : transform.forward;
            TurnTowards(actor.EyePosition + dir * 10f, 90f);
            transform.rotation = Quaternion.Euler(0, actor.yaw, 0);
            actor.aimDirection = Quaternion.Euler(actor.pitch, actor.yaw, 0) * Vector3.forward;
        }

        bool FindCover(Vector3 threatEye, out Vector3 point)
        {
            point = transform.position;
            float best = float.MaxValue;
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f;
                for (int r = 3; r <= 9; r += 3)
                {
                    Vector3 p = transform.position + Quaternion.Euler(0, ang, 0) * Vector3.forward * r;
                    if (!NavMesh.SamplePosition(p, out var h, 1.2f, NavMesh.AllAreas)) continue;
                    Vector3 head = h.position + Vector3.up * 1.5f;
                    if (Clear(threatEye, head)) continue;   // still exposed
                    float score = Vector3.Distance(h.position, transform.position) - Vector3.Distance(h.position, threatEye) * 0.3f;
                    if (score < best) { best = score; point = h.position; }
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>Ballistic solve: launch velocity so a grenade lands near the target point.</summary>
        bool ThrowAt(GrenadeType type, Vector3 targetPoint)
        {
            Vector3 start = actor.EyePosition;
            Vector3 delta = targetPoint - start;
            Vector3 flat = delta.Flat();
            float dx = flat.magnitude, dy = delta.y;
            if (dx < 2f) return false;
            const float g = 15f;
            float v = Mathf.Clamp(dx * 0.9f + 6f, 8f, 17f);
            float v2 = v * v;
            float disc = v2 * v2 - g * (g * dx * dx + 2f * dy * v2);
            float angle;
            if (disc < 0f) angle = 45f * Mathf.Deg2Rad;
            else angle = Mathf.Atan((v2 + Mathf.Sqrt(disc)) / (g * dx));   // high arc clears cover
            if (angle > 70f * Mathf.Deg2Rad) angle = Mathf.Atan((v2 - Mathf.Sqrt(Mathf.Max(0, disc))) / (g * dx));
            Vector3 vel = flat.normalized * v * Mathf.Cos(angle) + Vector3.up * v * Mathf.Sin(angle);
            // Errors on lower difficulties
            vel += Random.insideUnitSphere * (1f - profile.grenadeUse) * 1.5f;
            var prevSlot = actor.weapons.slot;
            TurnTowards(targetPoint, 999f);
            bool ok = actor.weapons.ThrowGrenadeAt(type, vel);
            if (ok && actor.weapons.HasSlot(prevSlot) && prevSlot != WeaponSlot.Grenade) actor.weapons.Equip(prevSlot, true);
            return ok;
        }

        // ---------------------------------------------------------------- Practice targets
        void PracticeUpdate()
        {
            agent.isStopped = practiceMode == PracticeBotMode.Stationary;
            var p = MatchManager.I != null ? MatchManager.I.Player : null;
            if (p != null) TurnTowards(p.EyePosition, 120f);
            if (practiceMode == PracticeBotMode.Moving)
            {
                if (Time.time > practiceNextMove)
                {
                    practiceNextMove = Time.time + Random.Range(0.6f, 1.8f);
                    Vector3 side = Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * Random.Range(2f, 6f);
                    if (NavMesh.SamplePosition(practiceAnchor + side, out var h, 2f, NavMesh.AllAreas)) MoveTo(h.position, false);
                    wantCrouch = Random.value < 0.15f;
                }
            }
        }
    }
}
