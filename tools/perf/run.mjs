import { mkdir, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';
import { parseArgs } from 'node:util';
import { performance } from 'node:perf_hooks';
import { createSyntheticLibrary } from './lib/data.mjs';
import { expectStatus, timedFetch as fetchTimed } from './lib/http.mjs';
import { createHubClient, createPlaylistConnection, waitForClients } from './lib/hub.mjs';
import { summarize } from './lib/stats.mjs';

const usage = `Usage: node run.mjs [options]

Options:
  --base <url>       Backend base URL (default: http://localhost:5001)
  --scenario <name>  coldstart, rest, bulk, search, hub, wsidle, or all (default: all)
  --songs <count>    Synthetic library size (default: 3700)
  --clients <count>  SignalR clients for hub (default: 20)
  --minutes <count>  WebSocket idle duration (default: 20)
  --label <name>     Result label
  --out <file>       Write aggregate results as JSON
  --help             Show this help
`;

const createSessionBody = {
  requireSingerName: false,
  pauseBetweenSongsSeconds: 10,
  allowSingersToReorder: false,
  theme: null
};

const searchTerms = [
  'queen', 'ramm', 'die ärzte', 'ÄRZTE', 'rammstien',
  'ueber den wolken', 'über den wolken', 'helene fischer',
  'moetley', 'strasse'
];

let scenarioHttp5xx = 0;

async function timedFetch(...args) {
  const response = await fetchTimed(...args);
  if (response.status >= 500) scenarioHttp5xx += 1;
  return response;
}

function parseOptions(args) {
  const { values } = parseArgs({
    args,
    options: {
      base: { type: 'string', default: 'http://localhost:5001' },
      scenario: { type: 'string', default: 'all' },
      songs: { type: 'string', default: '3700' },
      clients: { type: 'string', default: '20' },
      minutes: { type: 'string', default: '20' },
      label: { type: 'string', default: 'unspecified' },
      out: { type: 'string' },
      help: { type: 'boolean', short: 'h' }
    },
    allowPositionals: false,
    strict: true
  });

  if (values.help) return { help: true };
  const scenarios = ['coldstart', 'rest', 'bulk', 'search', 'hub', 'wsidle', 'all'];
  if (!scenarios.includes(values.scenario)) {
    throw new Error(`Unknown scenario "${values.scenario}"`);
  }

  const baseUrl = new URL(values.base);
  if (!['http:', 'https:'].includes(baseUrl.protocol) || baseUrl.username || baseUrl.password || baseUrl.search || baseUrl.hash) {
    throw new Error('--base must be an HTTP(S) URL without credentials, query, or fragment');
  }

  const parsePositiveInteger = (name, value, allowZero = false) => {
    const parsed = Number(value);
    if (!Number.isInteger(parsed) || parsed < (allowZero ? 0 : 1)) {
      throw new Error(`--${name} must be a ${allowZero ? 'non-negative' : 'positive'} integer`);
    }
    return parsed;
  };

  return {
    base: values.base.replace(/\/+$/, ''),
    scenario: values.scenario,
    songs: parsePositiveInteger('songs', values.songs),
    clients: parsePositiveInteger('clients', values.clients),
    minutes: parsePositiveInteger('minutes', values.minutes),
    label: values.label,
    out: values.out
  };
}

function requireResponse(response, expectedStatus, operation) {
  expectStatus(response, expectedStatus, operation);
  return response.body;
}

async function createSession(base) {
  const response = await timedFetch(base, '/api/sessions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(createSessionBody)
  });
  if (!response.body?.id) {
    requireResponse(response, 201, 'Session creation');
    throw new Error('Session creation response did not contain a session ID');
  }
  return { ...response.body, responseStatus: response.status };
}

async function endSession(base, session) {
  const response = await timedFetch(base, `/api/sessions/${session.id}/end`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ force: true })
  });
  if (response.status !== 200) {
    throw new Error(`Session cleanup returned HTTP ${response.status}; expected 200`);
  }
}

