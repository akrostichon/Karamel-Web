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
  validatePattern: vi.fn((pattern) => pattern || '%artist - %title'),
  clearID3FailureLog: vi.fn(),
  flushID3FailureLog: vi.fn()
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

    // Prevent extractDuration from hanging: make URL.createObjectURL throw so the
    // try/catch in extractDuration returns 0 immediately. Tests that need real duration
    // extraction override this in their own beforeEach.
    global.URL.createObjectURL = vi.fn(() => { throw new Error('URL.createObjectURL not supported in this test context'); });
    global.URL.revokeObjectURL = vi.fn();

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

    it('should detect and categorize .mp4 video files', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Artist - Song1.mp4': new MockFileSystemFileHandle('Artist - Song1.mp4', 'fake video data'),
        'Artist - Song2.mp3': new MockFileSystemFileHandle('Artist - Song2.mp3', 'fake mp3'),
        'Artist - Song2.cdg': new MockFileSystemFileHandle('Artist - Song2.cdg', 'fake cdg'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(2);

      // Find the video song
      const videoSong = songs.find(s => s.mediaType === 'video');
      expect(videoSong).toBeDefined();
      expect(videoSong.videoFileName).toBe('Artist - Song1.mp4');
      expect(videoSong.videoExtension).toBe('.mp4');
      expect(videoSong.mp3FileName).toBe(null);
      expect(videoSong.cdgFileName).toBe(null);
      expect(videoSong.artist).toBe('Artist');
      expect(videoSong.title).toBe('Song1');

      // Verify MP3+CDG song still works
      const mp3Song = songs.find(s => s.mp3FileName === 'Artist - Song2.mp3');
      expect(mp3Song).toBeDefined();
      expect(mp3Song.mediaType).toBeUndefined(); // MP3+CDG songs don't have mediaType
    });

    it('should detect and categorize .m4v video files', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Video Artist - Video Title.m4v': new MockFileSystemFileHandle('Video Artist - Video Title.m4v', 'fake video data'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(1);
      expect(songs[0].mediaType).toBe('video');
      expect(songs[0].videoFileName).toBe('Video Artist - Video Title.m4v');
      expect(songs[0].videoExtension).toBe('.m4v');
      expect(songs[0].artist).toBe('Video Artist');
      expect(songs[0].title).toBe('Video Title');
    });

    it('should skip video files larger than 500MB with console warning', async () => {
      // Create a mock file that reports size > 500MB
      const largeMockFile = {
        name: 'Large Video.mp4',
        size: 600 * 1024 * 1024, // 600MB
        async arrayBuffer() {
          return new ArrayBuffer(0);
        }
      };

      const largeVideoHandle = new MockFileSystemFileHandle('Large Video.mp4', 'fake');
      largeVideoHandle.getFile = vi.fn(async () => largeMockFile);

      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Large Video.mp4': largeVideoHandle,
        'Normal Artist - Normal Song.mp3': new MockFileSystemFileHandle('Normal Artist - Normal Song.mp3', 'fake mp3'),
        'Normal Artist - Normal Song.cdg': new MockFileSystemFileHandle('Normal Artist - Normal Song.cdg', 'fake cdg'),
      });

      // Spy on console.warn to verify warning is logged
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      // Should only include the MP3+CDG song, not the large video
      expect(songs).toHaveLength(1);
      expect(songs[0].mp3FileName).toBe('Normal Artist - Normal Song.mp3');

      // Verify warning was logged (single string parameter with both size and filename)
      expect(consoleWarnSpy).toHaveBeenCalledWith(
        expect.stringMatching(/Skipping large video.*600\.0MB.*Large Video\.mp4/)
      );

      consoleWarnSpy.mockRestore();
    });

    it('should use filename parsing fallback for video files', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'NoSeparator.mp4': new MockFileSystemFileHandle('NoSeparator.mp4', 'fake video'),
        'With Separator - Title.mp4': new MockFileSystemFileHandle('With Separator - Title.mp4', 'fake video'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(2);

      // Test fallback (no separator) - should use Unknown Artist
      const noSeparatorSong = songs.find(s => s.videoFileName === 'NoSeparator.mp4');
      expect(noSeparatorSong).toBeDefined();
      expect(noSeparatorSong.mediaType).toBe('video');
      expect(noSeparatorSong.artist).toBe('Unknown Artist');
      expect(noSeparatorSong.title).toBe('NoSeparator');

      // Test with separator - should parse correctly
      const withSeparatorSong = songs.find(s => s.videoFileName === 'With Separator - Title.mp4');
      expect(withSeparatorSong).toBeDefined();
      expect(withSeparatorSong.artist).toBe('With Separator');
      expect(withSeparatorSong.title).toBe('Title');
    });

    it('should process video files in subdirectories', async () => {
      const subdirectory = new MockFileSystemDirectoryHandle('videos', {
        'Subdir Artist - Subdir Song.mp4': new MockFileSystemFileHandle('Subdir Artist - Subdir Song.mp4', 'fake video'),
      });

      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Root Artist - Root Song.mp4': new MockFileSystemFileHandle('Root Artist - Root Song.mp4', 'fake video'),
        'videos': subdirectory,
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(2);

      // Root video
      const rootVideo = songs.find(s => s.path === '');
      expect(rootVideo).toBeDefined();
      expect(rootVideo.mediaType).toBe('video');
      expect(rootVideo.videoFileName).toBe('Root Artist - Root Song.mp4');
      expect(rootVideo.fullPath).toBe('Root Artist - Root Song');

      // Subdirectory video
      const subdirVideo = songs.find(s => s.path === 'videos');
      expect(subdirVideo).toBeDefined();
      expect(subdirVideo.mediaType).toBe('video');
      expect(subdirVideo.videoFileName).toBe('Subdir Artist - Subdir Song.mp4');
      expect(subdirVideo.fullPath).toBe('videos/Subdir Artist - Subdir Song');
    });

    it('should handle mixed MP3+CDG and video files in same directory', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Artist1 - MP3Song.mp3': new MockFileSystemFileHandle('Artist1 - MP3Song.mp3', 'fake mp3'),
        'Artist1 - MP3Song.cdg': new MockFileSystemFileHandle('Artist1 - MP3Song.cdg', 'fake cdg'),
        'Artist2 - VideoSong.mp4': new MockFileSystemFileHandle('Artist2 - VideoSong.mp4', 'fake video'),
        'Artist3 - AnotherVideo.m4v': new MockFileSystemFileHandle('Artist3 - AnotherVideo.m4v', 'fake video'),
        'Artist4 - SecondMP3.mp3': new MockFileSystemFileHandle('Artist4 - SecondMP3.mp3', 'fake mp3'),
        'Artist4 - SecondMP3.cdg': new MockFileSystemFileHandle('Artist4 - SecondMP3.cdg', 'fake cdg'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(4);

      const videoSongs = songs.filter(s => s.mediaType === 'video');
      const mp3Songs = songs.filter(s => s.mp3FileName && s.cdgFileName);

      expect(videoSongs).toHaveLength(2);
      expect(mp3Songs).toHaveLength(2);

      // Verify video songs
      expect(videoSongs[0].videoFileName).toMatch(/\.mp4|\.m4v/);
      expect(videoSongs[1].videoFileName).toMatch(/\.mp4|\.m4v/);

      // Verify MP3 songs
      expect(mp3Songs[0].mp3FileName).toContain('.mp3');
      expect(mp3Songs[0].cdgFileName).toContain('.cdg');
    });
  });

  describe('loadSongFiles', () => {
    beforeEach(async () => {
      // Restore URL.createObjectURL so byteStore.createObjectUrl works during loadSongFiles
      global.URL.createObjectURL = vi.fn(() => 'blob:test-url');
      global.URL.revokeObjectURL = vi.fn();

      // Make media elements fire 'error' immediately so extractDuration returns 0 quickly
      const _origCreate = document.createElement.bind(document);
      vi.spyOn(document, 'createElement').mockImplementation((tag) => {
        if (tag === 'audio' || tag === 'video') {
          const el = { duration: NaN, _h: {}, addEventListener(ev, fn) { this._h[ev] = fn; }, set src(_) { Promise.resolve().then(() => this._h.error?.()); } };
          return el;
        }
        return _origCreate(tag);
      });

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

  describe('loadVideoFile', () => {
    beforeEach(async () => {
      // Mock URL.createObjectURL
      global.URL.createObjectURL = vi.fn((file) => `blob:http://localhost/${file.name}`);
      global.URL.revokeObjectURL = vi.fn();

      // Make media elements fire 'error' immediately so extractDuration returns 0 quickly
      // (real media loading is not supported in happy-dom)
      const _origCreate = document.createElement.bind(document);
      vi.spyOn(document, 'createElement').mockImplementation((tag) => {
        if (tag === 'audio' || tag === 'video') {
          const el = { duration: NaN, _h: {}, addEventListener(ev, fn) { this._h[ev] = fn; }, set src(_) { Promise.resolve().then(() => this._h.error?.()); } };
          return el;
        }
        return _origCreate(tag);
      });
      
      // Set up a mock directory with video files
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'test-video.mp4': new MockFileSystemFileHandle('test-video.mp4', 'video content'),
        'another-video.m4v': new MockFileSystemFileHandle('another-video.m4v', 'video content'),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);
      await fileAccessModule.pickLibraryDirectory();
    });

    it('should return object URL for valid video file', async () => {
      const videoUrl = await fileAccessModule.loadVideoFile('', 'test-video.mp4');

      expect(videoUrl).toBeDefined();
      expect(typeof videoUrl).toBe('string');
      expect(videoUrl).toMatch(/^blob:/);
      expect(global.URL.createObjectURL).toHaveBeenCalled();
    });

    it('should throw error when library directory not selected', async () => {
      // Reset module to clear directory handle
      vi.resetModules();
      global.URL.createObjectURL = vi.fn((file) => `blob:http://localhost/${file.name}`);
      const freshModule = await import('../js/fileAccess.js');

      await expect(
        freshModule.loadVideoFile('', 'test-video.mp4')
      ).rejects.toThrow('No library directory selected');
    });

    it('should throw error when video file not found', async () => {
      await expect(
        fileAccessModule.loadVideoFile('', 'nonexistent.mp4')
      ).rejects.toThrow('Video file not found: nonexistent.mp4');
    });

    it('should handle subdirectory paths correctly', async () => {
      const subdirectory = new MockFileSystemDirectoryHandle('videos', {
        'subfolder-video.mp4': new MockFileSystemFileHandle('subfolder-video.mp4', 'video'),
      });

      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'videos': subdirectory,
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);
      await fileAccessModule.pickLibraryDirectory();

      const videoUrl = await fileAccessModule.loadVideoFile('videos', 'subfolder-video.mp4');

      expect(videoUrl).toBeDefined();
      expect(videoUrl).toMatch(/^blob:/);
    });

    it('should return valid blob URL format', async () => {
      const videoUrl = await fileAccessModule.loadVideoFile('', 'test-video.mp4');

      // Verify URL format
      expect(videoUrl.startsWith('blob:')).toBe(true);
      expect(videoUrl).toContain('test-video.mp4');
    });
  });
});

