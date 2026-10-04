# Decisions

Decisions with context, to avoid re-discussing them. Update when a decision is made or reopened.

## Resolved

### Networking stack (2026-09-26)
- FishNet 4.7.3 via UPM git URL pinned to a tag. Keeps `Assets/` clean, upgrade = change tag.
- Transports inside a Multipass, one chosen at start (Tugboat now, FishySteamworks later). FishNet's NetworkManager initializes first (`DefaultExecutionOrder(short.MinValue)`), so the transport cannot be swapped before it; the server starts only the selected transport.
- Steam P2P cannot be tested with two instances on one PC (same SteamID). Gameplay is tested on Tugboat; only lobby/connect is Steam-specific.
- Local multiplayer testing: Multiplayer Play Mode (built into Unity 6.3).

### Game direction (2026-10-04)
- Prototype a co-op automation game in the style of Alchemy Factory (3-person studio, 200k+ copies).
- Why: systems-heavy and asset-light (machines and belts are static props), loyal genre audience, fits a solo engineer without artist.
- Parked ideas: zombie/skeleton swappable body parts (good hook, gameplay unclear, combat/animation heavy), co-op dungeon puzzles, loot -> prepare -> boss loop, shop simulators (crowded), stone-skipping incremental (viral lottery).

### Factory prototype architecture (2026-10-04)
- Co-op from day one (user choice).
- Netcode: server-driven deterministic lockstep with commands (Factorio model). Bandwidth independent of item count; lockstep testable without network. Rejected: replicating belt state (scales with items).
- Sim in a pure C# assembly (`noEngineReferences`), integer state, 20 Hz fixed tick, own clock (not FishNet `TimeManager`, whose client tick is an estimate).
- Desync handling: checksum every 20 ticks, resync by full snapshot.
- Tile-based belts on a grid, not splines. Matches the sim and determinism. Satisfactory is the reference for interaction only.
- Prototype content defined in code; ScriptableObjects once the loop is validated.
- Greybox visuals generated from primitives at runtime to minimize prefab/scene authoring.

### Build UX (2026-10-04/05)
- Modes like Satisfactory: None by default, 1-6 Build, F Dismantle, RMB steps back then exits.
- Belts: two clicks with L path preview. End snapping: straight ahead first, then the only machine input; side belts never snap. R flips corner or turns the last tile.
- Dismantle: click marks, Ctrl sweeps, hold 0.5 s to dismantle (no instant delete).
- Own placements: visual pending ghosts only, no sim prediction.
- Saves: host only. Player poses keyed by FishNet ClientId for now.

### Workflow
- Claude makes small atomic commits and pushes verified work.
- Project files in simplified technical English. Context lives in `AGENTS.md`, `NEXT_STEPS.md`, `ARCHITECTURE.md`, `DECISIONS.md`.

## Open
- Minimal game loop: resource nodes vs buildable sources, building costs, commissions/orders, progression.
- Player identity in saves: Steam ID once Steam is in.
- Belt UX: keep two-click or add hold-drag.
- Scale: incremental checksums, snapshot compression, belt chains.
- Art direction: Kenney / Quaternius / KayKit kit choice, shared palette + post-process to unify.
- Single level for now; multi-level building and pipes later.