async function withSession(base, action) {
  let session;
  let actionError;
  try {
    session = await createSession(base);
    requireResponse({ status: session.responseStatus }, 201, 'Session creation');
    if (!session.adminToken || !session.singerToken) {
      throw new Error('Session creation response did not contain the expected tokens');
    }
    return await action(session);
  } catch (error) {
    actionError = error;
    throw error;
  } finally {
    if (session) {
      try {
        await endSession(base, session);
      } catch (cleanupError) {
        if (actionError) {
          throw new AggregateError([actionError, cleanupError], 'Scenario failed and session cleanup also failed');
        }
        throw cleanupError;
      }
    }
  }
}

function sessionLibraryPath(session) {
  return `/api/sessions/${session.id}/library`;
}

async function uploadLibrary(base, session, songs, { measuredUpload = false } = {}) {
  const response = await timedFetch(base, `${sessionLibraryPath(session)}/bulk`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Link-Token': session.adminToken
    },
    body: JSON.stringify(songs)
  });
  if (measuredUpload && response.status !== 202) {
    return response;
  }
  requireResponse(response, 202, 'Bulk library upload');

  const verify = await timedFetch(base, `${sessionLibraryPath(session)}?page=1&pageSize=1`);
  requireResponse(verify, 200, 'Library verification');
  if (verify.body?.totalCount !== songs.length) {
    throw new Error(`Library verification returned ${verify.body?.totalCount ?? 'no count'} songs; expected ${songs.length}`);
  }
  return response;
}

function summarizeRequests(requests) {
  return summarize(requests.map(request => request.ms));
}

async function runColdstart(base) {
  console.log('Reminder: Service must have been idle ≥ 20 minutes.');
  const startedAt = performance.now();
  let attempts = 0;
  let networkFailures = 0;
  let firstStatusSeen = null;

  while (true) {
    attempts += 1;
    try {
      const response = await timedFetch(base, '/health', { timeoutMs: 130_000 });
      if (firstStatusSeen === null) firstStatusSeen = response.status;
      if (response.status === 200) {
        return {
          coldStartMs: performance.now() - startedAt,
          attempts,
          firstStatusSeen,
          networkFailures
        };
      }
    } catch {
      networkFailures += 1;
    }
    await new Promise(resolve => setTimeout(resolve, 2000));
  }
}

async function runRest(base) {
  const warmup = await timedFetch(base, '/health');
  requireResponse(warmup, 200, 'Health warm-up');
  const requests = [];

  for (let index = 0; index < 20; index += 1) {
    const response = await timedFetch(base, '/api/sessions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(createSessionBody)
    });
    requests.push(response);
    if (response.body?.id) {
      await endSession(base, response.body);
    }
  }

  return {
    createSession: summarizeRequests(requests),
    responseStatuses: requests.reduce((counts, request) => {
      counts[request.status] = (counts[request.status] ?? 0) + 1;
      return counts;
    }, {})
  };
}

async function runBulk(base, songCount) {
  const sequentialMs = [];
  const uploadStatuses = [];
  for (let index = 0; index < 3; index += 1) {
    await withSession(base, async session => {
      const songs = createSyntheticLibrary(songCount);
      const upload = await uploadLibrary(base, session, songs, { measuredUpload: true });
      sequentialMs.push(upload.ms);
      uploadStatuses.push(upload.status);
    });
  }

  const parallelStartedAt = performance.now();
  const parallelUploads = await Promise.all(Array.from({ length: 3 }, () =>
    withSession(base, session => uploadLibrary(
      base,
      session,
      createSyntheticLibrary(songCount),
      { measuredUpload: true }
    ))
  ));
  const parallel3Ms = performance.now() - parallelStartedAt;
  return {
    bulkUpload: summarize(sequentialMs),
    bulkResponseStatuses: [...uploadStatuses, ...parallelUploads.map(upload => upload.status)],
    parallel3Ms
  };
}

