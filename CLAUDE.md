# CLAUDE.md

## Language
- Reply to the user in Russian.
- Code comments, identifiers and commit messages: English.

## Project
Pudge Wars AR: a student project, LAN multiplayer hook duel (Pudge Wars mechanics, no Dota 2 assets) on a real table in AR. Android/ARCore only. Unity 6000.6.5f1, URP.

`Docs/` is the source of truth and must stay in sync with the code. If a decision diverges from the TDD, update the TDD first (with a changelog entry), then the code.
- `Docs/pudge_wars_ar_tdd.md`: architecture, module boundaries, network protocol, hook state machine, balance, config fields. Requirement IDs (FR-…/NFR-…) are used project-wide.
- `Docs/pudge_wars_ar_checklist.md`: stages 0–7 with done criteria.

## Commands
Run from the repo root:
```
unity test --mode EditMode [--filter SmokeTests]   # results -> test-results.xml
unity build                                         # batch-mode build
```
After changing C# code, run the EditMode tests and fix all compile errors before finishing.

Packages: UPM git packages in `Packages/manifest.json`; R3 and ObservableCollections via NuGetForUnity (`Assets/packages.config` -> `Assets/Packages/`). Mirror (`Assets/Mirror`) and DOTween (`Assets/Plugins/Demigiant`) are vendored.

## Architecture
- "AR is a projector": the simulation runs only on the server in 2D arena coordinates (game units, `float2`) and knows nothing about AR. Each client places its own `ArenaRoot`; all game objects are its children. Only arena coordinates go over the network, never AR world coordinates. Height and scale are visual only. Flat (non-AR) mode is just another `IArenaAnchorProvider`.
- Server-authoritative: Mirror host mode, KCP, port 7777. Clients send only intents via `[Command]` (move, throw hook, buy upgrade, lobby actions); the server validates them and rejects invalid ones (FR-15).
- `ServerTickRunner` calls `MatchSimulation.Step(dt)` at a fixed 30 Hz (own accumulator, max 4 steps per frame, not `FixedUpdate`), then copies state to network components.
- All timestamps are `NetworkTime.time` values, not countdown timers.
- The hook syncs via the compact `HookNetState` struct (no `NetworkTransform`); clients compute the tip position every frame.

### Assemblies
`Assets/Game/Scripts/<Layer>/Game.<Layer>.asmdef`, root namespace = assembly name.

| Assembly | Purpose | May reference |
|---|---|---|
| Game.Core | Pure C# simulation, Geo2D, ScriptableObject configs, `IPlayerCommands` | Unity.Mathematics, Alchemy (inspector attributes only) |
| Game.Net | Mirror adapters, `PudgeNetworkManager`, tick runner, spawning | Core, Mirror, R3, VContainer, UniTask |
| Game.AR | Arena placement, anchors, tracking | AR Foundation, Input, R3, VContainer, UniTask |
| Game.Input | `ITouchGestures` (EnhancedTouch), `IArenaSpace`, `InputRouter` | Core, Input System, R3, VContainer |
| Game.Presentation / Game.UI | Views, VFX, HUD, screens | Core, Net, R3, DOTween (UI also UniTask) |
| Game.Services | Scene service, logging, loading screen (not in TDD yet) | UniTask, Alchemy, SceneReference, TMP, DOTween |
| Game.App | LifetimeScopes, entry points | All |
| Game.Tests.EditMode | NUnit EditMode tests (`Assets/Game/Tests/Edit Mode/`) | Core, Net, Input |

Boundary rules the compiler does not enforce:
- No MonoBehaviour logic, `Transform` or Physics in Core (UnityEngine only for ScriptableObject).
- Game.Input does not reference Game.Net: it publishes intents to `IPlayerCommands` (declared in Core, implemented in Net).

## Conventions
- All game files live in `Assets/Game/`; everything else is third-party or Unity-generated.
- Folder names may contain spaces (`Scene Service/`, `Edit Mode/`), namespaces do not (`Game.Services.SceneService`); this is intentional (`.editorconfig` disables IDE0130).
- C# style: block-scoped namespaces; `private readonly` fields named `_camelCase`; classes are `sealed` by default.
