using System.Collections.Generic;
using UnityEngine;

namespace Wardline
{
    /// <summary>
    /// Immediate-mode UI toolkit (Unity IMGUI) with a dark tactical theme, resolution-independent sizing,
    /// procedurally generated rounded panels, and touch-friendly widgets including drag-to-scroll lists.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Bg = new Color(0.05f, 0.06f, 0.06f, 0.92f);
        public static readonly Color Panel = new Color(0.1f, 0.115f, 0.11f, 0.9f);
        public static readonly Color PanelLight = new Color(0.16f, 0.18f, 0.17f, 0.95f);
        public static readonly Color Accent = new Color(0.86f, 0.68f, 0.28f);
        public static readonly Color AccentDim = new Color(0.55f, 0.44f, 0.2f);
        public static readonly Color Text = new Color(0.92f, 0.92f, 0.88f);
        public static readonly Color TextDim = new Color(0.62f, 0.64f, 0.6f);
        public static readonly Color Danger = new Color(0.9f, 0.32f, 0.25f);
        public static readonly Color Good = new Color(0.45f, 0.85f, 0.45f);

        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        static GUIStyle label, button, title, header, small, center, panel, mono;
        static float builtFor = -1f;

        public static float S => TouchControls.UIScale;

        public static Texture2D Solid(Color c)
        {
            string k = "s" + c;
            if (texCache.TryGetValue(k, out var t) && t) return t;
            t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c); t.Apply();
            texCache[k] = t;
            return t;
        }

        /// <summary>Rounded rectangle texture for 9-slicing.</summary>
        public static Texture2D Rounded(Color fill, Color border, int radius = 10, int borderPx = 2)
        {
            string k = $"r{fill}{border}{radius}{borderPx}";
            if (texCache.TryGetValue(k, out var t) && t) return t;
            int n = radius * 2 + 4;
            t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (n - 1 - radius)));
                float dy = Mathf.Max(0, Mathf.Max(radius - y, y - (n - 1 - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius + 0.5f - d);
                Color c = d > radius - borderPx ? border : fill;
                c.a *= a;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            texCache[k] = t;
            return t;
        }

        /// <summary>Filled or ring circle texture (touch buttons, joystick, scope).</summary>
        public static Texture2D Circle(bool ring, int size = 128, float thickness = 0.08f)
        {
            string k = $"c{ring}{size}{thickness}";
            if (texCache.TryGetValue(k, out var t) && t) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = ring ? Mathf.Clamp01((thickness - Mathf.Abs(d - (1f - thickness))) * size * 0.5f) : Mathf.Clamp01((1f - d) * size * 0.5f);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply();
            texCache[k] = t;
            return t;
        }

        /// <summary>Black mask with a transparent circular hole (sniper scope).</summary>
        public static Texture2D ScopeMask()
        {
            const string k = "scope";
            if (texCache.TryGetValue(k, out var t) && t) return t;
            const int n = 256;
            t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01((d - 0.97f) * 40f);
                float vignette = Mathf.Clamp01((d - 0.75f) * 1.2f) * 0.5f;
                t.SetPixel(x, y, new Color(0, 0, 0, Mathf.Max(a, vignette)));
            }
            t.Apply();
            texCache[k] = t;
            return t;
        }

        static void Build()
        {
            if (Mathf.Approximately(builtFor, S) && label != null) return;
            builtFor = S;
            label = new GUIStyle(GUI.skin.label) { fontSize = Px(22), wordWrap = true, richText = true };
            label.normal.textColor = Text;
            small = new GUIStyle(label) { fontSize = Px(17) };
            small.normal.textColor = TextDim;
            title = new GUIStyle(label) { fontSize = Px(64), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            title.normal.textColor = Text;
            header = new GUIStyle(label) { fontSize = Px(30), fontStyle = FontStyle.Bold, wordWrap = false };
            header.normal.textColor = Accent;
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            mono = new GUIStyle(label) { fontSize = Px(20), alignment = TextAnchor.MiddleLeft, wordWrap = false };
            button = new GUIStyle(GUI.skin.button) { fontSize = Px(22), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, border = new RectOffset(12, 12, 12, 12), padding = new RectOffset(8, 8, 4, 4) };
            button.normal.background = Rounded(new Color(0.15f, 0.17f, 0.16f, 0.95f), new Color(0.3f, 0.32f, 0.3f, 1f));
            button.hover.background = button.normal.background;
            button.active.background = Rounded(new Color(0.3f, 0.26f, 0.15f, 0.98f), Accent);
            button.normal.textColor = button.hover.textColor = Text;
            button.active.textColor = Color.white;
            panel = new GUIStyle { border = new RectOffset(12, 12, 12, 12) };
            panel.normal.background = Rounded(Panel, new Color(0.25f, 0.27f, 0.25f, 0.9f));
        }

        public static int Px(float size) => Mathf.Max(8, Mathf.RoundToInt(size * S * 0.82f));

        public static GUIStyle Label { get { Build(); return label; } }
        public static GUIStyle Small { get { Build(); return small; } }
        public static GUIStyle Title { get { Build(); return title; } }
        public static GUIStyle Header { get { Build(); return header; } }
        public static GUIStyle Center { get { Build(); return center; } }
        public static GUIStyle Mono { get { Build(); return mono; } }
        public static GUIStyle ButtonStyle { get { Build(); return button; } }

        public static GUIStyle Styled(GUIStyle baseStyle, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            var s = new GUIStyle(baseStyle) { fontSize = Px(size), alignment = anchor, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
            s.normal.textColor = color;
            return s;
        }

        public static void DrawPanel(Rect r, Color? tint = null)
        {
            Build();
            if (tint.HasValue)
            {
                var old = GUI.color; GUI.color = tint.Value;
                GUI.Box(r, GUIContent.none, panel);
                GUI.color = old;
            }
            else GUI.Box(r, GUIContent.none, panel);
        }

        public static void Fill(Rect r, Color c) => GUI.DrawTexture(r, Solid(c));

        public static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var c = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.7f * c.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        public static bool Button(Rect r, string text, bool selected = false, bool enabled = true)
        {
            Build();
            var old = GUI.enabled;
            GUI.enabled = enabled;
            var style = button;
            if (selected)
            {
                style = new GUIStyle(button);
                style.normal.background = style.hover.background = Rounded(new Color(0.32f, 0.27f, 0.14f, 0.98f), Accent);
                style.normal.textColor = style.hover.textColor = Color.white;
            }
            bool clicked = GUI.Button(r, text, style);
            GUI.enabled = old;
            if (clicked) AudioManager.Play2D("ui_click", 0.5f);
            return clicked;
        }

        public static float Slider(Rect r, string text, float value, float min, float max, string format = "0.00")
        {
            Build();
            GUI.Label(new Rect(r.x, r.y, r.width * 0.42f, r.height), text, label);
            var track = new Rect(r.x + r.width * 0.44f, r.y + r.height * 0.42f, r.width * 0.42f, r.height * 0.16f);
            Fill(track, new Color(0.25f, 0.27f, 0.25f));
            float k = Mathf.InverseLerp(min, max, value);
            Fill(new Rect(track.x, track.y, track.width * k, track.height), Accent);
            float knob = r.height * 0.6f;
            GUI.DrawTexture(new Rect(track.x + track.width * k - knob / 2, r.y + r.height / 2 - knob / 2, knob, knob), Circle(false), ScaleMode.StretchToFill, true, 0, Color.white, 0, 0);
            GUI.Label(new Rect(r.x + r.width * 0.88f, r.y, r.width * 0.12f, r.height), value.ToString(format), Styled(label, 20, Text, TextAnchor.MiddleRight));

            var e = Event.current;
            var hit = new Rect(track.x - knob, r.y, track.width + knob * 2, r.height);
            int id = GUIUtility.GetControlID(FocusType.Passive, hit);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (hit.Contains(e.mousePosition)) { GUIUtility.hotControl = id; value = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width)); e.Use(); GUI.changed = true; }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) { value = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width)); e.Use(); GUI.changed = true; }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
            }
            return value;
        }

        public static bool Toggle(Rect r, string text, bool value)
        {
            Build();
            GUI.Label(new Rect(r.x, r.y, r.width * 0.7f, r.height), text, label);
            var b = new Rect(r.xMax - r.height * 2.2f, r.y + r.height * 0.15f, r.height * 2.2f, r.height * 0.7f);
            if (Button(b, value ? "ON" : "OFF", value)) value = !value;
            return value;
        }

        public static int Selector(Rect r, string text, string[] options, int index)
        {
            Build();
            GUI.Label(new Rect(r.x, r.y, r.width * 0.4f, r.height), text, label);
            float bw = r.height * 1.1f;
            var left = new Rect(r.x + r.width * 0.42f, r.y + r.height * 0.1f, bw, r.height * 0.8f);
            var right = new Rect(r.xMax - bw, r.y + r.height * 0.1f, bw, r.height * 0.8f);
            if (Button(left, "<")) index = (index - 1 + options.Length) % options.Length;
            if (Button(right, ">")) index = (index + 1) % options.Length;
            GUI.Label(new Rect(left.xMax, r.y, right.x - left.xMax, r.height), options[Mathf.Clamp(index, 0, options.Length - 1)], Styled(label, 22, Accent, TextAnchor.MiddleCenter, true));
            return index;
        }

        public static void Bar(Rect r, float k, Color c)
        {
            Fill(r, new Color(0.2f, 0.22f, 0.2f, 0.9f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(k), r.height), c);
        }

        // ---- Touch-friendly drag scrolling ----
        static readonly Dictionary<int, float> scroll = new Dictionary<int, float>();
        static int dragId; static float dragStartY, dragStartScroll; static bool dragMoved;

        /// <summary>Begin a vertically scrolling region. Content is laid out from y = 0 in local coordinates.</summary>
        public static void BeginScroll(int key, Rect view, float contentHeight)
        {
            float max = Mathf.Max(0, contentHeight - view.height);
            scroll.TryGetValue(key, out float y);
            var e = Event.current;
            if (e.type == EventType.MouseDown && view.Contains(e.mousePosition)) { dragId = key; dragStartY = e.mousePosition.y; dragStartScroll = y; dragMoved = false; }
            if (e.type == EventType.MouseDrag && dragId == key)
            {
                float dy = e.mousePosition.y - dragStartY;
                if (Mathf.Abs(dy) > 8f * S) dragMoved = true;
                if (dragMoved) { y = dragStartScroll - dy; e.Use(); }
            }
            if (e.type == EventType.MouseUp && dragId == key) dragId = 0;
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { y += e.delta.y * 20f; e.Use(); }
            y = Mathf.Clamp(y, 0, max);
            scroll[key] = y;
            GUI.BeginGroup(view);
            GUI.BeginGroup(new Rect(0, -y, view.width, Mathf.Max(contentHeight, view.height)));
            // Scroll indicator
            if (max > 0)
            {
                GUI.EndGroup();
                float h = view.height * view.height / contentHeight;
                Fill(new Rect(view.width - 4 * S, (y / max) * (view.height - h), 3 * S, h), new Color(1, 1, 1, 0.25f));
                GUI.BeginGroup(new Rect(0, -y, view.width, Mathf.Max(contentHeight, view.height)));
            }
        }

        public static void EndScroll() { GUI.EndGroup(); GUI.EndGroup(); }

        /// <summary>True when the current click is a drag-scroll rather than a tap (suppresses accidental button presses).</summary>
        public static bool IsDragging => dragId != 0 && dragMoved;
    }
}
