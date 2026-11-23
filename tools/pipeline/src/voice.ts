/**
 * Pre-renders every line, word and answer the game can say with Azure neural voices
 * (architecture S7), so the game never depends on the tablet having a Burmese voice.
 *
 *   AZURE_SPEECH_KEY=... AZURE_SPEECH_REGION=... pnpm voice:render
 *
 * Reads build/content/<edition>/bundle.json (run pnpm content:build first) and writes
 * game/Assets/StreamingAssets/voice/<edition>/<voice>/<hash>.mp3 plus voice.json, the list
 * the game loads. A clip is named by the speaking character and a hash of its text (the same
 * rule as VoiceService.ClipKey in the game), so unchanged lines are never rendered twice and
 * a changed line simply gets a new file. Sentences with the child's name can't be
 * pre-rendered; the game shows them without sound.
 */
import { existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

// ---------- naming (must match VoiceService.ClipKey) ----------

export function hashText(text: string): string {
  let h = 2166136261;
  const t = text.trim();
  for (let i = 0; i < t.length; i++) {
    h ^= t.charCodeAt(i);
    h = Math.imul(h, 16777619) >>> 0;
  }
  return h.toString(16).padStart(8, "0");
}

export function voiceKey(speaker: string | undefined, text: string): string {
  const voice = !speaker || speaker === "child" ? "ekqo" : speaker;
  return `${voice}/${hashText(text)}`;
}

// ---------- what the game can say ----------

/* eslint-disable @typescript-eslint/no-explicit-any */
type Bundle = any;

export interface Clip { key: string; speaker: string; text: string }

export function collectClips(bundle: Bundle): Clip[] {
  const ed = bundle.edition;
  const names: Record<string, { text?: unknown }> = ed.edition.names ?? {};
  const lexicon = new Map<string, string>((ed.lexicon.entries as any[]).map((e) => [e.concept, e.text]));
  const templates = new Map<string, string>((ed.patterns.patterns as any[]).map((p) => [p.id, p.template]));
  const corePatterns = new Map<string, any>((bundle.core.concepts.patterns as any[]).map((p) => [p.id, p]));
  const clips = new Map<string, Clip>();
  const add = (speaker: string | undefined, text: string | undefined | null) => {
    if (!text || !text.trim()) return;
    const s = !speaker || speaker === "child" ? "ekqo" : speaker;
    const key = voiceKey(s, text);
    if (!clips.has(key)) clips.set(key, { key, speaker: s, text: text.trim() });
  };
  const value = (raw: string): string | null => {
    if (raw.startsWith("{") && raw.endsWith("}")) {
      const k = raw.slice(1, -1);
      if (k === "player_name") return null;
      const n = names[k]?.text;
      return typeof n === "string" ? n : null;
    }
    return lexicon.get(raw) ?? raw;
  };
  /** A pattern filled with names and words, or null when it needs the child's name. */
  const pattern = (id: string, fill?: Record<string, string>): string | null => {
    const t = templates.get(id);
    if (!t) return null;
    let ok = true;
    const out = t.replace(/\{([a-z_]+)(\.[a-z_]+)?\}/g, (_m, slot: string) => {
      const raw = fill?.[slot] ?? (corePatterns.get(id)?.slots?.[slot] === "player_name" ? "{player_name}" : undefined);
      const v = raw ? value(raw) : null;
      if (v == null) ok = false;
      return v ?? "";
    });
    return ok ? out : null;
  };

  // Every word: teaching, pictures to tap, review and the word book use Ekqo's voice.
  for (const w of lexicon.values()) add("ekqo", w);

  for (const eq of ed.quests as any[]) {
    const quest = (bundle.core.quests as any[]).find((q) => q.id === eq.quest);
    if (!quest) continue;
    for (const step of quest.steps as any[]) {
      const line = eq.lines?.[step.id];
      if (step.type === "line") {
        add(step.speaker, line?.text ?? (step.pattern ? pattern(step.pattern, step.fill) : null) ?? lexicon.get(step.concepts?.[0]));
      } else if (step.type === "say" || step.type === "use" || step.type === "conversation") {
        add(step.speaker, line?.prompt);
        const ex = step.expects ?? {};
        const answers: (string | null | undefined)[] = [
          ...(line?.tap ?? []),
          ...(ex.any_of ?? []).map((c: string) => lexicon.get(c)),
          ...(ex.concepts ?? []).map((c: string) => lexicon.get(c)),
          ...(line?.accept ?? []),
          ex.pattern ? pattern(ex.pattern, ex.slot_values) : null,
        ];
        // The answer is said back by Ekqo, and by the asking character after a wrong tap.
        for (const a of answers) { add("ekqo", a); add(step.speaker, a); }
      } else if (step.type === "teach") {
        for (const p of step.patterns ?? []) add("ekqo", pattern(p));
      }
    }
  }
  return [...clips.values()];
}

// ---------- Azure ----------

interface CharacterVoice { voice?: string; pitch?: string; rate?: string }

function ssml(locale: string, voiceName: string, cv: CharacterVoice, text: string): string {
  const esc = text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  const prosody = [cv.pitch ? `pitch="${cv.pitch}"` : "", cv.rate ? `rate="${cv.rate}"` : ""].filter(Boolean).join(" ");
  const body = prosody ? `<prosody ${prosody}>${esc}</prosody>` : esc;
  return `<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="${locale}"><voice name="${voiceName}">${body}</voice></speak>`;
}

async function render(region: string, key: string, body: string): Promise<Buffer> {
  for (let attempt = 1; ; attempt++) {
    const res = await fetch(`https://${region}.tts.speech.microsoft.com/cognitiveservices/v1`, {
      method: "POST",
      headers: {
        "Ocp-Apim-Subscription-Key": key,
        "Content-Type": "application/ssml+xml",
        "X-Microsoft-OutputFormat": "audio-24khz-48kbitrate-mono-mp3",
        "User-Agent": "ekqo-voice-pipeline",
      },
      body,
    });
    if (res.ok) return Buffer.from(await res.arrayBuffer());
    // The free tier allows about 20 requests a minute: wait and try again.
    if ((res.status === 429 || res.status >= 500) && attempt < 8) {
      const wait = Number(res.headers.get("retry-after") ?? 0) * 1000 || 4000 * attempt;
      await new Promise((r) => setTimeout(r, wait));
      continue;
    }
    const detail = await res.text().catch(() => "");
    if (res.status === 401) throw new Error("Azure refused the key (401). Check AZURE_SPEECH_KEY and AZURE_SPEECH_REGION.");
    throw new Error(`Azure answered ${res.status} ${res.statusText} ${detail}`);
  }
}

function listClips(dir: string): string[] {
  if (!existsSync(dir)) return [];
  const out: string[] = [];
  for (const voice of readdirSync(dir)) {
    const vdir = join(dir, voice);
    if (!statSync(vdir).isDirectory()) continue;
    for (const f of readdirSync(vdir)) if (f.endsWith(".mp3")) out.push(`${voice}/${f.slice(0, -4)}`);
  }
  return out.sort();
}

async function main() {
  const [contentDir, outRoot] = process.argv.slice(2);
  const key = process.env.AZURE_SPEECH_KEY;
  const region = process.env.AZURE_SPEECH_REGION;
  if (!key || !region) {
    console.error("Set AZURE_SPEECH_KEY and AZURE_SPEECH_REGION (from the Azure portal: your Speech resource → Keys and Endpoint).");
    process.exit(1);
  }
  for (const edition of readdirSync(contentDir)) {
    const bundlePath = join(contentDir, edition, "bundle.json");
    if (!existsSync(bundlePath)) continue;
    const bundle = JSON.parse(readFileSync(bundlePath, "utf8"));
    const tts = bundle.edition.edition.speech?.tts;
    const locale: string = bundle.edition.edition.locale;
    if (tts?.provider !== "azure") { console.log(`${edition}: no Azure voices set, skipped`); continue; }
    const outDir = join(outRoot, edition);
    const clips = collectClips(bundle);
    let made = 0;
    for (const c of clips) {
      const file = join(outDir, `${c.key}.mp3`);
      if (existsSync(file)) continue;
      const cv: CharacterVoice = tts.characters?.[c.speaker] ?? {};
      const voiceName = tts.voices[cv.voice === "male" ? "male" : "female"];
      mkdirSync(join(outDir, c.key.split("/")[0]), { recursive: true });
      writeFileSync(file, await render(region, key, ssml(locale, voiceName, cv, c.text)));
      made++;
      console.log(`${edition} ${c.key}  ${c.text}`);
    }
    const all = listClips(outDir);
    writeFileSync(join(outDir, "voice.json"), JSON.stringify({ clips: all }, null, 1) + "\n");
    console.log(`${edition}: ${made} new clips, ${all.length} in total (${clips.length} needed by the content).`);
  }
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  main().catch((e) => { console.error(e instanceof Error ? e.message : e); process.exit(1); });
}
