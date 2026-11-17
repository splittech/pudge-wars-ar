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

### Unity save dialogs
- When Unity shows the `Scene(s) Have Been Modified` dialog while working on this project, the agent is authorized to click `Save` independently, without asking for confirmation. This preserves the user's scene changes and allows tests or other Editor operations to continue.
- Before interacting, verify that the dialog belongs to this project's Unity Editor and select the exact `Save` button. Do not automatically select `Don't Save` or `Cancel`; discarding changes requires an explicit user instruction.
- If `Save` opens a Save As dialog for an unnamed scene, ask the user for the destination instead of inventing a scene name or overwriting another asset.

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

### C# code style

- Read `Docs/csharp_code_style.md` before creating or modifying C# code. Its rules and example are the accepted project style and are mandatory for new and modified code.
- Use four spaces for indentation and Allman braces.
- Always use braces for `if`, `else`, loops and `using` blocks, including single-statement bodies.
- Write one statement per line and declare each field separately. Do not compress assignments or control flow onto one line.
- Use expression-bodied members only for trivial properties and simple calculations. Methods with validation or state changes use block bodies.
- Avoid nested ternary expressions. Use descriptive names and separate logical steps with blank lines.
- Use `var` when the type is obvious from the right-hand side; otherwise prefer an explicit type.
- Follow the naming, member ordering and argument wrapping rules in the style document. Existing compact code is not a style precedent when it conflicts with these rules.
- Before finishing, review all added or modified C# code against the style document and fix violations.
- Do not reformat unrelated code or third-party files. A project-wide style migration requires a separate task.
