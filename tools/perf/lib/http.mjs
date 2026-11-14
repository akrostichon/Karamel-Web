import { performance } from 'node:perf_hooks';

export async function timedFetch(base, path, options = {}) {
  const { timeoutMs, ...fetchOptions } = options;
  const requestOptions = { ...fetchOptions };

  if (timeoutMs !== undefined) {
    requestOptions.signal = AbortSignal.timeout(timeoutMs);
  }

  const startedAt = performance.now();
  let response;
  try {
    response = await fetch(`${base}${path}`, requestOptions);
  } catch (error) {
    const method = fetchOptions.method ?? 'GET';
    throw new Error(`${method} request failed: ${error.message}`, { cause: error });
  }
  const text = await response.text();
  const ms = performance.now() - startedAt;
  let body = text;

  if (text && response.headers.get('content-type')?.includes('application/json')) {
    try {
      body = JSON.parse(text);
    } catch {
      body = text;
    }
  } else if (!text) {
    body = null;
  }

  return { status: response.status, ms, body };
}

export function expectStatus(response, expectedStatus, operation) {
  if (response.status !== expectedStatus) {
    throw new Error(`${operation} returned HTTP ${response.status}; expected ${expectedStatus}`);
  }
}
