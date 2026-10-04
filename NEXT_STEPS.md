# Next steps

Session handoff. Read at session start, update at session end (5 minutes max, facts only).
Last updated: 2026-10-05.

## Current state
- Active work: co-op factory automation prototype. Module `Assets/GameAssets/Features/FactoryPrototype/`, scene `Scenes/FactoryPrototype.unity`.
- Target feel: Alchemy Factory (first person, grid building, belts, machines, sell products), co-op.
- Friendslop sandbox (`Scenes/Bootstrap.unity`, `Characters/TestPlayer`) is idle. Kept as FishNet reference.
- 66 EditMode tests green. All work committed and pushed to `origin/main`.

## Done (factory prototype)
- Deterministic sim, pure C#: grid, ports, belts with back-pressure, crafters, sources, sell point, snapshot + checksum.
- Server-driven lockstep over FishNet: tick batches, late-join snapshot, checksum mismatch -> resync. Verified with MPPM (host + 1 client).
- Build UX:
  - Modes None (default) / Build (1-6) / Dismantle (F). RMB steps back, then exits the mode.
  - Belts: two clicks, L path preview, port snapping, R flips corner or turns the last tile.
  - Dismantle like Satisfactory: click marks, Ctrl sweeps, hold LMB 0.5 s dismantles.
  - MMB samples. Pending ghosts while the server confirms. Rejection reason under the crosshair.
- Machine status lamps, arrow meshes for direction and ports, inspector panel (E) with max and measured rate.
- Save/load on host (F6/F7): sim + player poses keyed by FishNet ClientId.

## Next (priority order)
1. Minimal game loop: resource nodes on the map (sources not buildable), buildings cost coins, 3-4 commissions as goals.
2. Build UX polish: hold-drag belts, undo, build menu (Q) with categories.
3. Logistics: splitter, merger, storage. Later: multi-level, pipes.
4. Move content (items, recipes, buildings) to ScriptableObjects.
5. Steam transport (FishySteamworks) + lobby. Player identity = Steam ID.

## Known issues / limits
- Player save key = ClientId: not stable across sessions (join order decides).
- No real prediction: own placements show as pending ghosts for ~100-250 ms.
- Full snapshot + full-state checksum every 20 ticks: cost grows with factory size.
- Belt item spacing at tile seams depends on build order (deterministic, cosmetic).
- Ghost (transparent) and lamp (emission) materials are configured at runtime: variants may be stripped in builds. Editor-only for now.
- Mouse/keyboard interactions cannot be simulated through UnityMCP: verify sim/network state via `execute_code`, UX by hand.

## How to verify
See `.claude/skills/unity-verify/SKILL.md`.
