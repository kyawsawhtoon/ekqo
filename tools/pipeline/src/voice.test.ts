// Checks the clip naming matches the game (VoiceService.ClipKey) and that Quest 1's
// spoken lines are all collected from the real bundle.
import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { collectClips, hashText, voiceKey } from "./voice.js";

// FNV-1a 32-bit over UTF-16 code units of the trimmed text, as in C#.
assert.equal(hashText(""), "811c9dc5");
assert.equal(hashText("a"), "e40c292c");
assert.equal(hashText("  a "), "e40c292c");
assert.equal(voiceKey(undefined, "a"), "ekqo/e40c292c");
assert.equal(voiceKey("child", "a"), "ekqo/e40c292c");
assert.equal(voiceKey("grandma", "a"), "grandma/e40c292c");

const bundlePath = "../../build/content/my/bundle.json";
if (existsSync(bundlePath)) {
  const clips = collectClips(JSON.parse(readFileSync(bundlePath, "utf8")));
  const texts = new Set(clips.map((c) => c.text));
  for (const t of ["ဘယ်သူလဲ", "မင်္ဂလာပါ", "ကြောက်လား", "ကြောက်တယ်", "မကြောက်ဘူး", "နာမည် Ekqo ပါ", "အိမ်ကို လိုက်ခဲ့"])
    assert.ok(texts.has(t), `missing clip for ${t}`);
  assert.ok(![...texts].some((t) => t.includes("{")), "no unfilled slots");
  console.log(`voice: ${clips.length} clips collected from the my bundle`);
}
console.log("voice tests passed");
