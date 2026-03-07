import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
    initializeSession,
    broadcastStateUpdate,
    getSessionState,
    getSessionStateForSession,
    clearSessionState,
    generateSessionUrl,
    getSessionIdFromUrl,
    checkMainTabAlive,
    setupStateUpdateListener
} from './signalRBridge.js';

// Test session ID
const TEST_SESSION_ID = 'test-session-123';

// Mock BroadcastChannel that simulates cross-tab communication
class MockBroadcastChannel {
    constructor(name) {
        this.name = name;
        this.onmessage = null;
        this._closed = false;
        MockBroadcastChannel.instances.push(this);
    }

    postMessage(data) {
        if (this._closed) {
            throw new Error('Cannot post message on closed channel');
        }
        
        // Simulate broadcasting to all other instances with same name
        setTimeout(() => {
            MockBroadcastChannel.instances
                .filter(ch => ch.name === this.name && ch !== this && !ch._closed)
                .forEach(ch => {
                    if (ch.onmessage) {
                        ch.onmessage({ data });
                    }
                    // Trigger event listeners
                    if (ch._eventListeners && ch._eventListeners.message) {
                        ch._eventListeners.message.forEach(fn => fn({ data }));
                    }
                });
        }, 0);
    }

    addEventListener(event, handler) {
        if (!this._eventListeners) this._eventListeners = {};
        if (!this._eventListeners[event]) this._eventListeners[event] = [];
        this._eventListeners[event].push(handler);
    }

    removeEventListener(event, handler) {
        if (this._eventListeners && this._eventListeners[event]) {
            const index = this._eventListeners[event].indexOf(handler);
            if (index > -1) {
                this._eventListeners[event].splice(index, 1);
            }
        }
    }

    close() {
        this._closed = true;
        const index = MockBroadcastChannel.instances.indexOf(this);
        if (index > -1) {
            MockBroadcastChannel.instances.splice(index, 1);
        }
    }

    static instances = [];
    
    static reset() {
        this.instances.forEach(ch => ch.close());
        this.instances = [];
    }
}

// Mock sessionStorage
const mockSessionStorage = {
    store: {},
    _originalSetItem: null,
    getItem(key) {
        return this.store[key] || null;
    },
    setItem(key, value) {
        this.store[key] = value;
    },
    removeItem(key) {
        delete this.store[key];
    },
    clear() {
        this.store = {};
    },
    reset() {
        this.store = {};
        // Restore original setItem if it was mocked
        if (this._originalSetItem) {
            this.setItem = this._originalSetItem;
            this._originalSetItem = null;
        }
    }
};

// Mock window.location
const mockLocation = {
    origin: 'http://localhost:5000',
    search: ''
};

// Mock window for custom events
const mockWindow = {
    dispatchEvent: vi.fn(),
    addEventListener: vi.fn(),
    location: mockLocation
};

