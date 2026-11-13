using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    /// <summary>Grenade with custom bounce physics (sphere casts against world geometry only).</summary>
    public class Grenade : MonoBehaviour
    {
        public static readonly List<Grenade> Live = new List<Grenade>();

        public GrenadeType type;
        public Actor owner;
        Vector3 velocity;
        float spawnTime, restTime = -1f;
        bool done;
        const float Radius = 0.07f, Gravity = 15f;

        public static Grenade Spawn(GrenadeType type, Vector3 pos, Vector3 velocity, Actor owner)
        {
            var go = new GameObject("Grenade_" + type);
            go.transform.position = pos;
            var g = go.AddComponent<Grenade>();
            g.type = type; g.owner = owner; g.velocity = velocity; g.spawnTime = Time.time;
            var parts = WeaponModels.Build(WeaponHandler.GrenadeDef(type), go.transform, 1.3f);
            parts.root.transform.localPosition = Vector3.zero;
            Live.Add(g);
            return g;
        }

        void OnDestroy() { Live.Remove(this); }

        float Fuse => type == GrenadeType.HE ? 1.7f : type == GrenadeType.Flash ? 1.5f : 2.2f;

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            if (restTime < 0f)
            {
                velocity.y -= Gravity * dt;
                Vector3 step = velocity * dt;
                float dist = step.magnitude;
                if (dist > 0f && Physics.SphereCast(transform.position, Radius, step / dist, out var hit, dist, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    transform.position += step.normalized * Mathf.Max(0f, hit.distance - 0.01f);
                    float impact = Vector3.Dot(velocity, -hit.normal);
                    velocity = Vector3.Reflect(velocity, hit.normal) * 0.45f;
                    if (impact > 1.5f) AudioManager.Play3D("bounce", transform.position, Mathf.Clamp01(impact / 10f), 25f);
                    if (hit.normal.y > 0.6f && velocity.magnitude < 1.2f) { restTime = Time.time; velocity = Vector3.zero; }
                }
                else transform.position += step;
                transform.Rotate(velocity.magnitude * 60f * dt, 0, 0, Space.Self);
            }

            bool fuseDone = Time.time - spawnTime >= Fuse;
            if (type == GrenadeType.Smoke) fuseDone = fuseDone && (restTime > 0f || Time.time - spawnTime > 4f);
            if (fuseDone) Detonate();
        }

        void Detonate()
        {
            done = true;
            Vector3 p = transform.position;
            switch (type)
            {
                case GrenadeType.HE:
                    Effects.Explosion(p, 1f);
                    AudioManager.Play3D("explosion", p, 1f, 90f);
                    Hearing.Emit(p, 50f, NoiseType.Explosion, owner);
                    Ballistics.Explosion(p, 9f, 98f, owner, WeaponDatabase.Get("he"));
                    CameraShake.Add(p, 0.6f, 14f);
                    break;
                case GrenadeType.Flash:
                    AudioManager.Play3D("flash", p, 1f, 60f);
                    Hearing.Emit(p, 35f, NoiseType.Explosion, owner);
                    Effects.Explosion(p, 0.15f);
                    FlashActors(p);
                    break;
                case GrenadeType.Smoke:
                    AudioManager.Play3D("smoke", p, 0.9f, 35f);
                    SmokeCloud.Create(p);
                    break;
            }
            Destroy(gameObject);
        }

        static void FlashActors(Vector3 p)
        {
            foreach (var a in Actor.All)
            {
                if (!a.alive) continue;
                Vector3 eye = a.EyePosition;
                Vector3 to = p - eye;
                float d = to.magnitude;
                if (d > 28f) continue;
                if (Physics.Linecast(eye, p + Vector3.up * 0.1f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                float facing = Vector3.Dot(a.aimDirection.normalized, to / Mathf.Max(d, 0.01f));
                float facingK = facing > 0.5f ? 1f : facing > 0f ? 0.6f : facing > -0.5f ? 0.3f : 0.12f;
                float distK = Mathf.Clamp01(1.15f - d / 28f);
                float duration = 5f * facingK * distK;
                if (duration > 0.3f) a.Blind(duration);
            }
        }
    }

    /// <summary>Vision-blocking smoke. Puffs sit on the Smoke layer, which blocks AI line of sight (not bullets).</summary>
    public class SmokeCloud : MonoBehaviour
    {
        public static readonly List<SmokeCloud> Active = new List<SmokeCloud>();
        public const float Radius = 4.6f, Duration = 16f;
        readonly List<Transform> puffs = new List<Transform>();
        readonly List<float> sizes = new List<float>();
        float born;

        public static void Create(Vector3 pos)
        {
            var go = new GameObject("Smoke");
            go.transform.position = pos;
            var s = go.AddComponent<SmokeCloud>();
            s.born = Time.time;
            var mat = MaterialLib.Lit(new Color(0.78f, 0.78f, 0.76f));
            int n = Effects.Quality == 0 ? 9 : 14;
            for (int i = 0; i < n; i++)
            {
                Vector2 c = Random.insideUnitCircle * (Radius - 1.6f);
                float h = Random.Range(0.8f, 2.6f);
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "Puff";
                puff.transform.SetParent(go.transform, false);
                puff.transform.localPosition = new Vector3(c.x, h, c.y);
                puff.layer = Layers.Smoke;
                puff.GetComponent<Collider>().isTrigger = true;
                var r = puff.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.puffs.Add(puff.transform);
                s.sizes.Add(Random.Range(3.2f, 4.4f));
                puff.transform.localScale = Vector3.one * 0.1f;
            }
            Active.Add(s);
        }

        void OnDestroy() { Active.Remove(this); }

        public float Density01(Vector3 point)
        {
            float age = Time.time - born;
            float grow = Mathf.Clamp01(age / 1.6f), fade = Mathf.Clamp01((Duration - age) / 2f);
            float d = Vector3.Distance(point.Flat(), transform.position.Flat());
            return Mathf.Clamp01((Radius - d) / 1.5f) * grow * fade;
        }

        void Update()
        {
            float age = Time.time - born;
            if (age > Duration) { Destroy(gameObject); return; }
            float grow = Mathf.Clamp01(age / 1.6f), fade = Mathf.Clamp01((Duration - age) / 2f);
            for (int i = 0; i < puffs.Count; i++)
            {
                float wob = 1f + Mathf.Sin(Time.time * 0.6f + i) * 0.05f;
                puffs[i].localScale = Vector3.one * sizes[i] * grow * fade * wob;
                puffs[i].localPosition += Vector3.up * Mathf.Sin(Time.time * 0.3f + i * 1.7f) * 0.002f;
            }
        }

        public static float DensityAt(Vector3 p)
        {
            float d = 0f;
            foreach (var s in Active) d = Mathf.Max(d, s.Density01(p));
            return d;
        }
    }

    /// <summary>Weapons and the charge lying in the world.</summary>
    public class Pickup : MonoBehaviour
    {
        public static readonly List<Pickup> All = new List<Pickup>();

        public WeaponInstance weapon;   // null when this is the charge
        public bool isCharge;
        public Actor dropper;
        public float noPickupUntil;
        float spawnTime;
        Vector3 velocity;
        bool resting;

        public static Pickup Drop(WeaponInstance w, Vector3 pos, Actor dropper, float throwSpeed = 0f)
        {
            if (w == null) return null;
            var p = Create(w.def, pos, throwSpeed, dropper);
            p.weapon = w;
            return p;
        }

        public static Pickup DropCharge(Vector3 pos, float throwSpeed = 0f)
        {
            var p = Create(WeaponDatabase.Charge, pos, throwSpeed, null);
            p.isCharge = true;
            if (MatchManager.I != null) MatchManager.I.OnChargeDropped(p);
            return p;
        }

        static Pickup Create(WeaponDef def, Vector3 pos, float throwSpeed, Actor dropper)
        {
            var go = new GameObject("Pickup_" + def.id);
            go.transform.position = pos + Vector3.up * 1f;
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 90f);
            var parts = WeaponModels.Build(def, go.transform, 1f);
            parts.root.transform.localPosition = Vector3.zero;
            var p = go.AddComponent<Pickup>();
            p.dropper = dropper;
            p.noPickupUntil = Time.time + (dropper != null ? 1.2f : 0.3f);
            p.spawnTime = Time.time;
            p.velocity = (dropper != null ? dropper.aimDirection.Flat().normalized : Vector3.zero) * throwSpeed + Vector3.up * (throwSpeed > 0 ? 2f : 0f);
            All.Add(p);
            return p;
        }

        void OnDestroy() { All.Remove(this); }

        void Update()
        {
            if (resting) return;
            float dt = Time.deltaTime;
            velocity.y -= 15f * dt;
            Vector3 step = velocity * dt;
            if (Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude + 0.06f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y > 0.5f)
                {
                    transform.position = hit.point + Vector3.up * 0.05f;
                    resting = true;
                    if (isCharge && MatchManager.I != null) MatchManager.I.OnChargeDropped(this);
                    return;
                }
                velocity = Vector3.Reflect(velocity, hit.normal) * 0.3f;
            }
            else transform.position += step;
            if (Time.time - spawnTime > 5f) resting = true;
        }

        public bool CanBeTakenBy(Actor a)
        {
            if (!a.alive || Time.time < noPickupUntil && a == dropper) return false;
            if (isCharge) return a.team == Team.Raiders && MatchManager.I != null && MatchManager.I.Config.mode == GameMode.Classic;
            return true;
        }

        /// <summary>Walk-over pickup when the matching slot is empty (charge is always auto-collected by Raiders).</summary>
        public static void AutoCollect(Actor a)
        {
            if (!a.alive) return;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var p = All[i];
                if (p == null || !p.CanBeTakenBy(a)) continue;
                if ((p.transform.position - a.transform.position).Flat().sqrMagnitude > 1.2f * 1.2f) continue;
                if (Mathf.Abs(p.transform.position.y - a.transform.position.y) > 1.6f) continue;
                if (p.isCharge) { a.weapons.Give(WeaponDatabase.Charge, false); Notify(a, "picked up the charge"); Destroy(p.gameObject); continue; }
                var s = p.weapon.def.slot;
                if ((s == WeaponSlot.Primary && a.weapons.primary == null) || (s == WeaponSlot.Secondary && a.weapons.secondary == null))
                {
                    a.weapons.GiveInstance(p.weapon);
                    AudioManager.Play3D("deploy", a.EyePosition, 0.5f, 10f);
                    Destroy(p.gameObject);
                }
            }
        }

        static void Notify(Actor a, string what)
        {
            if (a.isPlayer) GameEvents.RaiseAnnounce("You " + what, new Color(1f, 0.8f, 0.3f));
            else if (MatchManager.I != null && MatchManager.I.Player != null && MatchManager.I.Player.team == a.team) GameEvents.RaiseRadio(a, "I've got the charge.");
        }

        /// <summary>Nearest swappable weapon in front of the actor (used by the Use button).</summary>
        public static Pickup FindSwappable(Actor a, float range = 1.8f)
        {
            Pickup best = null; float bestD = range;
            foreach (var p in All)
            {
                if (p == null || p.isCharge || !p.CanBeTakenBy(a)) continue;
                float d = Vector3.Distance(p.transform.position, a.transform.position + Vector3.up * 0.2f);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        public void SwapInto(Actor a)
        {
            var old = a.weapons.Give(weapon.def, false);
            if (weapon.def.slot == WeaponSlot.Primary) a.weapons.primary = weapon; else a.weapons.secondary = weapon;
            a.weapons.Equip(weapon.def.slot);
            if (old != null) Drop(old, a.transform.position, a, 1.5f);
            Destroy(gameObject);
        }

        public static void ClearAll()
        {
            foreach (var p in All.ToArray()) if (p) Destroy(p.gameObject);
            All.Clear();
        }
    }

    /// <summary>Distance-attenuated camera shake requests (consumed by the player camera).</summary>
    public static class CameraShake
    {
        public static float amount;
        public static void Add(Vector3 pos, float strength, float radius)
        {
            var cam = Camera.main;
            if (cam == null) return;
            float d = Vector3.Distance(cam.transform.position, pos);
            amount = Mathf.Max(amount, strength * Mathf.Clamp01(1f - d / radius));
        }
    }
}
