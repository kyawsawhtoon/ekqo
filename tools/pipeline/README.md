# Content pipeline (workstream 6)

**Voice (done):** `src/voice.ts` pre-renders every line, word and answer in the content with
Azure neural voices into `game/Assets/StreamingAssets/voice/<edition>/`. Run it with the
**Render voice** GitHub Action, or locally:

```sh
pnpm content:build
AZURE_SPEECH_KEY=... AZURE_SPEECH_REGION=... pnpm voice:render
```

Clips are named by the speaking character and a hash of the text (the same rule as
`VoiceService.ClipKey` in the game), so only new or changed lines are recorded.

To come: drafting missing lines and variants with Claude Opus 5.5, and the native-speaker
review sheet (including live line candidates from play). See
`docs/Ekqo Technical Architecture.md`, sections 4.4 and 8.
