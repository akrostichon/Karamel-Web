/**
 * Home Page Interop - Session initialization and library setup
 * Handles session creation, library selection, and multi-tab initialization
 */

import { pickLibraryDirectory } from './fileAccess.js';
import { broadcastStateUpdate } from './signalRBridge.js';
import { validatePattern } from './metadata.js';

/**
 * Check if File System Access API is supported
 * @returns {boolean} True if supported
 */
export function isFileSystemAccessSupported() {
    return 'showDirectoryPicker' in window;
}

/**
 * Generate a cryptographically secure session GUID
 * @returns {string} Session GUID in format xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx
 */
export function generateSessionId() {
    // Use crypto.randomUUID if available (modern browsers)
    if ('randomUUID' in crypto) {
        return crypto.randomUUID();
    }
    
    // Fallback implementation using crypto.getRandomValues
    const template = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx';
    return template.replace(/[xy]/g, (c) => {
        const r = (crypto.getRandomValues(new Uint8Array(1))[0] % 16) | 0;
        const v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

/**
 * Generate session URL with session ID
 * @param {string} path - Path without leading slash (e.g., 'playlist', 'singer', 'nextsong')
 * @param {string} sessionId - Session GUID
 * @returns {string} Complete URL with session ID
 */
export function generateSessionUrl(path, sessionId, linkToken = null) {
    if (!path) {
        throw new Error('path is required');
    }
    if (!sessionId) {
        throw new Error('sessionId is required');
    }
    
    // Remove leading slash if present
    const cleanPath = path.startsWith('/') ? path.substring(1) : path;
    
    // Use current origin + base path
    const origin = window.location.origin;
    const basePath = window.location.pathname.substring(0, window.location.pathname.lastIndexOf('/') + 1);
    
    // Add linkToken to query string if provided
    const params = `session=${sessionId}${linkToken ? `&token=${linkToken}` : ''}`;
    return `${origin}${basePath}${cleanPath}?${params}`;
}

/**
 * Validate session configuration settings
 * @param {object} config - Configuration object
 * @param {boolean} config.requireSingerName - Whether singer name is required
 * @param {boolean} config.allowSingerReorder - Whether singers can reorder playlist
 * @param {number} config.pauseBetweenSongs - Seconds to pause between songs
 * @param {string} config.filenamePattern - Pattern for parsing filenames
 * @returns {object} Validation result with isValid flag and errors array
 */
export function validateConfiguration(config) {
    const errors = [];
    
    if (typeof config.requireSingerName !== 'boolean') {
        errors.push('requireSingerName must be a boolean');
    }
    
    if (typeof config.allowSingerReorder !== 'boolean') {
        errors.push('allowSingerReorder must be a boolean');
    }
    
    if (typeof config.pauseBetweenSongs !== 'number') {
        errors.push('pauseBetweenSongs must be a number');
    } else if (config.pauseBetweenSongs < 0) {
        errors.push('pauseBetweenSongs must be non-negative');
    } else if (config.pauseBetweenSongs > 60) {
        errors.push('pauseBetweenSongs must be 60 seconds or less');
    }
    
    if (!config.filenamePattern || typeof config.filenamePattern !== 'string') {
        errors.push('filenamePattern is required and must be a string');
    } else {
        const validatedPattern = validatePattern(config.filenamePattern);
        if (validatedPattern !== config.filenamePattern) {
            errors.push('filenamePattern must contain %artist and/or %title');
        }
    }
    
    return {
        isValid: errors.length === 0,
        errors
    };
}

/**
 * Select library directory and scan for songs
 * @param {string} filenamePattern - Pattern for parsing filenames
 * @returns {Promise<object>} Result with songs array and directory info, or null on error/cancel
 */
export async function selectLibrary(filenamePattern) {
    try {
        const songs = await pickLibraryDirectory(filenamePattern);
        
        if (!songs) {
            // User cancelled or error occurred
            return null;
        }
        
        return {
            songs,
            songCount: songs.length,
            success: true
        };
    } catch (error) {
        console.error('Error selecting library:', error);
        return {
            songs: [],
            songCount: 0,
            success: false,
            error: error.message
        };
    }
}

/**
 * Initialize a new karaoke session
 * @param {object} config - Session configuration
 * @param {string} config.sessionId - Session GUID
 * @param {boolean} config.requireSingerName - Whether singer name is required
 * @param {boolean} config.allowSingerReorder - Whether singers can reorder playlist
 * @param {number} config.pauseBetweenSongs - Seconds to pause between songs
 * @param {string} config.filenamePattern - Pattern for parsing filenames
 * @param {Array} songs - Library songs to save
 * @returns {Promise<void>}
 */
export async function initializeKaraokeSession(config, songs) {
    if (!config.sessionId) {
        throw new Error('sessionId is required');
    }
    
    // Validate configuration
    const validation = validateConfiguration(config);
    if (!validation.isValid) {
        throw new Error(`Invalid configuration: ${validation.errors.join(', ')}`);
    }
    
    // Session is already initialized by SessionService.InitializeAsync in Home.razor
    // with proper linkToken and backendUrl parameters, so we don't call initializeSession here
    // Library is also uploaded to server by SessionService.UploadLibraryToServerAsync
    
    // Broadcast session settings (includes all session data for secondary tabs)
    const sessionSettings = {
        sessionId: config.sessionId,
        libraryPath: 'Selected Library', // We don't have actual path from File System Access API
        requireSingerName: config.requireSingerName,
        allowSingerReorder: config.allowSingerReorder,
        pauseBetweenSongs: true, // Always enable pause screen
        pauseBetweenSongsSeconds: config.pauseBetweenSongs,
        filenamePattern: config.filenamePattern
    };

    // Only include theme if explicitly provided
    if (typeof config.theme !== 'undefined' && config.theme !== null) {
        sessionSettings.theme = config.theme;
    }

    broadcastStateUpdate('session-settings', sessionSettings);
    
    console.log('Karaoke session initialized:', config.sessionId);
}

/**
 * Open new tabs for playlist and singer views
 * @param {string} sessionId - Session GUID
 * @param {string|null} linkToken - Link token for authentication
 * @returns {object} Result with URLs (window references not returned to avoid circular JSON)
 */
export function openSessionTabs(sessionId, linkToken = null) {
    if (!sessionId) {
        throw new Error('sessionId is required');
    }
    
    const playlistUrl = generateSessionUrl('playlist', sessionId, linkToken);
    const singerUrl = generateSessionUrl('singer', sessionId, linkToken);
    
    // Open new tabs/windows in background (don't switch focus)
    window.open(playlistUrl, '_blank');
    window.open(singerUrl, '_blank');
    
    // Refocus the current window to stay on this tab
    window.focus();
    
    return {
        playlistUrl,
        singerUrl
    };
}

/**
 * Get navigation URL for current tab (NextSongView)
 * @param {string} sessionId - Session GUID
 * @param {string|null} linkToken - Link token for authentication
 * @returns {string} NextSongView URL with session ID
 */
export function getNextSongViewUrl(sessionId, linkToken = null) {
    if (!sessionId) {
        throw new Error('sessionId is required');
    }
    
    return generateSessionUrl('nextsong', sessionId, linkToken);
}

/**
 * Complete session startup flow
 * @param {object} config - Session configuration
 * @param {Array} songs - Library songs
 * @param {string|null} linkToken - Link token for authentication
 * @returns {Promise<object>} Result with navigation URL and opened tabs info
 */
export async function startKaraokeSession(config, songs, linkToken = null) {
    // Initialize session
    await initializeKaraokeSession(config, songs);
    
    // Open new tabs with linkToken
    const tabsResult = openSessionTabs(config.sessionId, linkToken);
    
    // Get navigation URL for current tab with linkToken
    const nextSongUrl = getNextSongViewUrl(config.sessionId, linkToken);
    
    return {
        sessionId: config.sessionId,
        nextSongUrl,
        ...tabsResult
    };
}
