/**
 * Validates Ekqo content: shape (zod) plus cross-references between core and
 * editions. Errors fail the build; warnings are the content backlog.
 *
 *   npx tsx src/validate.ts [contentDir] [--report out.md]
 */
import { readFileSync, readdirSync, existsSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { parse } from "yaml";
import { z } from "zod";
import * as S from "./schema.js";

const args = process.argv.slice(2);
const root = args.find((a, i) => !a.startsWith("--") && args[i - 1] !== "--report") ?? "content";
const reportPath = args.includes("--report") ? args[args.indexOf("--report") + 1] : undefined;

const errors: string[] = [];
const warnings: string[] = [];
const err = (where: string, msg: string) => errors.push(`${where}: ${msg}`);
const warn = (where: string, msg: string) => warnings.push(`${where}: ${msg}`);

function load<T extends z.ZodTypeAny>(file: string, schema: T): z.infer<T> | undefined {
  const raw = parse(readFileSync(file, "utf8"));
  const res = schema.safeParse(raw);
  if (!res.success) {
    for (const i of res.error.issues) err(file, `${i.path.join(".") || "(root)"} ${i.message}`);
    return undefined;
  }
  return res.data;
}
const yamls = (dir: string) => existsSync(dir) ? readdirSync(dir).filter((f) => f.endsWith(".yaml")).sort() : [];

// ---------- core ----------
const core = join(root, "core");
const world = load(join(core, "world.yaml"), S.World);
const concepts = load(join(core, "concepts.yaml"), S.Concepts);
const care = load(join(core, "care.yaml"), S.Care);
const disc = load(join(core, "discoveries.yaml"), S.Discoveries);
const quests = yamls(join(core, "quests")).map((f) => ({ f: join(core, "quests", f), q: load(join(core, "quests", f), S.Quest) }));

if (!world || !concepts || !care || !disc) {
  finish();
}

const conceptIds = new Set(concepts!.concepts.map((c) => c.id));
const patternIds = new Set(concepts!.patterns.map((p) => p.id));
const charIds = new Set(world!.characters.map((c) => c.id));
const itemIds = new Set(world!.items.map((i) => i.id));
const abilityIds = new Set(world!.ekqo.abilities.map((a) => a.id));
const careActions = new Set(care!.actions.map((a) => a.id));
const discIds = new Set(disc!.discoveries.map((d) => d.id));
const locs = new Map(world!.locations.map((l) => [l.id, new Set(l.areas)]));
const questById = new Map(quests.filter((x) => x.q).map((x) => [x.q!.id, x.q!]));

dupes(concepts!.concepts.map((c) => c.id), "core/concepts.yaml");
dupes(concepts!.patterns.map((p) => p.id), "core/concepts.yaml patterns");

const isSelector = (r: string) => r.includes(":") || r === "any" || r.startsWith("{");
function checkConcept(where: string, ref: string) {
  if (isSelector(ref)) return;
  if (!conceptIds.has(ref) && !ref.startsWith("x.")) err(where, `unknown concept ${ref}`);
}
function checkPattern(where: string, p?: string) {
  if (p && !patternIds.has(p)) err(where, `unknown pattern ${p}`);
}

for (const ch of world!.chapters) for (const q of ch.quests) if (!questById.has(q)) err("core/world.yaml", `chapter ${ch.id} lists missing quest ${q}`);

for (const { f, q } of quests) {
  if (!q) continue;
  if (!world!.chapters.some((c) => c.id === q.chapter && c.quests.includes(q.id))) err(f, `chapter ${q.chapter} does not list ${q.id}`);
  const [loc, area] = q.location.split(".");
  if (!locs.has(loc)) err(f, `unknown location ${loc}`);
  else if (area && !locs.get(loc)!.has(area)) err(f, `unknown area ${q.location}`);
  q.characters.forEach((c) => charIds.has(c) || err(f, `unknown character ${c}`));
  q.topics.forEach((c) => checkConcept(f, c));
  q.patterns.forEach((p) => checkPattern(f, p));
  q.rewards.forEach((i) => itemIds.has(i) || err(f, `unknown reward item ${i}`));
  q.reuses.forEach((r) => r < q.id || err(f, `reuses ${r}, which is not an earlier quest`));
  q.discoveries?.forEach((d) => discIds.has(d) || err(f, `unknown discovery ${d}`));
  if (q.care && !careActions.has(q.care.unlocks)) err(f, `unknown care action ${q.care.unlocks}`);
  q.ekqo?.abilities?.forEach((a) => abilityIds.has(a) || err(f, `unknown ability ${a}`));
  dupes(q.steps.map((s) => s.id), f);
  for (const s of q.steps) {
    const w = `${f}#${s.id}`;
    if ("speaker" in s && s.speaker && !q.characters.includes(s.speaker)) err(w, `speaker ${s.speaker} is not in the quest's characters`);
    if (s.type === "line") { s.concepts?.forEach((c) => checkConcept(w, c)); checkPattern(w, s.pattern); }
    if (s.type === "teach") { s.concepts.forEach((c) => checkConcept(w, c)); s.patterns?.forEach((p) => checkPattern(w, p)); }
    if (s.type === "say" || s.type === "use" || s.type === "conversation") {
      const e = s.expects;
      [...(e.concepts ?? []), ...(e.any_of ?? [])].forEach((c) => checkConcept(w, c));
      checkPattern(w, e.pattern); e.patterns_any?.forEach((p) => checkPattern(w, p));
      if (!e.open && !e.concepts && !e.any_of && !e.pattern && !e.patterns_any) err(w, "expects needs concepts, any_of, pattern or open: true");
    }
    if (s.type === "care" && !careActions.has(s.action)) err(w, `unknown care action ${s.action}`);
    if (s.type === "discover" && !discIds.has(s.discovery)) err(w, `unknown discovery ${s.discovery}`);
    if (s.type === "reward") {
      s.items?.forEach((i) => itemIds.has(i) || err(w, `unknown item ${i}`));
      s.abilities?.forEach((a) => abilityIds.has(a) || err(w, `unknown ability ${a}`));
    }
    if (s.type === "review_round") s.asks.forEach((t) => {
      if (!q.characters.includes(t.speaker)) err(w, `review speaker ${t.speaker} not in quest`);
      if (!(t.from_quest < q.id)) err(w, `review from ${t.from_quest} is not earlier`);
    });
  }
  if (!q.steps.some((s) => s.type === "review") && !q.steps.some((s) => s.type === "review_round")) warn(f, "no end-of-quest review step");
}

for (const d of disc!.discoveries) {
  d.concepts?.forEach((c) => checkConcept(`core/discoveries.yaml#${d.id}`, c));
  if (d.trigger.kind === "quest_step") {
    const q = questById.get(d.trigger.quest);
    if (!q) err(`core/discoveries.yaml#${d.id}`, `unknown quest ${d.trigger.quest}`);
    else if (!q.steps.some((s) => s.id === (d.trigger as { step: string }).step)) err(`core/discoveries.yaml#${d.id}`, `quest ${q.id} has no step ${d.trigger.step}`);
  } else if (d.trigger.kind === "care_reaction") checkConcept(`core/discoveries.yaml#${d.id}`, d.trigger.item);
}
for (const a of care!.actions) for (const m of a.moments) {
  checkPattern(`core/care.yaml#${a.id}.${m.id}`, m.pattern);
  m.concepts?.forEach((c) => checkConcept(`core/care.yaml#${a.id}.${m.id}`, c));
}

// ---------- editions ----------
const editionsDir = join(root, "editions");
const editionReports: string[] = [];
for (const ed of existsSync(editionsDir) ? readdirSync(editionsDir) : []) {
  const dir = join(editionsDir, ed);
  const edition = load(join(dir, "edition.yaml"), S.Edition);
  const lex = load(join(dir, "lexicon.yaml"), S.Lexicon);
  const pats = load(join(dir, "patterns.yaml"), S.EditionPatterns);
  if (!edition || !lex || !pats) continue;

  dupes(lex.entries.map((e) => e.concept), `${dir}/lexicon.yaml`);
  const lexMap = new Map(lex.entries.map((e) => [e.concept, e]));
  const tmpl = new Map(pats.patterns.map((p) => [p.id, p]));
  const scriptCheck = edition.script === "Mymr" ? checkMyanmarUnicode : () => {};

  for (const e of lex.entries) {
    const w = `${dir}/lexicon.yaml#${e.concept}`;
    if (e.local) { if (!e.concept.startsWith(`x.${ed}.`)) err(w, `edition-only concepts must start with x.${ed}.`); }
    else if (!conceptIds.has(e.concept)) err(w, "unknown core concept (mark local: true for edition-only words)");
    [e.text, e.stem, e.classifier].forEach((t) => t && scriptCheck(w, t));
  }
  for (const p of pats.patterns) {
    if (!patternIds.has(p.id)) err(`${dir}/patterns.yaml`, `unknown pattern ${p.id}`);
    scriptCheck(`${dir}/patterns.yaml#${p.id}`, p.template);
    const slots = Object.keys(concepts!.patterns.find((c) => c.id === p.id)?.slots ?? {});
    for (const m of p.template.matchAll(/\{(\w+)(?:\.\w+)?\}/g)) if (!slots.includes(m[1])) err(`${dir}/patterns.yaml#${p.id}`, `template uses {${m[1]}}, not a slot of the pattern`);
  }

  let sayTotal = 0, hearTotal = 0, linesNeeded = 0, linesDone = 0;
  const usedPatterns = new Set<string>();
  for (const { q } of quests) {
    if (!q) continue;
    q.patterns.forEach((p) => usedPatterns.add(p));
    q.steps.forEach((s) => { if (s.type === "line" && s.pattern) usedPatterns.add(s.pattern); });
    const file = join(dir, "quests", `${q.id}.yaml`);
    if (!existsSync(file)) { err(dir, `missing quests/${q.id}.yaml`); continue; }
    const eq = load(file, S.EditionQuest);
    if (!eq) continue;
    if (eq.quest !== q.id) err(file, `quest field ${eq.quest} does not match ${q.id}`);
    sayTotal += eq.say.length; hearTotal += eq.hear.length;
    for (const c of [...eq.say, ...eq.hear]) if (!lexMap.has(c)) err(file, `${c} has no lexicon entry`);
    const both = eq.say.filter((c) => eq.hear.includes(c));
    if (both.length) err(file, `in both say and hear: ${both.join(", ")}`);
    if (!eq.say_review && (eq.say.length < 6 || eq.say.length > 8)) warn(file, `${eq.say.length} words to say (learning design asks for 6-8)`);
    for (const s of q.steps) if (s.type === "teach") s.concepts.forEach((c) => eq.say.includes(c) || warn(`${file}#${s.id}`, `teaches ${c}, which is not in this edition's say list`));

    const stepIds = new Set(q.steps.map((s) => s.id));
    for (const k of Object.keys(eq.lines)) if (!stepIds.has(k)) err(file, `line for unknown step ${k}`);
    for (const s of q.steps) {
      const needs = (s.type === "line" && !s.pattern) || s.type === "say" || s.type === "use";
      if (!needs) continue;
      linesNeeded++;
      const l = eq.lines[s.id];
      const ok = l && (s.type === "line" ? !!l.text : !!(l.accept?.length || l.accept_from_pattern));
      if (ok) linesDone++;
      [l?.text, l?.prompt, ...(l?.accept ?? []), ...(l?.tap ?? []), ...(l?.variants ?? []).map((v) => v.text)].forEach((t) => t && scriptCheck(`${file}#${s.id}`, t));
      for (const t of l?.tap ?? []) if (!l?.accept?.includes(t)) err(`${file}#${s.id}`, `tap answer ${t} is not in accept`);
    }
  }
  // patterns used by core but not templated in this edition
  care!.actions.forEach((a) => a.moments.forEach((m) => m.pattern && usedPatterns.add(m.pattern)));
  for (const p of usedPatterns) if (!tmpl.has(p)) err(`${dir}/patterns.yaml`, `no template for ${p}, which the core content uses`);
  // concepts the core needs words for (care items, discoveries) that the lexicon lacks
  const coreNeeds = new Set<string>();
  disc!.discoveries.forEach((d) => { d.concepts?.forEach((c) => coreNeeds.add(c)); if (d.trigger.kind === "care_reaction") coreNeeds.add(d.trigger.item); });
  for (const c of coreNeeds) if (!lexMap.has(c)) warn(`${dir}/lexicon.yaml`, `no word yet for ${c} (needed by discoveries/care)`);
  for (const [k, n] of Object.entries(edition.names)) if (!n.text) warn(`${dir}/edition.yaml`, `no name yet for ${k}`);
  const noRoman = lex.entries.filter((e) => !e.roman).length;
  if (noRoman) warn(`${dir}/lexicon.yaml`, `${noRoman} of ${lex.entries.length} entries have no romanization yet`);
  if (linesDone < linesNeeded) warn(dir, `${linesNeeded - linesDone} of ${linesNeeded} spoken steps have no ${edition.name} line or accepted answers yet`);
  const drafts = lex.entries.filter((e) => e.status === "draft").length + pats.patterns.filter((p) => p.status === "draft").length;

  editionReports.push(
    `### ${edition.name} (${edition.id})\n\n` +
    `| Measure | Count |\n| --- | --- |\n` +
    `| Words to say (sum of quest lists) | ${sayTotal} |\n| Words to hear (sum of quest lists) | ${hearTotal} |\n` +
    `| Lexicon entries | ${lex.entries.length} |\n| Pattern templates | ${pats.patterns.length} |\n` +
    `| Spoken steps with lines | ${linesDone} of ${linesNeeded} |\n| Entries still marked draft | ${drafts} |\n`);
}

function checkMyanmarUnicode(where: string, text: string) {
  // Zawgyi stores the e-vowel (U+1031) before the consonant; Unicode never starts a syllable with it.
  if (/(^|[\s -\u007F])ေ/.test(text)) err(where, `"${text}" looks like Zawgyi (U+1031 before a consonant)`);
  // U+1060-U+1097 are Mon/Shan/Karen letters in Unicode; Zawgyi reuses them for Burmese stacking.
  if (/[ၠ-႗]/.test(text)) warn(where, `"${text}" uses U+1060-U+1097; check it is not Zawgyi`);
  if (!/[က-႟]/.test(text) && !/^[\s.…{}\w]+$/.test(text)) warn(where, `"${text}" has no Myanmar characters`);
}

function dupes(ids: string[], where: string) {
  const seen = new Set<string>();
  for (const i of ids) { if (seen.has(i)) err(where, `duplicate id ${i}`); seen.add(i); }
}

finish();

function finish(): never {
  const qn = quests.filter((x) => x.q).length;
  const summary =
    `# Content validation report\n\n` +
    `Core: ${qn} quests, ${conceptIdsSafe()} concepts, ${concepts?.patterns.length ?? 0} patterns, ` +
    `${disc?.discoveries.length ?? 0} discoveries, ${care?.actions.length ?? 0} care actions.\n\n` +
    `**${errors.length} errors, ${warnings.length} warnings.**\n\n` +
    editionReports.join("\n") +
    (errors.length ? `\n## Errors\n\n${errors.map((e) => `- ${e}`).join("\n")}\n` : "") +
    (warnings.length ? `\n## Warnings (content backlog)\n\n${warnings.map((e) => `- ${e}`).join("\n")}\n` : "");
  console.log(summary);
  if (reportPath) writeFileSync(reportPath, summary);
  process.exit(errors.length ? 1 : 0);
}
function conceptIdsSafe() { return concepts?.concepts.length ?? 0; }