async function runSearch(base, songCount) {
  const songs = createSyntheticLibrary(songCount);
  return withSession(base, async session => {
    await uploadLibrary(base, session, songs);

    const pageRequests = [];
    for (let index = 0; index < 100; index += 1) {
      const page = (index % 20) + 1;
      const response = await timedFetch(base, `${sessionLibraryPath(session)}?page=${page}&pageSize=50`);
      pageRequests.push(response);
    }

    const searchRequests = [];
    for (let index = 0; index < 60; index += 1) {
      const term = searchTerms[index % searchTerms.length];
      const query = new URLSearchParams({ page: '1', pageSize: '50', search: term });
      const response = await timedFetch(base, `${sessionLibraryPath(session)}?${query}`);
      searchRequests.push(response);
    }

    const functionalCounts = {};
    for (const term of ['Ärzte', 'ärzte', 'aerzte', 'Arzte']) {
      const query = new URLSearchParams({ page: '1', pageSize: '1', search: term });
      const response = await timedFetch(base, `${sessionLibraryPath(session)}?${query}`);
      functionalCounts[term] = response.body?.totalCount;
    }

    const artistRequests = [];
    for (let index = 0; index < 20; index += 1) {
      const response = await timedFetch(base, `${sessionLibraryPath(session)}/artists`);
      artistRequests.push(response);
    }

    return {
      libraryPages: summarizeRequests(pageRequests),
      searches: summarizeRequests(searchRequests),
      artistLists: summarizeRequests(artistRequests),
      functionalCounts
    };
  });
}

function randomIndex(random, length) {
  return Math.floor(random() * length);
}

function seededRandom(seed) {
  let state = seed >>> 0;
  return () => {
    state += 0x6D2B79F5;
    let value = state;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };
}

async function measureConnection(connection) {
  const startedAt = performance.now();
  await connection.start();
  return performance.now() - startedAt;
}

async function measureJoin(client, sessionId) {
  const startedAt = performance.now();
  await client.connection.invoke('JoinSession', sessionId);
  return performance.now() - startedAt;
}

async function runHub(base, songCount, clientCount) {
  const songs = createSyntheticLibrary(songCount);
  return withSession(base, async session => {
    await uploadLibrary(base, session, songs);
    let disconnects = 0;
    const clients = Array.from({ length: clientCount }, (_, index) => {
      const token = index === 0 ? session.adminToken : session.singerToken;
      return createHubClient(
        createPlaylistConnection(base, token),
        () => { disconnects += 1; }
      );
    });

    try {
      const connectTimes = await Promise.all(clients.map(client => measureConnection(client.connection)));
      const joinTimes = await Promise.all(clients.map(client => measureJoin(client, session.id)));
      const random = seededRandom(0x485542);
      const broadcastLatencies = [];
      let failures = 0;
      let invokeFailures = 0;

      for (let round = 0; round < 60; round += 1) {
        const senderIndex = randomIndex(random, clientCount);
        const sender = clients[senderIndex];
        const song = songs[randomIndex(random, songs.length)];
        const singerName = `perf-${round}`;
        const broadcastStartedAt = performance.now();

        try {
          await sender.connection.invoke('AddItemAsync', session.id, song.id, singerName);
        } catch {
          invokeFailures += 1;
          failures += 1;
          await new Promise(resolve => setTimeout(resolve, 100));
          continue;
        }
        const receivers = clients.filter((_, index) => index !== senderIndex);
        const received = await waitForClients(
          receivers,
          () => receivers.every(client => client.namesSeenAt.has(singerName)),
          15_000
        );
        if (received) {
          broadcastLatencies.push(
            Math.max(...receivers.map(client => client.namesSeenAt.get(singerName))) - broadcastStartedAt
          );
        } else {
          failures += 1;
        }
        await new Promise(resolve => setTimeout(resolve, 100));
      }

      const burstStartedAt = performance.now();
      const burstNames = clients.map((_, index) => `perf-burst-${index}`);
      const burstResults = await Promise.allSettled(clients.map((client, index) =>
        client.connection.invoke('AddItemAsync', session.id, songs[index % songs.length].id, burstNames[index])
      ));
      const burstInvokeFailures = burstResults.filter(result => result.status === 'rejected').length;
      const burstComplete = await waitForClients(
        clients,
        () => clients.every(client => burstNames.every(name => client.namesSeenAt.has(name))),
        20_000
      );

      return {
        broadcastLatency: summarize(broadcastLatencies),
        broadcastFailures: failures,
        broadcastInvokeFailures: invokeFailures,
        unexpectedDisconnects: disconnects,
        connect: summarize(connectTimes),
        joinSession: summarize(joinTimes),
        parallelBurstMs: burstComplete ? performance.now() - burstStartedAt : null,
        parallelBurstComplete: burstComplete && burstInvokeFailures === 0,
        parallelBurstInvokeFailures: burstInvokeFailures
      };
    } finally {
      for (const client of clients) client.markClosing();
      const stopResults = await Promise.allSettled(clients.map(client => client.connection.stop()));
      const failedStops = stopResults.filter(result => result.status === 'rejected');
      if (failedStops.length > 0) {
        throw new AggregateError(failedStops.map(result => result.reason), 'One or more SignalR connections failed to stop');
      }
    }
  });
}

