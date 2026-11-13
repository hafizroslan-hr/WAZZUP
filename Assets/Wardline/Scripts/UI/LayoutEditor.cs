using UnityEngine;

namespace Wardline
{
    /// <summary>Drag-and-drop editor for touch button positions and sizes (saved per device).</summary>
    public class LayoutEditor
    {
        string selected;
        string dragging;
        Vector2 dragOffset;

        /// <summary>Draws the editor. Returns true when the user taps DONE.</summary>
        public bool Draw(Rect sa)
        {
            float S = UIKit.S;
            var s = GameApp.Settings;
            UIKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.09f, 0.09f, 0.92f));
            // Movement zone hint
            float zoneX = s.leftHanded ? sa.x + sa.width * 0.58f : sa.x;
            UIKit.Fill(new Rect(zoneX, sa.y + sa.height * 0.22f, sa.width * 0.42f, sa.height * 0.78f), new Color(1f, 1f, 1f, 0.04f));
            GUI.Label(new Rect(zoneX + 20 * S, sa.yMax - 50 * S, 400 * S, 40 * S), "Movement joystick zone", UIKit.Small);
            UIKit.Fill(TouchControls.ScoreRect, new Color(1, 1, 1, 0.06f));
            UIKit.Fill(TouchControls.PauseRect, new Color(1, 1, 1, 0.06f));

            GUI.Label(new Rect(sa.x + 20 * S, sa.y + 10 * S, sa.width * 0.5f, 40 * S), "Drag buttons to move them. Tap one to resize or hide it.", UIKit.Styled(UIKit.Label, 20, UIKit.Text));

            var e = Event.current;
            foreach (var id in TouchControls.ButtonIds)
            {
                var b = s.Get(id);
                var r = TouchControls.ButtonRect(b, s);
                var old = GUI.color;
                GUI.DrawTexture(r, UIKit.Circle(false), ScaleMode.StretchToFill, true, 0, b.enabled ? new Color(0, 0, 0, 0.45f) : new Color(0.3f, 0, 0, 0.3f), 0, 0);
                GUI.DrawTexture(r, UIKit.Circle(true, 128, 0.05f), ScaleMode.StretchToFill, true, 0, id == selected ? UIKit.Accent : new Color(1, 1, 1, b.enabled ? 0.7f : 0.25f), 0, 0);
                GUI.color = old;
                GUI.Label(r, id.ToUpper(), UIKit.Styled(UIKit.Label, 16, Color.white, TextAnchor.MiddleCenter, true));

                if (e.type == EventType.MouseDown && r.Contains(e.mousePosition) && dragging == null)
                {
                    dragging = id; selected = id;
                    dragOffset = TouchControls.ButtonCenter(b, s.leftHanded) - e.mousePosition;
                    e.Use();
                }
            }
            if (dragging != null)
            {
                if (e.type == EventType.MouseDrag)
                {
                    var b = s.Get(dragging);
                    Vector2 c = e.mousePosition + dragOffset;
                    float nx = Mathf.Clamp01((c.x - sa.x) / sa.width), ny = Mathf.Clamp01((c.y - sa.y) / sa.height);
                    b.x = s.leftHanded ? 1f - nx : nx;
                    b.y = ny;
                    e.Use();
                }
                if (e.type == EventType.MouseUp) { dragging = null; e.Use(); }
            }

