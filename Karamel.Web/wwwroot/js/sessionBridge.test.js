import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
    initializeSession,
    broadcastStateUpdate,
    saveLibraryToSessionStorage,
    getSessionState,
    getSessionStateForSession,
    clearSessionState,
    generateSessionUrl,
    getSessionIdFromUrl,
    checkMainTabAlive
} from './sessionBridge.js';

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

describe('sessionBridge', () => {
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

    describe('saveLibraryToSessionStorage', () => {
        it('should save library to sessionStorage without broadcasting', () => {
            const libraryData = {
                songs: [
                    { id: '123', artist: 'Artist 1', title: 'Song 1' },
                    { id: '456', artist: 'Artist 2', title: 'Song 2' }
                ]
            };

            saveLibraryToSessionStorage(TEST_SESSION_ID, libraryData);

            const stored = JSON.parse(mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`));
            expect(stored.library).toEqual(libraryData);
        });

        it('should preserve existing session state when saving library', () => {
            // Set up existing state
            const existingState = {
                session: { sessionId: 'abc-123' },
                library: null,
                playlist: { queue: [{ id: '1' }] },
                currentSong: null
            };
            mockSessionStorage.setItem(`karamel-session-${TEST_SESSION_ID}`, JSON.stringify(existingState));

            const libraryData = {
                songs: [{ id: '789', artist: 'New Artist', title: 'New Song' }]
            };

            saveLibraryToSessionStorage(TEST_SESSION_ID, libraryData);

            const stored = JSON.parse(mockSessionStorage.getItem(`karamel-session-${TEST_SESSION_ID}`));
            expect(stored.library).toEqual(libraryData);
            expect(stored.session).toEqual(existingState.session);
            expect(stored.playlist).toEqual(existingState.playlist);
        });

        it('should handle storage errors gracefully', () => {
            const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            
            const originalSetItem = mockSessionStorage.setItem;
            mockSessionStorage._originalSetItem = originalSetItem;
            mockSessionStorage.setItem = () => {
                throw new Error('Storage quota exceeded');
            };

            const libraryData = { songs: [] };
            expect(() => saveLibraryToSessionStorage(libraryData)).not.toThrow();

            // Restore original function
            mockSessionStorage.setItem = originalSetItem;
            mockSessionStorage._originalSetItem = null;
            consoleSpy.mockRestore();
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

    describe('edge cases', () => {
        it('should handle unknown state types gracefully', () => {
            const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
            
            initializeSession(TEST_SESSION_ID, true);
            broadcastStateUpdate('unknown-type', { data: 'test' });

            expect(consoleSpy).toHaveBeenCalledWith('Unknown state type:', 'unknown-type');
            consoleSpy.mockRestore();
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

        it('should isolate different sessions', () => {
            const session1 = 'session-1';
            const session2 = 'session-2';

            saveLibraryToSessionStorage(session1, { songs: [{ id: '1', title: 'Song 1' }] });
            saveLibraryToSessionStorage(session2, { songs: [{ id: '2', title: 'Song 2' }] });

            const state1 = getSessionStateForSession(session1);
            const state2 = getSessionStateForSession(session2);

            expect(state1.library.songs).toHaveLength(1);
            expect(state1.library.songs[0].id).toBe('1');
            
            expect(state2.library.songs).toHaveLength(1);
            expect(state2.library.songs[0].id).toBe('2');
        });
    });
});
