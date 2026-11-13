using UnityEngine;

namespace Wardline
{
    /// <summary>
    /// Local player: CharacterController movement tuned for a classic tactical feel (fast acceleration, no slide,
    /// walk is silent and accurate, crouch is slow and steady), camera with recoil punch and scopes,
    /// interaction (plant/defuse/pickup), and a spectator camera after death.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public Actor actor;
        public CharacterController cc;
        public Camera cam, vmCam;
        public Transform camRoot;
        public ViewModel viewModel;
        public readonly TouchControls controls = new TouchControls();

        const float Gravity = 20f, JumpHeight = 1.15f, GroundAccel = 45f, GroundDecel = 40f, AirAccel = 7f;
        public const float BaseFov = 72f;
        float stepDistance, crouchBlend, lastGroundedTime, fallSpeed, landPenaltyUntil;
        bool wasGrounded = true;
        public Vector2 lastLook;
        public bool inputBlocked;            // menus (buy, pause, radio) take touches
        public bool spectating;
        public Actor spectateTarget;
        float deathTime;
        float shake;

        public static PlayerController Create(Actor actor)
        {
            var pc = actor.gameObject.AddComponent<PlayerController>();
            pc.actor = actor;
            actor.player = pc;
            actor.isPlayer = true;

            pc.cc = actor.gameObject.AddComponent<CharacterController>();
            pc.cc.height = 1.8f; pc.cc.radius = 0.35f; pc.cc.center = new Vector3(0, 0.9f, 0);
            pc.cc.stepOffset = 0.42f; pc.cc.slopeLimit = 45f; pc.cc.skinWidth = 0.04f;

            pc.camRoot = new GameObject("CameraRoot").transform;
            pc.camRoot.SetParent(actor.transform, false);
            pc.camRoot.localPosition = new Vector3(0, Actor.StandEye, 0);

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pc.camRoot, false);
            pc.cam = camGo.AddComponent<Camera>();
            pc.cam.fieldOfView = BaseFov;
            pc.cam.nearClipPlane = 0.05f;
            pc.cam.farClipPlane = 260f;
            pc.cam.cullingMask = ~((1 << Layers.ViewModel) | (1 << Layers.LocalBody));
            pc.cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();

            var vmGo = new GameObject("ViewModelCamera");
            vmGo.transform.SetParent(camGo.transform, false);
            pc.vmCam = vmGo.AddComponent<Camera>();
            pc.vmCam.fieldOfView = 58f;
            pc.vmCam.nearClipPlane = 0.01f;
            pc.vmCam.farClipPlane = 3f;
            pc.vmCam.cullingMask = 1 << Layers.ViewModel;
            QualityManager.SetupViewModelCamera(pc.cam, pc.vmCam);

            pc.viewModel = ViewModel.Create(actor, camGo.transform);
            actor.weapons.muzzleProvider = () => pc.viewModel.MuzzleWorld;
            actor.model.SetOwnerHidden(true);

