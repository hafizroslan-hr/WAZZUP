using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    [Serializable]
    public class ButtonLayout
    {
        public string id;
        public float x, y;     // normalized position inside the safe area (0..1, y measured from top)
        public float size = 1f;
        public bool enabled = true;
        public ButtonLayout() { }
        public ButtonLayout(string id, float x, float y, float size = 1f, bool enabled = true) { this.id = id; this.x = x; this.y = y; this.size = size; this.enabled = enabled; }
    }

    [Serializable]
    public class GameSettings
    {
        // Graphics
        public int graphicsQuality = 1;      // 0 Low, 1 Medium, 2 High
        public int targetFps = 60;
        public int shadowQuality = 1;        // 0 Off, 1 Hard, 2 Soft
        public int effectsQuality = 1;       // 0 Low, 1 Medium, 2 High
        public float renderScale = 0.85f;

        // Controls
        public float sensX = 1f, sensY = 0.85f, scopedSens = 0.55f, aimAcceleration = 0.2f;
        public bool gyroEnabled = false; public float gyroSens = 1f; public bool invertGyroY = false;
        public float buttonScale = 1f, joystickSize = 1f;
        public bool leftHanded = false, aimAssist = true, haptics = true, leftFireButton = true;
        public float mouseSens = 1f;

        // Audio
        public float masterVolume = 0.9f, musicVolume = 0.5f, sfxVolume = 1f, ambientVolume = 0.8f;

        // Gameplay
        public bool autoReload = true, autoFire = false, damageIndicators = true, hitMarker = true, showRadar = true;
        public int crosshairStyle = 0;       // 0 Dynamic, 1 Static, 2 Dot, 3 Cross + dot
        public float crosshairSize = 1f;
        public int crosshairColor = 0;
        public bool tutorialDone = false;

        public List<ButtonLayout> layout = DefaultLayout();

        public static readonly Color[] CrosshairColors = { new Color(0.3f, 1f, 0.35f), Color.white, new Color(1f, 0.95f, 0.2f), new Color(0.2f, 0.95f, 1f), new Color(1f, 0.3f, 0.3f), new Color(1f, 0.4f, 1f) };
        public static readonly string[] CrosshairColorNames = { "Green", "White", "Yellow", "Cyan", "Red", "Pink" };

        public static List<ButtonLayout> DefaultLayout() => new List<ButtonLayout>
        {
            new ButtonLayout("fire", 0.84f, 0.58f, 1.15f),
            new ButtonLayout("fireL", 0.10f, 0.36f, 0.75f),
            new ButtonLayout("aim", 0.95f, 0.36f, 0.8f),
            new ButtonLayout("jump", 0.95f, 0.62f, 0.8f),
            new ButtonLayout("crouch", 0.95f, 0.86f, 0.8f),
            new ButtonLayout("reload", 0.83f, 0.86f, 0.75f),
            new ButtonLayout("switch", 0.71f, 0.88f, 0.75f),
            new ButtonLayout("grenade", 0.71f, 0.70f, 0.7f),
            new ButtonLayout("use", 0.62f, 0.55f, 0.8f),
            new ButtonLayout("buy", 0.05f, 0.30f, 0.65f),
            new ButtonLayout("radio", 0.05f, 0.48f, 0.6f),
            new ButtonLayout("walk", 0.20f, 0.60f, 0.6f),
        };

        public ButtonLayout Get(string id)
        {
            foreach (var b in layout) if (b.id == id) return b;
            foreach (var b in DefaultLayout()) if (b.id == id) { layout.Add(b); return b; }
            return null;
        }
    }

    [Serializable]
    public class MapRecord { public string map; public int played, won, kills, deaths; }

    [Serializable]
    public class WeaponRecord { public string weapon; public int kills; }

    [Serializable]
    public class PlayerStats
    {
        public int matchesPlayed, matchesWon, matchesLost, matchesDrawn;
        public int kills, deaths, headshots, shotsFired, shotsHit, bestKillStreak, roundsWon, roundsPlayed, plants, defuses;
        public List<WeaponRecord> weapons = new List<WeaponRecord>();
        public List<MapRecord> maps = new List<MapRecord>();

        public void AddWeaponKill(string w)
        {
            var r = weapons.Find(x => x.weapon == w);
            if (r == null) { r = new WeaponRecord { weapon = w }; weapons.Add(r); }
            r.kills++;
        }

        public MapRecord Map(string m)
        {
            var r = maps.Find(x => x.map == m);
            if (r == null) { r = new MapRecord { map = m }; maps.Add(r); }
            return r;
        }

        public string FavouriteWeapon()
        {
            WeaponRecord best = null;
            foreach (var w in weapons) if (best == null || w.kills > best.kills) best = w;
            return best == null ? "-" : $"{best.weapon} ({best.kills} kills)";
        }

        public string BestMap()
        {
            MapRecord best = null; float bestScore = -1;
            foreach (var m in maps)
            {
                if (m.played == 0) continue;
                float score = m.won / (float)m.played + m.kills / (float)Mathf.Max(1, m.deaths) * 0.1f;
                if (score > bestScore) { bestScore = score; best = m; }
            }
            return best == null ? "-" : $"{best.map} ({best.won}/{best.played} won, K/D {best.kills / (float)Mathf.Max(1, best.deaths):0.00})";
        }
    }

    [Serializable]
    public class MatchConfig
    {
        public GameMode mode = GameMode.Classic;
        public int teamChoice = 2;           // 0 Raiders, 1 Wardens, 2 Random
        public int mapIndex = 0;
        public BotDifficulty difficulty = BotDifficulty.Normal;
        public int botCount = 9;
        public int teammateBots = 4;         // used when auto-balance is off
        public bool autoBalance = true;
        public bool friendlyFire = false;
        public int maxRounds = 30;
        public float roundTimeMinutes = 1.75f;
        public float bombTimer = 40f;
        public int startMoney = 800;
        public float freezeTime = 5f;
        public float buyTime = 25f;
        // Team deathmatch
        public int tdmKillLimit = 60;
        public float tdmMinutes = 10f;
        // Practice
        public PracticeBotMode practiceBots = PracticeBotMode.Moving;
        public bool unlimitedAmmo = true;

        public MatchConfig Clone() => JsonUtility.FromJson<MatchConfig>(JsonUtility.ToJson(this));
    }

    /// <summary>Local storage for settings, statistics and last-used match setups. Works fully offline.</summary>
    public static class Persistence
    {
        const string SettingsKey = "wardline.settings.v1";
        const string StatsKey = "wardline.stats.v1";
        const string ConfigKey = "wardline.config.v1.";

        public static GameSettings LoadSettings()
        {
            var s = Load<GameSettings>(SettingsKey) ?? new GameSettings();
            if (s.layout == null || s.layout.Count == 0) s.layout = GameSettings.DefaultLayout();
            return s;
        }
        public static void SaveSettings(GameSettings s) => Save(SettingsKey, s);

        public static PlayerStats LoadStats() => Load<PlayerStats>(StatsKey) ?? new PlayerStats();
        public static void SaveStats(PlayerStats s) => Save(StatsKey, s);

        public static MatchConfig LoadConfig(GameMode mode)
        {
            var c = Load<MatchConfig>(ConfigKey + mode) ?? new MatchConfig();
            c.mode = mode;
            if (mode == GameMode.Practice) { c.botCount = Mathf.Clamp(c.botCount, 1, 10); }
            return c;
        }
        public static void SaveConfig(MatchConfig c) => Save(ConfigKey + c.mode, c);

        static T Load<T>(string key) where T : class
        {
            try
            {
                string json = PlayerPrefs.GetString(key, "");
                return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json);
            }
            catch (Exception e) { Debug.LogWarning($"[Wardline] Could not read {key}: {e.Message}"); return null; }
        }

        static void Save<T>(string key, T value)
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(value));
            PlayerPrefs.Save();
        }

        public static void ResetStats() { PlayerPrefs.DeleteKey(StatsKey); PlayerPrefs.Save(); }
    }
}
