# Phase report

This report covers the first delivery. Phases 1–3 of the specification (foundation, classic gameplay, maps and
content) are implemented in code, plus most of Phase 4's UI and settings work.

> **Important:** this environment had no Unity editor, no C# compiler and no iOS device. The project has **not** been
> compiled, run in the editor or tested on an iPhone. The code was written against Unity 6 APIs and reviewed by hand.
> Bracket balance and map connectivity/balance were checked with scripts. Plan for a first pass of compile fixes and
> gameplay tuning in Unity.

## 1. Completed features

**Foundation (Phase 1)**
- Runtime bootstrap with no scene setup, persistent settings and stats, procedural textures, meshes and audio.
- First-person controller: walk/run/crouch/jump, fast-acceleration movement without slide, silent walking, landing
  penalty, fall damage, headroom check, recoil-punched camera, scopes, spectator camera.
- Touch controls: floating joystick, swipe look, drag-to-steer fire, second fire button, gyro, aim acceleration,
  per-axis and scoped sensitivity, safe area, left-handed mode, customizable layout editor, haptics (native iOS
  plugin). Keyboard and mouse for editor testing.
- Hitscan shooting with five hit zones, armour/helmet, range falloff, spread model (moving/air/crouch/sustained
  fire), recoil climb and sway, shotgun pellets, knife slash/stab/backstab.
- Bots at four difficulties with distinct behaviour profiles.

**Classic gameplay (Phase 2)**
- Raider/Warden teams, freeze time, buy time and zones, round timer, plant (3 s) and defuse (10 s, or 5 s with a kit),
  detonation, all specified win conditions (including "charge planted → Raider elimination doesn't end the round"),
  half-time side switch with money reset, first to 16 of 30 (configurable), match-end scoreboard.
- Economy: start money, per-weapon kill rewards, win rewards (3250/3500), loss streak bonus (1400 + 500 × n up to 3400),
  +800 to Raiders on a planted loss, no loss bonus for Raiders who survive a timed-out round, team-kill penalty, $16,000 cap.
- Bots buy based on their own money, team average money (eco rounds), side, difficulty and whether a teammate already
  has a sniper.
- Dropped weapons and charge, walk-over pickup, USE to swap.

**Maps and content (Phase 3)**
- Five validated original maps with distinct themes, lighting, fog, ambience, sites, lanes, platforms and cover.
- 21 guns, knife, three grenade types (bouncing physics, flash blinding that depends on facing and line of sight, smoke
  that blocks vision), armour, helmet, kit, charge.
- Map select with generated previews, descriptions and a live flyover preview.
- Team Deathmatch (respawn with spawn protection, kill and time limits, free loadouts) and Practice (stationary,
  moving or fighting targets, unlimited ammo, accuracy and headshot stats).

**UI / polish (part of Phase 4)**
- Main/Play/Setup/Map select/Settings/Weapon info/Profile/About screens, remembered setups per mode.
- HUD: health, armour, ammo, weapon, money, timer, scores, alive pips, radar (enemies appear only when spotted), kill
  feed with headshots, round and match announcements, charge status, plant/defuse progress, damage direction, flash,
  smoke and low-health overlays, hit markers, dynamic crosshair (4 styles, size and colour), sniper scope.
- Buy menu, scoreboard, radio menu (commands change bot behaviour), pause menu with in-match settings, first-launch
  tutorial.
- Settings for graphics (preset, FPS target, shadows, effects, render scale), controls, audio (master, music, SFX,
  footsteps/environment) and gameplay; all saved.
- Local statistics: matches, W/L/D, kills, deaths, headshots, accuracy, best streak, rounds, plants/defuses,
  favourite weapon, best map.

## 2. Features still pending, or simplified

| Item | Status |
|---|---|
| Hand-made 3D art and recorded audio | Uses procedural low-poly models and synthesized audio. `CharacterModel`/`WeaponModels` are the swap points for real assets |
| Skeletal animation | Procedural limb animation and death fall, not rigged animation clips |
| Voice radio lines | Radio is text with a radio squelch sound; there are no recorded voices |
| Wall penetration | Not implemented; bullets stop at walls (this also keeps bots fair) |
| Silencer attach/detach | Silenced weapons are permanently silenced |
| Map art detail | Maps are blocky geometry with tiled procedural textures, themed lighting and props (trees, lamps, crates). There is no hand-sculpted detail |
| Device thermal/battery profiling | Not done (no device available) |
| Localization | English only |

## 3. Known risks and bugs (untested)

- **Compilation:** written for Unity 6000.0 and URP 17. Small API differences may need fixes, for example URP asset
  property setters used in `QualityManager` and the editor setup.
- **URP auto-setup:** creating a `UniversalRendererData` from script may not fill every resource in some URP versions.
  The fallback is to create the URP asset by hand (see `IOS_BUILD.md`).
- **Gyroscope axes:** the mapping for landscape-left/right is reasoned, not measured. An "invert vertical" option is
  provided; horizontal may also need flipping on device.
- **IMGUI on device:** menus use IMGUI. It works on iOS but costs more CPU than uGUI. If profiling shows UI cost, port
  the HUD to uGUI or UI Toolkit.
- **Bot tuning:** reaction times, aim errors and the cover heuristics are first-pass values that need playtesting.

## 4. Performance notes

- Walls are merged into runs and all map geometry is static-batched. Materials are shared and cached, with GPU
  instancing enabled.
- Effects (debris, decals, tracers, muzzle flashes, smoke puffs) and audio sources are pooled with caps.
- Bot perception is throttled (0.1–0.35 s per bot by difficulty) and decisions run at 5 Hz. Raycasts per perception
  tick are at most 3 per visible-candidate enemy.
- Expected hotspots to profile: IMGUI `OnGUI` (HUD), the smoke-density checks, and point lights on the Research
  Facility map.

## 5. Test results

| Check | Result |
|---|---|
| `Tools/validate_maps.py` (shape, markers, full reachability, ramp sanity, Warden-first timing) | **Pass:** all 5 maps |
| Bracket balance across all C# files (script) | **Pass** |
| Unity compile | **Not run** (no editor available) |
| Editor play test / QA checklist items 1–14 | **Not run** |
| iPhone device test | **Not run** |

Map timing in cells (1 cell = 3 m), shortest route to the nearest site cell:

| Map | Site A (Raider / Warden) | Site B (Raider / Warden) |
|---|---|---|
| Desert Strike | 32 / 8 | 28 / 8 |
| Urban Assault | 27 / 6 | 31 / 5 |
| Arctic Base | 25 / 6 | 24 / 6 |
| Research Facility | 27 / 7 | 27 / 7 |
| Tropical Outpost | 20 / 5 | 23 / 5 |

## 6. Next development steps

1. Open in Unity 6, fix any compile errors, and run the QA checklist in the editor with keyboard and mouse.
2. Build to an iPhone; tune touch sensitivity defaults, gyro axes, button sizes and the safe-area layout.
3. Playtest each difficulty on each map, then tune `BotProfile` values and the coordinator's timings.
4. Profile on device (Medium preset, 10 bots); port the HUD to uGUI if needed.
5. Art pass: replace procedural characters and weapons with rigged models, add decals and props per theme, and add
   recorded audio.
