# ADR-002: Move Hosting off Azure to Cloudflare (frontend) + Render Free (backend) with ephemeral SQLite

- **Status**: Proposed — becomes **Accepted** only when the performance gate G1 (see "Validation") passes
- **Date**: 2026-11-07
- **Owners**: Karamel-Web maintainer
- **Related**: ADR-001 (service boundaries, unaffected), execution plans `plan-hostingMigration-*.md` (local, git-ignored)

## Context

Karamel-Web is a hobby karaoke app: Blazor WebAssembly frontend + ASP.NET Core backend (REST + one SignalR hub).
Usage is roughly 2–3 users per day. A public demo link is advertised in the README. Budget target: **0 € permanently**,
with minimal operational effort.

### Current infrastructure (Azure, in a company-managed Visual Studio subscription)

- App Service Plan **F1 (Free, Linux)** + Web App (backend)
- Azure SQL serverless + Private Endpoint + VNet + Private DNS
- Key Vault, Application Insights, Static Web App (frontend)
- ~400 lines of Bicep plus PowerShell scripts, a separate migration workflow
- A **self-hosted GitHub runner VM** (Managed Identity) that performs all deployments

### Problems

1. **Access/ownership.** The subscription belongs to the maintainer's employer. Rights are restricted, the maintainer
   currently cannot access it from his laptop, and the runner VM is broken, so **every deploy pipeline is red and the app is down**.
2. **Structural mismatch of the F1 plan.** Microsoft documents max **5 WebSockets per instance** and **60 CPU-minutes/day** for
   App Service Free (Linux). One host tab + player + next-song view + playlist + a few singers already exceeds this. B1 would cost ≈ 11–13 €/month.
3. **Over-engineering.** Private Endpoint is ineffective (F1 has no VNet integration; SQL public access is enabled), Key Vault/Managed
   Identity/App Insights/Azure SQL exist for data that is **ephemeral by design**.
4. **EF Core migration pain.** Two providers (SQLite locally, Azure SQL in prod) plus a persistent prod database caused repeated
   migration problems. This pain only exists because a persistent multi-provider schema is maintained.

### Facts about the application that drive the decision

- Backend data is **ephemeral**: sessions, songs, playlists live ≤ 30 minutes after the last heartbeat (`SessionConstants.DefaultTtlMinutes`,
  `SessionCleanupService`). There is no long-term server-side data.
- The 26 GB karaoke library (≈ 6,900 files: 616 videos, 3,002 MP3s) **never leaves the host browser** (File System Access API). Only artist/title/duration
  metadata of ≈ 3,600 songs (< 2 MB JSON) is uploaded to the backend.
- Authentication is **stateless**: HMAC tokens derived from `SessionId` + one secret (`TokenService`). No identity provider, no user database.
- The hub keeps in-process per-session locks (`PlaylistHub._sessionLocks`) and SignalR groups in memory ⇒ the backend is **single-instance by design**.
- Singers join from phones on arbitrary networks ⇒ the backend must be publicly reachable. "Local mode" means development only (`dotnet run` + SQLite).
- Known client bug: `signalRBridge.js` calls `JoinSession` only after the first `start()`, **not after automatic reconnects** ⇒ after a reconnect the client silently
  receives no more updates. Also the default `withAutomaticReconnect()` gives up after ~45 s, which is shorter than a Render cold start (~60 s).

## Decision drivers

1. Permanently free (no recurring cost, no surprise bills)
2. Low maintenance (no VM, no patching, no cloud IAM puzzle)
3. Public, supports WebSockets/SignalR
4. Keep the .NET code base (rewrite only if it clearly simplifies operations)
5. Security must not get worse than today
6. Hosting address may change; no custom domain required

## Options considered

| # | Option | Verdict | Reason |
|---|--------|---------|--------|
| A | Stay on Azure (fix subscription, keep F1) | ✗ | F1 limits (5 WebSockets) unfit for SignalR; subscription not under maintainer's control |
| B | Azure App Service B1 / personal subscription | ✗ | Costs ≈ 11–13 €/month |
| C | Azure Container Apps (personal sub, consumption free grants 180k vCPU-s, 360k GiB-s, 2M req/month) | fallback-2 | Possible, but needs registry/Log Analytics (side costs) and again Azure administration |
| D | Azure Functions + Azure SignalR Service | ✗ | Requires rewriting the 46 KB hub |
| E | **Cloudflare static hosting + Render Free container (.NET kept)** | **✔ chosen** | See Decision |
| F | Google Cloud Run (same Docker image) | **fallback-1** | Fast cold start (~2 s), generous free tier, but needs credit card, no hard cost cap, WebSocket request limit 60 min |
| G | Oracle Cloud Always Free VM | ✗ | Maintenance (patching, TLS, firewall) contradicts goal 2; idle Always-Free instances may be reclaimed — realistic at 2–3 users/day |
| H | Cloudflare Containers | ✗ | Not free (Workers Paid plan) |
| I | Fly.io / Koyeb / Railway / Hugging Face Docker | ✗ | No permanent free tier anymore (checked Nov 2026) |
| J | Cloudflare Workers + Durable Objects (one actor per session) | **long-term target** | Architecturally the cleanest fit (no cold start, one deploy unit) but requires a TypeScript rewrite of hub, repositories and tests with little operational gain over E today |
| K | P2P/WebRTC | ✗ | Still needs a signalling server; rewrite |

## Decision

1. **Frontend**: static Blazor WASM on **Cloudflare** (Workers Static Assets; Pages is an equivalent alternative). No credit card. Deployed from GitHub Actions
   with a narrowly scoped API token.
