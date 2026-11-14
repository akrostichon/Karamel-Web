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

## Gate G1 verdict (round 1)

Worst available Render measurements from the two recorded `all` runs and applicable standalone scenarios:

| Metric | Worst Render value | Hard limit | Target | PASS / FAIL / PASS(hard only) |
|---|---:|---:|---:|---|
| M1 — Cold start | 42.96 s (2 of 3 samples) | ≤120 s | ≤75 s | PASS |
| M2 — Session creation p95 | 455.46 ms | ≤1,000 ms | ≤400 ms | PASS(hard only) |
| M3 — Sequential bulk upload max | 13,169.20 ms | ≤15,000 ms | ≤8,000 ms | PASS(hard only) |
| M4a — Library pages p95 | 136.89 ms | ≤600 ms | ≤300 ms | PASS |
| M4b — Search p95 | 490.56 ms | ≤1,000 ms | ≤500 ms | PASS |
| M5 — Broadcast p95 / max / unexpected disconnects | 756.03 ms / 1,239.77 ms / 0 | ≤1,500 ms / ≤4,000 ms / 0 | p95 ≤700 ms | PASS(hard only) |
| M6 — WebSocket-only idle health check | HTTP 200 in 203.78 ms; 0 disconnects | HTTP 200 within 2,000 ms | — | PASS |
| M7 — Peak memory | NOT MEASURED | ≤450 MB | ≤300 MB | FAIL |
| M8 — HTTP 5xx / `database is locked` | 0 / 0 reported | 0 / 0 | — | PASS |

F1 and R1 are informational:

- Umlaut searches worked, but the casing check returned counts `6, 0, 0, 0` for `Ärzte`, `ärzte`, `aerzte`, and `Arzte`.
- Session startup felt delayed, likely due to cold start; the user considered the delay acceptable and suggested warming during library search.
- During manual redeployment, adding a song led to a blank NextSong screen and then “No Song Selected”; the app did not explain that the session was gone.
- The user has not explicitly judged the overall R1 experience “usable”; phone/mobile-data, duration, and other listed test details were not reported.

Step 2 outcome: **MITIGATE**, because M7 is not measured and is treated as FAIL by the plan. M6 did not fail.

## Gate G1 environment-variable mitigation re-run

The user reports setting the environment variables listed in plan 03 mitigation items 1 and 2, including `Logging__LogLevel__Default=Warning`. The following `all` run was performed after those changes. Because the settings were applied before this measurement, the individual effect of logging versus GC/JIT settings cannot be isolated. The user previously reported Render dashboard memory metrics unavailable.

Raw output: ignored `tools/perf/results/render-g1-round-1.json`.

| Scenario | Metric | Result |
|---|---|---|
| rest | createSession, n=20 | p50 52.61 ms; p95 102.78 ms; max 1,444.40 ms; 20×201 |
| rest | HTTP 5xx | 0 |
| bulk | bulkUpload, n=3 | p50 6,764.09 ms; max 7,026.02 ms; all 6 uploads HTTP 202 |
| bulk | parallel3Ms / HTTP 5xx | 17,237.84 ms / 0 |
| search | libraryPages, n=100 | p50 50.97 ms; p95 102.24 ms |
| search | searches, n=60 | p50 119.18 ms; p95 323.56 ms |
| search | artistLists, n=20 | p50 49.21 ms; p95 75.29 ms |
| search | F1 counts (`Ärzte`, `ärzte`, `aerzte`, `Arzte`) / HTTP 5xx | 6, 0, 0, 0 / 0 |
| hub | broadcast latency, n=60 | p50 345.42 ms; p95 493.21 ms; max 637.62 ms |
| hub | broadcast failures / unexpected disconnects / HTTP 5xx | 0 / 0 / 0 |
| hub | connect / `JoinSession`, p50 | 406.49 ms / 961.49 ms |
| hub | parallel burst | 5,208.03 ms; complete |

The measured M2–M5 values improved relative to the original Render runs, but this run cannot attribute the change to a specific environment-variable group. M7 remains unmeasured and therefore FAIL per the gate rules; the Render dashboard did not expose memory metrics to the user. The final round 2 verdict remains pending the ReadyToRun redeployment and re-measurement.

## ReadyToRun deployment build issue

The first Render build with ReadyToRun failed with `NETSDK1094`: restore had been run for `linux-x64` without `PublishReadyToRun=true`, while publish enabled ReadyToRun and used `--no-restore`. The restore command was updated to include `-p:PublishReadyToRun=true`. The matching Linux RID restore and ReadyToRun publish then completed locally; the Render image build still needs to be retried after pushing this correction.
