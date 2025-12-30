/**
 * Session Bridge - Cross-tab communication and session persistence
 * Uses Broadcast Channel API for real-time updates and sessionStorage for persistence
 */

let broadcastChannel = null;
let isMainTab = false;
let currentSessionId = null;

/**
 * Get channel name for a session
 * @param {string} sessionId - Session GUID
 * @returns {string} Channel name
 */
function getChannelName(sessionId) {
    return `karamel-session-${sessionId}`;
}

/**
 * Get storage key for a session
 * @param {string} sessionId - Session GUID
 * @returns {string} Storage key
 */
function getSessionKey(sessionId) {
    return `karamel-session-${sessionId}`;
}

/**
 * Initialize session bridge
 * @param {string} sessionId - Session GUID
 * @param {boolean} asMainTab - Whether this tab has directory handle (main tab)
 * @returns {Promise<void>}
 */
export function initializeSession(sessionId, asMainTab) {
    if (!sessionId) {
        throw new Error('sessionId is required');
    }
    
    currentSessionId = sessionId;
    isMainTab = asMainTab;
    
    try {
        broadcastChannel = new BroadcastChannel(getChannelName(sessionId));
        
        if (isMainTab) {
            // Main tab listens for state requests from secondary tabs
            broadcastChannel.onmessage = (event) => {
                if (event.data.type === 'request-state') {
                    console.log('Main tab received state request, sending current state...');
                    const currentState = getSessionState();
                    broadcastChannel.postMessage({
                        type: 'state-sync-response',
                        data: currentState,
                        timestamp: Date.now()
                    });
                }
            };
        } else {
            // Secondary tabs listen for updates and state sync responses
            broadcastChannel.onmessage = (event) => {
                if (event.data.type === 'state-sync-response') {
                    console.log('Secondary tab received state sync response:', event.data.data);
                    // Save the full state to this tab's sessionStorage
                    sessionStorage.setItem(getSessionKey(sessionId), JSON.stringify(event.data.data));
                    // Trigger custom event for Blazor to reload state
                    const stateEvent = new CustomEvent('session-state-synced', {
                        detail: event.data.data
                    });
                    window.dispatchEvent(stateEvent);
                } else {
                    handleBroadcastMessage(event.data);
                }
            };
            
            // Request current state from main tab
            console.log('Secondary tab requesting state from main tab...');
            broadcastChannel.postMessage({
                type: 'request-state',
                timestamp: Date.now()
            });
        }
        
        console.log(`Session bridge initialized as ${isMainTab ? 'MAIN' : 'SECONDARY'} tab for session ${sessionId}`);
    } catch (error) {
        console.error('Failed to initialize Broadcast Channel:', error);
        throw new Error('Broadcast Channel API is not supported in this browser');
    }
}

/**
 * Broadcast state update to all tabs (called by main tab only)
 * @param {string} type - Type of update (e.g., 'library-loaded', 'playlist-updated', 'session-settings')
 * @param {object} data - State data to broadcast
 */
export function broadcastStateUpdate(type, data) {
    if (!isMainTab) {
        console.warn('Only main tab can broadcast state updates');
        return;
    }
    
    if (!broadcastChannel) {
        console.error('Broadcast channel not initialized');
        return;
    }
    
    const message = {
        type,
        data,
        timestamp: Date.now()
    };
    
    // Save to sessionStorage for persistence
    saveToSessionStorage(type, data);
    
    // Broadcast to other tabs
    broadcastChannel.postMessage(message);
    console.log('Broadcasted:', type, data);
}

/**
 * Handle incoming broadcast messages (secondary tabs only)
 * @param {object} message - Broadcast message
 */
function handleBroadcastMessage(message) {
    console.log('Received broadcast:', message.type, message.data);
    
    // Save to sessionStorage
    saveToSessionStorage(message.type, message.data);
    
    // Trigger custom event for Blazor to handle
    const event = new CustomEvent('session-state-updated', {
        detail: message
    });
    window.dispatchEvent(event);
}

/**
 * Save state data to sessionStorage
 * @param {string} type - Type of data
 * @param {object} data - Data to save
 */
function saveToSessionStorage(type, data) {
    try {
        if (!currentSessionId) {
            console.error('Cannot save to sessionStorage: No active session');
            return;
        }
        
        const sessionState = getSessionState();
        
        switch (type) {
            case 'playlist-updated':
                sessionState.playlist = data;
                break;
            case 'session-settings':
                sessionState.session = data;
                break;
            case 'current-song':
                sessionState.currentSong = data;
                break;
            default:
                console.warn('Unknown state type:', type);
                return;
        }
        
        sessionStorage.setItem(getSessionKey(currentSessionId), JSON.stringify(sessionState));
    } catch (error) {
        console.error('Failed to save to sessionStorage:', error);
    }
}

/**
 * Save library to sessionStorage (main tab only, called once during session init)
 * @param {string} sessionId - Session GUID
 * @param {object} libraryData - Library data to save
 */
export function saveLibraryToSessionStorage(sessionId, libraryData) {
    try {
        if (!sessionId) {
            throw new Error('sessionId is required');
        }
        
        const sessionState = getSessionStateForSession(sessionId);
        sessionState.library = libraryData;
        sessionStorage.setItem(getSessionKey(sessionId), JSON.stringify(sessionState));
        console.log('Library saved to sessionStorage for session', sessionId, ':', libraryData.songs?.length || 0, 'songs');
    } catch (error) {
        console.error('Failed to save library to sessionStorage:', error);
    }
}

