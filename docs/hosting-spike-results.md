# Hosting spike results

## Run information

- Date recorded: 2026-11-14
- Render service: `karamel-web.onrender.com`, Frankfurt, Free Web Service
- Harness: `tools/perf`, Node.js ES modules; revision `e0a0c78`.
- Raw harness output: ignored files under `tools/perf/results/` (`local.json`, `render-all-1.json`, `render-all-2.json`, `render-coldstart-1.json`, `render-coldstart-2.json`, `render-wsidle.json`). These files are local-only and are not committed.

The limits and targets below are the proposed measurements from the hosting overview. This report records measurements and observations only; it does not assign verdicts.

## Metrics

| Metric | Hard limit | Target | Local baseline | Render run 1 | Render run 2 | Render run 3 | Verdict-free note |
|---|---:|---:|---|---|---|---|---|
| M1 — Cold start, first `/health` HTTP 200 after ≥20 min idle | ≤120 s | ≤75 s | Not run | 23.14 s; first attempt, 0 network failures, 0 HTTP 5xx | 42.96 s; first attempt, 0 network failures, 0 HTTP 5xx | Not run | Two cold-start samples were collected; one additional sample remains outstanding. |
| M2 — Warm `POST /api/sessions`, n=20 | p95 ≤1,000 ms | p95 ≤400 ms | p95 23.02 ms; max 27.07 ms; 20×201 | p95 455.46 ms; max 2,841.85 ms; 20×201 | p95 153.75 ms; max 155.48 ms; 20×201 | — | Render run 1 had a substantially longer tail than run 2. |
| M3 — Bulk upload of 3,700 songs | ≤15 s | ≤8 s | n=3; p50 1,280.90 ms; max 1,576.98 ms | n=3; p50 11,795.43 ms; max 13,169.20 ms | n=3; p50 4,546.89 ms; max 4,915.39 ms | — | All uploads returned HTTP 202. Parallel-three total: local 4,231.10 ms; Render 33,617.96 ms / 12,107.26 ms. |
| M4a — Library page requests, n=100 | p95 ≤600 ms | p95 ≤300 ms | p50 12.27 ms; p95 20.69 ms | p50 94.34 ms; p95 136.89 ms | p50 78.27 ms; p95 109.53 ms | — | — |
| M4b — Search requests, n=60 | p95 ≤1,000 ms | p95 ≤500 ms | p50 25.68 ms; p95 71.60 ms | p50 208.49 ms; p95 490.56 ms | p50 97.57 ms; p95 332.52 ms | — | Search scenario HTTP 5xx: 0 in local and both Render runs. |
| M5 — SignalR broadcast latency, 20 clients, n=60 | p95 ≤1,500 ms; max ≤4,000 ms; 0 unexpected disconnects | p95 ≤700 ms | p50 53.17 ms; p95 98.06 ms; max 131.44 ms; 0 failures / disconnects | p50 153.94 ms; p95 756.03 ms; max 1,239.77 ms; 0 failures / disconnects | p50 203.92 ms; p95 736.25 ms; max 906.78 ms; 0 failures / disconnects | — | Parallel burst completed in 717.28 ms locally and 7,284.65 / 4,730.64 ms on Render. |
| M6 — WebSocket-only idle for 20 min, then `/health` | HTTP 200 within 2 s | — | Not run | HTTP 200 in 203.78 ms; 0 disconnects / HTTP 5xx | — | — | — |
| M7 — Peak Render memory during M2–M5 | ≤450 MB | ≤300 MB | — | Not available | Not available | — | Memory metrics were unavailable on the current dashboard/plan. |
| M8 — Server errors during scenarios | 0 HTTP 5xx; 0 `database is locked` log entries | — | 0 HTTP 5xx reported | 0 HTTP 5xx reported | 0 HTTP 5xx reported | — | User reported no `database is locked` entries or HTTP 5xx in Render logs. |
| F1 — Search count for `Ärzte`, `ärzte`, `aerzte`, `Arzte` | Informational; document counts | Equal counts for the first two terms | 6, 0, 0, 0 | 6, 0, 0, 0 | 6, 0, 0, 0 | — | Counts are in the term order shown. |

### Other harness timings

| Scenario | Local baseline | Render run 1 | Render run 2 |
|---|---|---|---|
| SignalR connect p50 | 53.45 ms | 1,489.75 ms | 1,162.01 ms |
| SignalR `JoinSession` p50 | 13.85 ms | 1,213.48 ms | 69.43 ms |
| Artist list requests, n=20 | p50 17.21 ms; p95 44.07 ms | p50 80.42 ms; p95 112.46 ms | p50 95.22 ms; p95 122.15 ms |

## R1 — Real-library test, session 4

User observations:

- The real-library scan went well. Session startup took noticeably longer, likely because of a cold start; the user considered the delay acceptable. Warming the server while the library is being searched may make the delay unnoticeable.
- Searching with umlauts worked. A search casing issue was observed.
- During manual redeployment, a song was playing. After another song was added to the playlist, the app navigated to the NextSong screen without showing a title, then to the player, which displayed “No Song Selected”.
- The app did not notify the user that the session was gone.

Scan duration, upload duration, browser console errors, phone/mobile-data behavior, and whether three songs played or advanced were not reported.

## Observations / surprises

- The first local run reused song IDs across separate sessions; SQLite rejected the duplicates. The bulk scenario was changed to generate fresh IDs per session, and the rerun completed with uploads accepted.
- A PowerShell health probe using `localhost` did not reach the listener; direct loopback at `127.0.0.1` worked.
- Two cold-start samples are present in the raw results; the third requested sample was not collected.
- The Render dashboard did not expose peak memory metrics on the current plan. The user observed no `database is locked` entries or HTTP 5xx in the logs.
- Session 4 exposed a redeployment/session-loss UX issue: after adding a song, navigation continued without the song title, ended at “No Song Selected”, and did not explain that the session had been lost.
