---
name: unity-verify
description: Verify changes in this Unity project through UnityMCP - compile, run EditMode tests, play with Multiplayer Play Mode, inspect host and client state, screenshot. Use after any code, prefab or scene change in Frienslop_demo.
---

# Verify a change in Frienslop_demo

## 1. Compile
- `refresh_unity` with `compile: request`, `mode: force`.
- `read_console` types error + warning. Must be empty (MCP-FOR-UNITY port messages are fine).
- If Play Mode is active (`mcpforunity://editor/state` -> `play_mode.is_playing`), the user may be testing: do not stop it, ask or wait.

## 2. Tests
- `run_tests` EditMode, assembly `Friendslop.Features.FactoryPrototype.Tests`, `include_failed_tests: true`.
- `get_test_job` with `wait_timeout: 90`. Tests cannot run in Play Mode.

## 3. Play test (host + client)
- Scene `Assets/GameAssets/Scenes/FactoryPrototype.unity`. MPPM Player 2 is usually active.
- `manage_editor play`, then `sleep 10` (domain reload; MPPM clone starts).
- Two UnityMCP instances appear: main `Frienslop_demo@...` and clone `mppm...@...` (see `mcpforunity://instances`). Pin one with `set_active_instance` before each batch of calls.
- Host: `execute_code` with `FactoryNetwork.Instance` (`ActiveSim`, `RequestDemoLayout`, `RequestPlace/Remove`, `SaveToDisk/LoadFromDisk`), `FishNet.InstanceFinder.ServerManager.Clients`.
- Client: on the clone, `FactoryNetwork.Instance.Replica` (`Sim.Tick`, `ChecksumsOk`, `Mismatches`, `Resyncs`, `NeedsResync`).
- RPCs and batches take a tick or a round trip: read state in a separate call.
- `execute_code` uses CodeDom (C# 6): no `??=`, no tuples, qualify `UnityEngine.Object`.
- Screenshots: `manage_camera screenshot` with `view_position`/`view_target`, `output_folder: Temp/Captures` (never `Assets/`).
- `manage_editor stop`, then `read_console` again.

## 4. Scene and prefab edits
- Only through UnityMCP (`execute_code` with `PrefabUtility`/`EditorSceneManager`). Never edit YAML.
- After saving a scene with NetworkObjects: reopen it and check `NetworkObject.SceneId != 0` (reflection, field `SceneId`). If 0, call internal static `NetworkObject.CreateSceneId(scene, true, out changed)` by reflection and save.

## 5. Commit
- Small atomic commits by topic, message ends with the Co-Authored-By line, then `git push`.
- Update `NEXT_STEPS.md` at the end of the session.
