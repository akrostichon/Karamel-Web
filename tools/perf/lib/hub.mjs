import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel
} from '@microsoft/signalr';
import { performance } from 'node:perf_hooks';

export function createPlaylistConnection(base, token) {
  return new HubConnectionBuilder()
    .withUrl(`${base}/hubs/playlist`, {
      transport: HttpTransportType.WebSockets,
      accessTokenFactory: () => token,
      headers: { 'X-Link-Token': token }
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

export function createHubClient(connection, onUnexpectedDisconnect) {
  const namesSeenAt = new Map();
  const listeners = new Set();
  let closing = false;

  const notify = () => {
    for (const listener of listeners) {
      listener();
    }
  };

  connection.on('ReceivePlaylistUpdated', dto => {
    const items = dto?.items ?? dto?.Items ?? [];
    for (const item of items) {
      const singerName = item?.singerName ?? item?.SingerName;
      if (typeof singerName === 'string') {
        namesSeenAt.set(singerName, performance.now());
      }
    }
    notify();
  });
  connection.onclose(() => {
    if (!closing) onUnexpectedDisconnect();
  });
  connection.onreconnecting(() => {
    if (!closing) onUnexpectedDisconnect();
  });

  return {
    connection,
    namesSeenAt,
    markClosing() {
      closing = true;
    },
    onUpdate(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    }
  };
}

export async function waitForClients(clients, predicate, timeoutMs) {
  const deadline = performance.now() + timeoutMs;
  while (!predicate()) {
    const remaining = deadline - performance.now();
    if (remaining <= 0) return false;

    let resolveChange;
    const changed = new Promise(resolve => { resolveChange = resolve; });
    const unsubscribe = clients.map(client => client.onUpdate(resolveChange));
    if (predicate()) {
      unsubscribe.forEach(removeListener => removeListener());
      return true;
    }

    let timeout;
    try {
      await Promise.race([
        changed,
        new Promise(resolve => { timeout = setTimeout(resolve, remaining); })
      ]);
    } finally {
      clearTimeout(timeout);
      unsubscribe.forEach(removeListener => removeListener());
    }
  }
  return true;
}
