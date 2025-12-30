import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
    isFileSystemAccessSupported,
    generateSessionId,
    generateSessionUrl,
    validateConfiguration,
    selectLibrary,
    initializeKaraokeSession,
    openSessionTabs,
    getNextSongViewUrl,
    startKaraokeSession
} from './homeInterop.js';

// Mock dependencies
vi.mock('./fileAccess.js', () => ({
    pickLibraryDirectory: vi.fn()
}));

vi.mock('./sessionBridge.js', () => ({
    initializeSession: vi.fn(),
    saveLibraryToSessionStorage: vi.fn(),
    broadcastStateUpdate: vi.fn()
}));

vi.mock('./metadata.js', () => ({
    validatePattern: vi.fn((pattern) => {
        // Simulate real validation logic
        if (!pattern || (!pattern.includes('%artist') && !pattern.includes('%title'))) {
            return '%artist - %title'; // Default pattern
        }
        return pattern;
    })
}));

// Import mocked modules
import { pickLibraryDirectory } from './fileAccess.js';
import { initializeSession, saveLibraryToSessionStorage, broadcastStateUpdate } from './sessionBridge.js';
import { validatePattern } from './metadata.js';

describe('File System Access Support', () => {
    it('should detect if File System Access API is supported', () => {
        // Mock showDirectoryPicker
        window.showDirectoryPicker = vi.fn();
        
        const isSupported = isFileSystemAccessSupported();
        expect(isSupported).toBe(true);
    });
    
    it('should return false if File System Access API is not supported', () => {
        delete window.showDirectoryPicker;
        
        const isSupported = isFileSystemAccessSupported();
        expect(isSupported).toBe(false);
    });
});

describe('Session ID Generation', () => {
    it('should generate a valid GUID format', () => {
        const sessionId = generateSessionId();
        
        // Check GUID format: xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx
        const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
        expect(sessionId).toMatch(guidRegex);
    });
    
    it('should generate unique session IDs', () => {
        const id1 = generateSessionId();
        const id2 = generateSessionId();
        const id3 = generateSessionId();
        
        expect(id1).not.toBe(id2);
        expect(id2).not.toBe(id3);
        expect(id1).not.toBe(id3);
    });
    
    it('should use crypto.randomUUID if available', () => {
        const mockUUID = '550e8400-e29b-41d4-a716-446655440000';
        crypto.randomUUID = vi.fn(() => mockUUID);
        
        const sessionId = generateSessionId();
        expect(sessionId).toBe(mockUUID);
        expect(crypto.randomUUID).toHaveBeenCalled();
    });
});

describe('Session URL Generation', () => {
    beforeEach(() => {
        // Mock window.location
        delete window.location;
        window.location = {
            origin: 'http://localhost:5000',
            pathname: '/karaoke/'
        };
    });
    
    it('should generate correct playlist URL', () => {
        const sessionId = '550e8400-e29b-41d4-a716-446655440000';
        const url = generateSessionUrl('playlist', sessionId);
        
        expect(url).toBe('http://localhost:5000/karaoke/playlist?session=550e8400-e29b-41d4-a716-446655440000');
    });
    
    it('should generate correct singer URL', () => {
        const sessionId = '550e8400-e29b-41d4-a716-446655440000';
        const url = generateSessionUrl('singer', sessionId);
        
        expect(url).toBe('http://localhost:5000/karaoke/singer?session=550e8400-e29b-41d4-a716-446655440000');
    });
    
    it('should generate correct nextsong URL', () => {
        const sessionId = '550e8400-e29b-41d4-a716-446655440000';
        const url = generateSessionUrl('nextsong', sessionId);
        
        expect(url).toBe('http://localhost:5000/karaoke/nextsong?session=550e8400-e29b-41d4-a716-446655440000');
    });
    
    it('should handle path with leading slash', () => {
        const sessionId = '550e8400-e29b-41d4-a716-446655440000';
        const url = generateSessionUrl('/playlist', sessionId);
        
        expect(url).toBe('http://localhost:5000/karaoke/playlist?session=550e8400-e29b-41d4-a716-446655440000');
    });
    
    it('should throw error if path is missing', () => {
        expect(() => generateSessionUrl('', '550e8400-e29b-41d4-a716-446655440000')).toThrow('path is required');
    });
    
    it('should throw error if sessionId is missing', () => {
        expect(() => generateSessionUrl('playlist', '')).toThrow('sessionId is required');
    });
});

