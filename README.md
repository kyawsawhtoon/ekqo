# Ekqo

A language-learning adventure RPG for children aged 6–9. Burmese first, and built so
new language editions are added as content, not code.

| Folder | What it is |
| --- | --- |
| `docs/` | Technical architecture and the content format |
| `content/` | All game content as YAML: `core/` (the story, the same in every language) and `editions/<lang>/` |
| `packages/content` | Content schema (zod), validator and compiler to JSON bundles |
| `services/api` | Backend (Node + Fastify): speech tokens, answer checking, later accounts and saves |
| `game/` | Unity 6 game (Web build). See `game/README.md` for first-time setup |
| `tools/pipeline` | Content drafting, review and voice pre-rendering (to come) |

## Getting started

```sh
pnpm install
pnpm content:validate     # check content; writes content/VALIDATION.md
pnpm content:build        # build/content/<edition>/bundle.json for the game and the API
pnpm test                 # content checks + API tests
cd services/api && cp .env.example .env && pnpm dev
```

Requires Node 22+ and pnpm 10.