            // Inspector for the selected button
            var panel = new Rect(sa.center.x - 260 * S, sa.y + 60 * S, 520 * S, selected != null ? 190 * S : 80 * S);
            UIKit.DrawPanel(panel);
            if (selected != null)
            {
                var b = s.Get(selected);
                GUI.Label(new Rect(panel.x + 20 * S, panel.y + 10 * S, 300 * S, 36 * S), "Button: " + selected.ToUpper(), UIKit.Styled(UIKit.Label, 22, UIKit.Accent, TextAnchor.MiddleLeft, true));
                b.size = UIKit.Slider(new Rect(panel.x + 20 * S, panel.y + 50 * S, panel.width - 40 * S, 50 * S), "Size", b.size, 0.5f, 1.8f);
                bool optional = selected == "fireL" || selected == "walk" || selected == "radio" || selected == "grenade";
                if (optional) b.enabled = UIKit.Toggle(new Rect(panel.x + 20 * S, panel.y + 110 * S, panel.width - 40 * S, 50 * S), "Show this button", b.enabled);
                else GUI.Label(new Rect(panel.x + 20 * S, panel.y + 110 * S, panel.width - 40 * S, 50 * S), "Core button — always shown", UIKit.Small);
            }
            float by = panel.yMax + 10 * S;
            if (UIKit.Button(new Rect(sa.center.x - 260 * S, by, 250 * S, 56 * S), "RESET DEFAULTS")) { s.layout = GameSettings.DefaultLayout(); selected = null; }
            if (UIKit.Button(new Rect(sa.center.x + 10 * S, by, 250 * S, 56 * S), "DONE", true)) { Persistence.SaveSettings(s); return true; }
            return false;
        }
    }

    /// <summary>First-launch walkthrough of the touch controls, shown over the practice range.</summary>
    public class Tutorial
    {
        int step;
        public bool Active => step < Steps.Length;

        static readonly (string title, string body, string highlight)[] Steps =
        {
            ("Move", "Put your left thumb anywhere in the lower-left area. A joystick appears under it. A light push walks silently and keeps your aim steady; a full push runs, but footsteps are loud.", "joy"),
            ("Look", "Swipe anywhere on the right side to aim. Adjust sensitivity, aim acceleration and the optional gyroscope in Settings › Controls.", "look"),
            ("Shoot", "Hold FIRE to shoot. You can drag your thumb on FIRE to steer while spraying. Recoil climbs, so pull down slightly, or fire in short bursts. Standing still, walking or crouching is most accurate.", "fire"),
            ("Aim, jump, crouch", "AIM toggles scopes on scoped weapons and stabs with the knife. JUMP and DUCK are on the right edge. DUCK is a toggle.", "aim"),
            ("Reload, switch, grenades", "R reloads. SWAP cycles weapons. NADE selects grenades; hold FIRE and release it to throw.", "switch"),
            ("Buy equipment", "In Classic you earn money from kills and rounds. Open BUY at the start of a round while you're in your spawn. Armour matters!", "buy"),
            ("Objective", "Raiders carry the charge to site A or B and hold USE to plant it. Wardens hold USE on a planted charge to defuse it: 10 s, or 5 s with a kit. USE also picks up weapons.", "use"),
            ("Team play", "RADIO sends commands your bot teammates obey: follow me, hold, regroup, need backup, enemy spotted. Tap the score at the top for the scoreboard. You're ready. Good luck!", "radio"),
        };

        public bool Draw(Rect sa)
        {
            if (!Active) return true;
            float S = UIKit.S;
            var s = GameApp.Settings;
            var (title, body, hl) = Steps[step];
            UIKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.55f));

            // Highlight the relevant control
            Rect h;
            if (hl == "joy") h = new Rect(s.leftHanded ? sa.x + sa.width * 0.58f : sa.x, sa.y + sa.height * 0.22f, sa.width * 0.42f, sa.height * 0.78f);
            else if (hl == "look") h = new Rect(s.leftHanded ? sa.x : sa.x + sa.width * 0.42f, sa.y + sa.height * 0.15f, sa.width * 0.58f, sa.height * 0.5f);
            else h = TouchControls.ButtonRect(s.Get(hl), s);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            var ring = new Rect(h.x - 8 * S, h.y - 8 * S, h.width + 16 * S, h.height + 16 * S);
            if (hl == "joy" || hl == "look") UIKit.Fill(h, new Color(1f, 0.8f, 0.3f, 0.12f + 0.08f * pulse));
            else GUI.DrawTexture(ring, UIKit.Circle(true, 128, 0.06f), ScaleMode.StretchToFill, true, 0, new Color(1f, 0.8f, 0.3f, 0.6f + 0.4f * pulse), 0, 0);

            var r = new Rect(sa.center.x - 300 * S, sa.y + 70 * S, 600 * S, 260 * S);
            UIKit.DrawPanel(r);
            GUI.Label(new Rect(r.x + 24 * S, r.y + 14 * S, r.width - 48 * S, 40 * S), $"{step + 1}/{Steps.Length}  {title.ToUpper()}", UIKit.Header);
            GUI.Label(new Rect(r.x + 24 * S, r.y + 62 * S, r.width - 48 * S, 130 * S), body, UIKit.Label);
            if (UIKit.Button(new Rect(r.x + 24 * S, r.yMax - 70 * S, 170 * S, 54 * S), "SKIP")) step = Steps.Length;
            if (UIKit.Button(new Rect(r.xMax - 194 * S, r.yMax - 70 * S, 170 * S, 54 * S), step == Steps.Length - 1 ? "FINISH" : "NEXT", true)) step++;
            return !Active;
        }
    }
}