describe('Configuration Validation', () => {
    it('should validate correct configuration', () => {
        const config = {
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: 5,
            filenamePattern: '%artist - %title'
        };
        
        const result = validateConfiguration(config);
        expect(result.isValid).toBe(true);
        expect(result.errors).toHaveLength(0);
    });
    
    it('should reject non-boolean requireSingerName', () => {
        const config = {
            requireSingerName: 'yes',
            allowSingerReorder: false,
            pauseBetweenSongs: 5,
            filenamePattern: '%artist - %title'
        };
        
        const result = validateConfiguration(config);
        expect(result.isValid).toBe(false);
        expect(result.errors).toContain('requireSingerName must be a boolean');
    });
    
    it('should reject non-boolean allowSingerReorder', () => {
        const config = {
            requireSingerName: true,
            allowSingerReorder: 'no',
            pauseBetweenSongs: 5,
            filenamePattern: '%artist - %title'
        };
        
        const result = validateConfiguration(config);
        expect(result.isValid).toBe(false);
        expect(result.errors).toContain('allowSingerReorder must be a boolean');
    });
    
    it('should reject negative pauseBetweenSongs', () => {
        const config = {
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: -5,
            filenamePattern: '%artist - %title'
        };
        
        const result = validateConfiguration(config);
        expect(result.isValid).toBe(false);
        expect(result.errors).toContain('pauseBetweenSongs must be non-negative');
    });
    
    it('should reject pauseBetweenSongs over 60 seconds', () => {
        const config = {
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: 120,
            filenamePattern: '%artist - %title'
        };
        
        const result = validateConfiguration(config);
        expect(result.isValid).toBe(false);
        expect(result.errors).toContain('pauseBetweenSongs must be 60 seconds or less');
    });
    
    it('should accept pauseBetweenSongs at boundary (0 and 60)', () => {
        const config1 = {
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: 0,
            filenamePattern: '%artist - %title'
        };
        
        const config2 = {
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: 60,
            filenamePattern: '%artist - %title'
        };
        
        expect(validateConfiguration(config1).isValid).toBe(true);
        expect(validateConfiguration(config2).isValid).toBe(true);
    });
    
    it('should reject non-string filenamePattern', () => {
        const config = {
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: 5,
            filenamePattern: 123
        };
        
        const result = validateConfiguration(config);
        expect(result.isValid).toBe(false);
        expect(result.errors).toContain('filenamePattern is required and must be a string');
    });
    
    it('should reject invalid filenamePattern (no placeholders)', () => {
        const config = {
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: 5,
            filenamePattern: 'invalid pattern'
        };
        
        // Mock validatePattern to return default pattern
        validatePattern.mockReturnValueOnce('%artist - %title');
        
        const result = validateConfiguration(config);
        expect(result.isValid).toBe(false);
        expect(result.errors).toContain('filenamePattern must contain %artist and/or %title');
    });
    
    it('should accumulate multiple validation errors', () => {
        const config = {
            requireSingerName: 'invalid',
            allowSingerReorder: 'invalid',
            pauseBetweenSongs: -5,
            filenamePattern: ''
        };
        
        const result = validateConfiguration(config);
        expect(result.isValid).toBe(false);
        expect(result.errors.length).toBeGreaterThan(1);
    });
});

describe('Library Selection', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });
    
    it('should select library and return songs', async () => {
        const mockSongs = [
            { id: '1', artist: 'Artist 1', title: 'Song 1', mp3FileName: 'song1.mp3', cdgFileName: 'song1.cdg' },
            { id: '2', artist: 'Artist 2', title: 'Song 2', mp3FileName: 'song2.mp3', cdgFileName: 'song2.cdg' }
        ];
        
        pickLibraryDirectory.mockResolvedValue(mockSongs);
        
        const result = await selectLibrary('%artist - %title');
        
        expect(result).toEqual({
            songs: mockSongs,
            songCount: 2,
            success: true
        });
        expect(pickLibraryDirectory).toHaveBeenCalledWith('%artist - %title');
    });
    
    it('should return null when user cancels selection', async () => {
        pickLibraryDirectory.mockResolvedValue(null);
        
        const result = await selectLibrary('%artist - %title');
        
        expect(result).toBeNull();
    });
    
    it('should handle errors during selection', async () => {
        const errorMessage = 'Permission denied';
        pickLibraryDirectory.mockRejectedValue(new Error(errorMessage));
        
        const result = await selectLibrary('%artist - %title');
        
        expect(result.success).toBe(false);
        expect(result.songs).toHaveLength(0);
        expect(result.error).toBe(errorMessage);
    });
});

