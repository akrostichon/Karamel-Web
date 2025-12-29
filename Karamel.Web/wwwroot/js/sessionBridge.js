/**
 * Session Bridge - Cross-tab communication and session persistence
 * Uses Broadcast Channel API for real-time updates and sessionStorage for persistence
 */

const CHANNEL_NAME = 'karamel-session';
const SESSION_KEY = 'karamel-session-state';

let broadcastChannel = null;
let isMainTab = false;

/**
 * Initialize session bridge
 * @param {boolean} asMainTab - Whether this tab has directory handle (main tab)
 * @returns {Promise<void>}
 */
export function initializeSession(asMainTab) {
    isMainTab = asMainTab;
    
    try {
        broadcastChannel = new BroadcastChannel(CHANNEL_NAME);
        
        if (!isMainTab) {
            // Secondary tabs listen for updates from main tab
            broadcastChannel.onmessage = (event) => {
                handleBroadcastMessage(event.data);
            };
        }
        
        console.log(`Session bridge initialized as ${isMainTab ? 'MAIN' : 'SECONDARY'} tab`);
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
        
        sessionStorage.setItem(SESSION_KEY, JSON.stringify(sessionState));
    } catch (error) {
        console.error('Failed to save to sessionStorage:', error);
    }
}

/**
 * Save library to sessionStorage (main tab only, called once during session init)
 * @param {object} libraryData - Library data to save
 */
export function saveLibraryToSessionStorage(libraryData) {
    try {
        const sessionState = getSessionState();
        sessionState.library = libraryData;
        sessionStorage.setItem(SESSION_KEY, JSON.stringify(sessionState));
        console.log('Library saved to sessionStorage:', libraryData.songs?.length || 0, 'songs');
    } catch (error) {
        console.error('Failed to save library to sessionStorage:', error);
    }
}

/**
 * Get current session state from sessionStorage
 * @returns {object} Session state object
 */
export function getSessionState() {
    try {
        const stored = sessionStorage.getItem(SESSION_KEY);
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
 * Clear session state (when session ends)
 */
export function clearSessionState() {
    try {
        sessionStorage.removeItem(SESSION_KEY);
        
        if (broadcastChannel) {
            // Notify other tabs that session ended
            broadcastChannel.postMessage({
                type: 'session-ended',
                timestamp: Date.now()
            });
            broadcastChannel.close();
            broadcastChannel = null;
        }
        
        console.log('Session state cleared');
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
    return params.get('id');
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
