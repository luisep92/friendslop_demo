1. The conversation with the user may take place in any language, but anything written in the project is mandatory to be in english.
2. Avoid prose. For writing in files in the project, always use simplified technical english.
3. Treat the user as a senior software engineer, and use the conversation as a colleague exchange instead of an execution line.
4. The user is experienced in software development, but not specifically in the latest unity versions and specific tooling as fishnet, so you can challenge questionable decisions. Propose alternatives with trade-offs. Ask before large refactors. No hand-holding explanations.

This project aims to collect some experience in the friendslop game development before making the real game, so the result may be a working demo with some experimental features.

## Context files
- `NEXT_STEPS.md`: session handoff (state, done, next, known issues). Read at session start. Update at session end, 5 minutes max.
- `ARCHITECTURE.md`: modules, data flow, netcode. Update when architecture changes.
- `DECISIONS.md`: resolved and open decisions with rationale. Update when deciding; do not re-discuss resolved ones without new facts.
- `.claude/skills/unity-verify/`: how to compile, test and play-test through UnityMCP.
- Keep them lean and factual. Remove or flag stale content.

## Stack
- Unity 6000.3.25f1, URP.
- Networking: FishNet 4.7.3, installed via UPM git URL pinned to tag (`Packages/manifest.json`). Upgrade by changing the tag.
  - Config: `Assets/FishNet.Config.XML` (path hardcoded by FishNet, only allowed file outside `GameAssets/`).
  - Prefab generator scans `Assets/GameAssets` only. Output: `Core/Network/DefaultPrefabObjects.asset`.
  - Transports live in a Multipass (`GlobalServerActions` off). `Core/Network/NetworkBootstrap` picks one transport and the role at start. Game code must not depend on a specific transport.
  - Transport: `-transport=tugboat|steam` arg > editor (always Tugboat) > `NetworkConfig.DefaultTransport`.
  - Role: `-netrole=host|server|client|none` arg > editor (MPPM tag `Host`/`Server`/`Client`, else main editor = Host, virtual player = Client) > `NetworkConfig.DefaultBuildRole`.
  - Extra args: `-address`, `-port`.
- Local multiplayer testing: Multiplayer Play Mode (built into Unity 6.3, API `Unity.Multiplayer.PlayMode.CurrentPlayer`).
- Entry scene: `Scenes/Bootstrap.unity`.
- Input: new Input System only (`activeInputHandler` = Input System). Legacy `Input.GetKey*` API is not available. Read input only on the owning client.

## Structure
- Working directory: `Assets/GameAssets/`. Do not add project content outside it.
- Feature-based folders. Each self-contained unit (character, feature) owns its folder, with type subfolders inside (`Scripts/`, `Prefabs/`, `Materials/`, `Models/`, `Animations/`...).
- Shared assets (materials, shaders, SFX, UI kit) go in `Shared/`. Shared code, interfaces and events go in `Core/`.
- Modules depend on `Core/` and `Shared/` only. No references between sibling modules; communicate through interfaces/events in `Core/`.
- Self-contained modules get their own `.asmdef`.

```
Assets/GameAssets/
  Core/
  Shared/
  Characters/<Name>/
  Features/<Name>/
  Scenes/
```

## Factory prototype (`Features/FactoryPrototype/`)
- Co-op automation prototype. Scene: `Scenes/FactoryPrototype.unity`.
- Netcode: server-driven deterministic lockstep. Only build commands and tick numbers go over the network, never belt items.
  - Server steps the canonical sim at 20 Hz and broadcasts `{tick, applied commands, checksum every 20 ticks}`.
  - Clients start from a snapshot (`OnSpawnServer`), replay batches with a small buffer, and resync on checksum mismatch or tick gap.
  - `FactoryNetwork` only forwards bytes. Lockstep logic lives in the simulation assembly and is tested without FishNet.
- Simulation assembly (`Scripts/Simulation`, `noEngineReferences`) must stay deterministic:
  - Integer state only. No float, no `Time`, no `System.HashCode`, no static mutable state.
  - Never iterate `Dictionary`/`HashSet` for logic. Update order = building creation order.
  - All state changes go through `SimCommand`. Views read the sim, never write it.
  - New state must be added to `WriteSnapshot`/`ReadSnapshot`, or checksums and late join break.
- Tests: EditMode, assembly `Friendslop.Features.FactoryPrototype.Tests`. Run them after any sim change.
- Scene `NetworkObject`s need a non-zero SceneId. Objects added by script may get 0 and then never spawn, without error: verify after saving.
- Debug keys: F5 demo line, F6 save, F7 load (server); F8 corrupt replica, F9 join as client, F10 leave (client).
- Saves: `SaveFile` (header + snapshot) at `Application.persistentDataPath/factory_prototype.sav`. After a load, every client gets a fresh snapshot.
- MPPM virtual players also show up as UnityMCP instances: switch with `set_active_instance` to inspect the client replica.

## Unity workflow
- Edit scenes, prefabs and assets via UnityMCP. Never hand-edit `.unity`/`.prefab`/`.asset` YAML.
- Check the Unity console after script changes.
- Do not touch `Library/`, `Temp/`, `.meta` files, or `Packages/manifest.json` without asking.

## Version control
- Git + LFS. Binary assets (models, textures, audio) go through LFS via `.gitattributes`. Add new binary extensions there.
- Unity YAML merges use UnityYAMLMerge (local git config, see `.gitattributes`).
- One person edits a scene at a time. Build content in prefabs; scenes only compose them.