describe('Session Initialization', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        // Ensure window.focus exists in the test environment
        if (typeof window !== 'undefined' && typeof window.focus !== 'function') {
            window.focus = vi.fn();
        }
    });
    
    it('should initialize karaoke session with valid config', async () => {
        const config = {
            sessionId: '550e8400-e29b-41d4-a716-446655440000',
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: 5,
            filenamePattern: '%artist - %title'
        };
        
        const songs = [
            { id: '1', artist: 'Artist 1', title: 'Song 1' }
        ];
        
        await initializeKaraokeSession(config, songs);
        
        expect(initializeSession).toHaveBeenCalledWith(config.sessionId, true);
        expect(saveLibraryToSessionStorage).toHaveBeenCalledWith(config.sessionId, { songs });
        expect(broadcastStateUpdate).toHaveBeenCalledWith('session-settings', {
            sessionId: config.sessionId,
            libraryPath: 'Selected Library',
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: true,
            pauseBetweenSongsSeconds: config.pauseBetweenSongs,
            filenamePattern: '%artist - %title'
        });
    });
    
    it('should throw error if sessionId is missing', async () => {
        const config = {
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: 5,
            filenamePattern: '%artist - %title'
        };
        
        await expect(initializeKaraokeSession(config, [])).rejects.toThrow('sessionId is required');
    });
    
    it('should throw error if configuration is invalid', async () => {
        const config = {
            sessionId: '550e8400-e29b-41d4-a716-446655440000',
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: -5,
            filenamePattern: '%artist - %title'
        };
        
        await expect(initializeKaraokeSession(config, [])).rejects.toThrow('Invalid configuration');
    });
});

describe('Tab Management', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        
        // Mock window.open
        window.open = vi.fn((url) => ({ url }));
        
        // Mock window.location
        delete window.location;
        window.location = {
            origin: 'http://localhost:5000',
            pathname: '/karaoke/'
        };
    });
    
    it('should open playlist and singer tabs', () => {
        const sessionId = '550e8400-e29b-41d4-a716-446655440000';
        const result = openSessionTabs(sessionId);
        
        expect(window.open).toHaveBeenCalledTimes(2);
        expect(window.open).toHaveBeenCalledWith(
            'http://localhost:5000/karaoke/playlist?session=550e8400-e29b-41d4-a716-446655440000',
            '_blank'
        );
        expect(window.open).toHaveBeenCalledWith(
            'http://localhost:5000/karaoke/singer?session=550e8400-e29b-41d4-a716-446655440000',
            '_blank'
        );
        expect(result.playlistUrl).toContain('playlist');
        expect(result.singerUrl).toContain('singer');
    });
    
    it('should throw error if sessionId is missing', () => {
        expect(() => openSessionTabs('')).toThrow('sessionId is required');
    });
    
    it('should return NextSongView URL', () => {
        const sessionId = '550e8400-e29b-41d4-a716-446655440000';
        const url = getNextSongViewUrl(sessionId);
        
        expect(url).toBe('http://localhost:5000/karaoke/nextsong?session=550e8400-e29b-41d4-a716-446655440000');
    });
    
    it('should throw error when getting NextSongView URL without sessionId', () => {
        expect(() => getNextSongViewUrl('')).toThrow('sessionId is required');
    });
});

describe('Complete Session Startup Flow', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        
        window.open = vi.fn((url) => ({ url }));
        
        delete window.location;
        window.location = {
            origin: 'http://localhost:5000',
            pathname: '/karaoke/'
        };
    });
    
    it('should complete full session startup', async () => {
        const config = {
            sessionId: '550e8400-e29b-41d4-a716-446655440000',
            requireSingerName: true,
            allowSingerReorder: false,
            pauseBetweenSongs: 5,
            filenamePattern: '%artist - %title'
        };
        
        const songs = [
            { id: '1', artist: 'Artist 1', title: 'Song 1' }
        ];
        
        const result = await startKaraokeSession(config, songs);
        
        expect(result.sessionId).toBe(config.sessionId);
        expect(result.nextSongUrl).toContain('nextsong');
        expect(result.playlistUrl).toContain('playlist');
        expect(result.singerUrl).toContain('singer');
        expect(initializeSession).toHaveBeenCalledWith(config.sessionId, true);
        expect(window.open).toHaveBeenCalledTimes(2);
    });
});
