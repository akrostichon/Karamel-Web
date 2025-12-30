/**
 * Home Page Interop - Session initialization and library setup
 * Handles session creation, library selection, and multi-tab initialization
 */

import { pickLibraryDirectory } from './fileAccess.js';
import { initializeSession, saveLibraryToSessionStorage, broadcastStateUpdate } from './sessionBridge.js';
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
export function generateSessionUrl(path, sessionId) {
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
    
    return `${origin}${basePath}${cleanPath}?session=${sessionId}`;
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
    
    // Initialize session as main tab
    initializeSession(config.sessionId, true);
    
    // Save library to sessionStorage
    await saveLibraryToSessionStorage(config.sessionId, { songs });
    
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
 * @returns {object} Result with URLs (window references not returned to avoid circular JSON)
 */
export function openSessionTabs(sessionId) {
    if (!sessionId) {
        throw new Error('sessionId is required');
    }
    
    const playlistUrl = generateSessionUrl('playlist', sessionId);
    const singerUrl = generateSessionUrl('singer', sessionId);
    
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
 * @returns {string} NextSongView URL with session ID
 */
export function getNextSongViewUrl(sessionId) {
    if (!sessionId) {
        throw new Error('sessionId is required');
    }
    
    return generateSessionUrl('nextsong', sessionId);
}

/**
 * Complete session startup flow
 * @param {object} config - Session configuration
 * @param {Array} songs - Library songs
 * @returns {Promise<object>} Result with navigation URL and opened tabs info
 */
export async function startKaraokeSession(config, songs) {
    // Initialize session
    await initializeKaraokeSession(config, songs);
    
    // Open new tabs
    const tabsResult = openSessionTabs(config.sessionId);
    
    // Get navigation URL for current tab
    const nextSongUrl = getNextSongViewUrl(config.sessionId);
    
    return {
        sessionId: config.sessionId,
        nextSongUrl,
        ...tabsResult
    };
}
