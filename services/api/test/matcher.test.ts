import { describe, it, expect } from "vitest";
import { fileURLToPath } from "node:url";
import { buildBundle } from "@ekqo/content/bundle";
import { Matcher, normalize, syllables, stepHasPlayerName } from "../src/convo/matcher.js";
import { buildApp } from "../src/app.js";

const content = fileURLToPath(new URL("../../../content", import.meta.url));
const bundle = buildBundle(content, "my");
const m = new Matcher(bundle);
const step = (q: string, s: string) => bundle.core.quests.find((x) => x.id === q)!.steps.find((x) => x.id === s)!;

describe("normalize and syllables", () => {
  it("drops spaces and Burmese punctuation", () => {
    expect(normalize("မင်္ဂလာပါ ။")).toBe("မင်္ဂလာပါ");
  });
  it("splits Myanmar syllables", () => {
    expect(syllables("ကျေးဇူးတင်ပါတယ်")).toEqual(["ကျေး", "ဇူး", "တင်", "ပါ", "တယ်"]);
    expect(syllables("မင်္ဂလာပါ")).toEqual(["မင်္ဂ", "လာ", "ပါ"]);
  });
});

describe("matcher", () => {
  it("accepts a written answer", () => {
    expect(m.match("q01", step("q01", "say_hello"), ["မင်္ဂလာပါ"])?.verdict).toBe("correct");
  });
  it("accepts the name pattern with any name, without needing the name", () => {
    const s = step("q01", "say_name");
    expect(m.match("q01", s, ["နာမည် မောင်မောင် ပါ"])?.verdict).toBe("correct");
    expect(stepHasPlayerName(s, bundle)).toBe(true);
  });
  it("expands a pattern with slot words and counting words", () => {
    const s = step("q07", "ask_items");
    expect(m.match("q07", s, ["ကြက်ဥ ငါး လုံး ပေးပါ"])?.verdict).toBe("correct");
    expect(m.match("q07", s, ["ကြက်ဥ ငါး ခု ပေးပါ"])?.verdict).toBe("correct");   // ခု is taught in Q7
  });
  it("forgives one syllable in a longer answer", () => {
    expect(m.match("q07", step("q07", "thats_all"), ["ကျေးဇူးတင်ပါ"])?.verdict).toBe("correct");
  });
  it("finds any-of concepts inside a longer answer", () => {
    expect(m.match("q05", step("q05", "guide_ekqo"), ["ဘယ်ဘက် သွား"])?.matched).toContain("dir.left");
  });
  it("reports another language and silence", () => {
    expect(m.match("q01", step("q01", "say_hello"), ["hello"])?.verdict).toBe("other_language");
    expect(m.match("q01", step("q01", "say_hello"), [""])?.verdict).toBe("unclear");
  });
  it("is unsure about an unrelated Burmese answer", () => {
    expect(m.match("q05", step("q05", "ask_where_shop"), ["ထမင်း စားချင်တယ်"])).toBeNull();
  });
});

describe("POST /v1/convo/judge", () => {
  const app = buildApp({ bundles: new Map([["my", bundle]]), speech: { region: "southeastasia" } });
  it("answers from the matcher without an AI key", async () => {
    const res = await app.inject({ method: "POST", url: "/v1/convo/judge",
      payload: { edition: "my", quest: "q05", step: "ask_where_shop", transcript: "ဆိုင် ဘယ်မှာလဲ" } });
    expect(res.json()).toMatchObject({ verdict: "correct", source: "matcher" });
  });
  it("rejects unknown steps", async () => {
    const res = await app.inject({ method: "POST", url: "/v1/convo/judge",
      payload: { edition: "my", quest: "q05", step: "nope", transcript: "x" } });
    expect(res.statusCode).toBe(404);
  });
});