// ─────────────────────────────────────────────────────────────
// extractDuration behaviour (tested indirectly via pickLibraryDirectory)
// ─────────────────────────────────────────────────────────────

describe('fileAccess.js - extractDuration behaviour', () => {
  let durationModule;
  let mockDirPicker;
  const originalCreateElement = document.createElement.bind(document);

  /** Build a fake media element that fires `fireEvent` after src is set. */
  function makeMockMediaEl(duration, fireEvent = 'loadedmetadata') {
    const el = {
      duration,
      _handlers: {},
      addEventListener(ev, fn) { this._handlers[ev] = fn; },
      set src(_url) {
        // Fire the event asynchronously (microtask) to allow addEventListener to register first
        Promise.resolve().then(() => this._handlers[fireEvent]?.());
      },
    };
    return el;
  }

  beforeEach(async () => {
    vi.resetModules();
    vi.spyOn(global.crypto, 'randomUUID').mockReturnValue('aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee');

    mockDirPicker = vi.fn();
    global.window = { showDirectoryPicker: mockDirPicker };

    // Provide a working URL.createObjectURL so the element is created
    global.URL.createObjectURL = vi.fn(() => 'blob:test-duration');
    global.URL.revokeObjectURL = vi.fn();

    durationModule = await import('../js/fileAccess.js');
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function setupDirectory(entries) {
    const dir = new MockFileSystemDirectoryHandle('lib', entries);
    mockDirPicker.mockResolvedValue(dir);
  }

  it('returns correct durationSeconds for a fake audio song (215 s)', async () => {
    vi.spyOn(document, 'createElement').mockImplementation((tag) => {
      if (tag === 'audio') return makeMockMediaEl(215, 'loadedmetadata');
      return originalCreateElement(tag);
    });

    setupDirectory({
      'Artist - Song.mp3': new MockFileSystemFileHandle('Artist - Song.mp3', 'fake'),
      'Artist - Song.cdg': new MockFileSystemFileHandle('Artist - Song.cdg', 'fake'),
    });

    const songs = await durationModule.pickLibraryDirectory();
    expect(songs).toHaveLength(1);
    expect(songs[0].durationSeconds).toBe(215);
  });

  it('returns 0 durationSeconds when the error event fires', async () => {
    vi.spyOn(document, 'createElement').mockImplementation((tag) => {
      if (tag === 'audio') return makeMockMediaEl(NaN, 'error');
      return originalCreateElement(tag);
    });

    setupDirectory({
      'Artist - Song.mp3': new MockFileSystemFileHandle('Artist - Song.mp3', 'fake'),
      'Artist - Song.cdg': new MockFileSystemFileHandle('Artist - Song.cdg', 'fake'),
    });

    const songs = await durationModule.pickLibraryDirectory();
    expect(songs).toHaveLength(1);
    expect(songs[0].durationSeconds).toBe(0);
  });

  it('returns 0 durationSeconds when el.duration is NaN', async () => {
    vi.spyOn(document, 'createElement').mockImplementation((tag) => {
      if (tag === 'audio') return makeMockMediaEl(NaN, 'loadedmetadata');
      return originalCreateElement(tag);
    });

    setupDirectory({
      'Artist - Song.mp3': new MockFileSystemFileHandle('Artist - Song.mp3', 'fake'),
      'Artist - Song.cdg': new MockFileSystemFileHandle('Artist - Song.cdg', 'fake'),
    });

    const songs = await durationModule.pickLibraryDirectory();
    expect(songs).toHaveLength(1);
    expect(songs[0].durationSeconds).toBe(0);
  });
});