            pc.controls.isButtonVisible = pc.ButtonVisible;
            pc.controls.zoomSensitivityScale = () =>
            {
                float fov = actor.weapons.ZoomFov(BaseFov);
                return fov < BaseFov - 1f ? GameApp.Settings.scopedSens * (fov / BaseFov) * 1.6f : 1f;
            };
            return pc;
        }

        bool ButtonVisible(string id)
        {
            var mm = MatchManager.I;
            switch (id)
            {
                case "use": return actor.alive && mm != null && mm.CanUse(actor);
                case "buy": return actor.alive && mm != null && mm.CanBuy(actor);
                case "grenade": return actor.alive && actor.weapons.HasAnyGrenade;
                case "aim": var d = actor.weapons.CurrentDef; return actor.alive && d != null && (d.HasScope || d.category == WeaponCategory.Knife);
                case "fire": case "fireL": case "jump": case "crouch": case "reload": case "switch": case "walk": return actor.alive;
            }
            return true;
        }

        public void OnRespawn(Vector3 pos, float yawDeg)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            actor.yaw = yawDeg; actor.pitch = 0f;
            spectating = false; spectateTarget = null;
            camRoot.SetParent(transform, false);
            camRoot.localPosition = new Vector3(0, Actor.StandEye, 0);
            camRoot.localRotation = Quaternion.identity;
            controls.ResetState();
            cam.fieldOfView = BaseFov;
        }

        void Update()
        {
            var s = GameApp.Settings;
            bool paused = GameApp.I != null && GameApp.I.Paused;
            controls.enabled = !paused && !inputBlocked;
            controls.Poll(!paused && !inputBlocked && Cursor.lockState == CursorLockMode.Locked);
            var f = controls.frame;
            lastLook = f.look;

            if (!actor.alive) { UpdateSpectator(f); return; }
            if (paused) return;

            // ---- Look (with optional aim assist friction near visible enemies) ----
            Vector2 look = f.look;
            if (s.aimAssist && Application.isMobilePlatform) look *= AimFriction();
            actor.yaw += look.x;
            actor.pitch = Mathf.Clamp(actor.pitch + look.y, -88f, 88f);

            var w = actor.weapons;
            shake = Mathf.Max(shake * Mathf.Exp(-Time.deltaTime * 6f), CameraShake.amount);
            CameraShake.amount = 0f;
            Vector2 punch = w.punch;
            float viewPitch = actor.pitch - punch.x;
            float viewYaw = actor.yaw + punch.y;
            transform.rotation = Quaternion.Euler(0, actor.yaw, 0);
            camRoot.localRotation = Quaternion.Euler(viewPitch, punch.y, 0) * Quaternion.Euler(Random.insideUnitSphere * shake * 2f);
            actor.aimDirection = Quaternion.Euler(viewPitch, viewYaw, 0) * Vector3.forward;

            // ---- Weapon actions ----
            if (!inputBlocked)
            {
                if (f.slotPressed >= 0) w.Equip((WeaponSlot)f.slotPressed);
                if (f.switchPressed) w.CycleNext();
                if (f.lastWeaponPressed) w.SwapToPrevious();
                if (f.grenadePressed) w.SelectGrenadeSlot();
                if (f.reloadPressed) w.Reload();
                if (f.aimPressed) { if (w.CurrentDef != null && w.CurrentDef.category == WeaponCategory.Knife) w.Stab(); else w.ToggleZoom(); }
                if (f.dropPressed) { if (w.slot == WeaponSlot.Bomb) w.DropChargeForward(); else w.DropCurrent(); }
                bool canFire = !(MatchManager.I != null && MatchManager.I.IsPlantingOrDefusing(actor));
                if (canFire) w.TriggerUpdate(f.fireHeld, f.firePressed, f.fireReleased);
                if (MatchManager.I != null)
                {
                    bool plantIntent = f.useHeld || (w.slot == WeaponSlot.Bomb && f.fireHeld);
                    MatchManager.I.InteractUpdate(actor, plantIntent, f.usePressed);
                }
            }

            // ---- Movement ----
            Move(f);

            // ---- Camera FOV / scope ----
            float targetFov = w.ZoomFov(BaseFov);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 18f);
            bool scopedOverlay = w.zoomLevel > 0 && w.CurrentDef != null && w.CurrentDef.scopeOverlay;
            viewModel.Tick(look, scopedOverlay);

            Pickup.AutoCollect(actor);
        }

        float AimFriction()
        {
            Vector3 eye = actor.EyePosition;
            foreach (var a in Actor.All)
            {
                if (!a.alive || a.team == actor.team || a == actor) continue;
                Vector3 to = a.ChestPosition - eye;
                float ang = Vector3.Angle(actor.aimDirection, to);
                float tol = Mathf.Clamp(140f / Mathf.Max(to.magnitude, 1f), 1.5f, 6f);
                if (ang < tol && !Physics.Linecast(eye, a.ChestPosition, Layers.VisionMask, QueryTriggerInteraction.Collide)) return 0.55f;
            }
            return 1f;
        }

        void Move(InputFrame f)
        {
            float dt = Time.deltaTime;
            bool frozen = actor.InFreeze || (MatchManager.I != null && MatchManager.I.IsPlantingOrDefusing(actor));
            Vector2 input = frozen || inputBlocked ? Vector2.zero : f.move;

            // Crouch with headroom check
            bool wantCrouch = f.crouchHeld && !inputBlocked;
            if (!wantCrouch && actor.crouching)
            {
                if (Physics.SphereCast(transform.position + Vector3.up * 1.0f, cc.radius * 0.9f, Vector3.up, out _, 0.75f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    wantCrouch = true;
            }
            actor.crouching = wantCrouch;
            float targetH = actor.crouching ? 1.25f : 1.8f;
            cc.height = Mathf.MoveTowards(cc.height, targetH, dt * 6f);
            cc.center = new Vector3(0, cc.height / 2f, 0);
            crouchBlend = Mathf.MoveTowards(crouchBlend, actor.crouching ? 1f : 0f, dt * 7f);
            camRoot.localPosition = new Vector3(0, Mathf.Lerp(Actor.StandEye, Actor.CrouchEye, crouchBlend), 0);

            actor.walking = f.walk || actor.crouching;
            float speed = actor.MaxSpeed * (actor.crouching ? Actor.CrouchFactor : f.walk ? Actor.WalkFactor : 1f);
            if (Time.time < landPenaltyUntil) speed *= 0.75f;

            Vector3 wish = (transform.right * input.x + transform.forward * input.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            Vector3 target = wish * speed;
            Vector3 hv = new Vector3(actor.velocity.x, 0, actor.velocity.z);
            bool grounded = cc.isGrounded;

            if (grounded)
            {
                float rate = wish.sqrMagnitude > 0.01f ? GroundAccel : GroundDecel;
                hv = Vector3.MoveTowards(hv, target, rate * dt);
                lastGroundedTime = Time.time;
            }
            else
            {
                // Limited air control: can steer but not gain speed beyond max.
                Vector3 add = Vector3.MoveTowards(hv, target, AirAccel * dt) - hv;
                hv += add;
                if (hv.magnitude > speed && hv.magnitude > 0.01f) hv = hv.normalized * Mathf.Max(speed, new Vector3(actor.velocity.x, 0, actor.velocity.z).magnitude * 0.995f);
            }

            float vy = actor.velocity.y;
            if (grounded && vy < 0f) vy = -2f;
            if (f.jumpPressed && !inputBlocked && !frozen && (grounded || Time.time - lastGroundedTime < 0.1f) && !actor.crouching)
            {
                vy = Mathf.Sqrt(2f * Gravity * JumpHeight);
                lastGroundedTime = -1f;
                AudioManager.Play3D("jump", transform.position, 0.4f, 15f, 1f, SoundCategory.Footsteps);
                Hearing.Emit(transform.position, 14f, NoiseType.Footstep, actor);
            }
            vy -= Gravity * dt;

            Vector3 motion = new Vector3(hv.x, vy, hv.z);
            var flags = cc.Move(motion * dt);
            if ((flags & CollisionFlags.Above) != 0 && vy > 0) vy = 0f;
            Vector3 realV = cc.velocity;
            actor.velocity = new Vector3(realV.x, vy, realV.z);
            if (cc.isGrounded && vy < 0f) actor.velocity.y = -2f;
            actor.grounded = cc.isGrounded;

            // Landing
            if (!wasGrounded && actor.grounded)
            {
                if (fallSpeed > 6f)
                {
                    AudioManager.Play3D("land", transform.position, Mathf.Clamp01(fallSpeed / 14f), 20f, 1f, SoundCategory.Footsteps);
                    Hearing.Emit(transform.position, 16f, NoiseType.Land, actor);
                    landPenaltyUntil = Time.time + 0.25f;
                    if (fallSpeed > 12f)
                        actor.ApplyDamage(new DamageInfo { amount = (fallSpeed - 12f) * 9f, attacker = null, zone = HitZone.Leg, sourcePosition = transform.position + Vector3.down, explosive = true });
                }
            }
            fallSpeed = actor.grounded ? 0f : Mathf.Max(fallSpeed, -vy);
            wasGrounded = actor.grounded;

            // Footsteps: running is loud, walking/crouching is silent (classic stealth rule)
            float hs = actor.HorizontalSpeed;
            if (actor.grounded && hs > 0.5f)
            {
                stepDistance += hs * dt;
                if (stepDistance > 2.3f)
                {
                    stepDistance = 0f;
                    if (!actor.walking && hs > Actor.RunSpeed * 0.6f)
                    {
                        AudioManager.Play3D("step" + Random.Range(1, 5), transform.position, 0.55f, 22f, 1f, SoundCategory.Footsteps);
                        Hearing.Emit(transform.position, 22f, NoiseType.Footstep, actor);
                    }
                }
            }
        }

        // ---- Spectating after death ----
        void UpdateSpectator(InputFrame f)
        {
            if (!spectating)
            {
                spectating = true; deathTime = Time.time;
                spectateTarget = actor.lastAttacker;
                camRoot.SetParent(null, true);
            }
            if (f.firePressed || f.jumpPressed || spectateTarget == null || (!spectateTarget.alive && Time.time - deathTime > 2.5f))
                spectateTarget = NextSpectateTarget(spectateTarget);

            Vector3 focus;
            if (spectateTarget != null)
            {
                focus = spectateTarget.EyePosition;
                Vector3 back = spectateTarget.aimDirection.Flat().normalized;
                if (back.sqrMagnitude < 0.01f) back = spectateTarget.transform.forward;
                Vector3 desired = focus - back * 2.8f + Vector3.up * 0.5f;
                if (Physics.Linecast(focus, desired, out var hit, Layers.WorldMask, QueryTriggerInteraction.Ignore)) desired = hit.point + hit.normal * 0.2f;
                camRoot.position = Vector3.Lerp(camRoot.position, desired, Time.deltaTime * 8f);
            }
            else
            {
                focus = transform.position + Vector3.up * 0.3f;
                Vector3 desired = focus + new Vector3(Mathf.Sin(Time.time * 0.3f), 0.8f, Mathf.Cos(Time.time * 0.3f)) * 3.5f;
                camRoot.position = Vector3.Lerp(camRoot.position, desired, Time.deltaTime * 3f);
            }
            camRoot.rotation = Quaternion.Slerp(camRoot.rotation, Quaternion.LookRotation(focus - camRoot.position), Time.deltaTime * 10f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, BaseFov, Time.deltaTime * 5f);
            viewModel.Tick(Vector2.zero, true);
        }

        public void CycleSpectate() { if (spectating) spectateTarget = NextSpectateTarget(spectateTarget); }

        Actor NextSpectateTarget(Actor current)
        {
            var list = Actor.All;
            int start = current != null ? list.IndexOf(current) : -1;
            for (int i = 1; i <= list.Count; i++)
            {
                var a = list[(start + i + list.Count) % list.Count];
                if (a.alive && a != actor && (a.team == actor.team || MatchManager.I == null || MatchManager.I.Config.mode == GameMode.Practice)) return a;
            }
            return null;
        }
    }
}
