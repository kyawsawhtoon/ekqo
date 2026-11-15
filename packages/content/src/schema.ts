/**
 * Ekqo content schema (v1). Source of truth for content shape; the game client,
 * the conversation service and the content pipeline import these types.
 *
 * Two layers:
 *  - core/      language-neutral: world, concepts, patterns, quests, care, discoveries
 *  - editions/  per language: lexicon (concept -> words), pattern templates,
 *               per-quest say/hear lists and lines
 */
import { z } from "zod";

// ---------- shared ----------
const id = z.string().regex(/^[a-z0-9_]+$/, "lowercase id");
const conceptId = z.string().regex(/^[a-z]+(\.[a-z0-9_]+)+$/, "concept id like food.rice");
const patternId = z.string().regex(/^pat\.[a-z0-9_]+$/);
const questId = z.string().regex(/^q\d{2}$/);
/** Concept id, or a selector: "prefix:num.", "kind:noun care:feed", "any", "player_name". */
const conceptRef = z.string().min(1);
const status = z.enum(["approved", "draft", "missing"]).optional();

// ---------- core: world ----------
export const Character = z.object({
  id,
  role: z.enum(["companion", "family", "friend", "neighbor", "shopkeeper", "market_seller"]),
  personality: z.string(),
  speaks_to_child: z.enum(["casual", "polite"]),
  child_speaks_to: z.enum(["casual", "polite"]).optional(),
  voice: z.object({
    gender: z.enum(["female", "male", "neutral", "player_choice", "edition"]),
    style: z.string(),
    age: z.enum(["child", "adult", "older"]).optional(),
    pitch: z.string().optional(),
  }),
  first_chapter: z.number().int(),
});

export const World = z.object({
  schema: z.literal("ekqo.world/1"),
  ekqo: z.object({
    stages: z.array(z.object({
      stage: z.number().int().min(1).max(6), id, name: z.string(),
      reached: z.string(), moves: z.string(), power: z.string().optional(),
    })),
    abilities: z.array(z.object({ id, meaning: z.string(), unlocked_by: questId })),
  }),
  characters: z.array(Character),
  locations: z.array(z.object({
    id, meaning: z.string(), unlocked_by: z.union([z.literal("start"), questId]),
    areas: z.array(id),
  })),
  chapters: z.array(z.object({
    id, number: z.number().int(), title: z.string(), quests: z.array(questId),
    tune: id, ends_with_stage: z.number().int(), i_can: z.array(z.string()).min(1),
  })),
  tunes: z.array(z.object({ id, meaning: z.string(), lyrics: z.literal(false) })),
  items: z.array(z.object({
    id, kind: z.enum(["room", "care", "badge", "outfit", "key", "parent"]), meaning: z.string(),
  })),
  learning: z.record(z.unknown()),
});

// ---------- core: concepts and patterns ----------
export const Concept = z.object({
  id: conceptId,
  gloss: z.string(),
  kind: z.enum(["noun", "verb", "adjective", "adverb", "phrase", "number", "color", "direction"]),
  care: z.enum(["feed", "clean", "sleep", "play"]).optional(),
  value: z.number().optional(),
  image: z.string().optional(),
});
export const Pattern = z.object({
  id: patternId,
  meaning: z.string(),
  slots: z.record(conceptRef),
});
export const Concepts = z.object({
  schema: z.literal("ekqo.concepts/1"),
  concepts: z.array(Concept),
  patterns: z.array(Pattern),
});

// ---------- core: quests ----------
/** What a child's answer must mean. The local matcher and the AI judge both read this. */
export const Expects = z.object({
  intent: z.string(),
  concepts: z.array(conceptRef).optional(),      // all of these
  any_of: z.array(conceptRef).optional(),        // at least one of these
  pattern: patternId.optional(),
  patterns_any: z.array(patternId).optional(),
  slot_values: z.record(z.string()).optional(),
  from_task: z.string().optional(),
  open: z.boolean().optional(),                  // free answer judged only by intent
}).strict();

const AiPolicy = z.object({
  npc_vary: z.boolean(),                         // may the AI rephrase the NPC prompt?
  judge: z.literal("meaning"),
});

const stepBase = { id, meaning: z.string().optional(), note: z.string().optional() };
const speaker = id;

function speakingStep<T extends "say" | "use">(t: T) {
  return z.object({
    ...stepBase, type: z.literal(t), speaker,
    expects: Expects, ai: AiPolicy,
    repeat: z.number().int().min(1).optional(),
    on_success: z.record(z.unknown()).optional(),
  }).strict();
}

