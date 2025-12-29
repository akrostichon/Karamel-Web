import { describe, it, expect, beforeEach, vi } from 'vitest';

// Mock metadata module to avoid jsmediatags dependency in tests
vi.mock('../js/metadata.js', () => ({
  extractMetadata: vi.fn(async (file, relativePath, pattern) => {
    // Simple filename parsing for tests - extract artist/title from filename
    const basename = relativePath.replace(/\.[^/.]+$/, '');
    const nameOnly = basename.split('/').pop().split('\\').pop();
    
    if (nameOnly.includes(' - ')) {
      const [artist, title] = nameOnly.split(' - ');
      return { artist: artist.trim(), title: title.trim() };
    }
    
    return { artist: 'Unknown Artist', title: nameOnly || 'Unknown Title' };
  }),
  validatePattern: vi.fn((pattern) => pattern || '%artist - %title')
}));

// Mock File System Access API
class MockFileSystemFileHandle {
  constructor(name, content) {
    this.kind = 'file';
    this.name = name;
    this._content = content;
  }

  async getFile() {
    return {
      name: this.name,
      async arrayBuffer() {
        return new TextEncoder().encode(this._content).buffer;
      }
    };
  }
}

class MockFileSystemDirectoryHandle {
  constructor(name, entries = {}) {
    this.kind = 'directory';
    this.name = name;
    this._entries = entries;
  }

  async *values() {
    for (const entry of Object.values(this._entries)) {
      yield entry;
    }
  }

  async getFileHandle(name) {
    const entry = this._entries[name];
    if (!entry || entry.kind !== 'file') {
      throw new Error(`File not found: ${name}`);
    }
    return entry;
  }

  async getDirectoryHandle(name) {
    const entry = this._entries[name];
    if (!entry || entry.kind !== 'directory') {
      throw new Error(`Directory not found: ${name}`);
    }
    return entry;
  }
}

