using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    /// <summary>Lightweight pooled particle (no ParticleSystem dependency): moves, falls, shrinks, deactivates.</summary>
    public class Debris : MonoBehaviour
    {
        public Vector3 velocity;
        public float life, maxLife, gravity, startScale, growth;
        public GameObjectPool pool;

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            if (life >= maxLife) { pool.Release(gameObject); return; }
            velocity.y -= gravity * dt;
            velocity *= 1f - Mathf.Clamp01(dt * 2f);
            transform.position += velocity * dt;
            float k = life / maxLife;
            transform.localScale = Vector3.one * Mathf.Max(0.001f, startScale * (growth > 0 ? 1f + growth * k : 1f - k));
        }
    }

    /// <summary>Fading timed object (muzzle flash, tracers, decals).</summary>
    public class TimedFx : MonoBehaviour
    {
        public float until;
        public GameObjectPool pool;
        public Light lightComp;
        public LineRenderer line;
        float start, intensity;

        public void Begin(float duration) { start = Time.time; until = Time.time + duration; if (lightComp) intensity = lightComp.intensity; }

        void Update()
        {
            if (Time.time >= until) { pool.Release(gameObject); return; }
            float k = 1f - (Time.time - start) / Mathf.Max(0.001f, until - start);
            if (lightComp) lightComp.intensity = intensity * k;
            if (line) line.widthMultiplier = 0.018f * k;
        }
    }

    public static class Effects
    {
        static GameObjectPool debrisPool, flashPool, tracerPool, decalPool, smokePuffPool;
        static Transform root;
        public static int Quality = 1;

        public static void Init()
        {
            if (root) return;
            root = new GameObject("Effects").transform;
            Object.DontDestroyOnLoad(root.gameObject);

            debrisPool = new GameObjectPool(() =>
            {
                var g = MeshUtil.Visual(PrimitiveType.Cube, root, Vector3.zero, Vector3.one * 0.05f, MaterialLib.Unlit(Color.white));
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var d = g.AddComponent<Debris>(); d.pool = debrisPool;
                g.SetActive(false);
                return g;
            }, 160);

            flashPool = new GameObjectPool(() =>
            {
                var g = new GameObject("MuzzleFlash"); g.transform.SetParent(root);
                var v = MeshUtil.Visual(PrimitiveType.Sphere, g.transform, Vector3.zero, new Vector3(0.12f, 0.12f, 0.28f), MaterialLib.Unlit(new Color(1f, 0.85f, 0.45f)));
                v.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 6f; l.color = new Color(1f, 0.8f, 0.5f); l.intensity = 2.5f; l.shadows = LightShadows.None;
                var fx = g.AddComponent<TimedFx>(); fx.pool = flashPool; fx.lightComp = l;
                g.SetActive(false);
                return g;
            }, 12);

            tracerPool = new GameObjectPool(() =>
            {
                var g = new GameObject("Tracer"); g.transform.SetParent(root);
                var lr = g.AddComponent<LineRenderer>();
                lr.sharedMaterial = MaterialLib.Unlit(new Color(1f, 0.92f, 0.6f));
                lr.positionCount = 2; lr.widthMultiplier = 0.018f; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
                var fx = g.AddComponent<TimedFx>(); fx.pool = tracerPool; fx.line = lr;
                g.SetActive(false);
                return g;
            }, 24);

            decalPool = new GameObjectPool(() =>
            {
                var g = MeshUtil.Visual(PrimitiveType.Quad, root, Vector3.zero, Vector3.one * 0.09f, MaterialLib.Unlit(new Color(0.06f, 0.05f, 0.04f)));
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.SetActive(false);
                return g;
            }, 80);

            smokePuffPool = new GameObjectPool(() =>
            {
                var g = MeshUtil.Visual(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.2f, MaterialLib.Lit(new Color(0.7f, 0.68f, 0.64f)));
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var d = g.AddComponent<Debris>(); d.pool = smokePuffPool;
                g.SetActive(false);
                return g;
            }, 40);
        }

        public static void ClearAll()
        {
            debrisPool?.ReleaseAll(); flashPool?.ReleaseAll(); tracerPool?.ReleaseAll(); decalPool?.ReleaseAll(); smokePuffPool?.ReleaseAll();
        }

        static void Spawn(GameObjectPool pool, Vector3 pos, Vector3 vel, Color c, float scale, float life, float gravity, float growth = 0f, bool lit = false)
        {
            var g = pool.Get();
            g.transform.position = pos;
            g.transform.rotation = Random.rotation;
            g.GetComponent<Renderer>().sharedMaterial = lit ? MaterialLib.Lit(c) : MaterialLib.Unlit(c);
            var d = g.GetComponent<Debris>();
            d.velocity = vel; d.life = 0f; d.maxLife = life; d.gravity = gravity; d.startScale = scale; d.growth = growth;
            g.transform.localScale = Vector3.one * scale;
        }

        public static void Impact(Vector3 point, Vector3 normal, Color surface)
        {
            int n = Quality == 0 ? 2 : Quality == 1 ? 4 : 7;
            for (int i = 0; i < n; i++)
                Spawn(debrisPool, point + normal * 0.03f, (normal + Random.insideUnitSphere * 0.7f) * Random.Range(1.5f, 4f), Color.Lerp(surface, Color.white, 0.2f), Random.Range(0.02f, 0.045f), Random.Range(0.25f, 0.5f), 9f);
            if (Quality > 0) Spawn(debrisPool, point + normal * 0.02f, Random.Range(1f, 2f) * normal * 0.3f, new Color(1f, 0.8f, 0.4f), 0.03f, 0.08f, 0f);
            var d = decalPool.Get();
            d.transform.position = point + normal * 0.01f;
            d.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0, 360f));
            d.transform.localScale = Vector3.one * Random.Range(0.06f, 0.1f);
        }

        public static void Blood(Vector3 point, Vector3 dir, bool headshot)
        {
            int n = (Quality == 0 ? 3 : 6) + (headshot ? 4 : 0);
            for (int i = 0; i < n; i++)
                Spawn(debrisPool, point, (dir * 0.6f + Random.insideUnitSphere) * Random.Range(1f, 3.2f), new Color(0.55f, 0.03f, 0.03f), Random.Range(0.03f, 0.06f), Random.Range(0.3f, 0.6f), 9f);
        }

        public static void MuzzleFlash(Vector3 pos, Vector3 dir, bool silenced, int layer = 0)
        {
            if (silenced) return;
            var g = flashPool.Get();
            g.transform.position = pos;
            g.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, Random.Range(0, 360f));
            g.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
            Layers.SetLayerRecursive(g, layer);
            var fx = g.GetComponent<TimedFx>();
            fx.lightComp.intensity = 2.5f;
            fx.lightComp.enabled = Quality > 0;
            fx.Begin(0.05f);
        }

        public static void Tracer(Vector3 from, Vector3 to)
        {
            var g = tracerPool.Get();
            var fx = g.GetComponent<TimedFx>();
            fx.line.SetPosition(0, from); fx.line.SetPosition(1, to);
            fx.line.widthMultiplier = 0.018f;
            fx.Begin(0.06f);
        }

        public static void ShellCasing(Vector3 pos, Vector3 right, int layer)
        {
            var g = debrisPool.Get();
            g.layer = layer;
            g.transform.position = pos;
            g.GetComponent<Renderer>().sharedMaterial = MaterialLib.Lit(new Color(0.8f, 0.6f, 0.2f), null, 0.6f, 0.8f);
            var d = g.GetComponent<Debris>();
            d.velocity = (right * Random.Range(1.2f, 2f) + Vector3.up * Random.Range(1f, 1.8f)); d.life = 0; d.maxLife = 0.45f; d.gravity = 9.8f; d.startScale = 0.012f; d.growth = 0.001f;
            g.transform.localScale = new Vector3(0.012f, 0.012f, 0.03f);
            AudioManager.Play3D("shell", pos + Vector3.down * 1.2f, 0.15f, 8f);
        }

        public static void Explosion(Vector3 pos, float scale)
        {
            int n = Quality == 0 ? 10 : Quality == 1 ? 18 : 30;
            for (int i = 0; i < n; i++)
            {
                var c = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.05f), Random.value);
                Spawn(debrisPool, pos + Random.insideUnitSphere * 0.3f * scale, Random.insideUnitSphere * 9f * scale + Vector3.up * 3f, c, Random.Range(0.15f, 0.35f) * scale, Random.Range(0.25f, 0.6f), 2f, 2f);
            }
            for (int i = 0; i < n / 2; i++)
                Spawn(smokePuffPool, pos + Random.insideUnitSphere * 0.6f * scale, Random.insideUnitSphere * 2.5f * scale + Vector3.up * 2.5f, new Color(0.2f, 0.19f, 0.18f), Random.Range(0.5f, 1f) * scale, Random.Range(1.2f, 2.2f), -0.5f, 2.5f, true);
            var flash = flashPool.Get();
            flash.transform.position = pos + Vector3.up * 0.5f;
            flash.transform.localScale = Vector3.one * 6f * scale;
            Layers.SetLayerRecursive(flash, 0);
            var fx = flash.GetComponent<TimedFx>();
            fx.lightComp.range = 18f * scale; fx.lightComp.intensity = 6f; fx.lightComp.enabled = true;
            fx.Begin(0.25f);
        }

        public static void Dust(Vector3 pos)
        {
            if (Quality == 0) return;
            for (int i = 0; i < 3; i++)
                Spawn(smokePuffPool, pos + Random.insideUnitSphere * 0.2f, Random.insideUnitSphere * 0.6f + Vector3.up * 0.3f, new Color(0.6f, 0.56f, 0.5f), 0.15f, 0.5f, 0f, 1.5f, true);
        }
    }

    /// <summary>Hitscan bullets: hit detection by zone, damage falloff, friendly-fire rules, effects and noise.</summary>
    public static class Ballistics
    {
        static readonly RaycastHit[] hits = new RaycastHit[24];
        static readonly HitComparer comparer = new HitComparer();
        class HitComparer : IComparer<RaycastHit> { public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance); }

        public static bool FriendlyFire => MatchManager.I != null && MatchManager.I.Config.friendlyFire;

        /// <summary>Returns the first thing a ray hits, ignoring the shooter's own hitboxes.</summary>
        public static bool Trace(Actor shooter, Vector3 origin, Vector3 dir, float range, out RaycastHit result, out Hitbox hitbox)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, hits, range, Layers.BulletMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, 0, n, comparer);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                var hb = h.collider.GetComponent<Hitbox>();
                if (hb != null)
                {
                    if (hb.actor == shooter || !hb.actor.alive) continue;
                    result = h; hitbox = hb; return true;
                }
                if (h.collider.isTrigger) continue;
                result = h; hitbox = null; return true;
            }
            result = default; hitbox = null;
            return false;
        }

        public static void FireBullet(Actor shooter, Vector3 origin, Vector3 dir, WeaponDef def, Vector3 visualMuzzle, bool tracer)
        {
            float range = Mathf.Max(def.effectiveRange * 3f, 120f);
            Vector3 end = origin + dir * range;
            if (Trace(shooter, origin, dir, range, out var hit, out var hb))
            {
                end = hit.point;
                if (hb != null)
                {
                    var victim = hb.actor;
                    bool sameTeam = victim.team == shooter.team && MatchManager.I != null && MatchManager.I.Config.mode != GameMode.Practice;
                    if (!sameTeam || FriendlyFire)
                    {
                        float dmg = def.damage * Mathf.Pow(def.rangeModifier, hit.distance / 12f);
                        victim.ApplyDamage(new DamageInfo { amount = dmg, attacker = shooter, weapon = def, zone = hb.zone, point = hit.point, direction = dir, sourcePosition = origin });
                        if (shooter.isPlayer) PlayerHitFeedback.Register(hb.zone == HitZone.Head, !victim.alive);
                    }
                    Effects.Blood(hit.point, dir, hb.zone == HitZone.Head);
                    AudioManager.Play3D("flesh", hit.point, 0.7f, 20f);
                }
                else
                {
                    var r = hit.collider.GetComponent<Renderer>();
                    Color c = r != null && r.sharedMaterial != null ? r.sharedMaterial.color : Color.gray;
                    Effects.Impact(hit.point, hit.normal, c);
                    if (Random.value < 0.35f) AudioManager.Play3D(Random.value < 0.15f ? "ricochet" : "impact", hit.point, 0.5f, 25f);
                }
            }
            if (tracer) Effects.Tracer(visualMuzzle + dir * 0.4f, end);
        }

        public static void Explosion(Vector3 pos, float radius, float maxDamage, Actor owner, WeaponDef weapon)
        {
            foreach (var a in Actor.All.ToArray())
            {
                if (!a.alive) continue;
                Vector3 target = a.ChestPosition;
                float d = Vector3.Distance(pos, target);
                if (d > radius) continue;
                // Walls block blast damage.
                if (Physics.Linecast(pos + Vector3.up * 0.3f, target, Layers.WorldMask, QueryTriggerInteraction.Ignore)
                    && Physics.Linecast(pos + Vector3.up * 0.3f, a.HeadPosition, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                bool sameTeam = owner != null && a.team == owner.team && a != owner && MatchManager.I != null && MatchManager.I.Config.mode != GameMode.Practice;
                if (sameTeam && !FriendlyFire) continue;
                float dmg = maxDamage * (1f - d / radius);
                a.ApplyDamage(new DamageInfo { amount = dmg, attacker = owner, weapon = weapon, zone = HitZone.Chest, point = target, direction = (target - pos).normalized, sourcePosition = pos, explosive = true });
            }
        }
    }

    /// <summary>Hit markers and kill confirmation for the local player.</summary>
    public static class PlayerHitFeedback
    {
        public static float lastHitTime = -10f, lastKillTime = -10f;
        public static bool lastWasHead;
        public static int shotsHit;

        public static void Register(bool head, bool kill)
        {
            lastHitTime = Time.time; lastWasHead = head; shotsHit++;
            if (kill) lastKillTime = Time.time;
            if (GameApp.Settings.hitMarker) AudioManager.Play2D("hitmarker", 0.35f, head ? 1.3f : 1f);
            Haptics.Pulse(kill ? 2 : 0);
        }
    }
}
