---
description: 'Hosting rules for Karamel-Web: Render (backend) and Cloudflare (frontend)'
applyTo: 'render.yaml,wrangler.jsonc,.github/workflows/**,Karamel.Backend/Dockerfile,Karamel.Backend/Program.cs'
---

# Hosting

Deployment settings belong in [`render.yaml`](../../render.yaml), [`wrangler.jsonc`](../../wrangler.jsonc), and [the CI workflow](../workflows/ci.yml). Keep this file focused on constraints those configs cannot enforce:

- Keep one backend instance: per-session semaphores and SignalR groups are in-process.
- Treat backend data as disposable. SQLite lives in `/tmp` and its schema is recreated at startup; do not add persistence or EF migrations without updating the hosting ADR.
- Keep production CORS restricted to the configured frontend origin; never use a wildcard.
- Keep secrets in Render environment variables or GitHub Actions secrets, never in the repository.
- Expect Render Free to sleep or restart, losing SQLite and in-memory session state.
- Use the configured deployment pipelines; avoid out-of-band production changes.

See [ADR-002](../../docs/ADR-002-Hosting-Migration-Off-Azure.md) for the hosting decision and trade-offs.