/**
 * Get session state for a specific session from sessionStorage
 * @param {string} sessionId - Session GUID
 * @returns {object} Session state object
 */
export function getSessionStateForSession(sessionId) {
    try {
        if (!sessionId) {
            throw new Error('sessionId is required');
        }
        
        const stored = sessionStorage.getItem(getSessionKey(sessionId));
        return stored ? JSON.parse(stored) : {
            session: null,
            library: null,
            playlist: null,
            currentSong: null
        };
    } catch (error) {
        console.error('Failed to read from sessionStorage:', error);
        return {
            session: null,
            library: null,
            playlist: null,
            currentSong: null
        };
    }
}

/**
 * Get current session state from sessionStorage (uses current session)
 * @returns {object} Session state object
 */
export function getSessionState() {
    if (!currentSessionId) {
        console.warn('No active session');
        return {
            session: null,
            library: null,
            playlist: null,
            currentSong: null
        };
    }
    return getSessionStateForSession(currentSessionId);
}

/**
 * Clear session state (when session ends)
 */
export function clearSessionState() {
    try {
        if (currentSessionId) {
            sessionStorage.removeItem(getSessionKey(currentSessionId));
        }
        
        if (broadcastChannel) {
            // Notify other tabs that session ended
            broadcastChannel.postMessage({
                type: 'session-ended',
                timestamp: Date.now()
            });
            broadcastChannel.close();
            broadcastChannel = null;
        }
        
        console.log('Session state cleared for session', currentSessionId);
        currentSessionId = null;
    } catch (error) {
        console.error('Failed to clear session state:', error);
    }
}

/**
 * Generate session URL with SessionId query parameter
 * @param {string} path - Path (e.g., '/playlist', '/singer')
 * @param {string} sessionId - Session GUID
 * @returns {string} Full URL with session parameter
 */
export function generateSessionUrl(path, sessionId) {
    const url = new URL(path, window.location.origin);
    url.searchParams.set('id', sessionId);
    return url.toString();
}

/**
 * Get SessionId from current URL query parameter
 * @returns {string|null} SessionId or null if not found
 */
export function getSessionIdFromUrl() {
    const params = new URLSearchParams(window.location.search);
    return params.get('session');
}

/**
 * Setup listener for state sync completion
 * @param {object} dotNetRef - .NET object reference with OnStateSynced callback
 */
export function setupStateSyncListener(dotNetRef) {
    const handler = (event) => {
        if (event.type === 'session-state-synced') {
            console.log('State sync event received, notifying .NET');
            dotNetRef.invokeMethodAsync('OnStateSynced');
            window.removeEventListener('session-state-synced', handler);
        }
    };
    
    window.addEventListener('session-state-synced', handler);
    
    // Cleanup after timeout
    setTimeout(() => {
        window.removeEventListener('session-state-synced', handler);
    }, 3000);
}

/**
 * Setup listener for ongoing state updates from main tab (secondary tabs only)
 * @param {object} dotNetRef - .NET object reference with OnStateUpdated callback
 */
export function setupStateUpdateListener(dotNetRef) {
    if (isMainTab) {
        console.log('Main tab does not need to listen for state updates');
        return;
    }

    const handler = (event) => {
        if (event.type === 'session-state-updated') {
            console.log('State update event received:', event.detail.type);
            dotNetRef.invokeMethodAsync('OnStateUpdated', event.detail.type, event.detail.data);
        }
    };
    
    window.addEventListener('session-state-updated', handler);
    console.log('State update listener registered for secondary tab');
}

/**
 * Check if main tab is still alive
 * @returns {Promise<boolean>} True if main tab responds, false otherwise
 */
export function checkMainTabAlive() {
    return new Promise((resolve) => {
        if (isMainTab) {
            resolve(true);
            return;
        }
        
        if (!broadcastChannel) {
            resolve(false);
            return;
        }
        
        const timeoutId = setTimeout(() => {
            broadcastChannel.removeEventListener('message', handlePing);
            resolve(false);
        }, 2000);
        
        function handlePing(event) {
            if (event.data.type === 'ping-response') {
                clearTimeout(timeoutId);
                broadcastChannel.removeEventListener('message', handlePing);
                resolve(true);
            }
        }
        
        broadcastChannel.addEventListener('message', handlePing);
        broadcastChannel.postMessage({ type: 'ping' });
    });
}

/**
 * Handle ping requests from secondary tabs (main tab only)
 */
if (typeof window !== 'undefined') {
    window.addEventListener('load', () => {
        if (broadcastChannel && isMainTab) {
            const originalOnMessage = broadcastChannel.onmessage;
            broadcastChannel.onmessage = (event) => {
                if (event.data.type === 'ping') {
                    broadcastChannel.postMessage({ type: 'ping-response' });
                }
                if (originalOnMessage) {
                    originalOnMessage(event);
                }
            };
        }
    });
}

/**
 * Handle tab close (main tab only - notify secondary tabs)
 */
if (typeof window !== 'undefined') {
    window.addEventListener('beforeunload', () => {
        if (isMainTab && broadcastChannel) {
            broadcastChannel.postMessage({
                type: 'main-tab-closing',
                timestamp: Date.now()
            });
        }
    });
}
