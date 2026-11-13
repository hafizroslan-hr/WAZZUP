# Wardline

**Wardline** is an offline, round-based tactical first-person shooter for iPhone, built with Unity 6 (URP, C#).
Two teams, **Raiders** (attack) and **Wardens** (defend), fight over two charge sites across five original maps,
against bots with four difficulty levels that change how the bots *behave*.

Everything in the project is original. Teams, weapons, maps, models, textures, sound effects and music are designed in
code or generated procedurally at runtime, so there are no third-party assets and no licensing questions.

> **Status:** I wrote all the code and checked the maps with the included validator. I could not open the project
> in Unity, compile it, or run it on a device here, because no Unity editor was available. Expect a short round of
> compile fixes and tuning on first open. See [`Docs/PHASE_REPORT.md`](Docs/PHASE_REPORT.md) for an honest list of
> what's done, what's pending and what hasn't been tested.

## Features

| Area | What's implemented |
|---|---|
| Modes | **Classic Charge** (plant/defuse, rounds, economy, half-time side switch); **Team Deathmatch** (respawns, kill and time limits); **Practice Range** (stationary, moving or fighting targets, unlimited ammo, accuracy stats) |
| Bots | Finite-state AI on runtime-baked NavMesh. Vision cone + line of sight + smoke + flashbangs, hearing, reaction delays, aim settling, recoil control, burst fire, strafing, cover, grenades, plant/defuse/recover, rotations, team strategies (rush/default/split/fake), obeys your radio commands |
| Difficulty | Easy / Normal / Hard / Expert change reaction time, aim speed/error, head-aim rate, field of view, hearing, cover use, grenade use, flanking, burst discipline, teamwork and mistake rate. Health and damage stay the same at every level |
| Weapons | 5 pistols, 4 SMGs, 2 shotguns, 6 rifles, 3 snipers, 1 LMG, knife, HE/flash/smoke, armour, helmet, defuse kit, charge. Each has its own damage, fire rate, magazine, reload, recoil pattern, spread, range falloff, armour penetration, movement speed and sound |
| Combat | Hitscan with head/chest/stomach/arm/leg zones, armour model, range falloff, movement/jump/crouch accuracy, CS-style recoil climb and sway, scopes, knife backstabs, wall-blocked explosions, friendly fire option |
| Economy | Start money, kill rewards per weapon class, win/loss rewards with loss streak bonus, plant/defuse bonuses, buy zones and buy time, equipment kept by survivors |
| Maps | Desert Strike, Urban Assault, Arctic Base, Research Facility, Tropical Outpost. Each has two sites, three lanes, chokepoints, elevated positions, its own theme, lighting, fog and ambience. Maps are text files, so more can be added without code changes |
| Touch controls | Floating joystick (a light push walks silently), swipe look, drag-to-steer fire button, optional second fire button, gyro aiming, aim acceleration, separate X/Y/scoped sensitivity, customizable button positions and sizes, left-handed mirroring, safe-area aware, iOS edge-gesture deferral, haptics |
| UI | Main menu over a live map flyover, play/setup screens with remembered settings, map select with generated previews and a flyover preview, settings (graphics/controls/audio/gameplay), weapon info with stat bars, profile/stats, about, HUD with radar, kill feed, buy menu, scoreboard, radio, pause, tutorial on first launch |
| Persistence | Settings, button layout, last match setup per mode and career statistics, saved locally (PlayerPrefs) |
| Performance | Merged wall geometry + static batching, pooled effects/audio, throttled AI perception, quality presets, render scale, FPS target (30/60/120) |

## Quick start (Unity editor)

1. Install **Unity 6** (6000.0 LTS or newer) with the **iOS Build Support** module.
2. In Unity Hub, choose **Add → Add project from disk** and select this folder, then pick your Unity 6 version.
3. On first open, the package manager installs URP and the Input System. If Unity asks to enable the new input
   backend, choose **Yes** and let it restart.
4. The `Wardline/Setup Project` step runs automatically. It creates the scene, the URP asset, the shader-reference
   materials and the iOS player settings. You can run it again any time from the **Wardline** menu.
5. Open `Assets/Wardline/Scenes/Wardline.unity` and press **Play**. In the editor you can play with keyboard and
   mouse: WASD, mouse, LMB/RMB, R, E, B, F, G, Q, 1–5, Tab, Z, Esc.

To build for iPhone, see [`Docs/IOS_BUILD.md`](Docs/IOS_BUILD.md).

## Repository layout

```
Assets/
  Wardline/
    Scripts/
      Core/      app bootstrap, settings & stats persistence, rendering helpers, procedural textures/meshes
      Audio/     procedural sound synthesis, pooled 3D audio, AI hearing events
      Input/     Input System bridge (legacy fallback), touch controls
      Actors/    shared actor state, procedural character model, player controller, first-person view model
      Weapons/   weapon database, inventory & firing, ballistics, effects, grenades/smoke/pickups, models
      Bots/      difficulty profiles, bot buying, bot brain (FSM), team coordinator
      Maps/      map parsing, geometry builder, runtime NavMesh bake, previews, themes
      Match/     rules engine for all modes (rounds, charge, economy, respawns, radio, stats)
      UI/        IMGUI toolkit, menus, HUD, layout editor, tutorial
    Editor/      one-click setup + iOS build (menu and command line)
    Resources/Maps/*.txt   map layouts (data)
  Plugins/iOS/   native haptics (UIImpactFeedbackGenerator)
Docs/            architecture, iOS build, map format, phase report
Tools/           validate_maps.py (map connectivity & balance checker)
```

## Documentation

- [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md): how the systems fit together and how to extend them
- [`Docs/IOS_BUILD.md`](Docs/IOS_BUILD.md): building, signing and deploying to an iPhone
- [`Docs/MAP_FORMAT.md`](Docs/MAP_FORMAT.md): the map file format and how to add maps
- [`Docs/PHASE_REPORT.md`](Docs/PHASE_REPORT.md): completed features, pending work, known issues and test results
