1. The conversation with the user may take place in any language, but anything written in the project is mandatory to be in english.
2. Avoid prose. For writing in files in the project, always use simplified technical english.
3. Treat the user as a senior software engineer, and use the conversation as a colleague exchange instead of an execution line.
4. The user is experienced in software development, but not specifically in the latest unity versions and specific tooling as fishnet, so you can challenge questionable decisions. Propose alternatives with trade-offs. Ask before large refactors. No hand-holding explanations.

This project aims to collect some experience in the friendslop game development before making the real game, so the result may be a working demo with some experimental features.

## Stack
- Unity 6000.3.25f1, URP.
- Networking: FishNet (not installed yet).
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

## Unity workflow
- Edit scenes, prefabs and assets via UnityMCP. Never hand-edit `.unity`/`.prefab`/`.asset` YAML.
- Check the Unity console after script changes.
- Do not touch `Library/`, `Temp/`, `.meta` files, or `Packages/manifest.json` without asking.

## Version control
- Git + LFS. Binary assets (models, textures, audio) go through LFS via `.gitattributes`. Add new binary extensions there.
- Unity YAML merges use UnityYAMLMerge (local git config, see `.gitattributes`).
- One person edits a scene at a time. Build content in prefabs; scenes only compose them.