describe('fileAccess.js - Directory Scanning', () => {
  let fileAccessModule;
  let mockDirectoryPicker;

  beforeEach(async () => {
    // Reset module before each test
    vi.resetModules();
    
    // Mock crypto.randomUUID (use vi.spyOn instead of replacing global)
    vi.spyOn(global.crypto, 'randomUUID').mockReturnValue('12345678-1234-1234-1234-123456789abc');

    // Mock window.showDirectoryPicker
    mockDirectoryPicker = vi.fn();
    global.window = {
      showDirectoryPicker: mockDirectoryPicker
    };

    // Import module after mocking
    fileAccessModule = await import('../js/fileAccess.js');
  });

  describe('pickLibraryDirectory', () => {
    it('should scan directory and find MP3/CDG pairs', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Artist1 - Song1.mp3': new MockFileSystemFileHandle('Artist1 - Song1.mp3', 'fake mp3 data'),
        'Artist1 - Song1.cdg': new MockFileSystemFileHandle('Artist1 - Song1.cdg', 'fake cdg data'),
        'Artist2 - Song2.mp3': new MockFileSystemFileHandle('Artist2 - Song2.mp3', 'fake mp3 data'),
        'Artist2 - Song2.cdg': new MockFileSystemFileHandle('Artist2 - Song2.cdg', 'fake cdg data'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toBeDefined();
      expect(songs).toHaveLength(2);
      expect(songs[0]).toHaveProperty('id');
      expect(songs[0]).toHaveProperty('artist', 'Artist1');
      expect(songs[0]).toHaveProperty('title', 'Song1');
      expect(songs[0]).toHaveProperty('mp3FileName', 'Artist1 - Song1.mp3');
      expect(songs[0]).toHaveProperty('cdgFileName', 'Artist1 - Song1.cdg');
    });

    it('should only include MP3 files that have matching CDG files', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'A1 - song1.mp3': new MockFileSystemFileHandle('A1 - song1.mp3', 'fake mp3 data'),
        'A1 - song1.cdg': new MockFileSystemFileHandle('A1 - song1.cdg', 'fake cdg data'),
        'A2 - song2.mp3': new MockFileSystemFileHandle('A2 - song2.mp3', 'fake mp3 data'),
        'A3 - song3.mp3': new MockFileSystemFileHandle('A3 - song3.mp3', 'fake mp3 data'),
        'A3 - song3.cdg': new MockFileSystemFileHandle('A3 - song3.cdg', 'fake cdg data'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      // Only song1 and song3 should be included (both have CDG)
      expect(songs).toHaveLength(2);
      expect(songs.every(s => s.cdgFileName !== null)).toBe(true);
      expect(songs.find(s => s.mp3FileName === 'A1 - song1.mp3')).toBeDefined();
      expect(songs.find(s => s.mp3FileName === 'A3 - song3.mp3')).toBeDefined();
      expect(songs.find(s => s.mp3FileName === 'A2 - song2.mp3')).toBeUndefined();
    });

    it('should recursively scan subdirectories', async () => {
      const subdirectory = new MockFileSystemDirectoryHandle('rock', {
        'Rock Artist - rocksong.mp3': new MockFileSystemFileHandle('Rock Artist - rocksong.mp3', 'fake mp3'),
        'Rock Artist - rocksong.cdg': new MockFileSystemFileHandle('Rock Artist - rocksong.cdg', 'fake cdg'),
      });

      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Artist - song1.mp3': new MockFileSystemFileHandle('Artist - song1.mp3', 'fake mp3'),
        'Artist - song1.cdg': new MockFileSystemFileHandle('Artist - song1.cdg', 'fake cdg'),
        'rock': subdirectory,
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(2);
      
      const rootSong = songs.find(s => s.path === '');
      expect(rootSong).toBeDefined();
      expect(rootSong.mp3FileName).toBe('Artist - song1.mp3');
      expect(rootSong.artist).toBe('Artist');
      expect(rootSong.title).toBe('song1');

      const subSong = songs.find(s => s.path === 'rock');
      expect(subSong).toBeDefined();
      expect(subSong.mp3FileName).toBe('Rock Artist - rocksong.mp3');
      expect(subSong.fullPath).toBe('rock/Rock Artist - rocksong');
      expect(subSong.artist).toBe('Rock Artist');
      expect(subSong.title).toBe('rocksong');
    });

    it('should handle deeply nested directories', async () => {
      const level3 = new MockFileSystemDirectoryHandle('artist', {
        'Deep Artist - deep.mp3': new MockFileSystemFileHandle('Deep Artist - deep.mp3', 'fake mp3'),
        'Deep Artist - deep.cdg': new MockFileSystemFileHandle('Deep Artist - deep.cdg', 'fake cdg'),
      });

      const level2 = new MockFileSystemDirectoryHandle('genre', {
        'artist': level3,
      });

      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'genre': level2,
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(1);
      expect(songs[0].path).toBe('genre/artist');
      expect(songs[0].fullPath).toBe('genre/artist/Deep Artist - deep');
      expect(songs[0].artist).toBe('Deep Artist');
      expect(songs[0].title).toBe('deep');
    });

    it('should return null if user cancels directory picker', async () => {
      mockDirectoryPicker.mockRejectedValue(new Error('User cancelled'));

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toBe(null);
    });

    it('should ignore non-MP3 files and MP3s without CDG', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Artist - song1.mp3': new MockFileSystemFileHandle('Artist - song1.mp3', 'fake mp3'),
        'Artist - song1.cdg': new MockFileSystemFileHandle('Artist - song1.cdg', 'fake cdg'),
        'readme.txt': new MockFileSystemFileHandle('readme.txt', 'text'),
        'cover.jpg': new MockFileSystemFileHandle('cover.jpg', 'image'),
        'music.wav': new MockFileSystemFileHandle('music.wav', 'audio'),
        'Artist - nocdg.mp3': new MockFileSystemFileHandle('Artist - nocdg.mp3', 'mp3 without cdg'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(1);
      expect(songs[0].mp3FileName).toBe('Artist - song1.mp3');
    });

    it('should generate unique IDs for each song', async () => {
      let idCounter = 0;
      vi.spyOn(global.crypto, 'randomUUID').mockImplementation(() => `id-${++idCounter}`);

      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'A1 - song1.mp3': new MockFileSystemFileHandle('A1 - song1.mp3', 'fake'),
        'A1 - song1.cdg': new MockFileSystemFileHandle('A1 - song1.cdg', 'fake'),
        'A2 - song2.mp3': new MockFileSystemFileHandle('A2 - song2.mp3', 'fake'),
        'A2 - song2.cdg': new MockFileSystemFileHandle('A2 - song2.cdg', 'fake'),
        'A3 - song3.mp3': new MockFileSystemFileHandle('A3 - song3.mp3', 'fake'),
        'A3 - song3.cdg': new MockFileSystemFileHandle('A3 - song3.cdg', 'fake'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(3);
      expect(songs[0].id).toBe('id-1');
      expect(songs[1].id).toBe('id-2');
      expect(songs[2].id).toBe('id-3');
    });

    it('should handle case-insensitive file extensions', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Artist - song2.Cdg': new MockFileSystemFileHandle('Artist - song2.Cdg', 'fake cdg'),
        'Artist - Song1.MP3': new MockFileSystemFileHandle('Artist - Song1.MP3', 'fake mp3'),
        'Artist - Song1.CDG': new MockFileSystemFileHandle('Artist - Song1.CDG', 'fake cdg'),
        'Artist - song2.Mp3': new MockFileSystemFileHandle('Artist - song2.Mp3', 'fake mp3'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(2);
      // Note: implementation constructs filename as baseName + ".mp3" (lowercase extension)
      // So "Artist - Song1.MP3" becomes "Artist - Song1" + ".mp3" = "Artist - Song1.mp3"
      const allMp3Names = songs.map(s => s.mp3FileName);
      expect(allMp3Names).toContain('Artist - Song1.mp3'); // baseName: "Artist - Song1" from "Artist - Song1.MP3"
      expect(allMp3Names).toContain('Artist - song2.mp3'); // baseName: "Artist - song2" from "Artist - song2.Mp3"
      
      // Verify CDG matching works case-insensitively (Artist - Song1.MP3 matches Artist - Song1.CDG)
      const song1 = songs.find(s => s.mp3FileName === 'Artist - Song1.mp3');
      expect(song1).toBeDefined();
      expect(song1.cdgFileName).toBe('Artist - Song1.cdg'); // baseName + ".cdg"
    });
  });

  describe('loadSongFiles', () => {
    beforeEach(async () => {
      // Set up a mock directory structure first
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'test.mp3': new MockFileSystemFileHandle('test.mp3', 'mp3 content'),
        'test.cdg': new MockFileSystemFileHandle('test.cdg', 'cdg content'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);
      await fileAccessModule.pickLibraryDirectory();
    });

    it('should load MP3 and CDG files from root path', async () => {
      const result = await fileAccessModule.loadSongFiles('', 'test.mp3', 'test.cdg');

      expect(result).toBeDefined();
      expect(result.mp3Data).toBeInstanceOf(Uint8Array);
      expect(result.cdgData).toBeInstanceOf(Uint8Array);
    });

    it('should throw error if CDG file is missing', async () => {
      await expect(
        fileAccessModule.loadSongFiles('', 'test.mp3', 'nonexistent.cdg')
      ).rejects.toThrow();
    });

    it('should load files from subdirectory path', async () => {
      const subdirectory = new MockFileSystemDirectoryHandle('artist', {
        'song.mp3': new MockFileSystemFileHandle('song.mp3', 'mp3'),
        'song.cdg': new MockFileSystemFileHandle('song.cdg', 'cdg'),
      });

      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'artist': subdirectory,
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);
      await fileAccessModule.pickLibraryDirectory();

      const result = await fileAccessModule.loadSongFiles('artist', 'song.mp3', 'song.cdg');

      expect(result.mp3Data).toBeInstanceOf(Uint8Array);
      expect(result.cdgData).toBeInstanceOf(Uint8Array);
    });

    it('should throw error if no library directory selected', async () => {
      // Reset module to clear directory handle
      vi.resetModules();
      const freshModule = await import('../js/fileAccess.js');

      await expect(
        freshModule.loadSongFiles('', 'test.mp3', 'test.cdg')
      ).rejects.toThrow('No library directory selected');
    });
  });

  describe('getLibraryDirectoryHandle', () => {
    it('should return null if no directory selected', () => {
      const handle = fileAccessModule.getLibraryDirectoryHandle();
      expect(handle).toBe(null);
    });

    it('should return directory handle after selection', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {});
      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      await fileAccessModule.pickLibraryDirectory();
      const handle = fileAccessModule.getLibraryDirectoryHandle();

      expect(handle).toBe(mockDirectory);
      expect(handle.name).toBe('library');
    });
  });
});
