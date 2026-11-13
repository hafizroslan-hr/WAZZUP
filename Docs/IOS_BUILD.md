# Building Wardline for iPhone

## Requirements

- A Mac with **Xcode 15 or newer**
- **Unity 6** (6000.0 LTS or newer) with **iOS Build Support** installed through Unity Hub
- An Apple ID. A free account can deploy to your own device for 7 days; a paid Apple Developer Program membership
  is needed for TestFlight or the App Store.
- iPhone running **iOS 15 or later**. The target is iPhone XS / A12 or newer, and the quality presets scale down for
  older devices.

## 1. Prepare the project

1. Open the project in Unity 6. Accept the prompt to enable the new Input System backend, which restarts the editor.
2. Run **Wardline → Setup Project**. It also runs automatically on first open. It:
   - creates `Assets/Wardline/Scenes/Wardline.unity` and adds it to Build Settings
   - creates and assigns a URP asset, if the project doesn't have one
   - creates `Resources/WardlineMaterials/Lit.mat` and `Unlit.mat`, so the URP shaders are included in builds
   - sets iOS player settings: IL2CPP, Metal, landscape only, full screen, hidden status bar, deferred edge
     gestures, auto-hiding home indicator, iOS 15 minimum, automatic signing
   - sets *Active Input Handling* to **Both**
3. In **Project Settings → Player → iOS**, set your **Bundle Identifier** (default `com.yourcompany.wardline`) and your
   **Signing Team ID**.

## 2. Build the Xcode project

**From the editor:** choose **Wardline → Build iOS (Xcode project)**. The output goes to `Builds/iOS/`.

**From the command line or CI:**

```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /path/to/WAZZUP \
  -buildTarget iOS \
  -executeMethod Wardline.EditorTools.WardlineSetup.BuildIOS \
  -logFile build.log
```

The process exits with a non-zero code if the build fails.

## 3. Deploy with Xcode

1. Open `Builds/iOS/Unity-iPhone.xcodeproj`.
2. Select the **Unity-iPhone** target, then **Signing & Capabilities**, and choose your team.
3. Connect the iPhone, select it as the run destination and press **Run** (⌘R).
4. The first time, trust the developer certificate on the phone: **Settings → General → VPN & Device Management**.

For TestFlight, choose **Product → Archive**, then **Distribute App**.

## Performance tuning checklist

- **Settings → Graphics** in-game: *Low* turns off shadows and uses a 0.7 render scale. Use 30 FPS for battery saving
  or 120 FPS on ProMotion devices.
- Profile on device with the Unity Profiler (Development Build + Autoconnect Profiler). Watch:
  - `BotBrain.Update`: perception is throttled per difficulty (0.1–0.35 s)
  - `MapBuilder`: map geometry is merged and statically batched once at load
  - draw calls: about 150–300 per frame on Medium in the default maps
- Textures are 128×128 procedural textures with mipmaps, and audio clips are generated once at startup (22 kHz mono),
  so memory use stays small.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Everything renders pink | No URP asset is assigned. Run *Wardline → Setup Project*, or assign a URP asset in Project Settings → Graphics |
| `InvalidOperationException` about `UnityEngine.Input` | Set Active Input Handling to *Both* or *Input System Package* (the setup does this) and restart |
| Touches ignored near screen edges | Expected: edge gestures are deferred. Swipe again from the edge to use system gestures |
| No haptics | Haptics need a physical device with a Taptic Engine. Check Settings → Controls → Haptic feedback |
