using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wardline
{
    /// <summary>
    /// Creates and caches materials. Uses the shaders referenced by Resources/WardlineMaterials (created by the
    /// editor setup so they are never stripped from builds) and falls back to Shader.Find for the editor.
    /// </summary>
    public static class MaterialLib
    {
        static Material litBase, unlitBase;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static bool IsURP => GraphicsSettings.currentRenderPipeline != null;

        static Material LitBase
        {
            get
            {
                if (litBase) return litBase;
                litBase = Resources.Load<Material>("WardlineMaterials/Lit");
                if (!litBase)
                {
                    var sh = IsURP ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
                    if (!sh) sh = Shader.Find("Standard");
                    litBase = new Material(sh);
                }
                return litBase;
            }
        }

        static Material UnlitBase
        {
            get
            {
                if (unlitBase) return unlitBase;
                unlitBase = Resources.Load<Material>("WardlineMaterials/Unlit");
                if (!unlitBase)
                {
                    var sh = IsURP ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
                    if (!sh) sh = Shader.Find("Unlit/Color");
                    unlitBase = new Material(sh);
                }
                return unlitBase;
            }
        }

        public static Material Lit(Color c, Texture2D tex = null, float smooth = 0.15f, float metallic = 0f)
        {
            string key = $"L{c}{(tex ? tex.name : "")}{smooth}{metallic}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(LitBase) { color = c, name = "Lit_" + key };
            if (tex) m.mainTexture = tex;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            string key = $"U{c}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(UnlitBase) { color = c, name = "Unlit_" + key };
            cache[key] = m;
            return m;
        }

        public static void ClearCache() => cache.Clear();
    }

    /// <summary>Procedural textures so the project ships with zero third-party art.</summary>
    public static class ProcTex
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string kind)
        {
            if (cache.TryGetValue(kind, out var t) && t) return t;
            t = Generate(kind);
            t.name = kind;
            cache[kind] = t;
            return t;
        }

        static Texture2D Generate(string kind)
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
            var px = new Color32[N * N];
            var rng = new System.Random(kind.GetHashCode());
            float seed = (float)rng.NextDouble() * 100f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float n = Mathf.PerlinNoise(seed + u * 8f, seed + v * 8f) * 0.6f + Mathf.PerlinNoise(seed + u * 32f, seed + v * 32f) * 0.4f;
                float grain = (float)rng.NextDouble() * 0.08f;
                float val = 0.82f + n * 0.18f - grain;
                switch (kind)
                {
                    case "brick":
                    {
                        int row = y / 16; int off = (row % 2) * 16;
                        bool mortar = y % 16 < 2 || (x + off) % 32 < 2;
                        val = mortar ? 0.62f : val;
                        break;
                    }
                    case "panel":
                    {
                        bool seam = x % 64 < 2 || y % 64 < 2;
                        bool bolt = (x % 64 > 4 && x % 64 < 7 && y % 64 > 4 && y % 64 < 7);
                        val = seam ? 0.6f : bolt ? 0.7f : val;
                        break;
                    }
                    case "tile":
                    {
                        bool seam = x % 32 < 1 || y % 32 < 1;
                        val = seam ? 0.7f : 0.92f + n * 0.08f - grain * 0.5f;
                        break;
                    }
                    case "planks":
                    {
                        bool seam = y % 21 < 2;
                        float wood = Mathf.PerlinNoise(seed + u * 2f, seed + v * 40f);
                        val = seam ? 0.55f : 0.75f + wood * 0.25f - grain;
                        if (x % 64 < 2 && (y / 21) % 2 == 0) val = 0.55f;
                        break;
                    }
                    case "crate":
                    {
                        bool frame = x < 10 || x > N - 11 || y < 10 || y > N - 11;
                        bool diag = Mathf.Abs(x - y) < 6 || Mathf.Abs(x - (N - y)) < 6;
                        float wood = Mathf.PerlinNoise(seed + u * 3f, seed + v * 30f);
                        val = frame || diag ? 0.62f + wood * 0.15f : 0.8f + wood * 0.2f - grain;
                        if (y % 32 < 1) val *= 0.85f;
                        break;
                    }
                    case "snow":
                        val = 0.9f + n * 0.1f - grain * 0.3f;
                        break;
                    case "sand":
                        val = 0.86f + n * 0.14f - grain;
                        break;
                    case "grass":
                    {
                        float blade = Mathf.PerlinNoise(seed + u * 64f, seed + v * 64f);
                        val = 0.7f + n * 0.2f + blade * 0.15f - grain;
                        break;
                    }
                    case "hazard":
                    {
                        bool stripe = ((x + y) / 16) % 2 == 0;
                        val = stripe ? 1f : 0.25f;
                        break;
                    }
                }
                byte b = (byte)Mathf.Clamp(val * 255f, 0, 255);
                px[y * N + x] = new Color32(b, b, b, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }

    /// <summary>Mesh helpers. Boxes get world-scaled UVs so tiled textures keep a constant density.</summary>
    public static class MeshUtil
    {
        static readonly Dictionary<Vector3, Mesh> boxCache = new Dictionary<Vector3, Mesh>();
        const float UvMeters = 2.5f;

        public static Mesh Box(Vector3 size)
        {
            Vector3 key = new Vector3(Mathf.Round(size.x * 100) / 100, Mathf.Round(size.y * 100) / 100, Mathf.Round(size.z * 100) / 100);
            if (boxCache.TryGetValue(key, out var cached) && cached) return cached;
            var m = new Mesh { name = "Box" };
            Vector3 h = size * 0.5f;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            void Face(Vector3 n, Vector3 u, Vector3 v, float su, float sv)
            {
                int i = verts.Count;
                Vector3 c = Vector3.Scale(n, h);
                Vector3 hu = Vector3.Scale(u, h), hv = Vector3.Scale(v, h);
                verts.Add(c - hu - hv); verts.Add(c + hu - hv); verts.Add(c + hu + hv); verts.Add(c - hu + hv);
                for (int k = 0; k < 4; k++) norms.Add(n);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(su / UvMeters, 0)); uvs.Add(new Vector2(su / UvMeters, sv / UvMeters)); uvs.Add(new Vector2(0, sv / UvMeters));
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.left, Vector3.forward, size.x, size.z);
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            boxCache[key] = m;
            return m;
        }

        /// <summary>Creates a textured, collidable box in world space.</summary>
        public static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            if (rot.HasValue) go.transform.rotation = rot.Value;
            go.AddComponent<MeshFilter>().sharedMesh = Box(size);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>A primitive without its collider (for visuals only).</summary>
        public static GameObject Visual(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Quaternion? localRot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (localRot.HasValue) go.transform.localRotation = localRot.Value;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }
    }

    /// <summary>Applies graphics settings (quality, shadows, render scale, frame rate) at runtime.</summary>
    public static class QualityManager
    {
        public static void Apply(GameSettings s)
        {
            Application.targetFrameRate = s.targetFps;
            QualitySettings.vSyncCount = 0;
            float shadowDist = s.shadowQuality == 0 ? 0f : s.graphicsQuality == 0 ? 25f : s.graphicsQuality == 1 ? 40f : 60f;
            QualitySettings.shadowDistance = shadowDist;
            QualitySettings.antiAliasing = s.graphicsQuality == 2 ? 4 : s.graphicsQuality == 1 ? 2 : 0;
            QualitySettings.lodBias = 0.6f + s.graphicsQuality * 0.4f;

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = Mathf.Clamp(s.renderScale, 0.5f, 1f);
                urp.shadowDistance = shadowDist;
                urp.msaaSampleCount = s.graphicsQuality == 2 ? 4 : s.graphicsQuality == 1 ? 2 : 1;
            }
            else
            {
                // Built-in pipeline fallback: approximate render scale with resolution scaling on device.
                if (Application.isMobilePlatform)
                {
                    var r = Screen.currentResolution;
                    Screen.SetResolution(Mathf.RoundToInt(r.width * s.renderScale), Mathf.RoundToInt(r.height * s.renderScale), true);
                }
            }

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional)
                    l.shadows = s.shadowQuality == 0 ? LightShadows.None : s.shadowQuality == 1 ? LightShadows.Hard : LightShadows.Soft;
        }

        public static void SetupViewModelCamera(Camera main, Camera vm)
        {
            if (MaterialLib.IsURP)
            {
                var vmData = vm.GetUniversalAdditionalCameraData();
                vmData.renderType = CameraRenderType.Overlay;
                vmData.renderShadows = false;
                var mainData = main.GetUniversalAdditionalCameraData();
                if (!mainData.cameraStack.Contains(vm)) mainData.cameraStack.Add(vm);
            }
            else
            {
                vm.clearFlags = CameraClearFlags.Depth;
                vm.depth = main.depth + 1;
            }
        }
    }

    /// <summary>Native iOS haptics (UIImpactFeedbackGenerator) with a safe no-op elsewhere.</summary>
    public static class Haptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void _WardlineHaptic(int style);
#endif
        static float lastTime;
        public static bool Enabled = true;

        /// <param name="style">0 light, 1 medium, 2 heavy</param>
        public static void Pulse(int style = 0, float minInterval = 0.05f)
        {
            if (!Enabled || Time.unscaledTime - lastTime < minInterval) return;
            lastTime = Time.unscaledTime;
#if UNITY_IOS && !UNITY_EDITOR
            _WardlineHaptic(style);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (style >= 2) Handheld.Vibrate();
#endif
        }
    }
}
