/**
 * Server answer matcher (architecture 4.2, step 4). Decides clear cases without
 * calling the AI: the transcript is compared with the step's accepted answers,
 * the step's pattern expanded with every allowed slot word, and the lexicon
 * forms of the expected concepts. Returns null when it is unsure.
 *
 * Burmese has no spaces between words, so matching works on normalised strings
 * and Myanmar syllables, never on space-separated words.
 */
import type { Bundle } from "@ekqo/content/bundle";

export type Verdict = "correct" | "partial" | "other_language" | "off_topic" | "unclear";
export interface MatchResult { verdict: Verdict; matched: string[]; source: "matcher" }

type Step = Bundle["core"]["quests"][number]["steps"][number];
type LexEntry = Bundle["edition"]["lexicon"]["entries"][number];

const PUNCT = /[\s၊။.,!?…'"“”‘’()\-]/gu;          // includes ၊ and ။
export function normalize(text: string): string {
  return text.normalize("NFC").replace(PUNCT, "").toLowerCase();
}

/** Splits Myanmar text into syllables: a break before each consonant not stacked or killed. */
export function syllables(text: string): string[] {
  const s = normalize(text);
  const out: string[] = [];
  let cur = "";
  for (let i = 0; i < s.length; i++) {
    const ch = s[i];
    const isConsonant = /[က-အဣ-ဪဿ၀-၉]/u.test(ch);
    const prev = s[i - 1];
    const next = s[i + 1];
    const startsNew = isConsonant && cur !== "" && prev !== "္" && next !== "်" && next !== "္";
    if (startsNew) { out.push(cur); cur = ""; }
    cur += ch;
  }
  if (cur) out.push(cur);
  return out;
}

function editDistance<T>(a: T[], b: T[]): number {
  const d = Array.from({ length: a.length + 1 }, (_, i) => [i, ...Array(b.length).fill(0)]);
  for (let j = 1; j <= b.length; j++) d[0][j] = j;
  for (let i = 1; i <= a.length; i++)
    for (let j = 1; j <= b.length; j++)
      d[i][j] = Math.min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
  return d[a.length][b.length];
}

/** Close enough to a known answer: equal, or one syllable off for answers of 4+ syllables. */
export function closeMatch(transcript: string, answer: string): boolean {
  const t = normalize(transcript), a = normalize(answer);
  if (!a) return false;
  if (t === a) return true;
  const ta = syllables(t), aa = syllables(a);
  return aa.length >= 4 && editDistance(ta, aa) <= 1;
}

export class Matcher {
  private lex: Map<string, LexEntry>;
  private conceptsById: Map<string, Bundle["core"]["concepts"]["concepts"][number]>;
  constructor(private bundle: Bundle) {
    this.lex = new Map(bundle.edition.lexicon.entries.map((e) => [e.concept, e]));
    this.conceptsById = new Map(bundle.core.concepts.concepts.map((c) => [c.id, c]));
  }

  /** Concept ids matching a selector: an id, "prefix:num.", "kind:noun care:feed" or "any". */
  select(selector: string): string[] {
    if (!selector.includes(":") && selector !== "any") return [selector];
    const all = [...this.lex.keys()].filter((id) => !id.startsWith("x."));
    if (selector === "any") return all;
    const conds = selector.split(/\s+/).map((c) => c.split(":") as [string, string]);
    return all.filter((id) => {
      const c = this.conceptsById.get(id);
      return conds.every(([k, v]) =>
        k === "prefix" ? id.startsWith(v) : k === "kind" ? c?.kind === v : k === "care" ? c?.care === v : false);
    });
  }

  /** Every sentence a pattern can make. A player_name slot becomes a wildcard regex instead. */
  expand(patternId: string, slotValues: Record<string, string> = {}): { texts: string[]; wildcard?: RegExp } {
    const tmpl = this.bundle.edition.patterns.patterns.find((p) => p.id === patternId)?.template;
    const slots = this.bundle.core.concepts.patterns.find((p) => p.id === patternId)?.slots ?? {};
    if (!tmpl) return { texts: [] };
    const names = Object.keys(slots);
    const isName = (n: string) => slots[n] === "player_name" || slotValues[n] === "{player_name}";
    if (names.some(isName)) {
      const parts = normalize(tmpl).split(/\{(\w+)(?:\.\w+)?\}/);
      const re = parts.map((p, i) => (i % 2 ? (isName(p) ? ".+" : ".*") : p.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"))).join("");
      return { texts: [], wildcard: new RegExp(`^${re}$`, "u") };
    }
    let results: string[] = [tmpl];
    for (const n of names) {
      const ids = slotValues[n] ? [slotValues[n]] : this.select(slots[n]);
      const next: string[] = [];
      for (const r of results) for (const id of ids) {
        const e = this.lex.get(id);
        if (!e) continue;
        next.push(r
          .replaceAll(`{${n}.stem}`, e.stem ?? e.text)
          .replaceAll(`{${n}.classifier}`, e.classifier ?? "ခု")
          .replaceAll(`{${n}}`, e.text));
      }
      results = next;
    }
    // Counting words: Q7 teaches ခု for everything, so accept ခု in place of any classifier.
    const withGeneric = results.map((r) => r.replaceAll("လုံး", "ခု"));
    return { texts: [...new Set([...results, ...withGeneric])] };
  }

  private words(ids: string[]): Map<string, string[]> {
    const m = new Map<string, string[]>();
    for (const sel of ids) for (const id of this.select(sel)) {
      const e = this.lex.get(id);
      if (e) m.set(id, [e.text, ...(e.stem ? [e.stem] : [])]);
    }
    return m;
  }

  match(questId: string, step: Step, transcripts: string[]): MatchResult | null {
    if (!("expects" in step)) return null;
    const e = step.expects;
    const said = transcripts.map(normalize).filter(Boolean);
    if (said.length === 0) return { verdict: "unclear", matched: [], source: "matcher" };
    const hasMyanmar = transcripts.some((t) => /[က-႟]/u.test(t));
    if (!hasMyanmar) return { verdict: "other_language", matched: [], source: "matcher" };

    // 1. Accepted answers written by the content team
    const line = this.bundle.edition.quests.find((q) => q.quest === questId)?.lines[step.id];
    for (const a of line?.accept ?? []) if (said.some((t) => closeMatch(t, a))) return ok([]);

    // 2. Pattern expansions
    for (const p of [e.pattern, ...(e.patterns_any ?? [])].filter(Boolean) as string[]) {
      const { texts, wildcard } = this.expand(p, e.slot_values);
      if (wildcard && said.some((t) => wildcard.test(t))) return ok([p]);
      for (const x of texts) if (said.some((t) => closeMatch(t, x))) return ok([p]);
    }

    // 3. Concept words anywhere in the answer
    const all = this.words(e.concepts ?? []);
    const any = this.words(e.any_of ?? []);
    const contains = (forms: string[]) => said.some((t) => forms.some((f) => t.includes(normalize(f)) || closeMatch(t, f)));
    const allHit = [...all].filter(([, f]) => contains(f)).map(([id]) => id);
    const anyHit = [...any].filter(([, f]) => contains(f)).map(([id]) => id);
    const needAll = all.size > 0, needAny = any.size > 0;
    if ((needAll || needAny) && (!needAll || allHit.length === all.size) && (!needAny || anyHit.length > 0))
      return ok([...allHit, ...anyHit]);
    if (needAll && allHit.length > 0) return { verdict: "partial", matched: allHit, source: "matcher" };

    return null; // unsure: the AI judge decides
  }
}

const ok = (matched: string[]): MatchResult => ({ verdict: "correct", matched, source: "matcher" });

/** Steps whose answer contains the child's own name never go to the AI judge. */
export function stepHasPlayerName(step: Step, bundle: Bundle): boolean {
  if (!("expects" in step)) return false;
  const e = step.expects;
  if (Object.values(e.slot_values ?? {}).includes("{player_name}")) return true;
  return [e.pattern, ...(e.patterns_any ?? [])].some((p) =>
    p && Object.values(bundle.core.concepts.patterns.find((x) => x.id === p)?.slots ?? {}).includes("player_name"));
}
