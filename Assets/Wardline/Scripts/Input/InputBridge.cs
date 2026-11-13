using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

namespace Wardline
{
    public enum TPhase { Began, Moved, Stationary, Ended }

    public struct TouchPoint
    {
        public int id;
        public Vector2 pos;      // pixels, origin top-left (GUI space)
        public Vector2 delta;    // pixels since last frame, y down
        public TPhase phase;
    }

    public enum K { W, A, S, D, Space, Ctrl, Shift, R, E, B, G, Q, Tab, Esc, Z, X, F, One, Two, Three, Four, Five, Enter, C }

    /// <summary>
    /// Thin abstraction over the Unity Input System (preferred) with a legacy Input Manager fallback,
    /// so the project compiles and runs whichever input backend is active.
    /// </summary>
    public static class InputBridge
    {
        public static readonly List<TouchPoint> Touches = new List<TouchPoint>(10);
        static int lastFrame = -1;
        static bool gyroOn;

        public static void Init()
        {
#if ENABLE_INPUT_SYSTEM
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
#endif
        }

        /// <summary>Refresh the touch list once per frame.</summary>
        public static void Update()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            Touches.Clear();
            float h = Screen.height;
#if ENABLE_INPUT_SYSTEM
            foreach (var t in ETouch.activeTouches)
            {
                TPhase ph;
                switch (t.phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began: ph = TPhase.Began; break;
                    case UnityEngine.InputSystem.TouchPhase.Ended:
                    case UnityEngine.InputSystem.TouchPhase.Canceled: ph = TPhase.Ended; break;
                    case UnityEngine.InputSystem.TouchPhase.Moved: ph = TPhase.Moved; break;
                    default: ph = TPhase.Stationary; break;
                }
                var p = t.screenPosition; var d = t.delta;
                Touches.Add(new TouchPoint { id = t.touchId, pos = new Vector2(p.x, h - p.y), delta = new Vector2(d.x, -d.y), phase = ph });
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                TPhase ph = t.phase == UnityEngine.TouchPhase.Began ? TPhase.Began : (t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled) ? TPhase.Ended : t.phase == UnityEngine.TouchPhase.Moved ? TPhase.Moved : TPhase.Stationary;
                Touches.Add(new TouchPoint { id = t.fingerId, pos = new Vector2(t.position.x, h - t.position.y), delta = new Vector2(t.deltaPosition.x, -t.deltaPosition.y), phase = ph });
            }
#endif
        }

        public static bool HasTouchscreen
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Touchscreen.current != null;
#else
                return Input.touchSupported;
#endif
            }
        }

        public static bool HasKeyboard
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && !Application.isMobilePlatform;
#else
                return !Application.isMobilePlatform;
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM
        static Key Map(K k)
        {
            switch (k)
            {
                case K.W: return Key.W; case K.A: return Key.A; case K.S: return Key.S; case K.D: return Key.D;
                case K.Space: return Key.Space; case K.Ctrl: return Key.LeftCtrl; case K.Shift: return Key.LeftShift;
                case K.R: return Key.R; case K.E: return Key.E; case K.B: return Key.B; case K.G: return Key.G; case K.Q: return Key.Q;
                case K.Tab: return Key.Tab; case K.Esc: return Key.Escape; case K.Z: return Key.Z; case K.X: return Key.X; case K.F: return Key.F; case K.C: return Key.C;
                case K.One: return Key.Digit1; case K.Two: return Key.Digit2; case K.Three: return Key.Digit3; case K.Four: return Key.Digit4; case K.Five: return Key.Digit5;
                case K.Enter: return Key.Enter;
            }
            return Key.None;
        }
#else
        static KeyCode Map(K k)
        {
            switch (k)
            {
                case K.W: return KeyCode.W; case K.A: return KeyCode.A; case K.S: return KeyCode.S; case K.D: return KeyCode.D;
                case K.Space: return KeyCode.Space; case K.Ctrl: return KeyCode.LeftControl; case K.Shift: return KeyCode.LeftShift;
                case K.R: return KeyCode.R; case K.E: return KeyCode.E; case K.B: return KeyCode.B; case K.G: return KeyCode.G; case K.Q: return KeyCode.Q;
                case K.Tab: return KeyCode.Tab; case K.Esc: return KeyCode.Escape; case K.Z: return KeyCode.Z; case K.X: return KeyCode.X; case K.F: return KeyCode.F; case K.C: return KeyCode.C;
                case K.One: return KeyCode.Alpha1; case K.Two: return KeyCode.Alpha2; case K.Three: return KeyCode.Alpha3; case K.Four: return KeyCode.Alpha4; case K.Five: return KeyCode.Alpha5;
                case K.Enter: return KeyCode.Return;
            }
            return KeyCode.None;
        }
#endif

        public static bool KeyHeld(K k)
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[Map(k)].isPressed;
#else
            return Input.GetKey(Map(k));
#endif
        }

        public static bool KeyDown(K k)
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[Map(k)].wasPressedThisFrame;
#else
            return Input.GetKeyDown(Map(k));
#endif
        }

        /// <summary>Mouse delta in pixels this frame (y up).</summary>
        public static Vector2 MouseDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 20f;
#endif
            }
        }

        public static bool MouseButton(int b)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            return b == 0 ? Mouse.current.leftButton.isPressed : b == 1 ? Mouse.current.rightButton.isPressed : Mouse.current.middleButton.isPressed;
#else
            return Input.GetMouseButton(b);
#endif
        }

        public static bool MouseButtonDown(int b)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            return b == 0 ? Mouse.current.leftButton.wasPressedThisFrame : b == 1 ? Mouse.current.rightButton.wasPressedThisFrame : Mouse.current.middleButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(b);
#endif
        }

        public static float Scroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        /// <summary>Device angular velocity (rad/s) or zero when unavailable.</summary>
        public static Vector3 GyroRate(bool enable)
        {
#if ENABLE_INPUT_SYSTEM
            var g = UnityEngine.InputSystem.Gyroscope.current;
            if (g == null) return Vector3.zero;
            if (enable && !g.enabled) InputSystem.EnableDevice(g);
            if (!enable) { if (g.enabled) InputSystem.DisableDevice(g); return Vector3.zero; }
            return g.angularVelocity.ReadValue();
#else
            if (!SystemInfo.supportsGyroscope) return Vector3.zero;
            if (enable != gyroOn) { Input.gyro.enabled = enable; gyroOn = enable; }
            return enable ? Input.gyro.rotationRateUnbiased : Vector3.zero;
#endif
        }

        public static void SetCursorLocked(bool locked)
        {
            if (Application.isMobilePlatform) return;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
