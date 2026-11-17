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

## Burmese text test on tablets

The week-1 text test (`Assets/Ekqo/Game/TextSpike.cs`) lists every Burmese word and line
in Noto Sans Myanmar so a native speaker can check the shaping on iPad and Android.

1. Unity Hub → Installs → Unity 6 → ⚙ → Add modules → **Web Build Support** (once).
2. Open `game/` in Unity, then **Ekqo → Build Burmese text test**. The first build takes
   10 to 20 minutes. It writes the web build to `site/text-test/` at the repo root.
3. Commit and push it:

   ```sh
   git add -A && git commit -m "Build Burmese text test" && git push
   ```

4. About two minutes later, open https://kyawsawhtoon.github.io/ekqo/ on each tablet.

The **Pages** workflow publishes `site/` whenever it changes on `main`. GitHub Pages must
be set to **Source: GitHub Actions** once, in the repo's Settings → Pages. The build
uses gzip with in-page unpacking because Pages cannot serve Brotli; the project's own
Web settings are put back after the build. `Ekqo → Open Burmese text test scene` opens
the scene to try it with Play.

The test uses the committed `Assets/StreamingAssets/content/my/bundle.json`. After
content changes, run `pnpm content:build` and copy the new bundle there (see Content).

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
