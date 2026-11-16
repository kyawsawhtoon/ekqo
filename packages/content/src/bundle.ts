/** Loads YAML content into the compiled bundle shape shared by the game and the API. */
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { createHash } from "node:crypto";
import { join } from "node:path";
import { parse } from "yaml";
import type { z } from "zod";
import * as S from "./schema.js";

const read = <T extends z.ZodTypeAny>(file: string, schema: T): z.infer<T> =>
  schema.parse(parse(readFileSync(file, "utf8")));
const yamls = (dir: string) => readdirSync(dir).filter((f) => f.endsWith(".yaml")).sort();

export interface Bundle {
  schema: "ekqo.bundle/1";
  version: string;
  core: {
    world: S.TWorld;
    concepts: S.TConcepts;
    care: S.TCare;
    discoveries: S.TDiscoveries;
    quests: S.TQuest[];
  };
  edition: {
    edition: S.TEdition;
    lexicon: S.TLexicon;
    patterns: S.TEditionPatterns;
    quests: S.TEditionQuest[];
  };
}

export function listEditions(root: string): string[] {
  const dir = join(root, "editions");
  return readdirSync(dir).filter((e) => existsSync(join(dir, e, "edition.yaml"))).sort();
}

export function buildBundle(root: string, edition: string): Bundle {
  const core = join(root, "core");
  const dir = join(root, "editions", edition);
  const body = {
    core: {
      world: read(join(core, "world.yaml"), S.World),
      concepts: read(join(core, "concepts.yaml"), S.Concepts),
      care: read(join(core, "care.yaml"), S.Care),
      discoveries: read(join(core, "discoveries.yaml"), S.Discoveries),
      quests: yamls(join(core, "quests")).map((f) => read(join(core, "quests", f), S.Quest)),
    },
    edition: {
      edition: read(join(dir, "edition.yaml"), S.Edition),
      lexicon: read(join(dir, "lexicon.yaml"), S.Lexicon),
      patterns: read(join(dir, "patterns.yaml"), S.EditionPatterns),
      quests: yamls(join(dir, "quests")).map((f) => read(join(dir, "quests", f), S.EditionQuest)),
    },
  };
  const version = createHash("sha256").update(JSON.stringify(body)).digest("hex").slice(0, 12);
  return { schema: "ekqo.bundle/1", version, ...body };
}
