import Fastify from "fastify";
import Anthropic from "@anthropic-ai/sdk";
import { z } from "zod";
import type { Bundle } from "@ekqo/content/bundle";
import { Matcher, stepHasPlayerName } from "./convo/matcher.js";
import { judgeWithAI } from "./convo/judge.js";
import { issueSpeechToken, type SpeechConfig } from "./speech/token.js";

export interface AppDeps {
  bundles: Map<string, Bundle>;          // by edition id
  anthropic?: Anthropic;                 // absent: matcher only
  speech: SpeechConfig;
}

const JudgeRequest = z.object({
  edition: z.string(),
  quest: z.string().regex(/^q\d{2}$/),
  step: z.string(),
  transcript: z.string().max(400),
  nbest: z.array(z.string().max(400)).max(5).default([]),
  attempt: z.number().int().min(1).max(10).default(1),
});

export function buildApp(deps: AppDeps) {
  // Request bodies are never logged: they can hold a child's transcript.
  const app = Fastify({ logger: { level: "info", redact: ["req.body", "req.headers.authorization"] } });
  const matchers = new Map([...deps.bundles].map(([ed, b]) => [ed, new Matcher(b)]));

  app.get("/health", async () => ({ ok: true, editions: [...deps.bundles.keys()] }));

  // TODO(WS5): require a parent session and rate limit per child.
  app.post("/v1/speech/token", async (_req, reply) => {
    try { return await issueSpeechToken(deps.speech); }
    catch { return reply.code(503).send({ error: "speech_unavailable" }); }
  });

  app.post("/v1/convo/judge", async (req, reply) => {
    const parsed = JudgeRequest.safeParse(req.body);
    if (!parsed.success) return reply.code(400).send({ error: "bad_request" });
    const { edition, quest, step: stepId, transcript, nbest } = parsed.data;
    const bundle = deps.bundles.get(edition);
    const step = bundle?.core.quests.find((q) => q.id === quest)?.steps.find((s) => s.id === stepId);
    if (!bundle || !step) return reply.code(404).send({ error: "unknown_step" });

    const transcripts = [transcript, ...nbest];
    const local = matchers.get(edition)!.match(quest, step, transcripts);
    if (local) return local;
    if (!deps.anthropic || stepHasPlayerName(step, bundle)) return { verdict: "unclear", matched: [], source: "matcher" };
    return judgeWithAI({ client: deps.anthropic }, bundle, quest, step, transcripts);
  });

  return app;
}