export const Step = z.discriminatedUnion("type", [
  z.object({ ...stepBase, type: z.literal("scene"), effects: z.record(z.unknown()).optional() }).strict(),
  z.object({
    ...stepBase, type: z.literal("line"), speaker,
    concepts: z.array(conceptId).optional(), pattern: patternId.optional(),
    fill: z.record(z.string()).optional(), speed: z.enum(["slow", "normal", "fast"]).optional(),
  }).strict(),
  z.object({
    ...stepBase, type: z.literal("teach"),
    concepts: z.array(conceptId).min(1), patterns: z.array(patternId).optional(),
  }).strict(),
  speakingStep("say"),   // a character asks; the child answers in a real situation
  speakingStep("use"),   // the quest task needs the phrase to move on
  z.object({
    ...stepBase, type: z.literal("conversation"), speaker, turns: z.number().int().min(2),
    expects: Expects, ai: AiPolicy,
  }).strict(),
  z.object({
    ...stepBase, type: z.literal("review_round"),
    asks: z.array(z.object({ speaker, from_quest: questId, intent: z.string() })).min(1),
  }).strict(),
  z.object({
    ...stepBase, type: z.literal("care"), action: id,
    first_time: z.boolean().optional(), required: z.boolean().optional(),
  }).strict(),
  z.object({ ...stepBase, type: z.literal("discover"), discovery: z.string() }).strict(),
  z.object({
    ...stepBase, type: z.literal("stage"), stage: z.number().int(), full_shine: z.boolean().optional(),
  }).strict(),
  z.object({ ...stepBase, type: z.literal("review") }).strict(),
  z.object({
    ...stepBase, type: z.literal("reward"),
    items: z.array(id).optional(), abilities: z.array(id).optional(),
    sparks: z.number().int().optional(), friends: z.array(id).optional(),
  }).strict(),
]);

export const Quest = z.object({
  schema: z.literal("ekqo.quest/1"),
  id: questId,
  chapter: id,
  title: z.string(),
  location: z.string(),                          // "house" or "house.garden"
  time_of_day: z.enum(["morning", "day", "evening", "night"]).optional(),
  characters: z.array(id).min(1),
  function: z.string(),
  topics: z.array(conceptId),
  patterns: z.array(patternId),
  listening_weight: z.enum(["heavy", "normal"]),
  reuses: z.array(questId),
  care: z.object({ unlocks: id }).optional(),
  discoveries: z.array(z.string()).optional(),
  ekqo: z.object({
    stage_on_start: z.number().int().optional(),
    stage_on_complete: z.number().int().optional(),
    abilities: z.array(id).optional(),
    full_shine: z.boolean().optional(),
  }).optional(),
  rewards: z.array(id),
  unlocks: z.array(z.string()).optional(),
  music: id.optional(),
  task: z.record(z.unknown()).optional(),
  steps: z.array(Step).min(1),
});

// ---------- core: care and discoveries ----------
export const Care = z.object({
  schema: z.literal("ekqo.care/1"),
  rules: z.object({
    needs_change: z.literal("during_play_only"),
    worst_state: z.literal("grumpy"),
    notifications_to_child: z.literal(false),
    items_source: z.literal("quests_only"),
    growth_from_care: z.literal(false),
  }).passthrough(),
  needs: z.array(z.object({
    id, unlocked_by: questId, rise_per_active_minute: z.number().min(0),
    rise_on_events: z.record(z.number()).optional(),
    cues: z.array(id), met_by: id,
  })),
  moods: z.array(z.object({ id, when: z.string() }).passthrough()),
  actions: z.array(z.object({
    id, need: id.nullable(),
    location_area: z.string().optional(), items_from: z.string().optional(),
    item_source: z.string().optional(), music: id.optional(),
    moments: z.array(z.object({
      id, role: z.enum(["child_says", "ekqo_says"]), meaning: z.string(),
      pattern: patternId.optional(), fill: z.record(z.string()).optional(),
      concepts: z.array(conceptId).optional(), draws_from: z.string().optional(),
    }).strict()),
  })),
});

