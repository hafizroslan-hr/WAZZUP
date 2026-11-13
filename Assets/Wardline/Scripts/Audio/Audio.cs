using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    public enum SoundCategory { Sfx, Footsteps, Music, Ui }

    /// <summary>
    /// Procedurally synthesized sound effects and music. Everything is generated at startup, so the project
    /// contains no licensed audio and works offline.
    /// </summary>
    public static class SfxSynth
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static System.Random rng = new System.Random(1234);

        public static AudioClip Get(string id)
        {
            if (clips.TryGetValue(id, out var c)) return c;
            c = Build(id);
            clips[id] = c;
            return c;
        }

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        static AudioClip Make(string id, float seconds, System.Func<float, float, float> fn)
        {
            int n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = fn(t, t / seconds);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float norm = 0.95f / peak;
            for (int i = 0; i < n; i++) data[i] *= norm;
            var clip = AudioClip.Create(id, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A tiny one-pole low-pass filter state used by noise-based sounds.
        static float lp;
        static float LowPass(float x, float k) { lp += (x - lp) * k; return lp; }

        static AudioClip Build(string id)
        {
            lp = 0;
            switch (id)
            {
                // ---- Gunshots: body (filtered noise) + low thump + crack. Parameters vary by weapon class.
                case "shot_pistol": return Gun(id, 0.32f, 0.55f, 140f, 0.9f);
                case "shot_heavy_pistol": return Gun(id, 0.5f, 0.35f, 90f, 1.2f);
                case "shot_silenced": return Make(id, 0.18f, (t, p) => LowPass(Noise(), 0.18f) * Mathf.Exp(-t * 38f) + Mathf.Sin(t * 2 * Mathf.PI * 180f) * Mathf.Exp(-t * 60f) * 0.4f);
                case "shot_smg": return Gun(id, 0.26f, 0.6f, 160f, 0.8f);
                case "shot_rifle": return Gun(id, 0.42f, 0.42f, 110f, 1.1f);
                case "shot_rifle2": return Gun(id, 0.4f, 0.5f, 125f, 1.0f);
                case "shot_sniper": return Gun(id, 0.9f, 0.3f, 70f, 1.5f);
                case "shot_shotgun": return Gun(id, 0.6f, 0.28f, 75f, 1.6f);
                case "shot_lmg": return Gun(id, 0.38f, 0.38f, 95f, 1.15f);
                case "knife": return Make(id, 0.16f, (t, p) => LowPass(Noise(), 0.6f) * Mathf.Sin(p * Mathf.PI) * 0.6f);
                case "knife_hit": return Make(id, 0.15f, (t, p) => (LowPass(Noise(), 0.25f) + Mathf.Sin(t * 2 * Mathf.PI * 120f) * 0.5f) * Mathf.Exp(-t * 30f));
                case "dry": return Make(id, 0.06f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * 2400f) * Mathf.Exp(-t * 120f));
                // ---- Weapon handling
                case "mag_out": return Click(id, new[] { 0f, 0.05f }, 900f, 0.2f);
                case "mag_in": return Click(id, new[] { 0f, 0.07f }, 700f, 0.22f);
                case "bolt": return Click(id, new[] { 0f, 0.09f, 0.2f }, 1100f, 0.32f);
                case "pump": return Make(id, 0.3f, (t, p) => (LowPass(Noise(), 0.3f) * 0.6f) * (Bump(t, 0.02f, 0.06f) + Bump(t, 0.16f, 0.07f)));
                case "deploy": return Click(id, new[] { 0f, 0.06f }, 1300f, 0.15f);
                case "shell": return Make(id, 0.12f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * 3200f) * Mathf.Exp(-t * 45f) * 0.5f + Mathf.Sin(t * 2 * Mathf.PI * 4100f) * Mathf.Exp(-t * 60f) * 0.3f);
                case "scope": return Click(id, new[] { 0f }, 1800f, 0.06f);
                // ---- Movement
                case "step1": case "step2": case "step3": case "step4":
                {
                    float pitch = 0.8f + (id[4] - '1') * 0.08f;
                    return Make(id, 0.13f, (t, p) => (LowPass(Noise(), 0.12f * pitch) * 1.4f + Mathf.Sin(t * 2 * Mathf.PI * 70f * pitch) * 0.6f) * Mathf.Exp(-t * 34f));
                }
                case "jump": return Make(id, 0.14f, (t, p) => LowPass(Noise(), 0.15f) * Mathf.Exp(-t * 25f));
                case "land": return Make(id, 0.2f, (t, p) => (LowPass(Noise(), 0.08f) + Mathf.Sin(t * 2 * Mathf.PI * 55f)) * Mathf.Exp(-t * 22f));
                // ---- Impacts & damage
                case "impact": return Make(id, 0.08f, (t, p) => LowPass(Noise(), 0.5f) * Mathf.Exp(-t * 70f));
                case "ricochet": return Make(id, 0.35f, (t, p) => Mathf.Sin(2 * Mathf.PI * (3000f * t - 2500f * t * t)) * Mathf.Exp(-t * 12f) * 0.4f);
                case "flesh": return Make(id, 0.12f, (t, p) => LowPass(Noise(), 0.2f) * Mathf.Exp(-t * 40f) + Mathf.Sin(t * 2 * Mathf.PI * 90f) * Mathf.Exp(-t * 35f));
                case "headshot": return Make(id, 0.2f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * 1600f) * Mathf.Exp(-t * 25f) * 0.5f + LowPass(Noise(), 0.4f) * Mathf.Exp(-t * 50f));
                case "hitmarker": return Make(id, 0.05f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * 2600f) * Mathf.Exp(-t * 90f));
                case "hurt": return Make(id, 0.25f, (t, p) => Mathf.Sin(2 * Mathf.PI * (180f * t - 120f * t * t)) * Mathf.Exp(-t * 10f) * 0.7f + LowPass(Noise(), 0.1f) * 0.3f * Mathf.Exp(-t * 15f));
                case "death": return Make(id, 0.5f, (t, p) => Mathf.Sin(2 * Mathf.PI * (150f * t - 90f * t * t)) * Mathf.Exp(-t * 5f) * 0.6f + LowPass(Noise(), 0.06f) * Mathf.Exp(-t * 8f) * 0.5f);
                case "armor_hit": return Make(id, 0.1f, (t, p) => (Mathf.Sin(t * 2 * Mathf.PI * 900f) * 0.4f + LowPass(Noise(), 0.4f)) * Mathf.Exp(-t * 45f));
                // ---- Grenades & charge
                case "pin": return Click(id, new[] { 0f, 0.1f }, 2200f, 0.2f);
                case "throw": return Make(id, 0.2f, (t, p) => LowPass(Noise(), 0.3f) * Mathf.Sin(p * Mathf.PI));
                case "bounce": return Make(id, 0.08f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * 1400f) * Mathf.Exp(-t * 60f) + LowPass(Noise(), 0.4f) * Mathf.Exp(-t * 80f));
                case "explosion": return Make(id, 1.8f, (t, p) => (LowPass(Noise(), 0.05f + 0.15f * Mathf.Exp(-t * 6f)) * 1.5f + Mathf.Sin(t * 2 * Mathf.PI * (45f - t * 10f)) * Mathf.Exp(-t * 3f)) * Mathf.Exp(-t * 2.2f));
                case "big_explosion": return Make(id, 3.2f, (t, p) => (LowPass(Noise(), 0.03f + 0.1f * Mathf.Exp(-t * 3f)) * 1.6f + Mathf.Sin(t * 2 * Mathf.PI * (35f - t * 5f)) * Mathf.Exp(-t * 1.5f)) * Mathf.Exp(-t * 1.1f));
                case "flash": return Make(id, 0.8f, (t, p) => (Noise() * Mathf.Exp(-t * 18f) + Mathf.Sin(t * 2 * Mathf.PI * 3500f) * Mathf.Exp(-t * 3f) * 0.15f));
                case "smoke": return Make(id, 2.2f, (t, p) => LowPass(Noise(), 0.08f) * Mathf.Sin(Mathf.Min(1, t * 8f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 1.2f));
                case "beep": return Make(id, 0.09f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * 1250f) * (1 - p));
                case "plant_key": return Make(id, 0.07f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * 880f) * (1 - p));
                case "defuse_tick": return Click(id, new[] { 0f }, 1600f, 0.05f);
                case "armed": return Make(id, 0.6f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * (p < 0.5f ? 900f : 1200f)) * 0.8f);
                case "defused": return Make(id, 0.7f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * (p < 0.33f ? 700f : p < 0.66f ? 900f : 1300f)) * (1 - p * 0.5f));
                // ---- UI / announcer stingers
                case "ui_click": return Make(id, 0.05f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * 1500f) * Mathf.Exp(-t * 70f));
                case "ui_back": return Make(id, 0.06f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * 900f) * Mathf.Exp(-t * 60f));
                case "buy": return Make(id, 0.18f, (t, p) => Mathf.Sin(t * 2 * Mathf.PI * (p < 0.5f ? 1100f : 1650f)) * Mathf.Exp(-t * 8f));
                case "deny": return Make(id, 0.2f, (t, p) => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 180f)) * 0.4f * (1 - p));
                case "radio": return Make(id, 0.25f, (t, p) => (LowPass(Noise(), 0.7f) * 0.5f * (p < 0.3f ? 1 : 0)) + Mathf.Sin(t * 2 * Mathf.PI * 1900f) * 0.3f * (p > 0.85f ? 1 : 0));
                case "round_start": return Chord(id, 1.2f, new[] { 392f, 523f, 659f });
                case "round_win": return Arp(id, new[] { 523f, 659f, 784f, 1046f }, 0.12f);
                case "round_lose": return Arp(id, new[] { 466f, 392f, 311f, 233f }, 0.15f);
                case "match_end": return Chord(id, 2.5f, new[] { 262f, 330f, 392f, 523f });
                case "music_menu": return Music(id, 0);
                case "ambient_wind": return Make(id, 6f, (t, p) => LowPass(Noise(), 0.02f + 0.015f * Mathf.Sin(t * 0.9f)) * (0.6f + 0.4f * Mathf.Sin(p * Mathf.PI * 2f)) * Mathf.Min(1, Mathf.Min(t, 6f - t) * 2f));
                case "ambient_hum": return Make(id, 4f, (t, p) => (Mathf.Sin(t * 2 * Mathf.PI * 60f) * 0.5f + Mathf.Sin(t * 2 * Mathf.PI * 120f) * 0.25f + LowPass(Noise(), 0.05f) * 0.3f));
                case "ambient_jungle": return Make(id, 6f, (t, p) =>
                    LowPass(Noise(), 0.03f) * 0.5f + Mathf.Sin(t * 2 * Mathf.PI * (3200f + 300f * Mathf.Sin(t * 40f))) * 0.08f * Mathf.Max(0, Mathf.Sin(t * 5f)) * Mathf.Max(0, Mathf.Sin(t * 0.7f)));
            }
            Debug.LogWarning("[Wardline] Unknown sound " + id);
            return Make(id, 0.05f, (t, p) => 0f);
        }

        static float Bump(float t, float at, float width) { float d = (t - at) / width; return d < 0 || d > 1 ? 0 : Mathf.Sin(d * Mathf.PI); }

        static AudioClip Gun(string id, float len, float bright, float thumpHz, float body)
        {
            return Make(id, len, (t, p) =>
            {
                float crack = Noise() * Mathf.Exp(-t * 90f);
                float noise = LowPass(Noise(), bright * (0.2f + 0.8f * Mathf.Exp(-t * 20f))) * Mathf.Exp(-t * (9f / body)) * 1.4f;
                float thump = Mathf.Sin(2 * Mathf.PI * thumpHz * t * (1f - t * 0.8f)) * Mathf.Exp(-t * 18f) * body;
                float tail = LowPass(Noise(), 0.05f) * Mathf.Exp(-t * 4f) * 0.4f;
                return crack * 0.8f + noise + thump + tail;
            });
        }

        static AudioClip Click(string id, float[] times, float hz, float len)
        {
            return Make(id, len, (t, p) =>
            {
                float s = 0;
                foreach (float at in times) if (t >= at) s += (Mathf.Sin((t - at) * 2 * Mathf.PI * hz) * 0.6f + Noise() * 0.4f) * Mathf.Exp(-(t - at) * 80f);
                return s;
            });
        }

        static AudioClip Chord(string id, float len, float[] freqs)
        {
            return Make(id, len, (t, p) =>
            {
                float s = 0;
                foreach (var f in freqs) s += Mathf.Sin(t * 2 * Mathf.PI * f) + 0.3f * Mathf.Sin(t * 4 * Mathf.PI * f);
                return s * Mathf.Min(1, t * 30f) * Mathf.Exp(-t * 2.2f);
            });
        }

        static AudioClip Arp(string id, float[] notes, float step)
        {
            float len = notes.Length * step + 0.5f;
            return Make(id, len, (t, p) =>
            {
                float s = 0;
                for (int i = 0; i < notes.Length; i++)
                {
                    float at = i * step; if (t < at) continue;
                    float lt = t - at;
                    s += (Mathf.Sin(lt * 2 * Mathf.PI * notes[i]) + 0.25f * Mathf.Sign(Mathf.Sin(lt * 2 * Mathf.PI * notes[i]))) * Mathf.Exp(-lt * 4f);
                }
                return s;
            });
        }

        /// <summary>Slow, tense minor-key pad with a pulsing bass for menus.</summary>
        static AudioClip Music(string id, int variant)
        {
            float bpm = 84f, beat = 60f / bpm;
            float[] roots = { 110f, 87.31f, 130.81f, 98f };    // A2, F2, C3, G2
            float bar = beat * 4f;
            float len = bar * roots.Length * 2;
            return Make(id, len, (t, p) =>
            {
                int barIdx = (int)(t / bar) % roots.Length;
                float r = roots[barIdx];
                bool minor = barIdx == 0;
                float third = r * (minor ? 1.1892f : 1.2599f), fifth = r * 1.4983f;
                float lt = t % bar;
                float env = Mathf.Min(1f, lt * 2f) * Mathf.Min(1f, (bar - lt) * 2f);
                float pad = (Saw(t * r * 2) + Saw(t * third * 2 * 1.003f) + Saw(t * fifth * 2 * 0.997f)) * 0.12f * env;
                pad = LowPass(pad, 0.06f);
                float bt = t % beat;
                float bass = Mathf.Sin(t * 2 * Mathf.PI * r) * Mathf.Exp(-bt * 5f) * 0.55f;
                float hat = (bt % (beat / 2f) < 0.02f ? Noise() * 0.12f : 0f);
                float fadeLoop = Mathf.Min(1f, t * 0.5f) * Mathf.Min(1f, (len - t) * 0.5f);
                return (pad + bass + hat) * (0.6f + 0.4f * fadeLoop);
            });
        }

        static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
    }

    /// <summary>Pooled positional audio plus 2D UI/music playback with category volumes.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource music, ambient, ui;
        GameSettings settings;

        public static AudioManager Create(GameSettings s)
        {
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            I = go.AddComponent<AudioManager>();
            I.settings = s;
            I.music = I.Make2D("Music", true);
            I.ambient = I.Make2D("Ambient", true);
            I.ui = I.Make2D("UI", false);
            for (int i = 0; i < 24; i++) I.pool.Add(I.MakeSource());
            I.ApplyVolumes();
            return I;
        }

        AudioSource Make2D(string n, bool loop)
        {
            var go = new GameObject(n); go.transform.SetParent(transform);
            var a = go.AddComponent<AudioSource>(); a.loop = loop; a.playOnAwake = false; a.spatialBlend = 0f;
            return a;
        }

        AudioSource MakeSource()
        {
            var go = new GameObject("Src"); go.transform.SetParent(transform);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
            a.dopplerLevel = 0f; a.spread = 40f;
            return a;
        }

        public void ApplyVolumes()
        {
            AudioListener.volume = settings.masterVolume;
            music.volume = settings.musicVolume * 0.6f;
            ambient.volume = settings.ambientVolume * 0.35f;
            ui.volume = settings.sfxVolume;
        }

        float CatVolume(SoundCategory c) => c == SoundCategory.Footsteps ? settings.ambientVolume : c == SoundCategory.Music ? settings.musicVolume : settings.sfxVolume;

        public static void Play3D(string id, Vector3 pos, float volume = 1f, float maxDist = 60f, float pitch = 1f, SoundCategory cat = SoundCategory.Sfx)
        {
            if (I == null) return;
            I.PlayAt(SfxSynth.Get(id), pos, volume, maxDist, pitch, cat);
        }

        void PlayAt(AudioClip clip, Vector3 pos, float volume, float maxDist, float pitch, SoundCategory cat)
        {
            AudioSource src = null;
            foreach (var s in pool) if (!s.isPlaying) { src = s; break; }
            if (src == null)
            {
                if (pool.Count < 40) { src = MakeSource(); pool.Add(src); }
                else return;   // voice limit reached: drop the sound rather than stall
            }
            src.transform.position = pos;
            src.clip = clip;
            src.volume = volume * CatVolume(cat);
            src.pitch = pitch * Random.Range(0.96f, 1.04f);
            src.minDistance = 1.5f;
            src.maxDistance = maxDist;
            src.Play();
        }

        public static void Play2D(string id, float volume = 1f, float pitch = 1f)
        {
            if (I == null) return;
            I.ui.pitch = pitch;
            I.ui.PlayOneShot(SfxSynth.Get(id), volume);
        }

        public static void PlayMusic(string id)
        {
            if (I == null) return;
            var clip = id == null ? null : SfxSynth.Get(id);
            if (I.music.clip == clip && I.music.isPlaying) return;
            I.music.clip = clip;
            if (clip) I.music.Play(); else I.music.Stop();
        }

        public static void PlayAmbient(string id)
        {
            if (I == null) return;
            var clip = id == null ? null : SfxSynth.Get(id);
            I.ambient.clip = clip;
            if (clip) I.ambient.Play(); else I.ambient.Stop();
        }

        /// <summary>Generates all clips up front so the first gunshot doesn't hitch.</summary>
        public static void Prewarm()
        {
            string[] ids = { "shot_pistol", "shot_heavy_pistol", "shot_silenced", "shot_smg", "shot_rifle", "shot_rifle2", "shot_sniper", "shot_shotgun", "shot_lmg",
                "knife", "knife_hit", "dry", "mag_out", "mag_in", "bolt", "pump", "deploy", "shell", "scope", "step1", "step2", "step3", "step4", "jump", "land",
                "impact", "ricochet", "flesh", "headshot", "hitmarker", "hurt", "death", "armor_hit", "pin", "throw", "bounce", "explosion", "big_explosion",
                "flash", "smoke", "beep", "plant_key", "defuse_tick", "armed", "defused", "ui_click", "ui_back", "buy", "deny", "radio",
                "round_start", "round_win", "round_lose", "match_end" };
            foreach (var id in ids) SfxSynth.Get(id);
        }
    }

    public enum NoiseType { Gunshot, Footstep, Reload, Grenade, Explosion, Bomb, Knife, Land }

    public struct NoiseEvent
    {
        public Vector3 position;
        public float radius, time;
        public NoiseType type;
        public Actor source;
    }

    /// <summary>
    /// Sound propagation for AI hearing. Bots only learn that *a sound* happened at a position within range — never
    /// who made it or where the source went afterwards — so hearing obeys the same information rules as the player.
    /// </summary>
    public static class Hearing
    {
        static readonly List<NoiseEvent> events = new List<NoiseEvent>(64);
        public static IReadOnlyList<NoiseEvent> Events => events;

        public static void Emit(Vector3 pos, float radius, NoiseType type, Actor source)
        {
            if (events.Count > 60) events.RemoveAt(0);
            events.Add(new NoiseEvent { position = pos, radius = radius, time = Time.time, type = type, source = source });
        }

        public static void Prune(float maxAge = 2.5f)
        {
            float now = Time.time;
            events.RemoveAll(e => now - e.time > maxAge);
        }

        public static void Clear() => events.Clear();
    }
}
