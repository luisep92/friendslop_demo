# Architecture

High-level design. Update when architecture changes. Conventions and rules live in `AGENTS.md`.

## Modules (`Assets/GameAssets/`)
| Module | Assembly | Content |
|---|---|---|
| `Core/` | `Friendslop.Core` | NetworkManager prefab (Multipass + Tugboat), `NetworkBootstrap` (transport + role at start), `NetworkConfig`, input actions asset. |
| `Characters/TestPlayer/` | `Friendslop.Characters.TestPlayer` | Friendslop sandbox player. Idle. |
| `Features/FactoryPrototype/Scripts/Simulation` | `...FactoryPrototype.Simulation` (`noEngineReferences`) | Deterministic sim, lockstep, save format, belt planner. No Unity. |
| `Features/FactoryPrototype/Scripts/Runtime` | `...FactoryPrototype` | FishNet glue, views, player, build tool, HUD, debug overlay. |
| `Features/FactoryPrototype/Tests/EditMode` | `...FactoryPrototype.Tests` | Sim, lockstep, planner, save tests. |

Factory prefabs: `FactoryNetwork` (scene NetworkObject), `FactoryPresentation` (views + HUD, not networked), `FactoryPlayer` (FP player).

## Factory simulation
- `FactorySim`: buildings in creation order (= update order). Dictionaries for lookup only (cell, id).
- `Advance(commands)`: apply `SimCommand`s (Place/Remove) -> `Tick++` -> `Step` every building -> `Ticked` event.
- Grid: `Int3` cells, level y = 0 only, bounds -32..31. `Footprint` maps local cells and ports by rotation (clockwise, matches Unity yaw). 1 cell = 1 m.
- Buildings:
  - `SourceBuilding`: 1 item every N ticks through its output port; holds it while blocked.
  - `BeltBuilding`: item list, progress 0..100 per tile, speed 10/tick, spacing 25 (also across seams). Input back/sides, output front.
  - `CrafterBuilding`: one recipe, per-ingredient input buffers (cap 4), output buffer, ports.
  - `SinkBuilding`: sell point, adds item value to `Coins`.
- Item hand-off: `TryPush(port)` -> neighbor `TryAccept(item, cell, entrySide)`. Refusal = back-pressure.
- Status (`BuildingStatus`) and `Throughput` counters are read-only views for lamps and the inspector.
- Persistence: `WriteSnapshot`/`ReadSnapshot` (versioned, commit only on full parse). `ComputeChecksum` = FNV-1a over the snapshot. `SaveFile` = header + extra section (player poses) + snapshot.
- Content: `PrototypeContent` (code) -> `ContentDb` (id lookup). All peers must use identical content.

## Netcode: server-driven deterministic lockstep
```
Client build input -> BuildTool -> FactoryNetwork.RequestPlace/Remove -> ServerRpc -> LockstepServer.Enqueue
Server Update: FixedStepClock (20 Hz) -> LockstepServer.Step() -> TickBatch bytes -> ObserversRpc (ExcludeServer)
Client: LockstepClient.OnBatch -> queue (target buffer 2, catch-up) -> Update -> Sim.Advance
        checksum every 20 ticks / tick gap -> NeedsResync -> ServerRequestSnapshot -> TargetSnapshot
Late join: OnSpawnServer -> TargetSnapshot (reliable ordered: snapshot before later batches)
Save load: LockstepServer.TryLoad -> TargetSnapshot to every client -> TargetTeleport to each player owner
```
- Host runs only the server sim (no replica). Client-only peers run a `LockstepClient` replica.
- `FactoryNetwork` only forwards bytes; lockstep logic is transport-agnostic and unit-tested.
- Players are FishNet NetworkObjects with client-authoritative `NetworkTransform`. Not part of the sim.

## Presentation (not networked)
- `FactoryView`: binds to `FactoryNetwork.Instance.ActiveSim`; one greybox GameObject per building id; status lamps; belt items via `Graphics.RenderMeshInstanced`, interpolated with the clock alpha.
- `BuildingVisuals` + `ArrowMesh`: primitives, arrows for direction and ports, ghost style (transparent).
- `BuildTool` (owner only): modes, screen-center raycast, `BeltPlanner`, `GhostSet`, `PendingPlacements`, inspector target.
- `BuildHud`: crosshair, rejection reason, inspector panel. `FactoryDebugOverlay`: debug panel and F-keys.
