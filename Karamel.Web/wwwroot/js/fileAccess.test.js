import { describe, it, expect, beforeEach, vi } from 'vitest';

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
        'song1.mp3': new MockFileSystemFileHandle('song1.mp3', 'fake mp3 data'),
        'song1.cdg': new MockFileSystemFileHandle('song1.cdg', 'fake cdg data'),
        'song2.mp3': new MockFileSystemFileHandle('song2.mp3', 'fake mp3 data'),
        'song2.cdg': new MockFileSystemFileHandle('song2.cdg', 'fake cdg data'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toBeDefined();
      expect(songs).toHaveLength(2);
      expect(songs[0]).toHaveProperty('id');
      expect(songs[0]).toHaveProperty('mp3FileName', 'song1.mp3');
      expect(songs[0]).toHaveProperty('cdgFileName', 'song1.cdg');
    });

    it('should only include MP3 files that have matching CDG files', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'song1.mp3': new MockFileSystemFileHandle('song1.mp3', 'fake mp3 data'),
        'song1.cdg': new MockFileSystemFileHandle('song1.cdg', 'fake cdg data'),
        'song2.mp3': new MockFileSystemFileHandle('song2.mp3', 'fake mp3 data'),
        'song3.mp3': new MockFileSystemFileHandle('song3.mp3', 'fake mp3 data'),
        'song3.cdg': new MockFileSystemFileHandle('song3.cdg', 'fake cdg data'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      // Only song1 and song3 should be included (both have CDG)
      expect(songs).toHaveLength(2);
      expect(songs.every(s => s.cdgFileName !== null)).toBe(true);
      expect(songs.find(s => s.mp3FileName === 'song1.mp3')).toBeDefined();
      expect(songs.find(s => s.mp3FileName === 'song3.mp3')).toBeDefined();
      expect(songs.find(s => s.mp3FileName === 'song2.mp3')).toBeUndefined();
    });

    it('should recursively scan subdirectories', async () => {
      const subdirectory = new MockFileSystemDirectoryHandle('rock', {
        'rocksong.mp3': new MockFileSystemFileHandle('rocksong.mp3', 'fake mp3'),
        'rocksong.cdg': new MockFileSystemFileHandle('rocksong.cdg', 'fake cdg'),
      });

      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'song1.mp3': new MockFileSystemFileHandle('song1.mp3', 'fake mp3'),
        'song1.cdg': new MockFileSystemFileHandle('song1.cdg', 'fake cdg'),
        'rock': subdirectory,
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(2);
      
      const rootSong = songs.find(s => s.path === '');
      expect(rootSong).toBeDefined();
      expect(rootSong.mp3FileName).toBe('song1.mp3');

      const subSong = songs.find(s => s.path === 'rock');
      expect(subSong).toBeDefined();
      expect(subSong.mp3FileName).toBe('rocksong.mp3');
      expect(subSong.fullPath).toBe('rock/rocksong');
    });

    it('should handle deeply nested directories', async () => {
      const level3 = new MockFileSystemDirectoryHandle('artist', {
        'deep.mp3': new MockFileSystemFileHandle('deep.mp3', 'fake mp3'),
        'deep.cdg': new MockFileSystemFileHandle('deep.cdg', 'fake cdg'),
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
      expect(songs[0].fullPath).toBe('genre/artist/deep');
    });

    it('should return null if user cancels directory picker', async () => {
      mockDirectoryPicker.mockRejectedValue(new Error('User cancelled'));

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toBe(null);
    });

    it('should ignore non-MP3 files and MP3s without CDG', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'song1.mp3': new MockFileSystemFileHandle('song1.mp3', 'fake mp3'),
        'song1.cdg': new MockFileSystemFileHandle('song1.cdg', 'fake cdg'),
        'readme.txt': new MockFileSystemFileHandle('readme.txt', 'text'),
        'cover.jpg': new MockFileSystemFileHandle('cover.jpg', 'image'),
        'music.wav': new MockFileSystemFileHandle('music.wav', 'audio'),
        'nocdg.mp3': new MockFileSystemFileHandle('nocdg.mp3', 'mp3 without cdg'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(1);
      expect(songs[0].mp3FileName).toBe('song1.mp3');
    });

    it('should generate unique IDs for each song', async () => {
      let idCounter = 0;
      vi.spyOn(global.crypto, 'randomUUID').mockImplementation(() => `id-${++idCounter}`);

      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'song1.mp3': new MockFileSystemFileHandle('song1.mp3', 'fake'),
        'song1.cdg': new MockFileSystemFileHandle('song1.cdg', 'fake'),
        'song2.mp3': new MockFileSystemFileHandle('song2.mp3', 'fake'),
        'song2.cdg': new MockFileSystemFileHandle('song2.cdg', 'fake'),
        'song3.mp3': new MockFileSystemFileHandle('song3.mp3', 'fake'),
        'song3.cdg': new MockFileSystemFileHandle('song3.cdg', 'fake'),
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
        'song2.Cdg': new MockFileSystemFileHandle('song2.Cdg', 'fake cdg'),
        'Song1.MP3': new MockFileSystemFileHandle('Song1.MP3', 'fake mp3'),
        'Song1.CDG': new MockFileSystemFileHandle('Song1.CDG', 'fake cdg'),
        'song2.Mp3': new MockFileSystemFileHandle('song2.Mp3', 'fake mp3'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(2);
      // Note: implementation constructs filename as baseName + ".mp3" (lowercase extension)
      // So "Song1.MP3" becomes "Song1" + ".mp3" = "Song1.mp3"
      const allMp3Names = songs.map(s => s.mp3FileName);
      expect(allMp3Names).toContain('Song1.mp3'); // baseName: "Song1" from "Song1.MP3"
      expect(allMp3Names).toContain('song2.mp3'); // baseName: "song2" from "song2.Mp3"
      
      // Verify CDG matching works case-insensitively (Song1.MP3 matches Song1.CDG)
      const song1 = songs.find(s => s.mp3FileName === 'Song1.mp3');
      expect(song1).toBeDefined();
      expect(song1.cdgFileName).toBe('Song1.cdg'); // baseName + ".cdg"
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
