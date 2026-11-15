/**
 * Compiles validated YAML content into one JSON bundle per edition, which the
 * Unity game loads and the API reads for answer checking.
 *
 *   npx tsx src/compile.ts <contentDir> <outDir>     -> <outDir>/<edition>/bundle.json
 *
 * Run validate.ts first; this script assumes the content is valid.
 */
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { buildBundle, listEditions } from "./bundle.js";

const [root = "content", out = "build/content"] = process.argv.slice(2);
for (const ed of listEditions(root)) {
  const bundle = buildBundle(root, ed);
  mkdirSync(join(out, ed), { recursive: true });
  const file = join(out, ed, "bundle.json");
  writeFileSync(file, JSON.stringify(bundle, null, 1));
  console.log(`${ed}: ${file} (version ${bundle.version})`);
}
