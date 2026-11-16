# Ekqo Technical Architecture (MVP)

Version 0.3 (draft for Kyaw's review)

November 2026

This document sets the technical foundation for the Ekqo MVP: the stack, how the code is split between the six workstreams, the APIs between the parts, the data model, and the approach to child safety and privacy. It follows the BRD v1.4, the storyboard v1.0 and the Burmese edition v1.0. The content format is specified separately in `Ekqo Content Format.md`, and the converted Burmese content is in `content/`.

**What changed in v0.2:** the game client is built in **Unity** instead of Phaser 3, as Kyaw asked, and the MVP stays a **web app** (a Unity Web build for tablet and desktop browsers). Sections 2, 3, 4, 9 and 10 change. Section 9 covers Burmese text in Unity, web performance on tablets, and the path to native apps later.

**What changed in v0.3:** live AI lines now feed back into the reviewed line bank (S6, sections 4.3, 6, 7.1 and 8). Kyaw approved this on 15 November 2026.

Items marked **[Decide]** need Kyaw's confirmation. Each has a recommended default, listed together in section 2.

## 1. Architecture at a Glance

```
 Tablet / desktop browser                               Cloud (one region)
 ┌───────────────────────────────────────┐            ┌──────────────────────────────────┐
 │ Unity Web build (game/)               │  HTTPS     │ API (services/api, Node+Fastify) │
 │  2D URP: city, scenes, Ekqo           │◄──────────►│  auth + parental consent         │
 │  UI Toolkit: all text and UI          │            │  profiles, saves, progress       │
 │  Ekqo.Core: quest engine (pure C#)    │            │  speech token broker             │
 │  Addressables: content, art, audio    │            │  conversation service ──────────►│ Anthropic API
 │  Speech bridge (.jslib → Azure JS SDK)┼──audio────►│   (answer matcher + AI judge)    │  (Claude Haiku 5.5)
 └───────────────────────────────────────┘  stream    │  Postgres                        │
              │  direct, short-lived token     ▲      └──────────────────────────────────┘
              ▼                                │
        Azure AI Speech (my-MM) ───────────────┘ (no audio stored)
        CDN: Unity build, content JSON, art bundles, pre-rendered voice lines
```

The main ideas:

1. **The quest engine is deterministic and has no Unity dependencies.** It is a plain C# library that reads compiled content and decides everything about quest flow: what step comes next, when to teach, when to review, when Ekqo grows. The AI never decides quest flow; it only phrases lines and judges meaning, which is how the BRD's "the AI never goes off-script" is enforced by design rather than by prompt.
2. **Most answers never reach the AI.** The server's matcher checks the transcript against the known-good answers first. Claude is called only when the matcher is unsure, which keeps answers fast, cheap and predictable.
3. **Language lives in content, not code.** The engine, game and services know nothing about Burmese. A new edition is a new folder under `content/editions/` plus its voices and font.
4. **Children's data stays minimal.** No audio is kept, transcripts are never stored or logged, the child has a nickname and avatar only, and the parent owns the account.

## 2. Stack Choices (recommended defaults)

| # | Choice | Recommended default | Why | Main alternative |
| --- | --- | --- | --- | --- |
| S1 | Game engine | **Unity 6** (the current LTS release, pinned at scaffold), **2D URP**, **Web build** (WebGL 2) | Kyaw's choice. Strong 2D tooling and animation for Ekqo, a large talent pool, and the same project can later build native iPad and Android apps. | (Phaser 3, dropped in v0.2) |
| S2 | Text and UI | **UI Toolkit with the Advanced Text Generator** for every piece of text; no TextMeshPro for Burmese | TextMeshPro doesn't shape Myanmar script. UI Toolkit's Advanced Text Generator uses HarfBuzz and ICU, which do. Needs the week-1 check in section 9.1. | HTML text overlay on top of the Unity canvas (web-only fallback) |
| S3 | Code and repo | **One monorepo** (`kyawsawhtoon/ekqo`): C# for the game, TypeScript for the content tools and the API | The content schema stays in TypeScript and generates C# types, so the authoring pipeline and the game read the same shape. | C# backend (ASP.NET) to use one language everywhere |
| S4 | Backend | **Node.js + Fastify**, one modular service; **Postgres** with Drizzle ORM | One service is enough for family testing; modules map to workstreams and can split later. | Serverless functions plus a hosted database (Supabase or Firebase) |
| S5 | Cloud | **Azure** (Container Apps, Database for PostgreSQL, Blob Storage + Front Door CDN), in the **same region as Azure AI Speech** | Speech is already on Azure, so one cloud, one data processing agreement and one region for children's data. | Vercel + Neon or Supabase |
| S6 | NPC lines | **Reviewed line bank first, live generation second.** Scripted lines plus 3–5 variants per line are drafted offline by Claude Opus 5.5 and reviewed by a native speaker. Live Claude Haiku 5.5 generation is used for open conversation steps (Q10, Q12), restricted to the quest's word list. **Live lines feed back into the bank:** they are saved without any of the child's words, a native speaker reviews them, and approved ones join the bank with pre-rendered voice, so the game needs live generation less over time (decided). | Burmese generated live and unreviewed is the biggest quality risk for a learning product, and the feedback loop turns live generation into a source of new reviewed lines. | Live generation for every NPC line |
| S7 | Voice for scripted lines | **Pre-render all scripted and bank lines** with Azure neural voices at build time, served from the CDN; live text-to-speech only for live lines | Instant playback, near-zero cost, and the reviewer can listen before release. | Live text-to-speech for everything |
| S8 | Parent consent (COPPA) | **Family testing:** a signed consent form from each test family plus email verification. **Public launch:** a verifiable parental consent method chosen with legal review | Testing families are known people. The public method depends on the data flows in section 7. | A consent vendor from day one |
| S9 | Romanization | **Simple informal phonetic** spelling (like "Phwa Phwa"), drafted offline by Claude and checked by the native reviewer | Matches how families already write Burmese names in English letters. | MLCTS |

Smaller defaults that need no decision: Unity Addressables for loading content and art per location, the Unity Test Framework (NUnit) for the engine, GameCI on GitHub Actions for Unity builds (needs a Unity license secret), Vitest for the TypeScript parts, and Sentry with personal data scrubbing for error reports. Unity Personal is free below the revenue and funding threshold and Unity 6 has no runtime fee.

## 3. Workstreams and Module Boundaries

```
ekqo/
├─ content/                       # authored content (YAML): core/ and editions/<lang>/
├─ game/                          # Unity 6 project
│  └─ Assets/Ekqo/
│     ├─ Core/                    # WS4 quest engine, progression, review, care (pure C#, no UnityEngine)
│     ├─ Content/                 # WS6 generated C# types + JSON loader
│     ├─ Game/                    # WS1 scenes, city, Ekqo, UI Toolkit screens, word book, parent area
│     ├─ Speech/                  # WS3 ISpeechService + web bridge (Plugins/WebGL/speech.jslib)
│     └─ Convo/                   # WS2 client for the conversation API, line bank picker
├─ packages/
│  └─ content/                    # WS6 schema (zod), validator, compiler to JSON, C# type generation
├─ services/
│  └─ api/                        # WS5 (+WS2 server side) Fastify service and Postgres
└─ tools/
   └─ pipeline/                   # WS6 drafting with Claude, TTS pre-render, reviewer export/import
```

| Workstream | Owns | Depends on | Exposes |
| --- | --- | --- | --- |
| 1. Game client and city | `game/Assets/Ekqo/Game` | Core, Speech, Convo, content bundle | The playable game; scenes and UI |
| 2. Ekqo and NPC AI conversations | `game/Assets/Ekqo/Convo`, `services/api/src/convo` | content (word lists, personas) | `/v1/convo/judge`, `/v1/convo/line`, the line picker |
| 3. Speech | `game/Assets/Ekqo/Speech`, `services/api/src/speech` | Azure AI Speech JavaScript SDK | `ISpeechService.Listen()`, `Speak()`, `/v1/speech/token` |
| 4. Quests and progression | `game/Assets/Ekqo/Core` | generated content types | `QuestEngine`, `Dispatch()`, events, save snapshot |
| 5. Backend and privacy | `services/api` | Postgres, Azure | REST API in section 5; data model in section 6 |
| 6. Content pipeline | `content/`, `packages/content`, `tools/pipeline` | Claude (offline), Azure TTS | Validated JSON, C# types, audio and Addressables on the CDN |

**One content shape for both languages.** The zod schema in `packages/content` is the source of truth. The build exports it to JSON Schema and generates C# classes into `game/Assets/Ekqo/Content`, so a schema change that breaks the game fails CI.

**Build order.** The content kit is done as version 0 alongside this document. Week 1 is the Unity web spike in section 9.4. Then the quest engine and the game skeleton start in parallel, with Q1 as the vertical slice played with tap answers. Speech and conversations plug into the engine's `say` steps after that. The backend starts with auth, consent and saves; the game can save to browser storage until then.

## 4. How a Quest Runs

### 4.1 The quest engine (`Ekqo.Core`)

The engine is a pure C# state machine with no rendering, audio or network code, so it runs in NUnit tests outside the editor and behaves the same in the web build and any future native build.

```csharp
var engine = new QuestEngine(bundle, save, clock);   // clock counts active play time only
engine.Dispatch(new StartQuest("q01"));
engine.Dispatch(new Answer("say_hello", result));     // result from tap, or from the judge
engine.Events += e => presenter.Show(e);              // ShowLine, Teach, Ask, CareCue, Grow, Reward...
SaveSnapshot snapshot = engine.Save();                // stored locally and synced
```

It owns:

- **Step flow** for the step types in the content format (scene, line, teach, say, use, care, discover, stage, review, reward, review_round, conversation).
- **Listen-first teaching.** A `teach` step expands into the five steps from storyboard 11.1 (hear, show, echo, then the quest's own say and use steps), built from the edition's word list, the lexicon pictures and audio. It skips show and echo for words the child already said correctly first time.
- **Word progress.** Each concept counts how often it was heard, said and tapped, and in which situations (quest step, care action, review). A word is learned after three different situations.
- **Planned review.** The end-of-quest review picks 3–4 words from this quest and 1–2 earlier ones, favouring words close to being learned. "Ekqo forgets on purpose" picks a word due for review.
- **Care simulation.** Needs rise per minute of active play only, using `content/core/care.yaml`. Time while the game is closed is ignored.
- **Progression.** Ekqo stages, abilities, location unlocks, items, sparks and discoveries.

### 4.2 A speaking turn, end to end

1. The engine emits `Ask` for a `say` step. The game plays the prompt line (pre-rendered audio) and shows the mic button and the tap options.
2. The child taps the mic. Unity's `Microphone` API isn't available in web builds, so the game calls a small JavaScript plugin (`speech.jslib`) that runs the **Azure Speech JavaScript SDK** in the browser, with a 10-minute token from `/v1/speech/token` (the Azure key never reaches the browser). Audio streams to Azure and is not stored. The browser handles the microphone permission.
3. Azure returns the transcript and its alternatives (n-best) to the plugin, which passes them back to C#.
4. The game sends the transcript and the step id to `/v1/convo/judge`. The **server matcher** normalises the text (Unicode NFC, no punctuation or spaces, since Burmese has no word spaces) and compares it with the step's accepted answers, the pattern expanded with every allowed slot word, and the lexicon forms, allowing a small syllable-level edit distance. A clear match returns at once, with no AI call. The matcher lives on the server, in TypeScript next to the content tools, so it has one implementation; speech needs the network anyway, so this costs only one short round trip.
5. **AI judge**, only when the matcher is unsure. The server builds the prompt from content (step intent, expected concepts with their words, the character, the register) and calls Claude Haiku 5.5 with structured output. The verdict is `correct`, `partial`, `other_language`, `off_topic` or `unclear`, plus the concepts it matched.
6. The engine turns the verdict into feedback from storyboard 11.6: praise the attempt, and on a wrong or partial answer Ekqo recasts the right phrase ("Let's say it together"). There is never a failure screen.
7. **Tap fallback.** If the mic is unavailable, speech fails twice, or the child taps, the tap answer counts as a "tapped" attempt and needs no server call. Quests never block on speech.

**Latency target:** under 1 second from the end of speech to Ekqo's reaction when the matcher decides, under 2 seconds when the judge is called. While waiting, Ekqo shows a "thinking" animation.

### 4.3 NPC lines

The engine asks for a line by step id. The game picks, in order:

1. a **reviewed variant** from the line bank, at random, avoiding repeats;
2. for steps marked for live generation, a **live line** from `/v1/convo/line`, played with live text-to-speech;
3. the **scripted line**, always available.

Live lines are restricted to known words and checked before use (section 7.2). Any failure, timeout (800 ms) or refusal falls back silently to the scripted line.

### 4.4 Feeding live lines back into the bank

Every live line that passes the checks in section 7.2 is saved as a **line candidate**, so the bank grows from real play:

1. **What is saved.** Only the generated line and its content context: edition, quest, step, speaker, the line's text and word list, the support-language hint, the model and prompt version, and when it was made. **Nothing the child said or typed is saved**, no transcript, no recent turns, and no child, profile or account id. The nickname placeholder stays a placeholder. Because a live line may only use words from the edition's word list (section 7.2), it cannot carry the child's words or personal details.
2. **Deduplication.** Candidates are keyed by a hash of edition, step and normalised text. A repeat only increases its use count, so reviewers see each line once and can sort by how often it came up.
3. **Review queue.** Candidates go to the native-speaker review sheet (section 8) next to the drafted lines, grouped by quest and step, with an audio preview. The reviewer approves, edits or rejects each one. Rejected candidates are kept as rejected so the same line isn't queued again, and are deleted after 90 days.
4. **Promotion.** An approved candidate becomes a reviewed variant in the edition's quest file (`lines.<step>.variants`, with `source: live`), is pre-rendered with the character's voice, and ships in the next content release.
5. **Shrinking live use.** When a step has enough approved variants (default 8, set per step), the game stops asking for live lines on it and plays from the bank. The share of lines served live, from the bank and from the scripted fallback is tracked per step, so the team can watch live generation fall over time.

Steps that are open by nature (Q12's five-turn conversation) keep a live path, but most of their turns are served from the bank as it grows.

## 5. APIs

All endpoints are JSON over HTTPS under `/v1`, authenticated with the parent's session. Children never have their own credentials. The API publishes an OpenAPI description, and the C# client in `game/Assets/Ekqo/Convo` is generated from it.

| Endpoint | Purpose | Notes |
| --- | --- | --- |
| `POST /auth/magic-link`, `POST /auth/verify` | Parent sign-in by email link | No passwords to manage |
| `POST /consent` | Record parental consent | Method, notice version, time |
| `GET/POST/PATCH /children` | Child profiles | Nickname, avatar presets, edition, display settings |
| `GET /children/:id/save`, `PUT /children/:id/save` | Engine snapshot | Version number; last write wins, with a merge for word progress |
| `POST /children/:id/events` | Batched learning events | Outcomes only (step, mode, verdict, time); no transcripts |
| `GET /parent/summary/:childId` | Parent summary | Quests completed, words practised and learned, time played, "I can" goals |
| `POST /speech/token` | Short-lived Azure token | Rate limited per child |
| `POST /convo/judge` | Matcher, then meaning check | Request: `{childId, edition, quest, step, transcript, nbest[], attempt}`. Response: `{verdict, matched[], source: matcher or ai, recastKey?}` |
| `POST /convo/line` | Live NPC line | Request: `{childId, edition, quest, step, recentTurns[]}`. Response: `{text, words[], support, audioUrl?, source}` |
| `DELETE /children/:id`, `DELETE /account` | Deletion | Removes all data for the child or the family |

The parent area (sign-in, consent, settings, summary) is a few plain web pages served next to the game, not Unity screens. Forms, email links and accessibility are simpler in HTML, and the parent area stays light to load.

Content is not served by the API. The pipeline publishes versioned static files to the CDN: Addressables catalogs and bundles per location, JSON per quest and edition, and audio files named by line hash.

## 6. Data Model

Postgres, one database. Children's records hold no real name, birth date, photo, location or contact details.

| Table | Key fields | Notes |
| --- | --- | --- |
| `parent_account` | id, email, email_verified_at, country, created_at | The only contact detail in the system |
| `consent` | parent_id, method, notice_version, granted_at, revoked_at | One row per grant or revocation |
| `child_profile` | id, parent_id, nickname, avatar (json), edition, support_language, gender_option (optional), settings (romanization, English hints) | Nickname is free text set by the parent |
| `save_state` | child_id, version, snapshot (json), updated_at | The engine snapshot: current quest and step, Ekqo stage and needs, inventory, discoveries |
| `quest_progress` | child_id, quest_id, status, started_at, completed_at | Parent summary and the quest-completion KPI |
| `word_progress` | child_id, concept_id, heard, said, tapped, situations (text[]), first_try_correct, learned_at | Concept ids, not words, so it works for every edition |
| `learning_event` | child_id, at, quest_id, step_id, mode (speech, tap, echo), verdict, latency_ms | For KPIs; no transcript and no audio |
| `play_session` | child_id, started_at, active_seconds | Time played, retention KPIs |
| `line_candidate` | hash, edition, quest_id, step_id, speaker, text, words[], support, model, prompt_version, first_seen_at, use_count, review_status (queued, approved, edited, rejected), reviewed_at | Live AI lines for the review queue (section 4.4). No child id and no child input. |

**Retention:** save data and progress are kept while the account is active. Learning events are aggregated and the raw rows deleted after 90 days. Accounts inactive for 12 months are deleted after an email notice. A parent can delete a child or the whole account at any time, effective immediately.

## 7. Child Safety and Privacy

### 7.1 Privacy by design

- **The parent is the account holder.** Consent comes before any child profile exists (FR-11). The parent area sits behind a parent check (a simple sum or a PIN) so a child doesn't wander into it.
- **No audio storage.** Audio goes from the browser straight to Azure and is not stored by Ekqo. Azure's data logging stays off, and the resource sits in the same region as the database.
- **Transcripts are transient.** A transcript lives in memory for one judge request. It is never written to the database or logs, and error reports scrub request bodies.
- **Minimal data to the AI.** Judge and line requests contain the transcript, content ids and the step's words. The child's nickname is replaced with a placeholder before the request and put back in the game. No account or profile ids go to Anthropic. Request a zero-data-retention arrangement for the Anthropic organization before launch.
- **Live lines are saved without the child.** Live AI lines are kept for review (section 4.4), but they contain only words from the edition's word list and are stored with no link to any child, and never with what the child said.
- **No social features.** No chat, no free-text input, no photos, no leaderboards (BRD 6.3, FR-12).
- **No pressure mechanics.** No notifications to the child, needs freeze when the game is closed, no purchases.
- **No third-party analytics in the game.** Unity Analytics and ads services stay off; KPIs come from Ekqo's own `learning_event` and `play_session` tables.

**COPPA note.** Voice audio is personal information under COPPA. The FTC's 2017 policy statement allows collecting a child's voice without consent only when it replaces typed words and is deleted right away, but Ekqo also sends voice to Azure and transcripts to Anthropic as service providers, so the plan is to get consent in any case and keep both providers under data processing terms. The 2025 COPPA rule changes also require a written data retention policy and separate consent for any disclosure to third parties that isn't part of the service. A lawyer should review the notice, the consent method and the vendor terms before public launch. This section is an engineering plan, not legal advice.

### 7.2 AI safety layers

1. **Scripted structure.** The engine chooses every step; the AI only phrases lines and judges meaning.
2. **Closed input.** The only input that reaches the AI is a speech transcript for a specific step, capped in length. Children cannot type.
3. **Constrained prompts and structured output.** System prompts come from content (persona, register, the step's goal) and require a JSON response that matches a schema.
4. **Word-list check on live lines.** A live line must come back as a list of words, and every word must be in the allowed set: the edition's lexicon for this quest and earlier ones, the pattern templates, and a short reviewed list of function words. A line with any unknown word is discarded. This makes "child-safe and on-level" a check the code enforces, which a prompt alone can't guarantee.
5. **Output checks.** Myanmar Unicode only (no Zawgyi), a length limit, and a blocklist.
6. **Silent fallback.** Any error, refusal, timeout or failed check falls back to the scripted line or a gentle "Let's try again", never an error screen.
7. **Evaluation before release.** An offline test set of child-like answers per step (right, partial, English, silence, mumbling, off-topic, unkind words) with expected verdicts runs in CI whenever a prompt, model or content change lands.
8. **Logging without content.** Only verdicts, latency and fallback counts are logged.

### 7.3 Model use

| Use | Model | Where | Notes |
| --- | --- | --- | --- |
| Answer judging, live lines | Claude Haiku 5.5 (`claude-haiku-5-5`) | Live, server side | Fast and low cost, as the BRD specifies. The prompt prefix (persona, rules, step content) is cached. |
| Drafting lines, variants, romanization, test sets | Claude Opus 5.5 (`claude-opus-5-5`) | Offline in the pipeline | Every output goes to native-speaker review before release |

**Cost check:** a judge call is roughly 1,500 input tokens (mostly cached) and 100 output tokens, well under $0.001 per call at Haiku 5.5's list price, so even 100 judge calls in a session cost less than a cent. Speech recognition is the larger cost, and is still small at family-testing scale.

## 8. Content Pipeline

```
author YAML ──► validate (CI) ──► draft missing lines (Claude Opus 5.5, offline)
      ▲                                      │
      │                                      ▼
 import review ◄── reviewer sheet (text + audio preview) ◄── pre-render TTS (Azure)
                         ▲
                         └── live line candidates from play (section 4.4)
      │
      ▼
 compile ──► JSON + C# types + audio ──► Unity Addressables build ──► CDN (versioned)
```

- **Validate.** `pnpm content:validate` checks shape and cross-references and fails the build on errors. Warnings are the content backlog.
- **Draft.** For a step with no line, the pipeline asks Claude Opus 5.5 for a line and 3–5 variants using only the quest's words and patterns, marked `draft`.
- **Review.** A sheet per quest (text, romanization, English, audio preview) holds both drafted lines and live line candidates from play. The reviewer approves, edits or rejects each one, and the import sets `status: approved` (live candidates become variants with `source: live`). Only approved lines ship in a release build.
- **Pre-render.** Every approved line is rendered once with the character's voice settings and stored by content hash.
- **Signs and labels in the city** are localized sprites. The pipeline renders their Burmese text to images with HarfBuzz at build time, so world art never depends on runtime text shaping.

## 9. Unity on the Web: Burmese Text, Tablet Performance, Native Later

### 9.1 Burmese text shaping

Burmese needs complex shaping: vowel signs sit before or around consonants, consonants stack, and medials combine. A renderer that maps one character to one glyph shows broken text, which is what **TextMeshPro** does with Myanmar script, so Ekqo doesn't use TextMeshPro for any Burmese text.

**Default: UI Toolkit with the Advanced Text Generator.** Unity 6's UI Toolkit can switch a panel to the Advanced Text Generator (`-unity-text-generator: advanced;` in USS), which uses ICU and HarfBuzz to shape text. HarfBuzz has a dedicated Myanmar shaper. Requirements and limits from Unity's documentation:

- Only **dynamic font assets** work, so we bundle **Noto Sans Myanmar** as a dynamic font.
- Some rich-text tags aren't supported yet (`<sprite>`, `<size>`, `<font>`, `<space>`, `<mark>`). Ekqo's dialogue box uses separate elements for styling instead, and the word highlight is done with element backgrounds rather than `<mark>`.
- The ICU data adds about **4.8 MB** of memory.
- Unity's documentation names Arabic and Hebrew support but doesn't list Myanmar, and Unity's issue tracker has a crash report for Malayalam in one project. So Myanmar must be **proven on the target browsers in week 1** (section 9.4) before the UI is built on it.

All game text (dialogue, the word book, menus, care prompts, hints) is UI Toolkit. World-space labels are pre-rendered sprites (section 8).

**Fallback if the week-1 check fails: HTML text overlay.** The game asks a small JavaScript plugin to draw the dialogue text as HTML positioned over the Unity canvas, so the browser shapes Burmese exactly as it does on any web page. This works reliably on the web, and Unity keeps everything else. It would need replacing only if Ekqo later ships as a native app, by which time the Advanced Text Generator should have matured.

**Never Zawgyi.** Content validation rejects Zawgyi text, and every string is Myanmar Unicode.

### 9.2 Web performance on tablets

Unity's documentation lists iOS Safari 15+ and Chrome 58+ on Android as supported mobile browsers. A Unity Web build is heavier than a typical web game, so the MVP keeps a strict budget:

| Area | Budget and approach |
| --- | --- |
| First download | Under **20 MB** compressed to the title screen (engine + home location). Brotli compression, IL2CPP code stripping set to high, no unused Unity packages, Addressables so each location and its audio load on demand. |
| Load time | Under **15 seconds** on a first visit over a typical home connection, under 5 seconds on later visits from the browser cache. Ekqo animates on the loading screen. |
| Memory | Keep the Web memory heap under about **512 MB**. iPad Safari closes tabs that use too much memory, and that is the most likely way a Unity Web game crashes on a tablet. Sprite atlases, compressed mobile texture formats (ASTC) in the tablet build, and unloading a location when the child leaves it. |
| Frame rate | 60 fps on a 2021-or-newer iPad and a mid-range Android tablet; 2D URP with few lights and no post-processing. |
| Threads | Unity Web builds don't support managed C# threads. Engine, speech and network code is single-threaded and uses `Awaitable` and coroutines, as Unity recommends. |
| Audio | Unity's Web audio supports only basic features, which is enough for voice lines, music and effects. Browsers start audio only after a tap, so the title screen has a "Tap to start" with Ekqo. |
| Microphone | Requested only at the first speaking moment through the JavaScript speech plugin, with Ekqo explaining why. If permission is refused, the game runs on tap answers. |
| Orientation | Landscape, minimum 1024×768, touch first. Adding the game to the home screen (PWA template) gives full screen on iPad. |

### 9.3 Native apps later

The MVP is web only, as the BRD decides. Unity keeps the door open: the same project can build iPad and Android apps later. The parts that differ are small and sit behind interfaces from the start:

- **Speech:** `ISpeechService` has a web implementation (the JavaScript plugin) now, and can get a native one later using Microsoft's Speech SDK for Unity.
- **Text:** the Advanced Text Generator works the same in native builds; only the HTML-overlay fallback is web-only.
- **Storage and sign-in:** browser storage and email links now; native equivalents later.

### 9.4 Week-1 spike: prove Unity Web on tablets

Before the game UI is built, a throwaway Unity Web build checks the riskiest points on a real iPad (Safari) and an Android tablet (Chrome):

1. **Burmese text:** render all 139 lexicon entries and the Q1 lines with the Advanced Text Generator and compare them with the same text in a browser page. A native speaker confirms they look right.
2. **Size and load:** an empty 2D URP scene with UI Toolkit, the Myanmar font and ICU data, measured against the 20 MB and 15-second budgets.
3. **Memory:** the house scene with placeholder art at full resolution, watched in Safari's Web Inspector for 20 minutes of play.
4. **Speech:** the JavaScript plugin records, sends to Azure my-MM and returns a transcript, with the mic permission prompt on iPad.
5. **Audio:** voice lines and music play after the first tap, including after the tablet sleeps and wakes.

If text fails, switch to the HTML overlay (section 9.1). If size or memory fails, reduce the art budget or split the city into more Addressables groups. The result decides the UI approach before workstream 1 builds screens.

## 10. Open Items

1. **[Decide] S1–S9** in section 2 (S1 is decided: Unity, web build).
2. **Week-1 spike** (section 9.4), including a native speaker's check of the rendered Burmese.
3. **Content gaps** from the validator (`content/VALIDATION.md`): the word for Ekqo's sour fruit and a spicy dish, names for the two market sellers (whose traits are placeholders to confirm), Ekqo's name in Burmese script or "Ekqo", the parent's name forms, romanization for all words, and Burmese lines for 37 of the 45 spoken steps (Q1 is a draft example).
4. **Native-speaker reviewer** needed before the line drafting step (BRD 11).
5. **Early speech test** with a few Burmese-speaking children, since accuracy on children's voices is the biggest technical risk (BRD 11).
6. **Legal review** of consent, the privacy notice and vendor terms before public launch.
7. **Art pipeline:** sprite sheet format and Ekqo's animation tool (for example Unity's 2D Animation package or Spine), to agree with the artist.