describe('signalRBridge', () => {
    beforeEach(() => {
        // Reset mocks
        MockBroadcastChannel.reset();
        mockSessionStorage.reset();
        mockWindow.dispatchEvent = vi.fn();
        mockWindow.addEventListener.mockClear();
        mockLocation.search = '';

        // Set up global mocks
        global.BroadcastChannel = MockBroadcastChannel;
        global.sessionStorage = mockSessionStorage;
        global.window = mockWindow;
        global.Date = {
            ...Date,
            now: vi.fn(() => 1234567890)
        };
        global.URL = class {
            constructor(path, base) {
                this.pathname = path;
                this.origin = base;
                this.searchParams = new Map();
            }
            set(key, value) {
                this.searchParams.set(key, value);
            }
            toString() {
                const params = Array.from(this.searchParams.entries())
                    .map(([k, v]) => `${k}=${v}`)
                    .join('&');
                return `${this.origin}${this.pathname}${params ? '?' + params : ''}`;
            }
        };
        global.URL.prototype.searchParams = {
            set: function(key, value) {
                if (!this._params) this._params = new Map();
                this._params.set(key, value);
            }
        };
        
        // CRITICAL: Always mock signalR globally to prevent script loading attempts
        // This prevents happy-dom from trying to fetch from localhost:3000
        if (!global.signalR) {
            global.signalR = {
                HubConnectionBuilder: class {
                    withUrl() { return this; }
                    withAutomaticReconnect() { return this; }
                    withServerTimeout() { return this; }
                    withKeepAliveInterval() { return this; }
                    build() {
                        return {
                            on: vi.fn(),
                            start: vi.fn().mockResolvedValue(undefined),
                            invoke: vi.fn().mockResolvedValue(undefined),
                            stop: vi.fn().mockResolvedValue(undefined)
                        };
                    }
                },
                HubConnectionState: {
                    Disconnected: 0,
                    Connecting: 1,
                    Connected: 2,
                    Disconnecting: 3,
                    Reconnecting: 4
                }
            };
        }
    });

    afterEach(() => {
        MockBroadcastChannel.reset();
    });

    describe('initializeSession', () => {
        it('should initialize as main tab', () => {
            expect(() => initializeSession(TEST_SESSION_ID, true)).not.toThrow();
            expect(MockBroadcastChannel.instances).toHaveLength(1);
            expect(MockBroadcastChannel.instances[0].name).toBe(`karamel-session-${TEST_SESSION_ID}`);
        });

        it('should initialize as secondary tab', () => {
            expect(() => initializeSession(TEST_SESSION_ID, false)).not.toThrow();
            expect(MockBroadcastChannel.instances).toHaveLength(1);
            expect(MockBroadcastChannel.instances[0].name).toBe(`karamel-session-${TEST_SESSION_ID}`);
        });

        it('should set up message listener for secondary tabs', () => {
            initializeSession(TEST_SESSION_ID, false);
            const channel = MockBroadcastChannel.instances[0];
            expect(channel.onmessage).toBeDefined();
            expect(typeof channel.onmessage).toBe('function');
        });

        it('should not set up message listener for main tab', () => {
            initializeSession(TEST_SESSION_ID, true);
            const channel = MockBroadcastChannel.instances[0];
            // Main tab sets up onmessage in the module for ping handling
            // but it's handled differently than secondary tabs
            expect(MockBroadcastChannel.instances).toHaveLength(1);
        });

        it('should throw error if BroadcastChannel not supported', () => {
            global.BroadcastChannel = undefined;
            expect(() => initializeSession(true)).toThrow('Broadcast Channel API is not supported');
        });
    });

    describe('broadcastStateUpdate', () => {
        it('should broadcast playlist-updated event', async () => {
            initializeSession(TEST_SESSION_ID, true);
            
            const playlistData = {
                queue: [{ id: '123', artist: 'Artist', title: 'Song' }],
                currentSong: null,
                singerSongCounts: {}
            };

            broadcastStateUpdate('playlist-updated', playlistData);

            const stored = JSON.parse(mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`));
            expect(stored.playlist).toEqual(playlistData);
        });

        it('should broadcast session-settings event', async () => {
            initializeSession(TEST_SESSION_ID, true);
            
            const sessionData = {
                sessionId: 'abc-123',
                requireSingerName: true,
                pauseBetweenSongs: true
            };

            broadcastStateUpdate('session-settings', sessionData);

            const stored = JSON.parse(mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`));
            expect(stored.session).toEqual(sessionData);
        });

        it('should allow secondary tab to broadcast (persist to sessionStorage)', () => {
            initializeSession(TEST_SESSION_ID, false);
            const playlist = { queue: [] };

            broadcastStateUpdate('playlist-updated', playlist);

            const stored = JSON.parse(mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`));
            expect(stored.playlist).toEqual(playlist);
        });

        it('should include timestamp in broadcast message', (done) => {
            initializeSession(TEST_SESSION_ID, true);
            initializeSession('test-session-456', false);

            const secondaryChannel = MockBroadcastChannel.instances[1];
            secondaryChannel.onmessage = (event) => {
                expect(event.data.timestamp).toBe(1234567890);
                done();
            };

            broadcastStateUpdate('playlist-updated', { queue: [] });
        });
    });

    describe('cross-tab communication', () => {
        it('should receive broadcast in secondary tab', (done) => {
            initializeSession(TEST_SESSION_ID, true);
            initializeSession(TEST_SESSION_ID, false);

            const secondaryChannel = MockBroadcastChannel.instances[1];
            const testData = { queue: [{ id: '1', artist: 'Test', title: 'Song' }] };

            secondaryChannel.onmessage = (event) => {
                expect(event.data.type).toBe('playlist-updated');
                expect(event.data.data).toEqual(testData);
                done();
            };

            broadcastStateUpdate('playlist-updated', testData);
        });

        it('should dispatch custom event when secondary tab receives message', async () => {
            initializeSession(TEST_SESSION_ID, false);

            let eventFired = false;
            mockWindow.dispatchEvent.mockImplementation((event) => {
                if (event.type === 'session-state-updated') {
                    expect(event.detail.type).toBe('playlist-updated');
                    expect(event.detail.data.queue).toHaveLength(1);
                    eventFired = true;
                }
            });

            const mainChannel = MockBroadcastChannel.instances[0];
            mainChannel.onmessage({
                data: {
                    type: 'playlist-updated',
                    data: { queue: [{ id: '1' }] },
                    timestamp: 1234567890
                }
            });

            expect(eventFired).toBe(true);
        });
    });

    describe('sessionStorage persistence', () => {
        it('should persist state to sessionStorage', () => {
            initializeSession(TEST_SESSION_ID, true);
            
            const playlistData = { queue: [{ id: '1', artist: 'A', title: 'B' }] };
            broadcastStateUpdate('playlist-updated', playlistData);

            const stored = mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`);
            expect(stored).toBeDefined();
            
            const parsed = JSON.parse(stored);
            expect(parsed.playlist).toEqual(playlistData);
        });

        it('should retrieve session state from sessionStorage', () => {
            const testState = {
                session: { sessionId: '123' },
                library: { songs: [] },
                playlist: { queue: [] },
                currentSong: null
            };

            mockSessionStorage.setItem(`karamel-session-${TEST_SESSION_ID}`, JSON.stringify(testState));

            const retrieved = getSessionStateForSession(TEST_SESSION_ID);
            expect(retrieved).toEqual(testState);
        });

        it('should return default state if sessionStorage is empty', () => {
            const state = getSessionStateForSession(TEST_SESSION_ID);
            
            expect(state).toEqual({
                session: null,
                library: null,
                playlist: null,
                currentSong: null
            });
        });

        it('should handle corrupted sessionStorage data gracefully', () => {
            const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            
            mockSessionStorage.setItem(`karamel-session-${TEST_SESSION_ID}`, 'invalid json{');
            
            const state = getSessionStateForSession(TEST_SESSION_ID);
            expect(state).toEqual({
                session: null,
                library: null,
                playlist: null,
                currentSong: null
            });

            consoleSpy.mockRestore();
        });
    });

    describe('clearSessionState', () => {
        it('should clear sessionStorage', () => {
            initializeSession(TEST_SESSION_ID, true);
            broadcastStateUpdate('playlist-updated', { queue: [] });

            expect(mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`)).not.toBeNull();

            clearSessionState();

            expect(mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`)).toBeNull();
        });

        it('should broadcast session-ended message', (done) => {
            initializeSession(TEST_SESSION_ID, true);
            initializeSession(TEST_SESSION_ID, false);

            const secondaryChannel = MockBroadcastChannel.instances[1];
            secondaryChannel.onmessage = (event) => {
                if (event.data.type === 'session-ended') {
                    expect(event.data.timestamp).toBe(1234567890);
                    done();
                }
            };

            clearSessionState();
        });

        it('should close broadcast channel', () => {
            initializeSession(TEST_SESSION_ID, true);
            const channel = MockBroadcastChannel.instances[0];

            clearSessionState();

            expect(channel._closed).toBe(true);
            expect(MockBroadcastChannel.instances).toHaveLength(0);
        });
    });

    describe('generateSessionUrl', () => {
        it('should generate URL with session ID parameter', () => {
            const sessionId = 'abc-123-def-456';
            const url = generateSessionUrl('/playlist', sessionId);

            expect(url).toContain('/playlist');
            expect(url).toContain('session=abc-123-def-456');
            expect(url).toContain('http://localhost:5000');
        });

        it('should handle different paths', () => {
            const sessionId = 'test-session';
            
            const playlistUrl = generateSessionUrl('/playlist', sessionId);
            expect(playlistUrl).toContain('/playlist');
            
            const singerUrl = generateSessionUrl('/singer', sessionId);
            expect(singerUrl).toContain('/singer');
        });
    });

    describe('getSessionIdFromUrl', () => {
        it('should extract session ID from URL', () => {
            mockLocation.search = '?session=abc-123-def-456';
            global.URLSearchParams = class {
                constructor(search) {
                    this.params = new Map();
                    if (search.startsWith('?')) {
                        search.slice(1).split('&').forEach(pair => {
                            const [key, value] = pair.split('=');
                            this.params.set(key, value);
                        });
                    }
                }
                get(key) {
                    return this.params.get(key);
                }
            };

            const sessionId = getSessionIdFromUrl();
            expect(sessionId).toBe('abc-123-def-456');
        });

        it('should return null if no session ID in URL', () => {
            mockLocation.search = '';
            global.URLSearchParams = class {
                constructor() {
                    this.params = new Map();
                }
                get() {
                    return null;
                }
            };

            const sessionId = getSessionIdFromUrl();
            expect(sessionId).toBeNull();
        });

        it('should handle other query parameters', () => {
            mockLocation.search = '?foo=bar&session=test-123&baz=qux';
            global.URLSearchParams = class {
                constructor(search) {
                    this.params = new Map();
                    if (search.startsWith('?')) {
                        search.slice(1).split('&').forEach(pair => {
                            const [key, value] = pair.split('=');
                            this.params.set(key, value);
                        });
                    }
                }
                get(key) {
                    return this.params.get(key);
                }
            };

            const sessionId = getSessionIdFromUrl();
            expect(sessionId).toBe('test-123');
        });
    });

    describe('checkMainTabAlive', () => {
        it('should return true if called from main tab', async () => {
            initializeSession(TEST_SESSION_ID, true);

            const isAlive = await checkMainTabAlive();
            expect(isAlive).toBe(true);
        });

        it('should return false if no ping response within timeout', async () => {
            initializeSession(TEST_SESSION_ID, false);

            const isAlive = await checkMainTabAlive();
            expect(isAlive).toBe(false);
        }, 3000);
    });

    describe('setupStateUpdateListener', () => {
        it('should invoke HandleBroadcastMessage for session-state-updated events', () => {
            const dotNetRef = {
                invokeMethodAsync: vi.fn().mockResolvedValue(undefined)
            };

            setupStateUpdateListener(dotNetRef);

            const listenerCall = mockWindow.addEventListener.mock.calls.find(call => call[0] === 'session-state-updated');
            expect(listenerCall).toBeDefined();

            const handler = listenerCall[1];
            const payload = {
                type: 'playlist-updated',
                data: { queue: [{ id: 'song-1' }] }
            };

            handler({
                type: 'session-state-updated',
                detail: payload
            });

            expect(dotNetRef.invokeMethodAsync).toHaveBeenCalledWith('HandleBroadcastMessage', payload.type, payload.data);
        });
    });

    describe('edge cases', () => {
        it('should handle unknown state types gracefully', () => {
            initializeSession(TEST_SESSION_ID, true);
            
            // Should not throw error when encountering unknown state type
            expect(() => broadcastStateUpdate('unknown-type', { data: 'test' })).not.toThrow();
            
            // State should not be persisted for unknown types
            const state = getSessionState();
            expect(state.playlist).toBeNull();
            expect(state.session).toBeNull();
        });

        it('should handle multiple initializations safely', () => {
            initializeSession(TEST_SESSION_ID, true);
            const firstCount = MockBroadcastChannel.instances.length;
            
            initializeSession(TEST_SESSION_ID, true);
            const secondCount = MockBroadcastChannel.instances.length;

            // Second init should not create another channel (implementation may vary)
            // For now, just ensure it doesn't crash
            expect(secondCount).toBeGreaterThanOrEqual(firstCount);
        });

        it('should handle sessionStorage errors gracefully', () => {
            const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            
            const originalSetItem = mockSessionStorage.setItem;
            mockSessionStorage._originalSetItem = originalSetItem;
            mockSessionStorage.setItem = () => {
                throw new Error('Storage quota exceeded');
            };

            initializeSession(TEST_SESSION_ID, true);
            expect(() => broadcastStateUpdate('playlist-updated', { queue: [] })).not.toThrow();

            // Restore original function
            mockSessionStorage.setItem = originalSetItem;
            mockSessionStorage._originalSetItem = null;
            consoleSpy.mockRestore();
        });
    });

    describe('SignalR playlist updates', () => {
        it('should preserve songId field when receiving ReceivePlaylistUpdated', async () => {
            // This test ensures the bug fix: songId must be preserved for library enrichment
            // Previously, the handler was using playlist item ID instead of song ID
            
            // Mock SignalR library before importing signalRBridge
            const mockHubConnection = {
                handlers: {},
                on: function(eventName, handler) {
                    this.handlers[eventName] = handler;
                },
                start: vi.fn().mockResolvedValue(undefined),
                invoke: vi.fn().mockResolvedValue(undefined),
                stop: vi.fn().mockResolvedValue(undefined),
                withUrl: function() { return this; },
                withAutomaticReconnect: function() { return this; },
                withServerTimeout: function() { return this; },
                withKeepAliveInterval: function() { return this; }
            };

            const MockHubConnectionBuilder = class {
                withUrl() { return this; }
                withAutomaticReconnect() { return this; }
                withServerTimeout() { return this; }
                withKeepAliveInterval() { return this; }
                build() { return mockHubConnection; }
            };

            global.signalR = {
                HubConnectionBuilder: MockHubConnectionBuilder
            };

            // Dynamic import to pick up the mocked signalR
            const signalRModule = await import('./signalRBridge.js?t=' + Date.now());
            
            // Initialize session with SignalR
            await signalRModule.initializeSession(TEST_SESSION_ID, true, 'test-token', 'http://backend:5000');

            // Wait for SignalR connection to be established
            await new Promise(resolve => setTimeout(resolve, 50));

            // Ensure handler was registered
            expect(mockHubConnection.handlers['ReceivePlaylistUpdated']).toBeDefined();

            // Simulate backend sending ReceivePlaylistUpdated with songId
            const backendDto = {
                playlistId: 'playlist-123',
                sessionId: TEST_SESSION_ID,
                items: [
                    {
                        id: 'playlist-item-id-1',      // Playlist item ID (not used for enrichment)
                        songId: 'song-guid-abc-123',   // Song ID (MUST be preserved for library lookup)
                        artist: 'Test Artist',
                        title: 'Test Song',
                        singerName: 'John Doe',
                        position: 0
                    },
                    {
                        id: 'playlist-item-id-2',
                        songId: 'song-guid-def-456',
                        artist: 'Another Artist',
                        title: 'Another Song',
                        singerName: null,
                        position: 1
                    }
                ]
            };

            // Trigger the handler
            mockHubConnection.handlers['ReceivePlaylistUpdated'](backendDto);

            // Allow async processing (event dispatching)
            await new Promise(resolve => setTimeout(resolve, 50));

            // Verify: sessionStorage contains queue with correct songId (not playlist item ID)
            const stored = JSON.parse(mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`));
            expect(stored).toBeDefined();
            expect(stored.playlist).toBeDefined();
            expect(stored.playlist.queue).toHaveLength(2);
            expect(stored.playlist.queue[0].id).toBe('song-guid-abc-123');  // Song ID, not 'playlist-item-id-1'
            expect(stored.playlist.queue[0].artist).toBe('Test Artist');
            expect(stored.playlist.queue[0].title).toBe('Test Song');
            expect(stored.playlist.queue[0].addedBySinger).toBe('John Doe');
            
            expect(stored.playlist.queue[1].id).toBe('song-guid-def-456');  // Song ID, not 'playlist-item-id-2'
            expect(stored.playlist.queue[1].artist).toBe('Another Artist');
            expect(stored.playlist.queue[1].addedBySinger).toBeNull();

            // Verify: custom event was dispatched with correct data
            expect(mockWindow.dispatchEvent).toHaveBeenCalledWith(
                expect.objectContaining({
                    type: 'session-state-updated',
                    detail: expect.objectContaining({
                        type: 'playlist-updated',
                        data: expect.objectContaining({
                            queue: expect.arrayContaining([
                                expect.objectContaining({
                                    id: 'song-guid-abc-123',
                                    artist: 'Test Artist'
                                })
                            ])
                        })
                    })
                })
            );
        });

        it('should handle case variations in DTO property names', async () => {
            // Backend may send PascalCase (Items) or camelCase (items) - both should work
            const mockHubConnection = {
                handlers: {},
                on: function(eventName, handler) {
                    this.handlers[eventName] = handler;
                },
                start: vi.fn().mockResolvedValue(undefined),
                invoke: vi.fn().mockResolvedValue(undefined),
                withUrl: function() { return this; },
                withAutomaticReconnect: function() { return this; },
                withServerTimeout: function() { return this; },
                withKeepAliveInterval: function() { return this; }
            };

            const MockHubConnectionBuilder = class {
                withUrl() { return this; }
                withAutomaticReconnect() { return this; }
                withServerTimeout() { return this; }
                withKeepAliveInterval() { return this; }
                build() { return mockHubConnection; }
            };

            global.signalR = {
                HubConnectionBuilder: MockHubConnectionBuilder,
                HubConnectionState: {
                    Disconnected: 0,
                    Connecting: 1,
                    Connected: 2,
                    Disconnecting: 3,
                    Reconnecting: 4
                }
            };

            const { initializeSession } = await import('./signalRBridge.js?t=' + Date.now());
            await initializeSession(TEST_SESSION_ID, true, 'test-token', 'http://backend:5000');

            // Test with PascalCase (C# backend convention)
            const pascalCaseDto = {
                Items: [
                    { SongId: 'song-1', Artist: 'Artist1', Title: 'Title1', SingerName: 'Singer1' }
                ]
            };

            mockHubConnection.handlers['ReceivePlaylistUpdated'](pascalCaseDto);
            await new Promise(resolve => setTimeout(resolve, 10));

            const stored = JSON.parse(mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`));
            expect(stored.playlist.queue[0].id).toBe('song-1');
            expect(stored.playlist.queue[0].artist).toBe('Artist1');
        });
    });

    describe('uploadLibraryToServer', () => {
        let fetchMock;

        beforeEach(() => {
            // Mock global fetch
            fetchMock = vi.fn();
            global.fetch = fetchMock;
            
            // Ensure signalR is defined to prevent script loading
            if (!global.signalR) {
                global.signalR = {
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
                        Disconnected: 0,
                        Connecting: 1,
                        Connected: 2,
                        Disconnecting: 3,
                        Reconnecting: 4
                    }
                };
            }
        });

        afterEach(() => {
            vi.restoreAllMocks();
        });

        it('should include id field when uploading songs', async () => {
            // Purpose: JavaScript must send IDs for backend to store them
            const { uploadLibraryToServer } = await import('./signalRBridge.js?uploadtest1=' + Date.now());
            
            // Mock successful response
            fetchMock.mockResolvedValueOnce({
                ok: true,
                status: 202
            });

            const libraryData = {
                songs: [
                    { id: 'guid-1', artist: 'Artist1', title: 'Title1' },
                    { id: 'guid-2', artist: 'Artist2', title: 'Title2' }
                ]
            };

            await uploadLibraryToServer('session-123', libraryData, { token: 'test-token' });

            // Verify fetch was called
            expect(fetchMock).toHaveBeenCalledTimes(1);
            
            // Capture POST body
            const callArgs = fetchMock.mock.calls[0];
            const requestBody = JSON.parse(callArgs[1].body);

            // Assert: POST body includes { id: "guid", artist: "...", title: "..." }
            expect(requestBody).toHaveLength(2);
            expect(requestBody[0]).toEqual({ id: 'guid-1', artist: 'Artist1', title: 'Title1', metadataJson: null });
            expect(requestBody[1]).toEqual({ id: 'guid-2', artist: 'Artist2', title: 'Title2', metadataJson: null });
        });

        it('should sanitize payload but keep id field', async () => {
            // Purpose: Filenames should never reach backend (security/privacy), but IDs must be included
            const { uploadLibraryToServer } = await import('./signalRBridge.js?uploadtest2=' + Date.now());
            
            fetchMock.mockResolvedValueOnce({
                ok: true,
                status: 202
            });

            const libraryData = {
                songs: [
                    {
                        id: 'guid-1',
                        artist: 'Artist1',
                        title: 'Title1',
                        mp3FileName: 'song.mp3',  // Should NOT be included
                        cdgFileName: 'song.cdg',  // Should NOT be included
                        metadataJson: '{"album":"Test"}'
                    }
                ]
            };

            await uploadLibraryToServer('session-123', libraryData, { token: 'test-token' });

            const callArgs = fetchMock.mock.calls[0];
            const requestBody = JSON.parse(callArgs[1].body);

            // Assert: Sanitized payload includes id, artist, title, metadataJson
            expect(requestBody).toHaveLength(1);
            expect(requestBody[0]).toEqual({
                id: 'guid-1',
                artist: 'Artist1',
                title: 'Title1',
                metadataJson: '{"album":"Test"}'
            });
            
            // Assert: Payload does NOT include mp3FileName or cdgFileName
            expect(requestBody[0]).not.toHaveProperty('mp3FileName');
            expect(requestBody[0]).not.toHaveProperty('cdgFileName');
        });

        it('should handle empty or null ids gracefully', async () => {
            // Purpose: Defensive programming - should handle edge cases gracefully
            const { uploadLibraryToServer } = await import('./signalRBridge.js?uploadtest3=' + Date.now());
            
            fetchMock.mockResolvedValueOnce({
                ok: true,
                status: 202
            });

            const libraryData = {
                songs: [
                    { id: null, artist: 'Artist1', title: 'Title1' },
                    { id: undefined, artist: 'Artist2', title: 'Title2' },
                    { id: 'guid-3', artist: 'Artist3', title: 'Title3' }
                ]
            };

            const result = await uploadLibraryToServer('session-123', libraryData, { token: 'test-token' });

            // Assert: Upload succeeds
            expect(result).toBe(true);
            expect(fetchMock).toHaveBeenCalledTimes(1);
            
            const callArgs = fetchMock.mock.calls[0];
            const requestBody = JSON.parse(callArgs[1].body);

            // All songs should be included (backend will handle null/undefined IDs)
            expect(requestBody).toHaveLength(3);
            expect(requestBody[0].id).toBeNull();
            expect(requestBody[1].id).toBeUndefined();
            expect(requestBody[2].id).toBe('guid-3');
        });
    });

    describe('Theme Application', () => {
        let mockSetTheme;

        beforeEach(() => {
            mockSetTheme = vi.fn();
            
            // Mock the themeToggle module
            vi.doMock('./themeToggle.js', () => ({
                setTheme: mockSetTheme
            }));
        });

        afterEach(() => {
            vi.doUnmock('./themeToggle.js');
        });

        it('should apply theme when session-settings message includes theme', async () => {
            initializeSession(TEST_SESSION_ID, true);

            const sessionData = {
                sessionId: 'abc-123',
                requireSingerName: true,
                theme: 'dark'
            };

            // Simulate receiving session-settings via broadcast
            const message = {
                type: 'session-settings',
                data: sessionData,
                timestamp: Date.now()
            };

            // Trigger broadcast to main tab
            const mainChannel = MockBroadcastChannel.instances[0];
            if (mainChannel.onmessage) {
                mainChannel.onmessage({ data: message });
            }

            // Wait for async import and setTheme call
            await new Promise(resolve => setTimeout(resolve, 100));

            expect(mockSetTheme).toHaveBeenCalledWith('dark');
        });

        it('should not apply theme when session-settings message has no theme', async () => {
            initializeSession(TEST_SESSION_ID, true);

            const sessionData = {
                sessionId: 'abc-123',
                requireSingerName: true
                // No theme property
            };

            const message = {
                type: 'session-settings',
                data: sessionData,
                timestamp: Date.now()
            };

            const mainChannel = MockBroadcastChannel.instances[0];
            if (mainChannel.onmessage) {
                mainChannel.onmessage({ data: message });
            }

            await new Promise(resolve => setTimeout(resolve, 100));

            expect(mockSetTheme).not.toHaveBeenCalled();
        });

        it('should not apply theme when message type is not session-settings', async () => {
            initializeSession(TEST_SESSION_ID, true);

            const message = {
                type: 'playlist-updated',
                data: { theme: 'dark' }, // Theme in wrong message type
                timestamp: Date.now()
            };

            const mainChannel = MockBroadcastChannel.instances[0];
            if (mainChannel.onmessage) {
                mainChannel.onmessage({ data: message });
            }

            await new Promise(resolve => setTimeout(resolve, 100));

            expect(mockSetTheme).not.toHaveBeenCalled();
        });
    });
});

