// fetchLibraryPage.test.js
// T027: fetchLibraryPage REST fallback tests
// Isolated in a dedicated file to avoid global state interference with signalRBridge.test.js
// (vi.unstubAllGlobals + cache-busted dynamic imports must not share a vitest worker with
// the outer describe's direct global.signalR assignment pattern)

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

describe('fetchLibraryPage REST fallback', () => {
    let fetchMock;

    beforeEach(() => {
        fetchMock = vi.fn();
        vi.stubGlobal('fetch', fetchMock);

        vi.stubGlobal('signalR', {
            HubConnectionBuilder: class {
                withUrl() { return this; }
                withAutomaticReconnect() { return this; }
                withServerTimeout() { return this; }
                withKeepAliveInterval() { return this; }
                build() {
                    return {
                        on: vi.fn(),
                        start: vi.fn().mockResolvedValue(undefined),
                        invoke: vi.fn().mockResolvedValue(undefined)
                    };
                }
            },
            HubConnectionState: {
                Disconnected: 0, Connecting: 1, Connected: 2, Disconnecting: 3, Reconnecting: 4
            }
        });

        vi.stubGlobal('BroadcastChannel', class {
            constructor() { this.onmessage = null; }
            postMessage() {}
            addEventListener() {}
            removeEventListener() {}
            close() {}
        });

        vi.stubGlobal('sessionStorage', {
            store: {},
            getItem(key) { return this.store[key] || null; },
            setItem(key, value) { this.store[key] = value; },
            removeItem(key) { delete this.store[key]; },
            clear() { this.store = {}; },
            get length() { return Object.keys(this.store).length; },
            key(i) { return Object.keys(this.store)[i] || null; }
        });
    });

    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('extracts items from object body (not plain array)', async () => {
        const { fetchLibraryPage } = await import('./signalRBridge.js?fetchtest1=' + Date.now());

        fetchMock.mockResolvedValueOnce({
            ok: true,
            headers: { get: () => '2' },
            json: async () => ({
                items: [
                    { id: 'song-1', artist: 'Artist A', title: 'Title A' },
                    { id: 'song-2', artist: 'Artist B', title: 'Title B' },
                ],
                totalCount: 2,
                page: 1,
                pageSize: 10,
                suggestions: []
            })
        });

        const result = await fetchLibraryPage('session-123', 1, 10);

        expect(result.items).toHaveLength(2);
        expect(result.items[0].artist).toBe('Artist A');
        expect(result.totalCount).toBe(2);
        expect(result.suggestions).toEqual([]);
    });

    it('maps suggestion objects to plain string array', async () => {
        const { fetchLibraryPage } = await import('./signalRBridge.js?fetchtest2=' + Date.now());

        fetchMock.mockResolvedValueOnce({
            ok: true,
            headers: { get: () => '0' },
            json: async () => ({
                items: [],
                totalCount: 0,
                page: 1,
                pageSize: 10,
                suggestions: [
                    { text: 'beyonce', sourceField: 'artist' },
                    { text: 'beatles', sourceField: 'artist' }
                ]
            })
        });

        const result = await fetchLibraryPage('session-123', 1, 10);

        expect(result.items).toHaveLength(0);
        expect(result.suggestions).toEqual(['beyonce', 'beatles']);
    });

    it('handles legacy plain-array response for backward compatibility', async () => {
        const { fetchLibraryPage } = await import('./signalRBridge.js?fetchtest3=' + Date.now());

        fetchMock.mockResolvedValueOnce({
            ok: true,
            headers: { get: vi.fn().mockReturnValue('2') },
            json: async () => [
                { id: 'song-1', artist: 'Artist A', title: 'Title A' },
                { id: 'song-2', artist: 'Artist B', title: 'Title B' }
            ]
        });

        const result = await fetchLibraryPage('session-123', 1, 10);

        expect(result.items).toHaveLength(2);
        expect(result.suggestions).toEqual([]);
    });

    it('returns empty result on HTTP error', async () => {
        const { fetchLibraryPage } = await import('./signalRBridge.js?fetchtest4=' + Date.now());

        fetchMock.mockResolvedValueOnce({
            ok: false,
            status: 500,
            text: async () => 'Internal Server Error'
        });

        const result = await fetchLibraryPage('session-123', 1, 10);

        expect(result.items).toEqual([]);
        expect(result.totalCount).toBe(0);
        expect(result.suggestions).toEqual([]);
    });
});
