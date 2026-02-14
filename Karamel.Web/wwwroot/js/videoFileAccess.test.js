import { describe, it, expect, beforeEach, vi } from 'vitest';

// Mock metadata module
vi.mock('../js/metadata.js', () => ({
  extractMetadata: vi.fn(async (file, relativePath, pattern) => {
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
  constructor(name, content, size = 1000) {
    this.kind = 'file';
    this.name = name;
    this._content = content;
    this._size = size;
  }

  async getFile() {
    return {
      name: this.name,
      size: this._size,
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
}

describe('fileAccess.js - Video Support', () => {
  let fileAccessModule;
  let mockDirectoryPicker;
  const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

  beforeEach(async () => {
    vi.resetModules();
    vi.spyOn(global.crypto, 'randomUUID').mockReturnValue('video-test-uuid');

    mockDirectoryPicker = vi.fn();
    global.window = {
      showDirectoryPicker: mockDirectoryPicker
    };

    consoleWarnSpy.mockClear();
    fileAccessModule = await import('../js/fileAccess.js');
  });

  describe('Video file detection', () => {
    it('should detect .mp4 files and create video songs', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Artist1 - Video1.mp4': new MockFileSystemFileHandle('Artist1 - Video1.mp4', 'fake mp4 data', 10000000), // 10MB
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).not.toBeNull();
      expect(songs).toHaveLength(1);
      expect(songs[0].artist).toBe('Artist1');
      expect(songs[0].title).toBe('Video1');
      expect(songs[0].mediaType).toBe('video');
      expect(songs[0].videoFileName).toBe('Artist1 - Video1.mp4');
      expect(songs[0].videoExtension).toBe('.mp4');
      expect(songs[0].mp3FileName).toBeNull();
      expect(songs[0].cdgFileName).toBeNull();
    });

    it('should detect .m4v files and create video songs', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Artist2 - Video2.m4v': new MockFileSystemFileHandle('Artist2 - Video2.m4v', 'fake m4v data', 15000000), // 15MB
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).not.toBeNull();
      expect(songs).toHaveLength(1);
      expect(songs[0].mediaType).toBe('video');
      expect(songs[0].videoFileName).toBe('Artist2 - Video2.m4v');
      expect(songs[0].videoExtension).toBe('.m4v');
    });

    it('should skip video files larger than 500MB', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Large - Video.mp4': new MockFileSystemFileHandle('Large - Video.mp4', 'huge video', 600 * 1024 * 1024), // 600MB
        'Small - Video.mp4': new MockFileSystemFileHandle('Small - Video.mp4', 'small video', 100 * 1024 * 1024), // 100MB
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      // Should only return the small video
      expect(songs).toHaveLength(1);
      expect(songs[0].videoFileName).toBe('Small - Video.mp4');
      
      // Should have logged a warning for the large file
      expect(consoleWarnSpy).toHaveBeenCalledWith(
        expect.stringContaining('Skipping large video')
      );
      expect(consoleWarnSpy).toHaveBeenCalledWith(
        expect.stringContaining('Large - Video.mp4')
      );
    });

    it('should handle videos without artist-title pattern', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'SingleNameVideo.mp4': new MockFileSystemFileHandle('SingleNameVideo.mp4', 'video data', 5000000),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(1);
      expect(songs[0].artist).toBe('Unknown Artist');
      expect(songs[0].title).toBe('SingleNameVideo');
      expect(songs[0].mediaType).toBe('video');
    });

    it('should scan both MP3+CDG pairs and video files together', async () => {
      const mockDirectory = new MockFileSystemDirectoryHandle('library', {
        'Song1 - Title1.mp3': new MockFileSystemFileHandle('Song1 - Title1.mp3', 'mp3 data'),
        'Song1 - Title1.cdg': new MockFileSystemFileHandle('Song1 - Title1.cdg', 'cdg data'),
        'Video1 - Title1.mp4': new MockFileSystemFileHandle('Video1 - Title1.mp4', 'mp4 data', 5000000),
      });

      mockDirectoryPicker.mockResolvedValue(mockDirectory);

      const songs = await fileAccessModule.pickLibraryDirectory();

      expect(songs).toHaveLength(2);
      
      const mp3Song = songs.find(s => s.mp3FileName === 'Song1 - Title1.mp3');
      const videoSong = songs.find(s => s.videoFileName === 'Video1 - Title1.mp4');
      
      expect(mp3Song).toBeDefined();
      expect(mp3Song.mediaType).toBeUndefined(); // Default Mp3Cdg, mediaType may not be set in JS
      expect(mp3Song.mp3FileName).toBe('Song1 - Title1.mp3');
      expect(mp3Song.cdgFileName).toBe('Song1 - Title1.cdg');
      
      expect(videoSong).toBeDefined();
      expect(videoSong.mediaType).toBe('video');
      expect(videoSong.videoFileName).toBe('Video1 - Title1.mp4');
    });
  });
});
