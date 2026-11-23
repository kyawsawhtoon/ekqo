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

## Quest 1 on tablets

Quest 1 ("A Light in the Garden") is the first playable slice: Ekqo in the night garden,
listen-first teaching (hear, tap the picture, say it with Ekqo), the say steps with tap
answers, the end-of-quest review, the word book and the glowing jar. It runs on the
quest engine and the Burmese content bundle. The code is in `Assets/Ekqo/Game/Quest`.

1. Open `game/` in Unity and wait for it to finish compiling. The Console should have no red lines.
2. To try it in the editor: **Ekqo → Play Quest 1 here** (press Play again to stop). The
   editor has no voice, and the child's name is "Su".
3. For tablets: **Ekqo → Build Quest 1 for tablets**. It writes the web build to `site/quest1/`.
4. Commit and push it, the same way as the text test.
5. A few minutes later, open https://kyawsawhtoon.github.io/ekqo/quest1/ on the tablet.
   The bottom-left corner shows the build time, so you can tell a new build from a cached one.

**Answers are taps for now.** Each say step shows the right phrase and one other phrase.
The child says it out loud and taps it. Speech recognition plugs in here later (architecture 4.2).

**Voice.** Every line, word and answer is pre-recorded with Azure's Burmese neural voices
(the voices in `content/editions/my/edition.yaml`). Run **Actions → Render voice → Run workflow**
on GitHub after content changes; it records only new or changed lines into
`Assets/StreamingAssets/voice/my/` and commits them. Then pull and rebuild Quest 1. It needs the
repository secrets `AZURE_SPEECH_KEY` and `AZURE_SPEECH_REGION` (your Azure Speech resource →
Keys and Endpoint). To record on your own computer instead: `pnpm content:build`, then
`AZURE_SPEECH_KEY=... AZURE_SPEECH_REGION=... pnpm voice:render`. Lines without a clip fall
back to the tablet's own Burmese voice, if it has one. Sentences with the child's name are
shown without sound. When "English help" is on, the English hints are read with the tablet's
English voice.

The "English help" button in the top corner shows or hides the English hints for grown-ups.
Progress is saved in the browser at the end of the quest. "Start over as a new player" on
the last screen clears it.

## Layout

| Folder | Assembly | Workstream | Notes |
| --- | --- | --- | --- |
| `Assets/Ekqo/Core` | `Ekqo.Core` | 4 | Quest engine. Pure C# (`noEngineReferences`), tests in `Core/Tests` |
| `Assets/Ekqo/Content` | `Ekqo.Content` | 6 | Loads `bundle.json` into the Core models |
| `Assets/Ekqo/Speech` | `Ekqo.Speech` | 3 | `ISpeechService` and the web bridge |
| `Assets/Ekqo/Game` | `Ekqo.Game` | 1 | Bootstrap, UI Toolkit styles, the week-1 text spike, Quest 1 (`Quest/`) |
| `Assets/Plugins/WebGL` | | 3 | `EkqoSpeech.jslib` (Unity ↔ browser) |
| `Assets/WebGLTemplates/Ekqo` | | 1, 3 | Page template with tap-to-start and the Azure Speech JS SDK |
| `Assets/WebGLTemplates/EkqoQuest` | | 1 | Quest 1 page: the child's nickname, tap-to-start, loading progress |

## Content

Run `pnpm content:build` at the repo root and copy `build/content/my/bundle.json` to
`Assets/StreamingAssets/content/my/bundle.json`.

## Tests

Window → General → Test Runner → EditMode → Run All (the `Ekqo.Core.Tests` assembly: the quest engine and `LineText`, which fills lines, patterns and tap answers from the content).
