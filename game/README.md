# Ekqo game (Unity)

Unity 6 LTS, 2D URP, Web build. See `docs/Ekqo Technical Architecture.md`, sections 3, 4 and 9.

## First-time setup

The repo holds the game code under `Assets/Ekqo`, the speech plugin and the WebGL
template. Unity generates the rest the first time the project is opened:

1. In Unity Hub, create a new project from the **Universal 2D** template with the
   current **Unity 6 LTS** editor, in a temporary folder.
2. Copy its `ProjectSettings/` and `Packages/` folders into this `game/` folder.
3. Add these packages (Window → Package Manager, or `Packages/manifest.json`):
   - `com.unity.addressables`
   - `com.unity.nuget.newtonsoft-json`
   - `com.unity.test-framework` (usually already there)
4. Open `game/` in Unity Hub. Commit `ProjectSettings/`, `Packages/manifest.json`,
   `Packages/packages-lock.json` and the generated `.meta` files.
5. Player Settings → Web: WebGL template **Ekqo**, compression **Brotli**,
   managed stripping level **High**.
6. Make a dynamic font asset from Noto Sans Myanmar at
   `Assets/Ekqo/Game/Fonts/NotoSansMyanmar-Regular.asset` (Window → Text → Font Asset Creator, atlas population mode **Dynamic**).

## Layout

| Folder | Assembly | Workstream | Notes |
| --- | --- | --- | --- |
| `Assets/Ekqo/Core` | `Ekqo.Core` | 4 | Quest engine. Pure C# (`noEngineReferences`), tests in `Core/Tests` |
| `Assets/Ekqo/Content` | `Ekqo.Content` | 6 | Loads `bundle.json` into the Core models |
| `Assets/Ekqo/Speech` | `Ekqo.Speech` | 3 | `ISpeechService` and the web bridge |
| `Assets/Ekqo/Game` | `Ekqo.Game` | 1 | Bootstrap, UI Toolkit styles, the week-1 text spike |
| `Assets/Plugins/WebGL` | | 3 | `EkqoSpeech.jslib` (Unity ↔ browser) |
| `Assets/WebGLTemplates/Ekqo` | | 1, 3 | Page template with tap-to-start and the Azure Speech JS SDK |

## Content

Run `pnpm content:build` at the repo root and copy `build/content/my/bundle.json` to
`Assets/StreamingAssets/content/my/bundle.json`.

## Tests

Window → General → Test Runner → EditMode → Run All (the `Ekqo.Core.Tests` assembly).