2. **Backend**: ASP.NET Core in a **Docker container on Render Free Web Service, region Frankfurt**, defined by `render.yaml`. WebSocket messages count as traffic,
   so an active session keeps the service awake; idle > 15 min spins it down (cold start ≈ 1 min). Without a payment method Render **suspends** instead of billing (hard cost cap).
3. **Database**: **SQLite only**, file in the container (`/tmp`), schema created at startup (`EnsureDeleted()` + `EnsureCreated()`); **EF migrations, SQL Server provider, Azure.Identity,
   Managed-Identity code are removed**. Because all data is ephemeral, a schema upgrade problem can no longer occur.
4. **Secrets**: only `Karamel__TokenSecret` (Render environment variable). No Key Vault.
5. **Observability**: stdout logs (Render log stream). Application Insights and the IP-masking initializer are removed (fewer data processors).
6. **CI/CD**: one workflow on GitHub-hosted runners (tests, WASM publish, deploy frontend). Render deploys the backend from `main` (auto-deploy after checks pass, if supported). The self-hosted runner and all Bicep/PowerShell infra are deleted.
7. **Frontend and backend stay separate** so that the Render cold start never blocks the page load for strangers visiting the demo link. The frontend warms up the backend (`/health` polling + "server is starting" banner).
8. **Hardening that replaces what Azure never provided anyway**: rate limiting, body-size limits, session cap, mandatory CORS origin from configuration, SignalR
   reconnect with re-join and long retry window, stateless session restore after backend restarts, normalized search key (SQLite `LIKE` is only ASCII case-insensitive).
9. The Docker image stays **host-neutral** (configuration only via environment variables) so that moving to Cloud Run (fallback-1) requires no code changes.

## Validation (gate G1) — done BEFORE the full migration

Render Free is "best effort, no SLA, 0.1 CPU / 512 MB RAM (per Render docs), may restart at any time". Therefore a **spike on Render with a performance evaluation precedes everything else**:

- Minimal code changes only (SQLite `EnsureCreated`, `.dockerignore`, Render service from a feature branch).
- A reproducible load/latency harness (`tools/perf`) measures cold start, REST latency, bulk upload of ≈ 3,700 songs, search latency, SignalR broadcast latency with ~20 clients,
  WebSocket-keeps-awake behaviour, memory.
- A manual end-to-end test with the maintainer's real library (6,895 files, 26 GB) from a local frontend against the Render backend, with a phone on mobile data.
- Pass thresholds are defined in `plan-hostingMigration-00-overview.md` (section "Gate G1"). If G1 fails after the documented mitigations, execute **fallback-1 (Cloud Run)** with the same image, then re-run the same harness.

No hardening or refactoring work starts before G1 is decided.

## Consequences

### Positive

- Cost stays 0 €, with a hard cap (Render suspends instead of billing; Cloudflare static assets are free).
- No dependency on an employer-owned subscription; the project survives a job change.
- Infrastructure-as-code shrinks from ~400 lines of Bicep/PowerShell + VM to a `render.yaml` and a small `wrangler` config.
- No EF migrations, no migration workflow, no multi-provider drift.
- Fewer secrets and no deploy-capable VM identity ⇒ smaller attack surface; no telemetry service holding visitor data.
- Same Docker image can move to another host.

### Negative / accepted risks

- **No SLA.** Render may restart the free instance at any time; sessions are lost on restart. Mitigated by client-side reconnect + stateless session restore (token-verified), but singer queue replay is best effort.
- **Cold start ≈ 1 min** after > 15 min idle. Mitigated by warm-up while the host scans the folder, plus an explicit "server is starting" banner.
- **0.1 CPU** may make .NET start-up/JIT and bursts (bulk upload, fuzzy search) slow ⇒ validated by gate G1.
- Public unauthenticated session creation can be abused; a bot can make Render suspend the service for the rest of the month (no bill, but downtime). Mitigated by rate limiting, caps and body limits.
- Single-instance design is now permanent (free tier cannot scale out); documented as a constraint.
- Render and Cloudflare are US companies; personal data is limited to singer first names held ≤ 30 min in RAM/SQLite in Frankfurt; logs must not contain tokens/IPs beyond platform defaults.

### Neutral

- Search behaviour changes slightly because SQLite replaces SQL Server; handled by a normalized search key and tests.
- Public URLs change (README demo link must be updated).

## Revisit triggers

Re-evaluate this ADR (go to fallback-1 or option J) if any of the following happens: Render removes/limits the free web service; gate-G-like thresholds regress in monthly spot checks;
sessions are interrupted by restarts more than occasionally; or monthly usage grows beyond what a single free instance can serve.

## References (checked 2026-11-06/07)

- Render Free: https://render.com/docs/free
- App Service limits (WebSockets per instance, Linux Free = 5): https://learn.microsoft.com/azure/azure-resource-manager/management/azure-subscription-service-limits
- App Service pricing: https://azure.microsoft.com/pricing/details/app-service/linux/
- Azure Container Apps pricing: https://azure.microsoft.com/pricing/details/container-apps/
- Cloud Run pricing: https://cloud.google.com/run/pricing
- Cloudflare Pages limits: https://developers.cloudflare.com/pages/platform/limits/
- Koyeb pricing (no free tier): https://www.koyeb.com/pricing
- The draft was reviewed independently by two other LLMs (GPT-5.5, DeepSeek-V4-Pro); accepted changes: reconnect/self-healing before cutover, abuse protection in the first phase, SQLite search semantics, cold-start banner, Workers Static Assets instead of Pages, explicit performance gate. Rejected: Oracle Always Free VM as primary (maintenance, idle reclaim), Cloudflare Containers (not free).