async function runWsIdle(base, minutes) {
  return withSession(base, async session => {
    let disconnects = 0;
    const client = createHubClient(
      createPlaylistConnection(base, session.singerToken),
      () => { disconnects += 1; }
    );

    try {
      await client.connection.start();
      await client.connection.invoke('JoinSession', session.id);
      const durationMs = minutes * 60_000;
      const startedAt = performance.now();
      let nextProgress = 5 * 60_000;
      while (performance.now() - startedAt < durationMs) {
        const elapsed = performance.now() - startedAt;
        const waitMs = Math.min(durationMs - elapsed, nextProgress - elapsed);
        await new Promise(resolve => setTimeout(resolve, waitMs));
        if (performance.now() - startedAt >= nextProgress) {
          console.log(`WebSocket idle: ${Math.min(minutes, Math.floor((performance.now() - startedAt) / 60_000))} minute(s) elapsed.`);
          nextProgress += 5 * 60_000;
        }
      }

      const health = await timedFetch(base, '/health');
      return {
        idleMinutes: minutes,
        healthStatus: health.status,
        healthMs: health.ms,
        disconnects
      };
    } finally {
      client.markClosing();
      await client.connection.stop();
    }
  });
}

async function runScenario(name, options) {
  switch (name) {
    case 'coldstart':
      return runColdstart(options.base);
    case 'rest':
      return runRest(options.base);
    case 'bulk':
      return runBulk(options.base, options.songs);
    case 'search':
      return runSearch(options.base, options.songs);
    case 'hub':
      return runHub(options.base, options.songs, options.clients);
    case 'wsidle':
      return runWsIdle(options.base, options.minutes);
    default:
      throw new Error(`Unsupported scenario "${name}"`);
  }
}

function printResults(scenarios) {
  const rows = [];
  for (const [scenario, metrics] of Object.entries(scenarios)) {
    for (const [metric, value] of Object.entries(metrics)) {
      rows.push({
        Scenario: scenario,
        Metric: metric,
        Result: typeof value === 'object' ? JSON.stringify(value) : String(value)
      });
    }
  }
  console.table(rows);
}

async function main() {
  const options = parseOptions(process.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  const selectedScenarios = options.scenario === 'all'
    ? ['rest', 'bulk', 'search', 'hub']
    : [options.scenario];
  const startedAt = new Date().toISOString();
  const scenarios = {};

  for (const name of selectedScenarios) {
    console.log(`Running ${name} scenario against ${options.base}`);
    scenarioHttp5xx = 0;
    scenarios[name] = await runScenario(name, options);
    scenarios[name].http5xx = scenarioHttp5xx;
  }

  printResults(scenarios);
  if (options.out) {
    const output = {
      label: options.label,
      base: options.base,
      startedAt,
      scenarios
    };
    await mkdir(dirname(options.out), { recursive: true });
    await writeFile(options.out, `${JSON.stringify(output, null, 2)}\n`, 'utf8');
    console.log(`Results written to ${options.out}`);
  }
}

main().catch(error => {
  console.error(`Performance harness failed: ${error.message}`);
  process.exitCode = 1;
});
