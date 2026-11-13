using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    /// <summary>Main menu, play/setup flow, map selection with previews, settings, weapon info, profile and about.</summary>
    public class MenuUI : MonoBehaviour
    {
        enum Page { Main, Play, Setup, MapSelect, MapPreview, Settings, Layout, Weapons, Profile, About }
        Page page = Page.Main;
        readonly Stack<Page> history = new Stack<Page>();
        bool open;
        MatchConfig config;
        int settingsTab;
        WeaponCategory weaponTab = WeaponCategory.Rifle;
        WeaponDef selectedWeapon;
        bool firstLaunchPrompt;
        bool confirmResetStats;
        LayoutEditor layoutEditor;
        float openTime;

        public int SelectedMapIndex => config != null ? config.mapIndex : 0;

        public void Open()
        {
            open = true; enabled = true;
            page = Page.Main; history.Clear();
            openTime = Time.unscaledTime;
            firstLaunchPrompt = !GameApp.Settings.tutorialDone;
        }

        public void Close() { open = false; }

        void Go(Page p) { history.Push(page); page = p; }

        void Back()
        {
            AudioManager.Play2D("ui_back", 0.5f);
            if (page == Page.Settings || page == Page.Layout) GameApp.I.ApplySettings();
            if (page == Page.MapPreview && GameApp.I.Background) GameApp.I.Background.fullPreview = false;
            page = history.Count > 0 ? history.Pop() : Page.Main;
        }

        void OnGUI()
        {
            if (!open) return;
            GUI.depth = 0;
            var sa = TouchControls.SafeAreaTopLeft;
            float S = UIKit.S;

            if (page == Page.MapPreview) { DrawMapPreview(sa, S); return; }
            if (page == Page.Layout) { if (layoutEditor == null) layoutEditor = new LayoutEditor(); if (layoutEditor.Draw(sa)) Back(); return; }

            // Darken the live background so text stays readable
            UIKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, page == Page.Main ? 0.35f : 0.55f));

            switch (page)
            {
                case Page.Main: DrawMain(sa, S); break;
                case Page.Play: DrawPlay(sa, S); break;
                case Page.Setup: DrawSetup(sa, S); break;
                case Page.MapSelect: DrawMapSelect(sa, S); break;
                case Page.Settings: DrawSettings(sa, S, false); break;
                case Page.Weapons: DrawWeapons(sa, S); break;
                case Page.Profile: DrawProfile(sa, S); break;
                case Page.About: DrawAbout(sa, S); break;
            }

            if (page != Page.Main && UIKit.Button(new Rect(sa.x + 16 * S, sa.y + 14 * S, 120 * S, 54 * S), "< Back")) Back();
            if (firstLaunchPrompt && page == Page.Main) DrawFirstLaunch(sa, S);
        }

        // ------------------------------------------------------------ Main
        void DrawMain(Rect sa, float S)
        {
            float x = sa.x + 60 * S;
            float k = Mathf.Clamp01((Time.unscaledTime - openTime) * 2.5f);
            var titleStyle = UIKit.Styled(UIKit.Title, 92, UIKit.Text, TextAnchor.MiddleLeft, true);
            UIKit.Shadowed(new Rect(x - (1 - k) * 60 * S, sa.y + 40 * S, 800 * S, 110 * S), "WARDLINE", titleStyle);
            UIKit.Fill(new Rect(x, sa.y + 150 * S, 360 * S * k, 4 * S), UIKit.Accent);
            GUI.Label(new Rect(x, sa.y + 158 * S, 700 * S, 40 * S), "TACTICAL  ROUND-BASED  SHOOTER", UIKit.Styled(UIKit.Label, 22, UIKit.TextDim));

            string[] items = { "PLAY", "PROFILE", "SETTINGS", "WEAPON INFO", "ABOUT" };
            float y = sa.y + 230 * S, w = 380 * S, h = 66 * S;
            for (int i = 0; i < items.Length; i++)
            {
                float slide = Mathf.Clamp01((Time.unscaledTime - openTime) * 3f - i * 0.15f);
                var r = new Rect(x - (1 - slide) * 100 * S, y + i * (h + 12 * S), w, h);
                if (UIKit.Button(r, items[i], i == 0))
                {
                    switch (i)
                    {
                        case 0: Go(Page.Play); break;
                        case 1: Go(Page.Profile); break;
                        case 2: settingsTab = 0; Go(Page.Settings); break;
                        case 3: selectedWeapon = null; Go(Page.Weapons); break;
                        case 4: Go(Page.About); break;
                    }
                }
            }
            GUI.Label(new Rect(sa.xMax - 420 * S, sa.yMax - 50 * S, 400 * S, 40 * S), "Offline • v" + GameApp.Version, UIKit.Styled(UIKit.Small, 17, UIKit.TextDim, TextAnchor.MiddleRight));
        }

        void DrawFirstLaunch(Rect sa, float S)
        {
            UIKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));
            var r = new Rect(sa.center.x - 330 * S, sa.center.y - 170 * S, 660 * S, 340 * S);
            UIKit.DrawPanel(r);
            GUI.Label(new Rect(r.x + 30 * S, r.y + 24 * S, r.width - 60 * S, 50 * S), "Welcome, operator", UIKit.Header);
            GUI.Label(new Rect(r.x + 30 * S, r.y + 84 * S, r.width - 60 * S, 140 * S),
                "Wardline is built for touch: left thumb moves, right thumb looks and fires. Take the 2-minute controls tutorial on the practice range before your first match?", UIKit.Label);
            if (UIKit.Button(new Rect(r.x + 30 * S, r.yMax - 84 * S, 290 * S, 60 * S), "START TUTORIAL", true))
            {
                firstLaunchPrompt = false;
                var c = Persistence.LoadConfig(GameMode.Practice);
                c.practiceBots = PracticeBotMode.Moving; c.botCount = 4; c.mapIndex = 0;
                GameApp.I.StartMatch(c);
            }
            if (UIKit.Button(new Rect(r.xMax - 320 * S, r.yMax - 84 * S, 290 * S, 60 * S), "SKIP"))
            {
                firstLaunchPrompt = false;
                GameApp.Settings.tutorialDone = true;
                GameApp.I.ApplySettings();
            }
        }

        // ------------------------------------------------------------ Play
        void DrawPlay(Rect sa, float S)
        {
            GUI.Label(new Rect(sa.x + 160 * S, sa.y + 16 * S, 600 * S, 50 * S), "PLAY", UIKit.Header);
            var modes = new[] { (GameMode.Classic, "CLASSIC CHARGE", "Round-based attack & defence. Raiders plant the breach charge at site A or B; Wardens stop them or defuse it. Buy weapons with your earnings."),
                                (GameMode.TeamDeathmatch, "TEAM DEATHMATCH", "Instant respawns, free loadouts and a kill limit. Pure gunplay against bots."),
                                (GameMode.Practice, "PRACTICE RANGE", "Aim training against stationary, moving or fighting bots. Unlimited money and ammo. Includes the controls tutorial.") };
            float cw = (sa.width - 120 * S - 40 * S) / 3f, ch = sa.height - 180 * S;
            for (int i = 0; i < modes.Length; i++)
            {
                var r = new Rect(sa.x + 60 * S + i * (cw + 20 * S), sa.y + 100 * S, cw, ch);
                UIKit.DrawPanel(r);
                UIKit.Fill(new Rect(r.x, r.y, r.width, 6 * S), i == 0 ? UIKit.Accent : UIKit.AccentDim);
                GUI.Label(new Rect(r.x + 20 * S, r.y + 24 * S, r.width - 40 * S, 50 * S), modes[i].Item2, UIKit.Styled(UIKit.Header, 28, UIKit.Text, TextAnchor.UpperLeft, true));
                GUI.Label(new Rect(r.x + 20 * S, r.y + 90 * S, r.width - 40 * S, ch - 200 * S), modes[i].Item3, UIKit.Label);
                if (UIKit.Button(new Rect(r.x + 20 * S, r.yMax - 84 * S, r.width - 40 * S, 64 * S), "SELECT", i == 0))
                {
                    config = Persistence.LoadConfig(modes[i].Item1);
                    Go(Page.Setup);
                }
            }
        }

        // ------------------------------------------------------------ Setup
        static readonly string[] Teams = { "Raiders (attack)", "Wardens (defend)", "Random" };
        static readonly string[] Difficulties = { "Easy", "Normal", "Hard", "Expert" };
        static readonly string[] DifficultyNotes =
        {
            "Slow reactions, poor aim, little teamwork. Good for learning.",
            "Average reactions and aim, basic teamwork and objectives.",
            "Fast and accurate. Uses cover, grenades, flanks and rotations.",
            "Highly tactical: coordinated executes, utility, prediction and disciplined aim. Same rules as you."
        };
        static readonly int[] RoundOptions = { 10, 16, 20, 24, 30 };
        static readonly float[] RoundTimes = { 1f, 1.5f, 1.75f, 2f, 2.5f, 3f };
        static readonly float[] BombTimes = { 30f, 35f, 40f, 45f, 60f };
        static readonly int[] MoneyOptions = { 800, 1500, 2500, 4000, 8000, 16000 };
        static readonly int[] KillLimits = { 20, 40, 60, 80, 100, 150 };
        static readonly float[] TdmMinutes = { 5f, 8f, 10f, 15f, 20f };
        static readonly string[] PracticeModes = { "Stationary targets", "Moving targets", "Fighting bots" };

        void DrawSetup(Rect sa, float S)
        {
            string title = config.mode == GameMode.Classic ? "CLASSIC CHARGE — SETUP" : config.mode == GameMode.TeamDeathmatch ? "TEAM DEATHMATCH — SETUP" : "PRACTICE — SETUP";
            GUI.Label(new Rect(sa.x + 160 * S, sa.y + 16 * S, 900 * S, 50 * S), title, UIKit.Header);

            var maps = MapLibrary.All;
            config.mapIndex = Mathf.Clamp(config.mapIndex, 0, maps.Count - 1);
            var map = maps[config.mapIndex];

            // Left: map card
            var mapCard = new Rect(sa.x + 40 * S, sa.y + 90 * S, sa.width * 0.34f, sa.height - 190 * S);
            UIKit.DrawPanel(mapCard);
            var prev = MapLibrary.Preview(map);
            float pw = mapCard.width - 30 * S, ph = Mathf.Min(pw * map.height / map.width, mapCard.height - 190 * S);
            pw = ph * map.width / map.height;
            GUI.DrawTexture(new Rect(mapCard.center.x - pw / 2, mapCard.y + 15 * S, pw, ph), prev);
            GUI.Label(new Rect(mapCard.x + 15 * S, mapCard.y + 25 * S + ph, mapCard.width, 40 * S), map.name.ToUpper(), UIKit.Styled(UIKit.Header, 26, UIKit.Text, TextAnchor.MiddleLeft, true));
            if (UIKit.Button(new Rect(mapCard.x + 15 * S, mapCard.yMax - 74 * S, mapCard.width - 30 * S, 58 * S), "CHANGE MAP")) Go(Page.MapSelect);

            // Right: options
            var area = new Rect(mapCard.xMax + 30 * S, sa.y + 90 * S, sa.xMax - mapCard.xMax - 70 * S, sa.height - 190 * S);
            UIKit.DrawPanel(area);
            var rows = new List<System.Action<Rect>>();
            if (config.mode != GameMode.Practice)
                rows.Add(r => config.teamChoice = UIKit.Selector(r, "Team", Teams, config.teamChoice));
            if (config.mode != GameMode.Practice || config.practiceBots == PracticeBotMode.Fighting)
                rows.Add(r => config.difficulty = (BotDifficulty)UIKit.Selector(r, "Bot difficulty", Difficulties, (int)config.difficulty));
            if (config.mode == GameMode.Practice)
            {
                rows.Add(r => config.practiceBots = (PracticeBotMode)UIKit.Selector(r, "Targets", PracticeModes, (int)config.practiceBots));
                rows.Add(r => config.botCount = Mathf.RoundToInt(UIKit.Slider(r, "Bots", Mathf.Clamp(config.botCount, 1, 10), 1, 10, "0")));
                rows.Add(r => config.unlimitedAmmo = UIKit.Toggle(r, "Unlimited ammo", config.unlimitedAmmo));
            }
            else
            {
                rows.Add(r => config.botCount = Mathf.RoundToInt(UIKit.Slider(r, "Number of bots", config.botCount, 1, 15, "0")));
                rows.Add(r => config.autoBalance = UIKit.Toggle(r, "Auto team balance", config.autoBalance));
                if (!config.autoBalance) rows.Add(r => config.teammateBots = Mathf.RoundToInt(UIKit.Slider(r, "Bots on your team", Mathf.Min(config.teammateBots, config.botCount - 1), 0, Mathf.Max(0, config.botCount - 1), "0")));
                rows.Add(r => config.friendlyFire = UIKit.Toggle(r, "Friendly fire", config.friendlyFire));
            }
            if (config.mode == GameMode.Classic)
            {
                rows.Add(r => config.maxRounds = RoundOptions[UIKit.Selector(r, "Match length", Labels(RoundOptions, v => $"{v} rounds (first to {v / 2 + 1})"), IndexOf(RoundOptions, config.maxRounds, 4))]);
                rows.Add(r => config.roundTimeMinutes = RoundTimes[UIKit.Selector(r, "Round timer", Labels(RoundTimes, v => MathUtil.FormatTime(v * 60f)), IndexOf(RoundTimes, config.roundTimeMinutes, 2))]);
                rows.Add(r => config.bombTimer = BombTimes[UIKit.Selector(r, "Charge timer", Labels(BombTimes, v => $"{v:0} s"), IndexOf(BombTimes, config.bombTimer, 2))]);
                rows.Add(r => config.startMoney = MoneyOptions[UIKit.Selector(r, "Starting money", Labels(MoneyOptions, v => "$" + v), IndexOf(MoneyOptions, config.startMoney, 0))]);
            }
            if (config.mode == GameMode.TeamDeathmatch)
            {
                rows.Add(r => config.tdmKillLimit = KillLimits[UIKit.Selector(r, "Kill limit", Labels(KillLimits, v => v.ToString()), IndexOf(KillLimits, config.tdmKillLimit, 2))]);
                rows.Add(r => config.tdmMinutes = TdmMinutes[UIKit.Selector(r, "Time limit", Labels(TdmMinutes, v => $"{v:0} min"), IndexOf(TdmMinutes, config.tdmMinutes, 2))]);
            }

            float rh = 62 * S;
            var view = new Rect(area.x + 20 * S, area.y + 16 * S, area.width - 40 * S, area.height - 32 * S - 70 * S);
            UIKit.BeginScroll(101, view, rows.Count * rh);
            for (int i = 0; i < rows.Count; i++) rows[i](new Rect(0, i * rh, view.width - 10 * S, rh - 8 * S));
            UIKit.EndScroll();
            if (config.mode != GameMode.Practice || config.practiceBots == PracticeBotMode.Fighting)
                GUI.Label(new Rect(area.x + 20 * S, area.yMax - 74 * S, area.width - 40 * S, 60 * S), DifficultyNotes[(int)config.difficulty], UIKit.Small);

            if (UIKit.Button(new Rect(sa.xMax - 360 * S, sa.yMax - 86 * S, 320 * S, 70 * S), "START MATCH", true))
            {
                GameApp.I.StartMatch(config);
            }
        }

        static string[] Labels<T>(T[] arr, System.Func<T, string> f) { var s = new string[arr.Length]; for (int i = 0; i < arr.Length; i++) s[i] = f(arr[i]); return s; }
        static int IndexOf<T>(T[] arr, T v, int def) { int i = System.Array.IndexOf(arr, v); return i < 0 ? def : i; }

        // ------------------------------------------------------------ Map select & preview
        void DrawMapSelect(Rect sa, float S)
        {
            GUI.Label(new Rect(sa.x + 160 * S, sa.y + 16 * S, 600 * S, 50 * S), "SELECT MAP", UIKit.Header);
            var maps = MapLibrary.All;
            int cols = Mathf.Min(maps.Count, 5);
            float gap = 16 * S;
            float cw = (sa.width - 80 * S - gap * (cols - 1)) / cols;
            float ch = cw * 1.05f;
            for (int i = 0; i < maps.Count; i++)
            {
                var r = new Rect(sa.x + 40 * S + (i % cols) * (cw + gap), sa.y + 90 * S + (i / cols) * (ch + gap), cw, ch);
                bool sel = config.mapIndex == i;
                UIKit.DrawPanel(r, sel ? new Color(1f, 0.9f, 0.6f) : (Color?)null);
                var tex = MapLibrary.Preview(maps[i]);
                float pw = cw - 20 * S, ph = pw * maps[i].height / maps[i].width;
                GUI.DrawTexture(new Rect(r.x + 10 * S, r.y + 10 * S, pw, ph), tex);
                GUI.Label(new Rect(r.x + 10 * S, r.y + 16 * S + ph, cw - 20 * S, 40 * S), maps[i].name, UIKit.Styled(UIKit.Label, 22, sel ? UIKit.Accent : UIKit.Text, TextAnchor.MiddleLeft, true));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !UIKit.IsDragging)
                {
                    config.mapIndex = i;
                    AudioManager.Play2D("ui_click", 0.5f);
                    if (GameApp.I.Background) GameApp.I.Background.ShowMap(i);
                }
            }
            var m = maps[config.mapIndex];
            var info = new Rect(sa.x + 40 * S, sa.y + 110 * S + ch, sa.width - 80 * S, sa.yMax - (sa.y + 110 * S + ch) - 20 * S);
            UIKit.DrawPanel(info);
            GUI.Label(new Rect(info.x + 20 * S, info.y + 12 * S, info.width * 0.6f, 40 * S), m.name.ToUpper(), UIKit.Styled(UIKit.Header, 26, UIKit.Accent, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(info.x + 20 * S, info.y + 54 * S, info.width * 0.62f, info.height - 60 * S), m.description +
                $"\n<color=#9a9>Size {m.width * (int)MapBuilder.Cell} x {m.height * (int)MapBuilder.Cell} m • Bomb sites A and B • 3 lanes</color>", UIKit.Label);
            if (UIKit.Button(new Rect(info.xMax - 300 * S, info.y + 20 * S, 280 * S, 58 * S), "PREVIEW FLYOVER"))
            {
                if (GameApp.I.Background) { GameApp.I.Background.ShowMap(config.mapIndex); GameApp.I.Background.fullPreview = true; }
                Go(Page.MapPreview);
            }
            if (UIKit.Button(new Rect(info.xMax - 300 * S, info.y + 90 * S, 280 * S, 58 * S), "USE THIS MAP", true)) Back();
        }

        void DrawMapPreview(Rect sa, float S)
        {
            var m = MapLibrary.All[config.mapIndex];
            UIKit.Fill(new Rect(0, sa.yMax - 120 * S, Screen.width, 120 * S + (Screen.height - sa.yMax)), new Color(0, 0, 0, 0.5f));
            GUI.Label(new Rect(sa.x + 40 * S, sa.yMax - 110 * S, sa.width * 0.7f, 50 * S), m.name.ToUpper(), UIKit.Styled(UIKit.Header, 34, UIKit.Text, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(sa.x + 40 * S, sa.yMax - 60 * S, sa.width * 0.7f, 50 * S), "Live flyover preview", UIKit.Small);
            var tex = MapLibrary.Preview(m);
            float mw = 260 * S, mh = mw * m.height / m.width;
            UIKit.Fill(new Rect(sa.xMax - mw - 30 * S, sa.y + 20 * S, mw + 10 * S, mh + 10 * S), new Color(0, 0, 0, 0.5f));
            GUI.DrawTexture(new Rect(sa.xMax - mw - 25 * S, sa.y + 25 * S, mw, mh), tex);
            if (UIKit.Button(new Rect(sa.xMax - 230 * S, sa.yMax - 96 * S, 200 * S, 64 * S), "DONE", true)) Back();
        }

        // ------------------------------------------------------------ Settings (shared with pause menu)
        static readonly string[] Tabs = { "GRAPHICS", "CONTROLS", "AUDIO", "GAMEPLAY" };
        static readonly string[] Quality = { "Low", "Medium", "High" };
        static readonly string[] Shadows = { "Off", "Hard", "Soft" };
        static readonly string[] CrossStyles = { "Dynamic", "Static", "Dot", "Cross + dot" };
        static readonly int[] Fps = { 30, 60, 120 };

        public void DrawSettings(Rect sa, float S, bool inMatch)
        {
            GUI.Label(new Rect(sa.x + 160 * S, sa.y + 16 * S, 600 * S, 50 * S), "SETTINGS", UIKit.Header);
            float tw = 180 * S;
            for (int i = 0; i < Tabs.Length; i++)
                if (UIKit.Button(new Rect(sa.x + 40 * S + i * (tw + 10 * S), sa.y + 84 * S, tw, 54 * S), Tabs[i], settingsTab == i)) settingsTab = i;

            var area = new Rect(sa.x + 40 * S, sa.y + 150 * S, sa.width - 80 * S, sa.height - 170 * S);
            UIKit.DrawPanel(area);
            var s = GameApp.Settings;
            var rows = new List<System.Action<Rect>>();
            switch (settingsTab)
            {
                case 0:
                    rows.Add(r => { int q = UIKit.Selector(r, "Quality preset", Quality, s.graphicsQuality); if (q != s.graphicsQuality) { s.graphicsQuality = q; s.shadowQuality = q; s.effectsQuality = q; s.renderScale = q == 0 ? 0.7f : q == 1 ? 0.85f : 1f; } });
                    rows.Add(r => s.targetFps = Fps[UIKit.Selector(r, "Frame-rate target", new[] { "30 FPS (battery)", "60 FPS", "120 FPS (ProMotion)" }, IndexOf(Fps, s.targetFps, 1))]);
                    rows.Add(r => s.shadowQuality = UIKit.Selector(r, "Shadows", Shadows, s.shadowQuality));
                    rows.Add(r => s.effectsQuality = UIKit.Selector(r, "Effects", Quality, s.effectsQuality));
                    rows.Add(r => s.renderScale = Mathf.Round(UIKit.Slider(r, "Resolution scale", s.renderScale, 0.5f, 1f) * 20f) / 20f);
                    break;
                case 1:
                    rows.Add(r => s.sensX = UIKit.Slider(r, "Look sensitivity (horizontal)", s.sensX, 0.2f, 3f));
                    rows.Add(r => s.sensY = UIKit.Slider(r, "Look sensitivity (vertical)", s.sensY, 0.2f, 3f));
                    rows.Add(r => s.scopedSens = UIKit.Slider(r, "Scoped sensitivity", s.scopedSens, 0.1f, 2f));
                    rows.Add(r => s.aimAcceleration = UIKit.Slider(r, "Aim acceleration", s.aimAcceleration, 0f, 1f));
                    rows.Add(r => s.gyroEnabled = UIKit.Toggle(r, "Gyroscope aiming", s.gyroEnabled));
                    if (s.gyroEnabled)
                    {
                        rows.Add(r => s.gyroSens = UIKit.Slider(r, "Gyroscope sensitivity", s.gyroSens, 0.2f, 3f));
                        rows.Add(r => s.invertGyroY = UIKit.Toggle(r, "Invert gyroscope vertical", s.invertGyroY));
                    }
                    rows.Add(r => s.buttonScale = UIKit.Slider(r, "Button size", s.buttonScale, 0.6f, 1.6f));
                    rows.Add(r => s.joystickSize = UIKit.Slider(r, "Joystick size", s.joystickSize, 0.6f, 1.6f));
                    rows.Add(r => s.leftHanded = UIKit.Toggle(r, "Left-handed layout", s.leftHanded));
                    rows.Add(r => s.leftFireButton = UIKit.Toggle(r, "Second fire button (left side)", s.leftFireButton));
                    rows.Add(r => s.aimAssist = UIKit.Toggle(r, "Aim assist (slowdown on target)", s.aimAssist));
                    rows.Add(r => s.haptics = UIKit.Toggle(r, "Haptic feedback", s.haptics));
                    rows.Add(r => s.mouseSens = UIKit.Slider(r, "Mouse sensitivity (desktop)", s.mouseSens, 0.2f, 3f));
                    rows.Add(r => { if (UIKit.Button(new Rect(r.x, r.y, r.width * 0.5f, r.height), "CUSTOMIZE BUTTON LAYOUT", true)) { if (inMatch) GameApp.I.Hud.OpenLayoutEditor(); else Go(Page.Layout); } });
                    break;
                case 2:
                    rows.Add(r => s.masterVolume = UIKit.Slider(r, "Master volume", s.masterVolume, 0f, 1f));
                    rows.Add(r => s.musicVolume = UIKit.Slider(r, "Music volume", s.musicVolume, 0f, 1f));
                    rows.Add(r => s.sfxVolume = UIKit.Slider(r, "Sound effects volume", s.sfxVolume, 0f, 1f));
                    rows.Add(r => s.ambientVolume = UIKit.Slider(r, "Footsteps & environment", s.ambientVolume, 0f, 1f));
                    break;
                case 3:
                    rows.Add(r => s.autoReload = UIKit.Toggle(r, "Auto reload when empty", s.autoReload));
                    rows.Add(r => s.autoFire = UIKit.Toggle(r, "Auto-fire semi-automatic weapons (hold)", s.autoFire));
                    rows.Add(r => s.crosshairStyle = UIKit.Selector(r, "Crosshair style", CrossStyles, s.crosshairStyle));
                    rows.Add(r => s.crosshairSize = UIKit.Slider(r, "Crosshair size", s.crosshairSize, 0.5f, 2f));
                    rows.Add(r => s.crosshairColor = UIKit.Selector(r, "Crosshair colour", GameSettings.CrosshairColorNames, s.crosshairColor));
                    rows.Add(r => s.damageIndicators = UIKit.Toggle(r, "Damage direction indicators", s.damageIndicators));
                    rows.Add(r => s.hitMarker = UIKit.Toggle(r, "Hit markers", s.hitMarker));
                    rows.Add(r => s.showRadar = UIKit.Toggle(r, "Radar", s.showRadar));
                    if (!inMatch)
                    {
                        rows.Add(r => { if (UIKit.Button(new Rect(r.x, r.y, r.width * 0.5f, r.height), "REPLAY CONTROLS TUTORIAL")) { s.tutorialDone = false; GameApp.I.ApplySettings(); page = Page.Main; history.Clear(); firstLaunchPrompt = true; } });
                    }
                    rows.Add(r => GUI.Label(r, "Friendly fire is configured per match in the match setup screen.", UIKit.Small));
                    break;
            }
            float rh = 64 * S;
            var view = new Rect(area.x + 24 * S, area.y + 16 * S, area.width - 48 * S, area.height - 32 * S);
            UIKit.BeginScroll(200 + settingsTab, view, rows.Count * rh);
            bool changed = GUI.changed; GUI.changed = false;
            for (int i = 0; i < rows.Count; i++) rows[i](new Rect(0, i * rh, view.width - 12 * S, rh - 10 * S));
            UIKit.EndScroll();
            if (GUI.changed) GameApp.I.ApplySettings(false);
            GUI.changed = changed || GUI.changed;
        }

        // ------------------------------------------------------------ Weapon info
        static readonly WeaponCategory[] WeaponTabs = { WeaponCategory.Pistol, WeaponCategory.SMG, WeaponCategory.Shotgun, WeaponCategory.Rifle, WeaponCategory.Sniper, WeaponCategory.Heavy, WeaponCategory.Grenade, WeaponCategory.Equipment };

        void DrawWeapons(Rect sa, float S)
        {
            GUI.Label(new Rect(sa.x + 160 * S, sa.y + 16 * S, 600 * S, 50 * S), "WEAPON INFORMATION", UIKit.Header);
            float tw = (sa.width - 80 * S - 7 * 8 * S) / WeaponTabs.Length;
            for (int i = 0; i < WeaponTabs.Length; i++)
                if (UIKit.Button(new Rect(sa.x + 40 * S + i * (tw + 8 * S), sa.y + 84 * S, tw, 50 * S), WeaponTabs[i].ToString().ToUpper(), weaponTab == WeaponTabs[i])) { weaponTab = WeaponTabs[i]; selectedWeapon = null; }

            var list = new List<WeaponDef>(WeaponDatabase.Category(weaponTab));
            if (weaponTab == WeaponCategory.Equipment) { list.Add(WeaponDatabase.Knife); list.Add(WeaponDatabase.Charge); }
            if (selectedWeapon == null && list.Count > 0) selectedWeapon = list[0];

            var left = new Rect(sa.x + 40 * S, sa.y + 146 * S, sa.width * 0.32f, sa.height - 166 * S);
            UIKit.DrawPanel(left);
            float rh = 64 * S;
            var view = new Rect(left.x + 12 * S, left.y + 12 * S, left.width - 24 * S, left.height - 24 * S);
            UIKit.BeginScroll(300, view, list.Count * rh);
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                string price = w.price > 0 ? $"  ${w.price}" : "";
                if (UIKit.Button(new Rect(0, i * rh, view.width - 10 * S, rh - 8 * S), w.name + price, w == selectedWeapon) && !UIKit.IsDragging) selectedWeapon = w;
            }
            UIKit.EndScroll();

            var d = selectedWeapon;
            var right = new Rect(left.xMax + 20 * S, left.y, sa.xMax - left.xMax - 60 * S, left.height);
            UIKit.DrawPanel(right);
            if (d == null) return;
            float x = right.x + 24 * S, y = right.y + 16 * S, w2 = right.width - 48 * S;
            GUI.Label(new Rect(x, y, w2, 44 * S), d.name.ToUpper(), UIKit.Styled(UIKit.Header, 30, UIKit.Text, TextAnchor.MiddleLeft, true)); y += 44 * S;
            string team = d.team == Team.None ? "Both teams" : d.team.Label() + " only";
            GUI.Label(new Rect(x, y, w2, 30 * S), $"{d.archetype} • {team}" + (d.price > 0 ? $" • ${d.price}" : ""), UIKit.Small); y += 36 * S;
            GUI.Label(new Rect(x, y, w2, 60 * S), d.description, UIKit.Label); y += 64 * S;
            if (d.IsGun)
            {
                void Stat(string n, float k, string v) { GUI.Label(new Rect(x, y, w2 * 0.3f, 34 * S), n, UIKit.Label); UIKit.Bar(new Rect(x + w2 * 0.3f, y + 11 * S, w2 * 0.5f, 12 * S), k, UIKit.Accent); GUI.Label(new Rect(x + w2 * 0.82f, y, w2 * 0.18f, 34 * S), v, UIKit.Styled(UIKit.Label, 20, UIKit.Text, TextAnchor.MiddleRight)); y += 38 * S; }
                Stat("Damage", d.damage * d.pellets / 120f, d.pellets > 1 ? $"{d.damage}x{d.pellets}" : d.damage.ToString("0"));
                Stat("Fire rate", d.rpm / 900f, $"{d.rpm:0} rpm");
                Stat("Accuracy", 1f - Mathf.Clamp01((d.spreadStand + d.spreadPerShot * 3f) / 4f), (d.spreadStand).ToString("0.00") + "°");
                Stat("Recoil control", 1f - Mathf.Clamp01(d.recoilUp / 4.5f), d.recoilUp.ToString("0.0"));
                Stat("Range", d.effectiveRange / 200f, $"{d.effectiveRange:0} m");
                Stat("Armour pen.", d.armorPenetration, $"{d.armorPenetration * 100:0}%");
                Stat("Mobility", (d.moveSpeed - 0.75f) / 0.3f, $"{d.moveSpeed * 100:0}%");
                GUI.Label(new Rect(x, y, w2, 34 * S), $"Magazine {d.magazine} / {d.reserve}  •  Reload {d.reloadTime:0.0}s  •  {d.fireMode}" + (d.HasScope ? "  •  Scope" : "") + (d.silenced ? "  •  Suppressed" : "") + $"  •  Kill reward ${d.killReward:0}", UIKit.Small);
            }
        }

        // ------------------------------------------------------------ Profile
        void DrawProfile(Rect sa, float S)
        {
            GUI.Label(new Rect(sa.x + 160 * S, sa.y + 16 * S, 600 * S, 50 * S), "PLAYER PROFILE", UIKit.Header);
            var st = GameApp.Stats;
            var area = new Rect(sa.x + 40 * S, sa.y + 90 * S, sa.width - 80 * S, sa.height - 110 * S);
            UIKit.DrawPanel(area);
            float kd = st.kills / (float)Mathf.Max(1, st.deaths);
            float hs = st.kills > 0 ? st.headshots * 100f / st.kills : 0f;
            float acc = st.shotsFired > 0 ? st.shotsHit * 100f / st.shotsFired : 0f;
            var tiles = new (string, string)[]
            {
                ("Matches played", st.matchesPlayed.ToString()), ("Won / Lost / Drawn", $"{st.matchesWon} / {st.matchesLost} / {st.matchesDrawn}"),
                ("Win rate", st.matchesPlayed > 0 ? $"{st.matchesWon * 100f / st.matchesPlayed:0}%" : "-"), ("Kills", st.kills.ToString()), ("Deaths", st.deaths.ToString()),
                ("K/D ratio", kd.ToString("0.00")), ("Headshots", $"{st.headshots} ({hs:0}%)"), ("Accuracy", $"{acc:0.0}%"), ("Best kill streak", st.bestKillStreak.ToString()),
                ("Rounds won", $"{st.roundsWon} / {st.roundsPlayed}"), ("Charges planted / defused", $"{st.plants} / {st.defuses}"), ("Favourite weapon", st.FavouriteWeapon()),
                ("Best map", st.BestMap()),
            };
            int cols = 3;
            float tw = (area.width - 40 * S - (cols - 1) * 14 * S) / cols, th = 82 * S;
            for (int i = 0; i < tiles.Length; i++)
            {
                int c = i % cols, r = i / cols;
                var t = new Rect(area.x + 20 * S + c * (tw + 14 * S), area.y + 20 * S + r * (th + 12 * S), i == tiles.Length - 1 ? tw * 2 + 14 * S : tw, th);
                UIKit.Fill(t, UIKit.PanelLight);
                GUI.Label(new Rect(t.x + 14 * S, t.y + 6 * S, t.width - 28 * S, 30 * S), tiles[i].Item1, UIKit.Small);
                GUI.Label(new Rect(t.x + 14 * S, t.y + 34 * S, t.width - 28 * S, 44 * S), tiles[i].Item2, UIKit.Styled(UIKit.Label, 26, UIKit.Text, TextAnchor.MiddleLeft, true));
            }
            var rb = new Rect(area.xMax - 300 * S, area.yMax - 80 * S, 280 * S, 60 * S);
            if (!confirmResetStats) { if (UIKit.Button(rb, "RESET STATISTICS")) confirmResetStats = true; }
            else
            {
                GUI.Label(new Rect(rb.x - 330 * S, rb.y, 320 * S, rb.height), "Erase all statistics?", UIKit.Styled(UIKit.Label, 22, UIKit.Danger, TextAnchor.MiddleRight));
                if (UIKit.Button(new Rect(rb.x, rb.y, 135 * S, rb.height), "YES")) { GameApp.ResetStats(); confirmResetStats = false; }
                if (UIKit.Button(new Rect(rb.x + 145 * S, rb.y, 135 * S, rb.height), "NO")) confirmResetStats = false;
            }
        }

        // ------------------------------------------------------------ About
        void DrawAbout(Rect sa, float S)
        {
            GUI.Label(new Rect(sa.x + 160 * S, sa.y + 16 * S, 600 * S, 50 * S), "ABOUT", UIKit.Header);
            var area = new Rect(sa.x + 40 * S, sa.y + 90 * S, sa.width - 80 * S, sa.height - 110 * S);
            UIKit.DrawPanel(area);
            string text =
                "<b>WARDLINE</b>  v" + GameApp.Version + "\n" +
                "An offline, round-based tactical shooter for iPhone. Raiders attack two bomb sites with a breach charge; Wardens defend and defuse. " +
                "Buy weapons with round earnings, play as a team with radio commands, and face bots whose difficulty changes how they think — not how much damage they take.\n\n" +
                "<b>Touch controls</b>\nLeft thumb: floating joystick (light push = silent walk). Right thumb: swipe to look. FIRE can be dragged to steer while shooting. " +
                "AIM toggles scopes (or stabs with the knife). USE plants, defuses and swaps weapons. All buttons can be moved and resized in Settings › Controls.\n\n" +
                "<b>Keyboard (editor/desktop)</b>\nWASD move, Shift walk, Ctrl crouch, Space jump, Mouse aim/fire, RMB scope, R reload, E use, B buy, F grenades, G drop, Q last weapon, 1-5 slots, Tab scores, Z radio, Esc pause.\n\n" +
                "<b>Credits</b>\nDesign and code: original work. All models, textures, sounds and music are generated procedurally at runtime — no third-party assets. Built with Unity.";
            var view = new Rect(area.x + 24 * S, area.y + 20 * S, area.width - 48 * S, area.height - 40 * S);
            float h = UIKit.Label.CalcHeight(new GUIContent(text), view.width - 12 * S) + 20 * S;
            UIKit.BeginScroll(400, view, h);
            GUI.Label(new Rect(0, 0, view.width - 12 * S, h), text, UIKit.Label);
            UIKit.EndScroll();
        }
    }
}
