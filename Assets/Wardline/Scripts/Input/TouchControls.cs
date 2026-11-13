using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    /// <summary>One frame of player intent, produced by touch and/or keyboard+mouse.</summary>
    public class InputFrame
    {
        public Vector2 move;
        public bool walk;
        public Vector2 look;            // degrees (x = yaw, y = pitch down)
        public bool fireHeld, firePressed, fireReleased;
        public bool aimPressed, jumpPressed, crouchHeld, reloadPressed, switchPressed, grenadePressed;
        public bool usePressed, useHeld, buyPressed, scoreHeld, pausePressed, radioPressed, dropPressed, lastWeaponPressed;
        public int slotPressed = -1;

        public void Clear()
        {
            move = Vector2.zero; look = Vector2.zero; walk = false;
            fireHeld = firePressed = fireReleased = aimPressed = jumpPressed = reloadPressed = switchPressed = grenadePressed = false;
            usePressed = useHeld = buyPressed = scoreHeld = pausePressed = radioPressed = dropPressed = lastWeaponPressed = false;
            slotPressed = -1;
        }
    }

    /// <summary>
    /// Two-thumb FPS touch layout: floating joystick on the movement side, swipe-to-look everywhere else,
    /// customizable buttons (position/size, left-handed mirroring) placed inside the device safe area,
    /// optional gyroscope aiming and aim acceleration. Also merges keyboard/mouse for editor testing.
    /// </summary>
    public class TouchControls
    {
        public readonly InputFrame frame = new InputFrame();
        public Func<string, bool> isButtonVisible = id => true;
        public Func<float> zoomSensitivityScale = () => 1f;
        public bool crouchToggled, walkToggled;
        public bool enabled = true;

        // Joystick
        int joyTouch = -1;
        public Vector2 joyOrigin, joyCurrent;
        public bool JoyActive => joyTouch >= 0;

        // Look and buttons
        readonly Dictionary<int, string> touchOwner = new Dictionary<int, string>();
        readonly HashSet<string> heldButtons = new HashSet<string>();
        bool fireWasHeld;
        float useHeldTime;

        public static readonly string[] ButtonIds = { "fire", "fireL", "aim", "jump", "crouch", "reload", "switch", "grenade", "use", "buy", "radio", "walk" };

        public static float UIScale => Mathf.Max(0.6f, Mathf.Min(Screen.height / 720f, Screen.width / 1280f * 1.25f));

        public static Rect SafeAreaTopLeft
        {
            get
            {
                var sa = Screen.safeArea;
                return new Rect(sa.x, Screen.height - sa.yMax, sa.width, sa.height);
            }
        }

        public static Vector2 ButtonCenter(ButtonLayout b, bool leftHanded)
        {
            var sa = SafeAreaTopLeft;
            float x = leftHanded ? 1f - b.x : b.x;
            return new Vector2(sa.x + x * sa.width, sa.y + b.y * sa.height);
        }

        public static float ButtonRadius(ButtonLayout b, GameSettings s) => 46f * UIScale * b.size * s.buttonScale;

        public static Rect ButtonRect(ButtonLayout b, GameSettings s)
        {
            var c = ButtonCenter(b, s.leftHanded); float r = ButtonRadius(b, s);
            return new Rect(c.x - r, c.y - r, r * 2, r * 2);
        }

        /// <summary>Fixed system buttons (pause, scoreboard) in the top corners of the safe area.</summary>
        public static Rect PauseRect
        {
            get { var sa = SafeAreaTopLeft; float s = 54f * UIScale; return new Rect(sa.xMax - s - 8f * UIScale, sa.y + 6f * UIScale, s, s); }
        }

        public static Rect ScoreRect
        {
            get { var sa = SafeAreaTopLeft; float w = 300f * UIScale, h = 64f * UIScale; return new Rect(sa.center.x - w / 2, sa.y, w, h); }
        }

        public bool IsHeld(string id) => heldButtons.Contains(id);

        public float JoystickRadius => 70f * UIScale * GameApp.Settings.joystickSize;

        bool InMoveZone(Vector2 p, GameSettings s)
        {
            var sa = SafeAreaTopLeft;
            float nx = (p.x - sa.x) / sa.width;
            if (s.leftHanded) nx = 1f - nx;
            float ny = (p.y - sa.y) / sa.height;
            return nx < 0.42f && ny > 0.22f;
        }

        string HitButton(Vector2 p, GameSettings s)
        {
            string best = null; float bestD = float.MaxValue;
            foreach (var id in ButtonIds)
            {
                var b = s.Get(id);
                if (b == null || !b.enabled || !isButtonVisible(id)) continue;
                if (id == "fireL" && !s.leftFireButton) continue;
                var c = ButtonCenter(b, s.leftHanded);
                float r = ButtonRadius(b, s) * 1.15f;     // slightly generous touch target
                float d = Vector2.Distance(p, c);
                if (d <= r && d < bestD) { bestD = d; best = id; }
            }
            if (PauseRect.Contains(p)) return "pause";
            if (ScoreRect.Contains(p)) return "score";
            return best;
        }

        public void Poll(bool allowMouseLook)
        {
            var s = GameApp.Settings;
            InputBridge.Update();
            bool prevFire = fireWasHeld;
            frame.Clear();
            if (!enabled) { touchOwner.Clear(); heldButtons.Clear(); joyTouch = -1; fireWasHeld = false; return; }

            var pressedNow = new HashSet<string>();
            float degPerPx = 180f / Mathf.Max(1, Screen.height);

            foreach (var t in InputBridge.Touches)
            {
                if (t.phase == TPhase.Began)
                {
                    string btn = HitButton(t.pos, s);
                    if (btn != null)
                    {
                        touchOwner[t.id] = btn; heldButtons.Add(btn); pressedNow.Add(btn);
                        if (s.haptics) Haptics.Pulse(0, 0.03f);
                    }
                    else if (joyTouch < 0 && InMoveZone(t.pos, s))
                    {
                        joyTouch = t.id; joyOrigin = t.pos; joyCurrent = t.pos; touchOwner[t.id] = "joy";
                    }
                    else touchOwner[t.id] = "look";
                    continue;
                }

                if (!touchOwner.TryGetValue(t.id, out var owner)) continue;

                if (t.phase == TPhase.Ended)
                {
                    if (owner == "joy") joyTouch = -1;
                    else if (owner != "look") heldButtons.Remove(owner);
                    touchOwner.Remove(t.id);
                    continue;
                }

                if (owner == "joy")
                {
                    joyCurrent = t.pos;
                    // Floating stick follows the thumb if it drifts far past the edge.
                    Vector2 off = joyCurrent - joyOrigin;
                    float max = JoystickRadius * 1.6f;
                    if (off.magnitude > max) joyOrigin = joyCurrent - off.normalized * max;
                }
                else if (owner == "look" || owner == "fire" || owner == "fireL")
                {
                    // Swipe look (firing buttons can also be dragged to steer while shooting).
                    Vector2 d = t.delta;
                    float speedPx = d.magnitude / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                    float accel = 1f + s.aimAcceleration * Mathf.Clamp01(speedPx / 2500f) * 2f;
                    float k = degPerPx * accel * zoomSensitivityScale();
                    frame.look.x += d.x * k * s.sensX;
                    frame.look.y += d.y * k * s.sensY;
                }
            }

            // Joystick output
            if (joyTouch >= 0)
            {
                Vector2 off = joyCurrent - joyOrigin;
                Vector2 v = off / JoystickRadius;
                if (v.magnitude > 1f) v.Normalize();
                if (v.magnitude < 0.12f) v = Vector2.zero;
                frame.move = new Vector2(v.x, -v.y);
                frame.walk = v.magnitude < 0.55f;   // light push = quiet walk, full push = run
            }

            // Gyroscope aiming (landscape): yaw from rotation around the device's long axis, pitch around the short axis.
            if (s.gyroEnabled)
            {
                Vector3 rate = InputBridge.GyroRate(true) * Mathf.Rad2Deg * Time.deltaTime * s.gyroSens;
                float orient = Screen.orientation == ScreenOrientation.LandscapeRight ? -1f : 1f;
                frame.look.x += -rate.x * orient * zoomSensitivityScale();
                frame.look.y += rate.y * orient * (s.invertGyroY ? -1f : 1f) * zoomSensitivityScale();
            }
            else InputBridge.GyroRate(false);

            bool fireHeld = heldButtons.Contains("fire") || heldButtons.Contains("fireL");
            frame.fireHeld = fireHeld;
            frame.firePressed = pressedNow.Contains("fire") || pressedNow.Contains("fireL");
            frame.aimPressed = pressedNow.Contains("aim");
            frame.jumpPressed = pressedNow.Contains("jump");
            if (pressedNow.Contains("crouch")) crouchToggled = !crouchToggled;
            if (pressedNow.Contains("walk")) walkToggled = !walkToggled;
            frame.reloadPressed = pressedNow.Contains("reload");
            frame.switchPressed = pressedNow.Contains("switch");
            frame.grenadePressed = pressedNow.Contains("grenade");
            frame.usePressed = pressedNow.Contains("use");
            frame.useHeld = heldButtons.Contains("use");
            frame.buyPressed = pressedNow.Contains("buy");
            frame.radioPressed = pressedNow.Contains("radio");
            frame.pausePressed = pressedNow.Contains("pause");
            frame.scoreHeld = heldButtons.Contains("score");

            // ---- Keyboard + mouse (editor / desktop testing) ----
            if (InputBridge.HasKeyboard)
            {
                Vector2 kb = Vector2.zero;
                if (InputBridge.KeyHeld(K.W)) kb.y += 1; if (InputBridge.KeyHeld(K.S)) kb.y -= 1;
                if (InputBridge.KeyHeld(K.D)) kb.x += 1; if (InputBridge.KeyHeld(K.A)) kb.x -= 1;
                if (kb != Vector2.zero) { frame.move = kb.normalized; frame.walk = InputBridge.KeyHeld(K.Shift); }
                if (allowMouseLook)
                {
                    Vector2 md = InputBridge.MouseDelta * 0.08f * s.mouseSens * zoomSensitivityScale();
                    frame.look += new Vector2(md.x, -md.y);
                    if (InputBridge.MouseButton(0)) frame.fireHeld = true;
                    if (InputBridge.MouseButtonDown(0)) frame.firePressed = true;
                    if (InputBridge.MouseButtonDown(1)) frame.aimPressed = true;
                    float sc = InputBridge.Scroll;
                    if (sc < -0.1f) frame.switchPressed = true;
                }
                if (InputBridge.KeyDown(K.Space)) frame.jumpPressed = true;
                if (InputBridge.KeyHeld(K.Ctrl) || InputBridge.KeyHeld(K.C)) frame.crouchHeld = true;
                if (InputBridge.KeyDown(K.R)) frame.reloadPressed = true;
                if (InputBridge.KeyDown(K.E)) frame.usePressed = true;
                if (InputBridge.KeyHeld(K.E)) frame.useHeld = true;
                if (InputBridge.KeyDown(K.B)) frame.buyPressed = true;
                if (InputBridge.KeyDown(K.G)) frame.dropPressed = true;
                if (InputBridge.KeyDown(K.F)) frame.grenadePressed = true;
                if (InputBridge.KeyDown(K.Q)) frame.lastWeaponPressed = true;
                if (InputBridge.KeyHeld(K.Tab)) frame.scoreHeld = true;
                if (InputBridge.KeyDown(K.Esc)) frame.pausePressed = true;
                if (InputBridge.KeyDown(K.Z)) frame.radioPressed = true;
                if (InputBridge.KeyDown(K.One)) frame.slotPressed = 0;
                if (InputBridge.KeyDown(K.Two)) frame.slotPressed = 1;
                if (InputBridge.KeyDown(K.Three)) frame.slotPressed = 2;
                if (InputBridge.KeyDown(K.Four)) frame.slotPressed = 3;
                if (InputBridge.KeyDown(K.Five)) frame.slotPressed = 4;
            }

            if (crouchToggled) frame.crouchHeld = true;
            if (walkToggled) frame.walk = true;
            frame.fireReleased = prevFire && !frame.fireHeld;
            fireWasHeld = frame.fireHeld;
        }

        public void ResetState()
        {
            touchOwner.Clear(); heldButtons.Clear(); joyTouch = -1; crouchToggled = false; walkToggled = false; fireWasHeld = false;
        }
    }
}
