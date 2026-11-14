# Karamel performance harness

Requires Node.js 20 or newer. From this directory:

```powershell
npm install
npm test
node run.mjs --help
```

Run the local baseline against the Development backend:

```powershell
dotnet run --project ..\..\Karamel.Backend
node run.mjs --base http://127.0.0.1:5001 --scenario all --label local --out results\local.json
```

Available scenarios:

- `coldstart` polls `/health` until it responds with HTTP 200. The service must have been idle for at least 20 minutes.
- `rest` measures 20 sequential session creations.
- `bulk` measures three sequential uploads and one set of three parallel uploads.
- `search` measures library pages, searches, artist lists, and the umlaut functional check.
- `hub` measures SignalR connect/join times, 60 playlist broadcasts, concurrent adds, and disconnects.
- `wsidle` holds one WebSocket open without HTTP traffic for the requested number of minutes.
- `all` runs `rest`, `bulk`, `search`, and `hub` (not `coldstart` or `wsidle`).

Options include `--songs 3700`, `--clients 20`, `--minutes 20`, `--label`, and `--out`. Use only approved non-Azure test endpoints. The output contains aggregate measurements and never includes session tokens or session IDs. Cold-start and idle runs take at least as long as their specified wait periods; `all` can also take several minutes.
