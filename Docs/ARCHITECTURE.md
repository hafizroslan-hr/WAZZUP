# Architecture

Wardline is code-first. A single `RuntimeInitializeOnLoadMethod` (`GameApp.Boot`) builds the whole game at runtime, so
the scene is empty and there are no prefabs or binary assets to merge. Each system lives in its own namespace folder
and talks to the others through small public APIs or the `GameEvents` bus.

```
GameApp (persistent root)
 ├─ Settings / Stats ── Persistence (PlayerPrefs JSON)
 ├─ AudioManager ───── SfxSynth (procedural clips), Hearing (AI sound events)
 ├─ MenuUI ─────────── MenuBackground (live map flyover / preview)
 ├─ HUD ────────────── LayoutEditor, Tutorial
 └─ MatchManager (per match)
     ├─ MapRuntime ── MapBuilder (geometry, lighting, runtime NavMesh), MapLibrary (Resources/Maps)
     ├─ Actor[] ───── WeaponHandler, CharacterModel (+ Hitbox zones)
     │    ├─ PlayerController ── TouchControls → InputBridge, ViewModel, cameras
     │    └─ BotBrain ────────── BotProfile, TeamCoordinator, NavMeshAgent
     └─ ChargeObject, Pickup, Grenade, SmokeCloud, Effects (pooled)
```

## Key design decisions

| Decision | Why |
|---|---|
| **One rules path for players and bots** (`WeaponHandler`, `Ballistics`, `Actor.ApplyDamage`, `MatchManager.InteractUpdate`) | Bots can't cheat by construction: same spread, recoil, ammo, plant/defuse timing and damage rules |
| **Bot knowledge only from perception** (`BotBrain.Perceive`: FOV + line of sight through the `VisionMask`, which includes smoke; `Hearing` events; damage direction; teammate callouts) | Meets the fairness rule: no wallhacks, no reading hidden positions |
| **Difficulty = behaviour profile** (`BotProfile`) | Reaction, aim speed/error/settle, head-aim chance, recoil control, FOV, hearing, cover, grenades, strafing, burst discipline, teamwork, mistakes. Health and damage are never scaled |
| **Maps as data** (ASCII files) with **runtime NavMesh baking** (`NavMeshBuilder`) | Five maps without hand-placed geometry, guaranteed navigable, easy to add more, and validated by a script |
| **Procedural art and audio** | No licensing risk, tiny download, consistent style |
| **IMGUI for UI** | No prefab/canvas assets needed. Multi-touch gameplay input is handled separately by `TouchControls`, and IMGUI is used only for drawing and menu taps |
| **Hitboxes are trigger colliders on a dedicated layer** | Precise zone damage without disturbing CharacterController or NavMesh movement |

## Physics layers

| Layer | Index | Used for |
|---|---|---|
| World | 0 | Map geometry (bullets, vision, grenades, NavMesh) |
| Hitbox | 8 | Body-zone triggers (bullets only) |
| Actor | 9 | Movement capsules (player CharacterController, bot capsules) |
| Smoke | 10 | Smoke puffs: block AI vision, not bullets |
| ViewModel | 12 | First-person weapon, drawn by an overlay camera |

## Extending

- **New weapon:** add a `WeaponDef` in `WeaponDatabase`. Shape parameters (`modelLength`, `hasScope`, `boxMag`, …)
  drive the procedural model, and `sound` picks a synthesized clip. The buy menu, weapon info screen and bot buying
  pick it up automatically (bots buy by archetype in `BotBuy.PickPrimary`).
- **New map:** add a text file (see `MAP_FORMAT.md`).
- **New difficulty or tuning:** edit or add a `BotProfile`.
- **New mode:** add a `GameMode`, branch in `MatchManager.Update`/`StartRound`, and add a coordinator objective
  (`TeamCoordinator.GetObjective`).
- **Swap in real art:** replace `CharacterModel.Construct`/`WeaponModels.Build` with prefab instantiation. Hitboxes only
  need `Hitbox` components with the right `HitZone` on trigger colliders in layer 8.

## Bot state machine

`Idle → Advance (objective from TeamCoordinator) ⇄ Investigate (heard sound) ⇄ Search (lost target) ⇄ Engage`
`Engage → TakeCover (reloading / low HP, Hard+) / Retreat`
Objective states: `GuardSite`, `Rotate`, `Plant`, `Defuse`, `RecoverCharge`, `Patrol`. Order states: `Follow`, `HoldPosition`.

Each bot ticks perception every 0.1–0.35 s depending on difficulty and makes decisions every 0.2 s. Aiming and firing
run every frame. Stuck recovery goes in steps: re-path at 1.5 s, nudge, then warp along the path at 6 s.

## Team tactics (`TeamCoordinator`)

- **Raiders** pick a target site and a strategy each round. Easy always rushes. Normal uses rush or default. Hard
  adds split pushes. Expert adds fakes and a lurker. Bots stage at lane waypoints, then execute when about 60% have
  arrived or a timer expires, throwing smokes on the Warden rotation path and flashes into the site. After a plant
  they hold cover around the charge.
- **Wardens** split across both sites, with a mid lurker at 5+ players. They rotate when several callouts come from
  one site; how quickly depends on difficulty. After a plant, Hard and Expert regroup before retaking. Expert checks
  the area is clear before defusing unless time is short.
- **Radio:** player commands (*follow me, hold, regroup, need backup, enemy spotted*, …) change teammate bot orders
  or feed the team's shared intel.