export const Discoveries = z.object({
  schema: z.literal("ekqo.discoveries/1"),
  discoveries: z.array(z.object({
    id: z.string().regex(/^ekqo\.[a-z0-9_]+$/),
    category: z.enum(["identity", "favorite_food", "food_disliked", "makes_him_happy",
      "scares_him", "funny_habit", "favorite_game", "favorite_place", "memory_of_home"]),
    meaning: z.string(),
    concepts: z.array(conceptId).optional(),
    trigger: z.discriminatedUnion("kind", [
      z.object({ kind: z.literal("quest_step"), quest: questId, step: id }),
      z.object({ kind: z.literal("care_reaction"), action: id, item: conceptId, first_seen_in: questId }),
      z.object({ kind: z.literal("ask"), pattern: patternId }),
    ]),
  })),
});

// ---------- edition ----------
export const Edition = z.object({
  schema: z.literal("ekqo.edition/1"),
  id: z.string().regex(/^[a-z]{2,3}$/),
  name: z.string(), native_name: z.string(),
  locale: z.string(), script: z.string().length(4),
  encoding: z.literal("unicode"), direction: z.enum(["ltr", "rtl"]),
  support_language: z.string(),
  font: z.object({ family: z.string(), bundled: z.boolean() }),
  word_separator: z.enum(["space", "none"]),
  romanization: z.object({ scheme: z.string().nullable(), status }),
  speech: z.object({
    stt: z.object({ provider: z.string(), locale: z.string() }).passthrough(),
    pronunciation_scoring: z.boolean(),
    tts: z.object({
      provider: z.string(),
      voices: z.record(z.string()),
      characters: z.record(z.object({ voice: z.string(), pitch: z.string().optional(), rate: z.string().optional() })),
    }),
  }),
  register: z.record(z.unknown()),
  profile_options: z.record(z.unknown()).optional(),
  names: z.record(z.object({
    text: z.union([z.string(), z.record(z.string())]).nullable(),
    roman: z.string().optional(), status, note: z.string().optional(),
  })),
  review: z.record(z.unknown()),
});

export const LexiconEntry = z.object({
  concept: conceptId,
  text: z.string().min(1),
  roman: z.string().optional(),
  stem: z.string().optional(),
  classifier: z.string().optional(),
  local: z.boolean().optional(),                 // edition-only concept, e.g. x.my.classifier_item
  gloss: z.string().optional(),
  status, note: z.string().optional(),
}).strict();
export const Lexicon = z.object({
  schema: z.literal("ekqo.lexicon/1"), edition: z.string(), entries: z.array(LexiconEntry),
});

export const EditionPatterns = z.object({
  schema: z.literal("ekqo.edition-patterns/1"), edition: z.string(),
  patterns: z.array(z.object({
    id: patternId, template: z.string(), status, note: z.string().optional(),
  }).strict()),
});

export const Line = z.object({
  text: z.string().optional(),                   // what the speaker says (line steps)
  prompt: z.string().optional(),                 // what the speaker asks (say/use steps)
  support: z.string().optional(),                // support-language hint; falls back to core meaning
  roman: z.string().optional(),
  accept: z.array(z.string()).optional(),        // known-good answers for the local matcher
  accept_from_pattern: z.boolean().optional(),
  /** Reviewed alternatives for the text or prompt, played at random (the line bank). */
  variants: z.array(z.object({
    text: z.string(),
    support: z.string().optional(),
    roman: z.string().optional(),
    source: z.enum(["drafted", "live"]),         // live = promoted from a live AI line candidate
    status,
  }).strict()).optional(),
  /** Stop requesting live lines for this step once this many variants are approved (default 8). */
  live_until_variants: z.number().int().min(0).optional(),
  status, note: z.string().optional(),
}).strict();

export const EditionQuest = z.object({
  schema: z.literal("ekqo.edition-quest/1"),
  edition: z.string(), quest: questId,
  say: z.array(conceptId),
  say_review: z.literal("all").optional(),
  hear: z.array(conceptId),
  lines: z.record(Line),
});

export type TWorld = z.infer<typeof World>;
export type TConcepts = z.infer<typeof Concepts>;
export type TQuest = z.infer<typeof Quest>;
export type TStep = z.infer<typeof Step>;
export type TCare = z.infer<typeof Care>;
export type TDiscoveries = z.infer<typeof Discoveries>;
export type TEdition = z.infer<typeof Edition>;
export type TLexicon = z.infer<typeof Lexicon>;
export type TEditionPatterns = z.infer<typeof EditionPatterns>;
export type TEditionQuest = z.infer<typeof EditionQuest>;
