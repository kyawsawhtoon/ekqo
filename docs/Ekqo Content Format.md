# Ekqo Content Format

Version 1 (draft for Kyaw's review)

November 2026

This document specifies how Ekqo content is written: quests, dialogue, vocabulary (words to say and words to hear), care actions and Ekqo discoveries. The format is language-agnostic, so a new language edition adds files without touching the story. The machine-checkable version is `content-kit/src/schema.ts`, and the MVP content in this format is in `content/`.

## 1. Two Layers: Core and Edition

The storyboard's first rule is that only the language changes between editions. The format follows it by splitting content in two.

| Layer | Folder | Holds | Written in |
| --- | --- | --- | --- |
| **Core** | `content/core/` | The world, characters, places, chapters, quests and their steps, care rules, discoveries, and the list of concepts and sentence patterns | English, as authoring intent ("meaning"), never shown to the child as-is |
| **Edition** | `content/editions/<lang>/` | The words for each concept, the template for each sentence pattern, each quest's say and hear lists, the lines, names, voices and register | The target language, with support-language hints |

The two layers meet through **ids**: concept ids (`food.rice`), pattern ids (`pat.where_is`), and step ids inside each quest (`say_hello`). An edition never adds story; core never contains target-language text.

```
content/
├─ core/
│  ├─ world.yaml          # Ekqo stages and abilities, characters, locations, chapters, tunes, items, learning rules
│  ├─ concepts.yaml       # concept registry and sentence patterns
│  ├─ care.yaml           # needs, moods, care actions and their speaking moments
│  ├─ discoveries.yaml    # About Ekqo pages and what triggers them
│  └─ quests/q01.yaml …   # one file per quest
└─ editions/
   └─ my/                 # Burmese
      ├─ edition.yaml     # locale, script, font, speech and voices, register, names
      ├─ lexicon.yaml     # concept -> word
      ├─ patterns.yaml    # pattern -> template
      └─ quests/q01.yaml … # say and hear lists, lines, accepted answers
```

## 2. Concepts

A **concept** is a meaning that every edition needs a word for: `greet.hello`, `food.rice`, `num.3`, `dir.left`. Progress is stored by concept id, so the same progress data works for any edition, and a child's word book can show the same picture in every language.

```yaml
- { id: food.rice,       gloss: "rice (cooked, a meal)", kind: noun, care: feed }
- { id: food.rice_grain, gloss: "rice (uncooked, to buy)", kind: noun }
```

- `id` is `category.name` and never changes after release.
- `kind` is the word type (noun, verb, adjective, phrase, number, color, direction, adverb).
- `care` marks words a care action can use, so feeding offers any food the child has met.
- Where one language has one word and another has two (Burmese ထမင်း and ဆန် for rice), core has two concepts and an edition may give both the same word.

**Edition-only words** that have no meaning on their own, such as Burmese counting words, are declared in the edition's lexicon with `local: true` and an id starting `x.<lang>.` (for example `x.my.classifier_item` for ခု).

## 3. Sentence Patterns

A **pattern** is a communicative function with slots, such as "Where is ___?". Core names the pattern and what can fill each slot. Each edition gives a template.

```yaml
# core/concepts.yaml
- { id: pat.please_give_n, meaning: "Please give me [number] [thing]", slots: { thing: "kind:noun", n: "prefix:num." } }

# editions/my/patterns.yaml
- { id: pat.please_give_n, template: "{thing} {n} {thing.classifier} ပေးပါ" }
```

**Slot selectors:** a concept id, `prefix:num.` (any concept with that prefix), `kind:noun care:feed` (by fields), `player_name`, or `any`.

**Template placeholders:** `{x}` is the slot's word, `{x.stem}` is the word without its statement ending (Burmese တယ်), and `{x.classifier}` is the counting word from the lexicon (default ခု). This is how grammar that differs by language, such as counting words or word order, stays in the edition and out of the story.

The engine expands a template with every allowed slot word to build the accepted answers and the tap options, so authors rarely list answers by hand.

## 4. Quests

One file per quest in `core/quests/`. The header comes from the storyboard's quest table; the steps are the quest's script.

```yaml
schema: ekqo.quest/1
id: q05
chapter: ch2
title: "Ekqo Goes Outside"
location: street                 # location, or location.area
characters: [ekqo, neighbor]
function: "Asking where places are; following directions"
topics: [place.street, place.shop, dir.left, …]   # the storyboard's word topics
patterns: [pat.where_is]
listening_weight: normal         # heavy for Q1-Q3: more hearing, short answers
reuses: [q01, q02, q04]          # storyboard review map
rewards: [map_street]
unlocks: [location:shop, location:bakery]
ekqo: { abilities: [say_it_slowly] }
steps: [ … ]
```

### 4.1 Step types

| Type | What happens | Key fields |
| --- | --- | --- |
| `scene` | A story moment shown with art and animation | `meaning` (direction for art and animation), optional `effects` |
| `line` | A character speaks | `speaker`, `meaning` (the English line), `concepts`, or `pattern` + `fill`, optional `speed` |
| `teach` | Runs listen-first teaching for these words: hear, show (tap), echo | `concepts`, `patterns` |
| `say` | A character asks, the child answers | `speaker`, `expects`, `ai` |
| `use` | The quest task needs the phrase to move on (still with tap fallback) | as `say`, plus optional `repeat` |
| `care` | A care action inside the story | `action`, `first_time`, `required` |
| `discover` | Adds an About Ekqo page | `discovery` |
| `stage` | Ekqo grows | `stage`, `full_shine` |
| `review` | The end-of-quest review, generated by the engine | none |
| `review_round` | Several characters each ask for one earlier skill (Q12) | `asks[]` with `speaker`, `from_quest`, `intent` |
| `conversation` | A short open conversation (Q12's five turns with Ekqo) | `speaker`, `turns`, `expects`, `ai` |
| `reward` | Items, abilities, sparks, friends | `items`, `abilities`, `sparks`, `friends` |

### 4.2 What an answer must mean: `expects`

`expects` is read by both the local matcher and the AI judge.

```yaml
expects:
  intent: "asks where the shop is"     # required: the meaning, in plain words
  pattern: pat.where_is                # the answer uses this pattern…
  slot_values: { x: place.shop }       # …with this word in the slot
```

Other fields: `concepts` (all must be there), `any_of` (at least one, ids or selectors), `patterns_any`, `from_task` (the answer must match the quest task, such as Grandma's shopping list), and `open: true` for free answers judged only by intent.

### 4.3 AI policy: `ai`

```yaml
ai: { npc_vary: true, judge: meaning }
```

- `judge: meaning` is the only mode, as the BRD requires (no pronunciation scores).
- `npc_vary: true` lets the character's prompt use a reviewed variant or, where enabled, a live line. `false` keeps the exact scripted line, used for very early or very important moments (Ekqo's first hello).

## 5. Edition Quest Files: Words to Say, Words to Hear, Lines

Each edition has one file per quest with the same id.

```yaml
schema: ekqo.edition-quest/1
edition: my
quest: q01
say:  [greet.hello, word.name, ans.yes, ans.no, greet.bye]
hear: [q.who, nature.light, sky.star, time.night, place.garden, phrase.dont_be_scared]
lines:
  who:        { text: "ဘယ်သူလဲ", status: draft }
  say_scared:
    prompt:  "ကြောက်လား"
    support: "Are you scared?"
    accept:  [ဟုတ်ကဲ့, မဟုတ်ဘူး, ကြောက်တယ်, မကြောက်ဘူး]
    status:  draft
```

- **`say`** (6–8 words) are the quest's goal. They are taught, asked for and tracked by the review.
- **`hear`** (about 6) appear in lines and pictures but aren't asked for yet. A word can be heard in one quest and said in a later one (ကြယ် "star" is heard in Q1 and said in Q11), but not both in the same quest.
- The say and hear lists live in the edition, not the core, because the right words for a function differ by language. The core `topics` list is the storyboard's guide.
- **`lines`** are keyed by step id:
  - `text` for `line` steps (what the character says),
  - `prompt` for `say` and `use` steps (what the character asks),
  - `accept` (known-good answers) or `accept_from_pattern: true`,
  - `support` (the support-language hint; falls back to the core `meaning` for line steps),
  - `roman` (romanization), `status` and `note` for the reviewer,
  - `variants`: the **line bank**, reviewed alternatives played at random. Each has `text`, optional `support` and `roman`, `status`, and `source`: `drafted` (written offline) or `live` (a live AI line from play that a reviewer approved),
  - `live_until_variants`: once this many variants are approved (default 8), the game stops requesting live lines for the step.

```yaml
  greet_neighbor:
    prompt: "…"
    variants:
      - { text: "…", source: drafted, status: approved }
      - { text: "…", source: live,    status: approved }
```
- A `line` step with a `pattern` needs no line: the engine fills the template.

## 6. Lexicon

```yaml
- { concept: feel.hungry, text: ဗိုက်ဆာတယ်, stem: ဗိုက်ဆာ }
- { concept: food.egg,    text: ကြက်ဥ, classifier: လုံး }
- { concept: x.my.classifier_item, text: ခု, local: true, gloss: "counting word for items" }
```

Fields: `text` (required), `roman`, `stem`, `classifier`, `local`, `gloss`, `status`, `note`. Pictures and audio are looked up by concept id, so the lexicon holds only text.

## 7. Care Actions

`core/care.yaml` holds the rules the engine must keep (needs change only during play, the worst state is grumpy, no notifications, items come only from quests, care never makes Ekqo grow), the needs with their cues and rise rates, the moods with their small bonuses, and the care actions.

Each care action is a list of short speaking **moments**:

```yaml
- id: feed
  need: hunger
  items_from: "care:feed"           # any food the child has met
  moments:
    - { id: ask_hungry,  role: child_says, pattern: pat.are_you_feeling, fill: { x: feel.hungry }, meaning: "Are you hungry?" }
    - { id: offer_food,  role: child_says, pattern: pat.want_to_eat_q, meaning: "Do you want to eat ___?" }
    - { id: taste_reply, role: ekqo_says,  meaning: "Ekqo says how it tastes", draws_from: "prefix:taste." }
```

Care moments reuse the quests' patterns and words, so care is daily review (storyboard 11.3). Each care moment counts as a separate "situation" for word progress.

## 8. Ekqo Discoveries

```yaml
- id: ekqo.fav_food
  category: favorite_food
  meaning: "Sour fruit is his favorite; he puckers his face and asks for more"
  concepts: [fruit.sour, taste.sour]
  trigger: { kind: care_reaction, action: feed, item: fruit.sour, first_seen_in: q02 }
```

Trigger kinds: `quest_step` (a scripted moment), `care_reaction` (Ekqo reacts to an item during care), and `ask` (the child asks him with a pattern, for later acts). The page picture is looked up by discovery id. Edition text for discovery pages goes in a `discoveries.yaml` in the edition (not written yet).

## 9. Validation

`npx tsx src/validate.ts content` (in the repo: `pnpm content:validate`). It checks shape with the zod schema and then:

- every id referenced anywhere exists (concepts, patterns, characters, locations and areas, items, abilities, care actions, discoveries, quest steps);
- each quest is listed by its chapter; `reuses` and review rounds only point to earlier quests; speakers are in the quest's characters;
- each edition has a lexicon entry for every say and hear word, a template for every pattern the core uses, no word in both say and hear, and templates that use only real slots;
- **Myanmar text is Unicode, not Zawgyi** (a Zawgyi-style vowel order fails the build).

Warnings are the content backlog: quests outside 6–8 words to say, missing lines, missing names, missing romanization, and words the core needs but the edition lacks. The current report is `content/VALIDATION.md`.

## 10. Adding a Language Edition

1. Copy `editions/my/` to `editions/<lang>/` and fill `edition.yaml` (locale, script, font, voices, register, names).
2. Give a word for every concept in `lexicon.yaml`, adding `local` words where the grammar needs them.
3. Write a template for every pattern.
4. Choose the say and hear words for each quest.
5. Run the pipeline to draft lines, review them, and run the validator until there are no errors.

No code changes are needed, unless the language needs a new placeholder type (for example gendered adjective forms), which is added to the template engine once and then available to every edition.
