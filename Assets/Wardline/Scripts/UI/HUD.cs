using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    /// <summary>
    /// In-match interface: touch controls, crosshair/scope, health/armour/ammo/money, timer and scores,
    /// radar, kill feed, announcements, charge status, damage/flash/smoke overlays, buy menu, radio,
    /// scoreboard, pause menu, end-of-match screen and the first-launch tutorial.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        bool buyOpen, radioOpen, layoutOpen, confirmQuit, pauseSettings;
        WeaponCategory buyTab = WeaponCategory.Rifle;
        LayoutEditor layoutEditor;
        Tutorial tutorial;

        struct Announcement { public string text; public Color color; public float time; }
        readonly List<Announcement> announcements = new List<Announcement>();
        struct RadioLine { public string text; public Color color; public float time; }
        readonly List<RadioLine> radio = new List<RadioLine>();
        struct DamageMark { public Vector3 from; public float time; }
        readonly List<DamageMark> damageMarks = new List<DamageMark>();
        float lastDamageTime = -10f;

        public bool AnyMenuOpen => buyOpen || radioOpen || layoutOpen || (tutorial != null && tutorial.Active);

        MatchManager MM => MatchManager.I;
        Actor Player => MM != null ? MM.Player : null;
        PlayerController PC => Player != null ? Player.player : null;

        public void OnMatchStarted()
        {
            buyOpen = radioOpen = layoutOpen = confirmQuit = pauseSettings = false;
            announcements.Clear(); radio.Clear(); damageMarks.Clear();
            GameEvents.Announce -= OnAnnounce; GameEvents.Announce += OnAnnounce;
            GameEvents.Radio -= OnRadio; GameEvents.Radio += OnRadio;
            GameEvents.Damaged -= OnDamaged; GameEvents.Damaged += OnDamaged;
            tutorial = !GameApp.Settings.tutorialDone ? new Tutorial() : null;
        }

        void OnDisable()
        {
            GameEvents.Announce -= OnAnnounce; GameEvents.Radio -= OnRadio; GameEvents.Damaged -= OnDamaged;
        }

        void OnAnnounce(string t, Color c) { announcements.Add(new Announcement { text = t, color = c, time = Time.unscaledTime }); if (announcements.Count > 3) announcements.RemoveAt(0); }

        void OnRadio(Actor from, string t)
        {
            if (Player == null || from == null || (from.team != Player.team && MM.Config.mode != GameMode.Practice)) return;
            radio.Add(new RadioLine { text = $"<b>{from.displayName}</b> (radio): {t}", color = from.team.Tint(), time = Time.unscaledTime });
            if (radio.Count > 5) radio.RemoveAt(0);
            if (from != Player) AudioManager.Play2D("radio", 0.4f);
        }

        void OnDamaged(Actor attacker, Actor victim, float amount, Vector3 src)
        {
            if (victim != Player) return;
            lastDamageTime = Time.time;
            damageMarks.Add(new DamageMark { from = src, time = Time.time });
            if (damageMarks.Count > 6) damageMarks.RemoveAt(0);
            if (GameApp.Settings.haptics) Haptics.Pulse(1, 0.1f);
        }

        public void OpenLayoutEditor() { layoutOpen = true; layoutEditor = new LayoutEditor(); }

        void Update()
        {
            if (MM == null || PC == null) return;
            var f = PC.controls.frame;
            bool tutorialActive = tutorial != null && tutorial.Active;
            if (tutorialActive && Time.timeScale > 0f) Time.timeScale = 0f;

            if (!GameApp.I.Paused && !tutorialActive)
            {
                if (f.pausePressed || (AnyMenuOpen && InputBridge.KeyDown(K.Esc))) { if (AnyMenuOpen) CloseMenus(); else GameApp.I.SetPaused(true); }
                if (f.buyPressed || (buyOpen && InputBridge.KeyDown(K.B))) ToggleBuy();
                if (f.radioPressed || (radioOpen && InputBridge.KeyDown(K.Z))) { radioOpen = !radioOpen; buyOpen = false; }
                if (radioOpen)
                    for (int i = 0; i < 5; i++)
                        if (InputBridge.KeyDown((K)((int)K.One + i))) { MM.PlayerRadio(i); radioOpen = false; }
            }
            else if (GameApp.I.Paused && InputBridge.KeyDown(K.Esc)) { GameApp.I.SetPaused(false); }

            if (buyOpen && !MM.CanBuy(Player)) buyOpen = false;
            PC.inputBlocked = AnyMenuOpen;
            if (!GameApp.I.Paused) InputBridge.SetCursorLocked(!AnyMenuOpen && MM.Phase != MatchPhase.MatchOver);
        }

        void ToggleBuy()
        {
            if (!buyOpen && !MM.CanBuy(Player)) { AudioManager.Play2D("deny", 0.5f); OnAnnounce("Buying is not available here", UIKit.Danger); return; }
            buyOpen = !buyOpen; radioOpen = false;
        }

        void CloseMenus() { buyOpen = radioOpen = false; if (layoutOpen) { layoutOpen = false; GameApp.I.ApplySettings(); } }

        // ================================================================= Drawing
        void OnGUI()
        {
            if (MM == null || Player == null || PC == null) return;
            GUI.depth = 1;
            var sa = TouchControls.SafeAreaTopLeft;
            float S = UIKit.S;
            var p = Player;

            if (layoutOpen) { if (layoutEditor.Draw(sa)) { layoutOpen = false; GameApp.I.ApplySettings(); } return; }

            DrawScreenEffects(p, S);
            if (p.alive) DrawCrosshairOrScope(p, S);
            DrawTopBar(sa, S);
            if (GameApp.Settings.showRadar) DrawRadar(sa, S);
            DrawKillFeed(sa, S);
            DrawRadioLines(sa, S);
            if (p.alive) DrawStatusBar(sa, S);
            DrawObjective(sa, S);
            DrawAnnouncements(sa, S);
            if (MM.Config.mode == GameMode.Practice) DrawPractice(sa, S);
            if (!p.alive && MM.Phase != MatchPhase.MatchOver) DrawSpectator(sa, S);
            if (Application.isMobilePlatform || InputBridge.HasTouchscreen) DrawTouchControls(S);

            if (buyOpen) DrawBuyMenu(sa, S);
            if (radioOpen) DrawRadioMenu(sa, S);
            if (PC.controls.frame.scoreHeld || MM.Phase == MatchPhase.HalfTime) DrawScoreboard(sa, S, false);
            if (MM.Phase == MatchPhase.MatchOver) { DrawScoreboard(sa, S, true); return; }
            if (tutorial != null && tutorial.Active) { if (tutorial.Draw(sa)) { GameApp.Settings.tutorialDone = true; GameApp.I.ApplySettings(); Time.timeScale = 1f; } return; }
            if (GameApp.I.Paused) DrawPause(sa, S);
        }

        // ---------------------------------------------------------------- Overlays
        void DrawScreenEffects(Actor p, float S)
        {
            var full = new Rect(0, 0, Screen.width, Screen.height);
            if (p.alive && p.health < 35f)
            {
                float pulse = 0.25f + 0.15f * Mathf.Sin(Time.time * 5f);
                var old = GUI.color; GUI.color = new Color(0.8f, 0f, 0f, pulse * (1f - p.health / 35f));
                GUI.DrawTexture(full, UIKit.ScopeMask());
                GUI.color = old;
            }
            if (Time.time - lastDamageTime < 0.25f) UIKit.Fill(full, new Color(0.7f, 0f, 0f, 0.18f * (1f - (Time.time - lastDamageTime) / 0.25f)));
            float smoke = SmokeCloud.DensityAt(PC.cam.transform.position);
            if (smoke > 0f) UIKit.Fill(full, new Color(0.72f, 0.72f, 0.7f, smoke * 0.95f));

            if (GameApp.Settings.damageIndicators)
            {
                var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
                damageMarks.RemoveAll(d => Time.time - d.time > 1.6f);
                foreach (var d in damageMarks)
                {
                    Vector3 to = (d.from - p.transform.position).Flat();
                    if (to.sqrMagnitude < 0.01f) continue;
                    float ang = Vector3.SignedAngle(PC.cam.transform.forward.Flat(), to, Vector3.up);
                    float a = 1f - (Time.time - d.time) / 1.6f;
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(ang, c);
                    var old = GUI.color; GUI.color = new Color(1f, 0.15f, 0.1f, a * 0.85f);
                    GUI.DrawTexture(new Rect(c.x - 70 * S, c.y - 190 * S, 140 * S, 18 * S), UIKit.Rounded(Color.white, Color.white, 8, 0));
                    GUI.color = old; GUI.matrix = m;
                }
            }

            float blind = p.BlindAmount;
            if (blind > 0f) UIKit.Fill(full, new Color(1f, 1f, 1f, Mathf.Clamp01(blind * 1.15f)));
        }

        void DrawCrosshairOrScope(Actor p, float S)
        {
            var s = GameApp.Settings;
            var w = p.weapons;
            var def = w.CurrentDef;
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Color col = GameSettings.CrosshairColors[Mathf.Clamp(s.crosshairColor, 0, GameSettings.CrosshairColors.Length - 1)];

            if (w.zoomLevel > 0 && def != null && def.scopeOverlay)
            {
                float d = Mathf.Min(Screen.width, Screen.height) * 1.05f;
                var r = new Rect(cx - d / 2, cy - d / 2, d, d);
                GUI.DrawTexture(r, UIKit.ScopeMask());
                UIKit.Fill(new Rect(0, 0, r.x + 1, Screen.height), Color.black);
                UIKit.Fill(new Rect(r.xMax - 1, 0, Screen.width - r.xMax + 1, Screen.height), Color.black);
                UIKit.Fill(new Rect(0, 0, Screen.width, r.y + 1), Color.black);
                UIKit.Fill(new Rect(0, r.yMax - 1, Screen.width, Screen.height - r.yMax + 1), Color.black);
                UIKit.Fill(new Rect(r.x, cy - 0.5f, r.width, 1.5f), Color.black);
                UIKit.Fill(new Rect(cx - 0.5f, r.y, 1.5f, r.height), Color.black);
                DrawHitMarker(cx, cy, S);
                return;
            }
            if (def == null || def.category == WeaponCategory.Bomb) return;

            float size = 9f * S * s.crosshairSize, thick = Mathf.Max(2f, 2.2f * S * s.crosshairSize * 0.8f);
            float gap = 5f * S * s.crosshairSize;
            if (s.crosshairStyle == 0 && def.IsGun)
            {
                // Dynamic gap mirrors the real accuracy cone (converted from degrees to pixels).
                float spreadPx = Mathf.Tan(w.CurrentSpread() * Mathf.Deg2Rad) / Mathf.Tan(PC.cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Screen.height * 0.5f;
                gap = Mathf.Max(gap, spreadPx);
            }
            if (s.crosshairStyle != 2)
            {
                void Line(Rect r) { UIKit.Fill(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(0, 0, 0, 0.6f)); UIKit.Fill(r, col); }
                Line(new Rect(cx - gap - size, cy - thick / 2, size, thick));
                Line(new Rect(cx + gap, cy - thick / 2, size, thick));
                Line(new Rect(cx - thick / 2, cy - gap - size, thick, size));
                Line(new Rect(cx - thick / 2, cy + gap, thick, size));
            }
            if (s.crosshairStyle >= 2) { float dsz = thick * 1.4f; UIKit.Fill(new Rect(cx - dsz / 2 - 1, cy - dsz / 2 - 1, dsz + 2, dsz + 2), new Color(0, 0, 0, 0.6f)); UIKit.Fill(new Rect(cx - dsz / 2, cy - dsz / 2, dsz, dsz), col); }
            DrawHitMarker(cx, cy, S);
        }

        void DrawHitMarker(float cx, float cy, float S)
        {
            if (!GameApp.Settings.hitMarker) return;
            float t = Time.time - PlayerHitFeedback.lastHitTime;
            if (t > 0.18f) return;
            bool kill = Time.time - PlayerHitFeedback.lastKillTime < 0.3f;
            Color c = kill ? new Color(1f, 0.25f, 0.2f) : PlayerHitFeedback.lastWasHead ? new Color(1f, 0.85f, 0.2f) : Color.white;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
            float len = 9f * S, off = 7f * S, th = 2.5f * S;
            UIKit.Fill(new Rect(cx - off - len, cy - th / 2, len, th), c);
            UIKit.Fill(new Rect(cx + off, cy - th / 2, len, th), c);
            UIKit.Fill(new Rect(cx - th / 2, cy - off - len, th, len), c);
            UIKit.Fill(new Rect(cx - th / 2, cy + off, th, len), c);
            GUI.matrix = m;
        }

        // ---------------------------------------------------------------- Top bar: scores, timer, alive pips
        void DrawTopBar(Rect sa, float S)
        {
            var r = TouchControls.ScoreRect;
            UIKit.Fill(r, new Color(0, 0, 0, 0.45f));
            bool tdm = MM.Config.mode == GameMode.TeamDeathmatch;
            string timer;
            if (MM.Config.mode == GameMode.Practice) timer = "PRACTICE";
            else if (tdm) timer = MathUtil.FormatTime(MM.MatchTimeLeft);
            else if (MM.Phase == MatchPhase.Freeze) timer = MathUtil.FormatTime(MM.PhaseTimeLeft);
            else if (MM.ChargePlanted) timer = "• • •";
            else if (MM.Phase == MatchPhase.Live) timer = MathUtil.FormatTime(MM.RoundTimeLeft);
            else timer = "--:--";

            var mid = UIKit.Styled(UIKit.Label, 30, Color.white, TextAnchor.MiddleCenter, true);
            if (MM.ChargePlanted) mid.normal.textColor = Color.Lerp(UIKit.Danger, Color.white, Mathf.PingPong(Time.time * 2f, 1f));
            GUI.Label(new Rect(r.center.x - 60 * S, r.y, 120 * S, r.height * 0.7f), timer, mid);
            if (MM.Config.mode != GameMode.Practice)
            {
                var rs = UIKit.Styled(UIKit.Label, 30, Team.Raiders.Tint(), TextAnchor.MiddleCenter, true);
                var ws = UIKit.Styled(UIKit.Label, 30, Team.Wardens.Tint(), TextAnchor.MiddleCenter, true);
                GUI.Label(new Rect(r.x, r.y, 70 * S, r.height * 0.7f), MM.raiderScore.ToString(), rs);
                GUI.Label(new Rect(r.xMax - 70 * S, r.y, 70 * S, r.height * 0.7f), MM.wardenScore.ToString(), ws);
                // Alive pips
                if (!tdm) { DrawPips(new Rect(r.x + 70 * S, r.y + r.height * 0.25f, 70 * S, 14 * S), Team.Raiders, false); DrawPips(new Rect(r.xMax - 140 * S, r.y + r.height * 0.25f, 70 * S, 14 * S), Team.Wardens, true); }
                string sub = tdm ? $"First to {MM.Config.tdmKillLimit}" : $"Round {MM.roundNumber}/{MM.Config.maxRounds}";
                GUI.Label(new Rect(r.x, r.y + r.height * 0.62f, r.width, r.height * 0.38f), sub, UIKit.Styled(UIKit.Small, 15, UIKit.TextDim, TextAnchor.MiddleCenter));
            }
            // Pause button
            var pr = TouchControls.PauseRect;
            UIKit.Fill(pr, new Color(0, 0, 0, 0.35f));
            UIKit.Fill(new Rect(pr.x + pr.width * 0.33f, pr.y + pr.height * 0.28f, pr.width * 0.1f, pr.height * 0.44f), Color.white);
            UIKit.Fill(new Rect(pr.x + pr.width * 0.57f, pr.y + pr.height * 0.28f, pr.width * 0.1f, pr.height * 0.44f), Color.white);
        }

        void DrawPips(Rect r, Team t, bool rightAlign)
        {
            int n = 0, alive = 0;
            foreach (var a in Actor.All) if (a.team == t) { n++; if (a.alive) alive++; }
            float w = Mathf.Min(r.width / Mathf.Max(n, 1), r.height);
            for (int i = 0; i < n; i++)
            {
                float x = rightAlign ? r.xMax - (i + 1) * w : r.x + i * w;
                UIKit.Fill(new Rect(x + 1, r.y, w - 2, r.height), i < alive ? t.Tint() : new Color(0.3f, 0.3f, 0.3f, 0.6f));
            }
        }

        // ---------------------------------------------------------------- Radar
        void DrawRadar(Rect sa, float S)
        {
            float size = 150f * S;
            var r = new Rect(sa.x + 10 * S, sa.y + 10 * S, size, size);
            var def = MM.Map.def;
            float Wm = def.width * MapBuilder.Cell, Hm = def.height * MapBuilder.Cell;
            const float range = 26f;
            Vector3 c = Player.alive ? Player.transform.position : PC.cam.transform.position;
            var tex = MapLibrary.Preview(def);
            UIKit.Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(0, 0, 0, 0.6f));
            var old = GUI.color; GUI.color = new Color(1, 1, 1, 0.85f);
            GUI.DrawTextureWithTexCoords(r, tex, new Rect((c.x - range) / Wm, (c.z - range) / Hm, range * 2 / Wm, range * 2 / Hm));
            GUI.color = old;

            Vector2 ToRadar(Vector3 w) => new Vector2(r.center.x + (w.x - c.x) / range * size / 2, r.center.y - (w.z - c.z) / range * size / 2);
            void Dot(Vector3 w, Color col, float px)
            {
                var p = ToRadar(w);
                p.x = Mathf.Clamp(p.x, r.x + px, r.xMax - px); p.y = Mathf.Clamp(p.y, r.y + px, r.yMax - px);
                GUI.DrawTexture(new Rect(p.x - px, p.y - px, px * 2, px * 2), UIKit.Circle(false), ScaleMode.StretchToFill, true, 0, col, 0, 0);
            }
            foreach (var a in Actor.All)
            {
                if (!a.alive || a == Player) continue;
                if (a.team == Player.team) Dot(a.transform.position, a.weapons.hasCharge ? new Color(1f, 0.85f, 0.2f) : a.team.Tint(), 4f * S);
                else if (EnemySpotted(a)) Dot(a.transform.position, UIKit.Danger, 4.5f * S);
            }
            if (MM.ChargePlanted && Mathf.PingPong(Time.time * 3f, 1f) > 0.4f) Dot(MM.ChargePosition, UIKit.Danger, 6f * S);
            if (Player.team == Team.Raiders && MM.DroppedCharge != null) Dot(MM.DroppedCharge.transform.position, new Color(1f, 0.85f, 0.2f), 5f * S);
            // Site letters
            foreach (int site in new[] { 0, 1 })
            {
                var sp = ToRadar(MM.Map.SiteCenter(site));
                if (r.Contains(sp)) GUI.Label(new Rect(sp.x - 12 * S, sp.y - 12 * S, 24 * S, 24 * S), MM.Map.SiteName(site), UIKit.Styled(UIKit.Label, 22, Color.white, TextAnchor.MiddleCenter, true));
            }
            // Player arrow
            var m = GUI.matrix;
            float yaw = Player.alive ? Player.yaw : PC.cam.transform.eulerAngles.y;
            GUIUtility.RotateAroundPivot(yaw, r.center);
            GUI.Label(new Rect(r.center.x - 12 * S, r.center.y - 14 * S, 24 * S, 24 * S), "▲", UIKit.Styled(UIKit.Label, 20, Color.white, TextAnchor.MiddleCenter, true));
            GUI.matrix = m;

            // Money under the radar
            if (MM.Config.mode == GameMode.Classic)
                UIKit.Shadowed(new Rect(r.x, r.yMax + 6 * S, size, 34 * S), "$" + Player.money, UIKit.Styled(UIKit.Label, 28, new Color(0.55f, 0.95f, 0.5f), TextAnchor.MiddleLeft, true));
        }

        /// <summary>Enemies appear on radar only when the player or a teammate currently sees them.</summary>
        bool EnemySpotted(Actor enemy)
        {
            foreach (var a in Actor.All)
            {
                if (a.team != Player.team || !a.alive) continue;
                if (a.bot != null && a.bot.targetVisible && a.bot.target == enemy) return true;
            }
            if (!Player.alive) return false;
            Vector3 to = enemy.ChestPosition - Player.EyePosition;
            if (Vector3.Angle(Player.aimDirection, to) > PC.cam.fieldOfView * 0.8f) return false;
            return !Physics.Linecast(Player.EyePosition, enemy.ChestPosition, Layers.VisionMask, QueryTriggerInteraction.Collide);
        }

        // ---------------------------------------------------------------- Kill feed, radio, announcements
        void DrawKillFeed(Rect sa, float S)
        {
            float y = TouchControls.PauseRect.yMax + 8 * S;
            float w = 430 * S, h = 30 * S;
            var style = UIKit.Styled(UIKit.Label, 18, Color.white, TextAnchor.MiddleRight);
            style.richText = true;
            for (int i = MM.killFeed.Count - 1; i >= 0; i--)
            {
                var k = MM.killFeed[i];
                if (Time.time - k.time > 6f) continue;
                var r = new Rect(sa.xMax - w - 10 * S, y, w, h);
                UIKit.Fill(r, k.involvesPlayer ? new Color(0.35f, 0.08f, 0.06f, 0.6f) : new Color(0, 0, 0, 0.4f));
                string kc = ColorUtility.ToHtmlStringRGB(k.killerTeam.Tint()), vc = ColorUtility.ToHtmlStringRGB(k.victimTeam.Tint());
                string txt = (string.IsNullOrEmpty(k.killer) ? "" : $"<color=#{kc}>{k.killer}</color>  ") + $"<color=#ccc>[{k.weapon}{(k.headshot ? " ◉" : "")}]</color>  <color=#{vc}>{k.victim}</color>  ";
                GUI.Label(r, txt, style);
                y += h + 4 * S;
            }
        }

        void DrawRadioLines(Rect sa, float S)
        {
            float y = sa.y + sa.height * 0.24f;
            float x = sa.x + 170 * S;
            var style = UIKit.Styled(UIKit.Label, 17, Color.white);
            style.richText = true; style.wordWrap = false;
            radio.RemoveAll(l => Time.unscaledTime - l.time > 7f);
            foreach (var l in radio)
            {
                style.normal.textColor = l.color;
                UIKit.Shadowed(new Rect(x, y, 600 * S, 26 * S), l.text, style);
                y += 26 * S;
            }
        }

        void DrawAnnouncements(Rect sa, float S)
        {
            announcements.RemoveAll(a => Time.unscaledTime - a.time > 3f);
            float y = sa.y + sa.height * 0.26f;
            foreach (var a in announcements)
            {
                float age = Time.unscaledTime - a.time;
                float alpha = Mathf.Clamp01(3f - age) * Mathf.Clamp01(age * 6f);
                var st = UIKit.Styled(UIKit.Label, 34, new Color(a.color.r, a.color.g, a.color.b, alpha), TextAnchor.MiddleCenter, true);
                UIKit.Shadowed(new Rect(0, y, Screen.width, 50 * S), a.text, st);
                y += 52 * S;
            }
            if (MM.Phase == MatchPhase.RoundOver && !string.IsNullOrEmpty(MM.LastRoundReason))
                UIKit.Shadowed(new Rect(0, y + 10 * S, Screen.width, 40 * S), MM.LastRoundReason, UIKit.Styled(UIKit.Label, 24, UIKit.TextDim, TextAnchor.MiddleCenter));
        }

        // ---------------------------------------------------------------- Status bar (health, armour, weapon, ammo)
        void DrawStatusBar(Rect sa, float S)
        {
            var p = Player;
            float w = 520 * S, h = 58 * S;
            var r = new Rect(sa.center.x - w / 2, sa.yMax - h - 6 * S, w, h);
            UIKit.Fill(r, new Color(0, 0, 0, 0.42f));
            var big = UIKit.Styled(UIKit.Label, 34, Color.white, TextAnchor.MiddleLeft, true);
            var small = UIKit.Styled(UIKit.Small, 14, UIKit.TextDim, TextAnchor.UpperLeft);

            Color hc = p.health > 50 ? Color.white : p.health > 25 ? new Color(1f, 0.8f, 0.3f) : UIKit.Danger;
            GUI.Label(new Rect(r.x + 12 * S, r.y + 2 * S, 60 * S, 18 * S), "HEALTH", small);
            big.normal.textColor = hc;
            GUI.Label(new Rect(r.x + 12 * S, r.y + 14 * S, 90 * S, 44 * S), Mathf.CeilToInt(p.health).ToString(), big);
            GUI.Label(new Rect(r.x + 100 * S, r.y + 2 * S, 80 * S, 18 * S), p.helmet ? "ARMOUR+H" : "ARMOUR", small);
            big.normal.textColor = p.armor > 0 ? new Color(0.6f, 0.8f, 1f) : UIKit.TextDim;
            GUI.Label(new Rect(r.x + 100 * S, r.y + 14 * S, 90 * S, 44 * S), Mathf.CeilToInt(p.armor).ToString(), big);

            var wh = p.weapons;
            var def = wh.CurrentDef;
            string wname = def != null ? def.name : "";
            if (wh.slot == WeaponSlot.Grenade) wname += $" x{wh.GrenadeCount(wh.selectedGrenade)}";
            GUI.Label(new Rect(r.x + 200 * S, r.y + 2 * S, 200 * S, 18 * S), wh.reloading ? "RELOADING..." : "WEAPON", small);
            GUI.Label(new Rect(r.x + 200 * S, r.y + 18 * S, 190 * S, 36 * S), wname, UIKit.Styled(UIKit.Label, 20, Color.white, TextAnchor.MiddleLeft, true));
            var inst = wh.Current;
            if (inst != null && inst.def.IsGun)
            {
                big.normal.textColor = inst.mag <= inst.def.magazine * 0.25f ? new Color(1f, 0.6f, 0.3f) : Color.white;
                big.alignment = TextAnchor.MiddleRight;
                string reserve = wh.unlimitedAmmo ? "∞" : inst.reserve.ToString();
                GUI.Label(new Rect(r.xMax - 140 * S, r.y + 4 * S, 80 * S, 50 * S), inst.mag.ToString(), big);
                GUI.Label(new Rect(r.xMax - 58 * S, r.y + 14 * S, 50 * S, 40 * S), "/ " + reserve, UIKit.Styled(UIKit.Label, 18, UIKit.TextDim));
            }
            // Equipment icons
            string eq = "";
            if (wh.heCount > 0) eq += "HE ";
            if (wh.flashCount > 0) eq += "FL" + (wh.flashCount > 1 ? "x2 " : " ");
            if (wh.smokeCount > 0) eq += "SM ";
            if (p.defuseKit) eq += "KIT ";
            if (wh.hasCharge) eq += "CHARGE";
            if (eq.Length > 0) GUI.Label(new Rect(r.x, r.y - 26 * S, r.width, 24 * S), eq, UIKit.Styled(UIKit.Label, 17, wh.hasCharge ? new Color(1f, 0.8f, 0.3f) : UIKit.TextDim, TextAnchor.MiddleCenter, true));
        }

        // ---------------------------------------------------------------- Objective prompts & progress
        void DrawObjective(Rect sa, float S)
        {
            var p = Player;
            float cy = sa.y + sa.height * 0.62f;
            string prompt = null;
            if (MM.Config.mode == GameMode.Classic && p.alive)
            {
                if (MM.Phase == MatchPhase.Freeze) prompt = "Buy phase — open BUY to purchase equipment";
                if (p.team == Team.Raiders && p.weapons.hasCharge && !MM.ChargePlanted && MM.Map.SiteAt(p.transform.position) >= 0 && MM.Phase == MatchPhase.Live) prompt = "Hold USE to plant the charge";
                if (p.team == Team.Wardens && MM.ChargePlanted && Vector3.Distance(p.transform.position, MM.ChargePosition) < 1.8f) prompt = p.defuseKit ? "Hold USE to defuse (5 s)" : "Hold USE to defuse (10 s — no kit)";
            }
            var swap = p.alive ? Pickup.FindSwappable(p) : null;
            if (prompt == null && swap != null) prompt = "Tap USE to pick up " + swap.weapon.def.name;
            if (prompt != null) UIKit.Shadowed(new Rect(0, cy, Screen.width, 34 * S), prompt, UIKit.Styled(UIKit.Label, 22, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleCenter, true));

            float prog = MM.PlantingActor == p ? MM.PlantProgress : MM.DefusingActor == p ? MM.DefuseProgress : -1f;
            if (prog >= 0f)
            {
                var r = new Rect(sa.center.x - 160 * S, cy + 40 * S, 320 * S, 16 * S);
                UIKit.Bar(r, prog, MM.PlantingActor == p ? new Color(1f, 0.55f, 0.2f) : new Color(0.4f, 0.75f, 1f));
            }
            if (MM.ChargePlanted)
            {
                var r = new Rect(sa.center.x - 130 * S, TouchControls.ScoreRect.yMax + 6 * S, 260 * S, 30 * S);
                UIKit.Fill(r, new Color(0.45f, 0.05f, 0.03f, 0.7f));
                GUI.Label(r, $"CHARGE PLANTED • SITE {MM.Map.SiteName(MM.ChargeSite)}", UIKit.Styled(UIKit.Label, 17, Color.white, TextAnchor.MiddleCenter, true));
            }
            else if (MM.Config.mode == GameMode.Classic && MM.CanBuy(p) && MM.Phase == MatchPhase.Live)
            {
                GUI.Label(new Rect(sa.center.x - 130 * S, TouchControls.ScoreRect.yMax + 4 * S, 260 * S, 26 * S), "Buy zone active", UIKit.Styled(UIKit.Small, 16, new Color(0.55f, 0.95f, 0.5f), TextAnchor.MiddleCenter));
            }
        }

        void DrawSpectator(Rect sa, float S)
        {
            var t = PC.spectateTarget;
            string txt = t != null ? $"Spectating {t.displayName}" : "You are dead";
            if (MM.Config.mode != GameMode.Classic) txt = "Respawning...";
            var r = new Rect(sa.center.x - 220 * S, sa.yMax - 120 * S, 440 * S, 44 * S);
            UIKit.Fill(r, new Color(0, 0, 0, 0.45f));
            GUI.Label(r, txt, UIKit.Styled(UIKit.Label, 22, Color.white, TextAnchor.MiddleCenter, true));
            if (MM.Config.mode == GameMode.Classic && UIKit.Button(new Rect(r.center.x - 100 * S, r.yMax + 8 * S, 200 * S, 50 * S), "NEXT PLAYER")) PC.CycleSpectate();
        }

        void DrawPractice(Rect sa, float S)
        {
            int shots = Player.weapons.shotsFired;
            float acc = shots > 0 ? PlayerHitFeedback.shotsHit * 100f / shots : 0f;
            float hs = MM.practiceKills > 0 ? MM.practiceHeadshots * 100f / MM.practiceKills : 0f;
            var r = new Rect(sa.x + 10 * S, sa.y + (GameApp.Settings.showRadar ? 170 : 10) * S, 240 * S, 120 * S);
            UIKit.Fill(r, new Color(0, 0, 0, 0.45f));
            GUI.Label(new Rect(r.x + 10 * S, r.y + 6 * S, r.width, 110 * S), $"Kills  {MM.practiceKills}\nHeadshots  {hs:0}%\nAccuracy  {acc:0}%  ({PlayerHitFeedback.shotsHit}/{shots})", UIKit.Styled(UIKit.Label, 19, Color.white, TextAnchor.UpperLeft));
            if (UIKit.Button(new Rect(r.x, r.yMax + 6 * S, 115 * S, 44 * S), "RESET")) { MM.practiceKills = MM.practiceHeadshots = 0; Player.weapons.shotsFired = 0; PlayerHitFeedback.shotsHit = 0; }
        }

        // ---------------------------------------------------------------- Touch controls
        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "fire", "FIRE" }, { "fireL", "FIRE" }, { "aim", "AIM" }, { "jump", "JUMP" }, { "crouch", "DUCK" }, { "reload", "R" },
            { "switch", "SWAP" }, { "grenade", "NADE" }, { "use", "USE" }, { "buy", "BUY" }, { "radio", "RADIO" }, { "walk", "WALK" }
        };

        void DrawTouchControls(float S)
        {
            var s = GameApp.Settings;
            var tc = PC.controls;
            if (AnyMenuOpen || GameApp.I.Paused) return;
            foreach (var id in TouchControls.ButtonIds)
            {
                var b = s.Get(id);
                if (b == null || !b.enabled || !tc.isButtonVisible(id)) continue;
                if (id == "fireL" && !s.leftFireButton) continue;
                bool held = tc.IsHeld(id) || (id == "crouch" && tc.crouchToggled) || (id == "walk" && tc.walkToggled);
                string label = Labels[id];
                if (id == "aim" && Player.weapons.CurrentDef != null && Player.weapons.CurrentDef.category == WeaponCategory.Knife) label = "STAB";
                if (id == "grenade" && Player.weapons.slot == WeaponSlot.Grenade) label = Player.weapons.selectedGrenade.ToString().ToUpper();
                DrawRoundButton(TouchControls.ButtonRect(b, s), label, held, id == "fire" || id == "fireL", id == "use" || id == "buy");
            }
            // Joystick
            float jr = tc.JoystickRadius;
            if (tc.JoyActive)
            {
                var o = tc.joyOrigin;
                GUI.DrawTexture(new Rect(o.x - jr, o.y - jr, jr * 2, jr * 2), UIKit.Circle(true, 128, 0.05f), ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, 0.45f), 0, 0);
                Vector2 k = Vector2.ClampMagnitude(tc.joyCurrent - o, jr);
                float kr = jr * 0.45f;
                GUI.DrawTexture(new Rect(o.x + k.x - kr, o.y + k.y - kr, kr * 2, kr * 2), UIKit.Circle(false), ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, 0.45f), 0, 0);
            }
            else
            {
                var sa = TouchControls.SafeAreaTopLeft;
                float x = s.leftHanded ? sa.xMax - sa.width * 0.2f : sa.x + sa.width * 0.2f;
                var c = new Vector2(x, sa.y + sa.height * 0.72f);
                GUI.DrawTexture(new Rect(c.x - jr, c.y - jr, jr * 2, jr * 2), UIKit.Circle(true, 128, 0.04f), ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, 0.15f), 0, 0);
            }
        }

        static void DrawRoundButton(Rect r, string label, bool held, bool fire, bool highlight)
        {
            Color fill = held ? new Color(1f, 1f, 1f, 0.35f) : new Color(0f, 0f, 0f, 0.3f);
            Color ring = fire ? new Color(1f, 0.4f, 0.3f, 0.85f) : highlight ? new Color(1f, 0.8f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(r, UIKit.Circle(false), ScaleMode.StretchToFill, true, 0, fill, 0, 0);
            GUI.DrawTexture(r, UIKit.Circle(true, 128, 0.05f), ScaleMode.StretchToFill, true, 0, ring, 0, 0);
            int size = Mathf.RoundToInt(Mathf.Clamp(r.width * 0.22f, 10f, 40f));
            var st = new GUIStyle(UIKit.Label) { fontSize = size, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            st.normal.textColor = new Color(1, 1, 1, 0.9f);
            GUI.Label(r, label, st);
        }

        // ---------------------------------------------------------------- Buy menu
        static readonly WeaponCategory[] BuyTabs = { WeaponCategory.Pistol, WeaponCategory.SMG, WeaponCategory.Shotgun, WeaponCategory.Rifle, WeaponCategory.Sniper, WeaponCategory.Heavy, WeaponCategory.Grenade, WeaponCategory.Equipment };

        void DrawBuyMenu(Rect sa, float S)
        {
            UIKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.55f));
            var r = new Rect(sa.x + 30 * S, sa.y + 20 * S, sa.width - 60 * S, sa.height - 40 * S);
            UIKit.DrawPanel(r);
            bool unlimited = MM.Config.mode != GameMode.Classic;
            GUI.Label(new Rect(r.x + 24 * S, r.y + 14 * S, 400 * S, 44 * S), "BUY MENU", UIKit.Header);
            GUI.Label(new Rect(r.x + 260 * S, r.y + 14 * S, 300 * S, 44 * S), unlimited ? "Free loadout" : "$" + Player.money, UIKit.Styled(UIKit.Label, 30, new Color(0.55f, 0.95f, 0.5f), TextAnchor.MiddleLeft, true));
            if (MM.Config.mode == GameMode.Classic && MM.Phase == MatchPhase.Live)
                GUI.Label(new Rect(r.x + 480 * S, r.y + 14 * S, 300 * S, 44 * S), "Buy time ends soon", UIKit.Small);
            if (UIKit.Button(new Rect(r.xMax - 150 * S, r.y + 12 * S, 130 * S, 52 * S), "CLOSE")) buyOpen = false;

            float tabH = 54 * S, tabW = 170 * S;
            for (int i = 0; i < BuyTabs.Length; i++)
                if (UIKit.Button(new Rect(r.x + 20 * S, r.y + 76 * S + i * (tabH + 6 * S), tabW, tabH), BuyTabs[i].ToString().ToUpper(), buyTab == BuyTabs[i])) buyTab = BuyTabs[i];

            var items = new List<WeaponDef>(WeaponDatabase.Category(buyTab));
            items.RemoveAll(w => !WeaponDatabase.CanBuy(w, Player.team));
            var grid = new Rect(r.x + tabW + 40 * S, r.y + 76 * S, r.width - tabW - 60 * S, r.height - 96 * S);
            int cols = 3;
            float cw = (grid.width - (cols - 1) * 12 * S) / cols, ch = 118 * S;
            UIKit.BeginScroll(500, grid, Mathf.CeilToInt(items.Count / (float)cols) * (ch + 12 * S));
            for (int i = 0; i < items.Count; i++)
            {
                var w = items[i];
                var cr = new Rect((i % cols) * (cw + 12 * S), (i / cols) * (ch + 12 * S), cw, ch);
                bool afford = unlimited || Player.money >= w.price;
                bool owned = Owned(w);
                bool selected = owned;
                if (UIKit.Button(cr, "", selected, afford && !owned) && !UIKit.IsDragging) MM.PlayerBuy(w);
                GUI.Label(new Rect(cr.x + 14 * S, cr.y + 8 * S, cr.width - 28 * S, 34 * S), w.name, UIKit.Styled(UIKit.Label, 22, afford ? Color.white : UIKit.TextDim, TextAnchor.MiddleLeft, true));
                GUI.Label(new Rect(cr.x + 14 * S, cr.y + 40 * S, cr.width - 28 * S, 30 * S), w.archetype, UIKit.Styled(UIKit.Small, 16, UIKit.TextDim));
                GUI.Label(new Rect(cr.x + 14 * S, cr.yMax - 40 * S, cr.width - 28 * S, 34 * S), owned ? "OWNED" : unlimited ? "FREE" : "$" + w.price,
                    UIKit.Styled(UIKit.Label, 22, owned ? UIKit.Accent : afford ? new Color(0.55f, 0.95f, 0.5f) : UIKit.Danger, TextAnchor.MiddleLeft, true));
                if (w.IsGun) GUI.Label(new Rect(cr.x + cr.width * 0.45f, cr.yMax - 40 * S, cr.width * 0.5f, 34 * S), $"{w.damage}{(w.pellets > 1 ? "x" + w.pellets : "")} dmg • {w.magazine} rds", UIKit.Styled(UIKit.Small, 15, UIKit.TextDim, TextAnchor.MiddleRight));
            }
            UIKit.EndScroll();
        }

        bool Owned(WeaponDef w)
        {
            var wh = Player.weapons;
            switch (w.id)
            {
                case "kevlar": return Player.armor >= 100;
                case "kevlarhelm": return Player.armor >= 100 && Player.helmet;
                case "kit": return Player.defuseKit;
            }
            if (w.category == WeaponCategory.Grenade) return !wh.CanCarryGrenade(w);
            return (wh.primary != null && wh.primary.def == w) || (wh.secondary != null && wh.secondary.def == w);
        }

        // ---------------------------------------------------------------- Radio
        void DrawRadioMenu(Rect sa, float S)
        {
            var cmds = MatchManager.RadioCommands;
            float w = 360 * S, h = 52 * S;
            var r = new Rect(sa.x + 150 * S, sa.y + sa.height * 0.2f, w, (cmds.Length + 1) * (h + 6 * S) + 20 * S);
            UIKit.DrawPanel(r);
            for (int i = 0; i < cmds.Length; i++)
                if (UIKit.Button(new Rect(r.x + 10 * S, r.y + 10 * S + i * (h + 6 * S), w - 20 * S, h), $"{i + 1}. {cmds[i]}")) { MM.PlayerRadio(i); radioOpen = false; }
            if (UIKit.Button(new Rect(r.x + 10 * S, r.y + 10 * S + cmds.Length * (h + 6 * S), w - 20 * S, h), "CLOSE")) radioOpen = false;
        }

        // ---------------------------------------------------------------- Scoreboard & match end
        void DrawScoreboard(Rect sa, float S, bool final)
        {
            UIKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, final ? 0.7f : 0.45f));
            var r = new Rect(sa.x + 50 * S, sa.y + 40 * S, sa.width - 100 * S, sa.height - (final ? 130 : 80) * S);
            UIKit.DrawPanel(r);
            if (final)
            {
                string res = MM.ResultText();
                Color c = res == "VICTORY" ? UIKit.Good : res == "DEFEAT" ? UIKit.Danger : UIKit.Accent;
                GUI.Label(new Rect(r.x, r.y + 10 * S, r.width, 60 * S), res, UIKit.Styled(UIKit.Title, 52, c, TextAnchor.MiddleCenter, true));
            }
            string head = MM.Config.mode == GameMode.TeamDeathmatch ? "TEAM DEATHMATCH" : MM.Config.mode == GameMode.Practice ? "PRACTICE" : $"ROUND {MM.roundNumber} / {MM.Config.maxRounds}";
            GUI.Label(new Rect(r.x, r.y + (final ? 70 : 10) * S, r.width, 36 * S), head + "  •  " + MM.Map.def.name, UIKit.Styled(UIKit.Small, 18, UIKit.TextDim, TextAnchor.MiddleCenter));
            float top = r.y + (final ? 110 : 50) * S;
            float colW = (r.width - 60 * S) / 2f;
            DrawTeamTable(new Rect(r.x + 20 * S, top, colW, r.yMax - top - 10 * S), Team.Raiders, MM.raiderScore, S);
            DrawTeamTable(new Rect(r.x + 40 * S + colW, top, colW, r.yMax - top - 10 * S), Team.Wardens, MM.wardenScore, S);
            if (final)
            {
                var p = Player;
                float hs = p.kills > 0 ? p.headshots * 100f / p.kills : 0f;
                GUI.Label(new Rect(sa.x + 60 * S, sa.yMax - 80 * S, sa.width * 0.6f, 60 * S), $"Your match: {p.kills} kills • {p.deaths} deaths • {hs:0}% headshots • best streak {p.bestStreak}", UIKit.Label);
                if (UIKit.Button(new Rect(sa.xMax - 300 * S, sa.yMax - 82 * S, 250 * S, 66 * S), "CONTINUE", true)) GameApp.I.QuitMatch();
            }
        }

        void DrawTeamTable(Rect r, Team t, int score, float S)
        {
            UIKit.Fill(new Rect(r.x, r.y, r.width, 44 * S), new Color(t.Tint().r * 0.35f, t.Tint().g * 0.35f, t.Tint().b * 0.35f, 0.8f));
            GUI.Label(new Rect(r.x + 12 * S, r.y, r.width * 0.6f, 44 * S), t.Label().ToUpper() + (MM.Player.team == t ? "  (you)" : ""), UIKit.Styled(UIKit.Label, 22, t.Tint(), TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(r.xMax - 80 * S, r.y, 70 * S, 44 * S), score.ToString(), UIKit.Styled(UIKit.Label, 30, Color.white, TextAnchor.MiddleRight, true));
            var cols = new[] { 0.0f, 0.5f, 0.62f, 0.74f, 0.86f };
            var hdr = UIKit.Styled(UIKit.Small, 15, UIKit.TextDim);
            float y = r.y + 50 * S;
            bool showMoney = MM.Config.mode == GameMode.Classic && t == MM.Player.team;
            string[] titles = { "Name", "K", "D", "Score", showMoney ? "$" : "" };
            for (int i = 0; i < titles.Length; i++) GUI.Label(new Rect(r.x + 12 * S + r.width * cols[i], y, r.width * 0.2f, 24 * S), titles[i], hdr);
            y += 26 * S;
            var list = Actor.All.FindAll(a => a.team == t);
            list.Sort((a, b) => b.score.CompareTo(a.score));
            foreach (var a in list)
            {
                var row = new Rect(r.x, y, r.width, 32 * S);
                if (a.isPlayer) UIKit.Fill(row, new Color(1f, 1f, 1f, 0.08f));
                var st = UIKit.Styled(UIKit.Label, 19, a.alive ? Color.white : UIKit.TextDim);
                string name = a.displayName + (!a.alive ? "  ✝" : "") + (t == MM.Player.team && a.weapons.hasCharge ? "  [C]" : "") + (a.bot != null ? $"  <size={UIKit.Px(13)}><color=#888>BOT</color></size>" : "");
                st.richText = true;
                GUI.Label(new Rect(r.x + 12 * S, y, r.width * 0.5f, 32 * S), name, st);
                GUI.Label(new Rect(r.x + 12 * S + r.width * cols[1], y, r.width * 0.12f, 32 * S), a.kills.ToString(), st);
                GUI.Label(new Rect(r.x + 12 * S + r.width * cols[2], y, r.width * 0.12f, 32 * S), a.deaths.ToString(), st);
                GUI.Label(new Rect(r.x + 12 * S + r.width * cols[3], y, r.width * 0.12f, 32 * S), a.score.ToString(), st);
                if (showMoney) GUI.Label(new Rect(r.x + 12 * S + r.width * cols[4], y, r.width * 0.14f, 32 * S), "$" + a.money, st);
                y += 34 * S;
            }
        }

        // ---------------------------------------------------------------- Pause
        void DrawPause(Rect sa, float S)
        {
            UIKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.75f));
            if (pauseSettings)
            {
                GameApp.I.Menu.DrawSettings(sa, S, true);
                if (UIKit.Button(new Rect(sa.x + 16 * S, sa.y + 14 * S, 120 * S, 54 * S), "< Back")) { pauseSettings = false; GameApp.I.ApplySettings(); }
                return;
            }
            var r = new Rect(sa.center.x - 220 * S, sa.center.y - 200 * S, 440 * S, 400 * S);
            UIKit.DrawPanel(r);
            GUI.Label(new Rect(r.x, r.y + 16 * S, r.width, 50 * S), "PAUSED", UIKit.Styled(UIKit.Header, 34, UIKit.Accent, TextAnchor.MiddleCenter, true));
            float y = r.y + 90 * S, h = 64 * S;
            if (UIKit.Button(new Rect(r.x + 30 * S, y, r.width - 60 * S, h), "RESUME", true)) { GameApp.I.SetPaused(false); confirmQuit = false; }
            y += h + 14 * S;
            if (UIKit.Button(new Rect(r.x + 30 * S, y, r.width - 60 * S, h), "SETTINGS")) pauseSettings = true;
            y += h + 14 * S;
            if (!confirmQuit) { if (UIKit.Button(new Rect(r.x + 30 * S, y, r.width - 60 * S, h), "QUIT TO MENU")) confirmQuit = true; }
            else
            {
                GUI.Label(new Rect(r.x, y - 4 * S, r.width, 30 * S), "Leave the match? It counts as abandoned.", UIKit.Styled(UIKit.Small, 16, UIKit.Danger, TextAnchor.MiddleCenter));
                if (UIKit.Button(new Rect(r.x + 30 * S, y + 26 * S, (r.width - 70 * S) / 2, h), "LEAVE")) { confirmQuit = false; GameApp.I.QuitMatch(); }
                if (UIKit.Button(new Rect(r.center.x + 5 * S, y + 26 * S, (r.width - 70 * S) / 2, h), "STAY")) confirmQuit = false;
            }
        }
    }
}
