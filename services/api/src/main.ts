import Anthropic from "@anthropic-ai/sdk";
import { buildBundle, listEditions } from "@ekqo/content/bundle";
import { buildApp } from "./app.js";

const contentDir = process.env.CONTENT_DIR ?? "../../content";
const bundles = new Map(listEditions(contentDir).map((ed) => [ed, buildBundle(contentDir, ed)]));
const app = buildApp({
  bundles,
  anthropic: process.env.ANTHROPIC_API_KEY ? new Anthropic() : undefined,
  speech: { key: process.env.AZURE_SPEECH_KEY, region: process.env.AZURE_SPEECH_REGION ?? "southeastasia" },
});
await app.listen({ port: Number(process.env.PORT ?? 8080), host: "0.0.0.0" });
