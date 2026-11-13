using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    /// <summary>Raiders attack and plant the charge; Wardens defend and defuse.</summary>
    public enum Team { None = 0, Raiders = 1, Wardens = 2 }

    public enum GameMode { Classic, TeamDeathmatch, Practice }

    public enum BotDifficulty { Easy, Normal, Hard, Expert }

    public enum PracticeBotMode { Stationary, Moving, Fighting }

    public enum HitZone { Head, Chest, Stomach, Arm, Leg }

    public static class TeamUtil
    {
        public static Team Opposite(this Team t) => t == Team.Raiders ? Team.Wardens : t == Team.Wardens ? Team.Raiders : Team.None;
        public static string Label(this Team t) => t == Team.Raiders ? "Raiders" : t == Team.Wardens ? "Wardens" : "Spectator";
        public static Color Tint(this Team t) => t == Team.Raiders ? new Color(0.95f, 0.62f, 0.25f) : t == Team.Wardens ? new Color(0.35f, 0.65f, 1f) : Color.white;
    }

    /// <summary>Physics layers. Indices above 7 are user layers and work without being named in the Tag Manager.</summary>
    public static class Layers
    {
        public const int World = 0;
        public const int Hitbox = 8;
        public const int Actor = 9;
        public const int Smoke = 10;
        public const int ViewModel = 12;
        public const int LocalBody = 13;

        public const int WorldMask = 1 << World;
        public const int BulletMask = (1 << World) | (1 << Hitbox);
        public const int VisionMask = (1 << World) | (1 << Smoke);

        public static void ConfigurePhysics()
        {
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(Hitbox, i, true);
                Physics.IgnoreLayerCollision(Smoke, i, true);
                Physics.IgnoreLayerCollision(ViewModel, i, true);
            }
            Physics.IgnoreLayerCollision(Actor, Actor, false);
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }

    /// <summary>Global gameplay events. Systems subscribe instead of referencing each other directly.</summary>
    public static class GameEvents
    {
        public static event Action<Actor, Actor, WeaponDef, bool> Kill;          // killer, victim, weapon, headshot
        public static event Action<Actor, Actor, float, Vector3> Damaged;          // attacker, victim, amount, source position
        public static event Action<string, Color> Announce;
        public static event Action<Actor, string> Radio;

        public static void RaiseKill(Actor k, Actor v, WeaponDef w, bool hs) => Kill?.Invoke(k, v, w, hs);
        public static void RaiseDamaged(Actor a, Actor v, float amt, Vector3 src) => Damaged?.Invoke(a, v, amt, src);
        public static void RaiseAnnounce(string text, Color c) => Announce?.Invoke(text, c);
        public static void RaiseRadio(Actor from, string text) => Radio?.Invoke(from, text);

        public static void ClearAll() { Kill = null; Damaged = null; Announce = null; Radio = null; }
    }

    /// <summary>Simple GameObject pool keyed by prefab factory.</summary>
    public class GameObjectPool
    {
        readonly Func<GameObject> factory;
        readonly Queue<GameObject> free = new Queue<GameObject>();
        readonly List<GameObject> all = new List<GameObject>();
        readonly int max;
        int cursor;

        public GameObjectPool(Func<GameObject> factory, int max) { this.factory = factory; this.max = max; }

        public GameObject Get()
        {
            GameObject go = null;
            while (free.Count > 0 && go == null) go = free.Dequeue();
            if (go == null)
            {
                if (all.Count < max) { go = factory(); all.Add(go); }
                else { go = all[cursor]; cursor = (cursor + 1) % all.Count; }   // recycle oldest
            }
            go.SetActive(true);
            return go;
        }

        public void Release(GameObject go) { if (go == null) return; go.SetActive(false); free.Enqueue(go); }

        public void ReleaseAll() { free.Clear(); foreach (var g in all) if (g) { g.SetActive(false); free.Enqueue(g); } }
    }

    public static class MathUtil
    {
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Random direction inside a cone of the given half-angle (degrees) around forward.</summary>
        public static Vector3 SpreadDirection(Vector3 forward, float spreadDeg)
        {
            if (spreadDeg <= 0.0001f) return forward;
            Vector2 r = UnityEngine.Random.insideUnitCircle;
            float tan = Mathf.Tan(spreadDeg * Mathf.Deg2Rad);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(forward, right);
            return (forward + right * r.x * tan + up * r.y * tan).normalized;
        }

        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0, seconds);
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
