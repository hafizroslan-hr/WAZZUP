using UnityEngine;

namespace Wardline
{
    /// <summary>
    /// Application root. Created automatically at startup (no scene setup needed), owns persistent services,
    /// and switches between the menu and a running match.
    /// </summary>
    public class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }
        public static GameSettings Settings { get; private set; } = new GameSettings();
        public static PlayerStats Stats { get; private set; } = new PlayerStats();
        public const string Version = "1.0.0";

        public bool Paused { get; private set; }
        public bool InMatch => MatchManager.I != null;
        public MenuUI Menu { get; private set; }
        public HUD Hud { get; private set; }
        public MenuBackground Background { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Wardline");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }

        void Awake()
        {
            I = this;
            Settings = Persistence.LoadSettings();
            Stats = Persistence.LoadStats();

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            Layers.ConfigurePhysics();
            InputBridge.Init();
            AudioManager.Create(Settings);
            AudioManager.Prewarm();
            Effects.Init();

            // Remove any default scene camera/light so the app fully controls rendering.
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (cam.gameObject.scene.IsValid()) Destroy(cam.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.gameObject.scene.IsValid()) Destroy(l.gameObject);

            Menu = gameObject.AddComponent<MenuUI>();
            Hud = gameObject.AddComponent<HUD>();
            Hud.enabled = false;
            ApplySettings(false);
            ShowMenu();
        }

        public void ApplySettings(bool save = true)
        {
            Effects.Quality = Settings.effectsQuality;
            Haptics.Enabled = Settings.haptics;
            QualityManager.Apply(Settings);
            AudioManager.I?.ApplyVolumes();
            if (save) Persistence.SaveSettings(Settings);
        }

        public void SaveStats() => Persistence.SaveStats(Stats);

        public static void ResetStats() { Persistence.ResetStats(); Stats = new PlayerStats(); }

        void ShowMenu()
        {
            Time.timeScale = 1f;
            Paused = false;
            AudioListener.pause = false;
            InputBridge.SetCursorLocked(false);
            if (Background == null) Background = MenuBackground.Create(Menu.SelectedMapIndex);
            Menu.Open();
            Hud.enabled = false;
            AudioManager.PlayMusic("music_menu");
        }

        public void StartMatch(MatchConfig cfg)
        {
            Persistence.SaveConfig(cfg);
            if (Background) { Background.Dispose(); Background = null; }
            Menu.Close();
            AudioManager.PlayMusic(null);
            MatchManager.Begin(cfg);
            Hud.enabled = true;
            Hud.OnMatchStarted();
            InputBridge.SetCursorLocked(true);
            Paused = false;
            Time.timeScale = 1f;
        }

        public void QuitMatch()
        {
            if (MatchManager.I != null)
            {
                if (MatchManager.I.Phase != MatchPhase.MatchOver) MatchManager.I.EndMatch(true);
                MatchManager.I.Teardown();
            }
            ShowMenu();
        }

        public void SetPaused(bool p)
        {
            if (!InMatch) return;
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            AudioListener.pause = p;
            InputBridge.SetCursorLocked(!p && !Hud.AnyMenuOpen);
        }

        void OnApplicationPause(bool pause)
        {
            if (pause && InMatch && !Paused) SetPaused(true);
            if (pause) { Persistence.SaveSettings(Settings); SaveStats(); }
        }

        void OnApplicationQuit() { Persistence.SaveSettings(Settings); SaveStats(); }
    }

    /// <summary>Live 3D map flyover behind the main menu, also used to preview maps before a match.</summary>
    public class MenuBackground : MonoBehaviour
    {
        MapRuntime map;
        Camera cam;
        int mapIndex = -1;
        float t;
        public bool fullPreview;

        public static MenuBackground Create(int index)
        {
            var go = new GameObject("MenuBackground");
            var bg = go.AddComponent<MenuBackground>();
            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(go.transform);
            bg.cam = camGo.AddComponent<Camera>();
            bg.cam.fieldOfView = 55f; bg.cam.farClipPlane = 400f; bg.cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            bg.ShowMap(index);
            return bg;
        }

        public void ShowMap(int index)
        {
            var maps = MapLibrary.All;
            if (maps.Count == 0) return;
            index = Mathf.Clamp(index, 0, maps.Count - 1);
            if (index == mapIndex) return;
            mapIndex = index;
            map?.Destroy();
            map = MapBuilder.Build(maps[index], true);
            cam.backgroundColor = maps[index].Theme.sky;
            t = Random.value * 10f;
            QualityManager.Apply(GameApp.Settings);
        }

        void Update()
        {
            if (map == null) return;
            t += Time.unscaledDeltaTime * (fullPreview ? 0.06f : 0.035f);
            var def = map.def;
            Vector3 center = new Vector3(def.width * MapBuilder.Cell * 0.5f, 0, def.height * MapBuilder.Cell * 0.5f);
            float rx = def.width * MapBuilder.Cell * 0.42f, rz = def.height * MapBuilder.Cell * 0.42f;
            float height = fullPreview ? 30f + Mathf.Sin(t * 2f) * 8f : 16f + Mathf.Sin(t * 1.3f) * 4f;
            Vector3 pos = center + new Vector3(Mathf.Cos(t) * rx, height, Mathf.Sin(t) * rz);
            cam.transform.position = pos;
            Vector3 look = center + new Vector3(Mathf.Cos(t + 1.2f) * rx * 0.3f, 0, Mathf.Sin(t + 1.2f) * rz * 0.3f);
            cam.transform.rotation = Quaternion.LookRotation(look - pos);
        }

        public void Dispose()
        {
            map?.Destroy();
            map = null;
            Destroy(gameObject);
        }
    }
}
